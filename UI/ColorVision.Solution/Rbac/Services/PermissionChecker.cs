using ColorVision.Rbac.Entity;
using SqlSugar;
using System.Collections.Concurrent;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 权限检查器实现 - 带LRU缓存的细粒度权限验证
    /// </summary>
    public class PermissionChecker : IPermissionChecker
    {
        private readonly ISqlSugarClient _db;
        
        // LRU缓存实现
        private readonly ConcurrentDictionary<int, CacheEntry> _cache = new();
        private readonly ConcurrentDictionary<int, DateTimeOffset> _accessTimes = new();
        private const int CacheMinutes = 5;
        private const int MaxCacheSize = 1000; // 最大缓存1000个用户的权限

        // 缓存统计
        private long _cacheHits = 0;
        private long _cacheMisses = 0;

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
                    // 缓存命中
                    System.Threading.Interlocked.Increment(ref _cacheHits);
                    _accessTimes[userId] = DateTimeOffset.UtcNow;
                    return cacheEntry.Permissions;
                }
                else
                {
                    // 缓存已过期，移除
                    _cache.TryRemove(userId, out _);
                    _accessTimes.TryRemove(userId, out _);
                }
            }

            // 缓存未命中
            System.Threading.Interlocked.Increment(ref _cacheMisses);

            // 从数据库查询
            var permissions = await QueryUserPermissionsFromDbAsync(userId);

            // LRU驱逐策略：如果缓存已满，移除最旧的条目
            if (_cache.Count >= MaxCacheSize)
            {
                EvictLeastRecentlyUsed();
            }

            // 添加到缓存
            var newEntry = new CacheEntry
            {
                Permissions = permissions,
                ExpiresAt = DateTimeOffset.UtcNow.AddMinutes(CacheMinutes)
            };
            _cache.TryAdd(userId, newEntry);
            _accessTimes[userId] = DateTimeOffset.UtcNow;

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
            _accessTimes.TryRemove(userId, out _);
        }

        public void InvalidateAllCache()
        {
            _cache.Clear();
            _accessTimes.Clear();
            _cacheHits = 0;
            _cacheMisses = 0;
        }

        /// <summary>
        /// LRU驱逐：移除最少使用的10%缓存项
        /// </summary>
        private void EvictLeastRecentlyUsed()
        {
            try
            {
                var evictCount = Math.Max(1, MaxCacheSize / 10); // 移除10%
                var oldestEntries = _accessTimes
                    .OrderBy(kvp => kvp.Value)
                    .Take(evictCount)
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var userId in oldestEntries)
                {
                    _cache.TryRemove(userId, out _);
                    _accessTimes.TryRemove(userId, out _);
                }
            }
            catch
            {
                // 驱逐失败不应影响主流程
            }
        }

        /// <summary>
        /// 获取缓存统计信息
        /// </summary>
        public CacheStatistics GetCacheStatistics()
        {
            var totalRequests = _cacheHits + _cacheMisses;
            var hitRate = totalRequests > 0 ? (double)_cacheHits / totalRequests * 100 : 0;

            return new CacheStatistics
            {
                CacheHits = _cacheHits,
                CacheMisses = _cacheMisses,
                HitRate = hitRate,
                CachedItemsCount = _cache.Count,
                MaxCacheSize = MaxCacheSize
            };
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

    /// <summary>
    /// 缓存统计信息
    /// </summary>
    public class CacheStatistics
    {
        public long CacheHits { get; set; }
        public long CacheMisses { get; set; }
        public double HitRate { get; set; }
        public int CachedItemsCount { get; set; }
        public int MaxCacheSize { get; set; }

        public override string ToString()
        {
            return $"命中率: {HitRate:F2}%, 命中: {CacheHits}, 未命中: {CacheMisses}, 缓存项: {CachedItemsCount}/{MaxCacheSize}";
        }
    }
}
