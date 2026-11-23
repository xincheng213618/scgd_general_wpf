using ColorVision.Common.MVVM;
using ColorVision.Themes.Controls;
using ColorVision.Engine;
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
        private readonly IDatabaseCleanupTargetProvider? _targetProvider;
        private readonly IDatabaseCleanupSelectionProvider? _selectionProvider;
        private readonly IDatabaseCleanupBackupProvider? _backupProvider;
        private readonly IDatabaseCleanupMigrationProvider? _migrationProvider;
        private readonly IDatabaseCleanupOptimizationProvider? _optimizationProvider;
        private readonly Func<string, MessageBoxImage, bool>? _confirmOverride;
        private readonly Action<string, MessageBoxImage>? _messageOverride;
        private DatabaseCleanupTarget? _statisticsTarget;

        private sealed record CleanupPlan(IDatabaseCleanupSourceProvider Provider, DatabaseCleanupTarget? Target, bool BackupBeforeCleanup);
        private sealed class CleanupTargetChangedException() : InvalidOperationException(TargetChangedMessage) { }

        private string _description = string.Empty;
        private string _keepMonthsText = "3";
        private string _status = EngineLocalization.Get("打开窗口后会自动统计。");
        private bool _isBusy;
        private bool _backupBeforeCleanup;
        private bool _hasPendingMigration;
        private bool _suppressTableStateNotifications;

        public DatabaseCleanupSourceViewModel(IDatabaseCleanupSourceProvider provider) : this(provider, null, null)
        {
        }

        internal DatabaseCleanupSourceViewModel(IDatabaseCleanupSourceProvider provider,
            Func<string, MessageBoxImage, bool>? confirmOverride, Action<string, MessageBoxImage>? messageOverride)
        {
            _provider = provider;
            _targetProvider = provider as IDatabaseCleanupTargetProvider;
            _selectionProvider = provider as IDatabaseCleanupSelectionProvider;
            _backupProvider = provider as IDatabaseCleanupBackupProvider;
            _migrationProvider = provider as IDatabaseCleanupMigrationProvider;
            _optimizationProvider = provider as IDatabaseCleanupOptimizationProvider;
            _confirmOverride = confirmOverride;
            _messageOverride = messageOverride;
            _description = _targetProvider == null ? provider.Description : string.Empty;
            _backupBeforeCleanup = false;

            RefreshCommand = new RelayCommand(_ => _ = RefreshAsync(), _ => !IsBusy);
            BackupCommand = new RelayCommand(_ => _ = ExecuteBackupAsync(), _ => CanRunOperation && SupportsBackup);
            SelectAllCommand = new RelayCommand(_ => SetAllExistingTablesSelected(true), _ => CanRunOperation && SupportsTableCleanup && ExistingTableCount > 0);
            ClearSelectionCommand = new RelayCommand(_ => SetAllExistingTablesSelected(false), _ => !IsBusy && SelectedTableCount > 0);
            CleanupSelectedCommand = new RelayCommand(_ => ExecuteCleanupSelected(), _ => CanRunOperation && SupportsTableCleanup && SelectedTableCount > 0);
            CleanupHistoryCommand = new RelayCommand(_ => ExecuteCleanupHistory(), _ => CanRunOperation && ExistingTableCount > 0 && IsKeepMonthsValid);
            CleanupAllCommand = new RelayCommand(_ => ExecuteCleanupAll(), _ => CanRunOperation && ExistingTableCount > 0);
            MigrationCommand = new RelayCommand(_ => ExecuteMigration(), _ => CanRunOperation && SupportsMigration && HasPendingMigration && ExistingTableCount > 0);
            OptimizationCommand = new RelayCommand(_ => ExecuteOptimization(), _ => CanRunOperation && SupportsOptimization && ExistingTableCount > 0);
        }

        internal bool HasDatabaseTarget => _targetProvider != null;
        private bool CanRunOperation => !IsBusy && (!HasDatabaseTarget || _statisticsTarget?.IsCurrent == true);

        public string SourceId => _provider.Id;
        public string DisplayName => _provider.DisplayName;
        public int Order => _provider.Order;
        public bool SupportsTableCleanup => _selectionProvider != null;
        public bool SupportsBackup => _backupProvider != null;
        public bool SupportsMigration => _migrationProvider != null;
        public bool SupportsOptimization => _optimizationProvider != null;
        public bool HasPendingMigration
        {
            get => _hasPendingMigration;
            private set
            {
                if (_hasPendingMigration == value)
                    return;

                _hasPendingMigration = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }
        public string MigrationActionName => _migrationProvider?.MigrationActionName ?? string.Empty;
        public string OptimizationActionName => _optimizationProvider?.OptimizationActionName ?? string.Empty;
        public ObservableCollection<DatabaseCleanupTableInfo> Tables { get; } = new();

        public RelayCommand RefreshCommand { get; }
        public RelayCommand BackupCommand { get; }
        public RelayCommand SelectAllCommand { get; }
        public RelayCommand ClearSelectionCommand { get; }
        public RelayCommand CleanupSelectedCommand { get; }
        public RelayCommand CleanupHistoryCommand { get; }
        public RelayCommand CleanupAllCommand { get; }
        public RelayCommand MigrationCommand { get; }
        public RelayCommand OptimizationCommand { get; }

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
                if (_keepMonthsText == value)
                    return;

                _keepMonthsText = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsKeepMonthsValid));
                CommandManager.InvalidateRequerySuggested();
            }
        }

        public bool IsKeepMonthsValid => TryGetKeepMonths(out _);

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
        public string TableSummary => EngineLocalization.Format($"{ExistingTableCount:N0} 张表 · {ExistingRowCount:N0} 行") + Environment.NewLine + ExistingSizeDisplay;
        public string SelectionSummary => SelectedTableCount > 0
            ? EngineLocalization.Format($"已勾选 {SelectedTableCount:N0} 张表 · {Tables.Where(item => item.Exists && item.IsSelected).Sum(item => item.RowCount):N0} 行")
            : EngineLocalization.Get("尚未勾选数据表");

        public async Task RefreshAsync()
        {
            if (IsBusy)
                return;

            DatabaseCleanupTarget? previousTarget = _statisticsTarget;
            var selectedTableNames = GetSelectedTableNames().ToHashSet(StringComparer.OrdinalIgnoreCase);
            SetBusy(true);
            SetStatus(EngineLocalization.Get("正在统计表数据..."));

            try
            {
                var target = _targetProvider?.CaptureCurrentTarget();
                if (HasDatabaseTarget && target == null)
                {
                    ClearTargetStatistics(EngineLocalization.Get("请连接数据库并刷新统计。"));
                    return;
                }

                var provider = target?.Provider ?? _provider;
                var (snapshot, hasPendingMigration) = await Task.Run(() =>
                    ExecuteForCurrentTarget(target, () => LoadSnapshot(provider))).ConfigureAwait(false);
                RunOnUi(() =>
                {
                    if (target?.IsCurrent == false)
                    {
                        ClearTargetStatistics(TargetChangedMessage);
                        return;
                    }

                    if (previousTarget?.IsCurrent == false)
                        selectedTableNames.Clear();

                    _statisticsTarget = target;
                    Description = target?.Description ?? provider.Description;
                    ApplySnapshot(snapshot, selectedTableNames);
                    HasPendingMigration = hasPendingMigration;
                    int existingCount = snapshot.Count(item => item.Exists);
                    Status = existingCount > 0
                        ? EngineLocalization.Format($"已加载 {existingCount:N0} 张可清理数据表。")
                        : EngineLocalization.Get("当前没有找到可清理数据表。");
                });
            }
            catch (CleanupTargetChangedException)
            {
                ClearTargetStatistics(TargetChangedMessage);
            }
            catch (Exception ex)
            {
                if (HasDatabaseTarget) ClearTargetStatistics(EngineLocalization.Get("加载统计失败。"));
                SetStatus(EngineLocalization.Get("加载统计失败。"));
                ShowMessage(EngineLocalization.Format($"{DisplayName} 统计失败：{ex.Message}"), MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private async Task ExecuteBackupAsync()
        {
            var plan = CreateOperationPlan();
            if (plan?.Provider is not IDatabaseCleanupBackupProvider backupProvider)
                return;

            SetBusy(true);
            SetStatus(EngineLocalization.Format($"正在创建 {DisplayName} 完整备份..."));

            try
            {
                var backup = await Task.Run(() => ExecuteForCurrentTarget(plan.Target, backupProvider.CreateBackup)).ConfigureAwait(false);
                SetStatus(backup.StatusMessage);
                ShowMessage(DescribeTarget(plan, $"{backup.StatusMessage}{Environment.NewLine}{backup.FilePath}"), MessageBoxImage.Information);
            }
            catch (CleanupTargetChangedException)
            {
                ClearTargetStatistics(TargetChangedMessage);
            }
            catch (Exception ex)
            {
                SetStatus(EngineLocalization.Get("完整备份失败。"));
                ShowMessage(EngineLocalization.Format($"{DisplayName} 完整备份失败：{ex.Message}"), MessageBoxImage.Error);
            }
            finally
            {
                SetBusy(false);
            }
        }

        private void ExecuteCleanupHistory()
        {
            var plan = CreateOperationPlan();
            if (plan == null || ExistingTableCount == 0) return;
            if (!TryGetKeepMonths(out int keepMonths))
            {
                ShowMessage(EngineLocalization.Get("保留月数必须是大于 0 的整数。"), MessageBoxImage.Warning);
                return;
            }

            string confirmMessage =
                EngineLocalization.Format($"将保留最近 {keepMonths} 个月的数据，并清理其余历史结果。") + Environment.NewLine + Environment.NewLine +
                BuildBackupNotice(plan) + Environment.NewLine + Environment.NewLine +
                EngineLocalization.Get("是否继续？");

            if (!ConfirmCleanup(plan, confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                plan, () => plan.Provider.CleanupHistory(keepMonths),
                EngineLocalization.Format($"正在清理 {DisplayName} 历史数据..."));
        }

        private void ExecuteCleanupSelected()
        {
            var plan = CreateOperationPlan();
            if (plan?.Provider is not IDatabaseCleanupSelectionProvider selectionProvider)
                return;

            var selectedTableNames = GetSelectedTableNames();
            if (selectedTableNames.Count == 0)
            {
                ShowMessage(EngineLocalization.Get("请先选择至少一张要清理的数据表。"), MessageBoxImage.Warning);
                return;
            }

            string confirmMessage =
                EngineLocalization.Format($"将清空以下 {selectedTableNames.Count:N0} 张数据表：") + Environment.NewLine +
                BuildTableList(selectedTableNames) + Environment.NewLine + Environment.NewLine +
                BuildBackupNotice(plan) + Environment.NewLine + Environment.NewLine +
                EngineLocalization.Get("此操作不可撤销，是否继续？");

            if (!ConfirmCleanup(plan, confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                plan, () => selectionProvider.CleanupTables(selectedTableNames),
                EngineLocalization.Format($"正在清空选中的 {selectedTableNames.Count:N0} 张数据表..."));
        }

        private void ExecuteCleanupAll()
        {
            var plan = CreateOperationPlan();
            if (plan == null || ExistingTableCount == 0) return;
            var existingTableNames = GetExistingTableNames();
            string confirmMessage =
                EngineLocalization.Format($"将清空 {DisplayName} 中全部 {existingTableNames.Count:N0} 张可用数据表。") + Environment.NewLine + Environment.NewLine +
                BuildBackupNotice(plan) + Environment.NewLine + Environment.NewLine +
                EngineLocalization.Get("全部数据清理后无法撤销，是否确定继续？");

            if (!ConfirmCleanup(plan, confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(plan, plan.Provider.CleanupAll, EngineLocalization.Format($"正在清空 {DisplayName} 全部数据..."));
        }

        private void ExecuteMigration()
        {
            var plan = CreateOperationPlan();
            if (plan?.Provider is not IDatabaseCleanupMigrationProvider migrationProvider || !HasPendingMigration || ExistingTableCount == 0)
                return;

            if (plan.Provider is not IDatabaseCleanupBackupProvider)
            {
                ShowMessage(EngineLocalization.Get("该迁移没有可用的完整备份能力，已拒绝执行。"), MessageBoxImage.Error);
                return;
            }

            string confirmMessage =
                migrationProvider.MigrationConfirmationMessage + Environment.NewLine + Environment.NewLine +
                BuildBackupNotice(plan, forceBackup: true) + Environment.NewLine + Environment.NewLine +
                EngineLocalization.Get("是否继续？");
            if (!ConfirmCleanup(plan, confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                plan, migrationProvider.ExecuteMigration,
                EngineLocalization.Format($"正在执行 {DisplayName} 数据迁移并释放空间..."),
                "迁移",
                forceBackup: true,
                preflight: migrationProvider.HasPendingMigration);
        }

        private void ExecuteOptimization()
        {
            var plan = CreateOperationPlan();
            if (plan?.Provider is not IDatabaseCleanupOptimizationProvider optimizationProvider || ExistingTableCount == 0)
                return;

            string confirmMessage =
                optimizationProvider.OptimizationConfirmationMessage + Environment.NewLine + Environment.NewLine +
                EngineLocalization.Get("此操作不会删除业务数据，也不会自动创建完整备份。") + Environment.NewLine + Environment.NewLine +
                EngineLocalization.Get("是否继续？");
            if (!ConfirmCleanup(plan, confirmMessage, MessageBoxImage.Warning))
                return;

            _ = ExecuteCleanupAsync(
                plan, optimizationProvider.ExecuteOptimization,
                EngineLocalization.Format($"正在执行 {DisplayName} 索引优化..."),
                "索引优化",
                allowOptionalBackup: false,
                refreshTables: false);
        }

        private async Task ExecuteCleanupAsync(
            CleanupPlan plan,
            Func<DatabaseCleanupExecutionResult> action,
            string busyStatus,
            string operationName = "清理",
            bool forceBackup = false,
            bool allowOptionalBackup = true,
            bool refreshTables = true,
            Func<bool>? preflight = null)
        {
            if (IsBusy || !ValidateOperationPlan(plan))
                return;

            string localizedOperationName = EngineLocalization.Get(operationName);
            SetBusy(true);
            DatabaseCleanupBackupResult? backup = null;
            DatabaseCleanupExecutionResult? result = null;

            try
            {
                if (preflight != null)
                {
                    bool shouldExecute;
                    try
                    {
                        shouldExecute = await Task.Run(() => ExecuteForCurrentTarget(plan.Target, preflight)).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not CleanupTargetChangedException)
                    {
                        SetStatus(EngineLocalization.Get("迁移状态检查失败。"));
                        ShowMessage(EngineLocalization.Format($"{DisplayName} 迁移状态检查失败：{ex.Message}"), MessageBoxImage.Error);
                        return;
                    }

                    if (!shouldExecute)
                    {
                        SetHasPendingMigration(false);
                        SetStatus(EngineLocalization.Get("当前数据库已完成迁移，无需再次创建备份或执行迁移。"));
                        return;
                    }
                }

                if ((forceBackup || (allowOptionalBackup && plan.BackupBeforeCleanup))
                    && plan.Provider is IDatabaseCleanupBackupProvider backupProvider)
                {
                    if (plan.Provider is IDatabaseCleanupMaintenanceProvider maintenanceProvider)
                    {
                        SetStatus(EngineLocalization.Format($"正在同一维护操作中创建完整备份并执行{localizedOperationName}..."));
                        try
                        {
                            var maintenanceResult = await Task.Run(() => ExecuteForCurrentTarget(plan.Target, () =>
                                ExecuteBackupAndCleanup(backupProvider, maintenanceProvider,
                                    () => ExecuteForCurrentTarget(plan.Target, action)))).ConfigureAwait(false);
                            backup = maintenanceResult.Backup;
                            result = maintenanceResult.Cleanup;
                        }
                        catch (Exception ex) when (ex is not CleanupTargetChangedException)
                        {
                            SetStatus(EngineLocalization.Format($"完整备份与{localizedOperationName}组合操作失败。"));
                            ShowMessage(
                                EngineLocalization.Format($"{DisplayName} 完整备份与{localizedOperationName}组合操作失败：{ex.Message}") + Environment.NewLine +
                                EngineLocalization.Get("如备份已经生成，备份文件会保留；请刷新统计后确认当前数据状态。"),
                                MessageBoxImage.Error);
                            return;
                        }
                    }
                    else
                    {
                        SetStatus(EngineLocalization.Format($"正在创建{localizedOperationName}前完整备份..."));
                        try
                        {
                            backup = await Task.Run(() => ExecuteForCurrentTarget(plan.Target, backupProvider.CreateBackup)).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is not CleanupTargetChangedException)
                        {
                            SetStatus(EngineLocalization.Format($"备份失败，未执行{localizedOperationName}。"));
                            ShowMessage(EngineLocalization.Format($"{localizedOperationName}前完整备份失败，数据尚未更改：{ex.Message}"), MessageBoxImage.Error);
                            return;
                        }
                    }
                }

                if (result == null)
                {
                    SetStatus(busyStatus);
                    try
                    {
                        result = await Task.Run(() => ExecuteForCurrentTarget(plan.Target, action)).ConfigureAwait(false);
                    }
                    catch (Exception ex) when (ex is not CleanupTargetChangedException)
                    {
                        SetStatus(EngineLocalization.Format($"{localizedOperationName}失败。"));
                        ShowMessage(EngineLocalization.Format($"{DisplayName} {localizedOperationName}失败：{ex.Message}"), MessageBoxImage.Error);
                        return;
                    }
                }

                Exception? refreshError = null;
                if (refreshTables)
                {
                    try
                    {
                        var (snapshot, hasPendingMigration) = await Task.Run(() =>
                            ExecuteForCurrentTarget(plan.Target, () => LoadSnapshot(plan.Provider))).ConfigureAwait(false);
                        RunOnUi(() =>
                        {
                            if (plan.Target?.IsCurrent == false) throw new CleanupTargetChangedException();
                            Description = plan.Target?.Description ?? plan.Provider.Description;
                            ApplySnapshot(snapshot, GetSelectedTableNames().ToHashSet(StringComparer.OrdinalIgnoreCase));
                            HasPendingMigration = hasPendingMigration;
                        });
                    }
                    catch (CleanupTargetChangedException ex)
                    {
                        ClearTargetStatistics(TargetChangedMessage);
                        refreshError = ex;
                    }
                    catch (Exception ex)
                    {
                        refreshError = ex;
                    }
                }

                string status = backup == null
                    ? result.StatusMessage
                    : $"{result.StatusMessage} {backup.StatusMessage}";
                if (refreshError != null)
                {
                    status += EngineLocalization.Get(" 表统计刷新失败，请稍后手动刷新。");
                }
                RunOnUi(() => Status = plan.Target?.IsCurrent == false
                    ? $"{status} {TargetChangedMessage}"
                    : status);

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
                    messageLines.Add(EngineLocalization.Format($"数据已完成{localizedOperationName}，但统计刷新失败：{refreshError.Message}"));
                }

                ShowMessage(DescribeTarget(plan, string.Join(Environment.NewLine, messageLines)),
                    refreshError == null ? MessageBoxImage.Information : MessageBoxImage.Warning);
            }
            catch (CleanupTargetChangedException)
            {
                ClearTargetStatistics(TargetChangedMessage);
                if (backup != null)
                    ShowMessage(DescribeTarget(plan, $"{TargetChangedMessage}{Environment.NewLine}{backup.StatusMessage}{Environment.NewLine}{backup.FilePath}"), MessageBoxImage.Warning);
                else if (forceBackup || (allowOptionalBackup && plan.BackupBeforeCleanup))
                    ShowMessage(DescribeTarget(plan, TargetChangedMessage + Environment.NewLine +
                        EngineLocalization.Get("如备份已经生成，备份文件会保留；请刷新统计后确认当前数据状态。")), MessageBoxImage.Warning);
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

        private static (IReadOnlyList<DatabaseCleanupTableInfo> Snapshot, bool HasPendingMigration) LoadSnapshot(IDatabaseCleanupSourceProvider provider)
        {
            IReadOnlyList<DatabaseCleanupTableInfo> snapshot = provider.LoadTables();
            bool hasPendingMigration = provider is IDatabaseCleanupMigrationProvider migrationProvider && migrationProvider.HasPendingMigration();
            return (snapshot, hasPendingMigration);
        }

        private static string TargetChangedMessage => EngineLocalization.Get("数据库连接已更改，请刷新统计后再执行。");

        private CleanupPlan? CreateOperationPlan()
        {
            if (IsBusy) return null;
            if (!HasDatabaseTarget) return new CleanupPlan(_provider, null, BackupBeforeCleanup);
            if (_statisticsTarget is { IsCurrent: true } target)
                return new CleanupPlan(target.Provider, target, BackupBeforeCleanup);

            InvalidateRetiredTarget();
            return null;
        }

        internal void InvalidateRetiredTarget()
        {
            RunOnUi(() =>
            {
                if (_statisticsTarget?.IsCurrent == false)
                    ClearTargetStatistics(TargetChangedMessage);
            });
        }

        private void ClearTargetStatistics(string status)
        {
            RunOnUi(() =>
            {
                _statisticsTarget = null;
                Description = string.Empty;
                ApplySnapshot([], new HashSet<string>());
                HasPendingMigration = false;
                Status = status;
            });
        }

        private bool ValidateOperationPlan(CleanupPlan plan)
        {
            if (plan.Target == null || (ReferenceEquals(plan.Target, _statisticsTarget) && plan.Target.IsCurrent))
                return true;

            InvalidateRetiredTarget();
            return false;
        }

        private static T ExecuteForCurrentTarget<T>(DatabaseCleanupTarget? target, Func<T> action)
        {
            if (target?.IsCurrent == false) throw new CleanupTargetChangedException();
            return action();
        }

        private bool TryGetKeepMonths(out int keepMonths)
        {
            return int.TryParse(KeepMonthsText, out keepMonths) && keepMonths > 0;
        }

        private static string BuildBackupNotice(CleanupPlan plan, bool forceBackup = false)
        {
            if (plan.Provider is not IDatabaseCleanupBackupProvider)
                return EngineLocalization.Get("当前数据源不支持自动备份，请确认已有可恢复副本。");

            if (forceBackup)
                return EngineLocalization.Get("本次迁移会强制先创建完整备份；备份失败时不会执行迁移。");

            return plan.BackupBeforeCleanup
                ? EngineLocalization.Get("操作前会先创建完整备份；备份失败时不会继续。")
                : EngineLocalization.Get("本次操作不会自动创建备份。");
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

        private bool ConfirmCleanup(CleanupPlan plan, string message, MessageBoxImage image)
        {
            if (!ValidateOperationPlan(plan)) return false;
            message = DescribeTarget(plan, message);

            return RunOnUi(() =>
            {
                IsBusy = true;
                try
                {
                    bool confirmed = _confirmOverride?.Invoke(message, image) ?? (MessageBox1.Show(
                        Application.Current.GetActiveWindow(),
                        message,
                        DisplayName,
                        MessageBoxButton.OKCancel,
                        image) == MessageBoxResult.OK);
                    bool stillCurrent = ValidateOperationPlan(plan);
                    return confirmed && stillCurrent;
                }
                finally
                {
                    IsBusy = false;
                }
            });
        }

        private static string DescribeTarget(CleanupPlan plan, string message) => plan.Target == null
            ? message : plan.Target.Description + Environment.NewLine + Environment.NewLine + message;

        private void SetStatus(string status) => RunOnUi(() => Status = status);
        private void SetBusy(bool isBusy) => RunOnUi(() => IsBusy = isBusy);
        private void SetHasPendingMigration(bool hasPendingMigration) => RunOnUi(() => HasPendingMigration = hasPendingMigration);

        private void ShowMessage(string message, MessageBoxImage image)
        {
            RunOnUi(() =>
            {
                if (_messageOverride != null)
                    _messageOverride(message, image);
                else
                    MessageBox1.Show(Application.Current.GetActiveWindow(), message, DisplayName, MessageBoxButton.OK, image);
            });
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

        public ReadOnlyObservableCollection<DatabaseCleanupSourceViewModel> Sources { get; }
        public bool IsSourceScoped { get; }
        public bool ShowSourceNavigation => !IsSourceScoped;
        public string WindowTitle => IsSourceScoped
            ? $"{Sources[0].DisplayName} · {EngineLocalization.Get("数据库清理")}"
            : EngineLocalization.Get("数据库清理");
        public string ScopeDescription => IsSourceScoped
            ? EngineLocalization.Get("仅管理当前数据库，不显示或操作其他数据源。")
            : EngineLocalization.Get("选择数据源后再操作，每次仅处理当前数据库。");

        public DatabaseCleanupSourceViewModel? SelectedSource
        {
            get => _selectedSource;
            set
            {
                if (value != null && !Sources.Contains(value))
                    throw new ArgumentException("The selected cleanup source must belong to this window.", nameof(value));
                if (ReferenceEquals(_selectedSource, value))
                    return;

                _selectedSource = value;
                OnPropertyChanged();
            }
        }

        public DatabaseCleanupWindowViewModel() : this(DiscoverProviders())
        {
        }

        internal DatabaseCleanupWindowViewModel(IEnumerable<IDatabaseCleanupSourceProvider> providers, bool isSourceScoped = false)
        {
            ArgumentNullException.ThrowIfNull(providers);
            var providerList = providers
                .OrderBy(provider => provider.Order)
                .ThenBy(provider => provider.DisplayName)
                .ToList();
            if (isSourceScoped && providerList.Count != 1)
                throw new ArgumentException("A source-scoped cleanup window requires exactly one provider.", nameof(providers));

            IsSourceScoped = isSourceScoped;
            Sources = new ReadOnlyObservableCollection<DatabaseCleanupSourceViewModel>(
                new ObservableCollection<DatabaseCleanupSourceViewModel>(providerList.Select(provider => new DatabaseCleanupSourceViewModel(provider))));
            SelectedSource = Sources.FirstOrDefault();
        }

        private static IEnumerable<IDatabaseCleanupSourceProvider> DiscoverProviders()
        {
            AssemblyHandler assemblies = AssemblyHandler.GetInstance();
            assemblies.RefreshAssemblies();
            return assemblies.LoadImplementations<IDatabaseCleanupSourceProvider>();
        }

        public Task RefreshAllAsync()
        {
            return Task.WhenAll(Sources.Select(source => source.RefreshAsync()));
        }
    }
}
