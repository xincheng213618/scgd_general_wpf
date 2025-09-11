using ColorVision.Database;
using SqlSugar;

namespace ColorVision.Engine
{
    [SugarTable("t_scgd_sys_resource_group")]
    public class SysResourceGoupModel : EntityBase
    {

        [SugarColumn(ColumnName = "resource_id")]
        public int ResourceId { get; set; }
        [SugarColumn(ColumnName = "group_id")]
        public int GroupId { get; set; }

        [SugarColumn(IsIgnore = true)]
        public SysResourceModel Group { get; set; }
        [SugarColumn(IsIgnore = true)]
        public SysResourceModel Resourced { get; set; }
    }
}
