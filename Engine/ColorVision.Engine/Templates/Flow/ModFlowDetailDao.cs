using ColorVision.Engine.MySql.ORM;
using SqlSugar;
using System.Data;

namespace ColorVision.Engine.Templates.Flow
{
    [SugarTable("t_scgd_mod_param_detail")]
    public class ModDetailModel : PKModel
    {

        [SugarColumn(ColumnName ="cc_pid",Length = 11)]
        public int SysPid { get; set; }

        [SugarColumn(ColumnName ="pid",Length =11)]
        public int Pid { get; set; }

        [@SugarColumn(IsIgnore =true)]
        public string? Value { get; set; }

        [SugarColumn(ColumnName ="value_a",IsNullable = true)]
        public string? ValueA { get; set; }

        [SugarColumn(ColumnName ="value_b" ,IsNullable =true)]
        public string? ValueB { get; set; }

        [SugarColumn(ColumnName ="is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName ="is_delete")]
        public bool IsDelete { get; set; }
    }

}
