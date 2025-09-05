using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [SugarTable("t_scgd_mod_third_party_algorithms")]
    public class ModThirdPartyAlgorithmsModel : ViewEntity , IInitTables
    {
        [SugarColumn(ColumnName ="pid")]
        public int? PId { get => _PId; set { _PId = value; OnPropertyChanged(); } }
        private int? _PId;

        [SugarColumn(ColumnName ="code")]
        public string? Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string? _Code;

        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="json_val")]
        public string? JsonVal { get => _JsonVal; set { _JsonVal = value; OnPropertyChanged(); } }
        private string? _JsonVal;

        [SugarColumn(ColumnName ="is_enable")]
        public bool? IsEnable { get; set; } = true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; } = false;

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get; set; }

        [SugarColumn(ColumnName ="tenant_id")]
        public string? TenantId { get; set; }

    }

    public class ModThirdPartyAlgorithmsDao : BaseTableDao<ModThirdPartyAlgorithmsModel>
    {
        public static ModThirdPartyAlgorithmsDao Instance { get; set; } = new ModThirdPartyAlgorithmsDao();


    }
}
