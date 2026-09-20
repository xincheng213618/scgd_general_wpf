using ColorVision.Database;
using ColorVision.UI;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    public sealed class FlowDiagnosticsFeedbackCollector : SqliteFeedbackCollector
    {
        public override string Name => "流程与节点耗时记录";
        public override string Description => "流程、节点耗时、完整收发消息及关联诊断，用于分析取图和算法耗时";
        public override int Order => 33;
        protected override string DatabasePath => ConfigService.Instance.GetRequiredService<FlowNodeRecordConfig>().SqliteDbPath;
        protected override string DatabaseFileName => "FlowNodeRecords.db";

        protected override void ExportTables(SqliteFeedbackExport export)
        {
            string recentRuns = export.Recent("FlowRunRecord", "src", ["completed_time"],
                "started_time_utc", "completed_time_utc", "last_heartbeat_utc", "recovered_time_utc");
            foreach (string table in new[] { "FlowNodeRecord", "FlowNodeMessage" })
            {
                if (!export.HasTable(table)) continue;
                string recent = table == "FlowNodeRecord"
                    ? export.Recent(table, "child", ["start_time", "end_time"])
                    : export.Recent(table, "child", ["send_time", "recv_time"]);
                recentRuns += $" OR EXISTS (SELECT 1 FROM source.\"{table}\" child WHERE child.batch_id = src.batch_id AND child.serial_number IS src.serial_number AND {recent})";
            }
            foreach (var (table, times) in new[]
            {
                ("FlowExecutionEvent", new[] { "occurred_time_utc" }),
                ("FlowNodeAttempt", new[] { "started_time_utc", "completed_time_utc" }),
                ("FlowIncident", new[] { "detected_time_utc", "acknowledged_time_utc", "resolved_time_utc" })
            })
            {
                if (export.HasTable(table))
                    recentRuns += $" OR EXISTS (SELECT 1 FROM source.\"{table}\" child WHERE child.run_record_id = src.id AND {export.Recent(table, "child", [], times)})";
            }
            export.ExportTable("FlowRunRecord", recentRuns);

            string linkedRun = export.HasTable("FlowRunRecord")
                ? "src.run_record_id IN (SELECT id FROM main.FlowRunRecord)" : "0";
            export.ExportTable("FlowExecutionEvent", linkedRun);
            export.ExportTable("FlowNodeAttempt", linkedRun);
            export.ExportTable("FlowIncident", linkedRun);

            string linkedBatch = export.HasTable("FlowRunRecord")
                ? "EXISTS (SELECT 1 FROM main.FlowRunRecord run WHERE run.batch_id = src.batch_id AND run.serial_number IS src.serial_number)" : "0";
            string nodes = export.Recent("FlowNodeRecord", "src", ["start_time", "end_time"]) + " OR " + linkedBatch;
            if (export.HasColumn("FlowNodeAttempt", "legacy_node_record_id"))
                nodes += " OR src.id IN (SELECT legacy_node_record_id FROM main.FlowNodeAttempt)";
            // A message received near the boundary may belong to a node that started before the range.
            if (export.HasColumn("FlowNodeMessage", "node_record_id"))
                nodes += " OR src.id IN (SELECT child.node_record_id FROM source.FlowNodeMessage child WHERE "
                    + export.Recent("FlowNodeMessage", "child", ["send_time", "recv_time"]) + ")";
            export.ExportTable("FlowNodeRecord", nodes);

            string messages = export.Recent("FlowNodeMessage", "src", ["send_time", "recv_time"]) + " OR " + linkedBatch;
            if (export.HasTable("FlowNodeRecord") && export.HasColumn("FlowNodeMessage", "node_record_id"))
                messages += " OR src.node_record_id IN (SELECT id FROM main.FlowNodeRecord)";
            export.ExportTable("FlowNodeMessage", messages);
            export.ExportTable("FlowTemplateSnapshot", export.HasColumn("FlowRunRecord", "snapshot_id")
                ? "src.id IN (SELECT snapshot_id FROM main.FlowRunRecord)" : "0");
        }
    }
}
