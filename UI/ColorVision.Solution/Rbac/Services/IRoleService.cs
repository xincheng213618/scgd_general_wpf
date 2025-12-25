using ColorVision.Rbac.Entity;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 角色服务接口 - 提供角色的CRUD操作和权限管理
    /// </summary>
    public interface IRoleService
    {
        /// <summary>
        /// 获取所有启用的角色
        /// </summary>
        Task<List<RoleEntity>> GetAllRolesAsync(CancellationToken ct = default);

        /// <summary>
        /// 根据ID获取角色
        /// </summary>
        Task<RoleEntity?> GetRoleByIdAsync(int roleId, CancellationToken ct = default);

        /// <summary>
        /// 根据代码获取角色
        /// </summary>
        Task<RoleEntity?> GetRoleByCodeAsync(string code, CancellationToken ct = default);

        /// <summary>
        /// 创建新角色
        /// </summary>
        Task<bool> CreateRoleAsync(string name, string code, string? remark = null, CancellationToken ct = default);

        /// <summary>
        /// 更新角色信息
        /// </summary>
        Task<bool> UpdateRoleAsync(int roleId, string name, string? remark = null, CancellationToken ct = default);

        /// <summary>
        /// 删除角色（软删除）
        /// </summary>
        Task<bool> DeleteRoleAsync(int roleId, CancellationToken ct = default);

        /// <summary>
        /// 获取角色的所有权限
        /// </summary>
        Task<List<PermissionEntity>> GetRolePermissionsAsync(int roleId, CancellationToken ct = default);

        /// <summary>
        /// 为角色分配权限
        /// </summary>
        Task<bool> AssignPermissionsToRoleAsync(int roleId, IEnumerable<int> permissionIds, CancellationToken ct = default);

        /// <summary>
        /// 获取角色的用户数量
        /// </summary>
        Task<int> GetRoleUserCountAsync(int roleId, CancellationToken ct = default);
    }
}
