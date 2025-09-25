using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Rbac.ViewModels
{
    public class UserViewModel
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Remark { get; set; } = string.Empty;
        public bool IsEnable { get; set; }
        public bool? IsDelete { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        
        public List<RoleEntity> Roles { get; set; } = new List<RoleEntity>();
        
        public string RolesDisplay => Roles?.Any() == true ? string.Join(", ", Roles.Select(r => r.Name)) : "无";

        public static UserViewModel FromEntity(UserEntity user, List<RoleEntity> roles = null)
        {
            return new UserViewModel
            {
                Id = user.Id,
                Username = user.Username,
                Remark = user.Remark,
                IsEnable = user.IsEnable,
                IsDelete = user.IsDelete,
                CreatedAt = user.CreatedAt,
                UpdatedAt = user.UpdatedAt,
                Roles = roles ?? new List<RoleEntity>()
            };
        }
    }
}