using ColorVision.Rbac.Dtos;

namespace ColorVision.Rbac.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResultDto?> LoginAndGetDetailAsync(string userName, string password, CancellationToken ct = default);
    }
}
