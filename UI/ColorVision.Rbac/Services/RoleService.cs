using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Exceptions;
using SqlSugar;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 角色服务实现
    /// </summary>
    public class RoleService : IRoleService
    {
        private readonly ISqlSugarClient _db;
        private readonly IAuditLogService _auditLog;

        public RoleService(ISqlSugarClient db, IAuditLogService auditLog)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
            _auditLog = auditLog ?? throw new ArgumentNullException(nameof(auditLog));
        }

        public async Task<List<RoleEntity>> GetAllRolesAsync(CancellationToken ct = default)
        {
            return await _db.Queryable<RoleEntity>()
                .Where(r => r.IsDelete != true && r.IsEnable)
                .OrderBy(r => r.CreatedAt, OrderByType.Desc)
                .ToListAsync(ct);
        }

        public async Task<RoleEntity?> GetRoleByIdAsync(int roleId, CancellationToken ct = default)
        {
            return await _db.Queryable<RoleEntity>()
                .FirstAsync(r => r.Id == roleId && r.IsDelete != true, ct);
        }

        public async Task<RoleEntity?> GetRoleByCodeAsync(string code, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(code))
                return null;

            return await _db.Queryable<RoleEntity>()
                .FirstAsync(r => r.Code == code && r.IsDelete != true, ct);
        }

        public async Task<bool> CreateRoleAsync(string name, string code, string? remark = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code))
                return false;

            // 检查代码是否已存在
            if (await _db.Queryable<RoleEntity>().AnyAsync(r => r.Code == code, ct))
                return false;

            var role = new RoleEntity
            {
                Name = name,
                Code = code,
                Remark = remark ?? string.Empty,
                IsEnable = true,
                IsDelete = false,
                CreatedAt = DateTimeOffset.UtcNow,
                UpdatedAt = DateTimeOffset.UtcNow
            };

            try
            {
                await _db.Insertable(role).ExecuteCommandAsync(ct);
                
                // 记录审计日志
                try
                {
                    await _auditLog.AddAsync(null, null, "role.create", 
                        $"创建角色: {name}({code})");
                }
                catch
                {
                    // 审计日志失败不影响主流程
                }

                return true;
            }
            catch (Exception ex)
            {
                throw new RbacException($"创建角色失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> UpdateRoleAsync(int roleId, string name, string? remark = null, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            var role = await GetRoleByIdAsync(roleId, ct);
            if (role == null)
                throw new RoleNotFoundException(roleId);

            try
            {
                var count = await _db.Updateable<RoleEntity>()
                    .SetColumns(r => new RoleEntity
                    {
                        Name = name,
                        Remark = remark ?? string.Empty,
                        UpdatedAt = DateTimeOffset.UtcNow
                    })
                    .Where(r => r.Id == roleId)
                    .ExecuteCommandAsync(ct);

                if (count > 0)
                {
                    // 记录审计日志
                    try
                    {
                        await _auditLog.AddAsync(null, null, "role.update", 
                            $"更新角色: {role.Code} -> {name}");
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
                throw new RbacException($"更新角色失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> DeleteRoleAsync(int roleId, CancellationToken ct = default)
        {
            var role = await GetRoleByIdAsync(roleId, ct);
            if (role == null)
                throw new RoleNotFoundException(roleId);

            // 检查是否有用户关联此角色
            var userCount = await GetRoleUserCountAsync(roleId, ct);
            if (userCount > 0)
            {
                throw new RbacException($"无法删除角色，还有 {userCount} 个用户关联此角色");
            }

            try
            {
                var count = await _db.Updateable<RoleEntity>()
                    .SetColumns(r => new RoleEntity
                    {
                        IsDelete = true,
                        UpdatedAt = DateTimeOffset.UtcNow
                    })
                    .Where(r => r.Id == roleId)
                    .ExecuteCommandAsync(ct);

                if (count > 0)
                {
                    // 记录审计日志
                    try
                    {
                        await _auditLog.AddAsync(null, null, "role.delete", 
                            $"删除角色: {role.Name}({role.Code})");
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
                throw new RbacException($"删除角色失败: {ex.Message}", ex);
            }
        }

        public async Task<List<PermissionEntity>> GetRolePermissionsAsync(int roleId, CancellationToken ct = default)
        {
            return await _db.Queryable<PermissionEntity>()
                .InnerJoin<RolePermissionEntity>((p, rp) => p.Id == rp.PermissionId)
                .Where((p, rp) => rp.RoleId == roleId && p.IsDelete != true && p.IsEnable)
                .Select(p => p)
                .ToListAsync(ct);
        }

        public async Task<bool> AssignPermissionsToRoleAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken ct = default)
        {
            var role = await GetRoleByIdAsync(roleId, ct);
            if (role == null)
                throw new RoleNotFoundException(roleId);

            await _db.Ado.BeginTranAsync();
            try
            {
                // 删除现有权限关联
                await _db.Deleteable<RolePermissionEntity>()
                    .Where(rp => rp.RoleId == roleId)
                    .ExecuteCommandAsync(ct);

                // 添加新权限关联
                var distinctIds = permissionIds.Distinct().ToList();
                if (distinctIds.Count > 0)
                {
                    var list = distinctIds.Select(pid => new RolePermissionEntity 
                    { 
                        RoleId = roleId, 
                        PermissionId = pid 
                    }).ToList();

                    await _db.Insertable(list).ExecuteCommandAsync(ct);
                }

                await _db.Ado.CommitTranAsync();

                // 记录审计日志
                try
                {
                    await _auditLog.AddAsync(null, null, "role.assign_permissions", 
                        $"为角色 {role.Name} 分配 {distinctIds.Count} 个权限");
                }
                catch
                {
                    // 审计日志失败不影响主流程
                }

                return true;
            }
            catch (Exception ex)
            {
                await _db.Ado.RollbackTranAsync();
                throw new RbacException($"分配权限失败: {ex.Message}", ex);
            }
        }

        public async Task<int> GetRoleUserCountAsync(int roleId, CancellationToken ct = default)
        {
            return await _db.Queryable<UserRoleEntity>()
                .Where(ur => ur.RoleId == roleId)
                .CountAsync(ct);
        }
    }
}
