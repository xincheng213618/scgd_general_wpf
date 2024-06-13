using ColorVision.Common.MVVM;
using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    public class RCInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public RCInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 4;
        public async Task InitializeAsync()
        {
            _messageUpdater.UpdateMessage("正在尝试连接注册中心");
            bool isConnect = await MQTTRCService.GetInstance().Connect();
            if (!isConnect)
            {
                _messageUpdater.UpdateMessage("检测是否本地服务");
                if (!RCManager.GetInstance().IsLocalServiceRunning())
                {
                    if (RCManagerConfig.Instance.IsOpenCVWinSMS)
                    {
                        _messageUpdater.UpdateMessage("打开本地服务管理");
                        RCManager.GetInstance().OpenCVWinSMS();
                    }
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RCServiceConnect mQTTConnect = new() { Owner = Application.Current.GetActiveWindow() };
                    mQTTConnect.ShowDialog();
                });
            }
        }
    }
}
