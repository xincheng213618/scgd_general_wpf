using SqlSugar;
using System;

namespace ColorVision.Engine.FlowProcessing.Diagnostics
{
    [SugarTable("FlowNodeRecord")]
    [SugarIndex(
        "idx_flow_node_record_batch_time",
        nameof(BatchId),
        OrderByType.Asc,
        nameof(StartTime),
        OrderByType.Desc)]
    [SugarIndex(
        "idx_flow_node_record_node_time",
        nameof(NodeId),
        OrderByType.Asc,
        nameof(StartTime),
        OrderByType.Desc)]
    public class FlowNodeRecord
    {
        [SugarColumn(ColumnName = "id", IsPrimaryKey = true, IsIdentity = true)]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "batch_id")]
        public int BatchId { get; set; }

        [SugarColumn(ColumnName = "serial_number", IsNullable = true)]
        public string SerialNumber { get; set; }

        [SugarColumn(ColumnName = "node_id", IsNullable = true)]
        public string NodeId { get; set; }

        [SugarColumn(ColumnName = "node_name", IsNullable = true)]
        public string NodeName { get; set; }

        [SugarColumn(ColumnName = "node_type", IsNullable = true)]
        public string NodeType { get; set; }

        [SugarColumn(ColumnName = "start_time")]
        public DateTime StartTime { get; set; }

        [SugarColumn(ColumnName = "end_time", IsNullable = true)]
        public DateTime? EndTime { get; set; }

        [SugarColumn(ColumnName = "elapsed_ms")]
        public long ElapsedMs { get; set; }
    }
}
