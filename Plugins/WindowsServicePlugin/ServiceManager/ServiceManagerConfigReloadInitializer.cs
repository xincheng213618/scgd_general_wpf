using ColorVision.UI;
using System.Threading.Tasks;

namespace WindowsServicePlugin.ServiceManager
{
    public sealed class ServiceManagerConfigReloadInitializer : InitializerBase
    {
        public override int Order => 400;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(ServiceManagerViewModel.Instance);
            result.ThrowIfFailed("Could not initialize the Windows service configuration owner.");
            return Task.CompletedTask;
        }
    }
}
