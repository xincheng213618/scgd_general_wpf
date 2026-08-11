using ColorVision.UI;
using System.Threading.Tasks;

namespace ColorVision.Engine.Messages
{
    public sealed class MessagesConfigReloadInitializer : InitializerBase
    {
        public override int Order => 100;

        public override Task InitializeAsync()
        {
            ConfigReloadResult result = ConfigHandler.GetInstance()
                .RegisterReloadParticipants(MessagesListManager.GetInstance());
            result.ThrowIfFailed("Could not initialize the message database configuration owner.");
            return Task.CompletedTask;
        }
    }
}
