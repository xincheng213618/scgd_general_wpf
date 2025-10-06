using SqlSugar;

namespace ColorVision.Rbac
{
    [SugarTable("sys_permission")]    
    [SugarIndex("uidx_sys_permission_code", nameof(Code), OrderByType.Asc, true)]
    public class PermissionEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")] public int Id { get; set; }
        [SugarColumn(ColumnName = "name")] public string Name { get; set; } = string.Empty;
        [SugarColumn(ColumnName = "code")] public string Code { get; set; } = string.Empty; // e.g. user.create
        [SugarColumn(ColumnName = "group", IsNullable = true)] public string? Group { get; set; }
        [SugarColumn(ColumnName = "remark", IsNullable = true)] public string? Remark { get; set; }
        [SugarColumn(ColumnName = "is_enable")] public bool IsEnable { get; set; } = true;
        [SugarColumn(ColumnName = "is_delete")] public bool? IsDelete { get; set; } = false;
        [SugarColumn(ColumnName = "created_at", IsNullable = true)] public DateTimeOffset? CreatedAt { get; set; } = DateTimeOffset.Now;
        [SugarColumn(ColumnName = "updated_at", IsNullable = true)] public DateTimeOffset? UpdatedAt { get; set; } = DateTimeOffset.Now;
    }

    [SugarTable("sys_role_permission")]    
    public class RolePermissionEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")] public int Id { get; set; }
        [SugarColumn(ColumnName = "role_id")] public int RoleId { get; set; }
        [SugarColumn(ColumnName = "permission_id")] public int PermissionId { get; set; }
    }
}
