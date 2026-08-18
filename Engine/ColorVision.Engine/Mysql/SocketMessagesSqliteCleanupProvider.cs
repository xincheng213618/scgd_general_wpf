using ColorVision.SocketProtocol;
using ColorVision.Engine;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision.Database
{
    /// <summary>
    /// Socket 消息库在统一“数据清理”窗口中的维护入口。
    /// </summary>
    public sealed class SocketMessagesSqliteCleanupProvider :
        IDatabaseCleanupSourceProvider,
        IDatabaseCleanupBackupProvider,
        IDatabaseCleanupMaintenanceProvider,
        IDatabaseCleanupMigrationProvider
    {
        public string Id => "socketmessages-sqlite";
        public string DisplayName => EngineLocalization.Get("Socket 消息 SQLite");
        public string Description => EngineLocalization.Format($"数据库文件: {SocketMessageManager.SqliteDbPath}");
        public int Order => 22;
        public string MigrationActionName => EngineLocalization.Get("迁移并压缩历史消息");
        public string MigrationConfirmationMessage =>
            EngineLocalization.Get("将把 SocketMessage.Content 旧 TEXT 正文迁移为同表 GZip BLOB，生成列表预览，校验一致后清空旧字段并执行 VACUUM 释放空间。") + Environment.NewLine +
            EngineLocalization.Get("迁移后旧版程序不能读取这些历史正文；迁移期间 Socket 消息写入会暂时等待。");

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            string databasePath = SocketMessageManager.SqliteDbPath;
            var table = new DatabaseCleanupTableInfo
            {
                TableName = SocketMessagePayloadStorage.TableName,
                Exists = File.Exists(databasePath),
            };
            if (!table.Exists)
                return [table];

            SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                using SqlSugarClient db = CreateDbClient();
                table.RowCount = db.Queryable<SocketMessage>().Count();
                table.SizeBytes = SqliteFileMaintenance.GetTotalStorageBytes(databasePath);
            });
            return [table];
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepMonths);
            return SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                string databasePath = RequireDatabasePath();
                DateTime cutoff = DateTime.Now.AddMonths(-keepMonths);
                int deletedRows;
                using (SqlSugarClient db = CreateDbClient())
                    deletedRows = db.Deleteable<SocketMessage>().Where(item => item.MessageTime < cutoff).ExecuteCommand();

                SqliteVacuumResult vacuum = SqliteFileMaintenance.VacuumAndCheck(databasePath);
                RefreshLoadedMessages();
                var result = new DatabaseCleanupExecutionResult
                {
                    StatusMessage = EngineLocalization.Format($"已保留最近 {keepMonths} 个月的 Socket 消息。")
                };
                result.SummaryLines.Add(EngineLocalization.Format($"{SocketMessagePayloadStorage.TableName}: 删除 {deletedRows:N0} 行"));
                AddVacuumSummary(result, vacuum);
                return result;
            });
        }

        public DatabaseCleanupExecutionResult CleanupAll()
        {
            return SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                string databasePath = RequireDatabasePath();
                int deletedRows;
                using (SqlSugarClient db = CreateDbClient())
                {
                    db.Ado.BeginTran();
                    try
                    {
                        deletedRows = db.Deleteable<SocketMessage>().ExecuteCommand();
                        db.Ado.ExecuteCommand(
                            $"DELETE FROM sqlite_sequence WHERE name = '{SocketMessagePayloadStorage.TableName}';");
                        db.Ado.CommitTran();
                    }
                    catch
                    {
                        db.Ado.RollbackTran();
                        throw;
                    }
                }

                SqliteVacuumResult vacuum = SqliteFileMaintenance.VacuumAndCheck(databasePath);
                RefreshLoadedMessages();
                var result = new DatabaseCleanupExecutionResult
                {
                    StatusMessage = EngineLocalization.Get("已清空 Socket 消息数据库。")
                };
                result.SummaryLines.Add(EngineLocalization.Format($"{SocketMessagePayloadStorage.TableName}: 删除 {deletedRows:N0} 行"));
                AddVacuumSummary(result, vacuum);
                return result;
            });
        }

        public DatabaseCleanupBackupResult CreateBackup()
        {
            return SocketMessagePayloadStorage.RunDatabaseMaintenance(CreateBackupCore);
        }

        public DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(
            Func<DatabaseCleanupExecutionResult> cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(cleanupAction);
            return SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
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
                        ex.Message + Environment.NewLine + EngineLocalization.Format($"完整备份已保留：{backup.FilePath}"),
                        ex);
                }
            });
        }

        public DatabaseCleanupExecutionResult ExecuteMigration()
        {
            return SocketMessagePayloadStorage.RunDatabaseMaintenance(() =>
            {
                SqliteGzipTextMigrationReport report = LegacySocketMessageMigration.Execute(RequireDatabasePath());
                SqliteGzipTextMigrationTableReport table = report.Tables.Single();
                RefreshLoadedMessages();

                var result = new DatabaseCleanupExecutionResult
                {
                    StatusMessage = table.MigratedRows == 0 && table.ResidualRowsCleared == 0
                        ? EngineLocalization.Get("未发现待迁移的旧 Socket 正文，已完成数据库压缩整理。")
                        : EngineLocalization.Format($"已迁移 {table.MigratedRows:N0} 条历史 Socket 正文，并释放 SQLite 空闲空间。")
                };
                result.SummaryLines.Add(
                    EngineLocalization.Format($"{table.TableName}: 迁移 {table.MigratedRows:N0} 条，清理残留旧字段 {table.ResidualRowsCleared:N0} 条"));
                result.SummaryLines.Add(
                    EngineLocalization.Format($"数据库大小: {SqliteFileMaintenance.FormatSize(report.BeforeBytes)} → {SqliteFileMaintenance.FormatSize(report.AfterBytes)}"));
                result.SummaryLines.Add(EngineLocalization.Format($"SQLite 完整性检查: {report.IntegrityCheck}"));
                return result;
            });
        }

        private static DatabaseCleanupBackupResult CreateBackupCore()
        {
            SqliteBackupFileResult backup = SqliteFileMaintenance.CreateVerifiedBackup(
                RequireDatabasePath(),
                "SocketMessages.Backups",
                "SocketMessages");
            return new DatabaseCleanupBackupResult
            {
                StatusMessage =
                    EngineLocalization.Format($"已创建 Socket 消息完整备份（{SqliteFileMaintenance.FormatSize(backup.FileSizeBytes)}）。"),
                FilePath = backup.FilePath,
            };
        }

        private static SqlSugarClient CreateDbClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={SocketMessageManager.SqliteDbPath};Default Timeout=30",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private static string RequireDatabasePath()
        {
            string databasePath = SocketMessageManager.SqliteDbPath;
            if (!File.Exists(databasePath))
                throw new FileNotFoundException(EngineLocalization.Get("Socket 消息 SQLite 数据库文件不存在。"), databasePath);
            return databasePath;
        }

        private static void AddVacuumSummary(
            DatabaseCleanupExecutionResult result,
            SqliteVacuumResult vacuum)
        {
            result.SummaryLines.Add(
                EngineLocalization.Format($"数据库大小: {SqliteFileMaintenance.FormatSize(vacuum.BeforeBytes)} → {SqliteFileMaintenance.FormatSize(vacuum.AfterBytes)}"));
            result.SummaryLines.Add(EngineLocalization.Format($"SQLite 完整性检查: {vacuum.IntegrityCheck}"));
        }

        private static void RefreshLoadedMessages()
        {
            if (Application.Current?.Dispatcher == null)
                return;

            Application.Current.Dispatcher.BeginInvoke(() =>
            {
                SocketMessageManager manager = SocketMessageManager.GetInstance();
                manager.LoadAll(manager.Config.Count);
            });
        }
    }

    /// <summary>
    /// 一次性现场迁移适配器。所有现场完成迁移后可随本 provider 的迁移能力一起删除。
    /// </summary>
    internal static class LegacySocketMessageMigration
    {
        public static SqliteGzipTextMigrationReport Execute(string databasePath)
        {
            return SqliteGzipTextMigration.Execute(
                databasePath,
                [
                    new SqliteGzipTextMigrationSpec(
                        SocketMessagePayloadStorage.TableName,
                        SocketMessagePayloadStorage.IdColumnName,
                        SocketMessagePayloadStorage.LegacyContentColumnName,
                        SocketMessagePayloadStorage.GzipColumnName,
                        SocketMessagePayloadStorage.Utf8LengthColumnName,
                        SocketMessagePayloadStorage.PreviewColumnName,
                        SocketMessagePayloadStorage.PreviewCharacters)
                ]);
        }
    }
}
