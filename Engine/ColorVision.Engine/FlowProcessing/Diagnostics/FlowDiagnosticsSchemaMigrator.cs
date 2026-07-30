using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    internal static class FlowDiagnosticsSchemaMigrator
    {
        public static void EnsureSchema(SqlSugarClient db)
        {
            ArgumentNullException.ThrowIfNull(db);

            if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
            {
                // The legacy node writer and the execution journal use separate
                // connections to the same local database. WAL plus a bounded
                // busy timeout prevents short write bursts from failing
                // immediately with SQLITE_BUSY.
                db.Ado.ExecuteCommand("PRAGMA busy_timeout = 5000;");
                db.Ado.ExecuteCommand("PRAGMA journal_mode = WAL;");
            }

            db.CodeFirst.InitTables<FlowNodeRecord>();
            db.CodeFirst.InitTables<FlowNodeMessage>();
            db.CodeFirst.InitTables<FlowRunRecord>();
            db.CodeFirst.InitTables<FlowTemplateSnapshot>();
            db.CodeFirst.InitTables<FlowExecutionEvent>();
            db.CodeFirst.InitTables<FlowNodeAttempt>();
            db.CodeFirst.InitTables<FlowIncident>();

            if (db.CurrentConnectionConfig.DbType == DbType.Sqlite)
            {
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
