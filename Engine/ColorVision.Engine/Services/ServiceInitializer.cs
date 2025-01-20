#pragma warning disable CS1998
using ColorVision.Engine.MySql;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.UI;
using cvColorVision;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public class ServiceInitializer : InitializerBase
    {
        private readonly IMessageUpdater _messageUpdater;

        public ServiceInitializer(IMessageUpdater messageUpdater)
        {
            _messageUpdater = messageUpdater;
        }

        public override int Order => 5;

        public override string Name => nameof(ServiceInitializer);
        public override IEnumerable<string> Dependencies => new List<string> { "MySqlInitializer", "TemplateInitializer" };

        public override async Task InitializeAsync()
        {
            if (MySqlControl.GetInstance().IsConnect)
            {
                _messageUpdater.Update("正在加载物理相机");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PhyCameraManager.GetInstance();
                    ServiceManager ServiceManager = ServiceManager.GetInstance();
                });
                if (ServicesConfig.Instance.IsAutoConfig)
                {
                    _messageUpdater.Update("自动配置服务中");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServiceManager.GetInstance().GenDeviceDisplayControl();
                    });
                }
                else
                {
                    _messageUpdater.Update("初始化服务");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServiceManager.GetInstance().GenDeviceDisplayControl();
                        new WindowDevices() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    });
                }
                _messageUpdater.Update("服务初始化完成");

                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
                _messageUpdater.Update("初始化日志");
            }
            else
            {
                _messageUpdater.Update("数据库连接失败，跳过服务配置");
            }

        }
    }
}
