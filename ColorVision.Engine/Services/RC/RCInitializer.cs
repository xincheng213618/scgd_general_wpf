using ColorVision.Common.Utilities;
using ColorVision.UI;
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
            if (RCSetting.Instance.IsUseRCService)
            {
                _messageUpdater.UpdateMessage("正在尝试连接注册中心");
                bool isConnect = await MQTTRCService.GetInstance().Connect();
                if (!isConnect)
                {
                    _messageUpdater.UpdateMessage("检测是否本地服务");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        RCServiceConnect mQTTConnect = new() { Owner = Application.Current.GetActiveWindow() };
                        mQTTConnect.ShowDialog();
                    });
                }
            }
            else
            {
                _messageUpdater.UpdateMessage("跳过注册中心连接");
            }
        }
    }
}
