using ColorVision.UI;
using System.Threading.Tasks;

namespace Spectrum.Data
{
    public sealed class SpectrumResultConfigReloadInitializer : InitializerBase
    {
        public override int Order => 300;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(ViewResultManager.GetInstance());
            result.ThrowIfFailed("Could not initialize the Spectrum result configuration owner.");
            return Task.CompletedTask;
        }
    }
}
