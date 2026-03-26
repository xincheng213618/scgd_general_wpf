using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.Flow
{
    [SugarTable("FlowNodeRecord")]
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
