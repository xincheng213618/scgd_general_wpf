using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

/// <summary>Configuration and actuator identities selected before a local spectrum command runs.</summary>
internal sealed class LocalSpectrumOpticalScope
{
    private readonly bool includesNd;
    private readonly bool includesShutter;
    private readonly Func<bool> ownsDevice;
    private readonly Func<bool> ownsNd;
    private readonly Func<bool> ownsShutter;
    private readonly IReadOnlyList<DeviceCfwPort> directNdPeers;

    internal ConfigSpectrum Configuration { get; }
    internal DeviceConfigurationTarget ConfigurationTarget { get; }
    internal LocalSpectrumWheelBinding? Nd { get; }
    internal LocalSpectrumWheelBinding? Shutter { get; }
    internal DeviceCfwPort? NdDevice => Nd?.Device;
    internal LocalCfwPortConnection? DirectNdConnection { get; }
    internal bool SharesNdShutter => Nd != null && ReferenceEquals(Nd, Shutter);
    internal bool IsDeviceCurrent => ownsDevice();
    internal bool IsNdCurrent => ownsNd() && (Nd?.IsCurrent ?? true);
    internal bool IsShutterCurrent => ownsShutter() && (Shutter?.IsCurrent ?? true);
    internal bool IsCurrent => (!includesNd || IsNdCurrent) && (!includesShutter || IsShutterCurrent);

    private LocalSpectrumOpticalScope(DeviceSpectrum owner, ConfigSpectrum config, bool includeNd, bool includeShutter)
    {
        Configuration = config;
        ConfigurationTarget = owner.CaptureConfigurationTarget();
        includesNd = includeNd;
        includesShutter = includeShutter && config.IsShutterEnable;
        ConfigSpectrum live = owner.Config;
        var resource = owner.SysResourceModel;
        int resourceId = resource.Id;
        bool temporary = resourceId is 0 or -1;
        int? parentId = resource.Pid;
        var endpoint = owner.ResultDatabaseEndpoint;
        SysResourceDao? CurrentLocalDao() => temporary ? owner.LocalConfigurationResourceDao ?? SysResourceDao.Instance : LocalDao(owner);
        SysResourceDao? localDao = CurrentLocalDao();
        ServiceManager? manager = ServiceManager.Current;
        DeviceService[] members = (includesNd || includesShutter) && manager != null ? manager.DeviceServices.ToArray() : [];
        bool managed = manager?.DeviceServices.Contains(owner) == true;
        ownsDevice = () => owner.IsConfigurationTargetCurrent(ConfigurationTarget) && ReferenceEquals(owner.Config, live)
            && parentId == owner.SysResourceModel.Pid && (resourceId <= 0 || ReferenceEquals(owner.ResultDatabaseEndpoint, endpoint))
            && ((temporary && Nd == null && Shutter == null) || ReferenceEquals(CurrentLocalDao(), localDao))
            && string.Equals(live.Code, config.Code, StringComparison.Ordinal)
            && string.Equals(live.SN, config.SN, StringComparison.OrdinalIgnoreCase)
            && live.SpectrometerType == config.SpectrometerType && live.ComPort == config.ComPort && live.BaudRate == config.BaudRate
            && (!managed || (ReferenceEquals(ServiceManager.Current, manager) && manager!.DeviceServices.Contains(owner)));

        NDConfig ndConfig = config.NDConfig;
        string? ndCode = ndConfig.NDBindDeviceCode;
        bool ndEnabled = ndConfig.IsNDPort;
        bool ndBound = ndConfig.IsBingNDDevice;
        int ndDarkPort = ndConfig.DarkNDPort;
        LocalCfwPortConnection directConnection = LocalSpectrumNd.GetDirectConnection(ndConfig);
        ownsNd = () => IsDeviceCurrent && live.NDConfig.IsNDPort == ndEnabled && live.NDConfig.IsBingNDDevice == ndBound
            && string.Equals(live.NDConfig.NDBindDeviceCode, ndCode, StringComparison.Ordinal)
            && (!ndEnabled || (live.NDConfig.DarkNDPort == ndDarkPort
                && (ndBound || LocalSpectrumNd.GetDirectConnection(live.NDConfig) == directConnection)));

        ShutterConfig shutterConfig = config.ShutterCfg;
        var shutterSettings = ReadShutterSettings(shutterConfig);
        ownsShutter = () => IsDeviceCurrent && live.IsShutterEnable && ReadShutterSettings(live.ShutterCfg) == shutterSettings;

        DeviceCfwPort[] wheels = members.OfType<DeviceCfwPort>().ToArray();
        bool SameHierarchy(DeviceCfwPort wheel) => resourceId > 0
            ? wheel.SysResourceModel.Id > 0 && endpoint != null && ReferenceEquals(wheel.ResultDatabaseEndpoint, endpoint)
            : localDao != null && ReferenceEquals(LocalDao(wheel), localDao);
        if (includesNd && ndBound && (ndEnabled || !string.IsNullOrWhiteSpace(ndCode)))
            Nd = new LocalSpectrumWheelBinding(FindWheel(wheels.Where(SameHierarchy), ndCode), manager!, forceLocal: true);
        if (includesShutter && shutterConfig.UseFilterWheel)
        {
            var wheel = FindWheel(wheels.Where(SameHierarchy), shutterConfig.FilterWheelDeviceCode);
            Shutter = ReferenceEquals(NdDevice, wheel) ? Nd : new LocalSpectrumWheelBinding(wheel, manager!, forceLocal: false);
        }
        if (includesNd && ndEnabled && !ndBound)
        {
            DirectNdConnection = directConnection;
            directNdPeers = wheels.Where(wheel => UsesPort(wheel, directConnection.PortName)).ToArray();
        }
        else directNdPeers = [];
        if (Nd != null) RequireNdCurrent();
        if (Shutter != null) RequireShutterCurrent();
    }

