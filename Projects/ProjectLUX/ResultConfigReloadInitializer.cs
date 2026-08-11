using ColorVision.UI;
using System.Threading.Tasks;

namespace ProjectLUX
{
    public sealed class ProjectLUXResultConfigReloadInitializer : InitializerBase
    {
        public override int Order => 300;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(ViewResultManager.GetInstance());
            result.ThrowIfFailed("Could not initialize the ProjectLUX result configuration owner.");
            return Task.CompletedTask;
        }
    }
}
