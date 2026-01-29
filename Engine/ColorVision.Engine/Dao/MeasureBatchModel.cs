using ColorVision.Database;
using ColorVision.Engine.Templates.Flow;
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
    public class MeasureBatchModel : ViewEntity
    {

        [SugarColumn(ColumnName = "t_id", IsNullable = true)]
        public int? TId { get; set; }

        [SugarColumn(ColumnName = "name",IsNullable = true)]
        public string? Name { get; set; }
        [SugarColumn(ColumnName = "code",IsNullable = true)]
        public string? Code { get; set; }

        [SugarColumn(ColumnName = "create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "total_time")]
        public int TotalTime { get; set; }

        [SugarColumn(ColumnName = "result",IsNullable =true)]
        public string? Result { get; set; }

        [SugarColumn(ColumnName = "result_code", IsNullable = true)]
        public FlowStatus FlowStatus { get => _FlowStatus; set { if (_FlowStatus == value) return; _FlowStatus = value; OnPropertyChanged(); FlowStatusChaned?.Invoke(this,_FlowStatus); } } 
        private FlowStatus _FlowStatus = FlowStatus.Ready;

        public event EventHandler <FlowStatus> FlowStatusChaned;

        [SugarColumn(ColumnName = "archived_flag")]
        public ArchiveStatus ArchiveStatus { get; set; } = ArchiveStatus.Pending;

        [SugarColumn(ColumnName = "tenant_id")]
        public int TenantId { get; set; }
    }



    public class BatchResultMasterDao : BaseTableDao<MeasureBatchModel>
    {
        public static BatchResultMasterDao Instance { get; set; } = new BatchResultMasterDao();

        public MeasureBatchModel? GetByCode(string code) 
        {
            using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
            return Db.Queryable<MeasureBatchModel>().Where(a => a.Code == code).First();
        }
    }
}
