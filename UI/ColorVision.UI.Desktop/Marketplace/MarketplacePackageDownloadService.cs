using ColorVision.Common.Utilities;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.UI.Desktop.Marketplace
{
    public sealed class MarketplacePackageRequest
    {
        public required string PluginId { get; init; }
        public required string Version { get; init; }
        public string? ExpectedHash { get; init; }
    }

    public sealed class MarketplacePackageDownloadService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplacePackageDownloadService));
        private static readonly HttpClient LegacyHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly object Locker = new();
        private static MarketplacePackageDownloadService? _instance;

        public static MarketplacePackageDownloadService GetInstance()
        {
            lock (Locker)
            {
                return _instance ??= new MarketplacePackageDownloadService();
            }
        }

        private MarketplaceClient Client => MarketplaceClient.GetInstance();

        public string DownloadDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

        public async Task<string?> ResolveLatestVersionAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return null;

            try
            {
                string? version = await Client.GetLatestVersionAsync(pluginId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(version))
                    return version.Trim();
            }
            catch (Exception ex)
            {
                log.Debug($"ResolveLatestVersionAsync API failed for {pluginId}: {ex.Message}");
            }

            string latestReleaseUrl = MarketplaceConfig.BuildLegacyPluginUrl($"{pluginId}/LATEST_RELEASE");
            using var request = new HttpRequestMessage(HttpMethod.Get, latestReleaseUrl);
            string? authorization = DownloadFileConfig.Instance.Authorization;
            if (!string.IsNullOrWhiteSpace(authorization))
            {
                byte[] authBytes = Encoding.ASCII.GetBytes(authorization);
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(authBytes));
            }

            try
            {
                using HttpResponseMessage response = await LegacyHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
                response.EnsureSuccessStatusCode();
                string legacyVersion = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                return string.IsNullOrWhiteSpace(legacyVersion) ? null : legacyVersion.Trim();
            }
            catch (Exception ex)
            {
                log.Debug($"ResolveLatestVersionAsync legacy fallback failed for {pluginId}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> ResolveExpectedHashAsync(string pluginId, string version, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pluginId) || string.IsNullOrWhiteSpace(version))
                return null;

            try
            {
                MarketplacePluginDetail? detail = await Client.GetPluginDetailAsync(pluginId, cancellationToken).ConfigureAwait(false);
                return detail?.Versions
                    .Concat(detail.ArchivedVersions)
                    .FirstOrDefault(item => string.Equals(item.Version, version, StringComparison.OrdinalIgnoreCase))
                    ?.FileHash;
            }
            catch (Exception ex)
            {
                log.Debug($"ResolveExpectedHashAsync failed for {pluginId} v{version}: {ex.Message}");
                return null;
            }
        }

        public async Task<string?> EnsurePackageAvailableAsync(MarketplacePackageRequest request, bool showFailureDialog = true, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(request);
            Directory.CreateDirectory(DownloadDirectory);

            string? existingFile = MarketplaceClient.GetExistingFileIfValid(DownloadDirectory, request.PluginId, request.Version, request.ExpectedHash);
            if (existingFile != null)
            {
                log.Info($"Marketplace package cache hit: {request.PluginId} v{request.Version} -> {existingFile}");
                return existingFile;
            }

            RunOnUIThread(DownloadWindow.ShowInstance);
            return await StartDownloadAsync(request, showFailureDialog, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> EnsurePackagesAvailableAsync(IEnumerable<MarketplacePackageRequest> requests, bool showFailureDialog = false, CancellationToken cancellationToken = default)
        {
            List<MarketplacePackageRequest> distinctRequests = requests
                .Where(item => !string.IsNullOrWhiteSpace(item.PluginId) && !string.IsNullOrWhiteSpace(item.Version))
                .GroupBy(item => $"{item.PluginId}|{item.Version}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();

            if (distinctRequests.Count == 0)
                return Array.Empty<string>();

            Directory.CreateDirectory(DownloadDirectory);

            ConcurrentBag<string> packagePaths = new();
            List<MarketplacePackageRequest> missingRequests = new();
            foreach (MarketplacePackageRequest request in distinctRequests)
            {
                string? existingFile = MarketplaceClient.GetExistingFileIfValid(DownloadDirectory, request.PluginId, request.Version, request.ExpectedHash);
                if (existingFile != null)
                {
                    packagePaths.Add(existingFile);
                }
                else
                {
                    missingRequests.Add(request);
                }
            }

            if (missingRequests.Count > 0)
            {
                RunOnUIThread(DownloadWindow.ShowInstance);
                Task<string?>[] downloadTasks = missingRequests
                    .Select(item => StartDownloadAsync(item, showFailureDialog, cancellationToken))
                    .ToArray();

                string?[] downloadedPaths = await Task.WhenAll(downloadTasks).ConfigureAwait(false);
                foreach (string? downloadedPath in downloadedPaths.Where(path => !string.IsNullOrWhiteSpace(path)))
                {
                    packagePaths.Add(downloadedPath!);
                }
            }

            return packagePaths
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        public async Task<bool> OpenDownloadedPackageFolderAsync(MarketplacePackageRequest request, CancellationToken cancellationToken = default)
        {
            string? packagePath = await EnsurePackageAvailableAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(packagePath))
                return false;

            string? folderPath = Path.GetDirectoryName(packagePath);
            RunOnUIThread(() => PlatformHelper.OpenFolder(folderPath));
            return true;
        }

        public async Task<bool> InstallPackageAsync(MarketplacePackageRequest request, string? restartArguments = null, CancellationToken cancellationToken = default)
        {
            string? packagePath = await EnsurePackageAvailableAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(packagePath))
                return false;

            RunOnUIThread(() => PluginUpdater.UpdatePluginWithRestartArguments(restartArguments, packagePath));
            return true;
        }

        public void StartBackgroundBatchInstall(IEnumerable<MarketplacePackageRequest> requests, string? restartArguments = null, Action? onEmpty = null)
        {
            _ = StartBackgroundBatchInstallCoreAsync(requests, restartArguments, onEmpty);
        }

        private async Task StartBackgroundBatchInstallCoreAsync(IEnumerable<MarketplacePackageRequest> requests, string? restartArguments, Action? onEmpty)
        {
            try
            {
                IReadOnlyList<string> packagePaths = await EnsurePackagesAvailableAsync(requests).ConfigureAwait(false);
                if (packagePaths.Count == 0)
                {
                    RunOnUIThread(() => onEmpty?.Invoke());
                    return;
                }

                RunOnUIThread(() => PluginUpdater.UpdatePluginWithRestartArguments(restartArguments, packagePaths.ToArray()));
            }
            catch (Exception ex)
            {
                log.Error($"StartBackgroundBatchInstallCoreAsync failed: {ex.Message}", ex);
                RunOnUIThread(() => onEmpty?.Invoke());
            }
        }

        private async Task<string?> StartDownloadAsync(MarketplacePackageRequest request, bool showFailureDialog, CancellationToken cancellationToken)
        {
            string downloadUrl = Client.GetDownloadUrl(request.PluginId, request.Version);
            string fileName = $"{request.PluginId}-{request.Version}.cvxp";
            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenRegistration registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));

            Aria2cDownloadManager.GetInstance().AddDownload(downloadUrl, DownloadDirectory, DownloadFileConfig.Instance.Authorization, task =>
            {
                if (task.Status != DownloadStatus.Completed)
                {
                    log.Error($"Marketplace package download failed for {request.PluginId} v{request.Version}: {task.ErrorMessage}");
                    if (showFailureDialog)
                    {
                        RunOnUIThread(() =>
                            MessageBox.Show(Application.Current.GetActiveWindow(), task.ErrorMessage ?? Resources.MarketplaceLoadFailed, Resources.PluginManagerWindow, MessageBoxButton.OK, MessageBoxImage.Warning));
                    }

                    completionSource.TrySetResult(null);
                    return;
                }

                if (!MarketplaceClient.VerifyFileHash(task.SavePath, request.ExpectedHash))
                {
                    log.Error($"Marketplace package hash verification failed for {request.PluginId} v{request.Version}.");
                    if (showFailureDialog)
                    {
                        RunOnUIThread(() =>
                            MessageBox.Show(Application.Current.GetActiveWindow(), $"下载文件校验失败: {request.PluginId} v{request.Version}", Resources.PluginManagerWindow, MessageBoxButton.OK, MessageBoxImage.Error));
                    }

                    completionSource.TrySetResult(null);
                    return;
                }

                completionSource.TrySetResult(task.SavePath);
            }, fileName);

            return await completionSource.Task.ConfigureAwait(false);
        }

        private static void RunOnUIThread(Action action)
        {
            if (Application.Current?.Dispatcher == null)
            {
                action();
                return;
            }

            if (Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Application.Current.Dispatcher.Invoke(action);
        }
    }
}
