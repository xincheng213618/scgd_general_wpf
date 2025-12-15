using SqlSugar;
using System;

namespace ColorVision.Rbac.Entity
{
    /// <summary>
    /// 会话实体 - 用于管理用户登录会话
    /// </summary>
    [SugarTable("sys_session")]
    [SugarIndex("idx_sys_session_token", nameof(SessionToken), OrderByType.Asc)]
    [SugarIndex("idx_sys_session_user_id", nameof(UserId), OrderByType.Asc)]
    public class SessionEntity
    {
        [SugarColumn(IsPrimaryKey = true, IsIdentity = true, ColumnName = "id")]
        public int Id { get; set; }

        [SugarColumn(ColumnName = "user_id")]
        public int UserId { get; set; }

        [SugarColumn(ColumnName = "session_token", Length = 128)]
        public string SessionToken { get; set; } = string.Empty;

        [SugarColumn(ColumnName = "device_info", IsNullable = true, Length = 256)]
        public string? DeviceInfo { get; set; }

        [SugarColumn(ColumnName = "ip_address", IsNullable = true, Length = 45)]
        public string? IpAddress { get; set; }

        [SugarColumn(ColumnName = "created_at")]
        public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

        [SugarColumn(ColumnName = "expires_at")]
        public DateTimeOffset ExpiresAt { get; set; }

        [SugarColumn(ColumnName = "last_activity_at")]
        public DateTimeOffset LastActivityAt { get; set; } = DateTimeOffset.UtcNow;

        [SugarColumn(ColumnName = "is_revoked")]
        public bool IsRevoked { get; set; } = false;
    }
}
