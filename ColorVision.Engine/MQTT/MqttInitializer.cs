using ColorVision.Common.Utilities;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.MQTT
{
    public class MqttInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public MqttInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 2;
        public async Task InitializeAsync()
        {
            if (MQTTSetting.Instance.IsUseMQTT)
            {
                _messageUpdater.UpdateMessage("正在检测MQTT服务器连接情况");

                bool isConnect = await MQTTControl.GetInstance().Connect();
                _messageUpdater.UpdateMessage($"MQTT服务器连接{(MQTTControl.GetInstance().IsConnect ? Engine.Properties.Resources.Success : Engine.Properties.Resources.Failure)}");
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
                    RCManager.GetInstance();
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        MQTTConnect mQTTConnect = new() { Owner = Application.Current.GetActiveWindow() };
                        mQTTConnect.ShowDialog();
                    });
                }
            }
            else
            {
                _messageUpdater.UpdateMessage("已经跳过MQTT服务器连接");
                await Task.Delay(10);
            }
        }
    }

}
