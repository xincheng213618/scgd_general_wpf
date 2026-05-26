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

    public interface IMarketplacePackageClient
    {
        Task<string?> GetLatestVersionAsync(string pluginId, CancellationToken cancellationToken = default);
        Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId, CancellationToken cancellationToken = default);
        string GetDownloadUrl(string pluginId, string version);
        bool VerifyFileHash(string filePath, string? expectedHash);
        string? GetExistingFileIfValid(string downloadDirectory, string pluginId, string version, string? expectedHash);
    }

    public interface IMarketplacePackageDownloader
    {
        void AddDownload(string url, string downloadDirectory, string? authorization, Action<DownloadTask> onCompleted, string fileName);
    }

    public interface IMarketplacePackageInstaller
    {
        void Install(string? restartArguments, params string[] packagePaths);
    }

    public interface IMarketplacePackageUi
    {
        string DownloadDirectory { get; }
        string? Authorization { get; }
        void ShowDownloadWindow();
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);
        void OpenFolder(string? folderPath);
    }

    internal sealed class MarketplacePackageClientAdapter : IMarketplacePackageClient
    {
        private readonly MarketplaceClient _client = MarketplaceClient.GetInstance();

        public Task<string?> GetLatestVersionAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            return _client.GetLatestVersionAsync(pluginId, cancellationToken);
        }

        public Task<MarketplacePluginDetail?> GetPluginDetailAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            return _client.GetPluginDetailAsync(pluginId, cancellationToken);
        }

        public string GetDownloadUrl(string pluginId, string version)
        {
            return _client.GetDownloadUrl(pluginId, version);
        }

        public bool VerifyFileHash(string filePath, string? expectedHash)
        {
            return MarketplaceClient.VerifyFileHash(filePath, expectedHash);
        }

        public string? GetExistingFileIfValid(string downloadDirectory, string pluginId, string version, string? expectedHash)
        {
            return MarketplaceClient.GetExistingFileIfValid(downloadDirectory, pluginId, version, expectedHash);
        }
    }

    internal sealed class MarketplacePackageDownloaderAdapter : IMarketplacePackageDownloader
    {
        public void AddDownload(string url, string downloadDirectory, string? authorization, Action<DownloadTask> onCompleted, string fileName)
        {
            Aria2cDownloadManager.GetInstance().AddDownload(url, downloadDirectory, authorization, onCompleted, fileName);
        }
    }

    internal sealed class MarketplacePackageInstallerAdapter : IMarketplacePackageInstaller
    {
        public void Install(string? restartArguments, params string[] packagePaths)
        {
            PluginUpdater.UpdatePluginWithRestartArguments(restartArguments, packagePaths);
        }
    }

    internal sealed class MarketplacePackageUiAdapter : IMarketplacePackageUi
    {
        public string DownloadDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");

        public string? Authorization => DownloadFileConfig.Instance.Authorization;

        public void ShowDownloadWindow()
        {
            RunOnUiThread(DownloadWindow.ShowInstance);
        }

        public void ShowWarning(string message, string title)
        {
            RunOnUiThread(() => MessageBox.Show(Application.Current.GetActiveWindow(), message, title, MessageBoxButton.OK, MessageBoxImage.Warning));
        }

        public void ShowError(string message, string title)
        {
            RunOnUiThread(() => MessageBox.Show(Application.Current.GetActiveWindow(), message, title, MessageBoxButton.OK, MessageBoxImage.Error));
        }

        public void OpenFolder(string? folderPath)
        {
            RunOnUiThread(() => PlatformHelper.OpenFolder(folderPath));
        }

        private static void RunOnUiThread(Action action)
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

    public sealed class MarketplacePackageDownloadService
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplacePackageDownloadService));
        private static readonly HttpClient LegacyHttpClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly CompositeFormat DownloadVerificationFailedFormat = CompositeFormat.Parse(Resources.MarketplaceDownloadVerificationFailed);
        private static readonly object Locker = new();
        private static MarketplacePackageDownloadService? _instance;
        private readonly IMarketplacePackageClient _client;
        private readonly IMarketplacePackageDownloader _downloader;
        private readonly IMarketplacePackageInstaller _installer;
        private readonly IMarketplacePackageUi _ui;

        public static MarketplacePackageDownloadService GetInstance()
        {
            lock (Locker)
            {
                return _instance ??= new MarketplacePackageDownloadService();
            }
        }

        public MarketplacePackageDownloadService()
            : this(
                new MarketplacePackageClientAdapter(),
                new MarketplacePackageDownloaderAdapter(),
                new MarketplacePackageInstallerAdapter(),
                new MarketplacePackageUiAdapter())
        {
        }

        public MarketplacePackageDownloadService(
            IMarketplacePackageClient client,
            IMarketplacePackageDownloader downloader,
            IMarketplacePackageInstaller installer,
            IMarketplacePackageUi ui)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _downloader = downloader ?? throw new ArgumentNullException(nameof(downloader));
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
            _ui = ui ?? throw new ArgumentNullException(nameof(ui));
        }

        public async Task<string?> ResolveLatestVersionAsync(string pluginId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(pluginId))
                return null;

            try
            {
                string? version = await _client.GetLatestVersionAsync(pluginId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(version))
                    return version.Trim();
            }
            catch (Exception ex)
            {
                log.Debug($"ResolveLatestVersionAsync API failed for {pluginId}: {ex.Message}");
            }

            string latestReleaseUrl = MarketplaceConfig.BuildLegacyPluginUrl($"{pluginId}/LATEST_RELEASE");
            using var request = new HttpRequestMessage(HttpMethod.Get, latestReleaseUrl);
            string? authorization = _ui.Authorization;
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
                MarketplacePluginDetail? detail = await _client.GetPluginDetailAsync(pluginId, cancellationToken).ConfigureAwait(false);
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
            Directory.CreateDirectory(_ui.DownloadDirectory);

            string? existingFile = _client.GetExistingFileIfValid(_ui.DownloadDirectory, request.PluginId, request.Version, request.ExpectedHash);
            if (existingFile != null)
            {
                log.Info($"Marketplace package cache hit: {request.PluginId} v{request.Version} -> {existingFile}");
                return existingFile;
            }

            _ui.ShowDownloadWindow();
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

            Directory.CreateDirectory(_ui.DownloadDirectory);

            ConcurrentBag<string> packagePaths = new();
            List<MarketplacePackageRequest> missingRequests = new();
            foreach (MarketplacePackageRequest request in distinctRequests)
            {
                string? existingFile = _client.GetExistingFileIfValid(_ui.DownloadDirectory, request.PluginId, request.Version, request.ExpectedHash);
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
                _ui.ShowDownloadWindow();
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
            _ui.OpenFolder(folderPath);
            return true;
        }

        public async Task<bool> InstallPackageAsync(MarketplacePackageRequest request, string? restartArguments = null, CancellationToken cancellationToken = default)
        {
            string? packagePath = await EnsurePackageAvailableAsync(request, cancellationToken: cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrWhiteSpace(packagePath))
                return false;

            _installer.Install(restartArguments, packagePath);
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

                _installer.Install(restartArguments, packagePaths.ToArray());
            }
            catch (Exception ex)
            {
                log.Error($"StartBackgroundBatchInstallCoreAsync failed: {ex.Message}", ex);
                RunOnUIThread(() => onEmpty?.Invoke());
            }
        }

        private async Task<string?> StartDownloadAsync(MarketplacePackageRequest request, bool showFailureDialog, CancellationToken cancellationToken)
        {
            string downloadUrl = _client.GetDownloadUrl(request.PluginId, request.Version);
            string fileName = $"{request.PluginId}-{request.Version}.cvxp";
            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);

            using CancellationTokenRegistration registration = cancellationToken.Register(() => completionSource.TrySetCanceled(cancellationToken));

            _downloader.AddDownload(downloadUrl, _ui.DownloadDirectory, _ui.Authorization, task =>
            {
                if (task.Status != DownloadStatus.Completed)
                {
                    log.Error($"Marketplace package download failed for {request.PluginId} v{request.Version}: {task.ErrorMessage}");
                    if (showFailureDialog)
                    {
                        _ui.ShowWarning(task.ErrorMessage ?? Resources.MarketplaceLoadFailed, Resources.PluginManagerWindow);
                    }

                    completionSource.TrySetResult(null);
                    return;
                }

                if (!_client.VerifyFileHash(task.SavePath, request.ExpectedHash))
                {
                    log.Error($"Marketplace package hash verification failed for {request.PluginId} v{request.Version}.");
                    if (showFailureDialog)
                    {
                        _ui.ShowError(string.Format(null, DownloadVerificationFailedFormat, request.PluginId, request.Version), Resources.PluginManagerWindow);
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
