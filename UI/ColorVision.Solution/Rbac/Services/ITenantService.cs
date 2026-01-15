using ColorVision.Rbac.Entity;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 租户服务接口 - 提供多租户管理功能
    /// </summary>
    public interface ITenantService
    {
        /// <summary>
        /// 获取所有启用的租户
        /// </summary>
        Task<List<TenantEntity>> GetAllTenantsAsync(CancellationToken ct = default);

        /// <summary>
        /// 根据ID获取租户
        /// </summary>
        Task<TenantEntity?> GetTenantByIdAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// 根据代码获取租户
        /// </summary>
        Task<TenantEntity?> GetTenantByCodeAsync(string code, CancellationToken ct = default);

        /// <summary>
        /// 创建新租户
        /// </summary>
        Task<bool> CreateTenantAsync(string name, string code, string? remark = null, CancellationToken ct = default);

        /// <summary>
        /// 更新租户信息
        /// </summary>
        Task<bool> UpdateTenantAsync(int tenantId, string name, string? remark = null, CancellationToken ct = default);

        /// <summary>
        /// 删除租户（软删除）
        /// </summary>
        Task<bool> DeleteTenantAsync(int tenantId, CancellationToken ct = default);

        /// <summary>
        /// 将用户分配到租户
        /// </summary>
        Task<bool> AssignUserToTenantAsync(int userId, int tenantId, CancellationToken ct = default);

        /// <summary>
        /// 从租户移除用户
        /// </summary>
        Task<bool> RemoveUserFromTenantAsync(int userId, int tenantId, CancellationToken ct = default);

        /// <summary>
        /// 获取用户的所有租户
        /// </summary>
        Task<List<TenantEntity>> GetUserTenantsAsync(int userId, CancellationToken ct = default);

        /// <summary>
        /// 获取租户的所有用户
        /// </summary>
        Task<List<UserEntity>> GetTenantUsersAsync(int tenantId, CancellationToken ct = default);
    }
}
