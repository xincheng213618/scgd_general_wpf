using ColorVision.Rbac.Entity;
using SqlSugar;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 权限检查器实现 - 带缓存的细粒度权限验证
    /// </summary>
    public class PermissionChecker : IPermissionChecker
    {
        private readonly ISqlSugarClient _db;
        
        // 简单的内存缓存（后续可升级为IMemoryCache）
        private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();
        private const int CacheMinutes = 5;

        public PermissionChecker(ISqlSugarClient db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<bool> HasPermissionAsync(int userId, string permissionCode)
        {
            if (string.IsNullOrWhiteSpace(permissionCode))
                return false;

            var userPermissions = await GetUserPermissionCodesAsync(userId);
            return userPermissions.Contains(permissionCode);
        }

        public async Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes)
        {
            if (permissionCodes == null || permissionCodes.Length == 0)
                return false;

            var userPermissions = await GetUserPermissionCodesAsync(userId);
            return permissionCodes.Any(code => userPermissions.Contains(code));
        }

        public async Task<bool> HasAllPermissionsAsync(int userId, params string[] permissionCodes)
        {
            if (permissionCodes == null || permissionCodes.Length == 0)
                return true;

            var userPermissions = await GetUserPermissionCodesAsync(userId);
            return permissionCodes.All(code => userPermissions.Contains(code));
        }

        public async Task<List<string>> GetUserPermissionCodesAsync(int userId)
        {
            // 检查缓存
            if (_cache.TryGetValue(userId, out var cacheEntry))
            {
                if (cacheEntry.ExpiresAt > DateTimeOffset.UtcNow)
                {
                    return cacheEntry.Permissions;
                }
                else
                {
                    // 缓存已过期，移除
                    _cache.TryRemove(userId, out _);
                }
            }

            // 从数据库查询
            var permissions = await QueryUserPermissionsFromDbAsync(userId);

            // 添加到缓存
            var newEntry = new CacheEntry
            {
                Permissions = permissions,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(CacheMinutes)
            };
            _cache.TryAdd(userId, newEntry);

            return permissions;
        }

        private async Task<List<string>> QueryUserPermissionsFromDbAsync(int userId)
        {
            // 第一步：查询用户的所有角色ID
            var roleIds = await _db.Queryable<UserRoleEntity>()
                .Where(ur => ur.UserId == userId)
                .Select(ur => ur.RoleId)
                .ToListAsync();

            if (roleIds.Count == 0)
                return new List<string>();

            // 第二步：查询这些角色的所有权限代码
            var permissions = await _db.Queryable<RolePermissionEntity>()
                .InnerJoin<PermissionEntity>((rp, p) => rp.PermissionId == p.Id)
                .Where((rp, p) => roleIds.Contains(rp.RoleId) && p.IsEnable && p.IsDelete != true)
                .Select((rp, p) => p.Code)
                .Distinct()
                .ToListAsync();

            return permissions;
        }

        public void InvalidateUserCache(int userId)
        {
            _cache.TryRemove(userId, out _);
        }

        public void InvalidateAllCache()
        {
            _cache.Clear();
        }

        /// <summary>
        /// 缓存条目
        /// </summary>
        private class CacheEntry
        {
            public List<string> Permissions { get; set; } = new();
            public DateTimeOffset ExpiresAt { get; set; }
        }
    }
}
