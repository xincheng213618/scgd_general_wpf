using ColorVision.Database;
using SqlSugar;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates
{
    [SugarTable("t_scgd_sys_dictionary")]
    public class SysDictionaryModel : VPKModel
    {
        [SugarColumn(ColumnName ="name")]
        public string? Name { get; set; }

        [SugarColumn(ColumnName ="key")]
        public string? Key { get; set; }

        [SugarColumn(ColumnName ="pid")]
        public int Type { get; set; }

        [SugarColumn(ColumnName ="pid")]
        public int Pid { get; set; }
        [SugarColumn(ColumnName ="val",IsNullable =true)]
        public int Value { get; set; }

        [SugarColumn(ColumnName ="tenant_id")]
        public int TenantId { get; set; }

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; }

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get; set; }

        [SugarColumn(ColumnName ="is_hide")]
        public bool IsHide { get; set; }

        [SugarColumn(ColumnName ="remark")]
        public string? Remark { get; set; }

    }
    public class SysDictionaryDao : BaseTableDao<SysDictionaryModel>
    {
        public static SysDictionaryDao Instance { get; set; } = new SysDictionaryDao();

    }
}
