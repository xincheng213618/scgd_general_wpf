using SqlSugar;

namespace ColorVision.Rbac.Services
{
    public class PermissionService
    {
        private readonly ISqlSugarClient _db;
        public PermissionService(ISqlSugarClient db) { _db = db; }

        public async Task EnsureSeedAsync()
        {
            var seeds = new List<PermissionEntity>
            {
                new PermissionEntity{ Name="创建用户", Code="user.create", Group="User", Remark="创建新用户"},
                new PermissionEntity{ Name="编辑用户", Code="user.edit", Group="User", Remark="编辑用户"},
                new PermissionEntity{ Name="删除用户", Code="user.delete", Group="User", Remark="软删除用户"},
                new PermissionEntity{ Name="查看用户", Code="user.view", Group="User", Remark="查看用户列表"},
                new PermissionEntity{ Name="查看审计日志", Code="audit.view", Group="Audit", Remark="查看审计日志"},
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
    }
}
