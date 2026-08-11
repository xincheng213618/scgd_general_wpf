using ColorVision.UI;
using System.Threading.Tasks;

namespace ProjectKB
{
    public sealed class ProjectKBResultConfigReloadInitializer : InitializerBase
    {
        public override int Order => 300;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(ViewResultManager.GetInstance());
            result.ThrowIfFailed("Could not initialize the ProjectKB result configuration owner.");
            return Task.CompletedTask;
        }
    }
}
