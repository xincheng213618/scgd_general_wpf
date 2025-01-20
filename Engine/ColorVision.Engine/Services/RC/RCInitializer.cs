using ColorVision.Engine.MQTT;
using ColorVision.UI;
using System.Collections.Generic;
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
            if (RCSetting.Instance.IsUseRCService)
            {
                _messageUpdater.Update("正在尝试连接注册中心");
                bool isConnect = await MqttRCService.GetInstance().Connect();
                if (!isConnect)
                {
                    _messageUpdater.Update("检测是否本地服务");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RCServiceConnect connect = new() { Owner = Application.Current.GetActiveWindow() };
                        connect.ShowDialog();
                    });
                }
            }
            else
            {
                _messageUpdater.Update("跳过注册中心连接");
            }
        }
    }
}
