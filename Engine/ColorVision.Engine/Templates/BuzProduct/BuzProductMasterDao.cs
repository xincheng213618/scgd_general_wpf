using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.BuzProduct
{
    [SugarTable("t_scgd_buz_product_master")]
    public class BuzProductMasterModel : VPKModel
    {
        [SugarColumn(ColumnName ="code")]
        public string Code { get; set; }

        [SugarColumn(ColumnName ="name")]
        public string Name { get; set; }

        [SugarColumn(ColumnName ="buz_type")]
        public int? BuzType { get; set; }

        [SugarColumn(ColumnName ="cfg_json")]
        public string CfgJson { get; set; }

        [SugarColumn(ColumnName ="img_file")]
        public string ImgFile { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get; set; } = false;

        [SugarColumn(ColumnName ="tenant_id")]
        public int? TenantId { get; set; }

        [SugarColumn(ColumnName ="remark")]
        public string Remark { get; set; }
    }

    public class BuzProductMasterDao : BaseTableDao<BuzProductMasterModel>
    {
        public static BuzProductMasterDao Instance { get; set; } = new BuzProductMasterDao();
    }
}
