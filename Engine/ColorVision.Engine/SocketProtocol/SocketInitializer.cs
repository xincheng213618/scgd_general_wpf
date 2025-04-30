using ColorVision.Common.Utilities;
using ColorVision.UI;
using System;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine
{

    public class SocketInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public SocketInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }
        public override string Name => nameof(SocketInitializer);
        public override int Order => 5;

        public override Task InitializeAsync()
        {
            if (SocketConfig.Instance.IsSocketService)
            {
                _messageUpdater.Update("启动通讯协议");
                SocketControl.GetInstance().StartServer();
            }
            SocketConfig.Instance.IsSocketServiceChanged += (s, e) =>
            {
                if (e)
                {
                    SocketControl.GetInstance().StartServer();
                }
                else
                {
                    SocketControl.GetInstance().StopServer();
                }
            };
            return Task.CompletedTask;
        }
    }
}
