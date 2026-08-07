#pragma warning disable CA1822,CA1863,CS8602,CS8603
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using ColorVision.UI.ServiceHost;
using log4net;
using Microsoft.Win32;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace ColorVision.Database
{

    public class MySqlLocalConfig : IConfig
    {
        public static MySqlLocalConfig Instance => ConfigService.Instance.GetRequiredService<MySqlLocalConfig>();
        public string ServiceName { get; set; } = "MySQL";

        public string ImagePath { get; set; }
        public string MysqldPath { get; set; }

        public string MysqlPath { get; set; }

        public string MysqldumpPath { get; set; }
    }

    public class MysqlBack : ViewModelBase
    {
        public ContextMenu ContextMenu { get; set; }

        public RelayCommand RestoreCommand { get; set; }
        public RelayCommand SelectCommand { get; set; }

        public MysqlBack(string filePath)
        {
            FilePath = filePath;
            Name = Path.GetFileName(filePath);
            CreationTime = File.GetCreationTime(filePath);
            RestoreCommand = new RelayCommand(a => Restore());
            SelectCommand = new RelayCommand(a => Select());


            ContextMenu = new ContextMenu();
            ContextMenu.Items.Add(new MenuItem() { Header = "复制", Command = ApplicationCommands.Copy });
            ContextMenu.Items.Add(new MenuItem() { Header = "删除", Command = ApplicationCommands.Delete });
            ContextMenu.Items.Add(new MenuItem() { Header = "还原", Command = RestoreCommand });
            ContextMenu.Items.Add(new MenuItem() { Header = "选中", Command = SelectCommand });

        }

        public void Select()
        {
            PlatformHelper.OpenFolderAndSelectFile(FilePath);
        }

        public void Restore()
        {
            _ = MySqlLocalServicesManager.GetInstance().RestoreAndRestartAsync(FilePath);
        }

        public string FilePath { get => _FilePath; set { _FilePath = value; OnPropertyChanged(); } }
        private string _FilePath;
        public string Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string _Name;

        public DateTime CreationTime { get => _CreationTime; set { _CreationTime = value; OnPropertyChanged(); } }
        private DateTime _CreationTime;

        public string CreationTimeDisplay => CreationTime.ToString("yyyy-MM-dd HH:mm:ss");

    }

    public class MySqlCleanupTableInfo : ViewModelBase
    {
        public string TableName { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;

        public bool Exists { get => _Exists; set { _Exists = value; OnPropertyChanged(); OnPropertyChanged(nameof(ExistsDisplay)); OnPropertyChanged(nameof(RowCountDisplay)); OnPropertyChanged(nameof(SizeDisplay)); OnPropertyChanged(nameof(TimeRangeDisplay)); } }
        private bool _Exists;

        public long RowCount { get => _RowCount; set { _RowCount = value; OnPropertyChanged(); OnPropertyChanged(nameof(RowCountDisplay)); } }
        private long _RowCount;

        public long DataLength { get => _DataLength; set { _DataLength = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
        private long _DataLength;

        public long IndexLength { get => _IndexLength; set { _IndexLength = value; OnPropertyChanged(); OnPropertyChanged(nameof(SizeDisplay)); } }
        private long _IndexLength;

        public string? TimeColumn { get => _TimeColumn; set { _TimeColumn = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeColumnDisplay)); } }
        private string? _TimeColumn;

        public DateTime? OldestTime { get => _OldestTime; set { _OldestTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeRangeDisplay)); } }
        private DateTime? _OldestTime;

        public DateTime? NewestTime { get => _NewestTime; set { _NewestTime = value; OnPropertyChanged(); OnPropertyChanged(nameof(TimeRangeDisplay)); } }
        private DateTime? _NewestTime;

        public string CleanupMode { get => _CleanupMode; set { _CleanupMode = value; OnPropertyChanged(); } }
        private string _CleanupMode = string.Empty;

        public string ExistsDisplay => Exists ? "存在" : "未找到";
        public string RowCountDisplay => Exists ? RowCount.ToString("N0") : "-";
        public string SizeDisplay => Exists ? FormatSize(DataLength + IndexLength) : "-";
        public string TimeColumnDisplay => string.IsNullOrWhiteSpace(TimeColumn) ? "-" : TimeColumn;
        public string TimeRangeDisplay => !Exists
            ? "-"
            : OldestTime.HasValue || NewestTime.HasValue
                ? $"{FormatDate(OldestTime)} ~ {FormatDate(NewestTime)}"
                : "-";

        private static string FormatDate(DateTime? value) => value?.ToString("yyyy-MM-dd HH:mm:ss") ?? "-";

        private static string FormatSize(long bytes)
        {
            string[] units = { "B", "KB", "MB", "GB", "TB" };
            double size = bytes;
            int index = 0;

            while (size >= 1024 && index < units.Length - 1)
            {
                size /= 1024;
                index++;
            }

            return $"{size:0.##} {units[index]}";
        }
    }

    public class MySqlLocalServicesManager : ViewModelBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlLocalServicesManager));
        private const string RegistrationCenterServiceName = "RegistrationCenterService";
        private static readonly SemaphoreSlim DatabaseMaintenanceGate = new(1, 1);
        private static readonly AsyncLocal<int> DatabaseMaintenanceDepth = new();
        private static readonly TimeSpan MySqlCommandTimeout = TimeSpan.FromHours(2);
        private const string ResultMasterTableName = "t_scgd_algorithm_result_master";
        private const string MeasureBatchTableName = "t_scgd_measure_batch";
        private const string AlgorithmDetailPrefix = "t_scgd_algorithm_result_detail_";
        private const string MeasureResultPrefix = "t_scgd_measure_result_";
        private static readonly string[] CandidateTimeColumns = { "create_time", "create_date", "add_time" };
        private static MySqlLocalServicesManager _instance;
        private static readonly object _locker = new();
        public static MySqlLocalServicesManager GetInstance() { lock (_locker) { return _instance ??= new MySqlLocalServicesManager(); } }

        internal static T RunDatabaseMaintenance<T>(Func<T> maintenanceAction)
        {
            ArgumentNullException.ThrowIfNull(maintenanceAction);
            if (DatabaseMaintenanceDepth.Value > 0)
                return RunNestedDatabaseMaintenance(maintenanceAction);

            DatabaseMaintenanceGate.Wait();
            DatabaseMaintenanceDepth.Value = 1;
            try
            {
                return maintenanceAction();
            }
            finally
            {
                DatabaseMaintenanceDepth.Value = 0;
                DatabaseMaintenanceGate.Release();
            }
        }

        internal static async Task<T> RunDatabaseMaintenanceAsync<T>(Func<T> maintenanceAction)
        {
            ArgumentNullException.ThrowIfNull(maintenanceAction);
            if (DatabaseMaintenanceDepth.Value > 0)
                return RunNestedDatabaseMaintenance(maintenanceAction);

            await DatabaseMaintenanceGate.WaitAsync().ConfigureAwait(false);
            DatabaseMaintenanceDepth.Value = 1;
            try
            {
                return maintenanceAction();
            }
            finally
            {
                DatabaseMaintenanceDepth.Value = 0;
                DatabaseMaintenanceGate.Release();
            }
        }

        private static T RunNestedDatabaseMaintenance<T>(Func<T> maintenanceAction)
        {
            DatabaseMaintenanceDepth.Value++;
            try
            {
                return maintenanceAction();
            }
            finally
            {
                DatabaseMaintenanceDepth.Value--;
            }
        }

        public string BackupPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
        public ObservableCollection<MysqlBack> Backups { get; set; } = new ObservableCollection<MysqlBack>();
        public ObservableCollection<MySqlCleanupTableInfo> CleanupTables { get; } = new ObservableCollection<MySqlCleanupTableInfo>();
        public static IReadOnlyList<string> ServiceSettingTableNames { get; } =
        [
            "t_scgd_algorithm_poi_template_detail",
            "t_scgd_algorithm_poi_template_master",
            "t_scgd_buz_product_detail",
            "t_scgd_buz_product_master",
            "t_scgd_mod_param_detail",
            "t_scgd_mod_param_master"
        ];

        public static IReadOnlyList<string> ServiceConfigurationTableNames { get; } =
        [
            "t_scgd_camera_license",
            "t_scgd_sys_resource",
            "t_scgd_sys_resource_group"
        ];

        public static IReadOnlyList<string> MigrationBackupTableNames { get; } = ServiceSettingTableNames
            .Concat(ServiceConfigurationTableNames)
            .ToArray();

        private const string DictionaryMasterTableName = "t_scgd_sys_dictionary_mod_master";
        private const string DictionaryItemTableName = "t_scgd_sys_dictionary_mod_item";
        private const string ModParamMasterTableName = "t_scgd_mod_param_master";
        private const string ModParamDetailTableName = "t_scgd_mod_param_detail";


        public static MySqlLocalConfig Config => MySqlLocalConfig.Instance;

        public RelayCommand RestoreSelectCommand { get; set; }
        public RelayCommand BackupResourcesCommand { get; set; }
        public RelayCommand BackupAllResourcesCommand { get; set; }
        public RelayCommand RefreshCleanupTablesCommand { get; set; }
        public RelayCommand CleanupHistoryCommand { get; set; }
        public RelayCommand CleanupAllResultTablesCommand { get; set; }

        public string CleanupKeepMonthsText { get => _CleanupKeepMonthsText; set { _CleanupKeepMonthsText = value; OnPropertyChanged(); } }
        private string _CleanupKeepMonthsText = "3";

        public string CleanupStatus { get => _CleanupStatus; set { _CleanupStatus = value; OnPropertyChanged(); } }
        private string _CleanupStatus = "打开窗口后会自动统计可清理结果表。";

        public bool IsCleanupBusy { get => _IsCleanupBusy; set { _IsCleanupBusy = value; OnPropertyChanged(); CommandManager.InvalidateRequerySuggested(); } }
        private bool _IsCleanupBusy;

        public MySqlLocalServicesManager()   
        {
            try
            {
                bool result = FindMySQLPath("MySQL") || FindMySQLPath("MySQL57") || FindMySQLPath("MySQL80");
                if (!result)
                {
                    log.Info("找不到本地的mysql 服务");
                    if (File.Exists(MySqlLocalConfig.Instance.MysqldPath))
                    {
                        log.Info("系统更新，找不到本地的Mysql服务,请将数据库重新安装");
                    }
                    else
                    {
                        log.Info("找不到本地的Mysql服务");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }

            if (!Directory.Exists(BackupPath))
                Directory.CreateDirectory(BackupPath);

            var sqlFiles = Directory.EnumerateFiles(BackupPath, "*.sql", SearchOption.TopDirectoryOnly)
                .Where(filePath => string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(f => File.GetCreationTime(f));

            Backups.Clear(); // 如果需要清空原有数据
            foreach (var item in sqlFiles)
            {
                Backups.Add(new MysqlBack(item));
            }
            RestoreSelectCommand = new RelayCommand(a => RestoreSelect());
            BackupResourcesCommand = new RelayCommand(a => BackupResources());
            BackupAllResourcesCommand = new RelayCommand(a => _ = BackupAllWithFeedbackAsync());
            RefreshCleanupTablesCommand = new RelayCommand(a => _ = RefreshCleanupTablesAsync(), a => !IsCleanupBusy);
            CleanupHistoryCommand = new RelayCommand(a => CleanupHistoricalResults(), a => !IsCleanupBusy);
            CleanupAllResultTablesCommand = new RelayCommand(a => CleanupAllResultTables(), a => !IsCleanupBusy);

        }

        private bool IsRun { get; set; }

        public Task RefreshCleanupTablesAsync()
        {
            if (IsCleanupBusy)
                return Task.CompletedTask;

            return Task.Run(() =>
            {
                SetCleanupBusy(true);
                SetCleanupStatus(ColorVision.Engine.Properties.Resources.Engine_Msg_LoadingResultTableStats);

                try
                {
                    var snapshot = LoadCleanupTableInfos();
                    ApplyCleanupTableSnapshot(snapshot);
                    SetCleanupStatus(snapshot.Any(item => item.Exists)
                        ? string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_ResultTableStatsLoaded, snapshot.Count)
                        : ColorVision.Engine.Properties.Resources.Engine_Msg_NoCleanableResultTables);
                }
                catch (Exception ex)
                {
                    log.Error(ColorVision.Engine.Properties.Resources.Engine_Msg_LoadResultTableStatsFailed, ex);
                    SetCleanupStatus(ColorVision.Engine.Properties.Resources.Engine_Msg_LoadResultTableStatsFailed);
                    RunOnUi(() => MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_LoadResultTableStatsFailedDetail, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error));
                }
                finally
                {
                    SetCleanupBusy(false);
                }
            });
        }

        private void CleanupHistoricalResults()
        {
            if (!TryGetKeepMonths(out int keepMonths))
                return;

            DateTime cutoffDate = DateTime.Now.AddMonths(-keepMonths);
            string message = $"将删除以下数据表在 {cutoffDate:yyyy-MM-dd HH:mm:ss} 之前的数据：{Environment.NewLine}{BuildCleanupTableBulletList()}{Environment.NewLine}{Environment.NewLine}不会触碰资源、模板、配置类表。该操作不可恢复，是否继续？";
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), message, "ColorVision", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                return;

            _ = Task.Run(() => ExecuteHistoricalCleanup(keepMonths, cutoffDate));
        }

        private void CleanupAllResultTables()
        {
            string message = $"将整组清空以下数据表的全部数据：{Environment.NewLine}{BuildCleanupTableBulletList()}{Environment.NewLine}{Environment.NewLine}不会触碰资源、模板、配置类表。该操作不可恢复，是否继续？";
            if (MessageBox1.Show(Application.Current.GetActiveWindow(), message, "ColorVision", MessageBoxButton.OKCancel, MessageBoxImage.Warning) == MessageBoxResult.Cancel)
                return;

            _ = Task.Run(ExecuteFullCleanup);
        }

        private void ExecuteHistoricalCleanup(int keepMonths, DateTime cutoffDate)
        {
            SetCleanupBusy(true);
            SetCleanupStatus($"正在清理 {cutoffDate:yyyy-MM-dd HH:mm:ss} 之前的结果数据...");

            try
            {
                using var db = CreateDbClient(30);
                string databaseName = GetDatabaseName();
                HashSet<string> existingTables = GetExistingCleanupTables(db, databaseName);
                Dictionary<string, HashSet<string>> columnsByTable = GetColumnsByTable(db, databaseName, existingTables);
                List<string> summary = new List<string>();

                string? masterTimeColumn = ResolveTimeColumn(columnsByTable, ResultMasterTableName);
                foreach (string tableName in OrderCleanupTablesForExecution(existingTables.Where(IsAlgorithmDetailTable)))
                {
                    if (TableHasColumn(columnsByTable, tableName, "pid") && existingTables.Contains(ResultMasterTableName) && !string.IsNullOrWhiteSpace(masterTimeColumn))
                    {
                        int deleted = DeleteLinkedRowsBeforeCutoff(db, tableName, "pid", ResultMasterTableName, "id", masterTimeColumn, cutoffDate);
                        summary.Add($"{tableName}: 删除 {deleted:N0} 行");
                    }
                    else
                    {
                        string? detailTimeColumn = ResolveTimeColumn(columnsByTable, tableName);
                        if (!string.IsNullOrWhiteSpace(detailTimeColumn))
                        {
                            int deleted = DeleteRowsBeforeCutoff(db, tableName, detailTimeColumn, cutoffDate);
                            summary.Add($"{tableName}: 删除 {deleted:N0} 行");
                        }
                        else
                        {
                            summary.Add($"{tableName}: 未找到 pid 或可用时间列，跳过历史清理");
                        }
                    }
                }

                if (existingTables.Contains(ResultMasterTableName) && !string.IsNullOrWhiteSpace(masterTimeColumn))
                {
                    int deletedMaster = DeleteRowsBeforeCutoff(db, ResultMasterTableName, masterTimeColumn, cutoffDate);
                    summary.Add($"{ResultMasterTableName}: 删除 {deletedMaster:N0} 行");
                }
                else if (existingTables.Contains(ResultMasterTableName))
                {
                    summary.Add($"{ResultMasterTableName}: 未找到可用时间列，跳过历史清理");
                }

                string? measureBatchTimeColumn = ResolveTimeColumn(columnsByTable, MeasureBatchTableName);
                foreach (string tableName in OrderCleanupTablesForExecution(existingTables.Where(IsMeasureResultTable)))
                {
                    if (TableHasColumn(columnsByTable, tableName, "batch_id") && existingTables.Contains(MeasureBatchTableName) && !string.IsNullOrWhiteSpace(measureBatchTimeColumn))
                    {
                        int deleted = DeleteLinkedRowsBeforeCutoff(db, tableName, "batch_id", MeasureBatchTableName, "id", measureBatchTimeColumn, cutoffDate);
                        summary.Add($"{tableName}: 删除 {deleted:N0} 行");
                    }
                    else
                    {
                        string? timeColumn = ResolveTimeColumn(columnsByTable, tableName);
                        if (!string.IsNullOrWhiteSpace(timeColumn))
                        {
                            int deleted = DeleteRowsBeforeCutoff(db, tableName, timeColumn, cutoffDate);
                            summary.Add($"{tableName}: 删除 {deleted:N0} 行");
                        }
                        else
                        {
                            summary.Add($"{tableName}: 未找到 batch_id 或可用时间列，跳过历史清理");
                        }
                    }
                }

                if (existingTables.Contains(MeasureBatchTableName) && !string.IsNullOrWhiteSpace(measureBatchTimeColumn))
                {
                    int deletedMeasureBatch = DeleteRowsBeforeCutoff(db, MeasureBatchTableName, measureBatchTimeColumn, cutoffDate);
                    summary.Add($"{MeasureBatchTableName}: 删除 {deletedMeasureBatch:N0} 行");
                }
                else if (existingTables.Contains(MeasureBatchTableName))
                {
                    summary.Add($"{MeasureBatchTableName}: 未找到可用时间列，跳过历史清理");
                }

                ApplyCleanupTableSnapshot(LoadCleanupTableInfos());
                SetCleanupStatus(string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_DataTableCleanupComplete, keepMonths));
                RunOnUi(() => MessageBox1.Show(Application.Current.GetActiveWindow(), string.Join(Environment.NewLine, summary), ColorVision.Engine.Properties.Resources.Engine_Msg_DataTableCleanupComplete, MessageBoxButton.OK, MessageBoxImage.Information));
            }
            catch (Exception ex)
            {
                log.Error(ColorVision.Engine.Properties.Resources.Engine_Msg_CleanupHistoryFailed, ex);
                SetCleanupStatus(ColorVision.Engine.Properties.Resources.Engine_Msg_CleanupHistoryFailed);
                RunOnUi(() => MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_CleanupHistoryFailedDetail, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                SetCleanupBusy(false);
            }
        }

        private void ExecuteFullCleanup()
        {
            SetCleanupBusy(true);
            SetCleanupStatus(ColorVision.Engine.Properties.Resources.Engine_Msg_ClearingResultTables);

            try
            {
                using var db = CreateDbClient(30);
                string databaseName = GetDatabaseName();
                HashSet<string> existingTables = GetExistingCleanupTables(db, databaseName);

                db.Ado.ExecuteCommand("SET FOREIGN_KEY_CHECKS = 0;");
                try
                {
                    foreach (string tableName in OrderCleanupTablesForExecution(existingTables))
                    {
                        db.Ado.ExecuteCommand($"TRUNCATE TABLE {QuoteIdentifier(tableName)}");
                    }
                }
                finally
                {
                    db.Ado.ExecuteCommand("SET FOREIGN_KEY_CHECKS = 1;");
                }

                ApplyCleanupTableSnapshot(LoadCleanupTableInfos());
                SetCleanupStatus(ColorVision.Engine.Properties.Resources.Engine_Msg_AllResultTablesCleared);
                RunOnUi(() => MessageBox1.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_AllResultTablesCleared, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information));
            }
            catch (Exception ex)
            {
                log.Error(ColorVision.Engine.Properties.Resources.Engine_Msg_ClearResultTablesFailed, ex);
                SetCleanupStatus(ColorVision.Engine.Properties.Resources.Engine_Msg_ClearResultTablesFailed);
                RunOnUi(() => MessageBox1.Show(Application.Current.GetActiveWindow(), string.Format(ColorVision.Engine.Properties.Resources.Engine_Msg_ClearResultTablesFailedDetail, ex.Message), "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                SetCleanupBusy(false);
            }
        }

        private void BackupResources()
        {
            _ = BackupWithFeedbackAsync(BackupMysqlResource);
        }

        private Task BackupAllWithFeedbackAsync()
        {
            return BackupWithFeedbackAsync(BackupAllMysql);
        }

        private async Task BackupWithFeedbackAsync(Func<string> backupAction)
        {
            if (IsRun)
            {
                MessageBox.Show(ColorVision.Engine.Properties.Resources.Engine_Msg_BackupInProgress);
                return;
            }

            IsRun = true;
            try
            {
                await Task.Run(backupAction).ConfigureAwait(false);
                RunOnUi(() => MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_BackupSuccess));
            }
            catch (Exception ex)
            {
                log.Error("MySQL备份失败。", ex);
                RunOnUi(() => MessageBox.Show(Application.Current?.MainWindow, $"备份失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error));
            }
            finally
            {
                IsRun = false;
            }
        }


        public void RestoreSelect()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = BackupPath, // Set the initial directory
                Filter = "SQL Files (*.sql)|*.sql", // Filter for file types
                Title = "Select a Backup File"
            };

            // Show the dialog and get the result
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName; // Get the selected file path
                if (!string.Equals(Path.GetExtension(filePath), ".sql", StringComparison.OrdinalIgnoreCase))
                {
                    MessageBox.Show(Application.Current.MainWindow, "仅支持加载 .sql 备份文件。");
                    return;
                }

                _ = RestoreAndRestartAsync(filePath);
            }
        }

        public async Task RestoreAndRestartAsync(string backupFile)
        {
            bool ownsMaintenanceGate = false;
            if (DatabaseMaintenanceDepth.Value > 0)
            {
                DatabaseMaintenanceDepth.Value++;
            }
            else if (DatabaseMaintenanceGate.Wait(0))
            {
                DatabaseMaintenanceDepth.Value = 1;
                ownsMaintenanceGate = true;
            }
            else
            {
                RunOnUi(() => MessageBox.Show(Application.Current?.MainWindow, "已有数据库维护任务正在执行，请稍候。", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Information));
                return;
            }

            try
            {
                try
                {
                    await Task.Run(() => RestoreMysql(backupFile)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.Error($"加载MySQL备份失败：{backupFile}", ex);
                    RunOnUi(() => MessageBox.Show(Application.Current?.MainWindow, $"加载备份失败：{ex.Message}", "ColorVision", MessageBoxButton.OK, MessageBoxImage.Error));
                    return;
                }

                RunOnUi(() => MessageBox.Show(Application.Current?.MainWindow, ColorVision.Engine.Properties.Resources.Engine_Msg_RestoreSuccessRestarting));

                ServiceHostResponse response;
                try
                {
                    response = await ColorVisionServiceHostClient.Default.RestartServiceAsync(
                        RegistrationCenterServiceName,
                        timeoutSeconds: 60,
                        timeout: TimeSpan.FromSeconds(90)).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    log.Error($"通过ColorVisionServiceHost重启{RegistrationCenterServiceName}失败。", ex);
                    RunOnUi(() => MessageBox.Show(
                        Application.Current?.MainWindow,
                        $"{ColorVision.Engine.Properties.Resources.Engine_Msg_ServiceRestartFailed}{Environment.NewLine}{ex.Message}",
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
                    return;
                }

                if (!response.Success)
                {
                    RunOnUi(() => MessageBox.Show(
                        Application.Current?.MainWindow,
                        $"{ColorVision.Engine.Properties.Resources.Engine_Msg_ServiceRestartFailed}{Environment.NewLine}{response.Message}",
                        "ColorVision",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error));
                    return;
                }

                RunOnUi(() =>
                {
                    MessageBox.Show(Application.Current?.MainWindow, ColorVision.Engine.Properties.Resources.Engine_Msg_ServiceRestartSuccess);
                    try
                    {
                        string applicationPath = Path.ChangeExtension(Application.ResourceAssembly.Location, ".exe");
                        Process? restartedApplication = Process.Start(applicationPath, "-r");
                        if (restartedApplication == null)
                            throw new InvalidOperationException("未能创建新的应用进程。");
                    }
                    catch (Exception ex)
                    {
                        log.Error("数据库及服务已恢复，但应用自动重启失败。", ex);
                        MessageBox.Show(
                            Application.Current?.MainWindow,
                            $"数据库及服务已恢复，但应用自动重启失败。{Environment.NewLine}{ex.Message}",
                            "ColorVision",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);
                        return;
                    }

                    Application.Current.Shutdown();
                });
            }
            finally
            {
                DatabaseMaintenanceDepth.Value--;
                if (ownsMaintenanceGate)
                    DatabaseMaintenanceGate.Release();
            }
        }




        bool FindMySQLPath(string serviceName)
        {
            using (RegistryKey key = Registry.LocalMachine.OpenSubKey($@"SYSTEM\CurrentControlSet\Services\{serviceName}"))
            {
                if (key != null)
                {
                    Config.ServiceName = serviceName;
                    object imagePath = key.GetValue("ImagePath");
                    if (imagePath is string str)
                    {
                        Config.ImagePath = str;
                        Config.MysqldPath = ExtractExePath(Config.ImagePath);
                        if (File.Exists(Config.MysqldPath))
                        {
                            DirectoryInfo directory = Directory.GetParent(Config.MysqldPath);

                            string mysqlPath = Path.Combine(directory.FullName, "mysql.exe");
                            if (File.Exists(mysqlPath))
                            {
                                Config.MysqlPath = mysqlPath;
                            }
                            string mysqldumpPath = Path.Combine(directory.FullName, "mysqldump.exe");
                            if (File.Exists(mysqldumpPath))
                            {
                                Config.MysqldumpPath = mysqldumpPath;
                            }
                            return true;
                        }
                    }
                }
            }
            return false;
        }

        string ExtractExePath(string imagePath)
        {
            // 切分字符串并提取路径
            var parts = imagePath.Split(' ');
            foreach (var part in parts)
            {
                if (part.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    return part;
                }
            }
            return null;
        }

        private List<MySqlCleanupTableInfo> LoadCleanupTableInfos()
        {
            string databaseName = GetDatabaseName();
            using var db = CreateDbClient(15);
            List<TableStatusRow> tableStatusRows = db.Ado.SqlQuery<TableStatusRow>(
                $@"SELECT TABLE_NAME AS TableName,
                           IFNULL(TABLE_ROWS, 0) AS TableRows,
                           IFNULL(DATA_LENGTH, 0) AS DataLength,
                           IFNULL(INDEX_LENGTH, 0) AS IndexLength
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = @dbName
                      AND (
                          TABLE_NAME = @resultMasterTableName
                          OR TABLE_NAME = @measureBatchTableName
                          OR TABLE_NAME LIKE @algorithmDetailPattern
                          OR TABLE_NAME LIKE @measureResultPattern)",
                new
                {
                    dbName = databaseName,
                    resultMasterTableName = ResultMasterTableName,
                    measureBatchTableName = MeasureBatchTableName,
                    algorithmDetailPattern = $"{AlgorithmDetailPrefix}%",
                    measureResultPattern = $"{MeasureResultPrefix}%",
                });

            if (tableStatusRows.Count == 0)
            {
                return new List<MySqlCleanupTableInfo>();
            }

            List<string> tableNames = OrderCleanupTablesForDisplay(tableStatusRows.Select(item => item.TableName));
            Dictionary<string, TableStatusRow> tableStatus = tableStatusRows.ToDictionary(item => item.TableName, StringComparer.OrdinalIgnoreCase);
            Dictionary<string, HashSet<string>> columnsByTable = GetColumnsByTable(db, databaseName, tableNames);

            string? masterTimeColumn = ResolveTimeColumn(columnsByTable, ResultMasterTableName);
            string? measureBatchTimeColumn = ResolveTimeColumn(columnsByTable, MeasureBatchTableName);
            List<MySqlCleanupTableInfo> result = new List<MySqlCleanupTableInfo>(tableNames.Count);

            foreach (string tableName in tableNames)
            {
                TableStatusRow statusRow = tableStatus[tableName];
                var info = new MySqlCleanupTableInfo
                {
                    TableName = tableName,
                    DisplayName = GetCleanupTableDisplayName(tableName),
                    Exists = true,
                    DataLength = statusRow.DataLength ?? 0,
                    IndexLength = statusRow.IndexLength ?? 0,
                    RowCount = GetExactRowCount(db, tableName),
                };

                string? timeColumn = ResolveTimeColumn(columnsByTable, tableName);
                if (string.Equals(tableName, ResultMasterTableName, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(tableName, MeasureBatchTableName, StringComparison.OrdinalIgnoreCase))
                {
                    info.TimeColumn = timeColumn;
                    if (!string.IsNullOrWhiteSpace(timeColumn))
                    {
                        (info.OldestTime, info.NewestTime) = GetTimeRange(db, tableName, timeColumn);
                        info.CleanupMode = $"按 {timeColumn} 清理";
                    }
                    else
                    {
                        info.CleanupMode = "仅支持整表清空";
                    }
                }
                else if (IsAlgorithmDetailTable(tableName) && TableHasColumn(columnsByTable, tableName, "pid") && !string.IsNullOrWhiteSpace(masterTimeColumn))
                {
                    info.TimeColumn = masterTimeColumn;
                    info.CleanupMode = $"按主表 {masterTimeColumn} 关联清理";
                }
                else if (IsMeasureResultTable(tableName) && TableHasColumn(columnsByTable, tableName, "batch_id") && !string.IsNullOrWhiteSpace(measureBatchTimeColumn))
                {
                    info.TimeColumn = measureBatchTimeColumn;
                    info.CleanupMode = $"按批次表 {measureBatchTimeColumn} 关联清理";
                }
                else if (!string.IsNullOrWhiteSpace(timeColumn))
                {
                    info.TimeColumn = timeColumn;
                    (info.OldestTime, info.NewestTime) = GetTimeRange(db, tableName, timeColumn);
                    info.CleanupMode = $"按 {timeColumn} 清理";
                }
                else
                {
                    info.CleanupMode = "仅支持整表清空";
                }

                result.Add(info);
            }

            return result;
        }

        private static (DateTime? OldestTime, DateTime? NewestTime) GetTimeRange(SqlSugarClient db, string tableName, string timeColumn)
        {
            string sql = $"SELECT MIN({QuoteIdentifier(timeColumn)}) AS OldestTime, MAX({QuoteIdentifier(timeColumn)}) AS NewestTime FROM {QuoteIdentifier(tableName)}";
            TimeRangeRow? row = db.Ado.SqlQuery<TimeRangeRow>(sql).FirstOrDefault();
            return (row?.OldestTime, row?.NewestTime);
        }

        private static long GetExactRowCount(SqlSugarClient db, string tableName)
        {
            string sql = $"SELECT COUNT(1) AS Value FROM {QuoteIdentifier(tableName)}";
            ScalarLongRow? row = db.Ado.SqlQuery<ScalarLongRow>(sql).FirstOrDefault();
            return row?.Value ?? 0;
        }

        private static string? ResolveTimeColumn(Dictionary<string, HashSet<string>> columnsByTable, string tableName)
        {
            if (!columnsByTable.TryGetValue(tableName, out HashSet<string>? columns))
                return null;

            foreach (string candidate in CandidateTimeColumns)
            {
                if (columns.Contains(candidate))
                    return candidate;
            }

            return null;
        }

        private static string? ResolveTimeColumn(SqlSugarClient db, string databaseName, string tableName)
        {
            string sql = $@"SELECT COLUMN_NAME AS ColumnName
                            FROM INFORMATION_SCHEMA.COLUMNS
                            WHERE TABLE_SCHEMA = @dbName
                              AND TABLE_NAME = @tableName
                              AND COLUMN_NAME IN ({string.Join(",", CandidateTimeColumns.Select(item => $"'{item}'"))})";

            List<TableColumnNameRow> rows = db.Ado.SqlQuery<TableColumnNameRow>(sql, new { dbName = databaseName, tableName });
            foreach (string candidate in CandidateTimeColumns)
            {
                if (rows.Any(item => string.Equals(item.ColumnName, candidate, StringComparison.OrdinalIgnoreCase)))
                    return candidate;
            }

            return null;
        }

        private int DeleteRowsBeforeCutoff(SqlSugarClient db, string tableName, string timeColumn, DateTime cutoffDate)
        {
            string sql = $"DELETE FROM {QuoteIdentifier(tableName)} WHERE {QuoteIdentifier(timeColumn)} < @cutoffDate";
            return db.Ado.ExecuteCommand(sql, new SugarParameter("@cutoffDate", cutoffDate));
        }

        private int DeleteLinkedRowsBeforeCutoff(SqlSugarClient db, string detailTableName, string detailForeignKeyColumn, string masterTableName, string masterKeyColumn, string masterTimeColumn, DateTime cutoffDate)
        {
            string detailTable = QuoteIdentifier(detailTableName);
            string masterTable = QuoteIdentifier(masterTableName);
            string sql = $"DELETE d FROM {detailTable} d INNER JOIN {masterTable} m ON d.{QuoteIdentifier(detailForeignKeyColumn)} = m.{QuoteIdentifier(masterKeyColumn)} WHERE m.{QuoteIdentifier(masterTimeColumn)} < @cutoffDate";
            return db.Ado.ExecuteCommand(sql, new SugarParameter("@cutoffDate", cutoffDate));
        }

        private HashSet<string> GetExistingCleanupTables(SqlSugarClient db, string databaseName)
        {
            string sql = $@"SELECT TABLE_NAME AS TableName
                            FROM INFORMATION_SCHEMA.TABLES
                            WHERE TABLE_SCHEMA = @dbName
                              AND (
                                  TABLE_NAME = @resultMasterTableName
                                  OR TABLE_NAME = @measureBatchTableName
                                  OR TABLE_NAME LIKE @algorithmDetailPattern
                                  OR TABLE_NAME LIKE @measureResultPattern)";

            return db.Ado.SqlQuery<TableNameRow>(sql, new
            {
                dbName = databaseName,
                resultMasterTableName = ResultMasterTableName,
                measureBatchTableName = MeasureBatchTableName,
                algorithmDetailPattern = $"{AlgorithmDetailPrefix}%",
                measureResultPattern = $"{MeasureResultPrefix}%",
            })
                .Select(item => item.TableName)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static SqlSugarClient CreateDbClient(int timeout)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(MySqlSetting.Instance.MySqlConfig, timeout),
                DbType = SqlSugar.DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private string GetDatabaseName()
        {
            string databaseName = MySqlSetting.Instance.MySqlConfig.Database;
            if (string.IsNullOrWhiteSpace(databaseName))
                throw new InvalidOperationException("当前未配置数据库名。");

            return databaseName;
        }

        private bool TryGetKeepMonths(out int keepMonths)
        {
            keepMonths = 0;
            if (!int.TryParse(CleanupKeepMonthsText, out keepMonths) || keepMonths <= 0)
            {
                MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_EnterValidKeepMonths, "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private string BuildCleanupTableBulletList()
        {
            if (CleanupTables.Count > 0)
            {
                return string.Join(Environment.NewLine, CleanupTables.Where(item => item.Exists).Select(item => $" - {item.DisplayName} ({item.TableName})"));
            }

            return string.Join(Environment.NewLine, new[]
            {
                $" - {GetCleanupTableDisplayName(ResultMasterTableName)} ({ResultMasterTableName})",
                $" - 所有 {AlgorithmDetailPrefix}* 数据表",
                $" - {GetCleanupTableDisplayName(MeasureBatchTableName)} ({MeasureBatchTableName})",
                $" - 所有 {MeasureResultPrefix}* 数据表",
            });
        }

        private void ApplyCleanupTableSnapshot(IReadOnlyList<MySqlCleanupTableInfo> snapshot)
        {
            RunOnUi(() =>
            {
                CleanupTables.Clear();
                foreach (MySqlCleanupTableInfo item in snapshot)
                {
                    CleanupTables.Add(item);
                }
            });
        }

        private void SetCleanupBusy(bool value) => RunOnUi(() => IsCleanupBusy = value);

        private void SetCleanupStatus(string value) => RunOnUi(() => CleanupStatus = value);

        private static void RunOnUi(Action action)
        {
            if (Application.Current?.Dispatcher == null || Application.Current.Dispatcher.CheckAccess())
            {
                action();
                return;
            }

            Application.Current.Dispatcher.Invoke(action);
        }

        private static string QuoteIdentifier(string identifier)
        {
            return $"`{identifier.Replace("`", "``")}`";
        }

        private static Dictionary<string, HashSet<string>> GetColumnsByTable(SqlSugarClient db, string databaseName, IEnumerable<string> tableNames)
        {
            List<string> tableNameList = tableNames.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            if (tableNameList.Count == 0)
            {
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
            }

            string tableNameSql = string.Join(",", tableNameList.Select(item => $"'{item.Replace("'", "''")}'"));
            return db.Ado.SqlQuery<TableColumnRow>(
                $@"SELECT TABLE_NAME AS TableName,
                           COLUMN_NAME AS ColumnName
                    FROM INFORMATION_SCHEMA.COLUMNS
                    WHERE TABLE_SCHEMA = @dbName
                      AND TABLE_NAME IN ({tableNameSql})",
                new { dbName = databaseName })
                .GroupBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    item => item.Key,
                    item => new HashSet<string>(item.Select(column => column.ColumnName), StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static bool TableHasColumn(Dictionary<string, HashSet<string>> columnsByTable, string tableName, string columnName)
        {
            return columnsByTable.TryGetValue(tableName, out HashSet<string>? columns) && columns.Contains(columnName);
        }

        private static bool IsAlgorithmDetailTable(string tableName)
        {
            return tableName.StartsWith(AlgorithmDetailPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMeasureResultTable(string tableName)
        {
            return tableName.StartsWith(MeasureResultPrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static List<string> OrderCleanupTablesForDisplay(IEnumerable<string> tableNames)
        {
            return tableNames
                .OrderBy(GetDisplayOrder)
                .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static List<string> OrderCleanupTablesForExecution(IEnumerable<string> tableNames)
        {
            return tableNames
                .OrderBy(GetExecutionOrder)
                .ThenBy(item => item, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static int GetDisplayOrder(string tableName)
        {
            if (string.Equals(tableName, ResultMasterTableName, StringComparison.OrdinalIgnoreCase))
                return 0;

            if (IsAlgorithmDetailTable(tableName))
                return 1;

            if (string.Equals(tableName, MeasureBatchTableName, StringComparison.OrdinalIgnoreCase))
                return 2;

            if (IsMeasureResultTable(tableName))
                return 3;

            return 4;
        }

        private static int GetExecutionOrder(string tableName)
        {
            if (IsAlgorithmDetailTable(tableName))
                return 0;

            if (IsMeasureResultTable(tableName))
                return 1;

            if (string.Equals(tableName, ResultMasterTableName, StringComparison.OrdinalIgnoreCase))
                return 2;

            if (string.Equals(tableName, MeasureBatchTableName, StringComparison.OrdinalIgnoreCase))
                return 3;

            return 4;
        }

        private static string GetCleanupTableDisplayName(string tableName)
        {
            if (string.Equals(tableName, ResultMasterTableName, StringComparison.OrdinalIgnoreCase))
                return "算法结果主表";

            if (string.Equals(tableName, MeasureBatchTableName, StringComparison.OrdinalIgnoreCase))
                return "测量批次主表";

            if (IsAlgorithmDetailTable(tableName))
                return $"算法结果明细/{tableName[AlgorithmDetailPrefix.Length..]}";

            if (IsMeasureResultTable(tableName))
                return $"测量结果/{tableName[MeasureResultPrefix.Length..]}";

            return tableName;
        }

        //备份所有数据
        public string BackupAllMysql()
        {
            return RunDatabaseMaintenance(() => CreateMySqlBackup("All", GetTableNames(), replaceExistingRows: false));
        }

        //备份Mysql资源
        public string BackupMysqlResource()
        {
            return RunDatabaseMaintenance(() => CreateMySqlBackup(
                "Res",
                GetFilteredResourceTableNames(),
                replaceExistingRows: true,
                AppendMigrationDictionaryDependencySql));
        }

        private string CreateMySqlBackup(string prefix, IReadOnlyCollection<string> tableNames, bool replaceExistingRows, Action<string>? preparePartFile = null)
        {
            Directory.CreateDirectory(BackupPath);
            string backupFile = Path.Combine(BackupPath, $"{prefix}_{DateTime.Now:yyyyMMddHHmmssfff}_{Guid.NewGuid():N}.sql");
            string partFile = backupFile + ".part";

            try
            {
                RunMysqlDump(partFile, tableNames, replaceExistingRows);
                preparePartFile?.Invoke(partFile);

                FileInfo partInfo = new(partFile);
                if (!partInfo.Exists || partInfo.Length <= 0)
                    throw new InvalidOperationException("mysqldump 未生成有效的 SQL 备份文件。");

                File.Move(partFile, backupFile);
            }
            catch
            {
                TryDeletePartialBackup(partFile);
                throw;
            }

            RunOnUi(() => Backups.Add(new MysqlBack(backupFile)));
            return backupFile;
        }

        private static void RunMysqlDump(string outputFile, IReadOnlyCollection<string> tableNames, bool replaceExistingRows)
        {
            RunMysqlDumpAsync(outputFile, tableNames, replaceExistingRows).GetAwaiter().GetResult();
        }

        private static async Task RunMysqlDumpAsync(string outputFile, IReadOnlyCollection<string> tableNames, bool replaceExistingRows)
        {
            MySqlConfig config = MySqlSetting.Instance.MySqlConfig;
            ProcessStartInfo startInfo = CreateMySqlProcessStartInfo(Config.MysqldumpPath, config, redirectStandardInput: false);
            if (replaceExistingRows)
                startInfo.ArgumentList.Add("--replace");

            startInfo.ArgumentList.Add(config.Database);
            foreach (string tableName in tableNames)
            {
                startInfo.ArgumentList.Add(tableName);
            }

            using FileStream output = new(outputFile, new FileStreamOptions
            {
                Mode = FileMode.CreateNew,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("无法启动 mysqldump 进程。");

            Task copyOutputTask = process.StandardOutput.BaseStream.CopyToAsync(output);
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            string standardError = await CompleteMySqlProcessAsync(process, "mysqldump", errorTask, copyOutputTask).ConfigureAwait(false);
            await output.FlushAsync().ConfigureAwait(false);
            output.Flush(flushToDisk: true);

            if (process.ExitCode != 0)
                throw CreateMySqlProcessException("mysqldump", process.ExitCode, standardError);
        }

        private static void TryDeletePartialBackup(string partFile)
        {
            try
            {
                if (File.Exists(partFile))
                    File.Delete(partFile);
            }
            catch (Exception ex)
            {
                log.Warn($"删除失败的MySQL临时备份文件失败：{partFile}", ex);
            }
        }
        public List<string> GetTableNames()
        {
            var dbName = MySqlSetting.Instance.MySqlConfig.Database;
            var sql = @"SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES WHERE TABLE_SCHEMA = @dbName AND TABLE_TYPE = 'BASE TABLE'";
            using var DB = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            var result = DB.Ado.SqlQuery<string>(sql, new { dbName });
            return result;
        }

        public List<string> GetFilteredResourceTableNames()
        {
            return MigrationBackupTableNames.ToList();
        }

        private void AppendMigrationDictionaryDependencySql(string backupFile)
        {
            try
            {
                if (!File.Exists(backupFile))
                {
                    return;
                }

                string dependencySql = BuildMigrationDictionaryDependencySql(MySqlControl.GetConnectionString());
                if (string.IsNullOrWhiteSpace(dependencySql))
                {
                    return;
                }

                File.AppendAllText(backupFile, Environment.NewLine + dependencySql, new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                log.Error("追加模板字典依赖备份失败", ex);
            }
        }

        public static string BuildMigrationDictionaryDependencySql(string connectionString, Action<string>? logCallback = null)
        {
            try
            {
                using var db = new SqlSugarClient(new ConnectionConfig { ConnectionString = connectionString, DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                StringBuilder sql = new();
                AppendReferencedRowsSql(
                    db,
                    sql,
                    DictionaryMasterTableName,
                    $@"EXISTS (
                           SELECT 1
                           FROM {QuoteIdentifier(ModParamMasterTableName)} m
                           WHERE m.{QuoteIdentifier("mm_id")} = {QuoteIdentifier(DictionaryMasterTableName)}.{QuoteIdentifier("id")})
                       AND COALESCE({QuoteIdentifier(DictionaryMasterTableName)}.{QuoteIdentifier("mod_type")}, 0) <> 5");
                AppendReferencedRowsSql(
                    db,
                    sql,
                    DictionaryItemTableName,
                    $@"EXISTS (
                           SELECT 1
                           FROM {QuoteIdentifier(ModParamDetailTableName)} d
                           WHERE d.{QuoteIdentifier("cc_pid")} = {QuoteIdentifier(DictionaryItemTableName)}.{QuoteIdentifier("id")})
                       AND NOT EXISTS (
                           SELECT 1
                           FROM {QuoteIdentifier(DictionaryMasterTableName)} dm
                           WHERE dm.{QuoteIdentifier("id")} = {QuoteIdentifier(DictionaryItemTableName)}.{QuoteIdentifier("pid")}
                             AND dm.{QuoteIdentifier("mod_type")} = 5)");
                sql.Append(SensorTemplateMigrationSqlBuilder.Build(db));

                if (sql.Length == 0)
                {
                    return string.Empty;
                }

                logCallback?.Invoke(ColorVision.Engine.Properties.Resources.Mysql_DictionaryDependenciesAdded);
                return $"-- Referenced template dictionary dependencies{Environment.NewLine}{sql}";
            }
            catch (Exception ex)
            {
                logCallback?.Invoke(string.Format(ColorVision.Engine.Properties.Resources.Mysql_DictionaryDependenciesAddFailed, ex.Message));
                return string.Empty;
            }
        }

        private static void AppendReferencedRowsSql(SqlSugarClient db, StringBuilder sql, string tableName, string whereClause)
        {
            List<string> columns = GetTableColumns(db, tableName);
            if (columns.Count == 0)
            {
                return;
            }

            string columnSql = string.Join(", ", columns.Select(QuoteIdentifier));
            DataTable rows = db.Ado.GetDataTable($"SELECT {columnSql} FROM {QuoteIdentifier(tableName)} WHERE {whereClause}");
            if (rows.Rows.Count == 0)
            {
                return;
            }

            sql.AppendLine($"-- {tableName}: {rows.Rows.Count} referenced row(s)");
            foreach (DataRow row in rows.Rows)
            {
                string values = string.Join(", ", columns.Select(column => FormatSqlValue(row[column])));
                sql.AppendLine($"INSERT IGNORE INTO {QuoteIdentifier(tableName)} ({columnSql}) VALUES ({values});");
            }
        }

        private static List<string> GetTableColumns(SqlSugarClient db, string tableName)
        {
            return db.Ado.SqlQuery<string>(
                @"SELECT COLUMN_NAME
                  FROM INFORMATION_SCHEMA.COLUMNS
                  WHERE TABLE_SCHEMA = DATABASE()
                    AND TABLE_NAME = @tableName
                  ORDER BY ORDINAL_POSITION",
                new { tableName });
        }

        private static string FormatSqlValue(object? value)
        {
            if (value == null || value == DBNull.Value)
            {
                return "NULL";
            }

            return value switch
            {
                bool boolValue => boolValue ? "1" : "0",
                byte[] bytes => "0x" + Convert.ToHexString(bytes),
                DateTime dateTime => $"'{dateTime:yyyy-MM-dd HH:mm:ss.ffffff}'",
                DateTimeOffset dateTimeOffset => $"'{dateTimeOffset.LocalDateTime:yyyy-MM-dd HH:mm:ss.ffffff}'",
                byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal => Convert.ToString(value, CultureInfo.InvariantCulture) ?? "NULL",
                _ => $"'{EscapeSqlValue(Convert.ToString(value, CultureInfo.InvariantCulture) ?? string.Empty)}'"
            };
        }

        private static string EscapeSqlValue(string value)
        {
            return value
                .Replace("\\", "\\\\")
                .Replace("'", "\\'")
                .Replace("\0", "\\0")
                .Replace("\b", "\\b")
                .Replace("\n", "\\n")
                .Replace("\r", "\\r")
                .Replace("\t", "\\t")
                .Replace("\u001A", "\\Z");
        }



        public string RestoreMysql(string backupFile)
        {
            return RunDatabaseMaintenance(() => RestoreMysqlCore(backupFile));
        }

        private static string RestoreMysqlCore(string backupFile)
        {
            return RestoreMysqlCoreAsync(backupFile).GetAwaiter().GetResult();
        }

        private static async Task<string> RestoreMysqlCoreAsync(string backupFile)
        {
            string fullBackupPath = Path.GetFullPath(backupFile);
            if (!string.Equals(Path.GetExtension(fullBackupPath), ".sql", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("仅支持恢复 .sql 备份文件。");
            if (!File.Exists(fullBackupPath))
                throw new FileNotFoundException("MySQL备份文件不存在。", fullBackupPath);
            if (new FileInfo(fullBackupPath).Length <= 0)
                throw new InvalidOperationException("MySQL备份文件为空，已中止恢复。");

            MySqlConfig config = MySqlSetting.Instance.MySqlConfig;
            ProcessStartInfo startInfo = CreateMySqlProcessStartInfo(Config.MysqlPath, config, redirectStandardInput: true);
            startInfo.ArgumentList.Add(config.Database);

            using FileStream input = new(fullBackupPath, new FileStreamOptions
            {
                Mode = FileMode.Open,
                Access = FileAccess.Read,
                Share = FileShare.Read,
                Options = FileOptions.Asynchronous | FileOptions.SequentialScan,
            });
            using Process process = new() { StartInfo = startInfo };
            if (!process.Start())
                throw new InvalidOperationException("无法启动 mysql 进程。");

            Task inputTask = CopySqlToStandardInputAsync(input, process);
            Task outputTask = process.StandardOutput.BaseStream.CopyToAsync(Stream.Null);
            Task<string> errorTask = process.StandardError.ReadToEndAsync();
            string standardError = await CompleteMySqlProcessAsync(process, "mysql", errorTask, inputTask, outputTask).ConfigureAwait(false);
            if (process.ExitCode != 0)
                throw CreateMySqlProcessException("mysql", process.ExitCode, standardError);

            return fullBackupPath;
        }

        private static async Task CopySqlToStandardInputAsync(Stream input, Process process)
        {
            Exception? streamException = null;
            try
            {
                await input.CopyToAsync(process.StandardInput.BaseStream).ConfigureAwait(false);
                await process.StandardInput.BaseStream.FlushAsync().ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                streamException = ex;
            }

            try
            {
                process.StandardInput.Close();
            }
            catch (Exception ex)
            {
                streamException ??= ex;
            }

            if (streamException != null)
                throw new IOException("向 mysql 写入 SQL 备份失败。", streamException);
        }

        private static async Task<string> CompleteMySqlProcessAsync(Process process, string toolName, Task<string> errorTask, params Task[] streamTasks)
        {
            Task exitTask = process.WaitForExitAsync();
            Task[] allTasks = [exitTask, errorTask, .. streamTasks];
            List<Task> pendingTasks = allTasks.ToList();
            Stopwatch stopwatch = Stopwatch.StartNew();

            try
            {
                while (pendingTasks.Count > 0)
                {
                    TimeSpan remaining = MySqlCommandTimeout - stopwatch.Elapsed;
                    if (remaining <= TimeSpan.Zero)
                        throw new TimeoutException($"{toolName} 执行超过 {MySqlCommandTimeout.TotalHours:0.#} 小时，已终止。");

                    using CancellationTokenSource timeoutCancellation = new();
                    Task timeoutTask = Task.Delay(remaining, timeoutCancellation.Token);
                    Task completedTask = await Task.WhenAny([.. pendingTasks, timeoutTask]).ConfigureAwait(false);
                    if (ReferenceEquals(completedTask, timeoutTask))
                        throw new TimeoutException($"{toolName} 执行超过 {MySqlCommandTimeout.TotalHours:0.#} 小时，已终止。");

                    timeoutCancellation.Cancel();
                    pendingTasks.Remove(completedTask);
                    await completedTask.ConfigureAwait(false);
                }

                return await errorTask.ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                TerminateMySqlProcess(process, toolName);
                await ObserveMySqlProcessTasksAsync(allTasks).ConfigureAwait(false);
                throw;
            }
            catch (Exception ex)
            {
                TerminateMySqlProcess(process, toolName);
                await ObserveMySqlProcessTasksAsync(allTasks).ConfigureAwait(false);
                throw new InvalidOperationException($"{toolName} 数据流处理失败，进程已终止。", ex);
            }
        }

        private static void TerminateMySqlProcess(Process process, string toolName)
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch (Exception ex)
            {
                log.Warn($"终止超时或数据流失败的 {toolName} 进程失败。", ex);
            }
        }

        private static async Task ObserveMySqlProcessTasksAsync(Task[] tasks)
        {
            try
            {
                await Task.WhenAll(tasks).WaitAsync(TimeSpan.FromSeconds(15)).ConfigureAwait(false);
            }
            catch
            {
                // The original timeout or stream failure is reported by the caller.
            }
        }

        internal static ProcessStartInfo CreateMySqlProcessStartInfo(string executablePath, MySqlConfig config, bool redirectStandardInput)
        {
            ArgumentNullException.ThrowIfNull(config);
            if (string.IsNullOrWhiteSpace(executablePath))
                throw new InvalidOperationException("未配置MySQL命令行工具路径。");

            string fullExecutablePath = Path.GetFullPath(executablePath);
            if (!File.Exists(fullExecutablePath))
                throw new FileNotFoundException("MySQL命令行工具不存在。", fullExecutablePath);
            if (string.IsNullOrWhiteSpace(config.UserName))
                throw new InvalidOperationException("MySQL用户名不能为空。");
            if (string.IsNullOrWhiteSpace(config.Host))
                throw new InvalidOperationException("MySQL地址不能为空。");
            if (string.IsNullOrWhiteSpace(config.Database))
                throw new InvalidOperationException("MySQL数据库名不能为空。");

            ProcessStartInfo startInfo = new()
            {
                FileName = fullExecutablePath,
                WorkingDirectory = Path.GetDirectoryName(fullExecutablePath) ?? string.Empty,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = redirectStandardInput,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            startInfo.ArgumentList.Add("--user");
            startInfo.ArgumentList.Add(config.UserName);
            startInfo.ArgumentList.Add("--host");
            startInfo.ArgumentList.Add(config.Host);
            if (config.Port > 0)
            {
                startInfo.ArgumentList.Add("--port");
                startInfo.ArgumentList.Add(config.Port.ToString(CultureInfo.InvariantCulture));
            }

            startInfo.Environment["MYSQL_PWD"] = config.UserPwd ?? string.Empty;
            return startInfo;
        }

        private static InvalidOperationException CreateMySqlProcessException(string toolName, int exitCode, string standardError)
        {
            return new InvalidOperationException($"{toolName} 执行失败（退出码 {exitCode}）：{FormatMySqlProcessError(standardError)}");
        }

        private static string FormatMySqlProcessError(string standardError)
        {
            return string.IsNullOrWhiteSpace(standardError) ? "未返回错误信息。" : standardError.Trim();
        }

        private sealed class TableStatusRow
        {
            public string TableName { get; set; } = string.Empty;
            public long? TableRows { get; set; }
            public long? DataLength { get; set; }
            public long? IndexLength { get; set; }
        }

        private sealed class TableColumnRow
        {
            public string TableName { get; set; } = string.Empty;
            public string ColumnName { get; set; } = string.Empty;
        }

        private sealed class TableColumnNameRow
        {
            public string ColumnName { get; set; } = string.Empty;
        }

        private sealed class TimeRangeRow
        {
            public DateTime? OldestTime { get; set; }
            public DateTime? NewestTime { get; set; }
        }

        private sealed class ScalarLongRow
        {
            public long Value { get; set; }
        }

        private sealed class TableNameRow
        {
            public string TableName { get; set; } = string.Empty;
        }
    }
}
