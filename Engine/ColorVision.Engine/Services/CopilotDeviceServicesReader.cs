using ColorVision.Engine.Services.Devices.Algorithm;
using ColorVision.Engine.Services.Devices.Calibration;
using ColorVision.Engine.Services.Devices.Camera;
using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.LightingController;
using ColorVision.Engine.Services.Devices.Motor;
using ColorVision.Engine.Services.Devices.PG;
using ColorVision.Engine.Services.Devices.Sensor;
using ColorVision.Engine.Services.Devices.SMU;
using ColorVision.Engine.Services.Devices.Spectrum;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services;

public enum CopilotDeviceServiceStatusReadState
{
    Cached = 0,
    NoExistingService = 1,
    Unsupported = 2,
    ReadFailed = 3,
}

public sealed record CopilotDeviceServiceObservation(
    string DeviceRef,
    string ContextSourceId,
    int? ResourceId,
    string Code,
    string Name,
    int? ServiceTypeCode,
    int? StatusCode,
    CopilotDeviceServiceStatusReadState StatusReadState);

public sealed record CopilotDeviceServicesSnapshot(
    string SourceInstanceId,
    string InventoryRevision,
    DateTimeOffset ObservedAtUtc,
    IReadOnlyList<CopilotDeviceServiceObservation> Devices);

public sealed class CopilotDeviceServicesSourceChangedException(string message) : InvalidOperationException(message);
public sealed class CopilotDeviceServicesSourceInvalidException(string message) : InvalidOperationException(message);

/// <summary>Captures already loaded service identities and audited cached status without constructing devices or reading configuration.</summary>
public sealed class CopilotDeviceServicesReader
{
    private sealed class Identity
    {
        internal string Value { get; } = Guid.NewGuid().ToString("N");
    }

    private static readonly ConditionalWeakTable<ServiceManager, Identity> ManagerIdentities = new();
    private static readonly ConditionalWeakTable<DeviceService, Identity> DeviceIdentities = new();
    private static readonly IReadOnlyDictionary<Type, Type> AuditedServiceTypes = new Dictionary<Type, Type>
    {
        [typeof(DeviceCamera)] = typeof(MQTTCamera),
        [typeof(DevicePG)] = typeof(MQTTPG),
        [typeof(DeviceSpectrum)] = typeof(MQTTSpectrum),
        [typeof(DeviceSMU)] = typeof(MQTTSMU),
        [typeof(DeviceSensor)] = typeof(MQTTSensor),
        [typeof(DeviceAlgorithm)] = typeof(MQTTAlgorithm),
        [typeof(DeviceCfwPort)] = typeof(MQTTCfwPort),
        [typeof(DeviceCalibration)] = typeof(MQTTCalibration),
        [typeof(DeviceMotor)] = typeof(MQTTMotor),
        [typeof(DeviceLightingController)] = typeof(MQTTLightingController),
    };

    /// <summary>Freezes the existing owner before dispatch. An uninitialized manager remains uninitialized.</summary>
    public async Task<CopilotDeviceServicesSnapshot?> CaptureCurrentAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var manager = ServiceManager.Current;
        var dispatcher = Application.Current?.Dispatcher;
        if (manager == null || dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished) return null;
        if (dispatcher.CheckAccess()) return Capture(manager, cancellationToken);
        return await dispatcher.InvokeAsync(() => Capture(manager, cancellationToken), DispatcherPriority.Background, cancellationToken)
            .Task.ConfigureAwait(false);
    }

    public CopilotDeviceServicesSnapshot Capture(ServiceManager manager, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(manager);
        var dispatcher = Application.Current?.Dispatcher
            ?? throw new CopilotDeviceServicesSourceChangedException("The device services UI dispatcher is unavailable.");
        dispatcher.VerifyAccess();
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(ServiceManager.Current, manager))
            throw new CopilotDeviceServicesSourceChangedException("The current device services source changed before capture.");
        var collection = manager.DeviceServices
            ?? throw new CopilotDeviceServicesSourceInvalidException("The current device services inventory is unavailable.");
        var devices = collection.ToArray();
        ValidateMembers(devices);
        var observations = new List<CopilotDeviceServiceObservation>(devices.Length);
        foreach (var device in devices)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var resource = device.SysResourceModel;
            var (status, readState) = ReadCachedStatus(device);
            observations.Add(new(
                DeviceIdentities.GetValue(device, _ => new Identity()).Value,
                CopilotDeviceContextFactory.GetSourceId(device),
                resource?.Id,
                resource?.Code ?? string.Empty,
                resource?.Name ?? string.Empty,
                resource?.Type,
                status,
                readState));
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!ReferenceEquals(ServiceManager.Current, manager) || !ReferenceEquals(manager.DeviceServices, collection))
            throw new CopilotDeviceServicesSourceChangedException("The current device services source changed during capture.");
        var currentDevices = collection.ToArray();
        ValidateMembers(currentDevices);
        if (devices.Length != currentDevices.Length || !new HashSet<DeviceService>(devices, ReferenceEqualityComparer.Instance).SetEquals(currentDevices))
            throw new CopilotDeviceServicesSourceChangedException("The current device services membership changed during capture.");
        var ordered = observations.OrderBy(device => device.DeviceRef, StringComparer.Ordinal).ToArray();
        var inventoryRevision = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(string.Join("\n", ordered.Select(device => device.DeviceRef))))).ToLowerInvariant();
        return new(ManagerIdentities.GetValue(manager, _ => new Identity()).Value, inventoryRevision, DateTimeOffset.UtcNow, Array.AsReadOnly(ordered));
    }

    private static void ValidateMembers(DeviceService[] devices)
    {
        var unique = new HashSet<DeviceService>(ReferenceEqualityComparer.Instance);
        foreach (var device in devices)
            if (device == null || !unique.Add(device))
                throw new CopilotDeviceServicesSourceInvalidException("The device services inventory contains a null or duplicate object reference.");
    }

    private static (int? Status, CopilotDeviceServiceStatusReadState ReadState) ReadCachedStatus(DeviceService device)
    {
        // Exact types gate both virtual calls: plugin overrides are never invoked.
        if (!AuditedServiceTypes.TryGetValue(device.GetType(), out var serviceType))
            return (null, CopilotDeviceServiceStatusReadState.Unsupported);
        try
        {
            var service = device.GetMQTTService();
            if (service == null) return (null, CopilotDeviceServiceStatusReadState.NoExistingService);
            if (service.GetType() != serviceType) return (null, CopilotDeviceServiceStatusReadState.Unsupported);
            return ((int)service.DeviceStatus, CopilotDeviceServiceStatusReadState.Cached);
        }
        catch
        {
            return (null, CopilotDeviceServiceStatusReadState.ReadFailed);
        }
    }
}
