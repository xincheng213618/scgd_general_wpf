using ColorVision.Database;
using SqlSugar;
using System;

namespace ColorVision.Engine
{
    [SugarTable("t_scgd_sys_resource")]
    public class SysResourceModel : EntityBase
    {
        public SysResourceModel()
        {
        }

        [SugarColumn(ColumnName = "name")]
        public string? Name { get; set; }
        [SugarColumn(ColumnName = "code")]
        public string? Code { get; set; }
        [SugarColumn(ColumnName = "type")]
        public int Type { get; set; }

        [SugarColumn(ColumnName = "pid", IsNullable = true)]
        public int? Pid { get; set; }

        [SugarColumn(ColumnName = "txt_value", ColumnDataType = "longtext", IsNullable = true)]
        public string? Value { get; set; }

        [SugarColumn(ColumnName = "create_date", IsNullable = true)]
        public DateTime CreateDate { get; set; } = DateTime.Now;

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName = "tenant_id")]
        public int TenantId { get; set; } = 0;

        [SugarColumn(ColumnName = "remark", ColumnDataType = "text", IsNullable = true)]
        public string? Remark { get; set; }
    }
}
