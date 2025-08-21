using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Dao
{
    [SugarTable("t_scgd_sys_dictionary_mod_master")]
    public class SysModMasterModel : PKModel
    {
        [SugarColumn(ColumnName ="name")]
        public string? Name { get; set; }
        [SugarColumn(ColumnName ="code")]
        public string? Code { get; set; }
        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;
        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;
        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; } = false;
        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get; set; }
        [SugarColumn(ColumnName ="tenant_id")]
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
