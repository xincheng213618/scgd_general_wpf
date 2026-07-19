#pragma warning disable CS1998
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.RC;
using ColorVision.Engine.Templates;
using ColorVision.UI;
using cvColorVision;
using log4net;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services
{
    public class ServiceInitializer : InitializerBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceInitializer));


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
                    ServiceManager serviceManager = ServiceManager.GetInstance();
                    MqttRCService.GetInstance().ApplyPendingServiceUpdates(serviceManager);
                });
                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            }
            else
            {
                log.Info("数据库连接失败，跳过服务配置");
            }

        }
    }

    /// <summary>
    /// Materializes the heavyweight device display controls only after the main
    /// window has had an opportunity to render its first frame.
    /// </summary>
    public sealed class ServiceDisplayInitializer : MainWindowInitializedBase
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(ServiceDisplayInitializer));

        public override int Order { get; set; } = -100;

        public override async Task Initialize()
        {
            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                ServiceManager.GetInstance().GenDeviceDisplayControl();
                stopwatch.Stop();
                log.Info($"Service display controls materialized in {stopwatch.ElapsedMilliseconds} ms after first render.");
            }, DispatcherPriority.ApplicationIdle);
        }
    }
}
