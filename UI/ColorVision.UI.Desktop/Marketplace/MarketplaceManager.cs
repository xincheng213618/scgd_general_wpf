#pragma warning disable CA1001,CA1822,CA1863,CS8604
using ColorVision.Common.MVVM;
using ColorVision.UI.Desktop.Properties;
using ColorVision.UI.Marketplace;
using ColorVision.UI.Plugins;
using log4net;
using Microsoft.Win32;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.UI.Desktop.Marketplace
{
  
    public class MarketplaceWindowConfig : WindowConfig
    {
        public static MarketplaceWindowConfig Instance => ConfigService.Instance.GetRequiredService<MarketplaceWindowConfig>();

        /// <summary>
        /// 是否自动更新插件
        /// </summary>
        /// 
        [ConfigSetting(Order = 999)]
        [DisplayName("CheckPluginUpdates")]
        [Description("CheckPluginUpdatesDescription")]
        public bool IsAutoUpdate { get => _IsAutoUpdate; set { _IsAutoUpdate = value; OnPropertyChanged(); } }
        private bool _IsAutoUpdate = true;
    }


    public class MarketplaceManager:ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MarketplaceManager));
        private static MarketplaceManager _instance;
        private static readonly object _locker = new();
        private readonly MarketplacePackageDownloadService _packageDownloadService = MarketplacePackageDownloadService.GetInstance();
        private object? _currentDetailContext;
        private PluginInfoVM? _selectedInstalledPlugin;
        private bool _isMarketplaceTabActive;
        private CancellationTokenSource? _activeOperationCancellation;
        public static MarketplaceManager GetInstance() { lock (_locker) { _instance ??= new MarketplaceManager(); return _instance; } }
        public ObservableCollection<PluginInfoVM> Plugins { get; private set; } = new ObservableCollection<PluginInfoVM>();
        public MarketplaceCatalogViewModel Catalog { get; }
        public static MarketplaceWindowConfig Config => MarketplaceWindowConfig.Instance;
        public RelayCommand EditConfigCommand { get; set; }
        public RelayCommand InstallPackageCommand { get; set; }
        public ICommand DownloadPackageCommand { get; set; }
        public RelayCommand RestartCommand { get; set; }
        public ICommand RefreshVersionsCommand { get; set; }

        // 在 PluginLoader 类中添加
        public ICommand UpdateAllCommand { get; set; }

        public int UpdateAvailableCount { get => _UpdateAvailableCount; set { _UpdateAvailableCount = value; OnPropertyChanged(); } }
        private int _UpdateAvailableCount;
        public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); } }
        private bool _isBusy;
        private readonly SemaphoreSlim _refreshVersionsLock = new(1, 1);

        public PluginInfoVM? SelectedInstalledPlugin
        {
            get => _selectedInstalledPlugin;
            set
            {
                _selectedInstalledPlugin = value;
                OnPropertyChanged();
                UpdateCurrentDetailContext();
            }
        }

        public object? CurrentDetailContext
        {
            get => _currentDetailContext;
            private set
            {
                _currentDetailContext = value;
                OnPropertyChanged();
            }
        }

        public bool IsMarketplaceTabActive
        {
            get => _isMarketplaceTabActive;
            set
            {
                _isMarketplaceTabActive = value;
                OnPropertyChanged();
                UpdateCurrentDetailContext();
            }
        }

        public MarketplaceManager()
        {
            log.Info(UI.Properties.Resources.CheckingForAdditionalProjects);
            Catalog = new MarketplaceCatalogViewModel(FindInstalledPlugin, SetMarketplaceDetailContext);

            foreach (var item in UI.Plugins.PluginLoader.Config.Plugins)
            {
                if (item.Value.Manifest != null)
                {
                    // skipIndividualCheck=true: batch check will provide versions
                    PluginInfoVM info = new PluginInfoVM(item.Value, skipIndividualCheck: true);
                    info.PropertyChanged += (s, e) =>
                    {
                        if (e.PropertyName == nameof(PluginInfoVM.HasUpdate))
                        {
                            UpdateAvailableCount = Plugins.Count(p => p.HasUpdate);
                        }
                    };
                    Plugins.Add(info);
                }
            }

            _ = RunStartupRefreshAsync();
 
            InstallPackageCommand = new RelayCommand(a => InstallPackage());
            DownloadPackageCommand = new AsyncRelayCommand(_ => RunUserOperationAsync(DownloadPackageAsync), logger: log);
            EditConfigCommand = new RelayCommand(a => new PropertyEditorWindow(Config) { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog());
            RestartCommand = new RelayCommand(a => Restart());
            RefreshVersionsCommand = new AsyncRelayCommand(_ => RunUserOperationAsync(RefreshVersionsAsync), logger: log);
            UpdateAllCommand = new AsyncRelayCommand(_ => RunUserOperationAsync(UpdateAllAsync), logger: log);

            SelectedInstalledPlugin = Plugins.FirstOrDefault();
        }

        private async Task RunStartupRefreshAsync()
        {
            try
            {
                await RefreshVersionsAsync().ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Initial marketplace version refresh canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Initial marketplace version refresh failed.", ex);
            }
        }

        private async Task RunUserOperationAsync(Func<CancellationToken, Task> operation)
        {
            CancellationTokenSource operationCancellation = BeginUserOperation();
            try
            {
                IsBusy = true;
                await operation(operationCancellation.Token);
            }
            catch (OperationCanceledException)
            {
                log.Debug("Marketplace operation canceled.");
            }
            catch (Exception ex)
            {
                log.Error("Marketplace operation failed.", ex);
            }
            finally
            {
                IsBusy = false;
                if (ReferenceEquals(_activeOperationCancellation, operationCancellation))
                {
                    _activeOperationCancellation = null;
                }

                operationCancellation.Dispose();
            }
        }

        private CancellationTokenSource BeginUserOperation()
        {
            CancelActiveOperations();
            _activeOperationCancellation = new CancellationTokenSource();
            return _activeOperationCancellation;
        }

        public void CancelActiveOperations()
        {
            CancelAndDispose(ref _activeOperationCancellation);
        }

        public Task EnsureMarketplaceCatalogLoadedAsync(CancellationToken cancellationToken = default)
        {
            return Catalog.InitializeAsync(cancellationToken);
        }

        public Task RefreshMarketplaceCatalogAsync(CancellationToken cancellationToken = default)
        {
            return Catalog.RefreshAsync(forceReload: true, cancellationToken);
        }

        private PluginInfoVM? FindInstalledPlugin(string pluginId)
        {
            return Plugins.FirstOrDefault(plugin => string.Equals(plugin.PackageName, pluginId, StringComparison.OrdinalIgnoreCase));
        }

        private void SetMarketplaceDetailContext(MarketplaceDetailContext? detailContext)
        {
            if (IsMarketplaceTabActive)
            {
                CurrentDetailContext = detailContext;
            }
        }

        private void UpdateCurrentDetailContext()
        {
            CurrentDetailContext = IsMarketplaceTabActive
                ? Catalog.SelectedDetailContext
                : SelectedInstalledPlugin;
        }

        public async Task RefreshVersionsAsync(CancellationToken cancellationToken = default)
        {
            bool lockTaken = false;
            try
            {
                await _refreshVersionsLock.WaitAsync(cancellationToken);
                lockTaken = true;

                var enabledPlugins = Plugins.Where(p => p.PluginInfo.Enabled && !string.IsNullOrWhiteSpace(p.PackageName)).ToList();
                if (enabledPlugins.Count == 0)
                {
                    UpdateAvailableCount = 0;
                    return;
                }

                var client = MarketplaceClient.GetInstance();
                Dictionary<string, string?> versions = new(StringComparer.OrdinalIgnoreCase);

                try
                {
                    versions = await client.BatchVersionCheckAsync(enabledPlugins.Select(p => p.PackageName!).ToList(), cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log.Warn($"Batch version check failed, falling back to individual checks: {ex.Message}");
                }

                var fallbackTasks = new List<Task>();
                foreach (var plugin in enabledPlugins)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (versions.TryGetValue(plugin.PackageName!, out string? version) && !string.IsNullOrWhiteSpace(version))
                    {
                        plugin.SetVersionFromBatchCheck(version);
                    }
                    else
                    {
                        fallbackTasks.Add(plugin.CheckVersionAsync(cancellationToken));
                    }
                }

                if (fallbackTasks.Count > 0)
                {
                    await Task.WhenAll(fallbackTasks);
                }

                cancellationToken.ThrowIfCancellationRequested();
                UpdateAvailableCount = Plugins.Count(p => p.HasUpdate);
            }
            catch (OperationCanceledException)
            {
                log.Debug("RefreshVersionsAsync canceled.");
                throw;
            }
            catch (Exception ex)
            {
                log.Warn($"RefreshVersionsAsync failed: {ex.Message}");
            }
            finally
            {
                if (lockTaken)
                {
                    _refreshVersionsLock.Release();
                }
            }
        }

        public async Task<CombinedPluginUpdatePlan> BuildCombinedUpdatePlanAsync(Version hostVersion, CancellationToken cancellationToken = default)
        {
            await RefreshVersionsAsync(cancellationToken);

            CombinedPluginUpdatePlan plan = new() { HostVersion = hostVersion };
            var pluginsToCheck = Plugins
                .Where(plugin => plugin.PluginInfo.Enabled && plugin.HasUpdate && !string.IsNullOrWhiteSpace(plugin.PackageName))
                .ToList();

            if (pluginsToCheck.Count == 0)
                return plan;

            MarketplaceClient client = MarketplaceClient.GetInstance();
            foreach (var plugin in pluginsToCheck)
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    MarketplacePluginDetail? detail = await client.GetPluginDetailAsync(plugin.PackageName!, cancellationToken);
                    MarketplacePluginVersionInfo? candidate = PluginUpdateCompatibility.SelectLatestCompatibleVersion(plugin.AssemblyVersion, detail, hostVersion);
                    if (candidate != null)
                    {
                        plan.Updates.Add(new CombinedPluginUpdateItem
                        {
                            Plugin = plugin,
                            VersionInfo = candidate,
                        });
                    }
                    else
                    {
                        plan.SkippedIncompatiblePlugins.Add(plugin.Name ?? plugin.PackageName ?? "Unknown");
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    log.Warn($"BuildCombinedUpdatePlanAsync failed for {plugin.PackageName}: {ex.Message}");
                }
            }

            return plan;
        }

        public bool StartCombinedUpdate(CombinedPluginUpdatePlan plan, string? restartArguments = null, Action? noRestartAction = null, CancellationToken cancellationToken = default)
        {
            if (plan.Updates.Count == 0)
            {
                noRestartAction?.Invoke();
                return false;
            }

            List<MarketplacePackageRequest> requests = CreateCombinedUpdateRequests(plan);

            _packageDownloadService.StartBackgroundBatchInstall(requests, restartArguments, noRestartAction, cancellationToken);

            return true;
        }

        public async Task<bool> PrefetchCombinedUpdateAsync(CombinedPluginUpdatePlan plan, CancellationToken cancellationToken = default)
        {
            IReadOnlyList<string> packagePaths = await EnsureCombinedUpdatePackagesAsync(plan, showDownloadWindow: false, cancellationToken).ConfigureAwait(false);
            return packagePaths.Count == plan.Updates.Count;
        }

        public async Task<IReadOnlyList<string>> EnsureCombinedUpdatePackagesAsync(CombinedPluginUpdatePlan plan, bool showDownloadWindow, CancellationToken cancellationToken = default)
        {
            if (!plan.HasUpdates)
                return Array.Empty<string>();

            List<MarketplacePackageRequest> requests = CreateCombinedUpdateRequests(plan);
            if (requests.Count != plan.Updates.Count)
                return Array.Empty<string>();

            return await _packageDownloadService.EnsurePackagesAvailableAsync(
                requests,
                showFailureDialog: false,
                showDownloadWindow: showDownloadWindow,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private static List<MarketplacePackageRequest> CreateCombinedUpdateRequests(CombinedPluginUpdatePlan plan)
        {
            return plan.Updates
                .Where(item => !string.IsNullOrWhiteSpace(item.Plugin.PackageName) && !string.IsNullOrWhiteSpace(item.VersionInfo.Version))
                .Select(item => new MarketplacePackageRequest
                {
                    PluginId = item.Plugin.PackageName!,
                    Version = item.VersionInfo.Version,
                    ExpectedHash = item.VersionInfo.FileHash,
                })
                .ToList();
        }

        public async Task UpdateAllAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Version? hostVersion = PluginUpdateCompatibility.GetCurrentHostVersion();
            if (hostVersion == null)
            {
                log.Warn("UpdateAllAsync aborted because the current ColorVision version could not be resolved.");
                MessageBox.Show(Application.Current.GetActiveWindow(), Resources.MarketplaceBulkUpdatePackageDownloadFailed, Resources.MarketplaceBulkUpdateTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            CombinedPluginUpdatePlan plan = await BuildCombinedUpdatePlanAsync(hostVersion, cancellationToken);
            if (!plan.HasUpdates)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Resources.MarketplaceNoUpdates, Resources.PluginManagerWindow, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            if (MessageBox.Show(Application.Current.GetActiveWindow(), 
                string.Format(Resources.MarketplaceBulkUpdateConfirm, plan.Updates.Count),
                Resources.MarketplaceBulkUpdateTitle, 
                MessageBoxButton.YesNo, 
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return;
            }

            List<MarketplacePackageRequest> requests = CreateCombinedUpdateRequests(plan);
            IReadOnlyList<string> packagePaths = await EnsureCombinedUpdatePackagesAsync(plan, showDownloadWindow: true, cancellationToken);
            if (packagePaths.Count == 0)
            {
                log.Warn("UpdateAllAsync: no plugin packages were downloaded successfully.");
                MessageBox.Show(Application.Current.GetActiveWindow(), Resources.MarketplaceBulkUpdatePackageDownloadFailed, Resources.MarketplaceBulkUpdateTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (packagePaths.Count != requests.Count)
            {
                log.Warn($"UpdateAllAsync aborted: expected {requests.Count} plugin packages, got {packagePaths.Count}.");
                MessageBox.Show(Application.Current.GetActiveWindow(), Resources.MarketplaceBulkUpdatePackageDownloadFailed, Resources.MarketplaceBulkUpdateTitle, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (!_packageDownloadService.TryApproveBatchInstall(packagePaths, requests))
                return;

            await Application.Current!.Dispatcher.InvokeAsync(() => PluginUpdater.UpdatePlugin(packagePaths.ToArray()));
        }

        public void Restart()
        {
            ConfigService.Instance.SaveConfigs();
            PluginLoaderrConfig.Instance.Save();

            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-c MenuPluginManager");

            Application.Current.Shutdown();
        }

        public string SearchName { get => _SearchName; set { _SearchName = value; OnPropertyChanged(); }}
        private string _SearchName;
        public async Task DownloadPackageAsync(CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Version? hostVersion = PluginUpdateCompatibility.GetCurrentHostVersion();
            MarketplacePluginDetail? detail = hostVersion == null
                ? null
                : await MarketplaceClient.GetInstance().GetPluginDetailAsync(SearchName, cancellationToken);
            MarketplacePluginVersionInfo? candidate = hostVersion == null
                ? null
                : PluginUpdateCompatibility.SelectLatestCompatibleVersion(installedVersion: null, detail, hostVersion);
            if (candidate == null)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), Resources.MarketplaceNoUpdates, Resources.PluginManagerWindow, MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            MessageBoxResult confirmResult = await Application.Current.Dispatcher.InvokeAsync(() =>
                MessageBox.Show(
                    Application.Current.GetActiveWindow(),
                    string.Format(UI.Properties.Resources.FoundProjectDownloadConfirm, SearchName, $"{ColorVision.UI.Properties.Resources.Version}{candidate.Version}"),
                    "ColorVision",
                    MessageBoxButton.YesNo));

            if (confirmResult == MessageBoxResult.Yes)
            {
                await _packageDownloadService.InstallPackageAsync(new MarketplacePackageRequest
                {
                    PluginId = SearchName,
                    Version = candidate.Version,
                    ExpectedHash = candidate.FileHash,
                }, cancellationToken: cancellationToken);
            }
        }

        private static void CancelAndDispose(ref CancellationTokenSource? cancellationTokenSource)
        {
            if (cancellationTokenSource == null)
                return;

            try
            {
                cancellationTokenSource.Cancel();
            }
            catch (ObjectDisposedException)
            {
            }

            cancellationTokenSource.Dispose();
            cancellationTokenSource = null;
        }


        public static void InstallPackage()
        {
            // 打开文件选择对话框
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Plugin Packages (*.cvxp;*.zip)|*.cvxp;*.zip|Plugin Package (*.cvxp)|*.cvxp|Zip Archive (*.zip)|*.zip|All Files (*.*)|*.*",
                FilterIndex = 1,
                Title = Resources.MarketplaceSelectUpdateFile
            };

            if (openFileDialog.ShowDialog() == true)
            {
                string selectedZipPath = openFileDialog.FileName;
                PluginUpdater.UpdatePlugin(selectedZipPath);
            }
        }
    }
}
