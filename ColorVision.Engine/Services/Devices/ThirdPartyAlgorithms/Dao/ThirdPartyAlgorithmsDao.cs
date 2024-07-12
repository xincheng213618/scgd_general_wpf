using ColorVision.Engine.MySql.ORM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [Table("t_scgd_sys_third_party_algorithms")]
    public class ThirdPartyAlgorithmsModel : VPKModel
    {
        [Column("code")]
        public string? Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string? _Code;

        [Column("name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [Column("dev_type")]
        public string? DevType { get => _DevType; set { _DevType = value; NotifyPropertyChanged(); } }
        private string? _DevType;

        [Column("dic_model")]
        public int? DicModel { get; set; }

        [Column("request_type")]
        public int? RequestType { get; set; }

        [Column("is_enable")]
        public bool? IsEnable { get; set; }

        [Column("is_delete")]
        public bool? IsDelete { get; set; }

        [Column("remark")]
        public string? Remark { get; set; }
    }

    public class ThirdPartyAlgorithmsDao : BaseTableDao<ThirdPartyAlgorithmsModel>
    {
        public static ThirdPartyAlgorithmsDao Instance { get; set; } = new ThirdPartyAlgorithmsDao();

        public ThirdPartyAlgorithmsDao() : base("t_scgd_sys_third_party_algorithms", "guid")
        {

        }
    }

}
