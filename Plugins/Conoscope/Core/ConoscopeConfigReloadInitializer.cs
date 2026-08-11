using ColorVision.UI;
using System.Threading.Tasks;

namespace Conoscope.Core
{
    public sealed class ConoscopeConfigReloadInitializer : InitializerBase
    {
        public override int Order => 200;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(ConoscopeManager.GetInstance());
            result.ThrowIfFailed("Could not initialize the Conoscope runtime configuration owner.");
            return Task.CompletedTask;
        }
    }
}
