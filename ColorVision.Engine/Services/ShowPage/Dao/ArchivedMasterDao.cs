#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Services.ShowPage.Dao
{



    public class ArchivedMasterModel : PKModel
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Data { get; set; }
        public string Remark { get; set; }
        public int? TenantId { get; set; }

        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public DateTime? ArchDate { get; set; } = DateTime.Now;

    }
    public class ArchivedMasterDao : BaseTableDao<ArchivedMasterModel>
    {
        public static ArchivedMasterDao Instance { get; set; } = new ArchivedMasterDao();

        public ArchivedMasterDao() : base("t_scgd_archived_master", "code")
        {

        }

        public override ArchivedMasterModel GetModelFromDataRow(DataRow item) => new()
        {
            Name = item.Field<string>("name"),
            TenantId = item.Field<int?>("tenant_id"),
            Code = item.Field<string>("code"),
            Data = item.Field<string>("data"),
            Remark = item.Field<string>("data"),
            ArchDate = item.Field<DateTime?>("arch_date"),
            CreateDate = item.Field<DateTime?>("create_date"),
        };

        public List<ArchivedMasterModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "code", batchCode } });
        }
    }
}
