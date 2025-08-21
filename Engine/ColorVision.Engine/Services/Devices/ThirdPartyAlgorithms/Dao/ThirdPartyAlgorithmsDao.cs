using ColorVision.Engine.MySql;
using ColorVision.Engine.MySql.ORM;
using SqlSugar;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [SugarTable("t_scgd_sys_third_party_algorithms")]
    public class ThirdPartyAlgorithmsModel : VPKModel, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get => _Pid; set { _Pid = value; NotifyPropertyChanged(); } }
        private int? _Pid;

        [SugarColumn(ColumnName ="code")]
        public string? Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string? _Code;

        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="dev_type")]
        public string? DevType { get => _DevType; set { _DevType = value; NotifyPropertyChanged(); } }
        private string? _DevType;

        [SugarColumn(ColumnName ="dic_model")]
        public int? DicModel { get => _DicModel; set { _DicModel = value; NotifyPropertyChanged(); } }
        private int? _DicModel=0;

        [SugarColumn(ColumnName ="default_cfg")]
        public string? DefaultCfg { get => _DefaultCfg; set { _DefaultCfg = value; NotifyPropertyChanged(); } }
        private string? _DefaultCfg;

        [SugarColumn(ColumnName ="request_type")]
        public int? RequestType { get => _RequestType; set { _RequestType = value; NotifyPropertyChanged(); } }
        private int? _RequestType = 0;

        [SugarColumn(ColumnName ="result_type")]
        public int? ResultType { get => _ResultType; set { _ResultType = value; NotifyPropertyChanged(); } }
        private int? _ResultType = 0;

        [SugarColumn(ColumnName ="is_enable")]
        public bool? IsEnable { get => _IsEnable; set { _IsEnable = value; NotifyPropertyChanged(); } }
        private bool? _IsEnable = true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get => _IsDelete; set { _IsDelete = value; NotifyPropertyChanged(); } }
        private bool? _IsDelete = false;

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get => _Remark; set { _Remark = value; NotifyPropertyChanged(); } }
        private string? _Remark;
    }


    public class ThirdPartyAlgorithmsDao : BaseTableDao<ThirdPartyAlgorithmsModel>
    {
        public static ThirdPartyAlgorithmsDao Instance { get; set; } = new ThirdPartyAlgorithmsDao();

    }

}
