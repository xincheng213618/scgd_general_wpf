using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.CfwPort.Local;

internal sealed record LocalCfwPortConnection(string PortName, int BaudRate, int Timeout, int RetryCount, int Ports, int Delay)
{
    internal static LocalCfwPortConnection From(ConfigCfwPort config) => new(config.SzComName?.Trim().ToUpperInvariant() ?? string.Empty,
        config.BaudRate, config.Timeout, config.RetryCount, config.Ports, config.Delay);

    internal void Validate()
    {
        if (!PortName.StartsWith("COM", StringComparison.OrdinalIgnoreCase) || !int.TryParse(PortName.AsSpan(3), out int number) || number <= 0)
            throw new ArgumentException("请选择有效的滤色轮串口，例如 COM12。");
        if (BaudRate <= 0 || Timeout <= 0 || RetryCount < 0 || Delay < 0 || Ports is not (6 or 12))
            throw new ArgumentException("滤色轮波特率和超时必须大于零，重试次数和延时不能为负数，孔位数量须为 6 或 12。");
    }
}

internal sealed class LocalCfwPortSession(ILocalCfwPortTransport? transport = null) : IAsyncDisposable
{
    private static readonly Dictionary<string, LocalCfwPortSession> Owners = new(StringComparer.OrdinalIgnoreCase);
    internal static void EnsureNotLocallyOwned(string endpoint)
    {
        lock (Owners)
            if (Owners.ContainsKey(endpoint)) throw new InvalidOperationException("本地滤色轮或光谱 ND 正在使用此串口，请先关闭本地连接。");
    }
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILocalCfwPortTransport transport = transport ?? new LocalCfwPortTransport();
    private LocalCfwPortConnection? connection;
    private volatile bool disposed;
    private bool quarantined;
    private DeviceStatusType status = DeviceStatusType.Closed;
    internal event Action<DeviceStatusType>? StatusChanged;
    internal DeviceStatusType Status => status;
    internal string? Endpoint => connection?.PortName;

    private void SetStatus(DeviceStatusType value)
    {
        status = value;
        foreach (var handler in StatusChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action<DeviceStatusType>)handler)(value); }
            catch (Exception error) { MQTTServiceBase.log.Error("本地滤色轮状态订阅失败。", error); }
        }
    }

    internal async Task<int?> ExecuteAsync(string operation, LocalCfwPortConnection config, int? port = null, bool autoConnect = false, CancellationToken token = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await gate.WaitAsync(token).ConfigureAwait(false);
        try
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            token.ThrowIfCancellationRequested();
            // Cancellation must never close the serial port while a blocking command still uses it.
            int? result = await Task.Run(() => ExecuteCore(operation, config, port, autoConnect, token), CancellationToken.None).ConfigureAwait(false);
            if (operation != "Open") token.ThrowIfCancellationRequested();
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
        if (operation == "Open") return OpenCore(config, token);
        if (autoConnect && connection == null) OpenCore(config, token);
        if (connection == null) throw new InvalidOperationException("请先连接本地滤色轮。");
        if (connection != config) throw new InvalidOperationException("滤色轮配置已改变，请关闭连接后重新连接。");

        SetStatus(DeviceStatusType.Busy);
        try
        {
            int actual = -1;
            Exception? failure = null;
            for (int attempt = 0; ; attempt++)
            {
                token.ThrowIfCancellationRequested();
                actual = -1;
                failure = null;
                try
                {
                    if (operation == "SetPort")
                    {
                        transport.SetPort(port!.Value);
                        if (config.Delay != 0) token.WaitHandle.WaitOne(config.Delay);
                        token.ThrowIfCancellationRequested();
                    }
                    actual = transport.ReadPort(2000);
                }
                catch (Exception error) when (error is TimeoutException or IOException) { failure = error; }

                token.ThrowIfCancellationRequested();
                if (actual >= 0 && actual < config.Ports && (operation != "SetPort" || actual == port)) return actual;
                if (attempt >= config.RetryCount) break;
            }

            string detail = failure is TimeoutException ? "等待串口回包超时。" : failure?.Message ?? $"实际孔位 {actual}。";
            throw new InvalidOperationException($"滤色轮{(operation == "SetPort" ? "定位" : "读位")}失败，{detail} 目标孔位 {port?.ToString() ?? "读取"}。", failure);
        }
        finally { SetStatus(DeviceStatusType.Opened); }
    }

    private int? OpenCore(LocalCfwPortConnection config, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        if (connection != null)
        {
            if (connection == config) return null;
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
            token.ThrowIfCancellationRequested();
            transport.Open(config);
            int port = ConfirmOpenPosition(config, token);
            SetStatus(DeviceStatusType.Opened);
            return port;
        }
        catch (Exception error)
        {
            CloseCore();
            token.ThrowIfCancellationRequested();
            throw new InvalidOperationException($"无法连接本地滤色轮 {config.PortName}（{config.BaudRate} baud）。{error.Message}", error);
        }
    }

    private int ConfirmOpenPosition(LocalCfwPortConnection config, CancellationToken token)
    {
        for (int attempt = 0; ; attempt++)
        {
            token.ThrowIfCancellationRequested();
            int? port = null;
            TimeoutException? timeout = null;
            try { port = transport.ReadPort(config.Timeout); }
            catch (TimeoutException error) { timeout = error; }

            token.ThrowIfCancellationRequested();
            if (port is int actual && actual >= 0 && actual < config.Ports) return actual;
            if (attempt < config.RetryCount) continue;

            string detail = timeout != null
                ? "等待 NOW 孔位回包超时。"
                : $"NOW 返回无效孔位 {port}，有效范围为 0–{config.Ports - 1}。";
            throw new InvalidOperationException($"{detail} 共尝试 {attempt + 1L} 次。", timeout);
        }
    }

    private void CloseCore()
    {
        if (connection != null)
        {
            SetStatus(DeviceStatusType.Closing);
            try { transport.Close(); }
            catch (Exception error)
            {
                quarantined = true;
                throw new InvalidOperationException($"释放滤色轮失败（{connection.PortName}）。", error);
            }

            lock (Owners) Owners.Remove(connection.PortName);
            connection = null;
        }
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
