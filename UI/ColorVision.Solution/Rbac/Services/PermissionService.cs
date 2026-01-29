using ColorVision.Rbac.Entity;
using SqlSugar;

namespace ColorVision.Rbac.Services
{
    public class PermissionService : IPermissionService
    {
        private readonly ISqlSugarClient _db;
        public PermissionService(ISqlSugarClient db) { _db = db; }

        public async Task EnsureSeedAsync()
        {
            var seeds = new List<PermissionEntity>
            {
                // 用户管理权限
                new PermissionEntity{ Name="创建用户", Code="user.create", Group="User", Remark="创建新用户"},
                new PermissionEntity{ Name="编辑用户", Code="user.edit", Group="User", Remark="编辑用户信息"},
                new PermissionEntity{ Name="删除用户", Code="user.delete", Group="User", Remark="软删除用户"},
                new PermissionEntity{ Name="查看用户", Code="user.view", Group="User", Remark="查看用户列表"},
                new PermissionEntity{ Name="重置密码", Code="user.reset_password", Group="User", Remark="重置用户密码"},
                
                // 角色管理权限
                new PermissionEntity{ Name="创建角色", Code="role.create", Group="Role", Remark="创建新角色"},
                new PermissionEntity{ Name="编辑角色", Code="role.edit", Group="Role", Remark="编辑角色信息"},
                new PermissionEntity{ Name="删除角色", Code="role.delete", Group="Role", Remark="删除角色"},
                new PermissionEntity{ Name="查看角色", Code="role.view", Group="Role", Remark="查看角色列表"},
                new PermissionEntity{ Name="分配权限", Code="role.assign_permissions", Group="Role", Remark="为角色分配权限"},
                
                // 权限管理
                new PermissionEntity{ Name="查看权限", Code="permission.view", Group="Permission", Remark="查看权限列表"},
                new PermissionEntity{ Name="管理权限", Code="permission.manage", Group="Permission", Remark="管理系统权限"},
                
                // 审计日志
                new PermissionEntity{ Name="查看审计日志", Code="audit.view", Group="Audit", Remark="查看审计日志"},
                new PermissionEntity{ Name="导出审计日志", Code="audit.export", Group="Audit", Remark="导出审计日志"},
                
                // 租户管理
                new PermissionEntity{ Name="创建租户", Code="tenant.create", Group="Tenant", Remark="创建新租户"},
                new PermissionEntity{ Name="编辑租户", Code="tenant.edit", Group="Tenant", Remark="编辑租户信息"},
                new PermissionEntity{ Name="查看租户", Code="tenant.view", Group="Tenant", Remark="查看租户列表"},
            };
            var codes = seeds.Select(s=>s.Code).ToList();
            var exists = await _db.Queryable<PermissionEntity>().Where(p=>codes.Contains(p.Code)).Select(p=>p.Code).ToListAsync();
            var toInsert = seeds.Where(s=>!exists.Contains(s.Code)).ToList();
            if (toInsert.Count>0)
                await _db.Insertable(toInsert).ExecuteCommandAsync();
        }

        public Task<List<PermissionEntity>> GetAllAsync() => _db.Queryable<PermissionEntity>().Where(p=>p.IsDelete!=true && p.IsEnable).ToListAsync();

        public async Task<List<string>> GetCodesByRoleIdsAsync(IEnumerable<int> roleIds)
        {
            if (roleIds == null) return new List<string>();
            return await _db.Queryable<RolePermissionEntity, PermissionEntity>((rp,p)=> rp.PermissionId == p.Id)
                .Where((rp,p)=> roleIds.Contains(rp.RoleId) && p.IsEnable && p.IsDelete!=true)
                .Select((rp,p)=> p.Code)
                .Distinct()
                .ToListAsync();
        }

        public async Task<Dictionary<string, List<PermissionEntity>>> GetPermissionsByGroupAsync()
        {
            var permissions = await GetAllAsync();
            return permissions
                .GroupBy(p => p.Group ?? "其他")
                .OrderBy(g => g.Key)
                .ToDictionary(g => g.Key, g => g.OrderBy(p => p.Code).ToList());
        }
    }
}
