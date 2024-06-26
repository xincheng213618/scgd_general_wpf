#pragma warning disable CS8601
using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Data;

namespace ColorVision.Engine.Services.ShowPage.Dao
{



    public class ArchivedMasterModel : PKModel
    {
        [Column("code")]
        public string Code { get; set; }
        [Column("name")]
        public string Name { get; set; }
        [Column("data")]
        public string Data { get; set; }
        [Column("remark")]
        public string Remark { get; set; }
        [Column("tenant_id")]
        public int? TenantId { get; set; }
        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("arch_date")]
        public DateTime? ArchDate { get; set; } = DateTime.Now;

    }
    public class ArchivedMasterDao : BaseTableDao<ArchivedMasterModel>
    {
        public static ArchivedMasterDao Instance { get; set; } = new ArchivedMasterDao();

        public ArchivedMasterDao() : base("t_scgd_archived_master", "code")
        {

        }

        public List<ArchivedMasterModel> ConditionalQuery(string batchCode)
        {
            return ConditionalQuery(new Dictionary<string, object>() { { "code", batchCode } });
        }
    }
}
