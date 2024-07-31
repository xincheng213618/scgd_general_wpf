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
        [Column("pid")]
        public int? Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int? _Pid;

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
        public int? DicModel { get => _DicModel; set { _DicModel = value; NotifyPropertyChanged(); } }
        private int? _DicModel;

        [Column("default_cfg")]
        public string? DefaultCfg { get => _DefaultCfg; set { _DefaultCfg = value; NotifyPropertyChanged(); } }
        private string? _DefaultCfg;

        [Column("request_type")]
        public int? RequestType { get => _RequestType; set { _RequestType = value; NotifyPropertyChanged(); } }
        private int? _RequestType;

        [Column("result_type")]
        public int? ResultType { get => _ResultType; set { _ResultType = value; NotifyPropertyChanged(); } }
        private int? _ResultType;

        [Column("is_enable")]
        public bool? IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool? _IsEnable;

        [Column("is_delete")]
        public bool? IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool? _IsDelete;

        [Column("remark")]
        public string? Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string? _Remark;
    }


    public class ThirdPartyAlgorithmsDao : BaseTableDao<ThirdPartyAlgorithmsModel>
    {
        public static ThirdPartyAlgorithmsDao Instance { get; set; } = new ThirdPartyAlgorithmsDao();

        public ThirdPartyAlgorithmsDao() : base("t_scgd_sys_third_party_algorithms")
        {

        }
    }

}
