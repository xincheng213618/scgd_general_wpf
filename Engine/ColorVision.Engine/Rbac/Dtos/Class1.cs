using ColorVision.UI.Authorizations;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Rbac.Dtos
{
    public class LoginResultDto
    {
        public UserSummaryDto User { get; set; } = new();
        public UserDetailDto UserDetail { get; set; } = new();
        public List<RoleDto> Roles { get; set; } = new();
    }

    public class UserSummaryDto
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public bool IsEnable { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class UserDetailDto
    {
        public int UserId { get; set; }
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? Company { get; set; }
        public string? Department { get; set; }
        public string? Position { get; set; }
        public string? Remark { get; set; }
        public string? UserImage { get; set; }
        public PermissionMode PermissionMode { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }

    public class RoleDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = "";
    }
}
