using ColorVision.Engine.Services.Types;
using ColorVision.UI.Desktop.Operations;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class EngineOperationsDeviceHealthProvider : IOperationsDeviceHealthProvider
    {
        private const int DispatcherTimeoutMilliseconds = 1000;

        public OperationsDeviceHealthSnapshot Capture()
        {
            ServiceManager? manager = ServiceManager.Current;
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (manager == null || dispatcher == null
                || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return OperationsDeviceHealthSnapshot.CreateUnavailable();
            }
            if (dispatcher.CheckAccess())
                return CaptureOnDispatcher(manager);

            DispatcherOperation<OperationsDeviceHealthSnapshot> operation = dispatcher.InvokeAsync(
                () => CaptureOnDispatcher(manager), DispatcherPriority.Background);
            if (!operation.Task.Wait(DispatcherTimeoutMilliseconds))
            {
                operation.Abort();
                return OperationsDeviceHealthSnapshot.CreateUnavailable();
            }
            return operation.Task.GetAwaiter().GetResult();
        }

        private static OperationsDeviceHealthSnapshot CaptureOnDispatcher(ServiceManager manager) =>
            OperationsDeviceHealthSnapshotFactory.Create(manager.DeviceServices.Select(Observe));

        private static OperationsDeviceHealthObservation Observe(DeviceService device)
        {
            try
            {
                return new OperationsDeviceHealthObservation(
                    Category(device.ServiceTypes), State(device.GetMQTTService()?.DeviceStatus));
            }
            catch
            {
                return new OperationsDeviceHealthObservation(
                    Category(device.ServiceTypes), OperationsDeviceStates.Unknown);
            }
        }

        internal static string State(DeviceStatusType? status) => status switch
        {
            DeviceStatusType.Opened or DeviceStatusType.Free or DeviceStatusType.LiveOpened
                or DeviceStatusType.SP_Continuous_Mode => OperationsDeviceStates.Ready,
            DeviceStatusType.Busy => OperationsDeviceStates.Busy,
            DeviceStatusType.Opening or DeviceStatusType.Closing => OperationsDeviceStates.Transitioning,
            DeviceStatusType.Closed => OperationsDeviceStates.Closed,
            DeviceStatusType.Unauthorized or DeviceStatusType.UnInit or DeviceStatusType.OffLine
                => OperationsDeviceStates.Unavailable,
            _ => OperationsDeviceStates.Unknown,
        };

        private static string Category(ServiceTypes serviceType) => serviceType switch
        {
            ServiceTypes.Camera => OperationsDeviceCategories.Camera,
            ServiceTypes.Algorithm or ServiceTypes.ThirdPartyAlgorithms or ServiceTypes.ThirdPartyAlgorithms32
                => OperationsDeviceCategories.Algorithm,
            ServiceTypes.Spectrum => OperationsDeviceCategories.Spectrum,
            ServiceTypes.PG or ServiceTypes.SMU or ServiceTypes.Sensor
                or ServiceTypes.PowerControl or ServiceTypes.LightingControl
                => OperationsDeviceCategories.Instrument,
            ServiceTypes.FilterWheel or ServiceTypes.Motor or ServiceTypes.FocusRing
                => OperationsDeviceCategories.Motion,
            ServiceTypes.Calibration or ServiceTypes.SpCalibration
                => OperationsDeviceCategories.Calibration,
            _ => OperationsDeviceCategories.Other,
        };
    }
}
