using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowRunRecord")]
    public sealed class FlowRunRecord
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "template_id")]
        public int TemplateId { get; set; }

        [SugarColumn(ColumnName = "flow_name", IsNullable = true)]
        public string? FlowName { get; set; }

        [SugarColumn(ColumnName = "serial_number", IsNullable = true)]
        public string? SerialNumber { get; set; }

        [SugarColumn(ColumnName = "status")]
        public FlowStatus Status { get; set; }

        [SugarColumn(ColumnName = "elapsed_ms")]
        public long ElapsedMs { get; set; }

        [SugarColumn(ColumnName = "completed_time")]
        public DateTime CompletedTime { get; set; } = DateTime.Now;
    }
}
