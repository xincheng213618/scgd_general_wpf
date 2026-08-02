using SqlSugar;
using System;
using ColorVision.Engine.FlowProcessing.PostProcess;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowRunRecord")]
    [SugarIndex("idx_flow_run_template_completed", nameof(TemplateId), OrderByType.Asc, nameof(CompletedTime), OrderByType.Desc)]
    [SugarIndex("ux_flow_run_run_key", nameof(RunKey), OrderByType.Asc, true)]
    [SugarIndex("idx_flow_run_status_owner", nameof(Status), OrderByType.Asc, nameof(OwnerMachine), OrderByType.Asc)]
    public sealed class FlowRunRecord
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "template_id")]
        public int TemplateId { get; set; }

        [SugarColumn(ColumnName = "flow_key", IsNullable = true)]
        public string? FlowKey { get; set; }

        [SugarColumn(ColumnName = "flow_name", IsNullable = true)]
        public string? FlowName { get; set; }

        [SugarColumn(ColumnName = "serial_number", IsNullable = true)]
        public string? SerialNumber { get; set; }

        [SugarColumn(ColumnName = "batch_id", IsNullable = true)]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "run_key", IsNullable = true)]
        public string? RunKey { get; set; }

        [SugarColumn(ColumnName = "started_time_utc", IsNullable = true)]
        public DateTime? StartedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "owner_instance_id", IsNullable = true)]
        public string? OwnerInstanceId { get; set; }

        [SugarColumn(ColumnName = "owner_machine", IsNullable = true)]
        public string? OwnerMachine { get; set; }

        [SugarColumn(ColumnName = "owner_process_id", IsNullable = true)]
        public int? OwnerProcessId { get; set; }

        [SugarColumn(ColumnName = "owner_process_started_utc", IsNullable = true)]
        public DateTime? OwnerProcessStartedUtc { get; set; }

        [SugarColumn(ColumnName = "last_heartbeat_utc", IsNullable = true)]
        public DateTime? LastHeartbeatUtc { get; set; }

        [SugarColumn(ColumnName = "template_revision", IsNullable = true)]
        public int? TemplateRevision { get; set; }

        [SugarColumn(ColumnName = "execution_policy_revision", IsNullable = true)]
        public long? ExecutionPolicyRevision { get; set; }

        [SugarColumn(ColumnName = "execution_policy_hash", IsNullable = true, Length = 64)]
        public string? ExecutionPolicyHash { get; set; }

        [SugarColumn(ColumnName = "execution_policy_snapshot_json", IsNullable = true)]
        public string? ExecutionPolicySnapshotJson { get; set; }

        [SugarColumn(ColumnName = "content_hash", IsNullable = true, Length = 64)]
        public string? ContentHash { get; set; }

        [SugarColumn(ColumnName = "snapshot_id", IsNullable = true)]
        public long? SnapshotId { get; set; }

        [SugarColumn(ColumnName = "status")]
        public FlowStatus Status { get; set; }

        [SugarColumn(ColumnName = "final_outcome", IsNullable = true)]
        public FlowFinalOutcome? FinalOutcome { get; set; }

        [SugarColumn(ColumnName = "elapsed_ms")]
        public long ElapsedMs { get; set; }

        [SugarColumn(ColumnName = "completed_time")]
        public DateTime CompletedTime { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "completed_time_utc", IsNullable = true)]
        public DateTime? CompletedTimeUtc { get; set; }

        [SugarColumn(ColumnName = "recovered_time_utc", IsNullable = true)]
        public DateTime? RecoveredTimeUtc { get; set; }

        [SugarColumn(ColumnName = "recovery_reason", IsNullable = true)]
        public string? RecoveryReason { get; set; }
    }
}
