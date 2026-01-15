using ColorVision.Rbac.Entity;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 会话服务接口 - 提供会话管理和Token验证
    /// </summary>
    public interface ISessionService
    {
        /// <summary>
        /// 创建新会话并返回会话Token
        /// </summary>
        /// <param name="userId">用户ID</param>
        /// <param name="deviceInfo">设备信息</param>
        /// <param name="ipAddress">IP地址</param>
        /// <param name="expiration">过期时间（默认24小时）</param>
        Task<string> CreateSessionAsync(int userId, string? deviceInfo = null, string? ipAddress = null, TimeSpan? expiration = null);

        /// <summary>
        /// 验证会话Token是否有效
        /// </summary>
        Task<bool> ValidateSessionAsync(string sessionToken);

        /// <summary>
        /// 从会话Token获取用户ID
        /// </summary>
        Task<int?> GetUserIdFromSessionAsync(string sessionToken);

        /// <summary>
        /// 撤销指定会话
        /// </summary>
        Task RevokeSessionAsync(string sessionToken);

        /// <summary>
        /// 撤销用户的所有会话
        /// </summary>
        Task RevokeAllUserSessionsAsync(int userId);

        /// <summary>
        /// 更新会话的最后活动时间
        /// </summary>
        Task UpdateSessionActivityAsync(string sessionToken);

        /// <summary>
        /// 获取用户的所有活动会话
        /// </summary>
        Task<List<SessionEntity>> GetUserActiveSessionsAsync(int userId);

        /// <summary>
        /// 清理过期的会话
        /// </summary>
        Task CleanupExpiredSessionsAsync();
    }
}
