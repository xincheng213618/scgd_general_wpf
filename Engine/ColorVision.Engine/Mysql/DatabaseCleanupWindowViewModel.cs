using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Database
{
    public sealed class DatabaseCleanupSourceViewModel : ViewModelBase
    {
        private readonly IDatabaseCleanupSourceProvider _provider;
        private readonly IDatabaseCleanupSelectionProvider? _selectionProvider;
        private readonly IDatabaseCleanupBackupProvider? _backupProvider;
        private readonly IDatabaseCleanupMaintenanceProvider? _maintenanceProvider;
        private readonly IDatabaseCleanupMigrationProvider? _migrationProvider;

        private string _description = string.Empty;
        private string _keepMonthsText = "3";
        private string _status = "打开窗口后会自动统计。";
        private bool _isBusy;
        private bool _backupBeforeCleanup;
        private bool _suppressTableStateNotifications;

        public DatabaseCleanupSourceViewModel(IDatabaseCleanupSourceProvider provider)
        {
            _provider = provider;
            _selectionProvider = provider as IDatabaseCleanupSelectionProvider;
            _backupProvider = provider as IDatabaseCleanupBackupProvider;
            _maintenanceProvider = provider as IDatabaseCleanupMaintenanceProvider;
            _migrationProvider = provider as IDatabaseCleanupMigrationProvider;
            _description = provider.Description;
            _backupBeforeCleanup = _backupProvider != null;

            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsBusy);
            BackupCommand = new RelayCommand(_ => _ = ExecuteBackupAsync(), _ => !IsBusy && SupportsBackup);
            SelectAllCommand = new RelayCommand(_ => SetAllExistingTablesSelected(true), _ => !IsBusy && SupportsTableCleanup && ExistingTableCount > 0);
            ClearSelectionCommand = new RelayCommand(_ => SetAllExistingTablesSelected(false), _ => !IsBusy && SelectedTableCount > 0);
            CleanupSelectedCommand = new RelayCommand(_ => ExecuteCleanupSelected(), _ => !IsBusy && SupportsTableCleanup && SelectedTableCount > 0);
            CleanupHistoryCommand = new RelayCommand(_ => ExecuteCleanupHistory(), _ => !IsBusy && ExistingTableCount > 0);
            CleanupAllCommand = new RelayCommand(_ => ExecuteCleanupAll(), _ => !IsBusy && ExistingTableCount > 0);
            MigrationCommand = new RelayCommand(_ => ExecuteMigration(), _ => !IsBusy && SupportsMigration && ExistingTableCount > 0);
        }

        public string SourceId => _provider.Id;
        public string DisplayName => _provider.DisplayName;
        public int Order => _provider.Order;
        public bool SupportsTableCleanup => _selectionProvider != null;
        public bool SupportsBackup => _backupProvider != null;
        public bool SupportsMigration => _migrationProvider != null;
        public string MigrationActionName => _migrationProvider?.MigrationActionName ?? string.Empty;
        public ObservableCollection<DatabaseCleanupTableInfo> Tables { get; } = new();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackupCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand ClearSelectionCommand { get; }
        public RelayCommand CleanupSelectedCommand { get; }
        public RelayCommand CleanupHistoryCommand { get; }
        public RelayCommand CleanupAllCommand { get; }
        public RelayCommand MigrationCommand { get; }

        public string Description
        {
            get => _description;
            set
            {
                _description = value;
                OnPropertyChanged();
            }
        }

        public string KeepMonthsText
        {
            get => _keepMonthsText;
            set
            {
                _keepMonthsText = value;
                OnPropertyChanged();
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                _status = value;
                OnPropertyChanged();
            }
        }

        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                if (_isBusy == value)
                    return;

                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool BackupBeforeCleanup
        {
            get => _backupBeforeCleanup;
            set
            {
                bool nextValue = value && SupportsBackup;
                if (_backupBeforeCleanup == nextValue)
                    return;

                _backupBeforeCleanup = nextValue;
                OnPropertyChanged();
            }
        }

        public int ExistingTableCount => Tables.Count(item => item.Exists);
        public int SelectedTableCount => Tables.Count(item => item.Exists && item.IsSelected);
        public long ExistingRowCount => Tables.Where(item => item.Exists).Sum(item => item.RowCount);
        public string ExistingSizeDisplay => FormatSize(Tables.Where(item => item.Exists).Sum(item => item.SizeBytes));
        public string TableSummary => $"{ExistingTableCount:N0} 张表 · {ExistingRowCount:N0} 行 · {ExistingSizeDisplay}";
        public string SelectionSummary => SelectedTableCount > 0 ? $"已选择 {SelectedTableCount:N0} 张表" : "尚未选择数据表";

        public async Task RefreshAsync()
        {
            if (IsBusy)
                return;

            var selectedTableNames = GetSelectedTableNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            SetBusy(true);
            SetStatus("正在统计表数据...");

            try
            {
                SetDescription(_provider.Description);
                var snapshot = await Task.Run(_provider.LoadTables).ConfigureAwait(false);
                ApplySnapshot(snapshot, selectedTableNames);

                int existingCount = snapshot.Count(item => item.Exists);
                SetStatus(existingCount > 0
                    ? $"已加载 {existingCount:N0} 张可清理数据表。"
                    : "当前没有找到可清理数据表。");
            }
            catch (Exception ex)
            {
                SetStatus("加载统计失败。");
                ShowMessage($"{DisplayName} 统计失败：{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ExecuteBackupAsync()
        {
            if (_backupProvider == null || IsBusy)
                return;

            SetBusy(true);
            SetStatus($"正在创建 {DisplayName} 完整备份...");

            try
            {
                var backup = await Task.Run(_backupProvider.CreateBackup).ConfigureAwait(false);
                SetStatus(backup.StatusMessage);
                ShowMessage($"{backup.StatusMessage}{Environment.NewLine}{backup.FilePath}", MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                SetStatus("完整备份失败。");
                ShowMessage($"{DisplayName} 完整备份失败：{ex.Message}", MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ExecuteCleanupHistory()
        {
            if (!TryGetKeepMonths(out int keepMonths))
            {
                ShowMessage("保留月数必须是大于 0 的整数。", MessageBoxImage.Warning);
                return;
            }

            string confirmMessage =
                $"将保留最近 {keepMonths} 个月的数据，并清理其余历史结果。{Environment.NewLine}{Environment.NewLine}" +
                BuildBackupNotice() + Environment.NewLine + Environment.NewLine +
                "是否继续？";

            if (!ConfirmCleanup(confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                () => _provider.CleanupHistory(keepMonths),
                $"正在清理 {DisplayName} 历史数据...");
        }

        private void ExecuteCleanupSelected()
        {
            if (_selectionProvider == null)
                return;

            var selectedTableNames = GetSelectedTableNames();
            if (selectedTableNames.Count == 0)
            {
                ShowMessage("请先选择至少一张要清理的数据表。", MessageBoxImage.Warning);
                return;
            }

            string confirmMessage =
                $"将清空以下 {selectedTableNames.Count:N0} 张数据表：{Environment.NewLine}" +
                BuildTableList(selectedTableNames) + Environment.NewLine + Environment.NewLine +
                BuildBackupNotice() + Environment.NewLine + Environment.NewLine +
                "此操作不可撤销，是否继续？";

            if (!ConfirmCleanup(confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                () => _selectionProvider.CleanupTables(selectedTableNames),
                $"正在清空选中的 {selectedTableNames.Count:N0} 张数据表...");
        }

        private void ExecuteCleanupAll()
        {
            var existingTableNames = GetExistingTableNames();
            string confirmMessage =
                $"危险操作：将清空 {DisplayName} 中全部 {existingTableNames.Count:N0} 张可用数据表。{Environment.NewLine}{Environment.NewLine}" +
                BuildBackupNotice() + Environment.NewLine + Environment.NewLine +
                "全部数据清理后无法撤销，是否确定继续？";

            if (!ConfirmCleanup(confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(_provider.CleanupAll, $"正在清空 {DisplayName} 全部数据...");
        }

        private void ExecuteMigration()
        {
            if (_migrationProvider == null)
                return;

            if (_backupProvider == null)
            {
                ShowMessage("该迁移没有可用的完整备份能力，已拒绝执行。", MessageBoxImage.Error);
                return;
            }

            string confirmMessage =
                _migrationProvider.MigrationConfirmationMessage + Environment.NewLine + Environment.NewLine +
                BuildBackupNotice(forceBackup: true) + Environment.NewLine + Environment.NewLine +
                "是否继续？";
            if (!ConfirmCleanup(confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                _migrationProvider.ExecuteMigration,
                $"正在执行 {DisplayName} 数据迁移并释放空间...",
                "迁移",
                forceBackup: true);
        }

        private async Task ExecuteCleanupAsync(
            Func<DatabaseCleanupExecutionResult> action,
            string busyStatus,
            string operationName = "清理",
            bool forceBackup = false)
        {
            if (IsBusy)
                return;

            SetBusy(true);
            DatabaseCleanupBackupResult? backup = null;
            DatabaseCleanupExecutionResult? result = null;

            try
            {
                if ((forceBackup || BackupBeforeCleanup) && _backupProvider != null)
                {
                    if (_maintenanceProvider != null)
                    {
                        SetStatus($"正在同一维护操作中创建完整备份并执行{operationName}...");
                        try
                        {
                            var maintenanceResult = await Task.Run(() => ExecuteBackupAndCleanup(
                                _backupProvider,
                                _maintenanceProvider,
                                action)).ConfigureAwait(false);
                            backup = maintenanceResult.Backup;
                            result = maintenanceResult.Cleanup;
                        }
                        catch (Exception ex)
                        {
                            SetStatus($"完整备份与{operationName}组合操作失败。");
                            ShowMessage(
                                $"{DisplayName} 完整备份与{operationName}组合操作失败：{ex.Message}{Environment.NewLine}" +
                                "如备份已经生成，备份文件会保留；请刷新统计后确认当前数据状态。",
                                MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        SetStatus($"正在创建{operationName}前完整备份...");
                        try
                        {
                            backup = await Task.Run(_backupProvider.CreateBackup).ConfigureAwait(false);
                        }
                        catch (Exception ex)
                        {
                            SetStatus($"备份失败，未执行{operationName}。");
                            ShowMessage($"{operationName}前完整备份失败，数据尚未更改：{ex.Message}", MessageBoxImage.Error);
                            return;
                        }
                    }
                }

                if (result == null)
                {
                    SetStatus(busyStatus);
                    try
                    {
                        result = await Task.Run(action).ConfigureAwait(false);
                    }
                    catch (Exception ex)
                    {
                        SetStatus($"{operationName}失败。");
                        ShowMessage($"{DisplayName} {operationName}失败：{ex.Message}", MessageBoxImage.Error);
                        return;
                    }
                }

                Exception? refreshError = null;
                try
                {
                    SetDescription(_provider.Description);
                    var snapshot = await Task.Run(_provider.LoadTables).ConfigureAwait(false);
                    ApplySnapshot(snapshot, GetSelectedTableNames().ToHashSet(StringComparer.OrdinalIgnoreCase));
                }
                catch (Exception ex)
                {
                    refreshError = ex;
                }

                string status = backup == null
                    ? result.StatusMessage
                    : $"{result.StatusMessage} {backup.StatusMessage}";
                if (refreshError != null)
                {
                    status += " 表统计刷新失败，请稍后手动刷新。";
                }
                SetStatus(status);

                var messageLines = new List<string>();
                if (backup != null)
                {
                    messageLines.Add(backup.StatusMessage);
                    messageLines.Add(backup.FilePath);
                    messageLines.Add(string.Empty);
                }
                messageLines.AddRange(result.SummaryLines.Count > 0 ? result.SummaryLines : new[] { result.StatusMessage });
                if (refreshError != null)
                {
                    messageLines.Add(string.Empty);
                    messageLines.Add($"数据已完成{operationName}，但统计刷新失败：{refreshError.Message}");
                }

                ShowMessage(string.Join(Environment.NewLine, messageLines), refreshError == null ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            finally
            {
                SetBusy(false);
            }
        }

        internal static DatabaseCleanupMaintenanceResult ExecuteBackupAndCleanup(
            IDatabaseCleanupBackupProvider backupProvider,
            IDatabaseCleanupMaintenanceProvider? maintenanceProvider,
            Func<DatabaseCleanupExecutionResult> cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(backupProvider);
            ArgumentNullException.ThrowIfNull(cleanupAction);

            if (maintenanceProvider != null)
                return maintenanceProvider.ExecuteCleanupWithBackup(cleanupAction);

            return new DatabaseCleanupMaintenanceResult
            {
                Backup = backupProvider.CreateBackup(),
                Cleanup = cleanupAction()
            };
        }

        private bool TryGetKeepMonths(out int keepMonths)
        {
            return int.TryParse(KeepMonthsText, out keepMonths) && keepMonths > 0;
        }

        private string BuildBackupNotice(bool forceBackup = false)
        {
            if (!SupportsBackup)
                return "当前数据源不支持自动备份，请确认已有可恢复副本。";

            if (forceBackup)
                return "本次迁移会强制先创建完整备份；备份失败时不会执行迁移。";

            return BackupBeforeCleanup
                ? "操作前会先创建完整备份；备份失败时不会继续。"
                : "本次操作不会自动创建备份。";
        }

        private IReadOnlyList<string> GetExistingTableNames()
        {
            return RunOnUi(() => (IReadOnlyList<string>)Tables
                .Where(item => item.Exists)
                .Select(item => item.TableName)
                .ToArray());
        }

        private IReadOnlyList<string> GetSelectedTableNames()
        {
            return RunOnUi(() => (IReadOnlyList<string>)Tables
                .Where(item => item.Exists && item.IsSelected)
                .Select(item => item.TableName)
                .ToArray());
        }

        private static string BuildTableList(IEnumerable<string> tableNames)
        {
            return string.Join(Environment.NewLine, tableNames.Select(tableName => $"• {tableName}"));
        }

        private void SetAllExistingTablesSelected(bool isSelected)
        {
            RunOnUi(() =>
            {
                _suppressTableStateNotifications = true;
                try
                {
                    foreach (var item in Tables.Where(item => item.Exists))
                    {
                        item.IsSelected = isSelected;
                    }
                }
                finally
                {
                    _suppressTableStateNotifications = false;
                }

                NotifyTableStateChanged();
            });
        }

        private void ApplySnapshot(IReadOnlyList<DatabaseCleanupTableInfo> snapshot, ISet<string> selectedTableNames)
        {
            RunOnUi(() =>
            {
                foreach (var item in Tables)
                {
                    item.PropertyChanged -= TableInfo_PropertyChanged;
                }

                Tables.Clear();
                foreach (var item in snapshot)
                {
                    item.IsSelected = selectedTableNames.Contains(item.TableName);
                    item.PropertyChanged += TableInfo_PropertyChanged;
                    Tables.Add(item);
                }

                NotifyTableStateChanged();
            });
        }

        private void TableInfo_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!_suppressTableStateNotifications && (e.PropertyName == nameof(DatabaseCleanupTableInfo.IsSelected) || e.PropertyName == nameof(DatabaseCleanupTableInfo.Exists)))
            {
                NotifyTableStateChanged();
            }
        }

        private void NotifyTableStateChanged()
        {
            OnPropertyChanged(nameof(ExistingTableCount));
            OnPropertyChanged(nameof(SelectedTableCount));
            OnPropertyChanged(nameof(ExistingRowCount));
            OnPropertyChanged(nameof(ExistingSizeDisplay));
            OnPropertyChanged(nameof(TableSummary));
            OnPropertyChanged(nameof(SelectionSummary));
            CommandManager.InvalidateRequerySuggested();
        }

        private bool ConfirmCleanup(string message, MessageBoxImage image)
        {
            return RunOnUi(() => MessageBox1.Show(
                Application.Current.GetActiveWindow(),
                message,
                DisplayName,
                MessageBoxButton.OKCancel,
                image) == MessageBoxResult.OK);
        }

        private void SetDescription(string description) => RunOnUi(() => Description = description);
        private void SetStatus(string status) => RunOnUi(() => Status = status);
        private void SetBusy(bool isBusy) => RunOnUi(() => IsBusy = isBusy);

        private void ShowMessage(string message, MessageBoxImage image)
        {
            RunOnUi(() => MessageBox1.Show(
                Application.Current.GetActiveWindow(),
                message,
                DisplayName,
                MessageBoxButton.OK,
                image));
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0)
                return "0 B";

            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int unitIndex = 0;
            while (size >= 1024 && unitIndex < units.Length - 1)
            {
                size /= 1024;
                unitIndex++;
            }

            return $"{size:0.##} {units[unitIndex]}";
        }

        private static void RunOnUi(Action action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Application.Current.Dispatcher.Invoke(action);
        }

        private static T RunOnUi<T>(Func<T> action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
                return action();

            return Application.Current.Dispatcher.Invoke(action);
        }
    }

    public sealed class DatabaseCleanupWindowViewModel : ViewModelBase
    {
        private DatabaseCleanupSourceViewModel? _selectedSource;

        public ObservableCollection<DatabaseCleanupSourceViewModel> Sources { get; } = new();

        public DatabaseCleanupSourceViewModel? SelectedSource
        {
            get => _selectedSource;
            set
            {
                _selectedSource = value;
                OnPropertyChanged();
            }
        }

        public DatabaseCleanupWindowViewModel()
        {
            AssemblyHandler.GetInstance().RefreshAssemblies();

            var providers = AssemblyHandler.GetInstance()
                .LoadImplementations<IDatabaseCleanupSourceProvider>()
                .OrderBy(provider => provider.Order)
                .ThenBy(provider => provider.DisplayName)
                .ToList();

            foreach (var provider in providers)
            {
                Sources.Add(new DatabaseCleanupSourceViewModel(provider));
            }

            SelectedSource = Sources.FirstOrDefault();
        }

        public Task RefreshAllAsync()
        {
            return Task.WhenAll(Sources.Select(source => source.RefreshAsync()));
        }
    }
}
