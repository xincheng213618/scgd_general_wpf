using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowExecutionEvent")]
    [SugarIndex(
        "ux_flow_execution_event_run_sequence",
        nameof(RunRecordId),
        OrderByType.Asc,
        nameof(SequenceNo),
        OrderByType.Asc,
        true)]
    [SugarIndex(
        "ux_flow_execution_event_run_key",
        nameof(RunRecordId),
        OrderByType.Asc,
        nameof(EventKey),
        OrderByType.Asc,
        true)]
    public sealed class FlowExecutionEvent
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "INTEGER", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "run_record_id")]
        public int RunRecordId { get; set; }

        [SugarColumn(ColumnName = "sequence_no")]
        public long SequenceNo { get; set; }

        [SugarColumn(ColumnName = "event_key", IsNullable = true)]
        public string? EventKey { get; set; }

        [SugarColumn(ColumnName = "event_type")]
        public string EventType { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "occurred_time_utc")]
        public DateTime OccurredTimeUtc { get; set; }

        [SugarColumn(ColumnName = "node_id", IsNullable = true)]
        public string? NodeId { get; set; }

        [SugarColumn(ColumnName = "attempt_id", IsNullable = true)]
        public long? AttemptId { get; set; }

        [SugarColumn(ColumnName = "code", IsNullable = true)]
        public string? Code { get; set; }

        [SugarColumn(ColumnName = "message", IsNullable = true)]
        public string? Message { get; set; }

        [SugarColumn(ColumnName = "data_json", IsNullable = true)]
        public string? DataJson { get; set; }
    }
}
