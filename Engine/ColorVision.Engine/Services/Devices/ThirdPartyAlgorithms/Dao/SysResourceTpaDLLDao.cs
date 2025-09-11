using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [SugarTable("t_scgd_sys_resource_tpa_dll")]
    public class SysResourceTpaDLLModel : ViewEntity , IInitTables
    {

        [SugarColumn(ColumnName ="code")]
        public string? Code { get => _Code; set { _Code = value; OnPropertyChanged(); } }
        private string? _Code;

        [SugarColumn(ColumnName ="name")]
        public string? Name { get => _Name; set { _Name = value; OnPropertyChanged(); } }
        private string? _Name;

        [SugarColumn(ColumnName ="dll_file_name")]
        public string? DLLFileName { get => _DLLFileName; set { _DLLFileName = value; OnPropertyChanged(); } }
        private string? _DLLFileName;

        [SugarColumn(ColumnName ="cfg_json", ColumnDataType ="json")]
        public string? CfgJson { get => _CfgJson; set { _CfgJson = value; OnPropertyChanged(); } }
        private string? _CfgJson;

        [SugarColumn(ColumnName ="tenant_id")]
        public string? TenantId { get; set; }

        [SugarColumn(ColumnName ="is_enable")]
        public bool? IsEnable { get; set; }

        [SugarColumn(ColumnName ="is_delete")]
        public bool? IsDelete { get; set; }

        [SugarColumn(ColumnName ="create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get; set; }



    }


    public class SysResourceTpaDLLDao : BaseTableDao<SysResourceTpaDLLModel>
    {
        public static SysResourceTpaDLLDao Instance { get; set; } = new SysResourceTpaDLLDao();

    }

}
