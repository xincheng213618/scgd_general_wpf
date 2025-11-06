using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.SMU.Dao
{

    [SugarTable("t_scgd_measure_result_smu")]
    public class SMUResultModel : EntityBase, IInitTables
    {
        [SugarColumn(ColumnName = "device_code", Length = 255, IsNullable = true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName = "batch_id", IsNullable = true, ColumnDescription = "SN")]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName = "z_index", IsNullable = true)]
        public int? ZIndex { get; set; }

        [SugarColumn(ColumnName = "is_source_v", IsNullable = true, ColumnDescription = "是否电压")]
        public bool? IsSourceV { get; set; }

        [SugarColumn(ColumnName = "src_value", IsNullable = true, ColumnDescription = "源值")]
        public float? SrcValue { get; set; }

        [SugarColumn(ColumnName = "limit_value", IsNullable = true, ColumnDescription = "限值")]
        public float? LimitValue { get; set; }

        [SugarColumn(ColumnName = "v_result", IsNullable = true, ColumnDescription = "电压")]
        public float? VResult { get; set; }

        [SugarColumn(ColumnName = "i_result", IsNullable = true, ColumnDescription = "电流")]
        public float? IResult { get; set; }

        [SugarColumn(ColumnName = "result_code", IsNullable = true, ColumnDescription = "结果CODE")]
        public int? ResultCode { get; set; }

        [SugarColumn(ColumnName = "total_time", IsNullable = true, ColumnDescription = "总用时(ms)")]
        public int? TotalTime { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = false, ColumnDescription = "创建日期", DefaultValue = "CURRENT_TIMESTAMP")]
        public DateTime CreateDate { get; set; }

    }

}
