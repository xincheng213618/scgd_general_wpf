using ColorVision.Database;
using ColorVision.UI;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    public sealed class FlowDiagnosticsSqliteCleanupProvider :
        IDatabaseCleanupSourceProvider,
        IDatabaseCleanupBackupProvider,
        IDatabaseCleanupMaintenanceProvider,
        IDatabaseCleanupMigrationProvider
    {
        private static readonly string[] TableNames =
        [
            "FlowNodeMessage",
            "FlowNodeRecord",
            "FlowRunRecord",
            "FlowExecutionEvent",
            "FlowNodeAttempt",
            "FlowIncident",
            "FlowTemplateSnapshot",
        ];

        public string Id => "flow-diagnostics-sqlite";
        public string DisplayName => "流程诊断 SQLite";
        public string Description => $"数据库文件: {GetDatabasePath()}";
        public int Order => 22;
        public string MigrationActionName => "迁移并压缩流程消息";
        public string MigrationConfirmationMessage =>
            "将把 FlowNodeMessage 中旧的收发 TEXT Payload 迁移为同表 GZip BLOB，" +
            "逐条校验后清空旧字段并执行 VACUUM 释放空间。" + Environment.NewLine +
            "迁移后旧版程序不能读取这些历史 Payload；迁移期间请停止流程执行并关闭流程分析窗口。";

        public IReadOnlyList<DatabaseCleanupTableInfo> LoadTables()
        {
            string databasePath = GetDatabasePath();
            var result = new List<DatabaseCleanupTableInfo>(TableNames.Length);
            if (!File.Exists(databasePath))
            {
                foreach (string tableName in TableNames)
                    result.Add(new DatabaseCleanupTableInfo { TableName = tableName });
                return result;
            }

            using var db = CreateDb();
            long databaseBytes = SqliteFileMaintenance.GetTotalStorageBytes(databasePath);
            foreach (string tableName in TableNames)
            {
                bool exists = db.DbMaintenance.IsAnyTable(tableName, false);
                result.Add(new DatabaseCleanupTableInfo
                {
                    TableName = tableName,
                    Exists = exists,
                    RowCount = exists
                        ? Convert.ToInt64(db.Ado.GetScalar($"SELECT COUNT(*) FROM \"{tableName}\";"))
                        : 0,
                    SizeBytes = result.Count == 0 ? databaseBytes : 0,
                });
            }
            return result;
        }

        public DatabaseCleanupExecutionResult CleanupHistory(int keepMonths)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepMonths);
            return RunMaintenance(() => CleanupHistoryCore(
                keepMonths,
                GetDatabasePath(),
                DateTime.Now));
        }

        public DatabaseCleanupExecutionResult CleanupAll()
        {
            return RunMaintenance(CleanupAllCore);
        }

        public DatabaseCleanupBackupResult CreateBackup()
        {
            return RunMaintenance(CreateBackupCore);
        }

        public DatabaseCleanupMaintenanceResult ExecuteCleanupWithBackup(
            Func<DatabaseCleanupExecutionResult> cleanupAction)
        {
            ArgumentNullException.ThrowIfNull(cleanupAction);
            return RunMaintenance(() =>
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
            return RunMaintenance(ExecuteMigrationCore);
        }

        internal static DatabaseCleanupExecutionResult CleanupHistoryCore(
            int keepMonths,
            string databasePath,
            DateTime localNow)
        {
            ArgumentOutOfRangeException.ThrowIfNegativeOrZero(keepMonths);
            ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);
            EnsureDatabaseExists(databasePath);
            DateTime localCutoff = localNow.AddMonths(-keepMonths);

            int eventCount;
            int incidentCount;
            int attemptCount;
            int runCount;
            int messageCount;
            int recordCount;
            int snapshotCount;
            using (var db = CreateDb(databasePath))
            {
                FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
                SugarParameter[] CreateOldRunParameters() =>
                [
                    new SugarParameter("@cutoff", localCutoff),
                    new SugarParameter("@running", (int)FlowStatus.Runing),
                    new SugarParameter("@resolved", FlowIncidentStates.Resolved),
                ];
                SugarParameter[] CreateLegacyCleanupParameters() =>
                    CreateOldRunParameters();
                static string HasProtectedRun(string legacyAlias) =>
                    "EXISTS (SELECT 1 FROM \"FlowRunRecord\" AS protected_run " +
                    $"WHERE protected_run.\"batch_id\" = {legacyAlias}.\"batch_id\" " +
                    "AND (protected_run.\"serial_number\" = " +
                    $"{legacyAlias}.\"serial_number\" OR " +
                    "(protected_run.\"serial_number\" IS NULL AND " +
                    $"{legacyAlias}.\"serial_number\" IS NULL)) " +
                    "AND (protected_run.\"status\" = @running " +
                    "OR EXISTS (SELECT 1 FROM \"FlowIncident\" AS protected_incident " +
                    "WHERE protected_incident.\"run_record_id\" = protected_run.\"id\" " +
                    "AND (protected_incident.\"state\" <> @resolved " +
                    "OR protected_incident.\"resolved_time_utc\" IS NULL))))";
                db.Ado.BeginTran();
                try
                {
                    string oldRuns =
                        "SELECT run.\"id\" FROM \"FlowRunRecord\" AS run " +
                        "WHERE run.\"status\" <> @running " +
                        "AND run.\"completed_time\" < @cutoff " +
                        "AND NOT EXISTS (SELECT 1 FROM \"FlowIncident\" AS incident " +
                        "WHERE incident.\"run_record_id\" = run.\"id\" " +
                        "AND (incident.\"state\" <> @resolved " +
                        "OR incident.\"resolved_time_utc\" IS NULL))";
                    eventCount = db.Ado.ExecuteCommand(
                        $"DELETE FROM \"FlowExecutionEvent\" WHERE \"run_record_id\" IN ({oldRuns});",
                        CreateOldRunParameters());
                    incidentCount = db.Ado.ExecuteCommand(
                        $"DELETE FROM \"FlowIncident\" WHERE \"run_record_id\" IN ({oldRuns});",
                        CreateOldRunParameters());
                    attemptCount = db.Ado.ExecuteCommand(
                        $"DELETE FROM \"FlowNodeAttempt\" WHERE \"run_record_id\" IN ({oldRuns});",
                        CreateOldRunParameters());
                    runCount = db.Ado.ExecuteCommand(
                        $"DELETE FROM \"FlowRunRecord\" WHERE \"id\" IN ({oldRuns});",
                        CreateOldRunParameters());

                    string deletableLegacyRecords =
                        "SELECT legacy_record.\"id\" FROM \"FlowNodeRecord\" AS legacy_record " +
                        "WHERE legacy_record.\"end_time\" IS NOT NULL " +
                        "AND legacy_record.\"end_time\" < @cutoff " +
                        $"AND NOT {HasProtectedRun("legacy_record")}";
                    messageCount = db.Ado.ExecuteCommand(
                        "DELETE FROM \"FlowNodeMessage\" AS legacy_message WHERE " +
                        "legacy_message.\"node_record_id\" IN (" +
                        deletableLegacyRecords + ") OR " +
                        "(legacy_message.\"node_record_id\" IS NULL " +
                        "AND legacy_message.\"recv_time\" IS NOT NULL " +
                        "AND legacy_message.\"recv_time\" < @cutoff " +
                        $"AND NOT {HasProtectedRun("legacy_message")});",
                        CreateLegacyCleanupParameters());
                    recordCount = db.Ado.ExecuteCommand(
                        "DELETE FROM \"FlowNodeRecord\" WHERE \"id\" IN (" +
                        deletableLegacyRecords + ");",
                        CreateLegacyCleanupParameters());
                    snapshotCount = db.Ado.ExecuteCommand(
                        "DELETE FROM \"FlowTemplateSnapshot\" WHERE \"id\" NOT IN " +
                        "(SELECT DISTINCT \"snapshot_id\" FROM \"FlowRunRecord\" " +
                        "WHERE \"snapshot_id\" IS NOT NULL);");
                    db.Ado.CommitTran();
                }
                catch
                {
                    TryRollback(db);
                    throw;
                }
            }

            SqliteVacuumResult vacuum =
                SqliteFileMaintenance.VacuumAndCheck(databasePath);
            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = $"已保留最近 {keepMonths} 个月的流程诊断数据。",
            };
            AddCleanupSummary(
                result,
                eventCount,
                incidentCount,
                attemptCount,
                runCount,
                messageCount,
                recordCount,
                snapshotCount,
                vacuum);
            return result;
        }

        private static DatabaseCleanupExecutionResult CleanupAllCore()
        {
            string databasePath = GetDatabasePath();
            EnsureDatabaseExists(databasePath);
            int eventCount;
            int incidentCount;
            int attemptCount;
            int runCount;
            int messageCount;
            int recordCount;
            int snapshotCount;

            using (var db = CreateDb())
            {
                FlowDiagnosticsSchemaMigrator.EnsureSchema(db);
                db.Ado.BeginTran();
                try
                {
                    eventCount = db.Deleteable<FlowExecutionEvent>().ExecuteCommand();
                    incidentCount = db.Deleteable<FlowIncident>().ExecuteCommand();
                    attemptCount = db.Deleteable<FlowNodeAttempt>().ExecuteCommand();
                    runCount = db.Deleteable<FlowRunRecord>().ExecuteCommand();
                    messageCount = db.Deleteable<FlowNodeMessage>().ExecuteCommand();
                    recordCount = db.Deleteable<FlowNodeRecord>().ExecuteCommand();
                    snapshotCount = db.Deleteable<FlowTemplateSnapshot>().ExecuteCommand();
                    db.Ado.ExecuteCommand(
                        "DELETE FROM sqlite_sequence WHERE name IN " +
                        "('FlowExecutionEvent','FlowIncident','FlowNodeAttempt'," +
                        "'FlowRunRecord','FlowNodeMessage','FlowNodeRecord','FlowTemplateSnapshot');");
                    db.Ado.CommitTran();
                }
                catch
                {
                    TryRollback(db);
                    throw;
                }
            }

            SqliteVacuumResult vacuum =
                SqliteFileMaintenance.VacuumAndCheck(databasePath);
            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = "已清空流程诊断 SQLite 数据并释放空间。",
            };
            AddCleanupSummary(
                result,
                eventCount,
                incidentCount,
                attemptCount,
                runCount,
                messageCount,
                recordCount,
                snapshotCount,
                vacuum);
            return result;
        }

        private static DatabaseCleanupBackupResult CreateBackupCore()
        {
            string databasePath = GetDatabasePath();
            SqliteBackupFileResult backup =
                SqliteFileMaintenance.CreateVerifiedBackup(
                    databasePath,
                    "FlowNodeRecords.Backups",
                    "FlowNodeRecords");
            return new DatabaseCleanupBackupResult
            {
                FilePath = backup.FilePath,
                StatusMessage =
                    $"已创建流程诊断完整备份（{SqliteFileMaintenance.FormatSize(backup.FileSizeBytes)}）。",
            };
        }

        private static DatabaseCleanupExecutionResult ExecuteMigrationCore()
        {
            string databasePath = GetDatabasePath();
            SqliteGzipTextMigrationReport report =
                LegacyFlowNodeMessagePayloadMigration.Execute(databasePath);
            SqliteGzipTextMigrationTableReport send = report.Tables[0];
            SqliteGzipTextMigrationTableReport recv = report.Tables[1];
            int migrated = send.MigratedRows + recv.MigratedRows;
            int residual = send.ResidualRowsCleared + recv.ResidualRowsCleared;
            var result = new DatabaseCleanupExecutionResult
            {
                StatusMessage = migrated == 0 && residual == 0
                    ? "未发现待迁移的旧流程消息，已完成数据库压缩整理。"
                    : $"已迁移 {migrated:N0} 个流程消息 Payload 并释放空间。",
            };
            result.SummaryLines.Add(
                $"发送 Payload：迁移 {send.MigratedRows:N0} 个，清理旧字段 {send.ResidualRowsCleared:N0} 个");
            result.SummaryLines.Add(
                $"接收 Payload：迁移 {recv.MigratedRows:N0} 个，清理旧字段 {recv.ResidualRowsCleared:N0} 个");
            result.SummaryLines.Add(
                $"数据库大小：{SqliteFileMaintenance.FormatSize(report.BeforeBytes)} → " +
                SqliteFileMaintenance.FormatSize(report.AfterBytes));
            result.SummaryLines.Add($"SQLite 完整性检查：{report.IntegrityCheck}");
            return result;
        }

        private static T RunMaintenance<T>(Func<T> action)
        {
            ArgumentNullException.ThrowIfNull(action);
            if (Monitor.IsEntered(FlowDiagnosticsMaintenanceGate.SyncRoot))
                return action();
            if (!FlowNodeRecordDataBaseHelper.FlushPendingWrites(TimeSpan.FromSeconds(10)))
                throw new TimeoutException("等待流程诊断记录写入完成超时，请停止流程后重试。");
            return FlowDiagnosticsMaintenanceGate.RunExclusive(action);
        }

        private static void AddCleanupSummary(
            DatabaseCleanupExecutionResult result,
            int eventCount,
            int incidentCount,
            int attemptCount,
            int runCount,
            int messageCount,
            int recordCount,
            int snapshotCount,
            SqliteVacuumResult vacuum)
        {
            result.SummaryLines.Add($"FlowNodeMessage: 删除 {messageCount:N0} 行");
            result.SummaryLines.Add($"FlowNodeRecord: 删除 {recordCount:N0} 行");
            result.SummaryLines.Add($"FlowRunRecord: 删除 {runCount:N0} 行");
            result.SummaryLines.Add($"FlowExecutionEvent: 删除 {eventCount:N0} 行");
            result.SummaryLines.Add($"FlowNodeAttempt: 删除 {attemptCount:N0} 行");
            result.SummaryLines.Add($"FlowIncident: 删除 {incidentCount:N0} 行");
            result.SummaryLines.Add($"FlowTemplateSnapshot: 删除 {snapshotCount:N0} 行");
            result.SummaryLines.Add(
                $"数据库大小: {SqliteFileMaintenance.FormatSize(vacuum.BeforeBytes)} → " +
                SqliteFileMaintenance.FormatSize(vacuum.AfterBytes));
            result.SummaryLines.Add($"SQLite 完整性检查: {vacuum.IntegrityCheck}");
        }

        private static SqlSugarClient CreateDb()
        {
            return CreateDb(GetDatabasePath());
        }

        private static SqlSugarClient CreateDb(string databasePath)
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={databasePath};Default Timeout=30",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true,
                InitKeyType = InitKeyType.Attribute,
            });
        }

        private static string GetDatabasePath()
        {
            return ConfigService.Instance
                .GetRequiredService<FlowNodeRecordConfig>()
                .SqliteDbPath;
        }

        private static void EnsureDatabaseExists(string databasePath)
        {
            if (!File.Exists(databasePath))
                throw new FileNotFoundException("流程诊断 SQLite 数据库不存在。", databasePath);
        }

        private static void TryRollback(SqlSugarClient db)
        {
            try
            {
                db.Ado.RollbackTran();
            }
            catch
            {
            }
        }
    }
}
