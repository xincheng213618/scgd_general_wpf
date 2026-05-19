using ColorVision.UI;
using log4net;
using ProjectARVRPro.Services;
using System.Threading.Tasks;

namespace ProjectARVRPro.PluginConfig
{
    public class SocketRelayInitializer : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketRelayInitializer));

        public override int Order => 101;

        public override Task Initialize()
        {
            var relayManager = SocketRelayManager.GetInstance();
            var relayConfig = SocketRelayConfig.Instance;
            if (!relayConfig.AutoStart || relayManager.IsListening)
            {
                return Task.CompletedTask;
            }

            try
            {
                relayManager.StartServer(relayConfig.ListenIP, relayConfig.ListenPort);
                log.Info($"中转服务器自动启动: {relayConfig.ListenIP}:{relayConfig.ListenPort}");
            }
            catch (Exception ex)
            {
                log.Error("中转服务器自动启动失败", ex);
            }

            return Task.CompletedTask;
        }
    }
}