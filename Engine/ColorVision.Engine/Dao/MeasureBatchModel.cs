#pragma warning disable CA1822
using ColorVision.Database;
using ColorVision.Engine.FlowProcessing;
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
    public class MeasureBatchModel : ViewEntity, IInitTables
    {

        [SugarColumn(ColumnName = "t_id", IsNullable = true, ColumnDescription = "t_scgd_measure_template_master")]
        public int? TId { get; set; }

        [SugarColumn(ColumnName = "name",IsNullable = true)]
        public string? Name { get; set; }
        [SugarColumn(ColumnName = "code",IsNullable = true)]
        public string? Code { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = false, DefaultValue = "CURRENT_TIMESTAMP", ColumnDescription = "创建日期")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "total_time", IsNullable = false, DefaultValue = "0")]
        public int TotalTime { get; set; }

        [SugarColumn(ColumnName = "result",IsNullable =true)]
        public string? Result { get; set; }

        [SugarColumn(ColumnName = "result_code", IsNullable = true)]
        public FlowStatus FlowStatus { get => _FlowStatus; set { if (_FlowStatus == value) return; _FlowStatus = value; OnPropertyChanged(); FlowStatusChaned?.Invoke(this,_FlowStatus); } } 
        private FlowStatus _FlowStatus = FlowStatus.Ready;

        public event EventHandler <FlowStatus> FlowStatusChaned;

        [SugarColumn(ColumnName = "archived_flag", ColumnDataType = "smallint", Length = 6, IsNullable = true, DefaultValue = "0", ColumnDescription = "归档状态,-1:不归档，0:待归档，1:已归档，-2:归档失败")]
        public ArchiveStatus ArchiveStatus { get; set; } = ArchiveStatus.Pending;

        [SugarColumn(ColumnName = "tenant_id", IsNullable = true)]
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

        public MeasureBatchModel? GetByNameOrCode(string nameOrCode)
        {
            if (string.IsNullOrWhiteSpace(nameOrCode)) return null;

            try
            {
                using var Db = new SqlSugarClient(new ConnectionConfig { ConnectionString = MySqlControl.GetConnectionString(), DbType = SqlSugar.DbType.MySql, IsAutoCloseConnection = true });
                return Db.Queryable<MeasureBatchModel>()
                    .Where(a => a.Name == nameOrCode || a.Code == nameOrCode)
                    .OrderBy(a => a.Id, OrderByType.Desc)
                    .First();
            }
            catch
            {
                return null;
            }
        }
    }
}
