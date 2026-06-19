#pragma warning disable CA1822,CA1863,CS8602,CS8603
using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Themes.Controls;
using ColorVision.UI;
using log4net;
using Microsoft.Win32;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
            Task.Run(() =>
            {
                MySqlLocalServicesManager.GetInstance().RestoreMysql(FilePath);
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_RestoreSuccessRestartRequired);
                });
            });
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
        private const string ResultMasterTableName = "t_scgd_algorithm_result_master";
        private const string MeasureBatchTableName = "t_scgd_measure_batch";
        private const string AlgorithmDetailPrefix = "t_scgd_algorithm_result_detail_";
        private const string MeasureResultPrefix = "t_scgd_measure_result_";
        private static readonly string[] CandidateTimeColumns = { "create_time", "create_date", "add_time" };
        private static MySqlLocalServicesManager _instance;
        private static readonly object _locker = new();
        public static MySqlLocalServicesManager GetInstance() { lock (_locker) { return _instance ??= new MySqlLocalServicesManager(); } }
        public string BackupPath { get; set; } = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "ColorVision", "Backup");
        public ObservableCollection<MysqlBack> Backups { get; set; } = new ObservableCollection<MysqlBack>();
        public ObservableCollection<MySqlCleanupTableInfo> CleanupTables { get; } = new ObservableCollection<MySqlCleanupTableInfo>();


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

            var sqlFiles = Directory.GetFiles(BackupPath)
                .Where(f => f.EndsWith(".sql", StringComparison.CurrentCulture))
                .OrderByDescending(f => File.GetCreationTime(f));

            Backups.Clear(); // 如果需要清空原有数据
            foreach (var item in sqlFiles)
            {
                Backups.Add(new MysqlBack(item));
            }
            RestoreSelectCommand = new RelayCommand(a => RestoreSelect());
            BackupResourcesCommand = new RelayCommand(a => BackupResources());
            BackupAllResourcesCommand = new RelayCommand(a => BackupAllMysql());
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
            if (IsRun)
            {
                MessageBox.Show(ColorVision.Engine.Properties.Resources.Engine_Msg_BackupInProgress);
                return;
            }
            Task.Run(() =>
            {
                IsRun = true;
                BackupMysqlResource();
                Application.Current.Dispatcher.Invoke(() =>
                {
                    MessageBox.Show(Application.Current.GetActiveWindow(), ColorVision.Engine.Properties.Resources.Engine_Msg_BackupSuccess);
                });
                IsRun = false;
            });
        }


        public void RestoreSelect()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                InitialDirectory = BackupPath, // Set the initial directory
                Filter = "SQL Files (*.sql)|*.sql|All Files (*.*)|*.*", // Filter for file types
                Title = "Select a Backup File"
            };

            // Show the dialog and get the result
            if (openFileDialog.ShowDialog() == true)
            {
                string filePath = openFileDialog.FileName; // Get the selected file path

                Task.Run(() =>
                {
                    RestoreMysql(filePath); // Use the selected file path
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MessageBox.Show(Application.Current.MainWindow, ColorVision.Engine.Properties.Resources.Engine_Msg_RestoreSuccessRestarting);


                        if (Tool.ExecuteCommandAsAdmin("net stop RegistrationCenterService&&net start RegistrationCenterService"))
                        {
                            MessageBox.Show(Application.Current.MainWindow, ColorVision.Engine.Properties.Resources.Engine_Msg_ServiceRestartSuccess);
                            Process.Start(Application.ResourceAssembly.Location.Replace(".dll", ".exe"), "-r");
                            Application.Current.Shutdown();
                        }
                        else
                        {
                            MessageBox.Show(Application.Current.MainWindow, ColorVision.Engine.Properties.Resources.Engine_Msg_ServiceRestartFailed);
                        }


                    });
                });
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
        public void BackupAllMysql()
        {
            //备份的信息里应该只包含基础的信息不应该包含许多逻辑
            string BackTable = string.Join(" ", GetTableNames());

            string BackUpSql = Path.Combine(BackupPath, $"All_{DateTime.Now:yyyyMMddHHmmss}.sql");
            string backCommnad = $"{Config.MysqldumpPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} >\"{BackUpSql}\"";
            Common.Utilities.Tool.ExecuteCommandUI(backCommnad);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Backups.Add(new MysqlBack(BackUpSql));
            });
        }
        //备份Mysql资源
        public void BackupMysqlResource()
        {
            //备份的信息里应该只包含基础的信息不应该包含许多逻辑
            string BackTable = string.Join(" ",GetFilteredResourceTableNames());
            string BackUpSql = Path.Combine(BackupPath, $"Res_{DateTime.Now:yyyyMMddHHmmss}.sql");
            string backCommnad = $"{Config.MysqldumpPath} --replace -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} {BackTable} > \"{BackUpSql}\"";
            Common.Utilities.Tool.ExecuteCommandUI(backCommnad);
            Application.Current.Dispatcher.Invoke(() =>
            {
                Backups.Add(new MysqlBack(BackUpSql));
            });
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
            var tableNames = new List<string>() { "t_scgd_algorithm_poi_template_detail", "t_scgd_algorithm_poi_template_master", "t_scgd_camera_license", "t_scgd_mod_param_detail", "t_scgd_mod_param_master", "t_scgd_sys_resource", "t_scgd_sys_resource_group" };
            return tableNames;
        }



        public void RestoreMysql(string backupFile)
        {
            if (!File.Exists(backupFile))
            {
                MessageBox.Show("Backup file not found.");
                return;
            }
            string restoreCommand = $"{Config.MysqlPath} -u {MySqlSetting.Instance.MySqlConfig.UserName} -h {MySqlSetting.Instance.MySqlConfig.Host} -p{MySqlSetting.Instance.MySqlConfig.UserPwd} {MySqlSetting.Instance.MySqlConfig.Database} < \"{backupFile}\"";
            Common.Utilities.Tool.ExecuteCommandUI(restoreCommand);
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
