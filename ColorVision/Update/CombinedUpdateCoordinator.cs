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
    internal enum CombinedIncrementalCompletionAction
    {
        ApplyCombinedUpdate,
        ApplyApplicationOnly,
        DownloadFullInstaller,
    }

    public static class CombinedUpdateCoordinator
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CombinedUpdateCoordinator));
        private static readonly SemaphoreSlim _locker = new(1, 1);
        private static readonly object _prefetchLock = new();
        private static readonly TimeSpan PrefetchDelay = TimeSpan.FromSeconds(30);
        private static AutoUpdatePlan? _pendingStartupApplicationPlan;
        private static CombinedPluginUpdatePlan? _pendingStartupPluginPlan;
        private static CancellationTokenSource? _prefetchCancellation;
        private static Task? _prefetchTask;
        private static string? _prefetchPlanKey;
        private static bool _prefetchStarted;

        public static event EventHandler? PendingStartupUpdateChanged;

        public static bool HasPendingStartupUpdate => HasUpdates(_pendingStartupApplicationPlan, _pendingStartupPluginPlan);

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
                            forceRefresh: true,
                            cancellationToken: previewCancellation.Token);

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

                await FinishPendingPrefetchAsync(cancellationToken);
                ApplySelectedApplicationChoices(
                    ref applicationPlan,
                    GetSelectedApplicationUpdateMode(context));
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

            bool includeApplicationUpdates = AutoUpdateConfig.Instance.IsAutoUpdate;
            bool includePluginUpdates = MarketplaceWindowConfig.Instance.IsAutoUpdate;

            if (!includeApplicationUpdates && !includePluginUpdates)
                return;

            await _locker.WaitAsync(cancellationToken);
            try
            {
                (AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan) = await BuildUpdatePlansAsync(
                    includeApplicationUpdates: includeApplicationUpdates,
                    includePluginUpdates: includePluginUpdates,
                    respectSkippedVersion: true,
                    cancellationToken: cancellationToken);

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

        public static async Task StartPendingStartupUpdateAsync(CancellationToken cancellationToken = default)
        {
            await _locker.WaitAsync(cancellationToken);
            try
            {
                (AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan) = await BuildPendingStartupPlansAsync(cancellationToken);

                if (!HasUpdates(applicationPlan, pluginPlan))
                {
                    ClearPendingStartupUpdate();
                    ShowNoUpdatesMessage(pluginPlan);
                    return;
                }

                UpdatePreviewDialogContext defaultContext = BuildUpdatePreviewContext(applicationPlan, pluginPlan, allowSkipVersion: false, isStartupCheck: true);
                await FinishPendingPrefetchAsync(cancellationToken);
                ApplySelectedApplicationChoices(
                    ref applicationPlan,
                    GetSelectedApplicationUpdateMode(defaultContext));
                ApplySelectedPluginUpdates(pluginPlan, defaultContext);
                ClearPendingStartupUpdate();
                await StartWorkflowAsync(applicationPlan, pluginPlan, showNoUpdatesMessage: false);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Pending startup update start canceled.");
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
            SchedulePrefetch(applicationPlan, pluginPlan);
            if (hadUpdates != HasPendingStartupUpdate || HasPendingStartupUpdate)
                PendingStartupUpdateChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void ClearPendingStartupUpdate()
        {
            bool hadUpdates = HasPendingStartupUpdate;
            _pendingStartupApplicationPlan = null;
            _pendingStartupPluginPlan = null;
            CancelPendingPrefetch();
            if (hadUpdates)
                PendingStartupUpdateChanged?.Invoke(null, EventArgs.Empty);
        }

        private static void SchedulePrefetch(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            string planKey = CreatePrefetchPlanKey(applicationPlan, pluginPlan);
            lock (_prefetchLock)
            {
                if (string.Equals(_prefetchPlanKey, planKey, StringComparison.Ordinal) && _prefetchTask != null)
                    return;

                CancelPendingPrefetchNoLock();
                _prefetchPlanKey = planKey;
                _prefetchCancellation = new CancellationTokenSource();
                _prefetchStarted = false;
                _prefetchTask = RunDelayedPrefetchAsync(planKey, applicationPlan, pluginPlan, _prefetchCancellation.Token);
            }
        }

        private static async Task RunDelayedPrefetchAsync(string planKey, AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan, CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(PrefetchDelay, cancellationToken).ConfigureAwait(false);
                lock (_prefetchLock)
                {
                    if (!string.Equals(_prefetchPlanKey, planKey, StringComparison.Ordinal))
                        return;
                    _prefetchStarted = true;
                }

                List<Task<bool>> downloads = new();
                if (applicationPlan != null)
                    downloads.Add(AutoUpdater.PrefetchUpdatePlanAsync(applicationPlan, cancellationToken));
                if (pluginPlan?.HasUpdates == true)
                    downloads.Add(MarketplaceManager.GetInstance().PrefetchCombinedUpdateAsync(pluginPlan, cancellationToken));

                if (downloads.Count == 0)
                    return;

                bool[] results = await Task.WhenAll(downloads).ConfigureAwait(false);
                log.Info(results.All(result => result)
                    ? "Pending update packages were prefetched successfully."
                    : "Pending update prefetch completed with one or more unavailable packages.");
            }
            catch (OperationCanceledException)
            {
                log.Debug("Pending update prefetch canceled.");
            }
            catch (Exception ex)
            {
                log.Warn("Pending update prefetch failed; the normal update path remains available.", ex);
            }
        }

        private static async Task FinishPendingPrefetchAsync(CancellationToken cancellationToken)
        {
            Task? task;
            bool started;
            lock (_prefetchLock)
            {
                task = _prefetchTask;
                started = _prefetchStarted;
                if (!started)
                {
                    CancelPendingPrefetchNoLock();
                    return;
                }
            }

            if (task == null)
                return;

            if (!task.IsCompleted)
                PostToUiThread(DownloadWindow.ShowInstance);

            try
            {
                await task.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
            }
        }

        private static void CancelPendingPrefetch()
        {
            lock (_prefetchLock)
                CancelPendingPrefetchNoLock();
        }

        private static void CancelPendingPrefetchNoLock()
        {
            _prefetchCancellation?.Cancel();
            ResetPrefetchStateNoLock();
        }

        private static void ResetPrefetchStateNoLock()
        {
            _prefetchCancellation?.Dispose();
            _prefetchCancellation = null;
            _prefetchTask = null;
            _prefetchPlanKey = null;
            _prefetchStarted = false;
        }

        private static string CreatePrefetchPlanKey(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            string applicationKey = applicationPlan == null
                ? string.Empty
                : $"{applicationPlan.LatestVersion}|{applicationPlan.IsIncremental}|{string.Join(",", applicationPlan.VersionsToApply)}";
            string pluginKey = pluginPlan?.HasUpdates == true
                ? string.Join(",", pluginPlan.Updates.Select(item => $"{item.Plugin.PackageName}:{item.VersionInfo.Version}"))
                : string.Empty;
            return applicationKey + "#" + pluginKey;
        }

        public static bool TryApplyPrefetchedUpdateOnExit()
        {
            AutoUpdatePlan? applicationPlan = _pendingStartupApplicationPlan;
            CombinedPluginUpdatePlan? pluginPlan = _pendingStartupPluginPlan;
            if (!HasUpdates(applicationPlan, pluginPlan))
                return false;

            if (applicationPlan != null && !applicationPlan.IsIncremental)
            {
                log.Info("A full installer is cached or downloading; it will not be started automatically on exit.");
                return false;
            }

            IReadOnlyList<string> applicationPackagePaths = Array.Empty<string>();
            if (applicationPlan != null && !AutoUpdater.TryGetCachedIncrementalPackagePaths(applicationPlan, out applicationPackagePaths))
            {
                log.Info("Skipped exit-time update because the incremental application packages are incomplete.");
                return false;
            }

            IReadOnlyList<string> pluginPackagePaths = Array.Empty<string>();
            bool pluginsReady = pluginPlan?.HasUpdates != true || TryGetCachedPluginPackagePaths(pluginPlan, out pluginPackagePaths);
            if (applicationPlan == null && !pluginsReady)
            {
                log.Info("Skipped exit-time plugin update because one or more plugin packages are incomplete.");
                return false;
            }

            if (!pluginsReady)
            {
                log.Info("Plugin packages are incomplete; the cached application update will be applied alone on exit.");
                pluginPackagePaths = Array.Empty<string>();
            }

            if (applicationPackagePaths.Count == 0 && pluginPackagePaths.Count == 0)
                return false;

            return AutoUpdater.TryStartIncrementalApplicationUpdate(
                applicationPackagePaths,
                pluginPackagePaths,
                restartApplication: false,
                allowElevationFallback: false,
                showErrors: false);
        }

        private static bool TryGetCachedPluginPackagePaths(CombinedPluginUpdatePlan plan, out IReadOnlyList<string> packagePaths)
        {
            List<string> paths = new();
            string downloadDirectory = Environments.DirPluginPackageCache;
            foreach (CombinedPluginUpdateItem item in plan.Updates)
            {
                string? packageName = item.Plugin.PackageName;
                if (string.IsNullOrWhiteSpace(packageName) || string.IsNullOrWhiteSpace(item.VersionInfo.Version))
                {
                    packagePaths = Array.Empty<string>();
                    return false;
                }

                string? existingFile = MarketplaceClient.GetExistingFileIfValid(
                    downloadDirectory,
                    packageName,
                    item.VersionInfo.Version,
                    item.VersionInfo.FileHash);
                if (existingFile == null)
                {
                    packagePaths = Array.Empty<string>();
                    return false;
                }

                paths.Add(existingFile);
            }

            packagePaths = paths;
            return paths.Count == plan.Updates.Count;
        }

        private static Task<(AutoUpdatePlan? ApplicationPlan, CombinedPluginUpdatePlan? PluginPlan)> BuildPendingStartupPlansAsync(CancellationToken cancellationToken)
        {
            return BuildUpdatePlansAsync(
                includeApplicationUpdates: AutoUpdateConfig.Instance.IsAutoUpdate,
                includePluginUpdates: MarketplaceWindowConfig.Instance.IsAutoUpdate,
                respectSkippedVersion: false,
                forceRefresh: true,
                cancellationToken: cancellationToken);
        }

        private static async Task<(AutoUpdatePlan? ApplicationPlan, CombinedPluginUpdatePlan? PluginPlan)> BuildUpdatePlansAsync(bool includeApplicationUpdates, bool includePluginUpdates, bool respectSkippedVersion, bool forceRefresh = false, CancellationToken cancellationToken = default)
        {
            if (!WindowsNetworkState.IsConnectedToInternet())
            {
                log.Info("Skipped update plan check because Windows reports no internet connectivity.");
                return (null, null);
            }

            AutoUpdatePlan? applicationPlan = null;
            if (includeApplicationUpdates)
            {
                applicationPlan = await AutoUpdater.GetInstance().GetUpdatePlanAsync(forceRefresh, cancellationToken);
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
                await StartIncrementalCombinedUpdateAsync(applicationPlan, pluginPlan);
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

        private static void ShowUpdateDownloadFailedMessage()
        {
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

        private static async Task StartIncrementalCombinedUpdateAsync(AutoUpdatePlan applicationPlan, CombinedPluginUpdatePlan pluginPlan)
        {
            Task<IReadOnlyList<string>> applicationPackagesTask = AutoUpdater.EnsureUpdatePlanPackagesAsync(
                applicationPlan,
                showDownloadWindow: true,
                CancellationToken.None);
            Task<IReadOnlyList<string>> pluginPackagesTask = MarketplaceManager.GetInstance().EnsureCombinedUpdatePackagesAsync(
                pluginPlan,
                showDownloadWindow: true,
                CancellationToken.None);

            await Task.WhenAll(applicationPackagesTask, pluginPackagesTask);
            IReadOnlyList<string> applicationPackagePaths = await applicationPackagesTask;
            IReadOnlyList<string> pluginPackagePaths = await pluginPackagesTask;
            CombinedIncrementalCompletionAction action = DetermineCombinedIncrementalCompletionAction(
                applicationPlan.VersionsToApply.Distinct().Count(),
                applicationPackagePaths.Count,
                pluginPlan.Updates.Count,
                pluginPackagePaths.Count);

            if (action == CombinedIncrementalCompletionAction.DownloadFullInstaller)
            {
                log.Warn($"Combined incremental application packages are incomplete; falling back to the full installer for {applicationPlan.LatestVersion}.");
                AutoUpdater.StartFullUpdate(applicationPlan.LatestVersion, ShowUpdateDownloadFailedMessage);
                return;
            }

            if (action == CombinedIncrementalCompletionAction.ApplyApplicationOnly)
            {
                log.Warn("Plugin packages are incomplete; applying the application update now and leaving plugins for the next update check.");
                AutoUpdater.RestartIsIncrementApplication(applicationPackagePaths, null);
                return;
            }

            AutoUpdater.RestartIsIncrementApplication(applicationPackagePaths, pluginPackagePaths);
        }

        internal static CombinedIncrementalCompletionAction DetermineCombinedIncrementalCompletionAction(
            int expectedApplicationPackages,
            int availableApplicationPackages,
            int expectedPluginPackages,
            int availablePluginPackages)
        {
            if (availableApplicationPackages != expectedApplicationPackages)
                return CombinedIncrementalCompletionAction.DownloadFullInstaller;

            return availablePluginPackages == expectedPluginPackages
                ? CombinedIncrementalCompletionAction.ApplyCombinedUpdate
                : CombinedIncrementalCompletionAction.ApplyApplicationOnly;
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

        private static void ApplySelectedApplicationChoices(ref AutoUpdatePlan? applicationPlan, ApplicationUpdateMode selectedMode)
        {
            if (applicationPlan == null || !applicationPlan.IsIncremental)
                return;

            if (selectedMode != ApplicationUpdateMode.Full)
                return;

            applicationPlan = new AutoUpdatePlan
            {
                CurrentVersion = applicationPlan.CurrentVersion,
                LatestVersion = applicationPlan.LatestVersion,
                VersionsToApply = new[] { applicationPlan.LatestVersion },
                IsIncremental = false,
            };
        }

        private static ApplicationUpdateMode GetSelectedApplicationUpdateMode(UpdatePreviewDialogContext context)
        {
            return context.Items.FirstOrDefault(item => string.Equals(item.ItemId, "application", StringComparison.OrdinalIgnoreCase))?.ApplicationUpdateMode
                ?? ApplicationUpdateMode.Incremental;
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
                    ApplicationUpdateMode = applicationPlan.IsIncremental ? ApplicationUpdateMode.Incremental : ApplicationUpdateMode.Full,
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

}
