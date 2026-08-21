using SqlSugar;
using ColorVision.Engine;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Database
{
    public sealed class MySqlResultCleanupProvider : IDatabaseCleanupSourceProvider, IDatabaseCleanupSelectionProvider, IDatabaseCleanupBackupProvider, IDatabaseCleanupMaintenanceProvider
    {
        private const string ResultMasterTableName = "t_scgd_algorithm_result_master";
        private const string MeasureBatchTableName = "t_scgd_measure_batch";
        private const string AlgorithmDetailTablePrefix = "t_scgd_algorithm_result_detail_";
        private const string MeasureDetailTablePrefix = "t_scgd_measure_result_";

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

        public static IReadOnlyList<string> ResultTableNames { get; } = CleanupTableDefinitions.Select(item => item.TableName).ToArray();

        public string Id => "mysql-results";
        public string DisplayName => EngineLocalization.Get("MySQL 结果表");
        public int Order => 10;
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

        private static SqlSugarClient CreateDbClient(int timeout)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = MySqlControl.GetConnectionString(MySqlSetting.Instance.MySqlConfig, timeout),
                DbType = DbType.MySql,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private static HashSet<string> GetExistingTables(SqlSugarClient db)
        {
            var registeredTableNames = ResultTableNames.ToHashSet(StringComparer.OrdinalIgnoreCase);
            return db.Queryable<DatabaseTableStatusRow>()
                .AS("INFORMATION_SCHEMA.TABLES")
                .Where(row => row.TableSchema == MySqlSetting.Instance.MySqlConfig.Database && row.TableType == "BASE TABLE")
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

        private static Dictionary<string, HashSet<string>> GetColumnsByTable(SqlSugarClient db, HashSet<string> existingTables)
        {
            if (existingTables.Count == 0)
                return new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);

            return db.Queryable<DatabaseColumnRow>()
                .AS("INFORMATION_SCHEMA.COLUMNS")
                .Where(row => row.TableSchema == MySqlSetting.Instance.MySqlConfig.Database && existingTables.Contains(row.TableName))
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
    }
}
