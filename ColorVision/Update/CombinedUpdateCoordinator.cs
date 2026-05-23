using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Update
{
    public static class CombinedUpdateCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CombinedUpdateCoordinator));
        private static readonly SemaphoreSlim _locker = new(1, 1);

        private static CombinedUpdateWorkflowConfig WorkflowConfig => CombinedUpdateWorkflowConfig.Instance;

        public static async Task StartInteractiveAsync()
        {
            await _locker.WaitAsync();
            try
            {
                AutoUpdatePlan? applicationPlan = null;
                CombinedPluginUpdatePlan? pluginPlan = null;
                UpdatePreviewDialogContext context = CreateCheckingContext();

                UpdatePreviewWindow window = new(context, async currentWindow =>
                {
                    try
                    {
                        (applicationPlan, pluginPlan) = await BuildUpdatePlansAsync(
                            includeApplicationUpdates: true,
                            includePluginUpdates: true,
                            respectSkippedVersion: false);

                        if (currentWindow.IsClosed)
                            return;

                        if (!HasUpdates(applicationPlan, pluginPlan))
                        {
                            context.CopyFrom(CreateNoUpdatesContext(pluginPlan));
                            return;
                        }

                        UpdatePreviewDialogContext loadedContext = await BuildUpdatePreviewContextAsync(applicationPlan, pluginPlan, allowSkipVersion: false, isStartupCheck: false);
                        if (currentWindow.IsClosed)
                            return;

                        context.CopyFrom(loadedContext);
                    }
                    catch
                    {
                        if (currentWindow.IsClosed)
                            return;

                        context.IsChecking = false;
                        currentWindow.DialogResult = false;
                        throw;
                    }
                })
                {
                    Owner = Application.Current.GetActiveWindow(),
                    WindowStartupLocation = WindowStartupLocation.CenterOwner,
                };

                window.ShowDialog();
                await window.InitializationTask;

                if (window.SuppressPostCheckMessage)
                    return;

                if (!HasUpdates(applicationPlan, pluginPlan))
                    return;

                if (window.ResultAction != UpdatePreviewAction.UpdateNow)
                {
                    return;
                }

                ApplySelectedPluginUpdates(pluginPlan, context);
                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: false);
            }
            catch (Exception ex)
            {
                log.Error(ex);
                WorkflowConfig.Clear();
                ConfigService.Instance.SaveConfigs();
                MessageBox.Show(Application.Current.GetActiveWindow(), ex.Message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                _locker.Release();
            }
        }

        public static async Task CheckForUpdatesOnStartupAsync()
        {
            if (Debugger.IsAttached)
                return;

            if (WorkflowConfig.IsActive)
                return;

            bool includeApplicationUpdates = AutoUpdateConfig.Instance.IsAutoUpdate;
            bool includePluginUpdates = MarketplaceWindowConfig.Instance.IsAutoUpdate;

            if (!includeApplicationUpdates && !includePluginUpdates)
                return;

            await _locker.WaitAsync();
            try
            {
                if (WorkflowConfig.IsActive)
                    return;

                (AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan) = await BuildUpdatePlansAsync(
                    includeApplicationUpdates,
                    includePluginUpdates,
                    respectSkippedVersion: true);

                if (!HasUpdates(applicationPlan, pluginPlan))
                    return;

                UpdatePreviewAction action = await ShowUpdatePreviewAsync(applicationPlan, pluginPlan, allowSkipVersion: applicationPlan != null, isStartupCheck: true);

                if (applicationPlan != null)
                {
                    if (action == UpdatePreviewAction.SkipVersion)
                    {
                        AutoUpdateConfig.Instance.SkippedVersion = applicationPlan.LatestVersion.ToString();
                        ConfigService.Instance.SaveConfigs();
                        return;
                    }

                    if (action != UpdatePreviewAction.UpdateNow)
                        return;
                }
                else if (action != UpdatePreviewAction.UpdateNow)
                {
                    return;
                }

                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: false);
            }
            catch (Exception ex)
            {
                log.Error(ex);
            }
            finally
            {
                _locker.Release();
            }
        }

        public static Task ResumeIfNeededAsync()
        {
            if (WorkflowConfig.IsActive)
            {
                ClearWorkflowState();
            }

            return Task.CompletedTask;
        }

        private static async Task<(AutoUpdatePlan? ApplicationPlan, CombinedPluginUpdatePlan? PluginPlan)> BuildUpdatePlansAsync(bool includeApplicationUpdates, bool includePluginUpdates, bool respectSkippedVersion)
        {
            AutoUpdatePlan? applicationPlan = null;
            if (includeApplicationUpdates)
            {
                applicationPlan = await AutoUpdater.GetInstance().GetUpdatePlanAsync();
                if (respectSkippedVersion && ShouldSkipApplicationPlan(applicationPlan))
                {
                    applicationPlan = null;
                }
            }

            CombinedPluginUpdatePlan? pluginPlan = null;
            if (includePluginUpdates && (applicationPlan == null || applicationPlan.IsIncremental))
            {
                Version? hostVersion = applicationPlan?.LatestVersion ?? AutoUpdater.CurrentVersion;
                if (hostVersion != null)
                {
                    pluginPlan = await MarketplaceManager.GetInstance().BuildCombinedUpdatePlanAsync(hostVersion);
                }
            }

            return (applicationPlan, pluginPlan);
        }

        private static async Task StartWorkflowAsync(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool showNoUpdatesMessage)
        {
            if (!HasUpdates(applicationPlan, pluginPlan))
            {
                if (showNoUpdatesMessage)
                {
                    ShowNoUpdatesMessage(pluginPlan);
                }
                return;
            }

            if (applicationPlan?.IsIncremental == true && pluginPlan?.HasUpdates == true)
            {
                StartIncrementalCombinedUpdate(applicationPlan, pluginPlan);
                await Task.CompletedTask;
                return;
            }

            if (applicationPlan != null)
            {
                AutoUpdater.GetInstance().StartUpdatePlan(applicationPlan, ShowUpdateDownloadFailedMessage);
                return;
            }

            await StartPluginUpdateAsync(pluginPlan!, showNoUpdatesMessage);
        }

        private static bool HasUpdates(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            return applicationPlan != null || pluginPlan?.HasUpdates == true;
        }

        private static bool ShouldSkipApplicationPlan(AutoUpdatePlan? applicationPlan)
        {
            if (applicationPlan == null || string.IsNullOrWhiteSpace(AutoUpdateConfig.Instance.SkippedVersion))
                return false;

            if (!Version.TryParse(AutoUpdateConfig.Instance.SkippedVersion.Trim(), out Version? skippedVersion))
            {
                AutoUpdateConfig.Instance.SkippedVersion = string.Empty;
                return false;
            }

            return skippedVersion == applicationPlan.LatestVersion;
        }

        private static void ClearWorkflowState()
        {
            WorkflowConfig.Clear();
            ConfigService.Instance.SaveConfigs();
        }

        private static void ShowUpdateDownloadFailedMessage()
        {
            ClearWorkflowState();
            MessageBox.Show(Application.Current.GetActiveWindow(), "更新包下载失败，请稍后重试。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private static async Task StartPluginUpdateAsync(CombinedPluginUpdatePlan pluginPlan, bool showNoUpdatesMessage)
        {
            if (!pluginPlan.HasUpdates)
            {
                if (showNoUpdatesMessage)
                {
                    ShowNoUpdatesMessage(pluginPlan);
                }
                return;
            }

            bool started = MarketplaceManager.GetInstance().StartCombinedUpdate(
                pluginPlan,
                restartArguments: null,
                noRestartAction: () =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), "插件下载未成功完成，请稍后重试。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                });

            if (!started && showNoUpdatesMessage)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), "没有可更新的插件。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await Task.CompletedTask;
        }

        private static bool StartIncrementalCombinedUpdate(AutoUpdatePlan applicationPlan, CombinedPluginUpdatePlan pluginPlan)
        {
            string downloadDir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision");
            var manager = Aria2cDownloadManager.GetInstance();
            var client = MarketplaceClient.GetInstance();
            List<Version> applicationVersions = applicationPlan.VersionsToApply.Distinct().ToList();

            if (applicationVersions.Count == 0)
                return false;

            int totalCount = applicationVersions.Count + pluginPlan.Updates.Count;
            int completedCount = 0;
            bool hasFailure = false;
            object lockObj = new();
            Dictionary<string, string> applicationPackagePaths = new(StringComparer.OrdinalIgnoreCase);
            List<string> pluginPackagePaths = new();
            List<Version> applicationPackagesToDownload = new();
            List<CombinedPluginUpdateItem> pluginsToDownload = new();

            foreach (Version version in applicationVersions)
            {
                string packageFileName = AutoUpdater.GetIncrementalPackageFileName(version);
                string cachedPath = Path.Combine(downloadDir, packageFileName);
                if (AutoUpdater.IsIncrementalPackageFileReady(cachedPath))
                {
                    applicationPackagePaths[version.ToString()] = cachedPath;
                    completedCount++;
                }
                else
                {
                    applicationPackagesToDownload.Add(version);
                }
            }

            foreach (CombinedPluginUpdateItem item in pluginPlan.Updates)
            {
                string version = item.VersionInfo.Version;
                string? expectedHash = item.VersionInfo.FileHash;
                string? existingFile = MarketplaceClient.GetExistingFileIfValid(downloadDir, item.Plugin.PackageName!, version, expectedHash);
                if (existingFile != null)
                {
                    pluginPackagePaths.Add(existingFile);
                    completedCount++;
                }
                else
                {
                    pluginsToDownload.Add(item);
                }
            }

            void FinalizeIfCompleted()
            {
                bool readyToFinalize;
                bool failed;
                List<string>? orderedApplicationPaths = null;
                List<string>? orderedPluginPaths = null;

                lock (lockObj)
                {
                    readyToFinalize = completedCount == totalCount;
                    failed = hasFailure || applicationPackagePaths.Count != applicationVersions.Count || pluginPackagePaths.Count != pluginPlan.Updates.Count;
                    if (readyToFinalize && !failed)
                    {
                        orderedApplicationPaths = applicationVersions.Select(version => applicationPackagePaths[version.ToString()]).ToList();
                        orderedPluginPaths = pluginPackagePaths.ToList();
                    }
                }

                if (!readyToFinalize)
                    return;

                Application.Current?.Dispatcher.Invoke(() =>
                {
                    if (failed || orderedApplicationPaths == null || orderedPluginPaths == null)
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), "联合更新包下载不完整，请稍后重试。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    AutoUpdater.RestartIsIncrementApplication(orderedApplicationPaths, orderedPluginPaths);
                });
            }

            if (applicationPackagesToDownload.Count == 0 && pluginsToDownload.Count == 0)
            {
                AutoUpdater.RestartIsIncrementApplication(
                    applicationVersions.Select(version => applicationPackagePaths[version.ToString()]).ToList(),
                    pluginPackagePaths.ToList());
                return true;
            }

            DownloadWindow.ShowInstance();

            foreach (Version version in applicationPackagesToDownload)
            {
                string versionKey = version.ToString();
                string packageFileName = AutoUpdater.GetIncrementalPackageFileName(version);
                string downloadUrl = $"{AutoUpdateConfig.Instance.UpdatePath}/Update/{packageFileName}";

                manager.AddDownload(downloadUrl, downloadDir, "1:1", task =>
                {
                    lock (lockObj)
                    {
                        if (task.Status == DownloadStatus.Completed && AutoUpdater.IsIncrementalPackageFileReady(task.SavePath))
                        {
                            applicationPackagePaths[versionKey] = task.SavePath;
                        }
                        else
                        {
                            hasFailure = true;
                            log.Error($"Combined incremental application download failed: {downloadUrl}");
                        }

                        completedCount++;
                    }

                    FinalizeIfCompleted();
                }, packageFileName);
            }

            foreach (CombinedPluginUpdateItem item in pluginsToDownload)
            {
                string version = item.VersionInfo.Version;
                string? expectedHash = item.VersionInfo.FileHash;
                string url = client.GetDownloadUrl(item.Plugin.PackageName!, version);
                string expectedFileName = $"{item.Plugin.PackageName}-{version}.cvxp";

                manager.AddDownload(url, downloadDir, DownloadFileConfig.Instance.Authorization, task =>
                {
                    lock (lockObj)
                    {
                        if (task.Status == DownloadStatus.Completed)
                        {
                            if (!MarketplaceClient.VerifyFileHash(task.SavePath, expectedHash))
                            {
                                hasFailure = true;
                                log.Error($"Combined incremental plugin package invalid or hash mismatch for {item.Plugin.PackageName} v{version}.");
                            }
                            else
                            {
                                pluginPackagePaths.Add(task.SavePath);
                            }
                        }
                        else
                        {
                            hasFailure = true;
                            log.Error($"Combined incremental plugin download failed for {item.Plugin.PackageName}: {task.ErrorMessage}");
                        }

                        completedCount++;
                    }

                    FinalizeIfCompleted();
                }, expectedFileName);
            }

            return true;
        }

        private static async Task<UpdatePreviewAction> ShowUpdatePreviewAsync(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool allowSkipVersion, bool isStartupCheck)
        {
            UpdatePreviewDialogContext context = await BuildUpdatePreviewContextAsync(applicationPlan, pluginPlan, allowSkipVersion, isStartupCheck);

            UpdatePreviewWindow window = new(context)
            {
                Owner = Application.Current.GetActiveWindow(),
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };

            window.ShowDialog();

            if (window.ResultAction == UpdatePreviewAction.UpdateNow)
            {
                ApplySelectedPluginUpdates(pluginPlan, context);
            }

            return window.ResultAction;
        }

        private static UpdatePreviewDialogContext CreateCheckingContext()
        {
            return new UpdatePreviewDialogContext
            {
                WindowTitle = "插件更新",
                Heading = "插件更新",
                Summary = "正在检查可用更新，请稍候。",
                CheckingTitle = "正在检查更新",
                CheckingSummary = "正在获取主程序与插件的最新版本信息，请稍候。",
                StateGlyph = "\uE895",
                HostVersionText = $"主程序版本 {AutoUpdater.CurrentVersion}",
                ConfirmButtonText = "立即更新",
                CancelButtonText = "取消",
                SecondaryButtonText = null,
                IsChecking = true,
            };
        }

        private static UpdatePreviewDialogContext CreateNoUpdatesContext(CombinedPluginUpdatePlan? pluginPlan)
        {
            string summary = pluginPlan?.SkippedIncompatiblePlugins.Count > 0
                ? "当前没有符合兼容性要求的可用更新。"
                : "当前已是最新版本。";

            string emptyMessage = pluginPlan?.SkippedIncompatiblePlugins.Count > 0
                ? $"以下插件因兼容性要求被跳过：{string.Join("、", pluginPlan.SkippedIncompatiblePlugins)}"
                : "当前主程序和插件都已经是最新版本，无需执行更新。";

            return new UpdatePreviewDialogContext
            {
                WindowTitle = "插件更新",
                Heading = "插件更新",
                Summary = summary,
                CheckingTitle = "正在检查更新",
                CheckingSummary = "正在获取主程序与插件的最新版本信息，请稍候。",
                EmptyStateTitle = "当前没有可用更新",
                EmptyStateMessage = emptyMessage,
                StateGlyph = "\uE73E",
                HostVersionText = $"主程序版本 {AutoUpdater.CurrentVersion}",
                ConfirmButtonText = "立即更新",
                CancelButtonText = "关闭",
                SecondaryButtonText = null,
                IsChecking = false,
            };
        }

        private static void ApplySelectedPluginUpdates(CombinedPluginUpdatePlan? pluginPlan, UpdatePreviewDialogContext context)
        {
            if (pluginPlan == null || !pluginPlan.HasUpdates)
                return;

            HashSet<string> selectedPluginIds = context.Items
                .Where(item => item.IsSelectable && item.IsSelected && !string.IsNullOrWhiteSpace(item.ItemId))
                .Select(item => item.ItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            pluginPlan.Updates.RemoveAll(item => !selectedPluginIds.Contains(GetPluginItemId(item)));
        }

        private static async Task<UpdatePreviewDialogContext> BuildUpdatePreviewContextAsync(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool allowSkipVersion, bool isStartupCheck)
        {
            UpdatePreviewDialogContext context = new()
            {
                WindowTitle = applicationPlan == null ? "插件更新" : "发现更新",
                Heading = applicationPlan == null && pluginPlan?.HasUpdates == true ? "插件更新" : BuildPreviewHeading(applicationPlan, pluginPlan),
                Summary = BuildDialogSummary(applicationPlan, pluginPlan),
                HostVersionText = $"主程序版本 {applicationPlan?.CurrentVersion ?? AutoUpdater.CurrentVersion}",
                ConfirmButtonText = "立即更新",
                CancelButtonText = isStartupCheck ? "稍后" : "取消",
                SecondaryButtonText = allowSkipVersion ? "跳过此版本" : null,
            };

            if (applicationPlan != null)
            {
                UpdatePreviewItem previewItem = new()
                {
                    ItemId = "application",
                    Category = applicationPlan.IsIncremental ? "主体小版本" : "主体大版本",
                    Name = "ColorVision",
                    SecondaryLabel = applicationPlan.IsIncremental
                        ? $"{applicationPlan.VersionsToApply.Count} 个主体增量包"
                        : "完整主体安装包",
                    CurrentVersion = applicationPlan.CurrentVersion.ToString(),
                    TargetVersion = applicationPlan.LatestVersion.ToString(),
                    VersionSummary = $"{applicationPlan.CurrentVersion} -> {applicationPlan.LatestVersion}",
                    Summary = BuildApplicationCardSummary(applicationPlan),
                    DetailText = await BuildApplicationDetailTextAsync(applicationPlan),
                    IsSelectable = false,
                };

                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = "当前版本",
                    Value = applicationPlan.CurrentVersion.ToString(),
                });
                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = "目标版本",
                    Value = applicationPlan.LatestVersion.ToString(),
                });
                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = "更新方式",
                    Value = applicationPlan.IsIncremental ? "主体增量包" : "完整安装包",
                });
                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = applicationPlan.IsIncremental ? "更新包数" : "更新范围",
                    Value = applicationPlan.IsIncremental
                        ? applicationPlan.VersionsToApply.Count.ToString()
                        : "主体完整更新",
                });

                context.Items.Add(previewItem);
            }

            if (pluginPlan?.HasUpdates == true)
            {
                foreach (CombinedPluginUpdateItem item in pluginPlan.Updates)
                {
                    string pluginName = GetPluginDisplayName(item);
                    string pluginItemId = GetPluginItemId(item);
                    string currentVersion = item.Plugin.AssemblyVersion?.ToString() ?? "Unknown";

                    UpdatePreviewItem previewItem = new()
                    {
                        ItemId = pluginItemId,
                        Category = "插件更新",
                        Name = pluginName,
                        SecondaryLabel = BuildPluginSecondaryLabel(item, pluginName),
                        CurrentVersion = currentVersion,
                        TargetVersion = item.VersionInfo.Version,
                        HostRequirement = string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion)
                            ? "未指定"
                            : item.VersionInfo.RequiresVersion,
                        VersionSummary = $"{currentVersion} -> {item.VersionInfo.Version}",
                        Summary = BuildPluginCardSummary(item),
                        DetailText = BuildPluginDetailText(item),
                        IsSelectable = true,
                        IsSelected = true,
                    };

                    previewItem.Facts.Add(new UpdatePreviewFact
                    {
                        Label = "插件 ID",
                        Value = item.Plugin.PackageName ?? "Unknown",
                    });
                    previewItem.Facts.Add(new UpdatePreviewFact
                    {
                        Label = "当前版本",
                        Value = currentVersion,
                    });
                    previewItem.Facts.Add(new UpdatePreviewFact
                    {
                        Label = "目标版本",
                        Value = item.VersionInfo.Version,
                    });

                    if (!string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion))
                    {
                        previewItem.Facts.Add(new UpdatePreviewFact
                        {
                            Label = "宿主要求",
                            Value = item.VersionInfo.RequiresVersion,
                        });
                    }

                    context.Items.Add(previewItem);
                }
            }

            if (context.SelectedItem == null && context.Items.Count > 0)
            {
                context.SelectedItem = context.Items[0];
            }

            return context;
        }

        private static string BuildDialogSummary(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            if (applicationPlan == null && pluginPlan?.HasUpdates == true)
            {
                StringBuilder builder = new();
                builder.Append($"发现 {pluginPlan.Updates.Count} 个插件更新，可按需选择后立即安装。");

                if (pluginPlan.SkippedIncompatiblePlugins.Count > 0)
                {
                    builder.Append($" 另有 {pluginPlan.SkippedIncompatiblePlugins.Count} 个插件因兼容性要求未显示。");
                }

                return builder.ToString();
            }

            return BuildPreviewSummary(applicationPlan, pluginPlan);
        }

        private static string BuildApplicationCardSummary(AutoUpdatePlan applicationPlan)
        {
            if (applicationPlan.IsIncremental)
                return $"将应用 {applicationPlan.VersionsToApply.Count} 个主体增量包，并与选中的插件一起完成本轮更新。";

            return "将下载完整安装包并沿用当前主程序更新流程。";
        }

        private static string BuildPluginCardSummary(CombinedPluginUpdateItem item)
        {
            string? note = !string.IsNullOrWhiteSpace(item.VersionInfo.ChangeLog)
                ? item.VersionInfo.ChangeLog
                : item.Plugin.PluginInfo?.ChangeLog;

            if (string.IsNullOrWhiteSpace(note))
            {
                note = item.Plugin.Description;
            }

            if (string.IsNullOrWhiteSpace(note))
            {
                return NormalizeUpdateSummary(item.Plugin.Description);
            }

            return NormalizeUpdateSummary(note);
        }

        private static string BuildPreviewHeading(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            if (applicationPlan != null && !applicationPlan.IsIncremental)
                return "发现主体大版本更新";

            if (applicationPlan != null && pluginPlan?.HasUpdates == true)
                return "发现主体小版本和插件更新";

            if (applicationPlan != null)
                return "发现主体小版本更新";

            return "发现插件更新";
        }

        private static string BuildPreviewSummary(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            StringBuilder builder = new();

            if (applicationPlan != null)
            {
                if (applicationPlan.IsIncremental)
                {
                    int pluginCount = pluginPlan?.Updates.Count ?? 0;
                    builder.Append($"检测到主体版本 {applicationPlan.CurrentVersion} -> {applicationPlan.LatestVersion}。");
                    if (pluginCount > 0)
                    {
                        builder.Append($" 同时发现 {pluginCount} 个插件更新，所选更新将统一下载并在完成后重启应用。");
                    }
                    else
                    {
                        builder.Append($" 本次将应用 {applicationPlan.VersionsToApply.Count} 个主体增量包，并在完成后重启应用。");
                    }
                }
                else
                {
                    builder.Append($"检测到主体大版本更新 {applicationPlan.CurrentVersion} -> {applicationPlan.LatestVersion}。本次将按现有主体安装流程更新。");
                }
            }
            else if (pluginPlan?.HasUpdates == true)
            {
                builder.Append($"检测到 {pluginPlan.Updates.Count} 个插件可更新，本次会批量下载并安装所选插件。");
            }

            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
            {
                builder.Append($" 另有 {pluginPlan.SkippedIncompatiblePlugins.Count} 个插件因兼容性要求被跳过。");
            }

            return builder.ToString();
        }

        private static string BuildPluginSecondaryLabel(CombinedPluginUpdateItem item, string pluginName)
        {
            string[] candidates =
            {
                item.Plugin.PackageName ?? string.Empty,
                item.Plugin.AssemblyName ?? string.Empty,
            };

            return candidates
                .Select(candidate => candidate?.Trim() ?? string.Empty)
                .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate)
                    && !string.Equals(candidate, pluginName, StringComparison.OrdinalIgnoreCase))
                ?? string.Empty;
        }

        private static string NormalizeUpdateSummary(string? text)
        {
            const string fallback = "包含兼容性与稳定性更新。";
            const int maxLength = 160;

            if (string.IsNullOrWhiteSpace(text))
                return fallback;

            string normalized = text
                .Replace("\r\n", "\n")
                .Replace('\r', '\n')
                .Trim();

            List<string> lines = normalized
                .Split('\n')
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Where(line => !line.StartsWith('#'))
                .Where(line => !line.Equals("CHANGELOG", StringComparison.OrdinalIgnoreCase))
                .Where(line => !line.Equals("Changelog", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (lines.Count == 0)
                return fallback;

            List<string> paragraph = new();
            foreach (string line in lines)
            {
                if (paragraph.Count > 0 && IsLikelyParagraphBoundary(line))
                    break;

                paragraph.Add(line.TrimStart('-', '*', ' '));
            }

            string result = string.Join(" ", paragraph).Trim();
            if (string.IsNullOrWhiteSpace(result))
                return fallback;

            if (result.Length > maxLength)
                result = result[..maxLength].TrimEnd() + "…";

            return result;
        }

        private static bool IsLikelyParagraphBoundary(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
                return true;

            return line.StartsWith("- ", StringComparison.Ordinal)
                || line.StartsWith("* ", StringComparison.Ordinal)
                || line.StartsWith("##", StringComparison.Ordinal)
                || line.StartsWith("###", StringComparison.Ordinal);
        }

        private static string GetPluginDisplayName(CombinedPluginUpdateItem item)
        {
            return item.Plugin.Name
                ?? item.Plugin.PluginInfo?.Name
                ?? item.Plugin.PackageName
                ?? item.Plugin.AssemblyName
                ?? "未命名插件";
        }

        private static string GetPluginItemId(CombinedPluginUpdateItem item)
        {
            return item.Plugin.PackageName
                ?? item.Plugin.AssemblyName
                ?? GetPluginDisplayName(item);
        }

        private static string BuildPluginNamesPreview(IEnumerable<CombinedPluginUpdateItem> items)
        {
            List<string> names = items
                .Select(GetPluginDisplayName)
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (names.Count <= 4)
                return string.Join("、", names);

            return $"{string.Join("、", names.Take(4))} 等 {names.Count} 个插件";
        }

        private static async Task<string> BuildApplicationDetailTextAsync(AutoUpdatePlan applicationPlan)
        {
            StringBuilder builder = new();
            builder.AppendLine($"当前版本：{applicationPlan.CurrentVersion}");
            builder.AppendLine($"目标版本：{applicationPlan.LatestVersion}");

            if (applicationPlan.IsIncremental)
            {
                builder.AppendLine($"增量链：{string.Join(" -> ", applicationPlan.VersionsToApply)}");
                builder.AppendLine("执行方式：主体增量包与插件包会先全部下载，再一次性覆盖更新。");
            }
            else
            {
                builder.AppendLine("执行方式：下载完整安装包，按原来的主体更新流程安装。\n插件不参与本次自动更新。");
            }

            IReadOnlyList<ChangeLogEntry> changeLogEntries = await TryLoadRemoteChangeLogEntriesAsync();
            List<ChangeLogEntry> relevantEntries = SelectRelevantChangelogEntries(applicationPlan, changeLogEntries);
            if (relevantEntries.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine("更新说明：");
                builder.Append(BuildChangeLogText(relevantEntries));
            }

            return builder.ToString().TrimEnd();
        }

        private static async Task<IReadOnlyList<ChangeLogEntry>> TryLoadRemoteChangeLogEntriesAsync()
        {
            try
            {
                string? changeLogText = await AutoUpdater.GetInstance().GetChangeLog(AutoUpdater.GetInstance().CHANGELOGUrl);
                if (string.IsNullOrWhiteSpace(changeLogText))
                    return Array.Empty<ChangeLogEntry>();

                return ChangelogWindow.Parse(changeLogText).ToList();
            }
            catch (Exception ex)
            {
                log.Warn($"Load remote changelog failed: {ex.Message}");
                return Array.Empty<ChangeLogEntry>();
            }
        }

        private static List<ChangeLogEntry> SelectRelevantChangelogEntries(AutoUpdatePlan applicationPlan, IReadOnlyList<ChangeLogEntry> entries)
        {
            List<(ChangeLogEntry Entry, Version Version)> parsedEntries = entries
                .Select(entry => (Entry: entry, Version: TryParseVersion(entry.Version)))
                .Where(item => item.Version != null)
                .Select(item => (item.Entry, item.Version!))
                .ToList();

            if (applicationPlan.IsIncremental)
            {
                HashSet<string> targetVersions = applicationPlan.VersionsToApply
                    .Select(version => version.ToString())
                    .ToHashSet(StringComparer.OrdinalIgnoreCase);

                return parsedEntries
                    .Where(item => targetVersions.Contains(item.Entry.Version))
                    .OrderBy(item => item.Version)
                    .Select(item => item.Entry)
                    .ToList();
            }

            return parsedEntries
                .Where(item => item.Version > applicationPlan.CurrentVersion && item.Version <= applicationPlan.LatestVersion)
                .OrderBy(item => item.Version)
                .Select(item => item.Entry)
                .ToList();
        }

        private static Version? TryParseVersion(string? value)
        {
            return Version.TryParse(value, out Version? version) ? version : null;
        }

        private static string BuildChangeLogText(IEnumerable<ChangeLogEntry> entries)
        {
            StringBuilder builder = new();
            foreach (ChangeLogEntry entry in entries)
            {
                builder.AppendLine($"## {entry.Version}  {entry.ReleaseDate:yyyy/MM/dd}");
                if (!string.IsNullOrWhiteSpace(entry.ChangeLog))
                {
                    builder.AppendLine(entry.ChangeLog.Trim());
                }
                builder.AppendLine();
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildPluginDetailText(CombinedPluginUpdateItem item)
        {
            StringBuilder builder = new();
            string pluginName = GetPluginDisplayName(item);

            builder.AppendLine($"插件：{pluginName}");
            builder.AppendLine($"插件 ID：{item.Plugin.PackageName}");
            builder.AppendLine($"当前版本：{item.Plugin.AssemblyVersion?.ToString() ?? "Unknown"}");
            builder.AppendLine($"目标版本：{item.VersionInfo.Version}");

            if (!string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion))
            {
                builder.AppendLine($"宿主要求：{item.VersionInfo.RequiresVersion}");
            }

            if (!string.IsNullOrWhiteSpace(item.Plugin.Description))
            {
                builder.AppendLine();
                builder.AppendLine("插件说明：");
                builder.AppendLine(item.Plugin.Description.Trim());
            }

            string? changeLog = !string.IsNullOrWhiteSpace(item.VersionInfo.ChangeLog)
                ? item.VersionInfo.ChangeLog
                : item.Plugin.PluginInfo?.ChangeLog;

            if (!string.IsNullOrWhiteSpace(changeLog))
            {
                builder.AppendLine();
                builder.AppendLine("版本说明：");
                builder.AppendLine(changeLog.Trim());
            }

            return builder.ToString().TrimEnd();
        }

        private static void ShowNoUpdatesMessage(CombinedPluginUpdatePlan? pluginPlan)
        {
            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
            {
                string skippedPlugins = string.Join("、", pluginPlan.SkippedIncompatiblePlugins);
                MessageBox.Show(Application.Current.GetActiveWindow(), $"当前没有可执行的联合更新。以下插件因兼容性要求被跳过：{skippedPlugins}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox1.Show(Application.Current.GetActiveWindow(), "当前主体和插件都已经是最新版本。", "ColorVision", MessageBoxButton.OK);
        }
    }

    public class CombinedUpdateInitializer : MainWindowInitializedBase
    {
        public override int Order { get => 0; set { } }

        public override Task Initialize() => CombinedUpdateCoordinator.ResumeIfNeededAsync();
    }
}
