using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.SMU.Dao
{

    [SugarTable("t_scgd_measure_result_smu")]
    public class SMUResultModel : EntityBase
    {

        [SugarColumn(ColumnName ="batch_id",IsNullable =true)]
        public int? Batchid { get; set; }


        [SugarColumn(ColumnName = "device_code", IsNullable = true)]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName ="is_source_v")]
        public bool IsSourceV { get; set; }

        [SugarColumn(ColumnName ="src_value")]
        public float SrcValue { get; set; }

        [SugarColumn(ColumnName ="limit_value")]
        public float LimitValue { get; set; }

        [SugarColumn(ColumnName ="v_result")]
        public float VResult { get; set; }

        [SugarColumn(ColumnName ="i_result")]
        public float IResult { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }
    }

}
