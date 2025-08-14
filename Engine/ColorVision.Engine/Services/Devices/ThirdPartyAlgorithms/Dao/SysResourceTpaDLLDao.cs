using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System;

namespace ColorVision.Engine.Services.Devices.ThirdPartyAlgorithms.Dao
{
    [SugarTable("t_scgd_sys_resource_tpa_dll")]
    public class SysResourceTpaDLLModel : VPKModel
    {

        [Column("code")]
        public string? Code { get => _Code; set { _Code = value; NotifyPropertyChanged(); } }
        private string? _Code;

        [Column("name")]
        public string? Name { get => _Name; set { _Name = value; NotifyPropertyChanged(); } }
        private string? _Name;

        [Column("dll_file_name")]
        public string? DLLFileName { get => _DLLFileName; set { _DLLFileName = value; NotifyPropertyChanged(); } }
        private string? _DLLFileName;

        [Column("cfg_json")]
        public string? CfgJson { get => _CfgJson; set { _CfgJson = value; NotifyPropertyChanged(); } }
        private string? _CfgJson;

        [Column("tenant_id")]
        public string? TenantId { get; set; }

        [Column("is_enable")]
        public bool? IsEnable { get; set; }

        [Column("is_delete")]
        public bool? IsDelete { get; set; }

        [Column("create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [Column("remark")]
        public string? Remark { get; set; }



    }


    public class SysResourceTpaDLLDao : BaseTableDao<SysResourceTpaDLLModel>
    {
        public static SysResourceTpaDLLDao Instance { get; set; } = new SysResourceTpaDLLDao();

    }

}
