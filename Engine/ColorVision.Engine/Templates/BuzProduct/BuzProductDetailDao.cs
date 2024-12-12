using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class BuzProductDetailModel : PKModel
    {
        [Column("code")]
        public string Code { get; set; }

        [Column("name")]
        public string Name { get; set; }

        [Column("pid")]
        public int? Pid { get; set; }

        [Column("poi_id")]
        public int? PoiId { get; set; }

        [Column("order_index")]
        public int? OrderIndex { get; set; }

        [Column("cfg_json")]
        public string CfgJson { get; set; }

        [Column("val_rule_temp_id")]
        public int? ValRuleTempId { get; set; }
    }

    public class BuzProductDetailDao : BaseTableDao<BuzProductDetailModel>
    {
        public static BuzProductDetailDao Instance { get; set; } = new BuzProductDetailDao();

        public BuzProductDetailDao() : base("t_scgd_buz_product_detail", "id")
        {

        }
    }
}
