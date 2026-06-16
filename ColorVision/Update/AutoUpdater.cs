using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Marketplace;
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

namespace ColorVision.Update
{
    public class AutoUpdatePlan
    {
        public required Version CurrentVersion { get; init; }
        public required Version LatestVersion { get; init; }
        public required IReadOnlyList<Version> VersionsToApply { get; init; }
        public required bool IsIncremental { get; init; }
        public bool CreateBackupBeforeUpdate { get; init; } = true;

        public Version TargetVersion => VersionsToApply.Count > 0
            ? VersionsToApply[VersionsToApply.Count - 1]
            : LatestVersion;

        public bool HasMultipleSteps => VersionsToApply.Count > 1;
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

        /// <summary>
        /// 用户选择跳过的版本
        /// </summary>
        public string SkippedVersion { get => _SkippedVersion; set { _SkippedVersion = value; OnPropertyChanged(); } }
        private string _SkippedVersion = string.Empty;

        /// <summary>
        /// 增量更新前是否创建程序备份
        /// </summary>
        public bool CreateBackupBeforeIncrementalUpdate { get => _CreateBackupBeforeIncrementalUpdate; set { _CreateBackupBeforeIncrementalUpdate = value; OnPropertyChanged(); } }
        private bool _CreateBackupBeforeIncrementalUpdate = true;

    }


