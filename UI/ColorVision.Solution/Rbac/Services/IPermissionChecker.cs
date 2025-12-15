using System.Collections.Generic;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 权限检查器接口 - 提供细粒度的权限验证
    /// </summary>
    public interface IPermissionChecker
    {
        /// <summary>
        /// 检查用户是否拥有指定权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="permissionCode">权限代码（如：user.create）</param>
        Task<bool> HasPermissionAsync(int userId, string permissionCode);

        /// <summary>
        /// 检查用户是否拥有任意一个权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="permissionCodes">权限代码列表</param>
        Task<bool> HasAnyPermissionAsync(int userId, params string[] permissionCodes);

        /// <summary>
        /// 检查用户是否拥有所有权限
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="permissionCodes">权限代码列表</param>
        Task<bool> HasAllPermissionsAsync(int userId, params string[] permissionCodes);

        /// <summary>
        /// 获取用户的所有权限代码
        /// </summary>
        /// <param name="userId">用户ID</param>
        Task<List<string>> GetUserPermissionCodesAsync(int userId);

        /// <summary>
        /// 清除用户的权限缓存
        /// </summary>
        /// <param name="userId">用户ID</param>
        void InvalidateUserCache(int userId);

        /// <summary>
        /// 清除所有权限缓存
        /// </summary>
        void InvalidateAllCache();
    }
}
