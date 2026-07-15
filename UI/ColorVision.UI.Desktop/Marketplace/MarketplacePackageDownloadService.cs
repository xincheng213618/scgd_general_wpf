using ColorVision.Common.Utilities;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using ColorVision.UI;
using log4net;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
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
        DownloadTask AddDownload(string url, string downloadDirectory, string? authorization, Action<DownloadTask> onCompleted, string fileName);
        void CancelDownload(DownloadTask task);
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
        bool ConfirmInstall(string message, string title);
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
        public DownloadTask AddDownload(string url, string downloadDirectory, string? authorization, Action<DownloadTask> onCompleted, string fileName)
        {
            return Aria2cDownloadManager.GetInstance().AddDownload(url, downloadDirectory, authorization, onCompleted, fileName);
        }

        public void CancelDownload(DownloadTask task)
        {
            Aria2cDownloadManager.GetInstance().CancelDownload(task);
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
        private static readonly ILog AdapterLog = LogManager.GetLogger(typeof(MarketplacePackageUiAdapter));

        public string DownloadDirectory => Environments.DirPluginPackageCache;

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

        public bool ConfirmInstall(string message, string title)
        {
            return RunOnUiThread(() => MessageBox.Show(Application.Current.GetActiveWindow(), message, title, MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes);
        }

        public void OpenFolder(string? folderPath)
        {
            RunOnUiThread(() => PlatformHelper.OpenFolder(folderPath));
        }

        private static void RunOnUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.InvokeAsync(() => RunSafely(action, AdapterLog));
        }

        private static T RunOnUiThread<T>(Func<T> action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return action();
            return dispatcher.Invoke(action);
        }

        private static void RunSafely(Action action, ILog logger)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                logger.Error("Marketplace UI action failed.", ex);
            }
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

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                string? version = await _client.GetLatestVersionAsync(pluginId, cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(version))
                    return version.Trim();
            }
            catch (OperationCanceledException)
            {
                throw;
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
            catch (OperationCanceledException)
            {
                throw;
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

            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                MarketplacePluginDetail? detail = await _client.GetPluginDetailAsync(pluginId, cancellationToken).ConfigureAwait(false);
                return detail?.Versions
                    .Concat(detail.ArchivedVersions)
                    .FirstOrDefault(item => string.Equals(item.Version, version, StringComparison.OrdinalIgnoreCase))
                    ?.FileHash;
            }
            catch (OperationCanceledException)
            {
                throw;
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
            cancellationToken.ThrowIfCancellationRequested();
            Directory.CreateDirectory(_ui.DownloadDirectory);

            string? existingFile = _client.GetExistingFileIfValid(_ui.DownloadDirectory, request.PluginId, request.Version, request.ExpectedHash);
            if (existingFile != null)
            {
                log.Info($"Marketplace package cache hit: {request.PluginId} v{request.Version} -> {existingFile}");
                return existingFile;
            }

            DeleteInvalidPreferredPackage(request);
            cancellationToken.ThrowIfCancellationRequested();
            _ui.ShowDownloadWindow();
            return await StartDownloadAsync(request, showFailureDialog, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<string>> EnsurePackagesAvailableAsync(IEnumerable<MarketplacePackageRequest> requests, bool showFailureDialog = false, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
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
                cancellationToken.ThrowIfCancellationRequested();
                string? existingFile = _client.GetExistingFileIfValid(_ui.DownloadDirectory, request.PluginId, request.Version, request.ExpectedHash);
                if (existingFile != null)
                {
                    packagePaths.Add(existingFile);
                }
                else
                {
                    DeleteInvalidPreferredPackage(request);
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

        private void DeleteInvalidPreferredPackage(MarketplacePackageRequest request)
        {
            string filePath = Path.Combine(_ui.DownloadDirectory, $"{request.PluginId}-{request.Version}.cvxp");
            if (!File.Exists(filePath))
                return;

            bool isValid = false;
            try
            {
                isValid = _client.VerifyFileHash(filePath, request.ExpectedHash);
            }
            catch (Exception ex)
            {
                log.Debug($"Marketplace package cache validation failed for {request.PluginId} v{request.Version}: {ex.Message}");
            }

            if (isValid)
                return;

            TryDeleteFile(filePath);
            TryDeleteFile(filePath + ".aria2");
            log.Warn($"Deleted invalid marketplace package cache: {filePath}");
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

            cancellationToken.ThrowIfCancellationRequested();
            MarketplaceCopilotPackagePreflight preflight = MarketplaceCopilotPackagePreflightReader.Read(packagePath, request);
            if (!TryApproveSingleInstall(preflight, request))
                return false;

            _installer.Install(restartArguments, packagePath);
            return true;
        }

        public bool TryApproveBatchInstall(IEnumerable<string> packagePaths, IEnumerable<MarketplacePackageRequest>? requests = null)
        {
            MarketplacePackageRequest[] requestSnapshot = requests?.ToArray() ?? Array.Empty<MarketplacePackageRequest>();
            List<MarketplaceCopilotPackagePreflight> preflights = packagePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(path => MarketplaceCopilotPackagePreflightReader.Read(path, FindRequestForPackagePath(path, requestSnapshot)))
                .ToList();
            MarketplaceCopilotPackagePreflight? invalid = preflights.FirstOrDefault(item => !item.IsValid);
            if (invalid != null)
            {
                _ui.ShowError(BuildInvalidPackageMessage(invalid.ErrorMessage), Resources.PluginManagerWindow);
                return false;
            }

            MarketplaceCopilotPackagePreflight[] permissionPackages = preflights
                .Where(item => item.RequiresPermissionReview)
                .ToArray();
            if (permissionPackages.Length == 0)
                return true;

            string packageNames = string.Join(", ", permissionPackages.Select(item => FirstNonEmpty(item.PluginName, item.PluginId, "Unknown")));
            _ui.ShowWarning(BuildBatchReviewRequiredMessage(permissionPackages.Length, packageNames), Resources.PluginManagerWindow);
            return false;
        }

        public void StartBackgroundBatchInstall(IEnumerable<MarketplacePackageRequest> requests, string? restartArguments = null, Action? onEmpty = null, CancellationToken cancellationToken = default)
        {
            _ = StartBackgroundBatchInstallCoreAsync(requests, restartArguments, onEmpty, cancellationToken);
        }

        private async Task StartBackgroundBatchInstallCoreAsync(IEnumerable<MarketplacePackageRequest> requests, string? restartArguments, Action? onEmpty, CancellationToken cancellationToken)
        {
            try
            {
                List<MarketplacePackageRequest> distinctRequests = GetDistinctRequests(requests);
                IReadOnlyList<string> packagePaths = await EnsurePackagesAvailableAsync(distinctRequests, cancellationToken: cancellationToken).ConfigureAwait(false);
                if (packagePaths.Count == 0)
                {
                    RunOnUIThread(() => onEmpty?.Invoke());
                    return;
                }

                if (packagePaths.Count != distinctRequests.Count)
                {
                    log.Warn($"Marketplace background batch install aborted: expected {distinctRequests.Count} packages, got {packagePaths.Count}.");
                    RunOnUIThread(() => onEmpty?.Invoke());
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                if (!TryApproveBatchInstall(packagePaths, distinctRequests))
                {
                    RunOnUIThread(() => onEmpty?.Invoke());
                    return;
                }

                _installer.Install(restartArguments, packagePaths.ToArray());
            }
            catch (OperationCanceledException)
            {
                log.Info("Marketplace background batch install canceled.");
            }
            catch (Exception ex)
            {
                log.Error($"StartBackgroundBatchInstallCoreAsync failed: {ex.Message}", ex);
                RunOnUIThread(() => onEmpty?.Invoke());
            }
        }

        private static List<MarketplacePackageRequest> GetDistinctRequests(IEnumerable<MarketplacePackageRequest> requests)
        {
            return requests
                .Where(item => !string.IsNullOrWhiteSpace(item.PluginId) && !string.IsNullOrWhiteSpace(item.Version))
                .GroupBy(item => $"{item.PluginId}|{item.Version}", StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToList();
        }

        private async Task<string?> StartDownloadAsync(MarketplacePackageRequest request, bool showFailureDialog, CancellationToken cancellationToken)
        {
            string downloadUrl = _client.GetDownloadUrl(request.PluginId, request.Version);
            string fileName = $"{request.PluginId}-{request.Version}.cvxp";
            var completionSource = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
            DownloadTask? downloadTask = null;

            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (downloadTask != null)
                    {
                        _downloader.CancelDownload(downloadTask);
                    }
                }
                catch (Exception ex)
                {
                    log.Debug($"Cancel marketplace download failed for {request.PluginId} v{request.Version}: {ex.Message}");
                }

                completionSource.TrySetCanceled(cancellationToken);
            });

            try
            {
                downloadTask = _downloader.AddDownload(downloadUrl, _ui.DownloadDirectory, _ui.Authorization, task =>
                {
                    if (cancellationToken.IsCancellationRequested)
                    {
                        completionSource.TrySetCanceled(cancellationToken);
                        return;
                    }

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

                if (cancellationToken.IsCancellationRequested)
                {
                    _downloader.CancelDownload(downloadTask);
                    completionSource.TrySetCanceled(cancellationToken);
                }
            }
            catch (OperationCanceledException)
            {
                completionSource.TrySetCanceled(cancellationToken);
            }
            catch (Exception ex)
            {
                log.Error($"Failed to start marketplace package download for {request.PluginId} v{request.Version}.", ex);
                if (showFailureDialog)
                {
                    _ui.ShowWarning(ex.Message, Resources.PluginManagerWindow);
                }

                completionSource.TrySetResult(null);
            }

            return await completionSource.Task.ConfigureAwait(false);
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to delete file: {filePath}", ex);
            }
        }

        private bool TryApproveSingleInstall(MarketplaceCopilotPackagePreflight preflight, MarketplacePackageRequest request)
        {
            if (!preflight.IsValid)
            {
                _ui.ShowError(BuildInvalidPackageMessage(preflight.ErrorMessage), Resources.PluginManagerWindow);
                return false;
            }

            if (!preflight.RequiresPermissionReview)
                return true;

            return _ui.ConfirmInstall(BuildPermissionReviewMessage(preflight), BuildPermissionReviewTitle(preflight, request));
        }

        private static string BuildPermissionReviewTitle(MarketplaceCopilotPackagePreflight preflight, MarketplacePackageRequest request)
        {
            string name = FirstNonEmpty(preflight.PluginName, preflight.PluginId, request.PluginId, "ColorVision");
            string version = FirstNonEmpty(preflight.Version, request.Version);
            return string.IsNullOrWhiteSpace(version) ? name : $"{name} v{version}";
        }

        private static string BuildPermissionReviewMessage(MarketplaceCopilotPackagePreflight preflight)
        {
            bool chinese = IsChineseUi();
            var builder = new StringBuilder();
            builder.AppendLine(chinese
                ? $"此插件声明了 {preflight.Roles.Count} 个 Copilot 子 Agent 角色。请在安装前确认其只读权限和运行预算："
                : $"This plugin declares {preflight.Roles.Count} Copilot subagent role(s). Review their read permissions and run budgets before installation:");
            builder.AppendLine();
            foreach (MarketplaceCopilotRolePreview role in preflight.Roles)
            {
                builder.AppendLine($"• {role.DisplayName} ({role.ToolName})");
                builder.AppendLine(chinese
                    ? $"  权限：{role.Scope} · {string.Join(", ", role.Capabilities)}"
                    : $"  Access: {role.Scope} · {string.Join(", ", role.Capabilities)}");
                builder.AppendLine(chinese
                    ? $"  预算：{role.MaximumToolCalls} 次工具调用 · {role.MaximumAgentPasses} 轮 · {role.MaximumDurationSeconds} 秒 · 最多 {role.MaximumAnswerCharacters:N0} 字符"
                    : $"  Budget: {role.MaximumToolCalls} tool calls · {role.MaximumAgentPasses} passes · {role.MaximumDurationSeconds}s · {role.MaximumAnswerCharacters:N0} answer characters");
            }

            builder.AppendLine();
            builder.AppendLine(chinese
                ? $"提示元数据：{preflight.AdvertisedCharacters:N0}/{CopilotSubagentRoleManifestValidator.MaximumAdvertisedCharactersPerPlugin:N0} 字符。角色安装后默认启用，可在 Copilot 设置中逐项关闭。"
                : $"Prompt metadata: {preflight.AdvertisedCharacters:N0}/{CopilotSubagentRoleManifestValidator.MaximumAdvertisedCharactersPerPlugin:N0} characters. Roles start enabled and can be disabled individually in Copilot settings.");
            builder.Append(chinese ? "是否继续安装？" : "Continue installation?");
            return builder.ToString();
        }

        private static string BuildInvalidPackageMessage(string error)
        {
            return IsChineseUi()
                ? $"插件包预检失败，未执行安装。\n\n{error}"
                : $"Plugin package preflight failed. The package was not installed.\n\n{error}";
        }

        private static string BuildBatchReviewRequiredMessage(int packageCount, string packageNames)
        {
            return IsChineseUi()
                ? $"批量更新已停止：{packageCount} 个插件包声明了 Copilot 子 Agent 角色（{packageNames}）。请逐个更新这些插件，以便在安装前审核权限和提示成本。"
                : $"Bulk update stopped because {packageCount} package(s) declare Copilot subagent roles ({packageNames}). Update them individually to review permissions and prompt cost before installation.";
        }

        private static bool IsChineseUi()
        {
            return string.Equals(CultureInfo.CurrentUICulture.TwoLetterISOLanguageName, "zh", StringComparison.OrdinalIgnoreCase);
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
        }

        private static MarketplacePackageRequest? FindRequestForPackagePath(string packagePath, IReadOnlyList<MarketplacePackageRequest> requests)
        {
            string fileName = Path.GetFileName(packagePath);
            return requests.FirstOrDefault(request => string.Equals(fileName, $"{request.PluginId}-{request.Version}.cvxp", StringComparison.OrdinalIgnoreCase));
        }

        private static void RunOnUIThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null)
            {
                action();
                return;
            }

            if (dispatcher.CheckAccess())
            {
                action();
                return;
            }

            dispatcher.InvokeAsync(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    log.Error("Marketplace UI callback failed.", ex);
                }
            });
        }
    }
}
