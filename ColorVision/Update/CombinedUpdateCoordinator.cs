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

        public static async Task ResumeIfNeededAsync()
        {
            if (WorkflowConfig.IsActive)
            {
                ClearWorkflowState();
            }

            try
            {
                await UpdateRecoveryService.Instance.ResumeOrRollbackIfNeededAsync(Application.Current?.GetActiveWindow());
            }
            catch (Exception ex)
            {
                log.Error("Update recovery resume failed.", ex);
            }
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
                AutoUpdater.StartUpdatePlan(applicationPlan, ShowUpdateDownloadFailedMessage);
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
            MessageBox.Show(Application.Current.GetActiveWindow(), UpdatePreviewText.PackageDownloadFailed, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                    MessageBox.Show(Application.Current.GetActiveWindow(), UpdatePreviewText.PluginDownloadFailed, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                });

            if (!started && showNoUpdatesMessage)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), UpdatePreviewText.NoPluginUpdates, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
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
                        MessageBox.Show(Application.Current.GetActiveWindow(), UpdatePreviewText.CombinedPackageIncomplete, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                string downloadUrl = AutoUpdater.GetIncrementalPackageDownloadUrl(version);

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
                WindowTitle = UpdatePreviewText.WindowTitle,
                Heading = UpdatePreviewText.CheckingHeading,
                Summary = UpdatePreviewText.CheckingSummary,
                CheckingTitle = UpdatePreviewText.ScanningTitle,
                CheckingSummary = UpdatePreviewText.CheckingSummary,
                StateGlyph = "\uE895",
                HostVersionText = UpdatePreviewText.HostVersion(AutoUpdater.CurrentVersion?.ToString() ?? UpdatePreviewText.UnknownVersion),
                ConfirmButtonText = UpdatePreviewText.UpdateNowButtonText,
                CancelButtonText = UpdatePreviewText.CancelButtonText,
                SecondaryButtonText = null,
                WindowWidth = 780,
                WindowHeight = 360,
                WindowMinWidth = 760,
                WindowMinHeight = 320,
                WindowMaxHeight = 380,
                WindowAutoSizeHeight = false,
                IsChecking = true,
            };
        }

        private static UpdatePreviewDialogContext CreateNoUpdatesContext(CombinedPluginUpdatePlan? pluginPlan)
        {
            string emptyMessage = pluginPlan?.SkippedIncompatiblePlugins.Count > 0
                ? UpdatePreviewText.SkippedIncompatibleUpdates(string.Join(UpdatePreviewText.ListSeparator, pluginPlan.SkippedIncompatiblePlugins))
                : UpdatePreviewText.NoInstallableUpdatesMessage;

            return new UpdatePreviewDialogContext
            {
                WindowTitle = UpdatePreviewText.WindowTitle,
                Heading = UpdatePreviewText.AlreadyLatestHeading,
                Summary = UpdatePreviewText.DialogSummaryNoUpdates,
                CheckingTitle = UpdatePreviewText.ScanningTitle,
                CheckingSummary = UpdatePreviewText.CheckingSummary,
                EmptyStateTitle = UpdatePreviewText.AlreadyLatestHeading,
                EmptyStateMessage = emptyMessage,
                StateGlyph = "\uE73E",
                HostVersionText = UpdatePreviewText.HostVersion(AutoUpdater.CurrentVersion?.ToString() ?? UpdatePreviewText.UnknownVersion),
                ConfirmButtonText = UpdatePreviewText.UpdateNowButtonText,
                CancelButtonText = UpdatePreviewText.CloseButtonText,
                SecondaryButtonText = null,
                WindowWidth = 780,
                WindowHeight = 360,
                WindowMinWidth = 760,
                WindowMinHeight = 320,
                WindowMaxHeight = 380,
                WindowAutoSizeHeight = false,
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
                WindowTitle = UpdatePreviewText.WindowTitle,
                Heading = BuildPreviewHeading(applicationPlan, pluginPlan),
                Summary = BuildDialogSummary(applicationPlan, pluginPlan),
                HostVersionText = UpdatePreviewText.HostVersion((applicationPlan?.CurrentVersion ?? AutoUpdater.CurrentVersion)?.ToString() ?? UpdatePreviewText.UnknownVersion),
                ConfirmButtonText = UpdatePreviewText.UpdateNowButtonText,
                CancelButtonText = isStartupCheck ? UpdatePreviewText.LaterButtonText : UpdatePreviewText.CancelButtonText,
                SecondaryButtonText = allowSkipVersion ? UpdatePreviewText.SkipVersionButtonText : null,
            };

            if (applicationPlan != null)
            {
                UpdatePreviewItem previewItem = new()
                {
                    ItemId = "application",
                    Kind = applicationPlan.IsIncremental ? UpdatePreviewItemKind.ApplicationIncremental : UpdatePreviewItemKind.Application,
                    Category = applicationPlan.IsIncremental ? UpdatePreviewText.ApplicationIncrementalCategory : UpdatePreviewText.ApplicationUpdateCategory,
                    Name = "ColorVision",
                    SecondaryLabel = applicationPlan.IsIncremental
                        ? UpdatePreviewText.ApplicationIncrementalPackages(applicationPlan.VersionsToApply.Count)
                        : UpdatePreviewText.ApplicationFullPackageLabel,
                    CurrentVersion = applicationPlan.CurrentVersion.ToString(),
                    TargetVersion = applicationPlan.LatestVersion.ToString(),
                    VersionSummary = $"{applicationPlan.CurrentVersion} -> {applicationPlan.LatestVersion}",
                    Summary = BuildApplicationCardSummary(applicationPlan),
                    DetailText = await BuildApplicationDetailTextAsync(applicationPlan),
                    IsSelectable = false,
                };

                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = Properties.Resources.UpdateCurrentVersion,
                    Value = applicationPlan.CurrentVersion.ToString(),
                });
                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = Properties.Resources.UpdateTargetVersion,
                    Value = applicationPlan.LatestVersion.ToString(),
                });
                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = Properties.Resources.UpdateMethod,
                    Value = applicationPlan.IsIncremental ? Properties.Resources.UpdateIncrementalPackage : Properties.Resources.UpdateFullPackage,
                });
                previewItem.Facts.Add(new UpdatePreviewFact
                {
                    Label = applicationPlan.IsIncremental ? Properties.Resources.UpdatePackageCount : Properties.Resources.UpdateScope,
                    Value = applicationPlan.IsIncremental
                        ? applicationPlan.VersionsToApply.Count.ToString()
                        : Properties.Resources.UpdateFullApplicationUpdate,
                });

                context.Items.Add(previewItem);
            }

            if (pluginPlan?.HasUpdates == true)
            {
                foreach (CombinedPluginUpdateItem item in pluginPlan.Updates)
                {
                    string pluginName = GetPluginDisplayName(item);
                    string pluginItemId = GetPluginItemId(item);
                    string currentVersion = item.Plugin.AssemblyVersion?.ToString() ?? UpdatePreviewText.UnknownVersion;

                    UpdatePreviewItem previewItem = new()
                    {
                        ItemId = pluginItemId,
                        Kind = UpdatePreviewItemKind.Plugin,
                        Category = UpdatePreviewText.PluginUpdateCategory,
                        Name = pluginName,
                        SecondaryLabel = BuildPluginSecondaryLabel(item, pluginName),
                        CurrentVersion = currentVersion,
                        TargetVersion = item.VersionInfo.Version,
                        HostRequirement = string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion)
                            ? string.Empty
                            : item.VersionInfo.RequiresVersion,
                        VersionSummary = $"{currentVersion} -> {item.VersionInfo.Version}",
                        Summary = BuildPluginCardSummary(item),
                        DetailText = BuildPluginDetailText(item),
                        IsSelectable = true,
                        IsSelected = true,
                    };

                    previewItem.Facts.Add(new UpdatePreviewFact
                    {
                        Label = Properties.Resources.UpdatePluginId,
                        Value = item.Plugin.PackageName ?? "Unknown",
                    });
                    previewItem.Facts.Add(new UpdatePreviewFact
                    {
                        Label = Properties.Resources.UpdateCurrentVersion,
                        Value = currentVersion,
                    });
                    previewItem.Facts.Add(new UpdatePreviewFact
                    {
                        Label = Properties.Resources.UpdateTargetVersion,
                        Value = item.VersionInfo.Version,
                    });

                    if (!string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion))
                    {
                        previewItem.Facts.Add(new UpdatePreviewFact
                        {
                            Label = Properties.Resources.UpdateHostRequirement,
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

            ApplyWindowMetrics(context);

            return context;
        }

        private static string BuildDialogSummary(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            int updateCount = (applicationPlan != null ? 1 : 0) + (pluginPlan?.Updates.Count ?? 0);
            if (updateCount == 0)
                return UpdatePreviewText.DialogSummaryNoUpdates;

            List<string> updateKinds = BuildUpdateKinds(applicationPlan, pluginPlan);
            StringBuilder builder = new();

            if (updateKinds.Count > 1)
                builder.Append(UpdatePreviewText.DialogSummaryWithKinds(updateCount, string.Join(UpdatePreviewText.ListSeparator, updateKinds)));
            else
                builder.Append(UpdatePreviewText.DialogSummaryDefault(updateCount));

            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
                builder.Append($" {UpdatePreviewText.DialogSummarySkippedCount(pluginPlan.SkippedIncompatiblePlugins.Count)}");

            return builder.ToString();
        }

        private static string BuildApplicationCardSummary(AutoUpdatePlan applicationPlan)
        {
            if (applicationPlan.IsIncremental)
                return UpdatePreviewText.ApplicationCardSummaryIncremental(applicationPlan.VersionsToApply.Count);

            return UpdatePreviewText.ApplicationCardSummaryFull;
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
            return applicationPlan != null || pluginPlan?.HasUpdates == true
                ? UpdatePreviewText.FoundUpdatesHeading
                : UpdatePreviewText.WindowTitle;
        }

        private static string BuildPreviewSummary(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            return BuildDialogSummary(applicationPlan, pluginPlan);
        }

        private static List<string> BuildUpdateKinds(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            List<string> kinds = new();

            if (applicationPlan != null)
                kinds.Add(UpdatePreviewText.UpdateKindApplication);

            if (pluginPlan?.HasUpdates == true)
                kinds.Add(UpdatePreviewText.UpdateKindPlugin);

            return kinds;
        }

        private static void ApplyWindowMetrics(UpdatePreviewDialogContext context)
        {
            context.WindowWidth = 900;
            context.WindowHeight = context.Items.Count switch
            {
                <= 1 => 520d,
                <= 4 => 600d,
                _ => 680d,
            };
            context.WindowMinWidth = 860;
            context.WindowMinHeight = 460;
            context.WindowMaxHeight = 720;
            context.WindowAutoSizeHeight = true;
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
            string fallback = Properties.Resources.UpdateCompatibilityStability;
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
                ?? Properties.Resources.UpdateUnnamedPlugin;
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

            return $"{string.Join("、", names.Take(4))} {string.Format(Properties.Resources.UpdatePluginCount, names.Count)}";
        }

        private static async Task<string> BuildApplicationDetailTextAsync(AutoUpdatePlan applicationPlan)
        {
            StringBuilder builder = new();
            builder.AppendLine($"{Properties.Resources.UpdateCurrentVersionLabel}{applicationPlan.CurrentVersion}");
            builder.AppendLine($"{Properties.Resources.UpdateTargetVersionLabel}{applicationPlan.LatestVersion}");

            if (applicationPlan.IsIncremental)
            {
                builder.AppendLine(string.Format(Properties.Resources.UpdateIncrementalChain, string.Join(" -> ", applicationPlan.VersionsToApply)));
                builder.AppendLine(Properties.Resources.UpdateExecutionIncremental);
            }
            else
            {
                builder.AppendLine(Properties.Resources.UpdateExecutionFull);
            }

            IReadOnlyList<ChangeLogEntry> changeLogEntries = await TryLoadRemoteChangeLogEntriesAsync();
            List<ChangeLogEntry> relevantEntries = SelectRelevantChangelogEntries(applicationPlan, changeLogEntries);
            if (relevantEntries.Count > 0)
            {
                builder.AppendLine();
                builder.AppendLine(Properties.Resources.UpdateNotes);
                builder.Append(BuildChangeLogText(relevantEntries));
            }

            return builder.ToString().TrimEnd();
        }

        private static async Task<IReadOnlyList<ChangeLogEntry>> TryLoadRemoteChangeLogEntriesAsync()
        {
            try
            {
                string? changeLogText = await AutoUpdater.GetChangeLog(AutoUpdater.CHANGELOGUrl);
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

            builder.AppendLine($"{Properties.Resources.UpdatePlugin}{pluginName}");
            builder.AppendLine($"{Properties.Resources.UpdatePluginIdLabel}{item.Plugin.PackageName}");
            builder.AppendLine($"{Properties.Resources.UpdateCurrentVersionLabel}{item.Plugin.AssemblyVersion?.ToString() ?? "Unknown"}");
            builder.AppendLine($"{Properties.Resources.UpdateTargetVersionLabel}{item.VersionInfo.Version}");

            if (!string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion))
            {
                builder.AppendLine($"{Properties.Resources.UpdateHostRequirementLabel}{item.VersionInfo.RequiresVersion}");
            }

            if (!string.IsNullOrWhiteSpace(item.Plugin.Description))
            {
                builder.AppendLine();
                builder.AppendLine(Properties.Resources.UpdatePluginDescription);
                builder.AppendLine(item.Plugin.Description.Trim());
            }

            string? changeLog = !string.IsNullOrWhiteSpace(item.VersionInfo.ChangeLog)
                ? item.VersionInfo.ChangeLog
                : item.Plugin.PluginInfo?.ChangeLog;

            if (!string.IsNullOrWhiteSpace(changeLog))
            {
                builder.AppendLine();
                builder.AppendLine(Properties.Resources.UpdateVersionNotes);
                builder.AppendLine(changeLog.Trim());
            }

            return builder.ToString().TrimEnd();
        }

        private static void ShowNoUpdatesMessage(CombinedPluginUpdatePlan? pluginPlan)
        {
            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
            {
                string skippedPlugins = string.Join(UpdatePreviewText.ListSeparator, pluginPlan.SkippedIncompatiblePlugins);
                MessageBox.Show(Application.Current.GetActiveWindow(), UpdatePreviewText.ShowNoUpdatesSkippedMessage(skippedPlugins), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox1.Show(Application.Current.GetActiveWindow(), UpdatePreviewText.ShowNoUpdatesLatestMessage, "ColorVision", MessageBoxButton.OK);
        }
    }

    public class CombinedUpdateInitializer : MainWindowInitializedBase
    {
        public override int Order { get => 0; set { } }

        public override Task Initialize() => CombinedUpdateCoordinator.ResumeIfNeededAsync();
    }
}
