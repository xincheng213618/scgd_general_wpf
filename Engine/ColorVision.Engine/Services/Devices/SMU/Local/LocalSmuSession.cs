using ColorVision.Engine.Services.Devices.SMU.Dao;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.SMU.Local;

internal sealed class LocalSmuSession : IAsyncDisposable
{
    private static readonly object OwnersSync = new();
    private static readonly Dictionary<string, LocalSmuSession> Owners = new(StringComparer.OrdinalIgnoreCase);
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly ILocalSmuNative native;
    private readonly HashSet<SMUChannelType> outputChannels = [];
    private readonly Dictionary<SMUChannelType, long> outputGenerations = [];
    private long nextGeneration;
    private CancellationTokenSource lifetime = new();
    private Task? closing;
    private LocalSmuConnection? connection;
    private LocalSmuParameters? lastParameters;
    private int handle = -1;
    private bool disposed, quarantined;
    private DeviceStatusType status = DeviceStatusType.Closed;
    internal event Action<DeviceStatusType>? StatusChanged;
    internal DeviceStatusType Status { get { lock (sync) return status; } }
    internal bool IsOpen => Status is DeviceStatusType.Opened or DeviceStatusType.Busy;
    internal string? Endpoint { get { lock (sync) return connection?.Endpoint; } }
    internal string Idn { get; private set; } = string.Empty;

    internal LocalSmuSession(ILocalSmuNative? native = null) => this.native = native ?? new LocalSmuNative();
    private void SetStatus(DeviceStatusType value) { lock (sync) status = value; StatusChanged?.Invoke(value); }
    private static void Check(int code, string operation)
    {
        if (code != 1) throw new InvalidOperationException($"{operation}失败，SDK 返回 {code}。");
    }

