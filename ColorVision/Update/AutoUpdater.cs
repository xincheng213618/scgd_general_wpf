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
using System.Net;
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


    public static class AutoUpdater
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdater));
        private static readonly SemaphoreSlim _latestVersionSemaphore = new(1, 1);
        private static readonly object _latestVersionCacheLock = new();
        private static readonly TimeSpan MetadataRequestTimeout = TimeSpan.FromSeconds(4);
        private static readonly TimeSpan LatestVersionClientCacheDuration = TimeSpan.FromMinutes(5);
        private static string? _cachedLatestVersionUrl;
        private static Version? _cachedLatestVersion;
        private static DateTimeOffset _cachedLatestVersionAt = DateTimeOffset.MinValue;
        private static string? _cachedLatestVersionETag;
        public static string UpdateUrl => BuildAppApiUrl("latest-version");

        public static Version? CurrentVersion { get => Assembly.GetExecutingAssembly().GetName().Version; }

        public static string GetReleasePackageDownloadUrl(Version version) => BuildAppApiUrl($"releases/{Uri.EscapeDataString(version.ToString())}/download");

        public static string GetIncrementalPackageDownloadUrl(Version version) => BuildAppApiUrl($"updates/{Uri.EscapeDataString(version.ToString())}/download");

        public static string GetApplicationPackageCacheDirectory(bool isIncremental)
        {
            return isIncremental
                ? Environments.DirApplicationIncrementalPackageCache
                : Environments.DirApplicationFullPackageCache;
        }

        public static async Task ForceUpdate(CancellationToken cancellationToken = default)
        {
            Version latestVersion = await GetLatestVersionNumber(UpdateUrl, forceRefresh: true, cancellationToken: cancellationToken);
            if (latestVersion == new Version()) return;
            await InvokeOnUiThreadAsync(() =>
            {
                StartFullUpdate(latestVersion);
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
            if (string.IsNullOrWhiteSpace(url))
            {
                log.Warn("Failed to fetch update metadata: update service URL is empty.");
                return new Version();
            }

            if (!forceRefresh && TryGetFreshCachedLatestVersion(url, out Version freshCachedVersion))
                return freshCachedVersion;

            if (!WindowsNetworkState.IsConnectedToInternet())
            {
                if (TryGetAnyCachedLatestVersion(url, out Version offlineCachedVersion))
                {
                    log.Info("Windows reports no internet connectivity; using cached update metadata.");
                    return offlineCachedVersion;
                }

                log.Info("Skipped update metadata check because Windows reports no internet connectivity.");
                return new Version();
            }

            await _latestVersionSemaphore.WaitAsync(cancellationToken);
            try
            {
                if (!forceRefresh && TryGetFreshCachedLatestVersion(url, out freshCachedVersion))
                    return freshCachedVersion;

                string? cachedETag = GetCachedLatestVersionETag(url);
                using HttpResponseMessage response = await UpdateHttpClientProvider.SendWithTransientRetryAsync(
                    () =>
                    {
                        HttpRequestMessage request = new(HttpMethod.Get, url);
                        ApplyAuthorizationHeader(request);
                        if (!string.IsNullOrWhiteSpace(cachedETag))
                            request.Headers.TryAddWithoutValidation("If-None-Match", cachedETag);
                        return request;
                    },
                    MetadataRequestTimeout,
                    cancellationToken);
                if (response.StatusCode == HttpStatusCode.NotModified
                    && TryGetAnyCachedLatestVersion(url, out Version notModifiedVersion))
                {
                    TouchCachedLatestVersion(url);
                    return notModifiedVersion;
                }

                response.EnsureSuccessStatusCode();
                string payload = await response.Content.ReadAsStringAsync(cancellationToken);
                string versionString = ExtractVersionString(payload);
                if (!Version.TryParse(versionString.Trim(), out Version? latestVersion))
                {
                    log.Warn($"Invalid update version payload from {url}: {versionString}");
                    return TryGetAnyCachedLatestVersion(url, out Version invalidPayloadCachedVersion)
                        ? invalidPayloadCachedVersion
                        : new Version();
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
                return UseCachedVersionAfterFailure(url, $"Failed to fetch update metadata from {url}: {ex.GetBaseException().Message}");
            }
            catch (OperationCanceledException ex)
            {
                return UseCachedVersionAfterFailure(url, $"Timed out fetching update metadata from {url}: {ex.GetBaseException().Message}");
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected failure checking update metadata from {url}.", ex);
                return TryGetAnyCachedLatestVersion(url, out Version cachedVersion) ? cachedVersion : new Version();
            }
            finally
            {
                _latestVersionSemaphore.Release();
            }
        }

        private static Version UseCachedVersionAfterFailure(string url, string warning)
        {
            if (TryGetAnyCachedLatestVersion(url, out Version cachedVersion))
            {
                log.Warn($"{warning} Using the last successful response.");
                return cachedVersion;
            }

            log.Warn(warning);
            return new Version();
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
                    _cachedLatestVersionAt = DateTimeOffset.UtcNow;
            }
        }

        public static async Task<AutoUpdatePlan?> GetUpdatePlanAsync(bool forceRefresh, CancellationToken cancellationToken = default)
        {
            Version latestVersion = await GetLatestVersionNumber(UpdateUrl, forceRefresh, cancellationToken);
            if (latestVersion == new Version())
                return null;

            Version? currentVersion = CurrentVersion;
            return currentVersion == null ? null : BuildUpdatePlan(currentVersion, latestVersion);
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
                StartDownloadedFullInstaller(cachedInstaller);
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
                    StartDownloadedFullInstaller(packagePaths[0]);
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
                if (FindReadyApplicationPackagePath(downloadDirectory, fileName, plan.IsIncremental, version) != null)
                    continue;

                string canonicalPath = Path.Combine(downloadDirectory, fileName);
                _ = MoveInvalidApplicationPackageToRecovery(canonicalPath, plan.IsIncremental, version);

                string downloadUrl = plan.IsIncremental
                    ? GetIncrementalPackageDownloadUrl(version)
                    : GetReleasePackageDownloadUrl(version);
                downloads.Add(DownloadPackageAsync(downloadUrl, downloadDirectory, fileName, plan.IsIncremental, version, cancellationToken));
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
                string? packagePath = FindReadyApplicationPackagePath(downloadDirectory, GetIncrementalPackageFileName(version), isIncremental: true, version);
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
                isIncremental: false,
                version);
        }

        internal static string? FindReadyApplicationPackagePath(string directory, string canonicalFileName, bool isIncremental, Version? expectedVersion = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(directory) || string.IsNullOrWhiteSpace(canonicalFileName) || !Directory.Exists(directory))
                    return null;

                string canonicalPath = Path.Combine(directory, canonicalFileName);
                if (IsApplicationPackageFileReady(canonicalPath, isIncremental, expectedVersion))
                    return canonicalPath;

                string canonicalStem = Path.GetFileNameWithoutExtension(canonicalFileName);
                string extension = Path.GetExtension(canonicalFileName);
                return Directory.EnumerateFiles(directory, $"*{extension}", SearchOption.TopDirectoryOnly)
                    .Where(path => IsUniqueDownloadVariant(Path.GetFileNameWithoutExtension(path), canonicalStem))
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .FirstOrDefault(path => IsApplicationPackageFileReady(path, isIncremental, expectedVersion));
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
            Version expectedVersion,
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
                    && IsApplicationPackageFileReady(task.SavePath, isIncremental, expectedVersion);
                if (!ready && task.Status == DownloadStatus.Completed)
                {
                    _ = MoveInvalidApplicationPackageToRecovery(task.SavePath, isIncremental, expectedVersion);
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

        private static Version? GetIncrementalPackageVersion(string filePath)
        {
            string temporaryDirectory = Path.Combine(Path.GetTempPath(), $"ColorVisionPackageVersion-{Guid.NewGuid():N}");
            try
            {
                using ZipArchive archive = ZipFile.OpenRead(filePath);
                ZipArchiveEntry? executableEntry = archive.Entries.FirstOrDefault(entry =>
                    string.Equals(entry.FullName.Replace('\\', '/').TrimStart('/'), "ColorVision.exe", StringComparison.OrdinalIgnoreCase));
                if (executableEntry == null)
                    return null;

                Directory.CreateDirectory(temporaryDirectory);
                string executablePath = Path.Combine(temporaryDirectory, "ColorVision.exe");
                using (Stream source = executableEntry.Open())
                using (FileStream destination = File.Create(executablePath))
                    source.CopyTo(destination);
                return GetExecutableVersion(executablePath);
            }
            catch
            {
                return null;
            }
            finally
            {
                TryDeleteUpdateStage(temporaryDirectory);
            }
        }

        private static Version? GetExecutableVersion(string filePath)
        {
            try
            {
                FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(filePath);
                return ParseExecutableVersion(versionInfo.FileVersion) ?? ParseExecutableVersion(versionInfo.ProductVersion);
            }
            catch
            {
                return null;
            }
        }

        private static Version? ParseExecutableVersion(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            string versionText = new(value.TakeWhile(character => char.IsDigit(character) || character == '.').ToArray());
            return Version.TryParse(versionText.Trim('.'), out Version? version) ? version : null;
        }

        internal static bool IsApplicationPackageFileReady(string? filePath, bool isIncremental, Version? expectedVersion = null)
        {
            bool structurallyReady = isIncremental
                ? IsIncrementalPackageFileReady(filePath)
                : IsFullInstallerFileReady(filePath);
            if (!structurallyReady || expectedVersion == null)
                return structurallyReady;

            Version? packageVersion = isIncremental
                ? GetIncrementalPackageVersion(filePath!)
                : GetExecutableVersion(filePath!);
            return packageVersion == expectedVersion;
        }

        internal static string? MoveInvalidApplicationPackageToRecovery(string? filePath, bool isIncremental, Version? expectedVersion = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(filePath)
                    || !File.Exists(filePath)
                    || File.Exists(filePath + ".aria2")
                    || IsApplicationPackageFileReady(filePath, isIncremental, expectedVersion))
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

        private static void StartDownloadedFullInstaller(string downloadPath)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            RestartApplication(downloadPath);
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
            ExitUpdateHandoffState? handoffState = null;
            string? scanProtectionId = null;
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
                Directory.CreateDirectory(tempRoot);
                string batchFilePath = Path.Combine(tempRoot, "update.bat");
                string programDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string executableName = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe";
                scanProtectionId = ApplicationUpdateScanProtection.TryBegin(tempRoot);
                File.WriteAllText(batchFilePath, string.Empty);
                handoffState = ExitUpdateHandoff.Prepare(programDirectory, tempRoot);

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
                    ApplicationUpdateScanProtection.TryComplete(scanProtectionId);
                    ExitUpdateHandoff.Clear(handoffState);
                    TryDeleteUpdateStage(tempRoot);
                    return false;
                }

                ApplicationSnapshotService.Instance.CreateUpdateSnapshotIfEnabled();

                string batchContent = CreateIncrementalUpdateBatch(
                    stageDirectory,
                    tempRoot,
                    programDirectory,
                    executableName,
                    Environment.ProcessId,
                    handoffState,
                    repairServiceHost: !canUpdateWithoutElevation,
                    restartApplication: restartApplication,
                    scanProtectionId: scanProtectionId);
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
                    using Process updateProcess = ExitUpdateHandoff.Start(handoffState, startInfo);
                    if (restartApplication)
                        Environment.Exit(0);
                    return true;
                }
                catch (Exception ex)
                {
                    log.Error("Failed to start incremental update batch.", ex);
                    if (showErrors)
                        MessageBox.Show(ex.Message);
                    ApplicationUpdateScanProtection.TryComplete(scanProtectionId);
                    ExitUpdateHandoff.Clear(handoffState);
                    TryDeleteUpdateStage(tempRoot);
                    return false;
                }
            }
            catch (Exception ex)
            {
                log.Error("Failed to prepare incremental update.", ex);
                if (showErrors)
                    MessageBox.Show(ColorVision.Properties.Resources.UpdateFailed+$": {ex.Message}");
                ApplicationUpdateScanProtection.TryComplete(scanProtectionId);
                ExitUpdateHandoff.Clear(handoffState);
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
            int originalProcessId,
            ExitUpdateHandoffState handoffState,
            bool repairServiceHost,
            bool restartApplication,
            string? scanProtectionId)
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
            ExternalUpdateBatchScript.AppendSessionVariables(sb, originalProcessId, handoffState);
            sb.AppendLine($"set \"{ApplicationUpdateScanProtection.ProtectionIdEnvironmentVariable}={EscapeForBatchValue(scanProtectionId ?? string.Empty)}\"");
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
            sb.AppendLine("call :wait_for_original_process");
            ExternalUpdateBatchScript.AppendLog(sb, "Application update started.");
            sb.AppendLine("call :skip_shell_extension_files");
            sb.AppendLine("if errorlevel 1 goto fail");
            sb.AppendLine();
            sb.AppendLine("call :copy_application_files");
            sb.AppendLine("if errorlevel 1 goto fail");
            sb.AppendLine("call :repair_service_host");
            sb.AppendLine("goto success");
            sb.AppendLine();
            ExternalUpdateBatchScript.AppendWaitForOriginalProcess(sb);
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
            ExternalUpdateBatchScript.AppendLog(sb, "Application update completed.");
            sb.AppendLine("call :complete_handoff");
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();
            sb.AppendLine(":fail");
            ExternalUpdateBatchScript.AppendLog(sb, "Application update failed.");
            sb.AppendLine("call :complete_handoff");
            sb.AppendLine("call :schedule_cleanup");
            sb.AppendLine("exit /b 1");
            sb.AppendLine();
            sb.AppendLine(":complete_handoff");
            sb.AppendLine("if \"%RESTART_APPLICATION%\"==\"1\" goto launch_after_update");
            sb.AppendLine("del /f /q \"%UPDATE_MARKER%\" >nul 2>nul");
            sb.AppendLine("ping -n 2 127.0.0.1 >nul");
            sb.AppendLine("if exist \"%REOPEN_REQUEST%\" start \"\" /b \"%EXEPATH%\"");
            sb.AppendLine("del /f /q \"%REOPEN_REQUEST%\" >nul 2>nul");
            sb.AppendLine("exit /b 0");
            sb.AppendLine(":launch_after_update");
            sb.AppendLine($"set \"{ExitUpdateHandoff.LaunchTokenEnvironmentVariable}=%UPDATE_TOKEN%\"");
            sb.AppendLine("start \"\" /b \"%EXEPATH%\"");
            sb.AppendLine("ping -n 4 127.0.0.1 >nul");
            sb.AppendLine("del /f /q \"%UPDATE_MARKER%\" >nul 2>nul");
            sb.AppendLine("del /f /q \"%REOPEN_REQUEST%\" >nul 2>nul");
            sb.AppendLine("exit /b 0");
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
                ApplicationSnapshotService.Instance.CreateUpdateSnapshotIfEnabled();
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
