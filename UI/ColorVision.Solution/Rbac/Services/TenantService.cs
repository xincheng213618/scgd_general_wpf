using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Exceptions;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 租户服务实现
    /// </summary>
    public class TenantService : ITenantService
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuditLogService _auditLog;

        public TenantService(ISqlSugarClient db, IAuditLogService auditLog)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        }

        public async Task<List<TenantEntity>> GetAllTenantsAsync(CancellationToken ct = default)
        {
            return await _db.Queryable<TenantEntity>()
                .Where(t => t.IsEnable)
                .OrderBy(t => t.CreatedAt, OrderByType.Desc)
                .ToListAsync(ct);
        }

        public async Task<TenantEntity?> GetTenantByIdAsync(int tenantId, CancellationToken ct = default)
        {
            return await _db.Queryable<TenantEntity>()
                .FirstAsync(t => t.Id == tenantId, ct);
        }

        public async Task<TenantEntity?> GetTenantByCodeAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            return await _db.Queryable<TenantEntity>()
                .FirstAsync(t => t.Code == code, ct);
        }

        public async Task<bool> CreateTenantAsync(string name, string code, string? remark = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
                return false;

            // 检查代码是否已存在
            if (await _db.Queryable<TenantEntity>().AnyAsync(t => t.Code == code, ct))
                return false;

            var tenant = new TenantEntity
            {
                Name = name,
                Code = code,
                Remark = remark,
                IsEnable = true,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                await _db.Insertable(tenant).ExecuteCommandAsync(ct);

                // 记录审计日志
                try
                {
                    await _auditLog.AddAsync(null, null, "tenant.create",
                        $"创建租户: {name}({code})");
                }
                catch
                {
                    // 审计日志失败不影响主流程
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new RbacException($"创建租户失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateTenantAsync(int tenantId, string name, string? remark = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var tenant = await GetTenantByIdAsync(tenantId, ct);
            if (tenant == null)
                return false;

            try
            {
                var count = await _db.Updateable<TenantEntity>()
                    .SetColumns(t => new TenantEntity
                    {
                        Name = name,
                        Remark = remark,
                        UpdatedAt = DateTimeOffset.UtcNow
                    })
                    .Where(t => t.Id == tenantId)
                    .ExecuteCommandAsync(ct);

                if (count > 0)
                {
                    // 记录审计日志
                    try
                    {
                        await _auditLog.AddAsync(null, null, "tenant.update",
                            $"更新租户: {tenant.Code} -> {name}");
                    }
                    catch
                    {
                        // 审计日志失败不影响主流程
                    }
                }

                return count > 0;
            }
            catch (Exception ex)
            {
                throw new RbacException($"更新租户失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteTenantAsync(int tenantId, CancellationToken ct = default)
        {
            var tenant = await GetTenantByIdAsync(tenantId, ct);
            if (tenant == null)
                return false;

            // 检查是否有用户关联此租户
            var userCount = await _db.Queryable<UserTenantEntity>()
                .Where(ut => ut.TenantId == tenantId)
                .CountAsync(ct);

            if (userCount > 0)
            {
                throw new RbacException($"无法删除租户，还有 {userCount} 个用户关联此租户");
            }

            try
            {
                var count = await _db.Updateable<TenantEntity>()
                    .SetColumns(t => new TenantEntity
                    {
                        IsEnable = false,
                        UpdatedAt = DateTimeOffset.UtcNow
                    })
                    .Where(t => t.Id == tenantId)
                    .ExecuteCommandAsync(ct);

                if (count > 0)
                {
                    // 记录审计日志
                    try
                    {
                        await _auditLog.AddAsync(null, null, "tenant.delete",
                            $"删除租户: {tenant.Name}({tenant.Code})");
                    }
                    catch
                    {
                        // 审计日志失败不影响主流程
                    }
                }

                return count > 0;
            }
            catch (Exception ex)
            {
                throw new RbacException($"删除租户失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> AssignUserToTenantAsync(int userId, int tenantId, CancellationToken ct = default)
        {
            // 检查是否已存在
            if (await _db.Queryable<UserTenantEntity>()
                .AnyAsync(ut => ut.UserId == userId && ut.TenantId == tenantId, ct))
                return false;

            try
            {
                await _db.Insertable(new UserTenantEntity
                {
                    UserId = userId,
                    TenantId = tenantId
                }).ExecuteCommandAsync(ct);

                // 记录审计日志
                try
                {
                    await _auditLog.AddAsync(userId, null, "tenant.assign_user",
                        $"用户 {userId} 加入租户 {tenantId}");
                }
                catch
                {
                    // 审计日志失败不影响主流程
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new RbacException($"分配用户到租户失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> RemoveUserFromTenantAsync(int userId, int tenantId, CancellationToken ct = default)
        {
            try
            {
                var count = await _db.Deleteable<UserTenantEntity>()
                    .Where(ut => ut.UserId == userId && ut.TenantId == tenantId)
                    .ExecuteCommandAsync(ct);

                if (count > 0)
                {
                    // 记录审计日志
                    try
                    {
                        await _auditLog.AddAsync(userId, null, "tenant.remove_user",
                            $"用户 {userId} 离开租户 {tenantId}");
                    }
                    catch
                    {
                        // 审计日志失败不影响主流程
                    }
                }

                return count > 0;
            }
            catch (Exception ex)
            {
                throw new RbacException($"从租户移除用户失败: {ex.Message}", ex);
            }
        }

        public async Task<List<TenantEntity>> GetUserTenantsAsync(int userId, CancellationToken ct = default)
        {
            return await _db.Queryable<TenantEntity>()
                .InnerJoin<UserTenantEntity>((t, ut) => t.Id == ut.TenantId)
                .Where((t, ut) => ut.UserId == userId && t.IsEnable)
                .Select(t => t)
                .ToListAsync(ct);
        }

        public async Task<List<UserEntity>> GetTenantUsersAsync(int tenantId, CancellationToken ct = default)
        {
            return await _db.Queryable<UserEntity>()
                .InnerJoin<UserTenantEntity>((u, ut) => u.Id == ut.UserId)
                .Where((u, ut) => ut.TenantId == tenantId && u.IsDelete != true)
                .Select(u => u)
                .ToListAsync(ct);
        }
    }
}
