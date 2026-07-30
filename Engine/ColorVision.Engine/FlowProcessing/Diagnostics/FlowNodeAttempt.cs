using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowNodeAttempt")]
    [SugarIndex(
        "idx_flow_node_attempt_run_node",
        nameof(RunRecordId),
        OrderByType.Asc,
        nameof(NodeId),
        OrderByType.Asc)]
    [SugarIndex(
        "ux_flow_node_attempt_run_invocation",
        nameof(RunRecordId),
        OrderByType.Asc,
        nameof(InvocationId),
        OrderByType.Asc,
        true)]
    public sealed class FlowNodeAttempt
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "INTEGER", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "run_record_id")]
        public int RunRecordId { get; set; }

        [SugarColumn(ColumnName = "legacy_node_record_id", IsNullable = true)]
        public int? LegacyNodeRecordId { get; set; }

        [SugarColumn(ColumnName = "node_id")]
        public string NodeId { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "attempt_no")]
        public int AttemptNo { get; set; }

        [SugarColumn(ColumnName = "invocation_id")]
        public string InvocationId { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "started_time_utc")]
        public DateTime StartedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "completed_time_utc", IsNullable = true)]
        public DateTime? CompletedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "outcome", IsNullable = true)]
        public string? Outcome { get; set; }

        [SugarColumn(ColumnName = "error_code", IsNullable = true)]
        public string? ErrorCode { get; set; }

        [SugarColumn(ColumnName = "error_message", IsNullable = true)]
        public string? ErrorMessage { get; set; }
    }
}
