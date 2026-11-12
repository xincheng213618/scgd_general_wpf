using ColorVision.Engine.Services.Devices.PG;
using System;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

internal static class LocalCommunicationOwnership
{
    internal static void EnsureAvailable(DeviceService owner, bool local, string endpoint)
    {
        if (ServiceManager.Current == null) return;
        foreach (var other in ServiceManager.Current.DeviceServices)
        {
            if (ReferenceEquals(owner, other)) continue;
            var (backend, otherEndpoint) = other switch
            {
                DeviceSensor sensor => (sensor.SensorBackend, sensor.LocalSession.EndpointIdentity ?? LocalSensorSession.EndpointKey(sensor.Config)),
                DevicePG pg => (pg.PGBackend, pg.LocalSession.EndpointIdentity ?? pg.LocalEndpoint),
                _ => ((SensorBackendState?)null, (string?)null)
            };
            if (backend == null || otherEndpoint != endpoint) continue;
            if (local)
            {
                if (backend.LocalOwned) throw new InvalidOperationException("另一个本地设备正在使用此连接。");
                backend.EnsureLocalAvailable();
            }
            else backend.EnsureServiceAvailable();
        }
    }
}
