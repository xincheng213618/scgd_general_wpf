using ColorVision.UI;
using log4net;

namespace ColorVision.SocketProtocol
{

    public class SocketInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketInitializer));
        private readonly SocketConfig? _config;
        private readonly Func<SocketManager>? _getManager;

        public SocketInitializer()
        {
        }

        internal SocketInitializer(SocketConfig config, Func<SocketManager> getManager)
        {
            _config = config;
            _getManager = getManager;
        }

        public override string Name => nameof(SocketInitializer);
        public override int Order => 5;

        public override Task InitializeAsync()
        {
            SocketConfig config = _config ?? SocketConfig.Instance;
            Func<SocketManager> getManager = _getManager ?? SocketManager.GetInstance;
            if (config.IsServerEnabled)
            {
                log.Info("启动通讯协议");
                getManager().StartServer();
            }
            config.ServerEnabledChanged += (s, e) =>
            {
                if (e)
                {
                    getManager().StartServer();
                }
                else
                {
                    getManager().StopServer();
                }
            };
            return Task.CompletedTask;
        }
    }
}
