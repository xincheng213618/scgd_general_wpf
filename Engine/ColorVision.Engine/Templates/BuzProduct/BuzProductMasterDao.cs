using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Templates.BuzProduct
{
    [SugarTable("t_scgd_buz_product_master")]
    public class BuzProductMasterModel : VPKModel
    {
        [Column("code")]
        public string Code { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("buz_type")]
        public int? BuzType { get; set; }

        [Column("cfg_json")]
        public string CfgJson { get; set; }

        [Column("img_file")]
        public string ImgFile { get; set; }

        [Column("create_date")]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [Column("is_enable")]
        public bool IsEnable { get; set; } = true;

        [Column("is_delete")]
        public bool IsDelete { get; set; } = false;

        [Column("tenant_id")]
        public int? TenantId { get; set; }

        [Column("remark")]
        public string Remark { get; set; }
    }

    public class BuzProductMasterDao : BaseTableDao<BuzProductMasterModel>
    {
        public static BuzProductMasterDao Instance { get; set; } = new BuzProductMasterDao();
    }
}
