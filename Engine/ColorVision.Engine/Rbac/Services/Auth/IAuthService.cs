using ColorVision.Engine.Rbac.Dtos;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Auth
{
    public interface IAuthService
    {
        Task<LoginResultDto?> LoginAndGetDetailAsync(string userName, string password, CancellationToken ct = default);
    }
}
