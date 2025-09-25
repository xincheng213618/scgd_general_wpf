#pragma warning disable CS1998
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using cvColorVision;
using log4net;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.Services
{
    public class ServiceInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(TemplateInitializer));


        public override int Order => 5;

        public override string Name => nameof(ServiceInitializer);
        public override IEnumerable<string> Dependencies => new List<string> { "MySqlInitializer", "TemplateInitializer" };

        public override async Task InitializeAsync()
        {
            if (MySqlControl.GetInstance().IsConnect)
            {
                log.Info("正在加载物理相机");
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PhyCameraManager.GetInstance();
                    ServiceManager ServiceManager = ServiceManager.GetInstance();
                });
                if (ServicesConfig.Instance.IsAutoConfig)
                {
                    log.Info("自动配置服务中");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServiceManager.GetInstance().GenDeviceDisplayControl();
                    });
                }
                else
                {
                    log.Info("初始化服务");
                    Application.Current.Dispatcher.Invoke(() =>
                    {
                        ServiceManager.GetInstance().GenDeviceDisplayControl();
                        new WindowDevices() { Owner = Application.Current.GetActiveWindow(), WindowStartupLocation = WindowStartupLocation.CenterOwner }.ShowDialog();
                    });
                }
                log.Info("服务初始化完成");

                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
                log.Info("初始化日志");
            }
            else
            {
                log.Info("数据库连接失败，跳过服务配置");
            }

        }
    }
}
