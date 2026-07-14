using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.Sensor.Dao
{
    [SugarTable("t_scgd_measure_result_sensor")]
    public class SensorResultEntity : EntityBase, IInitTables
    {
        [SugarColumn(ColumnName = "batch_id", IsNullable = true)]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? ZIndex { get; set; }

        [SugarColumn(ColumnName = "cmd_type", IsNullable = true, Length = 255)]
        public string? CommandType { get; set; }

        [SugarColumn(ColumnName = "result_code", IsNullable = true)]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName = "result", IsNullable = true, ColumnDataType = "text")]
        public string? Result { get; set; }

        [SugarColumn(ColumnName = "total_time", IsNullable = true)]
        public int? TotalTime { get; set; }

        [SugarColumn(ColumnName = "device_code", IsNullable = true, Length = 255)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP")]
        public DateTime CreateDate { get; set; }
    }
}
