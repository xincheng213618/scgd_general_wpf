using ColorVision.Common.Utilities;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Plugins;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Recovery
{
    public partial class StartupRecoveryWindow : Window, INotifyPropertyChanged, IDisposable
    {
        private readonly StartupFailureInfo? _previousFailure;
        private readonly CancellationTokenSource _windowCancellation = new();
        private AutoUpdatePlan? _applicationUpdatePlan;
        private bool _isCheckingUpdates;
        private bool _isRecoveryBusy;
        private bool _isRefreshingPlugins;
        private bool _resultWasChosen;
        private bool _isDisposed;
        private string _updateStatusTitle = "正在检查主程序更新";
        private string _updateStatusDetail = "正在连接更新服务，请稍候。";
        private string _updateActionText = "检查中...";
        private string _operationStatusText = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public ObservableCollection<StartupRecoveryPluginItem> Plugins { get; } = new();

        public StartupRecoveryResult Result { get; private set; } = StartupRecoveryResult.Exit;

        public string PluginsDirectory { get; } = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Plugins");

        public string CurrentApplicationVersionText { get; }

        public Visibility FailureVisibility => _previousFailure == null
            ? Visibility.Collapsed
            : Visibility.Visible;

        public string FailureSummary => BuildFailureSummary(_previousFailure);

        public string UpdateStatusTitle
        {
            get => _updateStatusTitle;
            private set => SetProperty(ref _updateStatusTitle, value);
        }

        public string UpdateStatusDetail
        {
            get => _updateStatusDetail;
            private set => SetProperty(ref _updateStatusDetail, value);
        }

        public string UpdateActionText
        {
            get => _updateActionText;
            private set => SetProperty(ref _updateActionText, value);
        }

        public string OperationStatusText
        {
            get => _operationStatusText;
            private set => SetProperty(ref _operationStatusText, value);
        }

        public Visibility UpdateProgressVisibility => _isCheckingUpdates
            ? Visibility.Visible
            : Visibility.Collapsed;

        public bool CanRunApplicationUpdate =>
            !_isCheckingUpdates &&
            !_isRecoveryBusy &&
            !_isRefreshingPlugins &&
            (_applicationUpdatePlan != null || AutoUpdater.CurrentVersion != null);

        public bool CanRefreshPlugins => !_isRefreshingPlugins && !_isRecoveryBusy;

        public bool CanUseSelection =>
            !_isRecoveryBusy &&
            !_isRefreshingPlugins &&
            Plugins.Any(item => item.IsSelected && !item.IsBackupOnly);

        public bool CanContinueStartup => !_isRecoveryBusy && !_isRefreshingPlugins;

        public bool CanOpenOtherRecovery => !_isRecoveryBusy && !_isRefreshingPlugins;

        public string PluginSummaryText
        {
            get
            {
                int selectedCount = Plugins.Count(item => item.IsSelected);
                int backupOnlyCount = Plugins.Count(item => item.IsBackupOnly);
                string backupOnlyText = backupOnlyCount == 0
                    ? string.Empty
                    : $"，其中 {backupOnlyCount} 个仅备份可恢复";
                return selectedCount == 0
                    ? $"已发现 {Plugins.Count} 个插件项{backupOnlyText}"
                    : $"已发现 {Plugins.Count} 个，已选 {selectedCount} 个{backupOnlyText}";
            }
        }

        public StartupRecoveryWindow()
            : this(StartupRegistryChecker.PreviousFailure)
        {
        }

        public StartupRecoveryWindow(StartupFailureInfo? previousFailure)
        {
            _previousFailure = previousFailure;
            CurrentApplicationVersionText =
                $"当前版本 {AutoUpdater.CurrentVersion?.ToString() ?? "未知"}";
            DataContext = this;
            InitializeComponent();
            this.ApplyCaption();

            Loaded += StartupRecoveryWindow_Loaded;
            Closing += StartupRecoveryWindow_Closing;
            Closed += StartupRecoveryWindow_Closed;
        }

        private async void StartupRecoveryWindow_Loaded(object sender, RoutedEventArgs e)
        {
            Loaded -= StartupRecoveryWindow_Loaded;
            await Task.WhenAll(
                RefreshPluginsAsync(_windowCancellation.Token),
                CheckForApplicationUpdateAsync(_windowCancellation.Token));
        }

        private void StartupRecoveryWindow_Closed(object? sender, EventArgs e)
        {
            Dispose();
        }

        private void StartupRecoveryWindow_Closing(object? sender, CancelEventArgs e)
        {
            if (!_isRecoveryBusy || _resultWasChosen)
                return;

            // Closing the recovery window is an explicit exit/cancel choice. Keep Result=Exit so
            // the startup caller never continues plugin loading while an operation is in flight.
            Result = StartupRecoveryResult.Exit;
            _windowCancellation.Cancel();
        }

        public void Dispose()
        {
            if (_isDisposed)
                return;

            _isDisposed = true;
            _windowCancellation.Cancel();
            _windowCancellation.Dispose();
            GC.SuppressFinalize(this);
        }

        private async Task RefreshPluginsAsync(CancellationToken cancellationToken)
        {
            if (_isRefreshingPlugins)
                return;

            _isRefreshingPlugins = true;
            OnPropertyChanged(nameof(CanRefreshPlugins));
            OnPropertyChanged(nameof(CanUseSelection));
            OnPropertyChanged(nameof(CanContinueStartup));
            OnPropertyChanged(nameof(CanRunApplicationUpdate));
            OnPropertyChanged(nameof(CanOpenOtherRecovery));
            OperationStatusText = "正在扫描插件...";

            try
            {
                StartupRecoveryPluginItem[] scannedPlugins = await Task.Run(() =>
                    StartupRecoveryPluginScanner
                        .Scan(PluginsDirectory, _previousFailure)
                        .ToArray(),
                    cancellationToken).ConfigureAwait(true);

                foreach (StartupRecoveryPluginItem existingItem in Plugins)
                    existingItem.PropertyChanged -= PluginItem_PropertyChanged;

                Plugins.Clear();
                foreach (StartupRecoveryPluginItem item in scannedPlugins)
                {
                    item.PropertyChanged += PluginItem_PropertyChanged;
                    Plugins.Add(item);
                }

                OperationStatusText = Plugins.Count == 0
                    ? "未发现插件目录"
                    : "插件清单已加载";

                await LoadAvailableBackupsAsync(scannedPlugins, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                OperationStatusText = $"插件扫描失败：{ex.GetBaseException().Message}";
            }
            finally
            {
                _isRefreshingPlugins = false;
                OnPropertyChanged(nameof(CanRefreshPlugins));
                OnPropertyChanged(nameof(CanUseSelection));
                OnPropertyChanged(nameof(CanContinueStartup));
                OnPropertyChanged(nameof(CanRunApplicationUpdate));
                OnPropertyChanged(nameof(CanOpenOtherRecovery));
                OnPropertyChanged(nameof(PluginSummaryText));
            }
        }

        private static async Task LoadAvailableBackupsAsync(
            StartupRecoveryPluginItem[] plugins,
            CancellationToken cancellationToken)
        {
            (StartupRecoveryPluginItem Item, PluginRecoveryBackupInfo? Backup)[] backups = await Task.Run(() =>
            {
                var results = new (StartupRecoveryPluginItem, PluginRecoveryBackupInfo?)[plugins.Length];
                for (int index = 0; index < plugins.Length; index++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    StartupRecoveryPluginItem item = plugins[index];
                    PluginRecoveryBackupInfo? backup = item.Backup;
                    try
                    {
                        backup ??= PluginRecoveryBackupService.Instance.GetAvailableBackup(
                                item.PluginId ?? item.DirectoryName,
                                item.DirectoryPath);
                    }
                    catch
                    {
                        // A damaged or inaccessible backup must not hide the plugin from recovery.
                    }

                    results[index] = (item, backup);
                }

                return results;
            }, cancellationToken).ConfigureAwait(true);

            foreach ((StartupRecoveryPluginItem item, PluginRecoveryBackupInfo? backup) in backups)
                item.SetBackup(backup);
        }

        private async Task CheckForApplicationUpdateAsync(CancellationToken cancellationToken)
        {
            _isCheckingUpdates = true;
            OnUpdateStateChanged();

            try
            {
                if (!WindowsNetworkState.IsConnectedToInternet())
                {
                    SetRepairState(
                        "未连接到 Internet",
                        "暂时无法检查更新；修复当前版本需要连接更新服务。",
                        "修复当前版本");
                    return;
                }

                AutoUpdatePlanCheckResult checkResult = await AutoUpdater
                    .GetUpdatePlanCheckResultAsync(forceRefresh: true, cancellationToken)
                    .ConfigureAwait(true);
                _applicationUpdatePlan = checkResult.Plan;

                if (checkResult.Status == UpdateServerCheckStatus.NoInternetConnection)
                {
                    SetRepairState(
                        "未连接到 Internet",
                        "暂时无法检查更新；修复当前版本需要连接更新服务。",
                        "修复当前版本");
                    return;
                }

                if (checkResult.Status == UpdateServerCheckStatus.ServerUnavailable)
                {
                    SetRepairState(
                        "无法连接更新服务",
                        "未能确认是否存在新版本。可稍后重试，或尝试修复当前版本。",
                        "修复当前版本");
                    return;
                }

                if (_applicationUpdatePlan != null)
                {
                    Version targetVersion = _applicationUpdatePlan.TargetVersion;
                    UpdateStatusTitle = $"发现可用版本 {targetVersion}";
                    UpdateStatusDetail =
                        $"当前版本 {_applicationUpdatePlan.CurrentVersion}。更新完成后 ColorVision 将自动重启。";
                    UpdateActionText = $"更新到 {targetVersion} 并重启";
                }
                else
                {
                    string currentVersion = AutoUpdater.CurrentVersion?.ToString() ?? "未知";
                    SetRepairState(
                        "主程序已是最新版本",
                        $"检查完成，当前版本为 {currentVersion}。如果程序文件可能损坏，可重新安装当前版本。",
                        "修复当前版本");
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetRepairState(
                    "更新检查失败",
                    $"{ex.GetBaseException().Message} 可稍后重试，或尝试修复当前版本。",
                    "修复当前版本");
            }
            finally
            {
                _isCheckingUpdates = false;
                OnUpdateStateChanged();
            }
        }

        private void SetRepairState(string title, string detail, string actionText)
        {
            _applicationUpdatePlan = null;
            UpdateStatusTitle = title;
            UpdateStatusDetail = detail;
            UpdateActionText = actionText;
        }

        private void OnUpdateStateChanged()
        {
            OnPropertyChanged(nameof(UpdateProgressVisibility));
            OnPropertyChanged(nameof(CanRunApplicationUpdate));
        }

        private void PluginItem_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName != nameof(StartupRecoveryPluginItem.IsSelected))
                return;

            OnPropertyChanged(nameof(CanUseSelection));
            OnPropertyChanged(nameof(PluginSummaryText));
        }

        private async void RefreshPlugins_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRefreshPlugins)
                return;

            await RefreshPluginsAsync(_windowCancellation.Token);
        }

        private void ApplicationUpdate_Click(object sender, RoutedEventArgs e)
        {
            if (!CanRunApplicationUpdate)
                return;

            if (_applicationUpdatePlan == null)
            {
                Version? currentVersion = AutoUpdater.CurrentVersion;
                if (currentVersion == null)
                    return;

                string message =
                    $"将下载并运行 ColorVision {currentVersion} 的完整安装程序，用于修复当前版本。确定继续？";
                if (MessageBox.Show(this, message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
                    return;

                StartApplicationUpdate(() => AutoUpdater.StartFullUpdate(currentVersion, OnApplicationUpdateDownloadFailed));
                return;
            }

            AutoUpdatePlan plan = _applicationUpdatePlan;
            StartApplicationUpdate(() => AutoUpdater.StartUpdatePlan(plan, OnApplicationUpdateDownloadFailed));
        }

        private void StartApplicationUpdate(Action startUpdate)
        {
            SetRecoveryBusy(true);
            UpdateStatusDetail = "正在准备下载安装包；完成后将退出并重启 ColorVision。";

            try
            {
                startUpdate();
            }
            catch (Exception ex)
            {
                SetRecoveryBusy(false);
                UpdateStatusDetail = $"无法启动更新：{ex.GetBaseException().Message}";
            }
        }

        private void OnApplicationUpdateDownloadFailed()
        {
            SetRecoveryBusy(false);
            UpdateStatusDetail = "下载安装包失败。请检查网络后重试，或打开更新日志目录查看原因。";
        }

        private async void RestorePluginBackup_Click(object sender, RoutedEventArgs e)
        {
            if (_isRecoveryBusy ||
                _isRefreshingPlugins ||
                sender is not Button { CommandParameter: StartupRecoveryPluginItem item } ||
                item.Backup == null)
                return;

            string message = $"将退出 ColorVision，并把插件“{item.DisplayName}”回退到最近一次有效备份。确定继续？";
            if (MessageBox.Show(this, message, "ColorVision", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;

            PluginRecoveryBackupInfo backup = item.Backup;
            SetRecoveryBusy(true);
            OperationStatusText = $"正在准备回退 {item.DisplayName}...";

            try
            {
                await PluginRecoveryBackupService.Instance
                    .RestoreAsync(backup, _windowCancellation.Token)
                    .ConfigureAwait(true);
                OperationStatusText = "插件回退已启动，ColorVision 即将退出。";
            }
            catch (OperationCanceledException) when (_windowCancellation.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                SetRecoveryBusy(false);
                OperationStatusText = $"插件回退失败：{ex.GetBaseException().Message}";
                MessageBox.Show(this, OperationStatusText, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void OpenApplicationSnapshots_Click(object sender, RoutedEventArgs e)
        {
            if (!CanOpenOtherRecovery)
                return;

            new ApplicationSnapshotsWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            }.ShowDialog();
        }

        private void OpenApplicationLog_Click(object sender, RoutedEventArgs e)
        {
            if (!CanOpenOtherRecovery)
                return;

            OpenDirectory(ResolveApplicationLogDirectory(), "主日志目录");
        }

        private void OpenUpdateLog_Click(object sender, RoutedEventArgs e)
        {
            if (!CanOpenOtherRecovery)
                return;

            OpenDirectory(GetUpdateLogDirectory(), "更新日志目录");
        }

        private void OpenDirectory(string directory, string displayName)
        {
            try
            {
                Directory.CreateDirectory(directory);
                PlatformHelper.OpenFolder(directory);
            }
            catch (Exception ex)
            {
                OperationStatusText = $"无法打开{displayName}：{ex.GetBaseException().Message}";
            }
        }

        private void NormalStart_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(StartupRecoveryAction.NormalStart, Array.Empty<StartupRecoveryPluginItem>());
        }

        private void SkipSelected_Click(object sender, RoutedEventArgs e)
        {
            StartupRecoveryPluginItem[] selected = GetSelectedPlugins();
            if (selected.Length == 0)
                return;

            CloseWithResult(StartupRecoveryAction.SkipSelectedOnce, selected);
        }

        private void SkipAll_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(
                StartupRecoveryAction.SkipAllOnce,
                Plugins.Where(item => !item.IsBackupOnly).ToArray());
        }

        private void DisableSelected_Click(object sender, RoutedEventArgs e)
        {
            StartupRecoveryPluginItem[] selected = GetSelectedPlugins();
            if (selected.Length == 0)
                return;

            try
            {
                PersistDisabledPlugins(selected);
                CloseWithResult(StartupRecoveryAction.DisableSelectedAndStart, selected);
            }
            catch (Exception ex)
            {
                OperationStatusText = $"保存插件禁用状态失败：{ex.GetBaseException().Message}";
                MessageBox.Show(this, OperationStatusText, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            CloseWithResult(StartupRecoveryAction.Exit, Array.Empty<StartupRecoveryPluginItem>());
        }

        private StartupRecoveryPluginItem[] GetSelectedPlugins() =>
            Plugins.Where(item => item.IsSelected && !item.IsBackupOnly).ToArray();

        private static void PersistDisabledPlugins(IEnumerable<StartupRecoveryPluginItem> selectedPlugins)
        {
            PluginLoaderrConfig config = PluginLoaderrConfig.Instance;
            foreach (StartupRecoveryPluginItem item in selectedPlugins)
            {
                KeyValuePair<string, PluginInfo> existingPair = config.Plugins
                    .FirstOrDefault(pair =>
                        string.Equals(pair.Key, item.PluginKey, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(pair.Key, item.DirectoryName, StringComparison.OrdinalIgnoreCase));

                PluginInfo pluginInfo;
                if (existingPair.Value != null)
                {
                    pluginInfo = existingPair.Value;
                }
                else
                {
                    pluginInfo = new PluginInfo
                    {
                        Manifest = new PluginManifest
                        {
                            Id = item.PluginId ?? item.DirectoryName,
                            Name = item.DisplayName,
                            Version = item.VersionText,
                        },
                    };
                    config.Plugins[item.PluginKey] = pluginInfo;
                }

                pluginInfo.Enabled = false;
            }

            config.Save();
        }

        private void CloseWithResult(
            StartupRecoveryAction action,
            IReadOnlyCollection<StartupRecoveryPluginItem> selectedPlugins)
        {
            if (_resultWasChosen || _isRecoveryBusy)
                return;

            _resultWasChosen = true;
            Result = new StartupRecoveryResult(
                action,
                selectedPlugins.Select(item => item.ToSelection()).ToArray());

            try
            {
                DialogResult = action != StartupRecoveryAction.Exit;
            }
            catch (InvalidOperationException)
            {
                Close();
            }
        }

        private void SetRecoveryBusy(bool isBusy)
        {
            if (_isRecoveryBusy == isBusy)
                return;

            _isRecoveryBusy = isBusy;
            foreach (StartupRecoveryPluginItem item in Plugins)
                item.SetRecoveryBusy(isBusy);

            OnPropertyChanged(nameof(CanRunApplicationUpdate));
            OnPropertyChanged(nameof(CanRefreshPlugins));
            OnPropertyChanged(nameof(CanUseSelection));
            OnPropertyChanged(nameof(CanContinueStartup));
            OnPropertyChanged(nameof(CanOpenOtherRecovery));
        }

        private static string BuildFailureSummary(StartupFailureInfo? failure)
        {
            if (failure == null)
                return string.Empty;

            List<string> details = new();
            if (!string.IsNullOrWhiteSpace(failure.Stage))
                details.Add($"阶段：{failure.Stage}");
            if (!string.IsNullOrWhiteSpace(failure.Component))
                details.Add($"组件：{failure.Component}");
            if (!string.IsNullOrWhiteSpace(failure.Version))
                details.Add($"版本：{failure.Version}");
            if (failure.StartedAt.HasValue)
                details.Add($"时间：{failure.StartedAt.Value.ToLocalTime():yyyy-MM-dd HH:mm:ss}");

            return details.Count == 0
                ? "上次启动未完成；没有记录到可确认的故障组件。"
                : $"上次启动未完成（{string.Join("；", details)}）。“疑似”仅表示与记录匹配，不代表已确认插件有问题。";
        }

        private static string ResolveApplicationLogDirectory()
        {
            string? configuredPath = Environments.DirLog;
            if (string.IsNullOrWhiteSpace(configuredPath))
                return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");

            string fullPath = Path.GetFullPath(configuredPath);
            if (Directory.Exists(fullPath) || configuredPath.EndsWith(Path.DirectorySeparatorChar))
                return fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

            return Path.GetDirectoryName(fullPath) ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        }

        private static string GetUpdateLogDirectory()
        {
            string installationKey = ExitUpdateHandoff.GetInstallationKey(AppDomain.CurrentDomain.BaseDirectory);
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "ColorVision",
                "UpdateState",
                installationKey);
        }

        private void SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = "")
        {
            if (EqualityComparer<T>.Default.Equals(field, value))
                return;

            field = value;
            OnPropertyChanged(propertyName);
        }

        private void OnPropertyChanged([CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
