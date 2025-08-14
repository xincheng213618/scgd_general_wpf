using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Dao
{
    [SugarTable("t_scgd_sys_dictionary_mod_master")]
    public class SysModMasterModel : PKModel
    {
        [Column("name")]
        public string? Name { get; set; }
        [Column("code")]
        public string? Code { get; set; }
        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [Column("is_enable")]
        public bool IsEnable { get; set; } = true;
        [Column("is_delete")]
        public bool? IsDelete { get; set; } = false;
        [Column("remark")]
        public string? Remark { get; set; }
        [Column("tenant_id")]
        public int TenantId { get; set; }
    }

    public class SysModMasterDao : BaseTableDao<SysModMasterModel>
    {
        public static SysModMasterDao Instance { get; set; } = new SysModMasterDao();
        public SysModMasterDao() : base()
        {
        }
    }
}
