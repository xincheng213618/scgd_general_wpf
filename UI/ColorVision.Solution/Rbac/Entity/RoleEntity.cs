using SqlSugar;

namespace ColorVision.Rbac
{
    [SugarTable("sys_role")]
    [SugarIndex("uidx_sys_role_code", nameof(Code), OrderByType.Asc, true)]
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

        [SugarColumn(ColumnName = "created_at", IsNullable = true)]
        public DateTimeOffset? CreatedAt { get; set; } = DateTimeOffset.Now;
        [SugarColumn(ColumnName = "updated_at", IsNullable = true)]
        public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.Now;
    }
}
