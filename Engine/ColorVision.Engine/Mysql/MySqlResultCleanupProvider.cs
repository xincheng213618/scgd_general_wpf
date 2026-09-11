using SqlSugar;
using ColorVision.Engine;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace ColorVision.Database
{
    public sealed class MySqlResultCleanupProvider : IDatabaseCleanupSourceProvider, IDatabaseCleanupSelectionProvider, IDatabaseCleanupBackupProvider, IDatabaseCleanupMaintenanceProvider, IDatabaseCleanupOptimizationProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(MySqlResultCleanupProvider));

        private const string ResultMasterTableName = "t_scgd_algorithm_result_master";
        private const string MeasureBatchTableName = "t_scgd_measure_batch";
        private const string AlgorithmDetailTablePrefix = "t_scgd_algorithm_result_detail_";
        private const string MeasureDetailTablePrefix = "t_scgd_measure_result_";
        private const string OptimizationLockName = "colorvision_mysql_result_relation_indexes_v1";
        private const int OptimizationMetadataLockTimeoutSeconds = 5;
        private const int OptimizationCommandTimeoutSeconds = 7200;

        private static readonly string[] CandidateTimeColumns = { "create_time", "create_date", "add_time" };

        private static readonly CleanupTableDefinition[] CleanupTableDefinitions =
        {
            new(ResultMasterTableName, CleanupTableKind.ResultMaster),
            new("t_scgd_algorithm_result_detail_sfr", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_poi_mtf", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_poi_cie_file", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_light_area", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_image", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_ghost", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_fov", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_distortion", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_compliance_y", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_compliance_jnd", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_common", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_blackmura", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_binocular_fusion", CleanupTableKind.AlgorithmDetail),
            new("t_scgd_algorithm_result_detail_aoi", CleanupTableKind.AlgorithmDetail),
            new(MeasureBatchTableName, CleanupTableKind.MeasureBatch),
            new("t_scgd_measure_result_img", CleanupTableKind.MeasureDetail),
            new("t_scgd_measure_result_smu", CleanupTableKind.MeasureDetail),
            new("t_scgd_measure_result_smu_scan", CleanupTableKind.MeasureDetail),
            new("t_scgd_measure_result_sensor", CleanupTableKind.MeasureDetail),
            new("t_scgd_measure_result_spectrometer", CleanupTableKind.MeasureDetail),
            new("t_scgd_measure_result_third_party_algorithm", CleanupTableKind.MeasureDetail),
        };

        private static readonly OptimizationIndexDefinition[] OptimizationIndexDefinitions = CleanupTableDefinitions
            .Where(definition => definition.Kind != CleanupTableKind.MeasureBatch)
            .Select(CreateOptimizationIndexDefinition)
            .ToArray();

        public static IReadOnlyList<string> ResultTableNames { get; } = CleanupTableDefinitions.Select(item => item.TableName).ToArray();

        public string Id => "mysql-results";
        public string DisplayName => EngineLocalization.Get("MySQL 结果表");
        public int Order => 10;
        public string OptimizationActionName => EngineLocalization.Get("优化结果关联索引");
        public string OptimizationConfirmationMessage => EngineLocalization.Get("将为现存 MySQL 结果表的 batch_id / pid 关联列创建缺失索引。操作逐表执行，会占用数据库 I/O 和临时磁盘空间，并可能短暂等待元数据锁。请先暂停生产测试并确认 MySQL 临时目录与数据目录空间充足。");
        public string Description
        {
            get
            {
                var config = MySqlSetting.Instance.MySqlConfig;
                return EngineLocalization.Format($"数据库: {config.Database}    主机: {config.Host}:{config.Port}");
            }
        }

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            using var db = CreateDbClient(timeout: 15);

            var tableStats = db.Queryable<DatabaseTableStatusRow>()
                .AS("INFORMATION_SCHEMA.TABLES")
                .Where(row => row.TableSchema == MySqlSetting.Instance.MySqlConfig.Database && CleanupTableDefinitions.Select(item => item.TableName).Contains(row.TableName))
                .Select(row => new DatabaseTableStatusRow
                {
                    TableName = row.TableName,
                    DataLength = row.DataLength,
                    IndexLength = row.IndexLength,
                })
                .ToList()
                .ToDictionary(row => row.TableName, StringComparer.OrdinalIgnoreCase);

            var result = new List<DatabaseCleanupTableInfo>(CleanupTableDefinitions.Length);
            foreach (var definition in CleanupTableDefinitions)
            {
                var info = new DatabaseCleanupTableInfo
                {
                    TableName = definition.TableName,
                    Exists = tableStats.TryGetValue(definition.TableName, out var status),
                };

                if (status != null)
                {
                    info.RowCount = db.Queryable<object>().AS(definition.TableName).Count();
                    info.SizeBytes = (status.DataLength ?? 0) + (status.IndexLength ?? 0);
                }

                result.Add(info);
            }

            return result;
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths)
        {
            return MySqlLocalServicesManager.RunDatabaseMaintenance(() => CleanupHistoryCore(keepMonths));
        }

        private static DatabaseCleanupExecutionResult CleanupHistoryCore(int keepMonths)
        {
            DateTime cutoffDate = DateTime.Now.AddMonths(-keepMonths);
            using var db = CreateDbClient(timeout: 30);

            var existingTables = GetExistingTables(db);
            var unknownDetailTables = FindUnknownDetailTables(existingTables);
            if (unknownDetailTables.Count > 0)
            {
                string tableList = string.Join(Environment.NewLine, unknownDetailTables.Select(tableName => $"- {tableName}"));
                throw new InvalidOperationException(
                    EngineLocalization.Get("检测到未登记的结果明细表，无法安全清理历史数据。请先确认这些表与主表的关联关系：") + Environment.NewLine + tableList);
            }

            var columnsByTable = GetColumnsByTable(db, existingTables);
            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = EngineLocalization.Format($"已保留最近 {keepMonths} 个月的 MySQL 结果数据。")
            };

            string? resultMasterTimeColumn = ResolveTimeColumn(columnsByTable, ResultMasterTableName);
            string? measureBatchTimeColumn = ResolveTimeColumn(columnsByTable, MeasureBatchTableName);

            foreach (var definition in CleanupTableDefinitions.Where(item => item.Kind == CleanupTableKind.AlgorithmDetail && existingTables.Contains(item.TableName)))
            {
                int deletedRows = 0;
                if (existingTables.Contains(ResultMasterTableName)
                    && resultMasterTimeColumn != null
                    && HasColumn(columnsByTable, definition.TableName, "pid"))
                {
                    deletedRows = DeleteByParentDate(db, definition.TableName, "pid", ResultMasterTableName, "id", resultMasterTimeColumn, cutoffDate);
                }
                else if (ResolveTimeColumn(columnsByTable, definition.TableName) is string directTimeColumn)
                {
                    deletedRows = DeleteByDate(db, definition.TableName, directTimeColumn, cutoffDate);
                }

                result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 删除 {deletedRows:N0} 行"));
            }

            if (existingTables.Contains(ResultMasterTableName) && resultMasterTimeColumn != null)
            {
                int deletedRows = DeleteByDate(db, ResultMasterTableName, resultMasterTimeColumn, cutoffDate);
                result.SummaryLines.Add(EngineLocalization.Format($"{ResultMasterTableName}: 删除 {deletedRows:N0} 行"));
            }

            foreach (var definition in CleanupTableDefinitions.Where(item => item.Kind == CleanupTableKind.MeasureDetail && existingTables.Contains(item.TableName)))
            {
                int deletedRows = 0;
                if (existingTables.Contains(MeasureBatchTableName)
                    && measureBatchTimeColumn != null
                    && HasColumn(columnsByTable, definition.TableName, "batch_id"))
                {
                    deletedRows = DeleteByParentDate(db, definition.TableName, "batch_id", MeasureBatchTableName, "id", measureBatchTimeColumn, cutoffDate);
                }
                else if (ResolveTimeColumn(columnsByTable, definition.TableName) is string directTimeColumn)
                {
                    deletedRows = DeleteByDate(db, definition.TableName, directTimeColumn, cutoffDate);
                }

                result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 删除 {deletedRows:N0} 行"));
            }

            if (existingTables.Contains(MeasureBatchTableName) && measureBatchTimeColumn != null)
            {
                int deletedRows = DeleteByDate(db, MeasureBatchTableName, measureBatchTimeColumn, cutoffDate);
                result.SummaryLines.Add(EngineLocalization.Format($"{MeasureBatchTableName}: 删除 {deletedRows:N0} 行"));
            }

            if (result.SummaryLines.Count == 0)
            {
                result.SummaryLines.Add(EngineLocalization.Get("没有找到可执行的 MySQL 历史清理项。"));
            }

            return result;
        }

        public DatabaseCleanupExecutionResult CleanupAll()
        {
            return MySqlLocalServicesManager.RunDatabaseMaintenance(() => CleanupTablesCore(ResultTableNames, isCompleteCleanup: true));
        }

        public DatabaseCleanupExecutionResult CleanupTables(IReadOnlyCollection<string> tableNames)
        {
            var validatedTableNames = ValidateCleanupTableNames(tableNames);
            return MySqlLocalServicesManager.RunDatabaseMaintenance(() => CleanupTablesCore(validatedTableNames, isCompleteCleanup: false));
        }

        public DatabaseCleanupBackupResult CreateBackup()
        {
            return MySqlLocalServicesManager.RunDatabaseMaintenance(() =>
            {
                string backupPath = MySqlLocalServicesManager.GetInstance().BackupAllMysql();
                return new DatabaseCleanupBackupResult
                {
                    FilePath = backupPath,
                    StatusMessage = EngineLocalization.Format($"完整备份已创建：{Path.GetFileName(backupPath)}")
                };
            });
        }

        public DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(Func<DatabaseCleanupExecutionResult> cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(cleanupAction);

            return MySqlLocalServicesManager.RunDatabaseMaintenance(() =>
            {
                var backup = CreateBackup();
                var cleanup = cleanupAction();
                return new DatabaseCleanupMaintenanceResult
                {
                    Backup = backup,
                    Cleanup = cleanup
                };
            });
        }

        public DatabaseCleanupExecutionResult ExecuteOptimization()
        {
            return MySqlLocalServicesManager.RunDatabaseMaintenance(ExecuteOptimizationCore);
        }

        private static DatabaseCleanupExecutionResult ExecuteOptimizationCore()
        {
            Stopwatch totalStopwatch = Stopwatch.StartNew();
            var createdIndexes = new List<string>();
            string? currentTable = null;
            bool maintenanceLockAcquired = false;

            using var db = CreateDbClient(timeout: 30, autoCloseConnection: false);
            db.Ado.CommandTimeOut = OptimizationCommandTimeoutSeconds;

            try
            {
                if (db.Ado.Connection.State != System.Data.ConnectionState.Open)
                    db.Ado.Connection.Open();

                object? databaseValue = db.Ado.GetScalar("SELECT DATABASE();");
                string databaseName = Convert.ToString(databaseValue) ?? string.Empty;
                if (string.IsNullOrWhiteSpace(databaseName))
                    throw new InvalidOperationException(EngineLocalization.Get("当前 MySQL 连接未选择数据库，未执行索引优化。"));

                object? lockValue = db.Ado.GetScalar($"SELECT GET_LOCK('{OptimizationLockName}', 0);");
                maintenanceLockAcquired = lockValue != null
                    && lockValue != DBNull.Value
                    && Convert.ToInt32(lockValue) == 1;
                if (!maintenanceLockAcquired)
                    throw new InvalidOperationException(EngineLocalization.Get("另一个 MySQL 结果索引优化正在执行，本次未做任何更改。"));

                db.Ado.ExecuteCommand($"SET SESSION lock_wait_timeout = {OptimizationMetadataLockTimeoutSeconds};");

                HashSet<string> existingTables = GetExistingTables(db, databaseName);
                Dictionary<string, HashSet<string>> columnsByTable = GetColumnsByTable(db, existingTables, databaseName);
                Dictionary<string, string> tableEngines = LoadTableEngines(db, databaseName, existingTables);
                List<DatabaseIndexRow> indexRows = LoadIndexRows(db, databaseName, existingTables);
                var result = new DatabaseCleanupExecutionResult();
                var pendingIndexes = new List<OptimizationIndexDefinition>();
                var preflightErrors = new List<string>();
                int existingIndexCount = 0;
                int skippedTableCount = 0;

                foreach (string tableName in FindUnknownDetailTables(existingTables))
                {
                    result.SummaryLines.Add(EngineLocalization.Format($"{tableName}: 未在结果表白名单中，已跳过索引优化"));
                    skippedTableCount++;
                }

                foreach (OptimizationIndexDefinition definition in OptimizationIndexDefinitions)
                {
                    if (!existingTables.Contains(definition.TableName))
                    {
                        result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 未找到，已跳过"));
                        skippedTableCount++;
                        continue;
                    }

                    if (!HasColumn(columnsByTable, definition.TableName, definition.ColumnName))
                    {
                        result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 未找到 {definition.ColumnName} 列，已跳过"));
                        skippedTableCount++;
                        continue;
                    }

                    List<DatabaseIndexRow> tableIndexes = indexRows
                        .Where(row => string.Equals(row.TableName, definition.TableName, StringComparison.OrdinalIgnoreCase))
                        .ToList();
                    List<DatabaseIndexRow> namedIndex = tableIndexes
                        .Where(row => string.Equals(row.IndexName, definition.IndexName, StringComparison.OrdinalIgnoreCase))
                        .OrderBy(row => row.SeqInIndex)
                        .ToList();
                    if (namedIndex.Count > 0 && FindEquivalentIndexName(namedIndex, definition.ColumnName) == null)
                    {
                        string columns = string.Join(", ", namedIndex.Select(row => row.ColumnName));
                        preflightErrors.Add(EngineLocalization.Format($"{definition.TableName}: 索引名 {definition.IndexName} 已被非等价定义占用（{columns}）"));
                        continue;
                    }

                    string? equivalentIndexName = FindEquivalentIndexName(tableIndexes, definition.ColumnName);
                    if (equivalentIndexName != null)
                    {
                        result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 已有等价索引 {equivalentIndexName} ({definition.ColumnName} 为首列)，已跳过"));
                        existingIndexCount++;
                        continue;
                    }

                    if (!tableEngines.TryGetValue(definition.TableName, out string? engine)
                        || !string.Equals(engine, "InnoDB", StringComparison.OrdinalIgnoreCase))
                    {
                        string engineName = string.IsNullOrWhiteSpace(engine) ? "<unknown>" : engine;
                        preflightErrors.Add(EngineLocalization.Format(
                            $"{definition.TableName}: 待建索引表的存储引擎为 {engineName}，只允许对 InnoDB 执行 INPLACE / LOCK=NONE"));
                        continue;
                    }

                    pendingIndexes.Add(definition);
                }

                if (preflightErrors.Count > 0)
                {
                    throw new InvalidOperationException(
                        EngineLocalization.Get("索引优化预检失败，未执行任何 DDL：") + Environment.NewLine +
                        string.Join(Environment.NewLine, preflightErrors.Select(error => $"- {error}")));
                }

                foreach (OptimizationIndexDefinition definition in pendingIndexes)
                {
                    currentTable = definition.TableName;

                    List<DatabaseIndexRow> latestIndexes = LoadIndexRows(db, databaseName, new[] { definition.TableName });
                    string? concurrentEquivalentIndex = FindEquivalentIndexName(latestIndexes, definition.ColumnName);
                    if (concurrentEquivalentIndex != null)
                    {
                        result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 执行前已检测到等价索引 {concurrentEquivalentIndex}，已跳过"));
                        existingIndexCount++;
                        continue;
                    }

                    EnsureTargetIndexNameAvailable(latestIndexes, definition);
                    Stopwatch tableStopwatch = Stopwatch.StartNew();
                    string ddl = BuildOptimizationDdl(databaseName, definition);

                    try
                    {
                        db.Ado.ExecuteCommand(ddl);
                    }
                    catch (Exception ex)
                    {
                        List<DatabaseIndexRow> afterFailureIndexes = LoadIndexRows(db, databaseName, new[] { definition.TableName });
                        string? equivalentAfterFailure = FindEquivalentIndexName(afterFailureIndexes, definition.ColumnName);
                        if (equivalentAfterFailure == null)
                            throw new InvalidOperationException(EngineLocalization.Format($"{definition.TableName} 创建索引 {definition.IndexName} 失败：{ex.Message}"), ex);

                        tableStopwatch.Stop();
                        result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 已由并发操作建立等价索引 {equivalentAfterFailure}，用时 {FormatElapsed(tableStopwatch.Elapsed)}"));
                        existingIndexCount++;
                        continue;
                    }

                    tableStopwatch.Stop();
                    List<DatabaseIndexRow> createdIndexRows = LoadIndexRows(db, databaseName, new[] { definition.TableName });
                    string? verifiedIndexName = FindEquivalentIndexName(createdIndexRows, definition.ColumnName);
                    if (verifiedIndexName == null)
                        throw new InvalidOperationException(EngineLocalization.Format($"{definition.TableName}: DDL 已返回，但未验证到 {definition.ColumnName} 首列索引。"));

                    createdIndexes.Add($"{definition.TableName}.{verifiedIndexName}");
                    result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 已创建索引 {verifiedIndexName} ({definition.ColumnName})，用时 {FormatElapsed(tableStopwatch.Elapsed)}"));
                }

                totalStopwatch.Stop();
                result.StatusMessage = EngineLocalization.Format(
                    $"MySQL 结果关联索引优化完成：新建 {createdIndexes.Count:N0} 个，复用 {existingIndexCount:N0} 个，跳过 {skippedTableCount:N0} 张表，总用时 {FormatElapsed(totalStopwatch.Elapsed)}。");
                result.SummaryLines.Add(result.StatusMessage);
                return result;
            }
            catch (Exception ex)
            {
                totalStopwatch.Stop();
                string progress = createdIndexes.Count == 0
                    ? EngineLocalization.Get("本次尚未创建任何索引。")
                    : EngineLocalization.Format($"本次已创建 {createdIndexes.Count:N0} 个索引：{string.Join(", ", createdIndexes)}。");
                string target = string.IsNullOrWhiteSpace(currentTable)
                    ? string.Empty
                    : EngineLocalization.Format($" 当前表：{currentTable}。");
                throw new InvalidOperationException(
                    EngineLocalization.Format($"MySQL 结果关联索引优化失败。{target} {progress} 用时 {FormatElapsed(totalStopwatch.Elapsed)}。问题排除后可重新执行，已有等价索引会自动跳过。{Environment.NewLine}{ex.Message}"),
                    ex);
            }
            finally
            {
                if (maintenanceLockAcquired)
                {
                    try
                    {
                        db.Ado.GetScalar($"SELECT RELEASE_LOCK('{OptimizationLockName}');");
                    }
                    catch (Exception ex)
                    {
                        log.Warn("释放 MySQL 结果索引优化会话锁失败；连接关闭时将由 MySQL 释放。", ex);
                    }
                }
            }
        }

        internal static IReadOnlyList<OptimizationIndexDefinition> GetOptimizationIndexDefinitions()
        {
            return OptimizationIndexDefinitions;
        }

        internal static string GetOptimizationIndexName(string columnName)
        {
            if (string.Equals(columnName, "batch_id", StringComparison.OrdinalIgnoreCase))
                return "idx_cv_batch_id";
            if (string.Equals(columnName, "pid", StringComparison.OrdinalIgnoreCase))
                return "idx_cv_pid";

            throw new ArgumentException(EngineLocalization.Format($"不支持的结果关联列：{columnName}"), nameof(columnName));
        }

        internal static string BuildOptimizationDdl(string databaseName, OptimizationIndexDefinition definition)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
            ArgumentNullException.ThrowIfNull(definition);

            return $"ALTER TABLE {QuoteIdentifier(databaseName)}.{QuoteIdentifier(definition.TableName)} " +
                $"ADD INDEX {QuoteIdentifier(definition.IndexName)} ({QuoteIdentifier(definition.ColumnName)}), " +
                "ALGORITHM=INPLACE, LOCK=NONE;";
        }

        internal static string? FindEquivalentIndexName(IEnumerable<DatabaseIndexRow> indexRows, string columnName)
        {
            ArgumentNullException.ThrowIfNull(indexRows);
            ArgumentException.ThrowIfNullOrWhiteSpace(columnName);

            foreach (IGrouping<string, DatabaseIndexRow> index in indexRows.GroupBy(row => row.IndexName, StringComparer.OrdinalIgnoreCase))
            {
                DatabaseIndexRow? firstColumn = index.OrderBy(row => row.SeqInIndex).FirstOrDefault();
                if (firstColumn != null
                    && firstColumn.SeqInIndex == 1
                    && string.Equals(firstColumn.ColumnName, columnName, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(firstColumn.IndexType, "BTREE", StringComparison.OrdinalIgnoreCase))
                {
                    return index.Key;
                }
            }

            return null;
        }

        internal static void EnsureTargetIndexNameAvailable(IEnumerable<DatabaseIndexRow> indexRows, OptimizationIndexDefinition definition)
        {
            ArgumentNullException.ThrowIfNull(indexRows);
            ArgumentNullException.ThrowIfNull(definition);

            List<DatabaseIndexRow> namedIndex = indexRows
                .Where(row => string.Equals(row.IndexName, definition.IndexName, StringComparison.OrdinalIgnoreCase))
                .OrderBy(row => row.SeqInIndex)
                .ToList();
            if (namedIndex.Count == 0 || FindEquivalentIndexName(namedIndex, definition.ColumnName) != null)
                return;

            string columns = string.Join(", ", namedIndex.Select(row => row.ColumnName));
            throw new InvalidOperationException(EngineLocalization.Format(
                $"{definition.TableName}: 索引名 {definition.IndexName} 已被非等价定义占用（{columns}）"));
        }

        private static OptimizationIndexDefinition CreateOptimizationIndexDefinition(CleanupTableDefinition definition)
        {
            string columnName = definition.Kind == CleanupTableKind.AlgorithmDetail ? "pid" : "batch_id";
            return new OptimizationIndexDefinition(definition.TableName, columnName, GetOptimizationIndexName(columnName));
        }

        private static List<DatabaseIndexRow> LoadIndexRows(SqlSugarClient db, string databaseName, IEnumerable<string> tableNames)
        {
            string[] targetTables = tableNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (targetTables.Length == 0)
                return new List<DatabaseIndexRow>();

            return db.Queryable<DatabaseIndexRow>()
                .AS("INFORMATION_SCHEMA.STATISTICS")
                .Where(row => row.TableSchema == databaseName && targetTables.Contains(row.TableName))
                .Select(row => new DatabaseIndexRow
                {
                    TableName = row.TableName,
                    IndexName = row.IndexName,
                    SeqInIndex = row.SeqInIndex,
                    ColumnName = row.ColumnName,
                    IndexType = row.IndexType,
                })
                .ToList();
        }

        private static Dictionary<string, string> LoadTableEngines(SqlSugarClient db, string databaseName, IEnumerable<string> tableNames)
        {
            string[] targetTables = tableNames.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
            if (targetTables.Length == 0)
                return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            return db.Queryable<DatabaseTableStatusRow>()
                .AS("INFORMATION_SCHEMA.TABLES")
                .Where(row => row.TableSchema == databaseName && row.TableType == "BASE TABLE" && targetTables.Contains(row.TableName))
                .Select(row => new DatabaseTableStatusRow
                {
                    TableName = row.TableName,
                    Engine = row.Engine,
                })
                .ToList()
                .ToDictionary(row => row.TableName, row => row.Engine, StringComparer.OrdinalIgnoreCase);
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            return elapsed.TotalSeconds >= 1
                ? $"{elapsed.TotalSeconds:0.###} s"
                : $"{elapsed.TotalMilliseconds:0} ms";
        }

        internal static IReadOnlyList<string> ValidateCleanupTableNames(IReadOnlyCollection<string> tableNames)
        {
            ArgumentNullException.ThrowIfNull(tableNames);

            if (tableNames.Count == 0)
                throw new ArgumentException(EngineLocalization.Get("至少选择一张要清理的数据表。"), nameof(tableNames));

            var definitionsByName = CleanupTableDefinitions.ToDictionary(item => item.TableName, StringComparer.OrdinalIgnoreCase);
            var validatedDefinitions = new List<CleanupTableDefinition>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (string tableName in tableNames)
            {
                if (string.IsNullOrWhiteSpace(tableName) || !definitionsByName.TryGetValue(tableName, out var definition))
                    throw new ArgumentException(EngineLocalization.Format($"不允许清理未登记的数据表：{tableName}"), nameof(tableNames));

                if (seen.Add(definition.TableName))
                {
                    validatedDefinitions.Add(definition);
                }
            }

            return validatedDefinitions
                .OrderBy(GetCleanupOrder)
                .ThenBy(item => item.TableName, StringComparer.OrdinalIgnoreCase)
                .Select(item => item.TableName)
                .ToArray();
        }

        internal static IReadOnlyList<string> FindUnknownDetailTables(IReadOnlyCollection<string> existingTableNames)
        {
            ArgumentNullException.ThrowIfNull(existingTableNames);

            var registeredTableNames = ResultTableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return existingTableNames
                .Where(tableName => IsAlgorithmDetailTableName(tableName) || IsMeasureDetailTableName(tableName))
                .Where(tableName => !registeredTableNames.Contains(tableName))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .OrderBy(tableName => tableName, StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static DatabaseCleanupExecutionResult CleanupTablesCore(IReadOnlyCollection<string> tableNames, bool isCompleteCleanup)
        {
            using var db = CreateDbClient(timeout: 30);
            var existingTables = GetExistingTables(db);

            var requestedTables = tableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            var definitions = CleanupTableDefinitions
                .Where(definition => requestedTables.Contains(definition.TableName))
                .OrderBy(GetCleanupOrder)
                .ThenBy(definition => definition.TableName, StringComparer.OrdinalIgnoreCase)
                .ToList();
            var result = new DatabaseCleanupExecutionResult();
            int clearedTableCount = 0;

            db.Ado.ExecuteCommand("SET FOREIGN_KEY_CHECKS = 0;");
            try
            {
                foreach (var definition in definitions)
                {
                    if (!existingTables.Contains(definition.TableName))
                    {
                        if (!isCompleteCleanup)
                        {
                            result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 未找到，已跳过"));
                        }
                        continue;
                    }

                    db.Ado.ExecuteCommand($"TRUNCATE TABLE {QuoteIdentifier(definition.TableName)};");
                    result.SummaryLines.Add(EngineLocalization.Format($"{definition.TableName}: 已清空"));
                    clearedTableCount++;
                }
            }
            finally
            {
                db.Ado.ExecuteCommand("SET FOREIGN_KEY_CHECKS = 1;");
            }

            if (clearedTableCount == 0)
            {
                result.SummaryLines.Add(EngineLocalization.Get("没有找到可清空的 MySQL 结果表。"));
            }

            result.StatusMessage = isCompleteCleanup
                ? EngineLocalization.Format($"已清空全部 {clearedTableCount:N0} 张可用 MySQL 结果表。")
                : EngineLocalization.Format($"已清空选中的 {clearedTableCount:N0} 张 MySQL 结果表。");

            return result;
        }

        private static SqlSugarClient CreateDbClient(int timeout, bool autoCloseConnection = true)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(MySqlSetting.Instance.MySqlConfig, timeout),
                DbType = DbType.MySql,
                IsAutoCloseConnection = autoCloseConnection,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private static HashSet<string> GetExistingTables(SqlSugarClient db, string? databaseName = null)
        {
            databaseName ??= MySqlSetting.Instance.MySqlConfig.Database;
            var registeredTableNames = ResultTableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return db.Queryable<DatabaseTableStatusRow>()
                .AS("INFORMATION_SCHEMA.TABLES")
                .Where(row => row.TableSchema == databaseName && row.TableType == "BASE TABLE")
                .Select(row => row.TableName)
                .ToList()
                .Where(tableName => registeredTableNames.Contains(tableName) || IsAlgorithmDetailTableName(tableName) || IsMeasureDetailTableName(tableName))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        private static bool IsAlgorithmDetailTableName(string tableName)
        {
            return tableName.StartsWith(AlgorithmDetailTablePrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsMeasureDetailTableName(string tableName)
        {
            return tableName.StartsWith(MeasureDetailTablePrefix, StringComparison.OrdinalIgnoreCase);
        }

        private static Dictionary<string, HashSet<string>> GetColumnsByTable(SqlSugarClient db, HashSet<string> existingTables, string? databaseName = null)
        {
            if (existingTables.Count == 0)
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            databaseName ??= MySqlSetting.Instance.MySqlConfig.Database;
            return db.Queryable<DatabaseColumnRow>()
                .AS("INFORMATION_SCHEMA.COLUMNS")
                .Where(row => row.TableSchema == databaseName && existingTables.Contains(row.TableName))
                .ToList()
                .GroupBy(row => row.TableName)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(row => row.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase),
                    StringComparer.OrdinalIgnoreCase);
        }

        private static bool HasColumn(Dictionary<string, HashSet<string>> columnsByTable, string tableName, string columnName)
        {
            return columnsByTable.TryGetValue(tableName, out var columns) && columns.Contains(columnName);
        }

        private static string? ResolveTimeColumn(Dictionary<string, HashSet<string>> columnsByTable, string tableName)
        {
            if (!columnsByTable.TryGetValue(tableName, out var columns))
                return null;

            return CandidateTimeColumns.FirstOrDefault(columns.Contains);
        }

        private static int DeleteByDate(SqlSugarClient db, string tableName, string timeColumn, DateTime cutoffDate)
        {
            string sql = $"DELETE FROM {QuoteIdentifier(tableName)} WHERE {QuoteIdentifier(timeColumn)} < @cutoffDate;";
            return db.Ado.ExecuteCommand(sql, new SugarParameter("@cutoffDate", cutoffDate));
        }

        private static int DeleteByParentDate(SqlSugarClient db, string tableName, string foreignKeyColumn, string parentTableName, string parentKeyColumn, string parentTimeColumn, DateTime cutoffDate)
        {
            string sql = $@"
DELETE child
FROM {QuoteIdentifier(tableName)} child
INNER JOIN {QuoteIdentifier(parentTableName)} parent ON child.{QuoteIdentifier(foreignKeyColumn)} = parent.{QuoteIdentifier(parentKeyColumn)}
WHERE parent.{QuoteIdentifier(parentTimeColumn)} < @cutoffDate;";

            return db.Ado.ExecuteCommand(sql, new SugarParameter("@cutoffDate", cutoffDate));
        }

        private static int GetCleanupOrder(CleanupTableDefinition definition)
        {
            return definition.Kind switch
            {
                CleanupTableKind.AlgorithmDetail => 0,
                CleanupTableKind.MeasureDetail => 1,
                CleanupTableKind.ResultMaster => 2,
                CleanupTableKind.MeasureBatch => 3,
                _ => 99,
            };
        }

        private static string QuoteIdentifier(string identifier)
        {
            return $"`{identifier.Replace("`", "``")}`";
        }

        private sealed class CleanupTableDefinition
        {
            public CleanupTableDefinition(string tableName, CleanupTableKind kind)
            {
                TableName = tableName;
                Kind = kind;
            }

            public string TableName { get; }
            public CleanupTableKind Kind { get; }
        }

        internal sealed class OptimizationIndexDefinition
        {
            public OptimizationIndexDefinition(string tableName, string columnName, string indexName)
            {
                TableName = tableName;
                ColumnName = columnName;
                IndexName = indexName;
            }

            public string TableName { get; }
            public string ColumnName { get; }
            public string IndexName { get; }
        }

        private enum CleanupTableKind
        {
            ResultMaster,
            AlgorithmDetail,
            MeasureBatch,
            MeasureDetail,
        }

        private sealed class DatabaseTableStatusRow
        {
            [SugarColumn(ColumnName = "TABLE_SCHEMA")]
            public string TableSchema { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "TABLE_NAME")]
            public string TableName { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "TABLE_TYPE")]
            public string TableType { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "ENGINE")]
            public string Engine { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "DATA_LENGTH")]
            public long? DataLength { get; set; }

            [SugarColumn(ColumnName = "INDEX_LENGTH")]
            public long? IndexLength { get; set; }
        }

        private sealed class DatabaseColumnRow
        {
            [SugarColumn(ColumnName = "TABLE_SCHEMA")]
            public string TableSchema { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "TABLE_NAME")]
            public string TableName { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "COLUMN_NAME")]
            public string ColumnName { get; set; } = string.Empty;
        }

        internal sealed class DatabaseIndexRow
        {
            [SugarColumn(ColumnName = "TABLE_SCHEMA")]
            public string TableSchema { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "TABLE_NAME")]
            public string TableName { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "INDEX_NAME")]
            public string IndexName { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "SEQ_IN_INDEX")]
            public int SeqInIndex { get; set; }

            [SugarColumn(ColumnName = "COLUMN_NAME")]
            public string ColumnName { get; set; } = string.Empty;

            [SugarColumn(ColumnName = "INDEX_TYPE")]
            public string IndexType { get; set; } = string.Empty;
        }
    }
}
