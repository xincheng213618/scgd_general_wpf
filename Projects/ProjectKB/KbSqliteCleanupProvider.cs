using ColorVision.Database;
using ProjectKB.PluginConfig;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ProjectKB
{
    public sealed class KbSqliteCleanupProvider : IDatabaseCleanupSourceProvider
    {
        private const string CleanupTableName = "KBItemMaster";
        private const string SessionTableName = "KBProductionSession";

        public string Id => "projectkb-sqlite";
        public string DisplayName => "KB SQLite";
        public string Description => $"数据库文件: {ViewResultManager.SqliteDbPath}";
        public int Order => 20;

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

        private static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={ViewResultManager.SqliteDbPath}",
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

            Application.Current.Dispatcher.Invoke(() =>
            {
                var manager = ViewResultManager.GetInstance();
                manager.LoadAll(manager.Config.Count);
            });
        }
    }
}
