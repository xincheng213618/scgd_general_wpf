#pragma warning disable CS1998
using ColorVision.Database;
using ColorVision.Engine.Messages;
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
                Application.Current.Dispatcher.Invoke(() =>
                {
                    PhyCameraManager.GetInstance();
                    ServiceManager.GetInstance().GenDeviceDisplayControl();
                });
                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                log.Info("数据库连接失败，跳过服务配置");
            }

        }
    }
}
