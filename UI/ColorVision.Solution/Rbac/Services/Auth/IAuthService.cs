using ColorVision.Rbac.Dtos;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Rbac.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResultDto?> LoginAndGetDetailAsync(string userName, string password, CancellationToken ct = default);
    }
}
