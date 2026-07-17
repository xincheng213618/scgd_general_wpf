using ColorVision.Rbac.Dtos;

namespace ColorVision.Rbac.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResultDto?> LoginAndGetDetailAsync(string userName, string password, CancellationToken ct = default);

        /// <summary>
        /// 通过 SessionToken 恢复登录状态（用于自动登录）
        /// </summary>
        Task<LoginResultDto?> LoginBySessionTokenAsync(string sessionToken, CancellationToken ct = default);
    }
}