    internal Task OpenAsync(LocalSmuConnection config, CancellationToken token = default) => RunAsync(() =>
    {
        config.Validate();
        if (handle >= 0)
        {
            EnsureConnection(config);
            return true;
        }
        lock (OwnersSync)
        {
            if (Owners.TryGetValue(config.Endpoint, out var owner) && !ReferenceEquals(owner, this)) throw new InvalidOperationException("另一个本地源表正在使用此连接。");
            Owners[config.Endpoint] = this;
        }
        lock (sync) connection = config;
        SetStatus(DeviceStatusType.Opening);
        try
        {
            handle = native.Open(config);
            if (handle < 0) throw new InvalidOperationException($"打开本地源表失败，SDK 返回 {handle}。");
            Check(native.SetWiring(handle, config.Is4Wire, config.IsFront), "设置源表接线");
            if (config.DelayTime > 0) Check(native.SetDelay(handle, config.DelayTime), "设置源表延时");
            var text = new StringBuilder(1024);
            int length = text.Capacity;
            Check(native.GetIdn(handle, text, ref length), "读取源表标识");
            Idn = text.ToString();
            SetStatus(DeviceStatusType.Opened);
            return true;
        }
        catch (Exception failure)
        {
            try { CloseCore(); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }, token);

    internal Task<LocalSmuCapture> CaptureAsync(LocalSmuConnection config, LocalSmuParameters parameters, bool scan, bool step,
        bool checkLimits, bool read = false, CancellationToken token = default) => RunAsync(() =>
    {
        EnsureConnection(config);
        if (read && lastParameters != null) parameters = lastParameters with { IsCloseOutput = false };
        if (!read) parameters.Validate(config.DeviceType, scan, checkLimits);
        else if (!Enum.IsDefined(parameters.Channel)) throw new ArgumentException("源表通道无效。");
        SetStatus(DeviceStatusType.Busy);
        Exception? failure = null;
        LocalSmuCapture? capture = null;
        try
        {
            SelectChannel(parameters.Channel);
            if (!read) Check(native.SetSource(handle, parameters.IsSourceV), "设置源类型");
            if (!read) lastParameters = parameters;
            // A failing or canceled native call may already have enabled the selected output.
            outputChannels.Add(parameters.Channel);
            long generation = ++nextGeneration;
            outputGenerations[parameters.Channel] = generation;
            var watch = Stopwatch.StartNew();
            double[] voltages = new double[scan ? parameters.Points : 1], currents = new double[scan ? parameters.Points : 1];
            if (scan) Check(native.Sweep(handle, parameters, voltages, currents), "源表扫描");
            else if (read) Check(native.Read(handle, ref voltages[0], ref currents[0]), "读取源表");
            else Check(native.Measure(handle, parameters, step, ref voltages[0], ref currents[0]), "源表测量");
            token.ThrowIfCancellationRequested();
            lock (sync) lifetime.Token.ThrowIfCancellationRequested();
            for (int index = 0; index < currents.Length; index++)
            {
                currents[index] *= 1000;
                if (!double.IsFinite(voltages[index]) || !double.IsFinite(currents[index])) throw new InvalidOperationException("源表返回了无效测量数据。");
            }
            capture = new LocalSmuCapture(parameters, voltages, currents, (int)Math.Min(watch.ElapsedMilliseconds, int.MaxValue), scan) { Generation = generation };
        }
        catch (Exception ex) { failure = ex; }
        try
        {
            if (failure != null || parameters.IsCloseOutput) CloseChannel(parameters.Channel);
        }
        catch (Exception cleanup)
        {
            SetStatus(DeviceStatusType.Unknown);
            throw failure == null ? cleanup : new AggregateException(failure, cleanup);
        }
        finally { if (Status == DeviceStatusType.Busy) SetStatus(DeviceStatusType.Opened); }
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
        return capture!;
    }, token);

    internal Task CloseOutputAsync(SMUChannelType channel, CancellationToken token = default, long? expectedGeneration = null) => RunAsync(() =>
    {
        if (handle < 0) throw new InvalidOperationException("请先打开本地源表。");
        // A discarded flow result must not switch off a later measurement on the same channel.
        if (expectedGeneration.HasValue && (!outputGenerations.TryGetValue(channel, out long current) || current != expectedGeneration)) return false;
        try { CloseChannel(channel); }
        catch { SetStatus(DeviceStatusType.Unknown); throw; }
        SetStatus(DeviceStatusType.Opened);
        return true;
    }, token);

    private void EnsureConnection(LocalSmuConnection config)
    {
        if (handle < 0) throw new InvalidOperationException("请先打开本地源表。");
        if (connection != config) throw new InvalidOperationException("源表连接配置已更改，请先关闭连接后重新打开。");
    }
    private void SelectChannel(SMUChannelType channel)
    {
        if (!Enum.IsDefined(channel)) throw new ArgumentException("源表通道无效。");
        Check(native.SetChannel(handle, channel == SMUChannelType.A), "选择源表通道");
    }
    private void CloseChannel(SMUChannelType channel)
    {
        SelectChannel(channel);
        Check(native.CloseOutput(handle), "关闭源表输出");
        outputChannels.Remove(channel);
        outputGenerations.Remove(channel);
    }

    private async Task<T> RunAsync<T>(Func<T> work, CancellationToken token)
    {
        CancellationTokenSource linked;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (closing is { IsCompleted: false }) throw new InvalidOperationException("源表正在关闭，请等待关闭完成。");
            if (quarantined) throw new InvalidOperationException("源表释放失败，请重启程序后再连接。");
            linked = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime.Token);
        }
        using (linked)
        {
            await gate.WaitAsync(linked.Token).ConfigureAwait(false);
            bool started = false;
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                // Do not abandon a blocking native call: keep the handle and endpoint lease until it returns.
                started = true;
                T result = await Task.Run(work, CancellationToken.None).ConfigureAwait(false);
                linked.Token.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) when (started)
            {
                try { await Task.Run(CloseCore, CancellationToken.None).ConfigureAwait(false); }
                catch (Exception cleanup) { throw new AggregateException("源表取消后的释放失败。", cleanup); }
                throw;
            }
            finally { gate.Release(); }
        }
    }

    internal Task CloseAsync()
    {
        lock (sync)
        {
            if (closing is { IsCompleted: false }) return closing;
            lifetime.Cancel();
            closing = CloseAfterWorkAsync();
            return closing;
        }
    }
    private async Task CloseAfterWorkAsync()
    {
        await gate.WaitAsync().ConfigureAwait(false);
        try { await Task.Run(CloseCore).ConfigureAwait(false); }
        finally
        {
            lock (sync) { lifetime.Dispose(); lifetime = new CancellationTokenSource(); }
            gate.Release();
        }
    }
    private void CloseCore()
    {
        SetStatus(DeviceStatusType.Closing);
        List<Exception> failures = [];
        foreach (var channel in outputChannels.ToArray())
        {
            try { CloseChannel(channel); }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (handle >= 0)
        {
            try { Check(native.Close(handle), "关闭源表连接"); handle = -1; }
            catch (Exception ex) { failures.Add(ex); }
        }
        if (failures.Count != 0)
        {
            quarantined = true;
            SetStatus(DeviceStatusType.Unknown);
            throw new AggregateException("源表输出或连接释放失败。", failures);
        }
        lock (OwnersSync)
        {
            if (connection != null && Owners.TryGetValue(connection.Endpoint, out var owner) && ReferenceEquals(owner, this)) Owners.Remove(connection.Endpoint);
        }
        lock (sync) connection = null;
        Idn = string.Empty;
        lastParameters = null;
        SetStatus(DeviceStatusType.Closed);
    }
    public async ValueTask DisposeAsync()
    {
        lock (sync) { if (disposed) return; disposed = true; }
        await CloseAsync().ConfigureAwait(false);
    }
}
