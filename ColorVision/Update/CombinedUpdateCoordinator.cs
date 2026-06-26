#pragma warning disable CA1863
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.Desktop.Download;
using ColorVision.UI.Desktop.Marketplace;
using ColorVision.UI.Marketplace;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Resources = ColorVision.Properties.Resources;

namespace ColorVision.Update
{
    public static class CombinedUpdateCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CombinedUpdateCoordinator));
        private static readonly SemaphoreSlim _locker = new(1, 1);
        private static AutoUpdatePlan? _pendingStartupApplicationPlan;
        private static CombinedPluginUpdatePlan? _pendingStartupPluginPlan;

        private static CombinedUpdateWorkflowConfig WorkflowConfig => CombinedUpdateWorkflowConfig.Instance;

        public static event EventHandler? PendingStartupUpdateChanged;

        public static bool HasPendingStartupUpdate => HasUpdates(_pendingStartupApplicationPlan, _pendingStartupPluginPlan);

        private readonly struct UpdatePreviewResult
        {
            public UpdatePreviewResult(UpdatePreviewAction action, ApplicationUpdateMode applicationUpdateMode, bool createBackupBeforeIncrementalUpdate)
            {
                Action = action;
                ApplicationUpdateMode = applicationUpdateMode;
                CreateBackupBeforeIncrementalUpdate = createBackupBeforeIncrementalUpdate;
            }

            public UpdatePreviewAction Action { get; }

            public ApplicationUpdateMode ApplicationUpdateMode { get; }

            public bool CreateBackupBeforeIncrementalUpdate { get; }
        }

        public static async Task StartInteractiveAsync(CancellationToken cancellationToken = default)
        {
            await _locker.WaitAsync(cancellationToken);
            try
            {
                AutoUpdatePlan? applicationPlan = null;
                CombinedPluginUpdatePlan? pluginPlan = null;
                UpdatePreviewDialogContext context = CreateCheckingContext();
                using CancellationTokenSource previewCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                UpdatePreviewWindow window = new(context, async currentWindow =>
                {
                    try
                    {
                        (applicationPlan, pluginPlan) = await BuildUpdatePlansAsync(
                            includeApplicationUpdates: true,
                            includePluginUpdates: true,
                            respectSkippedVersion: false,
                            previewCancellation.Token);

                        if (currentWindow.IsClosed)
                            return;

                        if (!HasUpdates(applicationPlan, pluginPlan))
                        {
                            context.CopyFrom(CreateNoUpdatesContext(pluginPlan));
                            return;
                        }

                        UpdatePreviewDialogContext loadedContext = BuildUpdatePreviewContext(applicationPlan, pluginPlan, allowSkipVersion: false, isStartupCheck: false);
                        if (currentWindow.IsClosed)
                            return;

                        context.CopyFrom(loadedContext);
                    }
                    catch (OperationCanceledException)
                    {
                        if (!currentWindow.IsClosed)
                        {
                            context.IsChecking = false;
                        }
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

                window.Closed += (_, _) => previewCancellation.Cancel();
                window.ShowDialog();
                await window.InitializationTask;

                if (window.SuppressPostCheckMessage)
                    return;

                if (!HasUpdates(applicationPlan, pluginPlan))
                {
                    ClearPendingStartupUpdate();
                    return;
                }

                if (window.ResultAction != UpdatePreviewAction.UpdateNow)
                {
                    return;
                }

                ApplySelectedApplicationChoices(
                    ref applicationPlan,
                    GetSelectedApplicationUpdateMode(context),
                    GetSelectedCreateBackupBeforeIncrementalUpdate(context));
                ApplySelectedPluginUpdates(pluginPlan, context);
                ClearPendingStartupUpdate();
                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: false);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Interactive update check canceled.");
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

        public static async Task CheckForUpdatesOnStartupAsync(CancellationToken cancellationToken = default)
        {
            if (Debugger.IsAttached)
                return;

            if (WorkflowConfig.IsActive)
                return;

            bool includeApplicationUpdates = AutoUpdateConfig.Instance.IsAutoUpdate;
            bool includePluginUpdates = MarketplaceWindowConfig.Instance.IsAutoUpdate;

            if (!includeApplicationUpdates && !includePluginUpdates)
                return;

            await _locker.WaitAsync(cancellationToken);
            try
            {
                if (WorkflowConfig.IsActive)
                    return;

                (AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan) = await BuildUpdatePlansAsync(
                    includeApplicationUpdates,
                    includePluginUpdates,
                    respectSkippedVersion: true,
                    cancellationToken);

                if (!HasUpdates(applicationPlan, pluginPlan))
                {
                    ClearPendingStartupUpdate();
                    return;
                }

                SetPendingStartupUpdate(applicationPlan, pluginPlan);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Startup update check canceled.");
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

        public static async Task OpenPendingStartupUpdateAsync(CancellationToken cancellationToken = default)
        {
            await _locker.WaitAsync(cancellationToken);
            try
            {
                AutoUpdatePlan? applicationPlan = _pendingStartupApplicationPlan;
                CombinedPluginUpdatePlan? pluginPlan = _pendingStartupPluginPlan;

                if (!HasUpdates(applicationPlan, pluginPlan))
                {
                    ClearPendingStartupUpdate();
                    return;
                }

                UpdatePreviewResult previewResult = await ShowUpdatePreviewAsync(applicationPlan, pluginPlan, allowSkipVersion: applicationPlan != null, isStartupCheck: true);
                UpdatePreviewAction action = previewResult.Action;

                if (applicationPlan != null)
                {
                    if (action == UpdatePreviewAction.SkipVersion)
                    {
                        AutoUpdateConfig.Instance.SkippedVersion = applicationPlan.LatestVersion.ToString();
                        ConfigService.Instance.SaveConfigs();
                        ClearPendingStartupUpdate();
                        return;
                    }

                    if (action != UpdatePreviewAction.UpdateNow)
                        return;
                }
                else if (action != UpdatePreviewAction.UpdateNow)
                {
                    return;
                }

                ApplySelectedApplicationChoices(
                    ref applicationPlan,
                    previewResult.ApplicationUpdateMode,
                    previewResult.CreateBackupBeforeIncrementalUpdate);
                ClearPendingStartupUpdate();
                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: false);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Pending startup update preview canceled.");
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

        private static void SetPendingStartupUpdate(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            bool hadUpdates = HasPendingStartupUpdate;
            _pendingStartupApplicationPlan = applicationPlan;
            _pendingStartupPluginPlan = pluginPlan;
            if (hadUpdates != HasPendingStartupUpdate || HasPendingStartupUpdate)
                PendingStartupUpdateChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ClearPendingStartupUpdate()
        {
            bool hadUpdates = HasPendingStartupUpdate;
            if (_pendingStartupApplicationPlan == null && _pendingStartupPluginPlan == null)
                return;

            _pendingStartupApplicationPlan = null;
            _pendingStartupPluginPlan = null;
            if (hadUpdates)
                PendingStartupUpdateChanged?.Invoke(null, EventArgs.Empty);
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

        private static async Task<(AutoUpdatePlan? ApplicationPlan, CombinedPluginUpdatePlan? PluginPlan)> BuildUpdatePlansAsync(bool includeApplicationUpdates, bool includePluginUpdates, bool respectSkippedVersion, CancellationToken cancellationToken = default)
        {
            AutoUpdatePlan? applicationPlan = null;
            if (includeApplicationUpdates)
            {
                applicationPlan = await AutoUpdater.GetInstance().GetUpdatePlanAsync(cancellationToken);
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
                    pluginPlan = await MarketplaceManager.GetInstance().BuildCombinedUpdatePlanAsync(hostVersion, cancellationToken);
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
                StartIncrementalCombinedUpdate(applicationPlan, pluginPlan, applicationPlan.CreateBackupBeforeUpdate);
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
            MessageBox.Show(Application.Current.GetActiveWindow(), Resources.UpdatePreviewPackageDownloadFailed, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
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
                log.Error("Combined update UI action failed.", ex);
            }
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
                    MessageBox.Show(Application.Current.GetActiveWindow(), Resources.UpdatePreviewPluginDownloadFailed, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                });

            if (!started && showNoUpdatesMessage)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Resources.UpdatePreviewNoPluginUpdates, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
            }

            await Task.CompletedTask;
        }

        private static bool StartIncrementalCombinedUpdate(AutoUpdatePlan applicationPlan, CombinedPluginUpdatePlan pluginPlan, bool createBackupBeforeUpdate)
        {
            string applicationDownloadDir = Environments.DirApplicationIncrementalPackageCache;
            string pluginDownloadDir = Environments.DirPluginPackageCache;
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
                string cachedPath = Path.Combine(applicationDownloadDir, packageFileName);
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
                string? existingFile = MarketplaceClient.GetExistingFileIfValid(pluginDownloadDir, item.Plugin.PackageName!, version, expectedHash);
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

                if (failed || orderedApplicationPaths == null || orderedPluginPaths == null)
                {
                    PostToUiThread(() =>
                    {
                        MessageBox.Show(Application.Current.GetActiveWindow(), Resources.UpdatePreviewCombinedPackageIncomplete, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                    });
                    return;
                }

                AutoUpdater.RestartIsIncrementApplication(orderedApplicationPaths, orderedPluginPaths, createBackupBeforeUpdate);
            }

            if (applicationPackagesToDownload.Count == 0 && pluginsToDownload.Count == 0)
            {
                AutoUpdater.RestartIsIncrementApplication(
                    applicationVersions.Select(version => applicationPackagePaths[version.ToString()]).ToList(),
                    pluginPackagePaths.ToList(),
                    createBackupBeforeUpdate);
                return true;
            }

            DownloadWindow.ShowInstance();

            foreach (Version version in applicationPackagesToDownload)
            {
                string versionKey = version.ToString();
                string packageFileName = AutoUpdater.GetIncrementalPackageFileName(version);
                string downloadUrl = AutoUpdater.GetIncrementalPackageDownloadUrl(version);

                manager.AddDownload(downloadUrl, applicationDownloadDir, "1:1", task =>
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

                manager.AddDownload(url, pluginDownloadDir, DownloadFileConfig.Instance.Authorization, task =>
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

        private static async Task<UpdatePreviewResult> ShowUpdatePreviewAsync(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool allowSkipVersion, bool isStartupCheck)
        {
            UpdatePreviewDialogContext context = BuildUpdatePreviewContext(applicationPlan, pluginPlan, allowSkipVersion, isStartupCheck);

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

            return new UpdatePreviewResult(
                window.ResultAction,
                GetSelectedApplicationUpdateMode(context),
                GetSelectedCreateBackupBeforeIncrementalUpdate(context));
        }

        private static UpdatePreviewDialogContext CreateCheckingContext()
        {
            return new UpdatePreviewDialogContext
            {
                Heading = Resources.UpdatePreviewCheckingHeading,
                Summary = Resources.UpdatePreviewCheckingSummary,
                CheckingTitle = Resources.UpdatePreviewScanningTitle,
                CheckingSummary = Resources.UpdatePreviewCheckingSummary,
                StateGlyph = "\uE895",
                HostVersionValue = AutoUpdater.CurrentVersion?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                ConfirmButtonText = Resources.UpdatePreviewUpdateNowButtonText,
                CancelButtonText = Resources.UpdatePreviewCancelButtonText,
                SecondaryButtonText = null,
                IsChecking = true,
            };
        }

        private static UpdatePreviewDialogContext CreateNoUpdatesContext(CombinedPluginUpdatePlan? pluginPlan)
        {
            string listSeparator = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" or "ja" ? "、" : ", ";
            string emptyMessage = pluginPlan?.SkippedIncompatiblePlugins.Count > 0
                ? string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewSkippedIncompatibleUpdatesFormat, string.Join(listSeparator, pluginPlan.SkippedIncompatiblePlugins))
                : Resources.UpdatePreviewNoInstallableUpdatesMessage;

            return new UpdatePreviewDialogContext
            {
                Heading = Resources.UpdatePreviewAlreadyLatestHeading,
                Summary = Resources.UpdatePreviewDialogSummaryNoUpdates,
                CheckingTitle = Resources.UpdatePreviewScanningTitle,
                CheckingSummary = Resources.UpdatePreviewCheckingSummary,
                EmptyStateTitle = Resources.UpdatePreviewAlreadyLatestHeading,
                EmptyStateMessage = emptyMessage,
                StateGlyph = "\uE73E",
                HostVersionValue = AutoUpdater.CurrentVersion?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                ConfirmButtonText = Resources.UpdatePreviewUpdateNowButtonText,
                CancelButtonText = Resources.UpdatePreviewCloseButtonText,
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

        private static void ApplySelectedApplicationChoices(ref AutoUpdatePlan? applicationPlan, ApplicationUpdateMode selectedMode, bool createBackupBeforeIncrementalUpdate)
        {
            if (applicationPlan == null || !applicationPlan.IsIncremental)
                return;

            if (selectedMode != ApplicationUpdateMode.Full)
            {
                AutoUpdateConfig.Instance.CreateBackupBeforeIncrementalUpdate = createBackupBeforeIncrementalUpdate;
                ConfigService.Instance.SaveConfigs();

                applicationPlan = new AutoUpdatePlan
                {
                    CurrentVersion = applicationPlan.CurrentVersion,
                    LatestVersion = applicationPlan.LatestVersion,
                    VersionsToApply = applicationPlan.VersionsToApply,
                    IsIncremental = true,
                    CreateBackupBeforeUpdate = createBackupBeforeIncrementalUpdate,
                };
                return;
            }

            applicationPlan = new AutoUpdatePlan
            {
                CurrentVersion = applicationPlan.CurrentVersion,
                LatestVersion = applicationPlan.LatestVersion,
                VersionsToApply = new[] { applicationPlan.LatestVersion },
                IsIncremental = false,
                CreateBackupBeforeUpdate = false,
            };
        }

        private static ApplicationUpdateMode GetSelectedApplicationUpdateMode(UpdatePreviewDialogContext context)
        {
            return context.Items.FirstOrDefault(item => string.Equals(item.ItemId, "application", StringComparison.OrdinalIgnoreCase))?.ApplicationUpdateMode
                ?? ApplicationUpdateMode.Incremental;
        }

        private static bool GetSelectedCreateBackupBeforeIncrementalUpdate(UpdatePreviewDialogContext context)
        {
            return context.Items.FirstOrDefault(item => string.Equals(item.ItemId, "application", StringComparison.OrdinalIgnoreCase))?.CreateBackupBeforeIncrementalUpdate
                ?? AutoUpdateConfig.Instance.CreateBackupBeforeIncrementalUpdate;
        }

        private static UpdatePreviewDialogContext BuildUpdatePreviewContext(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, bool allowSkipVersion, bool isStartupCheck)
        {
            UpdatePreviewDialogContext context = new()
            {
                Heading = BuildPreviewHeading(applicationPlan, pluginPlan),
                Summary = BuildDialogSummary(applicationPlan, pluginPlan),
                HostVersionValue = (applicationPlan?.CurrentVersion ?? AutoUpdater.CurrentVersion)?.ToString() ?? Resources.UpdatePreviewUnknownVersion,
                ConfirmButtonText = Resources.UpdatePreviewUpdateNowButtonText,
                CancelButtonText = isStartupCheck ? Resources.UpdatePreviewLaterButtonText : Resources.UpdatePreviewCancelButtonText,
                SecondaryButtonText = allowSkipVersion ? Resources.UpdatePreviewSkipVersionButtonText : null,
            };

            if (applicationPlan != null)
            {
                string incrementalSummary = string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewApplicationCardSummaryIncrementalFormat, applicationPlan.VersionsToApply.Count);
                string fullSummary = Resources.UpdatePreviewApplicationCardSummaryFull;
                UpdatePreviewItem previewItem = new()
                {
                    ItemId = "application",
                    Kind = applicationPlan.IsIncremental ? UpdatePreviewItemKind.ApplicationIncremental : UpdatePreviewItemKind.Application,
                    Category = applicationPlan.IsIncremental ? Resources.UpdatePreviewApplicationIncrementalCategory : Resources.UpdatePreviewApplicationUpdateCategory,
                    Name = "ColorVision",
                    SecondaryLabel = applicationPlan.IsIncremental
                        ? string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewApplicationIncrementalPackagesFormat, applicationPlan.VersionsToApply.Count)
                        : Resources.UpdatePreviewApplicationFullPackageLabel,
                    CurrentVersion = applicationPlan.CurrentVersion.ToString(),
                    TargetVersion = applicationPlan.LatestVersion.ToString(),
                    Summary = applicationPlan.IsIncremental ? incrementalSummary : fullSummary,
                    IsSelectable = false,
                    CanChooseApplicationUpdateMode = applicationPlan.IsIncremental,
                    CanChooseIncrementalBackup = applicationPlan.IsIncremental,
                    ApplicationUpdateMode = applicationPlan.IsIncremental ? ApplicationUpdateMode.Incremental : ApplicationUpdateMode.Full,
                    CreateBackupBeforeIncrementalUpdate = applicationPlan.IsIncremental && AutoUpdateConfig.Instance.CreateBackupBeforeIncrementalUpdate,
                };

                previewItem.ConfigureApplicationUpdateModePresentation(applicationPlan.VersionsToApply.Count, incrementalSummary, fullSummary);
                context.Items.Add(previewItem);
            }

            if (pluginPlan?.HasUpdates == true)
            {
                foreach (CombinedPluginUpdateItem item in pluginPlan.Updates)
                {
                    string pluginName = GetPluginDisplayName(item);
                    string pluginItemId = GetPluginItemId(item);
                    string currentVersion = item.Plugin.AssemblyVersion?.ToString() ?? Resources.UpdatePreviewUnknownVersion;

                    UpdatePreviewItem previewItem = new()
                    {
                        ItemId = pluginItemId,
                        Kind = UpdatePreviewItemKind.Plugin,
                        Category = Resources.UpdatePreviewPluginUpdateCategory,
                        Name = pluginName,
                        SecondaryLabel = BuildPluginSecondaryLabel(item, pluginName),
                        CurrentVersion = currentVersion,
                        TargetVersion = item.VersionInfo.Version,
                        HostRequirement = string.IsNullOrWhiteSpace(item.VersionInfo.RequiresVersion)
                            ? string.Empty
                            : item.VersionInfo.RequiresVersion,
                        Summary = BuildPluginCardSummary(item),
                        IsSelectable = true,
                        IsSelected = true,
                    };

                    context.Items.Add(previewItem);
                }
            }

            return context;
        }

        private static string BuildDialogSummary(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            int updateCount = (applicationPlan != null ? 1 : 0) + (pluginPlan?.Updates.Count ?? 0);
            if (updateCount == 0)
                return Resources.UpdatePreviewDialogSummaryNoUpdates;

            List<string> updateKinds = BuildUpdateKinds(applicationPlan, pluginPlan);
            StringBuilder builder = new();
            string listSeparator = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" or "ja" ? "、" : ", ";

            if (updateKinds.Count > 1)
                builder.Append(string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewDialogSummaryWithKinds, updateCount, string.Join(listSeparator, updateKinds)));
            else
                builder.Append(string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewDialogSummaryDefault, updateCount));

            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
                builder.Append($" {string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewDialogSummarySkippedCount, pluginPlan.SkippedIncompatiblePlugins.Count)}");

            return builder.ToString();
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
                ? Resources.UpdatePreviewFoundUpdatesHeading
                : Resources.CheckForUpdates;
        }

        private static List<string> BuildUpdateKinds(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            List<string> kinds = new();

            if (applicationPlan != null)
                kinds.Add(Resources.UpdatePreviewUpdateKindApplication);

            if (pluginPlan?.HasUpdates == true)
                kinds.Add(Resources.UpdatePreviewUpdateKindPlugin);

            return kinds;
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


        private static void ShowNoUpdatesMessage(CombinedPluginUpdatePlan? pluginPlan)
        {
            if (pluginPlan?.SkippedIncompatiblePlugins.Count > 0)
            {
                string listSeparator = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName is "zh" or "ja" ? "、" : ", ";
                string skippedPlugins = string.Join(listSeparator, pluginPlan.SkippedIncompatiblePlugins);
                string message = string.Format(CultureInfo.CurrentCulture, Resources.UpdatePreviewShowNoUpdatesSkippedMessage, skippedPlugins);
                MessageBox.Show(Application.Current.GetActiveWindow(), message, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBox1.Show(Application.Current.GetActiveWindow(), Resources.UpdatePreviewShowNoUpdatesLatestMessage, "ColorVision", MessageBoxButton.OK);
        }
    }

    public class CombinedUpdateInitializer : MainWindowInitializedBase
    {
        public override int Order { get => 0; set { } }

        public override Task Initialize() => CombinedUpdateCoordinator.ResumeIfNeededAsync();
    }
}
