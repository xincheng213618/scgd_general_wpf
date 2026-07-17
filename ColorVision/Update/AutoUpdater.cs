using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using ColorVision.UI.ServiceHost;
using log4net;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Linq;
using System.Net;

namespace ColorVision.Update
{
    public class AutoUpdatePlan
    {
        public required Version CurrentVersion { get; init; }
        public required Version LatestVersion { get; init; }
        public required IReadOnlyList<Version> VersionsToApply { get; init; }
        public required bool IsIncremental { get; init; }

        public Version TargetVersion => VersionsToApply.Count > 0
            ? VersionsToApply[VersionsToApply.Count - 1]
            : LatestVersion;
    }

    public class AutoUpdateConfig:ViewModelBase, IConfig
    {
        public static AutoUpdateConfig Instance  => ConfigService.Instance.GetRequiredService<AutoUpdateConfig>();

        /// <summary>
        /// 是否自动更新
        /// </summary>
        [ConfigSetting(Order = 500, Section = ConfigSettingConstants.SectionBasic, Description = "CheckUpdatesOnStartupDescription")]
        [DisplayName("CheckUpdatesOnStartup")]
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; OnPropertyChanged(); } }
        private bool _IsAutoUpdate = true;

    }


    public class AutoUpdater : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdater));
        private static readonly HttpClient _metadataClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static readonly SemaphoreSlim _latestVersionSemaphore = new(1, 1);
        private static readonly object _latestVersionCacheLock = new();
        private static readonly TimeSpan MetadataRequestTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan LatestVersionClientCacheDuration = TimeSpan.FromSeconds(30);
        private static string? _cachedLatestVersionUrl;
        private static Version? _cachedLatestVersion;
        private static DateTimeOffset _cachedLatestVersionAt = DateTimeOffset.MinValue;
        private static string? _cachedLatestVersionETag;
        private static AutoUpdater _instance;
        private static readonly object _locker = new();
        public static AutoUpdater GetInstance() { lock (_locker) { return _instance ??= new AutoUpdater(); } }

        public static string UpdateUrl => BuildAppApiUrl("latest-version");

        public Version LatestVersion { get => _LatestVersion; set { _LatestVersion = value; OnPropertyChanged(); } }
        private Version _LatestVersion;

        public static Version? CurrentVersion { get => Assembly.GetExecutingAssembly().GetName().Version; }

        public static string GetReleasePackageDownloadUrl(Version version) => BuildAppApiUrl($"releases/{Uri.EscapeDataString(version.ToString())}/download");

        public static string GetIncrementalPackageDownloadUrl(Version version) => BuildAppApiUrl($"updates/{Uri.EscapeDataString(version.ToString())}/download");

        public static string GetApplicationPackageCacheDirectory(bool isIncremental)
        {
            return isIncremental
                ? Environments.DirApplicationIncrementalPackageCache
                : Environments.DirApplicationFullPackageCache;
        }

        public async Task ForceUpdate(CancellationToken cancellationToken = default)
        {
            LatestVersion = await GetLatestVersionNumber(UpdateUrl, forceRefresh: true, cancellationToken: cancellationToken);
            if (LatestVersion == new Version()) return;
            await InvokeOnUiThreadAsync(() =>
            {
                StartFullUpdate(LatestVersion);
            });
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action).Task;
        }

        private static void PostToUiThread(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                RunUiAction(action);
                return;
            }

            dispatcher.InvokeAsync(() => RunUiAction(action));
        }

        private static void RunUiAction(Action action)
        {
            try
            {
                action();
            }
            catch (Exception ex)
            {
                log.Error("AutoUpdater UI action failed.", ex);
            }
        }

        public static async Task<Version> GetLatestVersionNumber(string url, bool forceRefresh, CancellationToken cancellationToken = default)
        {
            string? versionString = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                log.Warn("Failed to fetch update metadata: update service URL is empty.");
                return new Version();
            }

            if (!WindowsNetworkState.IsConnectedToInternet())
            {
                log.Info("Skipped update metadata check because Windows reports no internet connectivity.");
                return new Version();
            }

            if (!forceRefresh && TryGetFreshCachedLatestVersion(url, out Version freshCachedVersion))
            {
                return freshCachedVersion;
            }

            await _latestVersionSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh && TryGetFreshCachedLatestVersion(url, out freshCachedVersion))
                {
                    return freshCachedVersion;
                }

                using CancellationTokenSource timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutSource.CancelAfter(MetadataRequestTimeout);
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                ApplyAuthorizationHeader(request);
                string? cachedETag = forceRefresh ? null : GetCachedLatestVersionETag(url);
                if (!string.IsNullOrWhiteSpace(cachedETag))
                {
                    request.Headers.TryAddWithoutValidation("If-None-Match", cachedETag);
                }

                using HttpResponseMessage response = await _metadataClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeoutSource.Token);
                if (response.StatusCode == HttpStatusCode.NotModified
                    && TryGetAnyCachedLatestVersion(url, out Version notModifiedVersion))
                {
                    TouchCachedLatestVersion(url);
                    return notModifiedVersion;
                }

                response.EnsureSuccessStatusCode();
                string payload = await response.Content.ReadAsStringAsync(timeoutSource.Token);
                versionString = ExtractVersionString(payload);
                if (!Version.TryParse(versionString.Trim(), out Version? latestVersion))
                {
                    log.Warn($"Invalid update version payload from {url}: {versionString}");
                    return new Version();
                }

                SetCachedLatestVersion(url, latestVersion, response.Headers.ETag?.ToString());
                return latestVersion;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                log.Warn($"Failed to fetch update metadata from {url}: {ex.GetBaseException().Message}");
                return new Version();
            }
            catch (OperationCanceledException ex)
            {
                log.Warn($"Timed out fetching update metadata from {url}: {ex.GetBaseException().Message}");
                return new Version();
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected failure checking update metadata from {url}.", ex);
                return new Version();
            }
            finally
            {
                _latestVersionSemaphore.Release();
            }
        }

        private static bool TryGetFreshCachedLatestVersion(string url, out Version version)
        {
            lock (_latestVersionCacheLock)
            {
                if (_cachedLatestVersion != null
                    && string.Equals(_cachedLatestVersionUrl, url, StringComparison.OrdinalIgnoreCase)
                    && DateTimeOffset.UtcNow - _cachedLatestVersionAt <= LatestVersionClientCacheDuration)
                {
                    version = _cachedLatestVersion;
                    return true;
                }
            }

            version = new Version();
            return false;
        }

        private static bool TryGetAnyCachedLatestVersion(string url, out Version version)
        {
            lock (_latestVersionCacheLock)
            {
                if (_cachedLatestVersion != null
                    && string.Equals(_cachedLatestVersionUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    version = _cachedLatestVersion;
                    return true;
                }
            }

            version = new Version();
            return false;
        }

        private static string? GetCachedLatestVersionETag(string url)
        {
            lock (_latestVersionCacheLock)
            {
                return string.Equals(_cachedLatestVersionUrl, url, StringComparison.OrdinalIgnoreCase)
                    ? _cachedLatestVersionETag
                    : null;
            }
        }

        private static void SetCachedLatestVersion(string url, Version version, string? etag)
        {
            lock (_latestVersionCacheLock)
            {
                _cachedLatestVersionUrl = url;
                _cachedLatestVersion = version;
                _cachedLatestVersionAt = DateTimeOffset.UtcNow;
                _cachedLatestVersionETag = etag;
            }
        }

        private static void TouchCachedLatestVersion(string url)
        {
            lock (_latestVersionCacheLock)
            {
                if (string.Equals(_cachedLatestVersionUrl, url, StringComparison.OrdinalIgnoreCase))
                {
                    _cachedLatestVersionAt = DateTimeOffset.UtcNow;
                }
            }
        }

        public async Task<AutoUpdatePlan?> GetUpdatePlanAsync(bool forceRefresh, CancellationToken cancellationToken = default)
        {
            LatestVersion = await GetLatestVersionNumber(UpdateUrl, forceRefresh, cancellationToken);
            if (LatestVersion == new Version())
                return null;

            Version? currentVersion = CurrentVersion;
            return currentVersion == null ? null : BuildUpdatePlan(currentVersion, LatestVersion);
        }

        internal static AutoUpdatePlan? BuildUpdatePlan(Version currentVersion, Version latestVersion)
        {
            if (latestVersion <= currentVersion)
                return null;

            bool isIncrement = latestVersion.Major == currentVersion.Major
                && latestVersion.Minor == currentVersion.Minor;
            IReadOnlyList<Version> versionsToApply = isIncrement
                ? BuildIncrementalUpdateChain(currentVersion, latestVersion)
                : new[] { latestVersion };

            return new AutoUpdatePlan
            {
                CurrentVersion = currentVersion,
                LatestVersion = latestVersion,
                VersionsToApply = versionsToApply,
                IsIncremental = isIncrement,
            };
        }

        public static void StartUpdatePlan(AutoUpdatePlan plan, Action? downloadFailedAction = null)
        {
            _ = StartUpdatePlanAsync(plan, downloadFailedAction);
        }

        public static void StartFullUpdate(Version version, Action? downloadFailedAction = null)
        {
            string? cachedInstaller = GetCachedFullInstallerPath(version);
            if (cachedInstaller != null)
            {
                UpdateApplication(cachedInstaller, isIncrement: false);
                return;
            }

            AutoUpdatePlan plan = new()
            {
                CurrentVersion = CurrentVersion ?? version,
                LatestVersion = version,
                VersionsToApply = new[] { version },
                IsIncremental = false,
            };
            _ = StartUpdatePlanAsync(plan, downloadFailedAction);
        }

        private static async Task StartUpdatePlanAsync(AutoUpdatePlan plan, Action? downloadFailedAction)
        {
            try
            {
                IReadOnlyList<string> packagePaths = await EnsureUpdatePlanPackagesAsync(
                    plan,
                    showDownloadWindow: true,
                    CancellationToken.None).ConfigureAwait(false);

                if (plan.IsIncremental)
                {
                    int expectedCount = plan.VersionsToApply.Distinct().Count();
                    if (RequiresFullInstallerFallback(expectedCount, packagePaths.Count))
                    {
                        log.Warn($"Incremental update chain is incomplete; falling back to the full installer for {plan.LatestVersion}.");
                        PostToUiThread(() => StartFullUpdate(plan.LatestVersion, downloadFailedAction));
                        return;
                    }

                    UpdateIncrementalApplications(packagePaths);
                    return;
                }

                if (packagePaths.Count == 1)
                {
                    UpdateApplication(packagePaths[0], isIncrement: false);
                    return;
                }

                log.Error($"Full installer download failed for {plan.TargetVersion}.");
                PostToUiThread(() => downloadFailedAction?.Invoke());
            }
            catch (Exception ex)
            {
                log.Error("Application update download failed.", ex);
                PostToUiThread(() => downloadFailedAction?.Invoke());
            }
        }

        internal static async Task<bool> PrefetchUpdatePlanAsync(AutoUpdatePlan plan, CancellationToken cancellationToken)
        {
            IReadOnlyList<string> packagePaths = await EnsureUpdatePlanPackagesAsync(plan, showDownloadWindow: false, cancellationToken).ConfigureAwait(false);
            int expectedCount = plan.IsIncremental ? plan.VersionsToApply.Distinct().Count() : 1;
            return packagePaths.Count == expectedCount;
        }

        internal static async Task<IReadOnlyList<string>> EnsureUpdatePlanPackagesAsync(AutoUpdatePlan plan, bool showDownloadWindow, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            List<Task<bool>> downloads = new();
            string downloadDirectory = GetApplicationPackageCacheDirectory(plan.IsIncremental);
            IEnumerable<Version> versions = plan.IsIncremental
                ? plan.VersionsToApply.Distinct()
                : new[] { plan.TargetVersion };

            foreach (Version version in versions)
            {
                string fileName = plan.IsIncremental
                    ? GetIncrementalPackageFileName(version)
                    : GetReleasePackageFileName(version);
                if (FindReadyApplicationPackagePath(downloadDirectory, fileName, plan.IsIncremental) != null)
                    continue;

                string canonicalPath = Path.Combine(downloadDirectory, fileName);
                _ = MoveInvalidApplicationPackageToRecovery(canonicalPath, plan.IsIncremental);

                string downloadUrl = plan.IsIncremental
                    ? GetIncrementalPackageDownloadUrl(version)
                    : GetReleasePackageDownloadUrl(version);
                downloads.Add(DownloadPackageAsync(downloadUrl, downloadDirectory, fileName, plan.IsIncremental, cancellationToken));
            }

            if (downloads.Count > 0)
            {
                if (showDownloadWindow)
                    PostToUiThread(DownloadWindow.ShowInstance);
                await Task.WhenAll(downloads).ConfigureAwait(false);
            }

            if (plan.IsIncremental)
                return TryGetCachedIncrementalPackagePaths(plan, out IReadOnlyList<string> packagePaths) ? packagePaths : Array.Empty<string>();

            string? installerPath = GetCachedFullInstallerPath(plan.TargetVersion);
            return installerPath == null ? Array.Empty<string>() : new[] { installerPath };
        }

        internal static bool TryGetCachedIncrementalPackagePaths(AutoUpdatePlan plan, out IReadOnlyList<string> packagePaths)
        {
            List<string> paths = new();
            if (!plan.IsIncremental)
            {
                packagePaths = paths;
                return false;
            }

            string downloadDirectory = GetApplicationPackageCacheDirectory(isIncremental: true);
            foreach (Version version in plan.VersionsToApply.Distinct())
            {
                string? packagePath = FindReadyApplicationPackagePath(downloadDirectory, GetIncrementalPackageFileName(version), isIncremental: true);
                if (packagePath == null)
                {
                    packagePaths = Array.Empty<string>();
                    return false;
                }

                paths.Add(packagePath);
            }

            packagePaths = paths;
            return paths.Count > 0;
        }

        internal static string? GetCachedFullInstallerPath(Version version)
        {
            return FindReadyApplicationPackagePath(
                GetApplicationPackageCacheDirectory(isIncremental: false),
                GetReleasePackageFileName(version),
                isIncremental: false);
        }

        internal static string? FindReadyApplicationPackagePath(string directory, string canonicalFileName, bool isIncremental)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(canonicalFileName) || !Directory.Exists(directory))
                    return null;

                string canonicalPath = Path.Combine(directory, canonicalFileName);
                if (IsApplicationPackageFileReady(canonicalPath, isIncremental))
                    return canonicalPath;

                string canonicalStem = Path.GetFileNameWithoutExtension(canonicalFileName);
                string extension = Path.GetExtension(canonicalFileName);
                return Directory.EnumerateFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly)
                    .Where(path => IsUniqueDownloadVariant(Path.GetFileNameWithoutExtension(path), canonicalStem))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault(path => IsApplicationPackageFileReady(path, isIncremental));
            }
            catch (Exception ex)
            {
                log.Debug($"Failed to inspect application update cache '{directory}': {ex.Message}");
                return null;
            }
        }

        private static bool IsUniqueDownloadVariant(string candidateStem, string canonicalStem)
        {
            if (!candidateStem.StartsWith(canonicalStem, StringComparison.OrdinalIgnoreCase))
                return false;

            string suffix = candidateStem[canonicalStem.Length..];
            if (suffix.StartsWith(" (", StringComparison.Ordinal))
                suffix = suffix[1..];

            if (suffix.Length > 2
                && suffix.StartsWith('(')
                && suffix.EndsWith(')')
                && int.TryParse(suffix.AsSpan(1, suffix.Length - 2), out _))
            {
                return true;
            }

            return suffix.Length == 15
                && suffix.StartsWith('_')
                && long.TryParse(suffix.AsSpan(1), out _);
        }

        private static async Task<bool> DownloadPackageAsync(
            string url,
            string downloadDirectory,
            string fileName,
            bool isIncremental,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Aria2cDownloadManager manager = Aria2cDownloadManager.GetInstance();
            DownloadTask? downloadTask = null;
            using CancellationTokenRegistration registration = cancellationToken.Register(() =>
            {
                if (downloadTask != null)
                    manager.CancelDownload(downloadTask);
                completion.TrySetCanceled(cancellationToken);
            });

            downloadTask = manager.AddDownload(url, downloadDirectory, "1:1", task =>
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    completion.TrySetCanceled(cancellationToken);
                    return;
                }

                bool ready = task.Status == DownloadStatus.Completed
                    && IsApplicationPackageFileReady(task.SavePath, isIncremental);
                if (!ready && task.Status == DownloadStatus.Completed)
                {
                    _ = MoveInvalidApplicationPackageToRecovery(task.SavePath, isIncremental);
                }

                completion.TrySetResult(ready);
            }, fileName);

            if (cancellationToken.IsCancellationRequested)
            {
                manager.CancelDownload(downloadTask);
                cancellationToken.ThrowIfCancellationRequested();
            }

            return await completion.Task.ConfigureAwait(false);
        }

        internal static bool RequiresFullInstallerFallback(int expectedPackageCount, int availablePackageCount)
        {
            return availablePackageCount != expectedPackageCount;
        }

        private static void UpdateIncrementalApplications(IReadOnlyList<string> downloadPaths)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            RestartIsIncrementApplication(downloadPaths, null);
        }

        private static List<Version> BuildIncrementalUpdateChain(Version currentVersion, Version latestVersion)
        {
            List<Version> versions = new();

            if (latestVersion.Build != currentVersion.Build)
            {
                for (int build = currentVersion.Build + 1; build <= latestVersion.Build; build++)
                {
                    versions.Add(new Version(currentVersion.Major, currentVersion.Minor, build, 1));
                }
            }

            if (versions.Count == 0 || versions[versions.Count - 1] < latestVersion)
            {
                versions.Add(latestVersion);
            }

            return versions;
        }

        public static string GetIncrementalPackageFileName(Version version) => $"ColorVision-Update-[{version}].cvx";

        public static string GetReleasePackageFileName(Version version) => $"ColorVision-{version}.exe";

        private static void ApplyAuthorizationHeader(HttpRequestMessage request)
        {
            string authorization = DownloadFileConfig.Instance.Authorization;
            if (string.IsNullOrWhiteSpace(authorization))
                return;

            byte[] byteArray = Encoding.ASCII.GetBytes(authorization);
            request.Headers.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        }

        private static string ExtractVersionString(string payload)
        {
            string trimmed = payload?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(trimmed))
                return string.Empty;

            if (!trimmed.StartsWith('{'))
                return trimmed;

            try
            {
                using JsonDocument document = JsonDocument.Parse(trimmed);
                if (document.RootElement.TryGetProperty("version", out JsonElement element)
                    && element.ValueKind == JsonValueKind.String)
                {
                    return element.GetString() ?? string.Empty;
                }
            }
            catch (JsonException ex)
            {
                log.Warn($"Failed to parse update version payload: {ex.Message}");
            }

            return trimmed;
        }

        private static string BuildAppApiUrl(string relativePath)
        {
            return MarketplaceConfig.BuildApiUrl($"api/app/{relativePath.TrimStart('/')}");
        }

        public static bool IsIncrementalPackageFileReady(string? filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)
                    || !File.Exists(filePath)
                    || File.Exists(filePath + ".aria2")
                    || !string.Equals(Path.GetExtension(filePath), ".cvx", StringComparison.OrdinalIgnoreCase)
                    || new FileInfo(filePath).Length == 0)
                {
                    return false;
                }

                using ZipArchive archive = ZipFile.OpenRead(filePath);
                return archive.Entries.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        public static bool IsFullInstallerFileReady(string? filePath)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)
                    || !File.Exists(filePath)
                    || File.Exists(filePath + ".aria2")
                    || !string.Equals(Path.GetExtension(filePath), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }

                using FileStream stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                if (stream.Length < 68)
                    return false;

                using BinaryReader reader = new(stream);
                if (reader.ReadUInt16() != 0x5A4D)
                    return false;

                stream.Position = 0x3C;
                int peHeaderOffset = reader.ReadInt32();
                if (peHeaderOffset < 64 || peHeaderOffset > stream.Length - sizeof(uint))
                    return false;

                stream.Position = peHeaderOffset;
                return reader.ReadUInt32() == 0x00004550;
            }
            catch
            {
                return false;
            }
        }

        internal static bool IsApplicationPackageFileReady(string? filePath, bool isIncremental)
        {
            return isIncremental
                ? IsIncrementalPackageFileReady(filePath)
                : IsFullInstallerFileReady(filePath);
        }

        internal static string? MoveInvalidApplicationPackageToRecovery(string? filePath, bool isIncremental)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)
                    || !File.Exists(filePath)
                    || File.Exists(filePath + ".aria2")
                    || IsApplicationPackageFileReady(filePath, isIncremental))
                {
                    return null;
                }

                string sourcePath = Path.GetFullPath(filePath);
                string recoveryDirectory = Path.Combine(Path.GetDirectoryName(sourcePath)!, "Recovery");
                Directory.CreateDirectory(recoveryDirectory);
                string recoveryFileName = $"{Path.GetFileNameWithoutExtension(sourcePath)}-unreadable-{DateTime.Now:yyyyMMdd-HHmmss}-{Guid.NewGuid():N}{Path.GetExtension(sourcePath)}";
                string recoveryPath = Path.Combine(recoveryDirectory, recoveryFileName);
                File.Move(sourcePath, recoveryPath);
                log.Warn($"Moved unreadable application package cache to recovery storage: {recoveryPath}");
                return recoveryPath;
            }
            catch (Exception ex)
            {
                log.Warn($"Failed to preserve unreadable application package cache '{filePath}': {ex.Message}");
                return null;
            }
        }

        private static void UpdateApplication(string downloadPath, bool isIncrement)
        {
            ConfigHandler.GetInstance().SaveConfigs();

            if (isIncrement)
            {
                RestartIsIncrementApplication(downloadPath);
            }
            else
            {
                RestartApplication(downloadPath);
            }
        }


        public static void RestartIsIncrementApplication(string downloadPath)
        {
            RestartIsIncrementApplication(new[] { downloadPath }, null);
        }

        public static void RestartIsIncrementApplication(IEnumerable<string> downloadPaths, IEnumerable<string>? pluginDownloadPaths)
        {
            TryStartIncrementalApplicationUpdate(
                downloadPaths,
                pluginDownloadPaths,
                restartApplication: true,
                allowElevationFallback: true,
                showErrors: true);
        }

        internal static bool TryStartIncrementalApplicationUpdate(
            IEnumerable<string> downloadPaths,
            IEnumerable<string>? pluginDownloadPaths,
            bool restartApplication,
            bool allowElevationFallback,
            bool showErrors)
        {
            string? tempRoot = null;
            try
            {
                List<string> applicationPackagePaths = downloadPaths?
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
                List<string> pluginPackagePaths = pluginDownloadPaths?
                    .Where(path => !string.IsNullOrWhiteSpace(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                if (applicationPackagePaths.Any(path => !IsIncrementalPackageFileReady(path)))
                    throw new InvalidDataException("One or more incremental application packages are incomplete or invalid.");

                // 更新脚本、解包中间文件和最终待复制文件相互隔离。
                tempRoot = Path.Combine(Path.GetTempPath(), $"ColorVisionUpdate-{Guid.NewGuid():N}");
                string stageDirectory = Path.Combine(tempRoot, "ColorVision");
                Directory.CreateDirectory(stageDirectory);

                bool hasAnyPackage = false;
                foreach (string downloadPath in applicationPackagePaths)
                {
                    ZipFile.ExtractToDirectory(downloadPath, stageDirectory, true);
                    hasAnyPackage = true;
                }

                if (pluginPackagePaths.Count > 0)
                {
                    string pluginsDirectory = Path.Combine(stageDirectory, "Plugins");
                    PluginUpdater.StagePluginPackages(pluginPackagePaths, pluginsDirectory, Path.Combine(tempRoot, "Packages"));
                    hasAnyPackage = true;
                }

                if (!hasAnyPackage)
                    throw new InvalidOperationException("Unable to locate incremental update package.");

                int skippedShellExtensionFiles = RemoveShellExtensionFilesFromUpdateStage(stageDirectory);
                if (skippedShellExtensionFiles > 0)
                {
                    log.Info($"Skipped {skippedShellExtensionFiles} shell extension file(s) during incremental update.");
                }

                // 创建批处理文件内容
                string batchFilePath = Path.Combine(tempRoot, "update.bat");
                string programDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string executableName = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe";

                TimeSpan? privilegeTimeout = allowElevationFallback ? null : TimeSpan.FromSeconds(3);
                string serviceHostPackageDirectory = Path.Combine(stageDirectory, "ServiceHost");
                string? availableServiceHostPackageDirectory = File.Exists(Path.Combine(serviceHostPackageDirectory, ServiceHostProtocol.ExecutableName))
                    ? serviceHostPackageDirectory
                    : null;
                bool canUpdateWithoutElevation = ApplicationUpdatePrivilegeBroker.TryPrepareApplicationDirectory(
                    availableServiceHostPackageDirectory,
                    privilegeTimeout);
                if (!canUpdateWithoutElevation && !allowElevationFallback)
                {
                    log.Info("Skipped exit-time update because ColorVisionServiceHost could not prepare the application directory silently.");
                    TryDeleteUpdateStage(tempRoot);
                    return false;
                }

                string batchContent = CreateIncrementalUpdateBatch(
                    stageDirectory,
                    tempRoot,
                    programDirectory,
                    executableName,
                    repairServiceHost: !canUpdateWithoutElevation,
                    restartApplication: restartApplication);

                File.WriteAllText(batchFilePath, batchContent);

                // 设置批处理文件的启动信息
                ProcessStartInfo startInfo = new()
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden,
                    WorkingDirectory = tempRoot,
                };

                if (!canUpdateWithoutElevation && allowElevationFallback)
                {
                    startInfo.Verb = "runas"; // 请求管理员权限
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                }
                try
                {
                    _ = Process.Start(startInfo) ?? throw new InvalidOperationException("Failed to start incremental update batch.");
                    if (restartApplication)
                        Environment.Exit(0);
                    return true;
                }
                catch (Exception ex)
                {
                    log.Error("Failed to start incremental update batch.", ex);
                    if (showErrors)
                        MessageBox.Show(ex.Message);
                    TryDeleteUpdateStage(tempRoot);
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to prepare incremental update.", ex);
                if (showErrors)
                    MessageBox.Show(ColorVision.Properties.Resources.UpdateFailed+$": {ex.Message}");
                TryDeleteUpdateStage(tempRoot);
                return false;
            }
        }

        private static void TryDeleteUpdateStage(string? tempDirectory)
        {
            if (string.IsNullOrWhiteSpace(tempDirectory) || !Directory.Exists(tempDirectory))
                return;

            try
            {
                Directory.Delete(tempDirectory, recursive: true);
            }
            catch (Exception ex)
            {
                log.Debug($"Failed to delete unused update stage '{tempDirectory}': {ex.Message}");
            }
        }

        private static int RemoveShellExtensionFilesFromUpdateStage(string stageDirectory)
        {
            if (string.IsNullOrWhiteSpace(stageDirectory) || !Directory.Exists(stageDirectory))
                return 0;

            int removedCount = 0;
            foreach (string filePath in Directory.EnumerateFiles(stageDirectory, "ColorVision.ShellExtension*", SearchOption.AllDirectories))
            {
                FileAttributes attributes = File.GetAttributes(filePath);
                if (attributes.HasFlag(FileAttributes.ReadOnly))
                {
                    File.SetAttributes(filePath, attributes & ~FileAttributes.ReadOnly);
                }

                File.Delete(filePath);
                removedCount++;
            }

            return removedCount;
        }

        private static string CreateIncrementalUpdateBatch(
            string stageDirectory,
            string cleanupDirectory,
            string programDirectory,
            string executableName,
            bool repairServiceHost,
            bool restartApplication)
        {
            string executablePath = Path.Combine(programDirectory, executableName);
            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal DisableDelayedExpansion");
            sb.AppendLine("title ColorVision Incremental Updater");
            sb.AppendLine($"set \"STAGE={EscapeForBatchValue(stageDirectory)}\"");
            sb.AppendLine($"set \"UPDATE_ROOT={EscapeForBatchValue(cleanupDirectory)}\"");
            sb.AppendLine($"set \"TARGET={EscapeForBatchValue(programDirectory)}\"");
            sb.AppendLine($"set \"EXE={EscapeForBatchValue(executableName)}\"");
            sb.AppendLine($"set \"EXEPATH={EscapeForBatchValue(executablePath)}\"");
            sb.AppendLine($"set \"REPAIR_SERVICE_HOST={(repairServiceHost ? "1" : "0")}\"");
            sb.AppendLine($"set \"RESTART_APPLICATION={(restartApplication ? "1" : "0")}\"");
            sb.AppendLine($"set \"SERVICE_NAME={ServiceHostProtocol.ServiceName}\"");
            sb.AppendLine($"set \"SERVICE_DISPLAY_NAME={ServiceHostProtocol.DisplayName}\"");
            sb.AppendLine($"set \"SERVICE_EXE_NAME={ServiceHostProtocol.ExecutableName}\"");
            sb.AppendLine("set \"SERVICE_SOURCE=%STAGE%\\ServiceHost\"");
            sb.AppendLine("set \"SERVICE_DEST=%ProgramData%\\ColorVision\\ServiceHost\"");
            sb.AppendLine("set \"SERVICE_EXE=%SERVICE_DEST%\\%SERVICE_EXE_NAME%\"");
            sb.AppendLine("set \"SERVICE_LOG=%SERVICE_DEST%\\install.log\"");
            sb.AppendLine();
            sb.AppendLine("if \"%RESTART_APPLICATION%\"==\"0\" ping -n 3 127.0.0.1 >nul");
            sb.AppendLine("taskkill /f /im \"%EXE%\" >nul 2>nul");
            sb.AppendLine("ping -n 3 127.0.0.1 >nul");
            sb.AppendLine("call :skip_shell_extension_files");
            sb.AppendLine("if errorlevel 1 goto fail");
            sb.AppendLine();
            sb.AppendLine("call :copy_application_files");
            sb.AppendLine("if errorlevel 1 goto fail");
            sb.AppendLine("call :repair_service_host");
            sb.AppendLine("goto success");
            sb.AppendLine();
            sb.AppendLine(":skip_shell_extension_files");
            sb.AppendLine("for /r \"%STAGE%\" %%F in (ColorVision.ShellExtension*) do (");
            sb.AppendLine("  del /f /q \"%%F\" >nul 2>nul");
            sb.AppendLine("  if exist \"%%F\" exit /b 1");
            sb.AppendLine(")");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();
            sb.AppendLine(":copy_application_files");
            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if errorlevel 1 goto copy_application_files_xcopy");
            sb.AppendLine("robocopy \"%STAGE%\" \"%TARGET%\" *.* /E /IS /IT /XF ColorVision.ShellExtension* /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("if errorlevel 8 exit /b 8");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":copy_application_files_xcopy");
            sb.AppendLine("xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("if errorlevel 1 exit /b 1");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();
            AppendServiceHostRepairBatch(sb);
            sb.AppendLine();
            sb.AppendLine(":success");
            sb.AppendLine("if \"%RESTART_APPLICATION%\"==\"1\" start \"\" /b \"%EXEPATH%\"");
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();
            sb.AppendLine(":fail");
            sb.AppendLine("if \"%RESTART_APPLICATION%\"==\"1\" start \"\" /b \"%EXEPATH%\"");
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("exit /b 1");
            sb.AppendLine();
            sb.AppendLine(":schedule_cleanup");
            sb.AppendLine("start \"\" /b cmd /d /c ping -n 4 127.0.0.1 ^>nul ^& rd /s /q \"%UPDATE_ROOT%\" 2^>nul");
            sb.AppendLine("exit /b 0");
            return sb.ToString();
        }

        private static void AppendServiceHostRepairBatch(StringBuilder sb)
        {
            sb.AppendLine(":repair_service_host");
            sb.AppendLine("if not \"%REPAIR_SERVICE_HOST%\"==\"1\" exit /b 0");
            sb.AppendLine("if not exist \"%SERVICE_SOURCE%\\%SERVICE_EXE_NAME%\" exit /b 0");
            sb.AppendLine("if not exist \"%SERVICE_DEST%\" mkdir \"%SERVICE_DEST%\" >nul 2>nul");
            sb.AppendLine("if not exist \"%SERVICE_DEST%\" exit /b 0");
            sb.AppendLine("call :service_host_log \"Repair started.\"");
            sb.AppendLine("sc.exe stop \"%SERVICE_NAME%\" >nul 2>nul");
            sb.AppendLine("ping -n 4 127.0.0.1 >nul");
            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if errorlevel 1 goto repair_service_host_xcopy");
            sb.AppendLine("robocopy \"%SERVICE_SOURCE%\" \"%SERVICE_DEST%\" *.* /E /IS /IT /XF install.log /NFL /NDL /NP /NJH /NJS /R:2 /W:1 >nul");
            sb.AppendLine("if errorlevel 8 goto repair_service_host_failed");
            sb.AppendLine("goto repair_service_host_configure");
            sb.AppendLine(":repair_service_host_xcopy");
            sb.AppendLine("xcopy /y /e /i \"%SERVICE_SOURCE%\\*\" \"%SERVICE_DEST%\\\" >nul");
            sb.AppendLine("if errorlevel 1 goto repair_service_host_failed");
            sb.AppendLine(":repair_service_host_configure");
            sb.AppendLine("sc.exe query \"%SERVICE_NAME%\" >nul 2>nul");
            sb.AppendLine("if errorlevel 1 goto repair_service_host_create");
            sb.AppendLine("sc.exe config \"%SERVICE_NAME%\" binPath= \"\\\"%SERVICE_EXE%\\\"\" start= auto DisplayName= \"%SERVICE_DISPLAY_NAME%\" >nul");
            sb.AppendLine("if errorlevel 1 goto repair_service_host_failed");
            sb.AppendLine("goto repair_service_host_description");
            sb.AppendLine(":repair_service_host_create");
            sb.AppendLine("sc.exe create \"%SERVICE_NAME%\" binPath= \"\\\"%SERVICE_EXE%\\\"\" start= auto DisplayName= \"%SERVICE_DISPLAY_NAME%\" >nul");
            sb.AppendLine("if errorlevel 1 goto repair_service_host_failed");
            sb.AppendLine(":repair_service_host_description");
            sb.AppendLine($"sc.exe description \"%SERVICE_NAME%\" \"{ServiceHostProtocol.Description}\" >nul 2>nul");
            sb.AppendLine("sc.exe start \"%SERVICE_NAME%\" >nul 2>nul");
            sb.AppendLine("if not errorlevel 1 goto repair_service_host_completed");
            sb.AppendLine("set \"SERVICE_ERROR=%ERRORLEVEL%\"");
            sb.AppendLine("if \"%SERVICE_ERROR%\"==\"1056\" goto repair_service_host_completed");
            sb.AppendLine("goto repair_service_host_failed");
            sb.AppendLine(":repair_service_host_completed");
            sb.AppendLine("call :service_host_log \"Repair completed.\"");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":repair_service_host_failed");
            sb.AppendLine("sc.exe start \"%SERVICE_NAME%\" >nul 2>nul");
            sb.AppendLine("call :service_host_log \"Repair failed with error %ERRORLEVEL%. The application update will continue.\"");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":service_host_log");
            sb.AppendLine(">>\"%SERVICE_LOG%\" echo [%date% %time%] %~1");
            sb.AppendLine("exit /b 0");
        }

        private static string EscapeForBatchValue(string value)
        {
            return value.Replace("%", "%%");
        }

        public static void RestartApplication(string downloadPath)
        {
            ProcessStartInfo startInfo = new()
            {
                UseShellExecute = true,
                WorkingDirectory = Environment.CurrentDirectory,
                FileName = downloadPath,
            };

            try
            {
                Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


    }
}
