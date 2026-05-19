using ColorVision.Database;
using ProjectARVRPro.PluginConfig;
using SqlSugar;
using System.IO;
using System.Windows;

namespace ProjectARVRPro
{
    public sealed class ArvrSqliteCleanupProvider : IDatabaseCleanupSourceProvider
    {
        private const string CleanupTableName = "ARVRReuslt";

        public string Id => "projectarvrpro-sqlite";
        public string DisplayName => "ARVRPro SQLite";
        public string Description => $"数据库文件: {ViewResultManager.SqliteDbPath}";
        public int Order => 21;

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
            tableInfo.RowCount = db.Queryable<ProjectARVRReuslt>().Count();
            tableInfo.SizeBytes = new FileInfo(ViewResultManager.SqliteDbPath).Length;
            return new[] { tableInfo };
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths)
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
            int deletedRows = db.Deleteable<ProjectARVRReuslt>().Where(item => item.CreateTime < cutoffDate).ExecuteCommand();
            TryVacuum(db);
            RefreshArvrWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = $"已保留最近 {keepMonths} 个月的 ARVRPro 数据。"
            };
            result.SummaryLines.Add($"{CleanupTableName}: 删除 {deletedRows:N0} 行");
            return result;
        }

        public DatabaseCleanupExecutionResult CleanupAll()
        {
            if (!File.Exists(ViewResultManager.SqliteDbPath))
            {
                return new DatabaseCleanupExecutionResult
                {
                    StatusMessage = "ARVRPro SQLite 数据库文件不存在。"
                };
            }

            using var db = CreateDbClient();
            int deletedRows = db.Deleteable<ProjectARVRReuslt>().ExecuteCommand();
            TryResetIdentity(db);
            TryVacuum(db);
            RefreshArvrWindowIfOpen();

            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = "已清空 ARVRPro SQLite 结果表。"
            };
            result.SummaryLines.Add($"{CleanupTableName}: 删除 {deletedRows:N0} 行");
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

        private static void RefreshArvrWindowIfOpen()
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