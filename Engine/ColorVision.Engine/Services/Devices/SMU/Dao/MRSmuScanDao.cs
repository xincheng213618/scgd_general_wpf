using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Services.Devices.SMU.Dao
{
    [SugarTable("t_scgd_measure_result_smu_scan")]
    public class SmuScanModel : PKModel
    {
        [SugarColumn(ColumnName ="device_code")]
        public string? DeviceCode { get; set; }

        [SugarColumn(ColumnName ="batch_id")]
        public int? BatchId { get; set; }

        [SugarColumn(ColumnName ="is_source_v")]
        public bool IsSourceV { get; set; }

        [SugarColumn(ColumnName ="src_end")]
        public float SrcBegin { get; set; }

        [SugarColumn(ColumnName ="src_begin")]
        public float SrcEnd { get; set; }

        [SugarColumn(ColumnName ="v_result")]
        public string? VResult { get; set; }

        [SugarColumn(ColumnName ="i_result")]
        public string? IResult { get; set; }

        [SugarColumn(ColumnName ="points")]
        public int Points { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; }
    }

    public class MRSmuScanDao : BaseTableDao<SmuScanModel>
    {
        public static MRSmuScanDao Instance { get; set; } = new MRSmuScanDao();
        public List<SmuScanModel> ConditionalQuery(string id, string batchid, DateTime? dateTimeSTART, DateTime? dateTimeEnd)
        {
            Dictionary<string, object> keyValuePairs = new();
            keyValuePairs.Add("id", id);
            keyValuePairs.Add("batch_id", batchid);

#pragma warning disable CS8604 // 引用类型参数可能为 null。
            keyValuePairs.Add(">create_date", dateTimeSTART);
            keyValuePairs.Add("<create_date", dateTimeEnd);
#pragma warning restore CS8604 // 引用类型参数可能为 null。
            return ConditionalQuery(keyValuePairs);
        }

    }
}
