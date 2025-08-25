using ColorVision.Database;
using System;
using SqlSugar;

namespace ColorVision.Engine
{
    [SugarTable("t_scgd_mod_param_master")]
    public class ModMasterModel : PKModel
    {

        [SugarColumn(ColumnName = "code", IsNullable = true)]
        public string? Code { get; set; }
        [SugarColumn(ColumnName = "name",IsNullable =true)]
        public string? Name { get; set; }

        [SugarColumn(ColumnName = "mm_id", IsNullable = true,ColumnDescription = "t_scgd_sys_dictionary_mod_master")]
        public int Pid { get; set; }

        [SugarColumn(ColumnName = "res_pid", IsNullable = true,ColumnDescription = "t_scgd_sys_resource")]
        public int? ResourceId { get; set; }

        [SugarColumn(ColumnName = "cfg_json", ColumnDataType = "json",IsNullable =true)]
        public string? JsonVal { get; set; } 

        [SugarColumn(ColumnName = "create_date")]
        public DateTime? CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName = "remark", ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }

        [SugarColumn(ColumnName = "tenant_id", IsNullable = true)]
        public int TenantId { get; set; }
    }
}
