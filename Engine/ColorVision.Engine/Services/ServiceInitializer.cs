#pragma warning disable CS1998
using ColorVision.Database;
using ColorVision.Engine.Services.PhyCameras;
using ColorVision.Engine.Services.RC;
using ColorVision.UI;
using cvColorVision;
using log4net;
using System;
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

        public override async Task InitializeAsync()
        {

            Stopwatch totalStopwatch = Stopwatch.StartNew();
            Stopwatch phaseStopwatch = new();
            long physicalCameraMilliseconds = 0;
            long serviceHierarchyMilliseconds = 0;
            long pendingUpdatesMilliseconds = 0;
            long displayControlsMilliseconds = 0;
            var dispatcher = Application.Current.Dispatcher;
            await dispatcher.InvokeAsync(() =>
            {
                phaseStopwatch.Restart();
                PhyCameraManager.GetInstance();
                physicalCameraMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            }, DispatcherPriority.Background);

            ServiceManager serviceManager = await dispatcher.InvokeAsync(() =>
            {
                phaseStopwatch.Restart();
                ServiceManager manager = ServiceManager.GetInstance();
                DisPlayManager.GetInstance().DeviceConfigurationCommand = new ExportWindowService().Command;
                serviceHierarchyMilliseconds = phaseStopwatch.ElapsedMilliseconds;
                return manager;
            }, DispatcherPriority.Background);

            await dispatcher.InvokeAsync(() =>
            {
                phaseStopwatch.Restart();
                MqttRCService.GetInstance().ApplyPendingServiceUpdates(serviceManager);
                pendingUpdatesMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            }, DispatcherPriority.Background);

            phaseStopwatch.Restart();
            await dispatcher.InvokeAsync(serviceManager.GenDeviceDisplayControl, DispatcherPriority.Background);
            displayControlsMilliseconds = phaseStopwatch.ElapsedMilliseconds;

            phaseStopwatch.Restart();
            if (MySqlSetting.IsConnect) cvCameraCSLib.InitResource(IntPtr.Zero, IntPtr.Zero);
            long cameraResourceMilliseconds = phaseStopwatch.ElapsedMilliseconds;
            totalStopwatch.Stop();
            log.Info($"Service initialization completed. PhysicalCameras={physicalCameraMilliseconds}ms, " +
                $"Hierarchy={serviceHierarchyMilliseconds}ms, PendingUpdates={pendingUpdatesMilliseconds}ms, " +
                $"DisplayControls={displayControlsMilliseconds}ms, CameraResource={cameraResourceMilliseconds}ms, " +
                $"Total={totalStopwatch.ElapsedMilliseconds}ms.");


        }
    }
}
