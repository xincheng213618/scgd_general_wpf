using ColorVision.UI;
using log4net;

namespace ColorVision.SocketProtocol
{

    public class SocketInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketInitializer));

        public override string Name => nameof(SocketInitializer);
        public override int Order => 5;

        public override Task InitializeAsync()
        {
            if (SocketConfig.Instance.IsServerEnabled )
            {
                log.Info("启动通讯协议");
                SocketManager.GetInstance().StartServer();
            }
            SocketConfig.Instance.ServerEnabledChanged += (s, e) =>
            {
                if (e)
                {
                    SocketManager.GetInstance().StartServer();
                }
                else
                {
                    SocketManager.GetInstance().StopServer();
                }
            };
            return Task.CompletedTask;
        }
    }
}
