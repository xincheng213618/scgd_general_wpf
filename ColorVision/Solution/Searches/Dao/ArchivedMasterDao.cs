using System;
using System.Collections.Generic;
using System.Data;
using ColorVision.MySql;
using ColorVision.Services.Devices.Camera.Dao;

namespace ColorVision.Solution.Searches
{
    public class ArchivedMasterModel : PKModel
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? Data { get; set; }
        public string? Remark { get; set; }
        public string? TenantId { get; set; }

        public DateTime? CreateDate { get; set; } = DateTime.Now;
        public DateTime? ArchDate { get; set; } = DateTime.Now;

    }
    public class ArchivedMasterDao : BaseTableDao<ArchivedMasterModel>
    {
        public static ArchivedMasterDao Instance { get; set; } = new ArchivedMasterDao();

        public ArchivedMasterDao() : base("t_scgd_archived_master", "id")
        {

        }

        public override ArchivedMasterModel GetModelFromDataRow(DataRow item) => new ArchivedMasterModel()
        {
            Id = item.Field<int>("id"),
            Name = item.Field<string?>("name"),
            TenantId =item.Field<string?>("tenant_id"),
            Code = item.Field<string?>("code"),
            Data = item.Field<string?>("data"),
            Remark = item.Field<string?>("data"),
            ArchDate = item.Field<DateTime?>("arch_date"),
            CreateDate = item.Field<DateTime?>("create_date"),
        };

        public List<ArchivedMasterModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "code", batchCode } });
        }
    }
}
