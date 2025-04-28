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
        public override int Order => 1;

        public override Task InitializeAsync()
        {
            _messageUpdater.Update("启动通讯协议");
            SocketControl.GetInstance();
            return Task.CompletedTask;
        }
    }
}