    internal static LocalSpectrumOpticalScope Capture(DeviceSpectrum owner, ConfigSpectrum config, bool includeNd = true, bool includeShutter = true)
        => new(owner, config, includeNd, includeShutter);

    internal void EnsureCurrent()
    {
        if (!IsCurrent) throw new InvalidOperationException("光谱仪或绑定滤色轮已更改，请重新选择设备后重试。");
    }

    internal void RequireNdCurrent()
    {
        if (!IsNdCurrent) throw new InvalidOperationException("光谱仪或绑定的 ND 滤色轮已更改，请重新选择设备后重试。");
    }

    internal string? GetNdHoleName(int port) => Nd?.Holes.FirstOrDefault(hole => hole.Index == port).Name
        ?? new FilterWheelConfig().HoleMapping.FirstOrDefault(hole => hole.HoleIndex == port)?.HoleName;

    internal void RequireShutterCurrent()
    {
        if (!IsShutterCurrent) throw new InvalidOperationException("光谱仪或校零滤色轮已更改，请重新选择设备后重试。");
    }

    internal void EnsureDirectNdAvailable()
    {
        RequireNdCurrent();
        if (DirectNdConnection is { } connection)
        {
            // Check claims under the CFW backend lock used by the caller before opening the direct session.
            var currentPeers = ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>() ?? [];
            foreach (DeviceCfwPort wheel in directNdPeers.Concat(currentPeers).Distinct())
                if (UsesPort(wheel, connection.PortName)) wheel.CfwBackend.EnsureAvailable(true);
        }
    }

    internal static DeviceCfwPort FindWheel(IEnumerable<DeviceCfwPort> wheels, string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new InvalidOperationException("请选择绑定的滤色轮设备。");
        DeviceCfwPort[] matches = wheels.Where(wheel => string.Equals(wheel.Code, code, StringComparison.Ordinal)).Take(2).ToArray();
        if (matches.Length != 1)
            throw new InvalidOperationException(matches.Length == 0
                ? $"当前设备配置中找不到绑定的滤色轮 {code}。"
                : $"当前设备配置中有多个滤色轮使用编码 {code}，请使用唯一的设备编码。");
        return matches[0];
    }

