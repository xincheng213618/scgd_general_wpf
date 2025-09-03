using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [SugarTable("t_scgd_sys_third_party_algorithms")]
    public class ThirdPartyAlgorithmsModel : VPKModel, IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int? Pid { get => _Pid; set { _Pid = value; OnPropertyChanged(); } }
        private int? _Pid;

        [SugarColumn(ColumnName ="code")]
        public string? Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string? _Code;

        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="dev_type")]
        public string? DevType { get => _DevType; set { _DevType = value; OnPropertyChanged(); } }
        private string? _DevType;

        [SugarColumn(ColumnName ="dic_model")]
        public int? DicModel { get => _DicModel; set { _DicModel = value; OnPropertyChanged(); } }
        private int? _DicModel=0;

        [SugarColumn(ColumnName ="default_cfg")]
        public string? DefaultCfg { get => _DefaultCfg; set { _DefaultCfg = value; OnPropertyChanged(); } }
        private string? _DefaultCfg;

        [SugarColumn(ColumnName ="request_type")]
        public int? RequestType { get => _RequestType; set { _RequestType = value; OnPropertyChanged(); } }
        private int? _RequestType = 0;

        [SugarColumn(ColumnName ="result_type")]
        public int? ResultType { get => _ResultType; set { _ResultType = value; OnPropertyChanged(); } }
        private int? _ResultType = 0;

        [SugarColumn(ColumnName ="is_enable")]
        public bool? IsEnable { get => _IsEnable; set { _IsEnable = value; OnPropertyChanged(); } }
        private bool? _IsEnable = true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get => _IsDelete; set { _IsDelete = value; OnPropertyChanged(); } }
        private bool? _IsDelete = false;

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get => _Remark; set { _Remark = value; OnPropertyChanged(); } }
        private string? _Remark;
    }


    public class ThirdPartyAlgorithmsDao : BaseTableDao<ThirdPartyAlgorithmsModel>
    {
        public static ThirdPartyAlgorithmsDao Instance { get; set; } = new ThirdPartyAlgorithmsDao();

    }

}
