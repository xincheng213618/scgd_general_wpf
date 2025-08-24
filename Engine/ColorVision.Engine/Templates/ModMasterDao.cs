using ColorVision.Database;
using ColorVision.Engine.Templates.SysDictionary;
using System;
using System.Collections.Generic;
using System.Data;
using SqlSugar;

namespace ColorVision.Engine.Templates
{
    [SugarTable("t_scgd_mod_param_master")]
    public class ModMasterModel : PKModel
    {
        public ModMasterModel()
        {
        }


        [SugarColumn(ColumnName = "code", IsNullable = true)]
        public string? Code { get; set; }
        [SugarColumn(ColumnName = "name",IsNullable =true)]
        public string? Name { get; set; }

        [SugarColumn(ColumnName = "mm_id", IsNullable = true)]
        public int Pid { get; set; }

        [SugarColumn(ColumnName = "res_pid", IsNullable = true)]
        public int? ResourceId { get; set; }

        [SugarColumn(ColumnName = "cfg_json",IsNullable =true)]
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
