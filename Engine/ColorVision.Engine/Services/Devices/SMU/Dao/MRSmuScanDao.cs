using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.SMU.Dao
{
    [SugarTable("t_scgd_measure_result_smu_scan")]
    public class SmuScanModel : EntityBase
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

    }
}
