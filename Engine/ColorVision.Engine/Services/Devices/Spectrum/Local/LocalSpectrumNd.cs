using ColorVision.Engine.Services.Devices.CfwPort;
using ColorVision.Engine.Services.Devices.CfwPort.Local;
using ColorVision.Engine.Services.Devices.Spectrum.Configs;
using ColorVision.Engine.Services.PhyCameras.Configs;
using System;
using System.Linq;

namespace ColorVision.Engine.Services.Devices.Spectrum.Local;

internal interface ILocalSpectrumNdOperation : IDisposable
{
    int GetPort();
    int SetPort(int port);
    int RestorePort(int port) => SetPort(port);
}

// ND belongs to the spectrum connection. A bound wheel lends its existing session;
// an unbound wheel is owned here and closed with the spectrum session.
internal sealed class LocalSpectrumNd(Func<NDConfig, ILocalSpectrumNdOperation>? acquire = null, LocalCfwPortSession? directSession = null)
{
    internal const string WheelNotConfigured = "请先绑定 ND 滤光轮，或配置直连 ND。";
    private readonly LocalCfwPortSession direct = directSession ?? new();

    internal ILocalSpectrumNdOperation Acquire(NDConfig config, LocalSpectrumOpticalScope? scope = null)
    {
        if (!config.HasConfiguredWheel) throw new InvalidOperationException(WheelNotConfigured);
        if (acquire != null) return acquire(config);
        scope?.RequireNdCurrent();
        if (config.IsBingNDDevice)
        {
            if (scope is not { Nd: { } binding })
                throw new InvalidOperationException($"未捕获绑定的 ND 滤光轮 {config.NDBindDeviceCode}，请重新选择设备。");
            return new BoundOperation(binding, scope);
        }
        var connection = scope?.DirectNdConnection ?? GetDirectConnection(config);
        lock (CfwPortBackendState.Sync)
        {
            scope?.EnsureDirectNdAvailable();
            direct.ExecuteAsync("Open", connection).GetAwaiter().GetResult();
        }
        return new DirectOperation(direct, connection, scope);
    }

    internal static LocalCfwPortConnection GetDirectConnection(NDConfig config)
        => LocalCfwPortConnection.From(new ConfigCfwPort { SzComName = config.SzComName, BaudRate = config.BaudRate, Ports = GetDirectPortCount(config) });

    internal static int GetDirectPortCount(NDConfig config)
    {
        if (config.LocalNDPortCount is int configured) return configured;
        int highestLegacyPort = Math.Max(config.DarkNDPort, config.NDRate.Count - 1);
        int highestLocalPort = config.LocalNDRates?.Keys.DefaultIfEmpty(-1).Max() ?? -1;
        return Math.Max(highestLegacyPort, highestLocalPort) >= 6 ? 12 : 6;
    }

    internal void Disconnect() => direct.ExecuteAsync("Close", LocalCfwPortConnection.From(new ConfigCfwPort())).GetAwaiter().GetResult();

    private sealed class BoundOperation : ILocalSpectrumNdOperation
    {
        private readonly LocalSpectrumWheelBinding binding;
        private readonly LocalSpectrumOpticalScope scope;
        private readonly IDisposable reservation;
        internal BoundOperation(LocalSpectrumWheelBinding binding, LocalSpectrumOpticalScope scope)
        {
            this.binding = binding;
            this.scope = scope;
            reservation = binding.Service.ReserveForCalibration();
        }
        private int Execute(string operation, int? port, bool restore = false)
        {
            if (!restore) scope.RequireNdCurrent();
            return binding.ExecuteLocal(operation, port, autoConnect: !restore)
                ?? throw new InvalidOperationException("ND 未返回确认孔位。");
        }
        public int GetPort() => Execute("GetPort", null);
        public int SetPort(int port) => Execute("SetPort", port);
        public int RestorePort(int port) => Execute("SetPort", port, restore: true);
        public void Dispose() => reservation.Dispose();
    }

    private sealed class DirectOperation(LocalCfwPortSession session, LocalCfwPortConnection config, LocalSpectrumOpticalScope? scope) : ILocalSpectrumNdOperation
    {
        private int Execute(string operation, int? port, bool restore = false)
        {
            if (!restore) scope?.RequireNdCurrent();
            return session.ExecuteAsync(operation, config, port, autoConnect: false).GetAwaiter().GetResult()
                ?? throw new InvalidOperationException("ND 未返回确认孔位。");
        }
        public int GetPort() => Execute("GetPort", null);
        public int SetPort(int port) => Execute("SetPort", port);
        public int RestorePort(int port) => Execute("SetPort", port, restore: true);
        public void Dispose() { }
    }

    internal sealed record Calibration(int Port, string WavelengthFile, string MagnitudeFile, string? GroupName);

    internal static Calibration? ResolveCalibration(ConfigSpectrum config, int port, LocalSpectrumOpticalScope? scope = null, bool allowUnassociated = false)
    {
        if (port < 0) throw new InvalidOperationException("ND 返回了无效孔位。");
        var exact = config.CalibrationGroups.Where(group => group.NDHoleIndex == port).ToArray();
        if (exact.Length > 1) throw new InvalidOperationException($"ND 孔位 {port} 关联了多个标定分组，请检查配置。");
        string? legacy = port >= 0 && port < config.NDConfig.NDCaliNameGroups.Count ? config.NDConfig.NDCaliNameGroups[port] : null;
        var group = exact.SingleOrDefault()
            ?? config.CalibrationGroups.FirstOrDefault(item => string.Equals(item.GroupName, legacy, StringComparison.OrdinalIgnoreCase));
        if (group == null)
        {
            string? holeName = scope?.GetNdHoleName(port)
                ?? new FilterWheelConfig().HoleMapping.FirstOrDefault(hole => hole.HoleIndex == port)?.HoleName;
            group = config.CalibrationGroups.FirstOrDefault(item => string.Equals(item.GroupName, holeName, StringComparison.OrdinalIgnoreCase));
        }
        if (group == null && string.IsNullOrWhiteSpace(legacy))
        {
            if (allowUnassociated) return null;
            throw new InvalidOperationException($"ND 孔位 {port} 没有对应的标定分组或幅值文件。");
        }
        string wave = LocalSpectrumSession.ResolvePath(group?.WavelengthFile ?? config.WavelengthFile);
        string magnitude = LocalSpectrumSession.ResolvePath(group?.MaguideFile ?? legacy!);
        string? error = DeviceSpectrum.ValidateMeasurementCalibrationFiles(wave, magnitude);
        if (error != null) throw new InvalidOperationException(error);
        return new(port, wave, magnitude, group?.GroupName);
    }
}
