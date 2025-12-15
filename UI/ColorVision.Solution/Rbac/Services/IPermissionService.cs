using ColorVision.Rbac.Entity;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 权限服务接口
    /// </summary>
    public interface IPermissionService
    {
        /// <summary>
        /// 确保种子权限数据存在
        /// </summary>
        Task EnsureSeedAsync();

        /// <summary>
        /// 获取所有启用的权限
        /// </summary>
        Task<List<PermissionEntity>> GetAllAsync();

        /// <summary>
        /// 根据角色ID列表获取权限代码
        /// </summary>
        Task<List<string>> GetCodesByRoleIdsAsync(IEnumerable<int> roleIds);

        /// <summary>
        /// 获取按组分类的权限
        /// </summary>
        Task<Dictionary<string, List<PermissionEntity>>> GetPermissionsByGroupAsync();
    }
}