    internal static SysResourceDao? LocalDao(DeviceService device) => SysResourceDao.IsLocalId(device.SysResourceModel.Id)
        ? device.LocalConfigurationResourceDao ?? SysResourceDao.Instance : null;

    internal static bool UsesPort(DeviceCfwPort wheel, string port) => string.Equals(
        wheel.LocalSession.Endpoint ?? wheel.CfwBackend.Endpoint ?? LocalCfwPortConnection.From(wheel.Config).PortName,
        port, StringComparison.OrdinalIgnoreCase);

    private static (bool, string, int, int, string, int, string, string, bool, int) ReadShutterSettings(ShutterConfig config)
        => (config.UseFilterWheel, config.FilterWheelDeviceCode, config.MeasurementPort, config.DarkPort,
            config.Addr, config.BaudRate, config.OpenCmd, config.CloseCmd, config.OpenForDark, config.DelayTime);
}

internal sealed class LocalSpectrumWheelBinding
{
    private readonly Func<bool> ownsWheel;
    private readonly bool forceLocal;
    internal DeviceCfwPort Device { get; }
    internal MQTTCfwPort Service { get; }
    internal LocalCfwPortConnection Connection { get; }
    internal bool UsesLocal { get; }
    internal MQTTCfwPort.CalibrationRoute? RemoteRoute { get; }
    internal IReadOnlyList<(int Index, string Name)> Holes { get; }
    internal bool IsCurrent => ownsWheel() && (forceLocal || Device.CfwBackend.OpensLocally == UsesLocal)
        && (RemoteRoute is not { } route || Service.IsCalibrationRouteUsable(route));

    internal LocalSpectrumWheelBinding(DeviceCfwPort device, ServiceManager manager, bool forceLocal)
    {
        Device = device;
        this.forceLocal = forceLocal;
        Service = device.DService;
        DeviceConfigurationTarget target = device.CaptureConfigurationTarget();
        var config = device.Config;
        var localDao = LocalSpectrumOpticalScope.LocalDao(device);
        string serial = config.SN;
        string sendTopic = Service.SendTopic;
        string subscribeTopic = Service.SubscribeTopic;
        int? parentId = device.SysResourceModel.Pid;
        Connection = LocalCfwPortConnection.From(device.Config);
        UsesLocal = forceLocal || device.CfwBackend.OpensLocally;
        ownsWheel = () => !Service.IsDisposed && ReferenceEquals(device.DService, Service) && target.IsCurrent(device)
            && ReferenceEquals(LocalSpectrumOpticalScope.LocalDao(device), localDao)
            && parentId == device.SysResourceModel.Pid && ReferenceEquals(device.Config, config)
            && LocalCfwPortConnection.From(config) == Connection && string.Equals(config.SN, serial, StringComparison.OrdinalIgnoreCase)
            && string.Equals(Service.SendTopic, sendTopic, StringComparison.Ordinal)
            && string.Equals(Service.SubscribeTopic, subscribeTopic, StringComparison.Ordinal)
            && ReferenceEquals(ServiceManager.Current, manager) && manager.DeviceServices.Contains(device);
        Holes = device.FilterWheelConfig.HoleMapping.Select(hole => (hole.HoleIndex, hole.HoleName)).ToArray();
        if (!UsesLocal) RemoteRoute = Service.CaptureCalibrationRoute();
    }

    internal int? ExecuteLocal(string command, int? port, bool autoConnect)
    {
        if (Service.IsDisposed || !ReferenceEquals(Device.DService, Service)
            || (!autoConnect && !string.Equals(Device.LocalSession.Endpoint, Connection.PortName, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("原滤色轮本地连接已不可用。");
        return Service.ExecuteLocalAsync(command, Connection, port, CancellationToken.None, autoConnect).GetAwaiter().GetResult();
    }
}
