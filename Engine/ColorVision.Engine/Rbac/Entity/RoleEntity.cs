using SqlSugar;

namespace ColorVision.Engine.Rbac
{
    [SugarTable("sys_role")]
    public class RoleEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "name")]
        public string Name { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "code")]
        public string Code { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "remark")]
        public string Remark { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "is_enable")]
        public bool IsEnable { get; set; } = true;

        [SugarColumn(ColumnName = "is_delete")]
        public bool? IsDelete { get; set; } = false;
    }
}
