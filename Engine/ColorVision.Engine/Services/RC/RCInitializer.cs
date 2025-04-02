using ColorVision.Common.Utilities;
using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System.Collections.Generic;
using System.ServiceProcess;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services.RC
{
    public class RCInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public RCInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }
        public override string Name => nameof(RCInitializer);
        public override IEnumerable<string> Dependencies => new List<string>() { nameof(MqttInitializer) };
        public override int Order => 4;
        public override async Task InitializeAsync()
        {
            if (!RCSetting.Instance.IsUseRCService)
            {
                _messageUpdater.Update("跳过注册中心连接");
                return;
            }

            _messageUpdater.Update("正在尝试连接注册中心");
            bool isConnect = await MqttRCService.GetInstance().Connect();
            if (!isConnect)
            {
                ServiceController ServiceController = new ServiceController("RegistrationCenterService");
                if (ServiceController != null)
                {
                    _messageUpdater.Update("检测到本地注册中心配置,正在尝试启动");
                    if (Tool.IsAdministrator())
                    {

                        ServiceController.Start();
                        isConnect = await MqttRCService.GetInstance().Connect();
                        if (isConnect) return;
                    }
                    else
                    {
                        if (Tool.ExecuteCommandAsAdmin("net start RegistrationCenterService"))
                        {
                            isConnect = await MqttRCService.GetInstance().Connect();
                            if (isConnect) return;
                        }
                    }
                }
                Application.Current.Dispatcher.Invoke(() =>
                {
                    RCServiceConnect connect = new() { Owner = Application.Current.GetActiveWindow() };
                    connect.ShowDialog();
                });
            }
        }
    }
}
