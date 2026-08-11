using ColorVision.UI;
using log4net;

namespace ColorVision.SocketProtocol
{

    public class SocketInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(SocketInitializer));
        private readonly Func<SocketManager>? _getManager;

        public SocketInitializer()
        {
        }

        internal SocketInitializer(Func<SocketManager> getManager)
        {
            _getManager = getManager ?? throw new ArgumentNullException(nameof(getManager));
        }

        public override string Name => nameof(SocketInitializer);
        public override int Order => 5;

        public override Task InitializeAsync()
        {
            Func<SocketManager> getManager = _getManager ?? SocketManager.GetInstance;
            SocketManager manager = getManager();
            if (manager.Config.IsServerEnabled)
                log.Info("启动通讯协议");
            manager.InitializeServer();
            return Task.CompletedTask;
        }
    }
}
