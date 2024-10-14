using ColorVision.Common.Utilities;
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public class ServiceInitializer : IInitializer
    {
        private readonly IMessageUpdater _messageUpdater;

        public ServiceInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public int Order => 5;

        public async Task InitializeAsync()
        {
            if (MySqlControl.GetInstance().IsConnect)
            {
                _messageUpdater.Update("正在加载物理相机");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PhyCameraManager.GetInstance();
                    ServiceManager ServiceManager = ServiceManager.GetInstance();
                });
                if (!ServicesConfig.Instance.IsDefaultOpenService)
                {
                    _messageUpdater.Update("初始化服务");
                    await Task.Delay(10);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServiceManager.GetInstance().GenDeviceDisplayControl();
                        new WindowDevices() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    });
                }
                else
                {
                    _messageUpdater.Update("自动配置服务中");
                    await Task.Delay(10);
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServiceManager.GetInstance().GenDeviceDisplayControl();
                    });
                }
                _messageUpdater.Update("服务初始化完成");
            }
            else
            {
                _messageUpdater.Update("数据库连接失败，跳过服务配置");
            }

        }
    }
}
