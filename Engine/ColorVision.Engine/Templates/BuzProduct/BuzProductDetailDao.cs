using ColorVision.Engine.MySql.ORM;

namespace ColorVision.Engine.Templates.BuzProduct
{
    public class BuzProductDetailModel : VPKModel
    {
        [Column("code")]
        public string Code { get=> _Code; set { _Code = value; NotifyPropertyChanged();  } }
        private string _Code;

        [Column("name")]
        public string Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string _Name;

        [Column("pid")]
        public int? Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int? _Pid;

        [Column("poi_id")]
        public int? PoiId { get => _PoiId; set { _PoiId = value; NotifyPropertyChanged(); } }
        private int? _PoiId;

        [Column("order_index")]
        public int? OrderIndex { get => _OrderIndex; set { _OrderIndex = value; NotifyPropertyChanged(); } }
        private int? _OrderIndex;

        [Column("cfg_json")]
        public string CfgJson { get => _CfgJson; set { _CfgJson = value; NotifyPropertyChanged(); } }
        private string _CfgJson;

        [Column("val_rule_temp_id")]
        public int? ValRuleTempId { get => _ValRuleTempId; set { _ValRuleTempId = value; NotifyPropertyChanged(); } }
        private int? _ValRuleTempId;
    }

    public class BuzProductDetailDao : BaseTableDao<BuzProductDetailModel>
    {
        public static BuzProductDetailDao Instance { get; set; } = new BuzProductDetailDao();

        public BuzProductDetailDao() : base("t_scgd_buz_product_detail", "id")
        {

        }
    }
}
