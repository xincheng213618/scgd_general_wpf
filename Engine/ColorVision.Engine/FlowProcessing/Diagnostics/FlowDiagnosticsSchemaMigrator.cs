using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal static class FlowDiagnosticsSchemaMigrator
    {
        private const string ConfigureBusyTimeout = "PRAGMA busy_timeout = 5000;";

        internal static void PrepareServiceConnection(SqlSugarClient db, bool useLocalWriterSchema)
        {
            // The node writer keeps this SQLite database open for the process lifetime.
            // Its schema version lets readers avoid CodeFirst and its writer transaction.
            if (useLocalWriterSchema && FlowNodeRecordDataBaseHelper.HasPreparedSchema(db))
            {
                db.Ado.ExecuteCommand(ConfigureBusyTimeout);
                return;
            }

            FlowDiagnosticsMaintenanceGate.RunExclusive(() =>
            {
                EnsureSchema(db);
                if (useLocalWriterSchema)
                    FlowNodeRecordDataBaseHelper.RefreshPreparedSchema(db);
            });
        }

        public static void EnsureSchema(SqlSugarClient db)
        {
            ArgumentNullException.ThrowIfNull(db);

            if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
            {
                // The legacy node writer and the execution journal use separate
                // connections to the same local database. WAL plus a bounded
                // busy timeout prevents short write bursts from failing
                // immediately with SQLITE_BUSY.
                db.Ado.ExecuteCommand(ConfigureBusyTimeout);
                db.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
            }

            db.CodeFirst.InitTables<FlowNodeRecord>();
            db.CodeFirst.InitTables<FlowNodeMessage>();
            db.CodeFirst.InitTables<FlowRunRecord>();
            db.CodeFirst.InitTables<FlowTemplateSnapshot>();
            db.CodeFirst.InitTables<FlowExecutionEvent>();
            db.CodeFirst.InitTables<FlowNodeAttempt>();
            db.CodeFirst.InitTables<FlowIncident>();
            FlowNodeMessagePayloadStorage.EnsureSchema(db);

            if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
            {
                db.Ado.ExecuteCommand($"""
                    CREATE INDEX IF NOT EXISTS idx_flow_run_normalized_key_id
                    ON FlowRunRecord ({FlowRunIdentitySql.TrimmedKey("flow_key")} COLLATE BINARY, id DESC);
                    """);
                // Only records without a batch use the exact-serial fallback.
                db.Ado.ExecuteCommand("""
                    CREATE INDEX IF NOT EXISTS idx_flow_run_legacy_serial
                    ON FlowRunRecord (serial_number COLLATE BINARY)
                    WHERE batch_id IS NULL AND serial_number IS NOT NULL;
                    """);
                db.Ado.ExecuteCommand("""
                    CREATE INDEX IF NOT EXISTS idx_flow_node_record_completed_end
                    ON FlowNodeRecord (node_id, end_time DESC)
                    WHERE end_time IS NOT NULL AND elapsed_ms > 0;
                    """);
                // Keep the existing run/node index for reading attempts in ID order.
                db.Ado.ExecuteCommand("""
                    CREATE INDEX IF NOT EXISTS idx_flow_node_attempt_run_node_number
                    ON FlowNodeAttempt (run_record_id, node_id, attempt_no DESC);
                    """);
                // Match incident page order and check state before loading the full row.
                db.Ado.ExecuteCommand("""
                    CREATE INDEX IF NOT EXISTS idx_flow_incident_detected_id_state
                    ON FlowIncident (detected_time_utc DESC, id DESC, state);
                    """);
                // Histories pick a node across multiple batches. Leave time-only reads on their original indexes.
                db.Ado.ExecuteCommand("""
                    CREATE INDEX IF NOT EXISTS idx_flow_node_message_batch_node
                    ON FlowNodeMessage (batch_id, node_id)
                    WHERE batch_id IS NOT NULL AND node_id IS NOT NULL;
                    """);

                // FlowParam.Id is reorderable, so the old TemplateId+hash
                // uniqueness rule cannot remain authoritative once FlowKey is
                // available. The non-unique lookup index is created by
                // CodeFirst; fallback de-duplication is serialized by the
                // journal write lock.
                db.Ado.ExecuteCommand(
                    "DROP INDEX IF EXISTS ux_flow_template_snapshot_template_hash;");
            }
        }
    }
}
