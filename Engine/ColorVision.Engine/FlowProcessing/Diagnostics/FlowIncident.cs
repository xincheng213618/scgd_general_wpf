using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowIncident")]
    [SugarIndex(
        "idx_flow_incident_run_state",
        nameof(RunRecordId),
        OrderByType.Asc,
        nameof(State),
        OrderByType.Asc)]
    [SugarIndex(
        "ux_flow_incident_run_key",
        nameof(RunRecordId),
        OrderByType.Asc,
        nameof(IncidentKey),
        OrderByType.Asc,
        true)]
    public sealed class FlowIncident
    {
        [SugarColumn(ColumnName = "id", ColumnDataType = "INTEGER", IsPrimaryKey = true, IsIdentity = true)]
        public long Id { get; set; }

        [SugarColumn(ColumnName = "run_record_id")]
        public int RunRecordId { get; set; }

        [SugarColumn(ColumnName = "incident_key", IsNullable = true)]
        public string? IncidentKey { get; set; }

        [SugarColumn(ColumnName = "attempt_id", IsNullable = true)]
        public long? AttemptId { get; set; }

        [SugarColumn(ColumnName = "node_id", IsNullable = true)]
        public string? NodeId { get; set; }

        [SugarColumn(ColumnName = "kind")]
        public string Kind { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "severity")]
        public string Severity { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "state")]
        public string State { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "summary")]
        public string Summary { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "details_json", IsNullable = true)]
        public string? DetailsJson { get; set; }

        [SugarColumn(ColumnName = "detected_time_utc")]
        public DateTime DetectedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "acknowledged_time_utc", IsNullable = true)]
        public DateTime? AcknowledgedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "acknowledged_operator", IsNullable = true)]
        public string? AcknowledgedOperator { get; set; }

        [SugarColumn(ColumnName = "acknowledgment_note", IsNullable = true)]
        public string? AcknowledgmentNote { get; set; }

        [SugarColumn(ColumnName = "resolved_time_utc", IsNullable = true)]
        public DateTime? ResolvedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "resolution", IsNullable = true)]
        public string? Resolution { get; set; }

        [SugarColumn(ColumnName = "operator_name", IsNullable = true)]
        public string? OperatorName { get; set; }
    }
}
