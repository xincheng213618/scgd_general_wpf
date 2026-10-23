using cvColorVision;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.CfwPort.Local;

internal sealed record LocalCfwPortConnection(string PortName, int BaudRate, int Timeout, int RetryCount, int Ports, int Delay, bool IsCVMode, bool EnableVSPHub)
{
    internal static LocalCfwPortConnection From(ConfigCfwPort config) => new(config.SzComName?.Trim().ToUpperInvariant() ?? string.Empty,
        config.BaudRate, config.Timeout, config.RetryCount, config.Ports, config.Delay, config.IsCVMode, config.EnableVSPHub);

    internal void Validate()
    {
        if (!PortName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || !int.TryParse(PortName.AsSpan(3), out int number) || number <= 0)
            throw new ArgumentException("请选择有效的滤色轮串口，例如 COM12。");
        if (BaudRate <= 0 || Timeout <= 0 || RetryCount < 0 || Delay < 0 || Ports is not (6 or 12))
            throw new ArgumentException("滤色轮波特率和超时必须大于零，重试次数和延时不能为负数，孔位数量须为 6 或 12。");
        if (IsCVMode) throw new NotSupportedException("本地独立滤色轮使用串口连接，请关闭 IsCVMode；相机内置滤轮请通过相机控制。");
        if (EnableVSPHub) throw new NotSupportedException("本地滤色轮不创建虚拟串口集线器，请关闭 EnableVSPHub 或使用服务模式。");
    }
}

internal interface ILocalCfwPortNative
{
    IntPtr Open(LocalCfwPortConnection config);
    int Close(IntPtr handle);
    int SetPort(IntPtr handle, int port);
    int GetPort(IntPtr handle, ref int port);
}

internal sealed class LocalCfwPortNative : ILocalCfwPortNative
{
    // The SDK's standalone filter-wheel handle map is process global and not synchronized.
    private static readonly object NativeGate = new();
    public IntPtr Open(LocalCfwPortConnection config)
    {
        lock (NativeGate) return NdCFWPortAPI.CM_CreatNdCFWPort(config.PortName, (uint)config.BaudRate, false, (uint)config.Timeout);
    }
    public int Close(IntPtr handle) { lock (NativeGate) return NdCFWPortAPI.CM_ReleaseNdCFWPort(handle); }
    public int SetPort(IntPtr handle, int port) { lock (NativeGate) return NdCFWPortAPI.CM_SetNdCFWPort(handle, port); }
    public int GetPort(IntPtr handle, ref int port) { lock (NativeGate) return NdCFWPortAPI.CM_GetNdCFWPort(handle, ref port); }
}

internal sealed class LocalCfwPortSession(ILocalCfwPortNative? native = null) : IAsyncDisposable
{
    private static readonly Dictionary<string, LocalCfwPortSession> Owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILocalCfwPortNative native = native ?? new LocalCfwPortNative();
    private LocalCfwPortConnection? connection;
    private IntPtr handle;
    private volatile bool disposed;
    private bool quarantined;
    private DeviceStatusType status = DeviceStatusType.Closed;
    internal event Action<DeviceStatusType>? StatusChanged;
    internal DeviceStatusType Status => status;
    internal string? Endpoint => connection?.PortName;

    private void SetStatus(DeviceStatusType value) { status = value; StatusChanged?.Invoke(value); }

    internal async Task<int?> ExecuteAsync(string operation, LocalCfwPortConnection config, int? port = null, bool autoConnect = false, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            token.ThrowIfCancellationRequested();
            // Cancellation must never free a handle while a blocking SDK call still uses it.
            int? result = await Task.Run(() => ExecuteCore(operation, config, port, autoConnect, token), CancellationToken.None).ConfigureAwait(false);
            token.ThrowIfCancellationRequested();
            return result;
        }
        finally { gate.Release(); }
    }

    private int? ExecuteCore(string operation, LocalCfwPortConnection config, int? port, bool autoConnect, CancellationToken token)
    {
        if (quarantined) throw new InvalidOperationException("滤色轮释放失败，请重启程序后再连接。");
        if (operation == "Close") { CloseCore(); return null; }
        if (operation is not ("Open" or "SetPort" or "GetPort")) throw new NotSupportedException($"本地滤色轮不支持 {operation}。");
        config.Validate();
        if (operation == "SetPort" && (!port.HasValue || port < 0 || port >= config.Ports)) throw new ArgumentOutOfRangeException(nameof(port), "滤色轮孔位超出配置范围。");
        if (operation == "Open" || (autoConnect && handle == IntPtr.Zero)) OpenCore(config);
        if (operation == "Open") return null;
        if (handle == IntPtr.Zero) throw new InvalidOperationException("请先连接本地滤色轮。");
        if (connection != config) throw new InvalidOperationException("滤色轮配置已改变，请关闭连接后重新连接。");

        SetStatus(DeviceStatusType.Busy);
        try
        {
            int code = 0, actual = -1;
            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                code = operation == "SetPort" ? native.SetPort(handle, port!.Value) : 0;
                if (code == 0)
                {
                    if (config.Delay != 0 && token.WaitHandle.WaitOne(config.Delay)) token.ThrowIfCancellationRequested();
                    actual = -1;
                    code = native.GetPort(handle, ref actual);
                    if (code == 0 && actual >= 0 && actual < config.Ports && (operation != "SetPort" || actual == port)) return actual;
                }
                if (attempt >= config.RetryCount) break;
            }
            throw new InvalidOperationException($"滤色轮{(operation == "SetPort" ? "定位" : "读位")}失败，SDK 返回 {code}，实际孔位 {actual}，目标孔位 {port?.ToString() ?? "读取"}。");
        }
        finally { SetStatus(DeviceStatusType.Opened); }
    }

    private void OpenCore(LocalCfwPortConnection config)
    {
        if (handle != IntPtr.Zero)
        {
            if (connection == config) return;
            throw new InvalidOperationException("滤色轮已连接，请先关闭再修改连接配置。");
        }
        lock (Owners)
        {
            if (Owners.TryGetValue(config.PortName, out var owner) && !ReferenceEquals(owner, this))
                throw new InvalidOperationException($"另一个本地滤色轮正在使用 {config.PortName}。");
            Owners[config.PortName] = this;
        }
        connection = config;
        SetStatus(DeviceStatusType.Opening);
        try
        {
            handle = native.Open(config);
            if (handle == IntPtr.Zero) throw new InvalidOperationException($"无法打开滤色轮 {config.PortName}（{config.BaudRate} baud），请检查串口、接线及服务占用。");
            SetStatus(DeviceStatusType.Opened);
        }
        catch
        {
            lock (Owners) Owners.Remove(config.PortName);
            connection = null;
            SetStatus(DeviceStatusType.Closed);
            throw;
        }
    }

    private void CloseCore()
    {
        if (handle != IntPtr.Zero)
        {
            SetStatus(DeviceStatusType.Closing);
            try
            {
                int code = native.Close(handle);
                if (code != 0) throw new InvalidOperationException($"释放滤色轮失败，SDK 返回 {code}。");
                handle = IntPtr.Zero;
            }
            catch { quarantined = true; throw; } // Retain ownership when release cannot be confirmed.
        }
        if (connection != null) lock (Owners) Owners.Remove(connection.PortName);
        connection = null;
        SetStatus(DeviceStatusType.Closed);
    }

    public async ValueTask DisposeAsync()
    {
        disposed = true;
        await gate.WaitAsync().ConfigureAwait(false);
        try { if (!quarantined) await Task.Run(CloseCore).ConfigureAwait(false); }
        finally { gate.Release(); }
    }
}
