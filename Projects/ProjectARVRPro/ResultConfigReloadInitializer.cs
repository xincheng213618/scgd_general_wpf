using ColorVision.UI;
using System.Threading.Tasks;

namespace ProjectARVRPro
{
    public sealed class ProjectARVRProResultConfigReloadInitializer : InitializerBase
    {
        public override int Order => 300;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(ViewResultManager.GetInstance());
            result.ThrowIfFailed("Could not initialize the ProjectARVRPro result configuration owner.");
            return Task.CompletedTask;
        }
    }
}
