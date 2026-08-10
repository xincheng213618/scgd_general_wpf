using ColorVision.Database;
using Microsoft.Data.Sqlite;
using ProjectKB.PluginConfig;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ProjectKB
{
    public sealed class KbSqliteCleanupProvider :
        IDatabaseCleanupSourceProvider,
        IDatabaseCleanupBackupProvider,
        IDatabaseCleanupMaintenanceProvider,
        IDatabaseCleanupMigrationProvider
    {
        private const string CleanupTableName = "KBItemMaster";
        private const string SessionTableName = "KBProductionSession";

        public string Id => "projectkb-sqlite";
        public string DisplayName => "KB SQLite";
        public string Description => $"数据库文件: {ViewResultManager.SqliteDbPath}";
        public int Order => 20;
        public string MigrationActionName => "迁移并压缩历史结果";
        public string MigrationConfirmationMessage =>
            "将把旧 ItemsJson 迁移为按需加载的 ResultPayloadGzip，并把当前同名 Model 的 Recipe 写入 RecipeSnapshotGzip。" +
            Environment.NewLine +
            "旧数据补写的 Recipe 会明确标记为“由当前关联 Recipe 重建”，它不代表可证明的历史原值；找不到同名 Recipe 时保持为空。" +
            Environment.NewLine +
            "逐条回读校验后才会清空旧 ItemsJson 并执行 VACUUM。迁移过程中请勿执行 KB 测试，旧版插件不能读取迁移后的明细。";

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            var tableInfo = new DatabaseCleanupTableInfo
            {
                TableName = CleanupTableName,
                Exists = File.Exists(ViewResultManager.SqliteDbPath),
            };

            if (!tableInfo.Exists)
                return new[] { tableInfo };

            using var db = CreateDbClient();
            tableInfo.RowCount = db.Queryable<KBItemMaster>().Count();
            tableInfo.SizeBytes = new FileInfo(ViewResultManager.SqliteDbPath).Length;
            return new[] { tableInfo };
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths)
        {
            return KBResultPayloadStorage.RunDatabaseMaintenance(() => CleanupHistoryCore(keepMonths));
        }

        private static DatabaseCleanupExecutionResult CleanupHistoryCore(int keepMonths)
        {
            if (!File.Exists(ViewResultManager.SqliteDbPath))
            {
                return new DatabaseCleanupExecutionResult
                {
                    StatusMessage = "KB SQLite 数据库文件不存在。"
                };
            }

            DateTime cutoffDate = DateTime.Now.AddMonths(-keepMonths);
            using var db = CreateDbClient();
            int deletedRows = db.Deleteable<KBItemMaster>().Where(item => item.CreateTime < cutoffDate).ExecuteCommand();
            int deletedSessions = db.Deleteable<KBProductionSession>()
                .Where(item => item.StartTime < cutoffDate)
                .ExecuteCommand();
            TryVacuum(db);
            RefreshKbWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = $"已保留最近 {keepMonths} 个月的 KB 数据。"
            };
            result.SummaryLines.Add($"{CleanupTableName}: 删除 {deletedRows:N0} 行");
            result.SummaryLines.Add($"{SessionTableName}: 删除 {deletedSessions:N0} 个历史会话");
            return result;
        }

        public DatabaseCleanupExecutionResult CleanupAll()
        {
            return KBResultPayloadStorage.RunDatabaseMaintenance(CleanupAllCore);
        }

        private static DatabaseCleanupExecutionResult CleanupAllCore()
        {
            if (!File.Exists(ViewResultManager.SqliteDbPath))
            {
                return new DatabaseCleanupExecutionResult
                {
                    StatusMessage = "KB SQLite 数据库文件不存在。"
                };
            }

            using var db = CreateDbClient();
            int deletedRows = db.Deleteable<KBItemMaster>().ExecuteCommand();
            DateTime today = DateTime.Today;
            int deletedSessions = db.Deleteable<KBProductionSession>()
                .Where(item => item.EndTime != null || item.StartTime < today)
                .ExecuteCommand();
            TryResetIdentity(db);
            TryVacuum(db);
            RefreshKbWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = "已清空 KB SQLite 结果表。"
            };
            result.SummaryLines.Add($"{CleanupTableName}: 删除 {deletedRows:N0} 行");
            result.SummaryLines.Add($"{SessionTableName}: 删除 {deletedSessions:N0} 个已结束会话，当前会话保留");
            return result;
        }

        public DatabaseCleanupBackupResult CreateBackup()
        {
            return KBResultPayloadStorage.RunDatabaseMaintenance(CreateBackupCore);
        }

        private static DatabaseCleanupBackupResult CreateBackupCore()
        {
            string databasePath = ViewResultManager.SqliteDbPath;
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("KB SQLite 数据库文件不存在。", databasePath);

            string databaseDirectory = Path.GetDirectoryName(databasePath)
                ?? throw new InvalidOperationException("无法确定 KB SQLite 数据库目录。");
            string backupDirectory = Path.Combine(databaseDirectory, "ProjectKB.Backups");
            Directory.CreateDirectory(backupDirectory);
            string backupPath = Path.Combine(
                backupDirectory,
                $"ProjectKB.backup-{DateTime.Now:yyyyMMdd_HHmmss_fff}.db");

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
                    StatusMessage = $"已创建 KB 完整备份（{FormatSize(new FileInfo(backupPath).Length)}）。",
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

            return KBResultPayloadStorage.RunDatabaseMaintenance(() =>
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
            return KBResultPayloadStorage.RunDatabaseMaintenance(ExecuteMigrationCore);
        }

        private static DatabaseCleanupExecutionResult ExecuteMigrationCore()
        {
            IReadOnlyDictionary<string, KBRecipeConfig> recipes = RecipeManager.GetInstance().RecipeConfigs;
            LegacyKbResultMigrationReport report = LegacyKbResultMigration.Execute(ViewResultManager.SqliteDbPath, recipes);
            RefreshKbWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = report.ItemsRowsMigrated == 0 && report.ResidualItemsRowsCleared == 0 && report.RecipeSnapshotsRebuilt == 0
                    ? "未发现待迁移的旧结果，已完成数据库压缩整理。"
                    : $"已迁移 {report.ItemsRowsMigrated:N0} 条历史结果，并重建 {report.RecipeSnapshotsRebuilt:N0} 条 Recipe 快照。"
            };
            result.SummaryLines.Add($"{CleanupTableName}: 迁移 {report.ItemsRowsMigrated:N0} 条，清理残留旧字段 {report.ResidualItemsRowsCleared:N0} 条");
            result.SummaryLines.Add($"Recipe 快照: 重建 {report.RecipeSnapshotsRebuilt:N0} 条，无同名 Recipe {report.RecipeSnapshotsUnavailable:N0} 条");
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
                db.Ado.ExecuteCommand($"DELETE FROM sqlite_sequence WHERE name = '{CleanupTableName}';");
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

        private static void RefreshKbWindowIfOpen()
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
