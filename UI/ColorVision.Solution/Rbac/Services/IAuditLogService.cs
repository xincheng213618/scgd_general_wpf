using ColorVision.Rbac.Entity;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 审计日志服务接口
    /// </summary>
    public interface IAuditLogService
    {
        /// <summary>
        /// 添加审计日志
        /// </summary>
        Task<int> AddAsync(int? userId, string? username, string action, string? detail = null, string? ip = null);

        /// <summary>
        /// 查询审计日志
        /// </summary>
        Task<List<AuditLogEntity>> QueryAsync(int top = 200);
    }
}
