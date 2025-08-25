using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine
{

    public enum ArchiveStatus
    {
        NotArchived = -1,
        Pending = 0,
        Archived = 1,
        Failed = -2
    }

    [SugarTable("t_scgd_measure_batch")]
    public class MeasureBatchModel : PKModel, IInitTables
    {

        [SugarColumn(ColumnName = "t_id")]
        public int? TId { get; set; }
        [SugarColumn(ColumnName = "name")]
        public string? Name { get; set; }
        [SugarColumn(ColumnName = "code")]
        public string? Code { get; set; }
        [SugarColumn(ColumnName = "create_date")]
        public DateTime? CreateDate { get; set; }
        [SugarColumn(ColumnName = "total_time")]
        public int? TotalTime { get; set; }

        [SugarColumn(ColumnName = "result")]
        public string? Result { get; set; }


        [SugarColumn(ColumnName = "archived_flag")]
        public ArchiveStatus ArchiveStatus { get; set; }

        [SugarColumn(ColumnName = "tenant_id")]
        public int TenantId { get; set; }
    }



    public class BatchResultMasterDao : BaseTableDao<MeasureBatchModel>
    {
        public static BatchResultMasterDao Instance { get; set; } = new BatchResultMasterDao();

        public MeasureBatchModel? GetByCode(string code) => Db.Queryable<MeasureBatchModel>().Where(a => a.Code == code).First();
    }
}
