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
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                Stopwatch phaseStopwatch = new();
                long physicalCameraMilliseconds = 0;
                long serviceHierarchyMilliseconds = 0;
                long pendingUpdatesMilliseconds = 0;
                long displayControlsMilliseconds = 0;
                Application.Current.Dispatcher.Invoke(() =>
                {
                    phaseStopwatch.Restart();
                    PhyCameraManager.GetInstance();
                    physicalCameraMilliseconds = phaseStopwatch.ElapsedMilliseconds;

                    phaseStopwatch.Restart();
                    ServiceManager serviceManager = ServiceManager.GetInstance();
                    serviceHierarchyMilliseconds = phaseStopwatch.ElapsedMilliseconds;

                    phaseStopwatch.Restart();
                    MqttRCService.GetInstance().ApplyPendingServiceUpdates(serviceManager);
                    pendingUpdatesMilliseconds = phaseStopwatch.ElapsedMilliseconds;

                    phaseStopwatch.Restart();
                    serviceManager.GenDeviceDisplayControl();
                    displayControlsMilliseconds = phaseStopwatch.ElapsedMilliseconds;
                });

                phaseStopwatch.Restart();
                cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
                long cameraResourceMilliseconds = phaseStopwatch.ElapsedMilliseconds;
                totalStopwatch.Stop();
                log.Info($"Service initialization completed. PhysicalCameras={physicalCameraMilliseconds}ms, " +
                    $"Hierarchy={serviceHierarchyMilliseconds}ms, PendingUpdates={pendingUpdatesMilliseconds}ms, " +
                    $"DisplayControls={displayControlsMilliseconds}ms, CameraResource={cameraResourceMilliseconds}ms, " +
                    $"Total={totalStopwatch.ElapsedMilliseconds}ms.");
            }
            else
            {
                log.Info("数据库连接失败，跳过服务配置");
            }

        }
    }
}