    public class AutoUpdater : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(AutoUpdater));
        private static readonly HttpClient _metadataClient = new() { Timeout = TimeSpan.FromSeconds(15) };
        private static AutoUpdater _instance;
        private static readonly object _locker = new();
        public static AutoUpdater GetInstance() { lock (_locker) { return _instance ??= new AutoUpdater(); } }

        public static string UpdateUrl => BuildAppApiUrl("latest-version");

        public static string CHANGELOGUrl => BuildAppApiUrl("changelog");

        public Version LatestVersion { get => _LatestVersion; set { _LatestVersion = value; OnPropertyChanged(); } }
        private Version _LatestVersion;


        public AutoUpdater()
        {
            UpdateCommand = new RelayCommand( async (e) => await CheckAndUpdate(false));
        }

        public RelayCommand UpdateCommand { get; set; }

        public static Version? CurrentVersion { get => Assembly.GetExecutingAssembly().GetName().Version; }

        public static string GetReleasePackageDownloadUrl(Version version) => BuildAppApiUrl($"releases/{Uri.EscapeDataString(version.ToString())}/download");

        public static string GetIncrementalPackageDownloadUrl(Version version) => BuildAppApiUrl($"updates/{Uri.EscapeDataString(version.ToString())}/download");

        public static string GetApplicationPackageCacheDirectory(bool isIncremental)
        {
            return isIncremental
                ? Environments.DirApplicationIncrementalPackageCache
                : Environments.DirApplicationFullPackageCache;
        }

        public static void Update(string Version, string DownloadPath) => Update(new Version(Version.Trim()), DownloadPath);

        public static void Update(Version Version, string DownloadPath,bool IsIncrement = false, Action? downloadFailedAction = null)
        {
            string downloadUrl = IsIncrement
                ? GetIncrementalPackageDownloadUrl(Version)
                : GetReleasePackageDownloadUrl(Version);
            Action<DownloadTask>? taskCallback;
            taskCallback = task =>
            {
                if (task.Status == DownloadStatus.Completed)
                {
                    UpdateApplication(task.SavePath, IsIncrement, Version);
                }
                else
                {
                    log.Error($"Download failed via IDownloadService: {downloadUrl}");
                    PostToUiThread(() => downloadFailedAction?.Invoke());
                }
            };
            DownloadWindow.ShowInstance();
            Aria2cDownloadManager.GetInstance().AddDownload(downloadUrl, DownloadPath, "1:1", taskCallback);
        }

        public async Task ForceUpdate(CancellationToken cancellationToken = default)
        {
            LatestVersion = await GetLatestVersionNumber(UpdateUrl, cancellationToken);
            if (LatestVersion == new Version()) return;
            await InvokeOnUiThreadAsync(() =>
            {
                Update(LatestVersion, GetApplicationPackageCacheDirectory(isIncremental: false));
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

        public async Task CheckAndUpdateV1(bool detection = true, bool skipped = false, CancellationToken cancellationToken = default)
        {
            // 获取本地版本
            try
            {
                // 获取服务器版本
            LatestVersion = await GetLatestVersionNumber(UpdateUrl, cancellationToken);
                log.Info(LatestVersion);
                if (LatestVersion == new Version()) return;

                var Version = Assembly.GetExecutingAssembly().GetName().Version;
                if (LatestVersion > Version)
                {
                    // 检查是否是用户已跳过的版本
                    if (skipped)
                    {
                        if (!string.IsNullOrEmpty(AutoUpdateConfig.Instance.SkippedVersion))
                        {
                            try
                            {
                                Version skippedVersion = new Version(AutoUpdateConfig.Instance.SkippedVersion);
                                if (LatestVersion == skippedVersion)
                                {
                                    return;
                                }
                            }
                            catch
                            {
                                AutoUpdateConfig.Instance.SkippedVersion = string.Empty;
                            }
                        }

                    }



                    bool IsIncrement = false;
                    if (LatestVersion.Minor == Version.Minor)
                        IsIncrement = true;
                    if (IsIncrement)
                    {
                        if (LatestVersion.Build != Version.Build)
                            LatestVersion = new Version(Version.Major, Version.Minor, Version.Build + 1, 1);
                    }



                    string CHANGELOG = await GetChangeLog(CHANGELOGUrl, cancellationToken);
                    string versionPattern = $"## \\[{LatestVersion}\\].*?\\n(.*?)(?=\\n## |$)";
                    Match match = Regex.Match(CHANGELOG ?? string.Empty, versionPattern, RegexOptions.Singleline);
                    string msg = string.Empty;
                    if (match.Success)
                    {
                        // 如果找到匹配项，提取变更日志
                        string changeLogForCurrentVersion = match.Groups[1].Value.Trim();
                        msg = $"{changeLogForCurrentVersion}{Environment.NewLine}{Environment.NewLine}{Properties.Resources.ConfirmUpdate}?{Environment.NewLine}{Environment.NewLine}{ColorVision.Properties.Resources.ClickYesToUpdateNow}";
                    }
                    else
                    {
                        msg = $"{Properties.Resources.NewVersionFound}{LatestVersion},{Properties.Resources.ConfirmUpdate}{Environment.NewLine}{ColorVision.Properties.Resources.ClickYesToUpdateNow}";
                    }

                    await InvokeOnUiThreadAsync(() =>
                    {
                        MessageBoxResult result = MessageBox1.Show(Application.Current.GetActiveWindow(), msg, $"{Properties.Resources.NewVersionFound}{LatestVersion}", MessageBoxButton.YesNoCancel);
                        if (result == MessageBoxResult.Yes)
                        {
                            Update(LatestVersion, GetApplicationPackageCacheDirectory(IsIncrement), IsIncrement);
                        }
                        else if (result == MessageBoxResult.No)
                        {
                            // 用户选择跳过该版本
                            AutoUpdateConfig.Instance.SkippedVersion = LatestVersion.ToString();
                        }
                    });
                }
                else
                {
                    if (detection)
                    {
                        await InvokeOnUiThreadAsync(() =>
                        {
                            MessageBox1.Show(Application.Current.GetActiveWindow(), Properties.Resources.CurrentVersionIsUpToDate, Version?.ToString() ?? string.Empty, MessageBoxButton.OK);
                        });
                    }
                }
            }
            catch (OperationCanceledException)
            {
                log.Debug("CheckAndUpdateV1 canceled.");
                throw;
            }
            catch (Exception ex)
            {
                LatestVersion = CurrentVersion ?? new Version();
                MessageBox.Show(ex.Message);
                log.Info(ex);
            }
        }


        public async Task CheckAndUpdate(bool detection = true, bool IsIncrement = false, CancellationToken cancellationToken = default)
        {
            // 获取本地版本
            try
            {
                // 获取服务器版本
            LatestVersion = await GetLatestVersionNumber(UpdateUrl, cancellationToken);
                if (LatestVersion == new Version()) return;

                var Version = Assembly.GetExecutingAssembly().GetName().Version;
                if (LatestVersion > Version)
                {
                    if (IsIncrement && LatestVersion.Build != Version.Build)
                    {
                        LatestVersion = new Version(LatestVersion.Major, LatestVersion.Minor, LatestVersion.Build + 1, 1);
                    }

                    string CHANGELOG = await GetChangeLog(CHANGELOGUrl, cancellationToken);
                    string versionPattern = $"## \\[{LatestVersion}\\].*?\\n(.*?)(?=\\n## |$)";
                    Match match = Regex.Match(CHANGELOG??string.Empty, versionPattern, RegexOptions.Singleline);
                    if (match.Success)
                    {
                        // 如果找到匹配项，提取变更日志
                        string changeLogForCurrentVersion = match.Groups[1].Value.Trim();

                        await InvokeOnUiThreadAsync(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(),$"{changeLogForCurrentVersion}{Environment.NewLine}{Environment.NewLine}{Properties.Resources.ConfirmUpdate}?",$"{ Properties.Resources.NewVersionFound}{ LatestVersion}", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, GetApplicationPackageCacheDirectory(IsIncrement), IsIncrement);
                            }
                        });
                    }
                    else
                    {
                        await InvokeOnUiThreadAsync(() =>
                        {
                            if (MessageBox1.Show(Application.Current.GetActiveWindow(),$"{Properties.Resources.NewVersionFound}{LatestVersion},{Properties.Resources.ConfirmUpdate}", "ColorVision", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
                            {
                                Update(LatestVersion, GetApplicationPackageCacheDirectory(IsIncrement), IsIncrement);
                            }
                        });
                    }
                }
                else
                {
                    await InvokeOnUiThreadAsync(() =>
                    {
                        if (detection)
                            MessageBox1.Show(Application.Current.GetActiveWindow(),Properties.Resources.CurrentVersionIsUpToDate, "ColorVision", MessageBoxButton.OK);
                    });

                }
            }
            catch (OperationCanceledException)
            {
                log.Debug("CheckAndUpdate canceled.");
                throw;
            }
            catch (Exception ex)
            {
                LatestVersion = CurrentVersion??new Version();
                log.Info(ex);
            }
        }

        public static async Task<string?> GetChangeLog(string url, CancellationToken cancellationToken = default)
        {
            string? versionString = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                log.Warn("Failed to fetch changelog: update service URL is empty.");
                return null;
            }

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                ApplyAuthorizationHeader(request);
                using HttpResponseMessage response = await _metadataClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                versionString = await response.Content.ReadAsStringAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (HttpRequestException ex)
            {
                log.Warn($"Failed to fetch changelog from {url}: {ex.GetBaseException().Message}");
            }
            catch (TaskCanceledException ex)
            {
                log.Warn($"Timed out fetching changelog from {url}: {ex.GetBaseException().Message}");
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected failure fetching changelog from {url}.", ex);
            }
            if (versionString == null)
            {
                return null;
            }

            return versionString;
        }



        public static async Task<Version> GetLatestVersionNumber(string url, CancellationToken cancellationToken = default)
        {
            string? versionString = null;
            if (string.IsNullOrWhiteSpace(url))
            {
                log.Warn("Failed to fetch update metadata: update service URL is empty.");
                return new Version();
            }

            try
            {
                using HttpRequestMessage request = new(HttpMethod.Get, url);
                ApplyAuthorizationHeader(request);
                using HttpResponseMessage response = await _metadataClient.SendAsync(request, cancellationToken);
                response.EnsureSuccessStatusCode();
                string payload = await response.Content.ReadAsStringAsync(cancellationToken);
                versionString = ExtractVersionString(payload);
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
            catch (TaskCanceledException ex)
            {
                log.Warn($"Timed out fetching update metadata from {url}: {ex.GetBaseException().Message}");
                return new Version();
            }
            catch (Exception ex)
            {
                log.Error($"Unexpected failure checking update metadata from {url}.", ex);
                return new Version();
            }

            // If versionString is still null, it means there was an issue with getting the ServiceVersion number
            if (versionString == null)
            {
                throw new InvalidOperationException("Unable to retrieve version number.");
            }

            return new Version(versionString.Trim());
        }

        public async Task<AutoUpdatePlan?> GetUpdatePlanAsync(CancellationToken cancellationToken = default)
        {
            LatestVersion = await GetLatestVersionNumber(UpdateUrl, cancellationToken);
            if (LatestVersion == new Version())
                return null;

            Version? currentVersion = CurrentVersion;
            if (currentVersion == null || LatestVersion <= currentVersion)
                return null;

            bool isIncrement = LatestVersion.Minor == currentVersion.Minor;
            IReadOnlyList<Version> versionsToApply = isIncrement
                ? BuildIncrementalUpdateChain(currentVersion, LatestVersion)
                : new[] { LatestVersion };

            return new AutoUpdatePlan
            {
                CurrentVersion = currentVersion,
                LatestVersion = LatestVersion,
                VersionsToApply = versionsToApply,
                IsIncremental = isIncrement,
            };
        }

        public static void StartUpdatePlan(AutoUpdatePlan plan, Action? downloadFailedAction = null)
        {
            string downloadPath = GetApplicationPackageCacheDirectory(plan.IsIncremental);

            if (plan.IsIncremental)
            {
                UpdateIncrementalChain(plan.VersionsToApply, downloadPath, plan.CreateBackupBeforeUpdate, downloadFailedAction);
                return;
            }

            Update(plan.TargetVersion, downloadPath, false, downloadFailedAction);
        }

        private static void UpdateIncrementalChain(IReadOnlyList<Version> versions, string downloadPath, bool createBackupBeforeUpdate, Action? downloadFailedAction)
        {
            if (versions.Count == 0)
                return;

            List<Version> orderedVersions = versions.Distinct().ToList();
            Dictionary<string, string> downloadedPaths = new(StringComparer.OrdinalIgnoreCase);
            object lockObj = new();
            int totalCount = orderedVersions.Count;
            int completedCount = 0;
            bool hasFailure = false;
            List<Version> versionsToDownload = new();

            foreach (Version version in orderedVersions)
            {
                string packageFileName = GetIncrementalPackageFileName(version);
                string cachedPath = Path.Combine(downloadPath, packageFileName);
                if (IsIncrementalPackageFileReady(cachedPath))
                {
                    downloadedPaths[version.ToString()] = cachedPath;
                    completedCount++;
                }
                else
                {
                    versionsToDownload.Add(version);
                }
            }

            if (versionsToDownload.Count == 0)
            {
                UpdateIncrementalApplications(orderedVersions.Select(version => downloadedPaths[version.ToString()]).ToList(), createBackupBeforeUpdate);
                return;
            }

            void FinalizeIfCompleted()
            {
                bool readyToFinalize;
                List<string>? orderedPaths = null;
                bool failed;

                lock (lockObj)
                {
                    readyToFinalize = completedCount == totalCount;
                    failed = hasFailure || downloadedPaths.Count != totalCount;
                    if (readyToFinalize && !failed)
                    {
                        orderedPaths = orderedVersions.Select(version => downloadedPaths[version.ToString()]).ToList();
                    }
                }

                if (!readyToFinalize)
                    return;

                if (failed || orderedPaths == null)
                {
                    PostToUiThread(() =>
                    {
                        downloadFailedAction?.Invoke();
                        MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Properties.Resources.UpdateFailed, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                UpdateIncrementalApplications(orderedPaths, createBackupBeforeUpdate);
            }

            DownloadWindow.ShowInstance();

            foreach (Version version in versionsToDownload)
            {
                string versionKey = version.ToString();
                string packageFileName = GetIncrementalPackageFileName(version);
                string downloadUrl = GetIncrementalPackageDownloadUrl(version);

                Aria2cDownloadManager.GetInstance().AddDownload(downloadUrl, downloadPath, "1:1", task =>
                {
                    lock (lockObj)
                    {
                        if (task.Status == DownloadStatus.Completed && IsIncrementalPackageFileReady(task.SavePath))
                        {
                            downloadedPaths[versionKey] = task.SavePath;
                        }
                        else
                        {
                            hasFailure = true;
                            log.Error($"Download failed via IDownloadService: {downloadUrl}");
                        }

                        completedCount++;
                    }

                    FinalizeIfCompleted();
                }, packageFileName);
            }
        }

        private static void UpdateIncrementalApplications(IReadOnlyList<string> downloadPaths, bool createBackupBeforeUpdate)
        {
            ConfigHandler.GetInstance().SaveConfigs();
            RestartIsIncrementApplication(downloadPaths, null, createBackupBeforeUpdate);
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
                return !string.IsNullOrWhiteSpace(filePath)
                    && File.Exists(filePath)
                    && !File.Exists(filePath + ".aria2")
                    && new FileInfo(filePath).Length > 0;
            }
            catch
            {
                return false;
            }
        }

        private static void UpdateApplication(string downloadPath, bool isIncrement, Version? targetVersion = null)
        {
            ConfigHandler.GetInstance().SaveConfigs();

            if (isIncrement)
            {
                RestartIsIncrementApplication(downloadPath);
            }
            else
            {
                RestartApplication(downloadPath, targetVersion);
            }
        }


        public static void RestartIsIncrementApplication(string downloadPath)
        {
            RestartIsIncrementApplication(new[] { downloadPath });
        }

        public static void RestartIsIncrementApplication(IEnumerable<string> downloadPaths)
        {
            RestartIsIncrementApplication(downloadPaths, null, AutoUpdateConfig.Instance.CreateBackupBeforeIncrementalUpdate);
        }

        public static void RestartIsIncrementApplication(IEnumerable<string> downloadPaths, IEnumerable<string>? pluginDownloadPaths)
        {
            RestartIsIncrementApplication(downloadPaths, pluginDownloadPaths, AutoUpdateConfig.Instance.CreateBackupBeforeIncrementalUpdate);
        }

        public static void RestartIsIncrementApplication(IEnumerable<string> downloadPaths, IEnumerable<string>? pluginDownloadPaths, bool createBackupBeforeUpdate)
        {
            // 保存数据库配置
            UpdateBackupPrepareResult? backupPrepareResult = null;
            try
            {
                List<string> applicationPackagePaths = downloadPaths?
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();
                List<string> pluginPackagePaths = pluginDownloadPaths?
                    .Where(path => !string.IsNullOrWhiteSpace(path) && File.Exists(path))
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .ToList() ?? new List<string>();

                // 解压缩 ZIP 文件到临时目录
                string tempDirectory = Path.Combine(Path.GetTempPath(), "ColorVisionUpdate");
                if (Directory.Exists(tempDirectory))
                {
                    Directory.Delete(tempDirectory, true);
                }

                bool hasAnyPackage = false;
                foreach (string downloadPath in applicationPackagePaths)
                {
                    ZipFile.ExtractToDirectory(downloadPath, tempDirectory, true);
                    hasAnyPackage = true;
                }

                if (pluginPackagePaths.Count > 0)
                {
                    string pluginsDirectory = Path.Combine(tempDirectory, "Plugins");
                    Directory.CreateDirectory(pluginsDirectory);

                    foreach (string pluginDownloadPath in pluginPackagePaths)
                    {
                        ZipFile.ExtractToDirectory(pluginDownloadPath, pluginsDirectory, true);
                        hasAnyPackage = true;
                    }
                }

                if (!hasAnyPackage)
                    throw new InvalidOperationException("Unable to locate incremental update package.");

                int skippedShellExtensionFiles = RemoveShellExtensionFilesFromUpdateStage(tempDirectory);
                if (skippedShellExtensionFiles > 0)
                {
                    log.Info($"Skipped {skippedShellExtensionFiles} shell extension file(s) during incremental update.");
                }

                // 创建批处理文件内容
                string batchFilePath = Path.Combine(tempDirectory, "update.bat");
                string programDirectory = AppDomain.CurrentDomain.BaseDirectory.TrimEnd('\\', '/');
                string executableName = Path.GetFileName(Environment.ProcessPath) ?? "ColorVision.exe";
                Version? targetVersion = TryGetTargetVersionFromPackagePaths(applicationPackagePaths);
                if (createBackupBeforeUpdate)
                {
                    ApplicationSnapshotInfo updateSnapshot = ApplicationSnapshotService.Instance.CreateUpdateSnapshot(CurrentVersion, targetVersion);
                    log.Info($"Created update snapshot before incremental update: {updateSnapshot.FilePath}");
                    backupPrepareResult = UpdateRecoveryService.Instance.PrepareBackup(
                        tempDirectory,
                        programDirectory,
                        CurrentVersion,
                        targetVersion,
                        applicationPackagePaths,
                        pluginPackagePaths,
                        updateSnapshot.FilePath);
                }
                else
                {
                    log.Info("Skipping backup before incremental update by user choice.");
                }

                string batchContent = CreateIncrementalUpdateBatch(tempDirectory, programDirectory, executableName, backupPrepareResult);

                File.WriteAllText(batchFilePath, batchContent);

                // 设置批处理文件的启动信息
                ProcessStartInfo startInfo = new()
                {
                    FileName = batchFilePath,
                    UseShellExecute = true,
                    WindowStyle = ProcessWindowStyle.Hidden // 隐藏命令行窗口
                };

                if (!Tool.HasWritePermission(AppDomain.CurrentDomain.BaseDirectory))
                {
                    startInfo.Verb = "runas"; // 请求管理员权限
                    startInfo.WindowStyle = ProcessWindowStyle.Normal;
                }
                try
                {
                    Process p = Process.Start(startInfo);
                    Environment.Exit(0);
                }
                catch (Exception ex)
                {
                    if (backupPrepareResult != null)
                        UpdateRecoveryService.Instance.MarkFailed($"Failed to start update batch: {ex.Message}");

                    log.Error("Failed to start incremental update batch.", ex);
                    MessageBox.Show(ex.Message);
                }
            }
            catch (Exception ex)
            {
                if (backupPrepareResult != null)
                    UpdateRecoveryService.Instance.MarkFailed($"Failed to prepare update batch: {ex.Message}");

                log.Error("Failed to prepare incremental update.", ex);
                MessageBox.Show(ColorVision.Properties.Resources.UpdateFailed+$": {ex.Message}");
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

        private static string CreateIncrementalUpdateBatch(string tempDirectory, string programDirectory, string executableName, UpdateBackupPrepareResult? backupPrepareResult)
        {
            string executablePath = Path.Combine(programDirectory, executableName);
            StringBuilder sb = new();
            sb.AppendLine("@echo off");
            sb.AppendLine("setlocal enabledelayedexpansion");
            sb.AppendLine("title ColorVision Incremental Updater");
            sb.AppendLine($"set \"STAGE={EscapeForBatchValue(tempDirectory)}\"");
            sb.AppendLine($"set \"TARGET={EscapeForBatchValue(programDirectory)}\"");
            sb.AppendLine($"set \"EXE={EscapeForBatchValue(executableName)}\"");
            sb.AppendLine($"set \"EXEPATH={EscapeForBatchValue(executablePath)}\"");
            if (backupPrepareResult != null)
            {
                sb.AppendLine($"set \"UPDATE_STATE_PATH={EscapeForBatchValue(backupPrepareResult.StateFilePath)}\"");
                sb.AppendLine($"set \"STATE_APPLYING={EscapeForBatchValue(backupPrepareResult.ApplyingStatePath)}\"");
                sb.AppendLine($"set \"STATE_APPLIED={EscapeForBatchValue(backupPrepareResult.AppliedStatePath)}\"");
                sb.AppendLine($"set \"STATE_FAILED={EscapeForBatchValue(backupPrepareResult.FailedStatePath)}\"");
                sb.AppendLine($"set \"BACKUP={EscapeForBatchValue(backupPrepareResult.BackupPath)}\"");
            }
            else
            {
                sb.AppendLine("set \"UPDATE_STATE_PATH=\"");
                sb.AppendLine("set \"STATE_APPLYING=\"");
                sb.AppendLine("set \"STATE_APPLIED=\"");
                sb.AppendLine("set \"STATE_FAILED=\"");
                sb.AppendLine("set \"BACKUP=\"");
            }
            sb.AppendLine();
            sb.AppendLine("taskkill /f /im \"%EXE%\" >nul 2>nul");
            sb.AppendLine("timeout /t 2 /nobreak >nul");
            sb.AppendLine("call :mark_state \"%STATE_APPLYING%\"");
            sb.AppendLine("if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine();
            sb.AppendLine("call :skip_shell_extension_files");
            sb.AppendLine("if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine();
            sb.AppendLine("call :copy_application_files");
            sb.AppendLine("if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine("goto success");
            sb.AppendLine();
            sb.AppendLine(":mark_state");
            sb.AppendLine("if \"%UPDATE_STATE_PATH%\"==\"\" exit /b 0");
            sb.AppendLine("if \"%~1\"==\"\" exit /b 1");
            sb.AppendLine("if not exist \"%~1\" exit /b 1");
            sb.AppendLine("copy /y \"%~1\" \"%UPDATE_STATE_PATH%\" >nul");
            sb.AppendLine("exit /b !ERRORLEVEL!");
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
            sb.AppendLine("if !ERRORLEVEL! EQU 0 (");
            sb.AppendLine("  robocopy \"%STAGE%\" \"%TARGET%\" *.* /E /XF update.bat ColorVision.ShellExtension* /NFL /NDL /NP /NJH /NJS /R:2 /W:1");
            sb.AppendLine("  set \"RC=!ERRORLEVEL!\"");
            sb.AppendLine("  if !RC! LSS 8 exit /b 0");
            sb.AppendLine("  exit /b !RC!");
            sb.AppendLine(")");
            sb.AppendLine("xcopy /y /e /i \"%STAGE%\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("exit /b !ERRORLEVEL!");
            sb.AppendLine();
            sb.AppendLine(":rollback");
            sb.AppendLine("if \"%BACKUP%\"==\"\" exit /b 0");
            sb.AppendLine("where robocopy >nul 2>nul");
            sb.AppendLine("if !ERRORLEVEL! EQU 0 (");
            sb.AppendLine("  if exist \"%BACKUP%\\App\" robocopy \"%BACKUP%\\App\" \"%TARGET%\" *.* /E /NFL /NDL /NP /NJH /NJS /R:2 /W:1 >nul");
            sb.AppendLine("  if exist \"%BACKUP%\\Plugins\" robocopy \"%BACKUP%\\Plugins\" \"%TARGET%\\Plugins\" *.* /E /NFL /NDL /NP /NJH /NJS /R:2 /W:1 >nul");
            sb.AppendLine("  exit /b 0");
            sb.AppendLine(")");
            sb.AppendLine("if exist \"%BACKUP%\\App\" xcopy /y /e /i \"%BACKUP%\\App\\*\" \"%TARGET%\\\" >nul");
            sb.AppendLine("if exist \"%BACKUP%\\Plugins\" xcopy /y /e /i \"%BACKUP%\\Plugins\\*\" \"%TARGET%\\Plugins\\\" >nul");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();
            sb.AppendLine(":success");
            sb.AppendLine("call :mark_state \"%STATE_APPLIED%\"");
            sb.AppendLine("if !ERRORLEVEL! NEQ 0 goto fail");
            sb.AppendLine("start \"\" \"%EXEPATH%\"");
            sb.AppendLine("start \"\" cmd /c \"ping -n 4 127.0.0.1 >nul & rd /s /q \\\"%STAGE%\\\" 2>nul\"");
            sb.AppendLine("exit /b 0");
            sb.AppendLine();
            sb.AppendLine(":fail");
            sb.AppendLine("call :mark_state \"%STATE_FAILED%\"");
            sb.AppendLine("call :rollback");
            sb.AppendLine("start \"\" \"%EXEPATH%\"");
            sb.AppendLine("exit /b 1");
            return sb.ToString();
        }

        private static Version? TryGetTargetVersionFromPackagePaths(IEnumerable<string> packagePaths)
        {
            return packagePaths
                .Select(TryParseIncrementalPackageVersion)
                .Where(version => version != null)
                .OrderBy(version => version)
                .LastOrDefault();
        }

        private static Version? TryParseIncrementalPackageVersion(string packagePath)
        {
            string fileName = Path.GetFileName(packagePath);
            Match match = Regex.Match(fileName, @"ColorVision-Update-\[(?<version>[^\]]+)\]\.(cvx|zip)$", RegexOptions.IgnoreCase);
            return match.Success && Version.TryParse(match.Groups["version"].Value, out Version? version)
                ? version
                : null;
        }

        private static string EscapeForBatchValue(string value)
        {
            return value
                .Replace("^", "^^")
                .Replace("&", "^&")
                .Replace("|", "^|")
                .Replace("<", "^<")
                .Replace(">", "^>");
        }

        public static void RestartApplication(string downloadPath)
        {
            RestartApplication(downloadPath, null);
        }

        public static void RestartApplication(string downloadPath, Version? targetVersion)
        {
            ProcessStartInfo startInfo = new();
            startInfo.UseShellExecute = true; // 必须为true才能使用Verb属性
            startInfo.WorkingDirectory = Environment.CurrentDirectory;
            startInfo.FileName = downloadPath;

            if (!Tool.HasWritePermission(AppDomain.CurrentDomain.BaseDirectory))
            {
                startInfo.Verb = "runas"; // 请求管理员权限
                startInfo.WindowStyle = ProcessWindowStyle.Normal;
            }
            try
            {
                ApplicationSnapshotInfo updateSnapshot = ApplicationSnapshotService.Instance.CreateUpdateSnapshot(CurrentVersion, targetVersion);
                log.Info($"Created update snapshot before full installer: {updateSnapshot.FilePath}");
                Process p = Process.Start(startInfo);
                Environment.Exit(0);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }


    }
}
