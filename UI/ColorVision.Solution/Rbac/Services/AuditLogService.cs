using SqlSugar;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System;

namespace ColorVision.Rbac.Services
{
    public class AuditLogService
    {
        private readonly ISqlSugarClient _db;
        public AuditLogService(ISqlSugarClient db) { _db = db; }

        public Task<int> AddAsync(int? userId, string? username, string action, string? detail = null, string? ip=null)
        {
            return _db.Insertable(new AuditLogEntity{ UserId=userId, Username=username, Action=action, Detail=detail, Ip=ip, CreatedAt=DateTimeOffset.UtcNow}).ExecuteReturnIdentityAsync();
        }

        public Task<List<AuditLogEntity>> QueryAsync(int top = 200)
        {
            return _db.Queryable<AuditLogEntity>().OrderBy(x=>x.Id, OrderByType.Desc).Take(top).ToListAsync();
        }
    }
}
