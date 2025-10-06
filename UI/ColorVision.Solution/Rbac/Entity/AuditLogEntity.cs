using SqlSugar;

namespace ColorVision.Rbac
{
    [SugarTable("sys_audit_log")]    
    public class AuditLogEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")] public int Id { get; set; }
        [SugarColumn(ColumnName = "user_id", IsNullable = true)] public int? UserId { get; set; }
        [SugarColumn(ColumnName = "username", IsNullable = true)] public string? Username { get; set; }
        [SugarColumn(ColumnName = "action")] public string Action { get; set; } = string.Empty; // e.g. user.create
        [SugarColumn(ColumnName = "detail", IsNullable = true)] public string? Detail { get; set; }
        [SugarColumn(ColumnName = "created_at")] public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
        [SugarColumn(ColumnName = "ip", IsNullable = true)] public string? Ip { get; set; }
    }
}
