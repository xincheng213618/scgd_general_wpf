#pragma warning disable 
using ColorVision.Rbac.Dtos;
using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Security;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    public interface IUserService
    {
        // ... 其他方法省略

        // 使用乐观并发：传入原始的 UpdatedAt 作为 expected，用于防止覆盖他人修改
        Task UpdateUserDetailAsync(UserDetailDto dto, DateTimeOffset expectedUpdatedAt, CancellationToken ct = default);
        Task<bool> CreateUserAsync(string username, string password, string? remark = null, IEnumerable<int>? roleIds = null, CancellationToken ct = default);
        Task<List<RoleDto>> GetAllRolesAsync(CancellationToken ct = default);
    }

    public class UserService : IUserService
    {
        private readonly ISqlSugarClient _db;
        public UserService(ISqlSugarClient db) { _db = db; }

        public async Task<bool> CreateUserAsync(string username, string password, string? remark = null, IEnumerable<int>? roleIds = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password)) return false;
            username = username.Trim();

            if (await _db.Queryable<UserEntity>().AnyAsync(u => u.Username == username, ct))
                return false;

            var utcNow = DateTimeOffset.UtcNow;
            var user = new UserEntity
            {
                Username = username,
                Password = PasswordHasher.Hash(password),
                IsEnable = true,
                IsDelete = false,
                Remark = remark ?? string.Empty,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };
            var userId = await _db.Insertable(user).ExecuteReturnIdentityAsync();

            // 详情表
            await _db.Insertable(new UserDetailEntity
            {
                UserId = userId,
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }).ExecuteCommandAsync();

            if (roleIds != null)
            {
                var list = roleIds.Distinct().Select(rid => new UserRoleEntity { UserId = userId, RoleId = rid }).ToList();
                if (list.Count > 0)
                    await _db.Insertable(list).ExecuteCommandAsync();
            }
            return true;
        }

        public async Task<List<RoleDto>> GetAllRolesAsync(CancellationToken ct = default)
        {
            var roles = await _db.Queryable<RoleEntity>()
                .Where(r => r.IsDelete != true && r.IsEnable)
                .Select(r => new RoleDto { Id = r.Id, Name = r.Name })
                .ToListAsync(ct);
            return roles;
        }

        // ... 其他方法省略

        public async Task UpdateUserDetailAsync(UserDetailDto dto, DateTimeOffset expectedUpdatedAt, CancellationToken ct = default)
        {
            var utcNow = DateTime.UtcNow;

            // 将 DTO 映射为实体需要更新的列；避免更新 CreatedAt
            var count = await _db.Updateable<UserDetailEntity>()
                .SetColumns(ud => new UserDetailEntity
                {
                    Email = dto.Email,
                    Phone = dto.Phone,
                    Address = dto.Address,
                    Company = dto.Company,
                    Department = dto.Department,
                    Position = dto.Position,
                    Remark = dto.Remark,
                    UserImage = dto.UserImage,
                    PermissionMode = dto.PermissionMode, // 若你的 DTO 已是枚举，去掉强转
                    UpdatedAt = utcNow
                })
                .Where(ud => ud.UserId == dto.UserId && ud.UpdatedAt == expectedUpdatedAt) // 乐观并发
                .ExecuteCommandAsync(ct);

            if (count == 0)
            {
                // 可能是并发冲突或记录不存在
                throw new InvalidOperationException("保存失败：数据已被其他人修改或记录不存在。请刷新后重试。");
            }

            // 成功则把新的 UpdatedAt 回写给调用方
            dto.UpdatedAt = utcNow;
        }
    }

}
