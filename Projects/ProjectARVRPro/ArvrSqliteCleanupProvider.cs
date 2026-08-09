using ColorVision.Database;
using Microsoft.Data.Sqlite;
using ProjectARVRPro.PluginConfig;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public sealed class ArvrSqliteCleanupProvider :
        IDatabaseCleanupSourceProvider,
        IDatabaseCleanupBackupProvider,
        IDatabaseCleanupMaintenanceProvider,
        IDatabaseCleanupMigrationProvider
    {
        private const string ResultTableName = "ARVRReuslt";
        private const string ObjectiveResultTableName = "ObjectiveTestResultRecord";

        public string Id => "projectarvrpro-sqlite";
        public string DisplayName => "ARVRPro SQLite";
        public string Description => $"数据库文件: {ViewResultManager.SqliteDbPath}";
        public int Order => 21;
        public string MigrationActionName => "迁移并压缩历史结果";
        public string MigrationConfirmationMessage =>
            "将把两张结果表中的旧 TEXT JSON 迁移为同表 GZip BLOB，并在校验一致后清空旧字段、执行 VACUUM 释放空间。" +
            Environment.NewLine +
            "迁移后旧版插件不能读取这些历史 JSON；迁移过程中请勿执行 ARVR 测试。此按钮仅用于现场数据库过渡，全部迁移完成后可移除。";

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            var resultTable = new DatabaseCleanupTableInfo
            {
                TableName = ResultTableName,
                Exists = File.Exists(ViewResultManager.SqliteDbPath),
            };
            var objectiveTable = new DatabaseCleanupTableInfo
            {
                TableName = ObjectiveResultTableName,
                Exists = resultTable.Exists,
            };

            if (!resultTable.Exists)
                return new[] { resultTable, objectiveTable };

            using var db = CreateDbClient();
            resultTable.RowCount = db.Queryable<ProjectARVRReuslt>().Count();
            objectiveTable.RowCount = db.Queryable<ObjectiveTestResultRecord>().Count();
            resultTable.SizeBytes = new FileInfo(ViewResultManager.SqliteDbPath).Length;
            return new[] { resultTable, objectiveTable };
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths)
        {
            return ResultJsonPayloadStorage.RunDatabaseMaintenance(() => CleanupHistoryCore(keepMonths));
        }

        private static DatabaseCleanupExecutionResult CleanupHistoryCore(int keepMonths)
        {
            if (!File.Exists(ViewResultManager.SqliteDbPath))
            {
                return new DatabaseCleanupExecutionResult
                {
                    StatusMessage = "ARVRPro SQLite 数据库文件不存在。"
                };
            }

            DateTime cutoffDate = DateTime.Now.AddMonths(-keepMonths);
            using var db = CreateDbClient();
            int deletedResultRows;
            int deletedObjectiveRows;
            db.Ado.BeginTran();
            try
            {
                deletedResultRows = db.Deleteable<ProjectARVRReuslt>().Where(item => item.CreateTime < cutoffDate).ExecuteCommand();
                deletedObjectiveRows = db.Deleteable<ObjectiveTestResultRecord>().Where(item => item.UpdateTime < cutoffDate).ExecuteCommand();
                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
            TryVacuum(db);
            RefreshArvrWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = $"已保留最近 {keepMonths} 个月的 ARVRPro 数据。"
            };
            result.SummaryLines.Add($"{ResultTableName}: 删除 {deletedResultRows:N0} 行");
            result.SummaryLines.Add($"{ObjectiveResultTableName}: 删除 {deletedObjectiveRows:N0} 行");
            return result;
        }

        public DatabaseCleanupExecutionResult CleanupAll()
        {
            return ResultJsonPayloadStorage.RunDatabaseMaintenance(CleanupAllCore);
        }

        private static DatabaseCleanupExecutionResult CleanupAllCore()
        {
            if (!File.Exists(ViewResultManager.SqliteDbPath))
            {
                return new DatabaseCleanupExecutionResult
                {
                    StatusMessage = "ARVRPro SQLite 数据库文件不存在。"
                };
            }

            using var db = CreateDbClient();
            int deletedResultRows;
            int deletedObjectiveRows;
            db.Ado.BeginTran();
            try
            {
                deletedResultRows = db.Deleteable<ProjectARVRReuslt>().ExecuteCommand();
                deletedObjectiveRows = db.Deleteable<ObjectiveTestResultRecord>().ExecuteCommand();
                TryResetIdentity(db);
                db.Ado.CommitTran();
            }
            catch
            {
                db.Ado.RollbackTran();
                throw;
            }
            TryVacuum(db);
            RefreshArvrWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = "已清空 ARVRPro SQLite 结果表。"
            };
            result.SummaryLines.Add($"{ResultTableName}: 删除 {deletedResultRows:N0} 行");
            result.SummaryLines.Add($"{ObjectiveResultTableName}: 删除 {deletedObjectiveRows:N0} 行");
            return result;
        }

        public DatabaseCleanupBackupResult CreateBackup()
        {
            return ResultJsonPayloadStorage.RunDatabaseMaintenance(CreateBackupCore);
        }

        private static DatabaseCleanupBackupResult CreateBackupCore()
        {
            string databasePath = ViewResultManager.SqliteDbPath;
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("ARVRPro SQLite 数据库文件不存在。", databasePath);

            string databaseDirectory = Path.GetDirectoryName(databasePath)
                ?? throw new InvalidOperationException("无法确定 ARVRPro SQLite 数据库目录。");
            string backupDirectory = Path.Combine(databaseDirectory, "ProjectARVRPro.Backups");
            Directory.CreateDirectory(backupDirectory);
            string backupPath = Path.Combine(
                backupDirectory,
                $"ProjectARVRPro.backup-{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");

            try
            {
                using (var source = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = databasePath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                    DefaultTimeout = 30,
                }.ToString()))
                using (var destination = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = backupPath,
                    Mode = SqliteOpenMode.ReadWriteCreate,
                    Pooling = false,
                    DefaultTimeout = 30,
                }.ToString()))
                {
                    source.Open();
                    destination.Open();
                    source.BackupDatabase(destination);
                }

                using var verification = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = backupPath,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false,
                }.ToString());
                verification.Open();
                using SqliteCommand command = verification.CreateCommand();
                command.CommandText = "PRAGMA quick_check;";
                string quickCheck = Convert.ToString(command.ExecuteScalar()) ?? string.Empty;
                if (!string.Equals(quickCheck, "ok", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException($"备份完整性检查未通过：{quickCheck}");

                return new DatabaseCleanupBackupResult
                {
                    StatusMessage = $"已创建 ARVRPro 完整备份（{FormatSize(new FileInfo(backupPath).Length)}）。",
                    FilePath = backupPath,
                };
            }
            catch
            {
                if (File.Exists(backupPath))
                    File.Delete(backupPath);
                throw;
            }
        }

        public DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(Func<DatabaseCleanupExecutionResult> cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(cleanupAction);

            return ResultJsonPayloadStorage.RunDatabaseMaintenance(() =>
            {
                DatabaseCleanupBackupResult backup = CreateBackupCore();
                try
                {
                    return new DatabaseCleanupMaintenanceResult
                    {
                        Backup = backup,
                        Cleanup = cleanupAction(),
                    };
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        $"{ex.Message}{Environment.NewLine}完整备份已保留：{backup.FilePath}",
                        ex);
                }
            });
        }

        public DatabaseCleanupExecutionResult ExecuteMigration()
        {
            return ResultJsonPayloadStorage.RunDatabaseMaintenance(ExecuteMigrationCore);
        }

        private static DatabaseCleanupExecutionResult ExecuteMigrationCore()
        {
            LegacyResultJsonMigrationReport report = LegacyResultJsonMigration.Execute(ViewResultManager.SqliteDbPath);
            RefreshArvrWindowIfOpen();

            int migrated = report.ViewResultRowsMigrated + report.ObjectiveResultRowsMigrated;
            int residualCleared = report.ViewResidualRowsCleared + report.ObjectiveResidualRowsCleared;
            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = migrated == 0 && residualCleared == 0
                    ? "未发现待迁移的旧 JSON，已完成数据库压缩整理。"
                    : $"已迁移 {migrated:N0} 条历史 JSON，并释放 SQLite 空闲空间。"
            };
            result.SummaryLines.Add($"{ResultTableName}: 迁移 {report.ViewResultRowsMigrated:N0} 条，清理残留旧字段 {report.ViewResidualRowsCleared:N0} 条");
            result.SummaryLines.Add($"{ObjectiveResultTableName}: 迁移 {report.ObjectiveResultRowsMigrated:N0} 条，清理残留旧字段 {report.ObjectiveResidualRowsCleared:N0} 条");
            result.SummaryLines.Add($"数据库大小: {FormatSize(report.BeforeBytes)} → {FormatSize(report.AfterBytes)}");
            result.SummaryLines.Add($"SQLite 完整性检查: {report.IntegrityCheck}");
            return result;
        }

        private static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={ViewResultManager.SqliteDbPath};Default Timeout=5",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private static void TryResetIdentity(SqlSugarClient db)
        {
            try
            {
                db.Ado.ExecuteCommand(
                    $"DELETE FROM sqlite_sequence WHERE name IN ('{ResultTableName}', '{ObjectiveResultTableName}');");
            }
            catch
            {
            }
        }

        private static void TryVacuum(SqlSugarClient db)
        {
            try
            {
                db.Ado.ExecuteCommand("VACUUM;");
            }
            catch
            {
            }
        }

        private static void RefreshArvrWindowIfOpen()
        {
            if (ProjectWindowInstance.WindowInstance == null || Application.Current?.Dispatcher == null)
                return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                var manager = ViewResultManager.GetInstance();
                manager.LoadAll(manager.Config.Count);
            });
        }

        private static string FormatSize(long bytes)
        {
            string[] units = ["B", "KB", "MB", "GB", "TB"];
            double value = Math.Max(0, bytes);
            int unitIndex = 0;
            while (value >= 1024 && unitIndex < units.Length - 1)
            {
                value /= 1024;
                unitIndex++;
            }
            return $"{value:0.##} {units[unitIndex]}";
        }
    }
}
