using ColorVision.Rbac.Entity;
using ColorVision.Rbac.Exceptions;
using SqlSugar;
using System.Security.Cryptography;

namespace ColorVision.Rbac.Services
{
    /// <summary>
    /// 会话服务实现
    /// </summary>
    public class SessionService : ISessionService
    {
        private readonly ISqlSugarClient _db;
        private const int DefaultSessionHours = 24;

        public SessionService(ISqlSugarClient db)
        {
            _db = db ?? throw new ArgumentNullException(nameof(db));
        }

        public async Task<string> CreateSessionAsync(int userId, string? deviceInfo = null, string? ipAddress = null, TimeSpan? expiration = null)
        {
            var sessionToken = GenerateSecureToken();
            var expirationTime = expiration ?? TimeSpan.FromHours(DefaultSessionHours);

            var session = new SessionEntity
            {
                UserId = userId,
                SessionToken = sessionToken,
                DeviceInfo = deviceInfo ?? GetDefaultDeviceInfo(),
                IpAddress = ipAddress ?? "unknown",
                CreatedAt = DateTimeOffset.UtcNow,
                ExpiresAt = DateTimeOffset.UtcNow.Add(expirationTime),
                LastActivityAt = DateTimeOffset.UtcNow,
                IsRevoked = false
            };

            try
            {
                await _db.Insertable(session).ExecuteCommandAsync();
                return sessionToken;
            }
            catch (Exception ex)
            {
                throw new RbacException($"创建会话失败: {ex.Message}", ex);
            }
        }

        public async Task<bool> ValidateSessionAsync(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                return false;

            var session = await _db.Queryable<SessionEntity>()
                .FirstAsync(s => s.SessionToken == sessionToken && !s.IsRevoked);

            if (session == null)
                return false;

            if (session.ExpiresAt < DateTimeOffset.UtcNow)
            {
                // 会话已过期，撤销它
                await RevokeSessionAsync(sessionToken);
                return false;
            }

            // 会话有效，更新最后活动时间
            await UpdateSessionActivityAsync(sessionToken);

            return true;
        }

        public async Task<int?> GetUserIdFromSessionAsync(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                return null;

            var session = await _db.Queryable<SessionEntity>()
                .FirstAsync(s => s.SessionToken == sessionToken && !s.IsRevoked);

            if (session == null || session.ExpiresAt < DateTimeOffset.UtcNow)
                return null;

            return session.UserId;
        }

        public async Task RevokeSessionAsync(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                return;

            try
            {
                await _db.Updateable<SessionEntity>()
                    .SetColumns(s => new SessionEntity { IsRevoked = true })
                    .Where(s => s.SessionToken == sessionToken)
                    .ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                throw new RbacException($"撤销会话失败: {ex.Message}", ex);
            }
        }

        public async Task RevokeAllUserSessionsAsync(int userId)
        {
            try
            {
                await _db.Updateable<SessionEntity>()
                    .SetColumns(s => new SessionEntity { IsRevoked = true })
                    .Where(s => s.UserId == userId && !s.IsRevoked)
                    .ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                throw new RbacException($"撤销用户所有会话失败: {ex.Message}", ex);
            }
        }

        public async Task UpdateSessionActivityAsync(string sessionToken)
        {
            if (string.IsNullOrWhiteSpace(sessionToken))
                return;

            try
            {
                await _db.Updateable<SessionEntity>()
                    .SetColumns(s => new SessionEntity { LastActivityAt = DateTimeOffset.UtcNow })
                    .Where(s => s.SessionToken == sessionToken)
                    .ExecuteCommandAsync();
            }
            catch
            {
                // 更新活动时间失败不应影响主流程
            }
        }

        public async Task<List<SessionEntity>> GetUserActiveSessionsAsync(int userId)
        {
            var now = DateTimeOffset.UtcNow;
            return await _db.Queryable<SessionEntity>()
                .Where(s => s.UserId == userId && !s.IsRevoked && s.ExpiresAt > now)
                .OrderBy(s => s.LastActivityAt, OrderByType.Desc)
                .ToListAsync();
        }

        public async Task CleanupExpiredSessionsAsync()
        {
            try
            {
                var now = DateTimeOffset.UtcNow;
                await _db.Updateable<SessionEntity>()
                    .SetColumns(s => new SessionEntity { IsRevoked = true })
                    .Where(s => !s.IsRevoked && s.ExpiresAt < now)
                    .ExecuteCommandAsync();
            }
            catch (Exception ex)
            {
                throw new RbacException($"清理过期会话失败: {ex.Message}", ex);
            }
        }

        private string GenerateSecureToken()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        private string GetDefaultDeviceInfo()
        {
            return $"{Environment.OSVersion.Platform} - {Environment.MachineName}";
        }
    }
}
