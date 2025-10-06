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
                new PermissionEntity{ Name="�����û�", Code="user.create", Group="User", Remark="�������û�"},
                new PermissionEntity{ Name="�༭�û�", Code="user.edit", Group="User", Remark="�༭�û�"},
                new PermissionEntity{ Name="ɾ���û�", Code="user.delete", Group="User", Remark="��ɾ���û�"},
                new PermissionEntity{ Name="�鿴�û�", Code="user.view", Group="User", Remark="�鿴�û��б�"},
                new PermissionEntity{ Name="�鿴�����־", Code="audit.view", Group="Audit", Remark="�鿴�����־"},
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
