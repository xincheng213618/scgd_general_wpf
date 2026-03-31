#pragma warning disable 
using ColorVision.Rbac.Dtos;
using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Security;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    public interface IUserService
    {
        Task UpdateUserDetailAsync(UserDetailDto dto, DateTimeOffset expectedUpdatedAt, CancellationToken ct = default);
        Task<bool> CreateUserAsync(string username, string password, string? remark = null, IEnumerable<int>? roleIds = null, CancellationToken ct = default);
        Task<List<RoleDto>> GetAllRolesAsync(CancellationToken ct = default);

        Task<bool> DeleteUserAsync(int userId, CancellationToken ct = default);
        Task<bool> EnableUserAsync(int userId, CancellationToken ct = default);
        Task<bool> DisableUserAsync(int userId, CancellationToken ct = default);
        Task<string?> ResetUserPasswordAsync(int userId, CancellationToken ct = default);
        Task<bool> UpdateUserRolesAsync(int userId, IEnumerable<int> roleIds, CancellationToken ct = default);
        Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken ct = default);
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

        public async Task UpdateUserDetailAsync(UserDetailDto dto, DateTimeOffset expectedUpdatedAt, CancellationToken ct = default)
        {
            var utcNow = DateTimeOffset.UtcNow;

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
                    PermissionMode = dto.PermissionMode,
                    UpdatedAt = utcNow
                })
                .Where(ud => ud.UserId == dto.UserId && ud.UpdatedAt == expectedUpdatedAt)
                .ExecuteCommandAsync(ct);

            if (count == 0)
            {
                throw new InvalidOperationException("保存失败：数据已被其他人修改或记录不存在。请刷新后重试。");
            }

            dto.UpdatedAt = utcNow;
        }

        public async Task<bool> DeleteUserAsync(int userId, CancellationToken ct = default)
        {
            var count = await _db.Updateable<UserEntity>()
                .SetColumns(u => new UserEntity { IsDelete = true })
                .Where(u => u.Id == userId)
                .ExecuteCommandAsync(ct);
            return count > 0;
        }

        public async Task<bool> EnableUserAsync(int userId, CancellationToken ct = default)
        {
            var count = await _db.Updateable<UserEntity>()
                .SetColumns(u => new UserEntity { IsEnable = true })
                .Where(u => u.Id == userId)
                .ExecuteCommandAsync(ct);
            return count > 0;
        }

        public async Task<bool> DisableUserAsync(int userId, CancellationToken ct = default)
        {
            var count = await _db.Updateable<UserEntity>()
                .SetColumns(u => new UserEntity { IsEnable = false })
                .Where(u => u.Id == userId)
                .ExecuteCommandAsync(ct);
            return count > 0;
        }

        public async Task<string?> ResetUserPasswordAsync(int userId, CancellationToken ct = default)
        {
            string newPassword = GenerateRandomPassword();
            string hashedPassword = PasswordHasher.Hash(newPassword);

            var count = await _db.Updateable<UserEntity>()
                .SetColumns(u => new UserEntity { Password = hashedPassword })
                .Where(u => u.Id == userId)
                .ExecuteCommandAsync(ct);

            return count > 0 ? newPassword : null;
        }

        public async Task<bool> UpdateUserRolesAsync(int userId, IEnumerable<int> roleIds, CancellationToken ct = default)
        {
            await _db.Deleteable<UserRoleEntity>().Where(ur => ur.UserId == userId).ExecuteCommandAsync(ct);
            if (roleIds != null)
            {
                var list = roleIds.Distinct().Select(rid => new UserRoleEntity { UserId = userId, RoleId = rid }).ToList();
                if (list.Count > 0)
                    await _db.Insertable(list).ExecuteCommandAsync(ct);
            }
            return true;
        }

        public async Task<bool> ChangePasswordAsync(int userId, string oldPassword, string newPassword, CancellationToken ct = default)
        {
            var user = await _db.Queryable<UserEntity>().FirstAsync(u => u.Id == userId && u.IsDelete != true, ct);
            if (user == null) return false;

            // 验证旧密码
            if (!PasswordHasher.Verify(oldPassword, user.Password, out _))
                return false;

            // 更新为新密码
            var hashedPassword = PasswordHasher.Hash(newPassword);
            var count = await _db.Updateable<UserEntity>()
                .SetColumns(u => new UserEntity { Password = hashedPassword, UpdatedAt = DateTimeOffset.UtcNow })
                .Where(u => u.Id == userId)
                .ExecuteCommandAsync(ct);

            return count > 0;
        }

        private static string GenerateRandomPassword()
        {
            const string chars = "ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz23456789";
            byte[] randomBytes = new byte[8];
            RandomNumberGenerator.Fill(randomBytes);
            return new string(Enumerable.Range(0, 8)
                .Select(i => chars[randomBytes[i] % chars.Length]).ToArray());
        }
    }
}
