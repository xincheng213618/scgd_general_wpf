using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using System;
using System.Linq;
using System.Threading;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal interface ILocalSpectrumNdOperation : IDisposable
{
    int GetPort();
    int SetPort(int port);
}

// ND belongs to the spectrum connection. A bound wheel lends its existing session;
// an unbound wheel is owned here and closed with the spectrum session.
internal sealed class LocalSpectrumNd(Func<NDConfig, ILocalSpectrumNdOperation>? acquire = null, LocalCfwPortSession? directSession = null)
{
    private readonly LocalCfwPortSession direct = directSession ?? new();

    internal ILocalSpectrumNdOperation Acquire(NDConfig config)
    {
        if (!config.IsNDPort) throw new InvalidOperationException("请先在光谱仪配置中启用 ND。");
        if (acquire != null) return acquire(config);
        if (config.IsBingNDDevice)
        {
            var device = FindBoundDevice(config) ?? throw new InvalidOperationException($"绑定的 ND 滤光轮 {config.NDBindDeviceCode} 未加载，请重新选择设备。");
            return new BoundOperation(device);
        }
        int highestPort = Math.Max(config.DarkNDPort, config.NDRate.Count - 1);
        var connection = LocalCfwPortConnection.From(new ConfigCfwPort { SzComName = config.SzComName, BaudRate = config.BaudRate, Ports = highestPort >= 6 ? 12 : 6 });
        lock (CfwPortBackendState.Sync)
        {
            foreach (var device in ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>() ?? [])
            {
                if (string.Equals(device.LocalSession.Endpoint ?? device.CfwBackend.Endpoint ?? device.Config.SzComName, connection.PortName, StringComparison.OrdinalIgnoreCase))
                    device.CfwBackend.EnsureAvailable(true);
            }
            direct.ExecuteAsync("Open", connection).GetAwaiter().GetResult();
        }
        return new DirectOperation(direct, connection);
    }

    internal static DeviceCfwPort? FindBoundDevice(NDConfig config) => config.IsBingNDDevice
        ? ServiceManager.Current?.DeviceServices.OfType<DeviceCfwPort>().FirstOrDefault(device => device.Code == config.NDBindDeviceCode) : null;

    internal void Disconnect() => direct.ExecuteAsync("Close", LocalCfwPortConnection.From(new ConfigCfwPort())).GetAwaiter().GetResult();

    private sealed class BoundOperation : ILocalSpectrumNdOperation
    {
        private readonly DeviceCfwPort device;
        private readonly LocalCfwPortConnection config;
        private readonly IDisposable reservation;
        internal BoundOperation(DeviceCfwPort device)
        {
            this.device = device;
            config = LocalCfwPortConnection.From(device.Config);
            reservation = device.DService.ReserveForCalibration();
        }
        private int Execute(string operation, int? port) => device.DService.ExecuteLocalAsync(operation, config, port, CancellationToken.None)
            .GetAwaiter().GetResult() ?? throw new InvalidOperationException("ND 未返回确认孔位。");
        public int GetPort() => Execute("GetPort", null);
        public int SetPort(int port) => Execute("SetPort", port);
        public void Dispose() => reservation.Dispose();
    }

    private sealed class DirectOperation(LocalCfwPortSession session, LocalCfwPortConnection config) : ILocalSpectrumNdOperation
    {
        private int Execute(string operation, int? port) => session.ExecuteAsync(operation, config, port, true).GetAwaiter().GetResult()
            ?? throw new InvalidOperationException("ND 未返回确认孔位。");
        public int GetPort() => Execute("GetPort", null);
        public int SetPort(int port) => Execute("SetPort", port);
        public void Dispose() { }
    }

    internal sealed record Calibration(int Port, string WavelengthFile, string MagnitudeFile, string? GroupName);

    internal static Calibration ResolveCalibration(ConfigSpectrum config, int port)
    {
        if (port < 0) throw new InvalidOperationException("ND 返回了无效孔位。");
        var exact = config.CalibrationGroups.Where(group => group.NDHoleIndex == port).ToArray();
        if (exact.Length > 1) throw new InvalidOperationException($"ND 孔位 {port} 关联了多个标定分组，请检查配置。");
        string? legacy = port >= 0 && port < config.NDConfig.NDCaliNameGroups.Count ? config.NDConfig.NDCaliNameGroups[port] : null;
        string? holeName = FindBoundDevice(config.NDConfig)?.FilterWheelConfig.HoleMapping.FirstOrDefault(hole => hole.HoleIndex == port)?.HoleName
            ?? new FilterWheelConfig().HoleMapping.FirstOrDefault(hole => hole.HoleIndex == port)?.HoleName;
        var group = exact.SingleOrDefault()
            ?? config.CalibrationGroups.FirstOrDefault(item => string.Equals(item.GroupName, legacy, StringComparison.OrdinalIgnoreCase))
            ?? config.CalibrationGroups.FirstOrDefault(item => string.Equals(item.GroupName, holeName, StringComparison.OrdinalIgnoreCase));
        string wave = LocalSpectrumSession.ResolvePath(group?.WavelengthFile ?? config.WavelengthFile);
        string magnitude = LocalSpectrumSession.ResolvePath(group?.MaguideFile ?? (string.IsNullOrWhiteSpace(legacy)
            ? throw new InvalidOperationException($"ND 孔位 {port} 没有对应的标定分组或幅值文件。") : legacy));
        string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wave, magnitude);
        if (error != null) throw new InvalidOperationException(error);
        return new(port, wave, magnitude, group?.GroupName);
    }
}
