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

    [Flags]
    internal enum ExitUpdateContent
    {
        None = 0,
        Application = 1,
        Plugins = 2,
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
                UpdatePreviewDialogContext context = UpdatePreviewContextFactory.CreateCheckingContext();
                using CancellationTokenSource previewCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

                UpdatePreviewWindow window = new(context, async currentWindow =>
                {
                    try
                    {
                        (applicationPlan, pluginPlan) = await BuildUpdatePlansAsync(
                            includeApplicationUpdates: true,
                            includePluginUpdates: true,
                            forceRefresh: true,
                            cancellationToken: previewCancellation.Token);

                        if (currentWindow.IsClosed)
                            return;

                        if (!HasUpdates(applicationPlan, pluginPlan))
                        {
                            context.CopyFrom(UpdatePreviewContextFactory.CreateNoUpdatesContext(pluginPlan));
                            return;
                        }

                        UpdatePreviewDialogContext loadedContext = UpdatePreviewContextFactory.Build(applicationPlan, pluginPlan, isStartupCheck: false);
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
                    includeCurrentHostPluginUpdatesWhenFullApplicationUpdate: true,
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

                UpdatePreviewDialogContext defaultContext = UpdatePreviewContextFactory.Build(applicationPlan, pluginPlan, isStartupCheck: true);
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

            bool applicationPackagesReady = false;
            IReadOnlyList<string> applicationPackagePaths = Array.Empty<string>();
            if (applicationPlan != null && !applicationPlan.IsIncremental)
            {
                log.Info("A full installer is cached or downloading; it will not be started automatically on exit.");
            }
            else if (applicationPlan != null)
            {
                applicationPackagesReady = AutoUpdater.TryGetCachedIncrementalPackagePaths(applicationPlan, out applicationPackagePaths);
                if (!applicationPackagesReady)
                    log.Info("Skipped exit-time application update because the incremental packages are incomplete.");
            }

            Version pluginHostVersion = applicationPackagesReady && applicationPlan != null
                ? applicationPlan.LatestVersion
                : applicationPlan?.CurrentVersion ?? AutoUpdater.CurrentVersion ?? pluginPlan?.HostVersion ?? new Version();
            CombinedPluginUpdatePlan? exitPluginPlan = pluginPlan?.CreateCompatibleSubset(pluginHostVersion);
            IReadOnlyList<string> pluginPackagePaths = Array.Empty<string>();
            bool hasPluginUpdates = exitPluginPlan?.HasUpdates == true;
            bool pluginPackagesReady = hasPluginUpdates && TryGetCachedPluginPackagePaths(exitPluginPlan!, out pluginPackagePaths);
            if (hasPluginUpdates && !pluginPackagesReady)
                log.Info("Skipped exit-time plugin update because one or more plugin packages are incomplete.");
            if (pluginPlan?.HasUpdates == true && exitPluginPlan?.HasUpdates != true)
                log.Info($"Skipped exit-time plugin update because the cached plugins require a newer ColorVision host than {pluginHostVersion}.");

            ExitUpdateContent content = DetermineExitUpdateContent(
                applicationPlan != null,
                applicationPlan?.IsIncremental == true,
                applicationPackagesReady,
                hasPluginUpdates,
                pluginPackagesReady);
            if (!content.HasFlag(ExitUpdateContent.Application))
                applicationPackagePaths = Array.Empty<string>();
            if (!content.HasFlag(ExitUpdateContent.Plugins))
                pluginPackagePaths = Array.Empty<string>();

            if (applicationPackagePaths.Count == 0 && pluginPackagePaths.Count == 0)
                return false;

            return AutoUpdater.TryStartIncrementalApplicationUpdate(
                applicationPackagePaths,
                pluginPackagePaths,
                restartApplication: false,
                allowElevationFallback: false,
                showErrors: false);
        }

        internal static ExitUpdateContent DetermineExitUpdateContent(
            bool hasApplicationUpdate,
            bool isIncrementalApplicationUpdate,
            bool applicationPackagesReady,
            bool hasPluginUpdates,
            bool pluginPackagesReady)
        {
            ExitUpdateContent content = ExitUpdateContent.None;
            if (hasApplicationUpdate && isIncrementalApplicationUpdate && applicationPackagesReady)
                content |= ExitUpdateContent.Application;
            if (hasPluginUpdates && pluginPackagesReady)
                content |= ExitUpdateContent.Plugins;
            return content;
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
                forceRefresh: true,
                cancellationToken: cancellationToken);
        }

        private static async Task<(AutoUpdatePlan? ApplicationPlan, CombinedPluginUpdatePlan? PluginPlan)> BuildUpdatePlansAsync(
            bool includeApplicationUpdates,
            bool includePluginUpdates,
            bool includeCurrentHostPluginUpdatesWhenFullApplicationUpdate = false,
            bool forceRefresh = false,
            CancellationToken cancellationToken = default)
        {
            if (!WindowsNetworkState.IsConnectedToInternet())
            {
                log.Info("Skipped update plan check because Windows reports no internet connectivity.");
                return (null, null);
            }

            AutoUpdatePlan? applicationPlan = null;
            if (includeApplicationUpdates)
            {
                applicationPlan = await AutoUpdater.GetUpdatePlanAsync(forceRefresh, cancellationToken);
            }

            CombinedPluginUpdatePlan? pluginPlan = null;
            if (includePluginUpdates)
            {
                Version? hostVersion = ResolvePluginPlanHostVersion(
                    applicationPlan,
                    AutoUpdater.CurrentVersion,
                    includeCurrentHostPluginUpdatesWhenFullApplicationUpdate);
                if (hostVersion != null)
                {
                    pluginPlan = await MarketplaceManager.GetInstance().BuildCombinedUpdatePlanAsync(hostVersion, cancellationToken);
                }
            }

            return (applicationPlan, pluginPlan);
        }

        internal static Version? ResolvePluginPlanHostVersion(
            AutoUpdatePlan? applicationPlan,
            Version? currentVersion,
            bool includeCurrentHostPluginUpdatesWhenFullApplicationUpdate)
        {
            if (applicationPlan == null)
                return currentVersion;

            if (applicationPlan.IsIncremental)
                return applicationPlan.LatestVersion;

            return includeCurrentHostPluginUpdatesWhenFullApplicationUpdate ? currentVersion : null;
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

            StartPluginUpdate(pluginPlan!, showNoUpdatesMessage);
        }

        private static bool HasUpdates(AutoUpdatePlan? applicationPlan, CombinedPluginUpdatePlan? pluginPlan)
        {
            return applicationPlan != null || pluginPlan?.HasUpdates == true;
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

        private static void StartPluginUpdate(CombinedPluginUpdatePlan pluginPlan, bool showNoUpdatesMessage)
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

        private static void ApplySelectedPluginUpdates(CombinedPluginUpdatePlan? pluginPlan, UpdatePreviewDialogContext context)
        {
            if (pluginPlan == null || !pluginPlan.HasUpdates)
                return;

            HashSet<string> selectedPluginIds = context.Items
                .Where(item => item.IsSelectable && item.IsSelected && !string.IsNullOrWhiteSpace(item.ItemId))
                .Select(item => item.ItemId)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            pluginPlan.Updates.RemoveAll(item => !selectedPluginIds.Contains(UpdatePreviewContextFactory.GetPluginItemId(item)));
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
