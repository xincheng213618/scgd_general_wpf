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
    private LocalSmuFlowLease? flowLease;
    private DeviceStatusType status = DeviceStatusType.Closed;
    internal event Action<DeviceStatusType>? StatusChanged;
    internal DeviceStatusType Status { get { lock (sync) return status; } }
    internal bool IsOpen => Status is DeviceStatusType.Opened or DeviceStatusType.Busy;
    internal string? Endpoint { get { lock (sync) return connection?.Endpoint; } }
    internal string Idn { get; private set; } = string.Empty;
    internal int SweepCapabilities { get; private set; }
    internal int LocalFamily { get; private set; }
    private bool advancedMode;

    internal LocalSmuSession(ILocalSmuNative? native = null) => this.native = native ?? new LocalSmuNative();
    private void SetStatus(DeviceStatusType value) { lock (sync) status = value; StatusChanged?.Invoke(value); }
    private static void Check(int code, string operation)
    {
        if (code != 1) throw new InvalidOperationException($"{operation}失败，SDK 返回 {code}。");
    }

    internal Task OpenAsync(LocalSmuConnection config, CancellationToken token = default, object? flowOwner = null) => RunAsync(() =>
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
            Check(native.SetDelay(handle, config.DelayTime), "设置源表延时");
            var text = new StringBuilder(1024);
            int length = text.Capacity;
            Check(native.GetIdn(handle, text, ref length), "读取源表标识");
            Idn = text.ToString();
            SweepCapabilities = native.GetSweepCapabilities(handle);
            LocalFamily = native.GetLocalFamily(handle);
            SetStatus(DeviceStatusType.Opened);
            return true;
        }
        catch (Exception failure)
        {
            try { CloseCore(); }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }, token, flowOwner);

    internal Task<LocalSmuCapture> CaptureAsync(LocalSmuConnection config, LocalSmuParameters parameters, bool scan, bool step,
        bool checkLimits, bool read = false, CancellationToken token = default, object? flowOwner = null)
    {
        parameters = parameters with { SourceValues = parameters.SourceValues?.ToArray() };
        return RunAsync(() =>
        {
            EnsureConnection(config);
            if (read && advancedMode) throw new InvalidOperationException("当前使用本地高级模式，请从本地源表窗口执行测量；切换普通点测后可使用旧读取命令。");
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
                if (!read) advancedMode = false;
                if (!read) lastParameters = parameters;
                // A failing or canceled native call may already have enabled the selected output.
                outputChannels.Add(parameters.Channel);
                long generation = ++nextGeneration;
                outputGenerations[parameters.Channel] = generation;
                var watch = Stopwatch.StartNew();
                double[] voltages = new double[scan ? parameters.Points : 1], currents = new double[scan ? parameters.Points : 1];
                if (scan) Check(parameters.IsListSweep
                    ? native.List(handle, parameters, parameters.SourceValues!, voltages, currents)
                    : native.Sweep(handle, parameters, voltages, currents), "源表扫描");
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
            }, token, flowOwner);
    }

    internal Task CloseOutputAsync(SMUChannelType channel, CancellationToken token = default, long? expectedGeneration = null, object? flowOwner = null) => RunAsync(() =>
    {
        if (handle < 0) throw new InvalidOperationException("请先打开本地源表。");
        // A discarded flow result must not switch off a later measurement on the same channel.
        if (expectedGeneration.HasValue && (!outputGenerations.TryGetValue(channel, out long current) || current != expectedGeneration)) return false;
        try { CloseChannel(channel); }
        catch { SetStatus(DeviceStatusType.Unknown); throw; }
        SetStatus(DeviceStatusType.Opened);
        return true;
    }, token, flowOwner);

    internal Task<LocalSmuCapture> CaptureAdvancedAsync(LocalSmuConnection config, LocalSmuSettings settings, bool checkLimits,
        CancellationToken token = default, object? flowOwner = null)
    {
        settings = settings.Clone();
        settings.Validate(config.DeviceType, checkLimits);
        double[] sources = settings.SourceValues(), limits = settings.Limits(sources.Length);
        return RunAsync(() =>
        {
            EnsureConnection(config);
            if (LocalFamily == 0) throw new NotSupportedException("当前 DLL 或仪器命令集不支持本地高级源表功能；请核对实际型号并更新 DLL。");
            settings.ValidateFamily(LocalFamily);
            SetStatus(DeviceStatusType.Busy);
            SelectChannel(settings.Channel);
            outputChannels.Add(settings.Channel);
            long generation = ++nextGeneration;
            outputGenerations[settings.Channel] = generation;
            try
            {
                double[] v = new double[sources.Length], i = new double[sources.Length], r = new double[sources.Length];
                int[] status = new int[sources.Length];
                var watch = Stopwatch.StartNew();
                advancedMode = true;
                native.ExecuteAdvanced(handle, settings, sources, limits, v, i, r, status);
                token.ThrowIfCancellationRequested();
                lock (sync) lifetime.Token.ThrowIfCancellationRequested();
                bool measured = settings.Operation != SmuOperation.SourceOnly;
                if (measured && (v.Any(value => !double.IsFinite(value) || Math.Abs(value) >= 1e36)
                    || i.Any(value => !double.IsFinite(value) || Math.Abs(value) >= 1e36))) throw new InvalidOperationException("源表返回无效或超量程的 V/I。");
                var parameters = new LocalSmuParameters { Channel = settings.Channel, IsSourceV = settings.IsSourceV,
                    MeasureValue = sources[^1], LimitValue = settings.Limit, BeginValue = sources[0], EndValue = sources[^1],
                    SourceValues = sources, Points = sources.Length, IsCloseOutput = settings.CloseOutput, AdvancedSettings = settings };
                lastParameters = null;
                if (settings.CloseOutput) { outputChannels.Remove(settings.Channel); outputGenerations.Remove(settings.Channel); }
                return new LocalSmuCapture(parameters, measured ? v : [], measured ? i.Select(value => value * 1000).ToArray() : [],
                    (int)Math.Min(watch.ElapsedMilliseconds, int.MaxValue), measured && sources.Length > 1)
                { Generation = generation, Details = new(settings, sources,
                    measured ? r.Select(value => double.IsFinite(value) && Math.Abs(value) < 1e36 ? (double?)value : null).ToArray() : [],
                    measured ? status : [], settings.Waveform == SmuWaveform.DC ? "DC" : LocalFamily == 1 ? "2400-SDM-Approximate" : LocalFamily == 2 ? "2430-Pulse" : "2600-Timer") };
            }
            catch
            {
                try { CloseChannel(settings.Channel); }
                catch { SetStatus(DeviceStatusType.Unknown); throw; }
                throw;
            }
            finally { if (Status == DeviceStatusType.Busy) SetStatus(DeviceStatusType.Opened); }
        }, token, flowOwner);
    }

    internal Task CloseAdvancedOutputAsync(LocalSmuConnection config, LocalSmuSettings settings, CancellationToken token, object? owner)
    {
        settings = settings.Clone();
        return RunAsync(() =>
        {
            EnsureConnection(config);
            if (!Enum.IsDefined(settings.Channel) || !Enum.IsDefined(settings.OffMode)) throw new ArgumentException("关闭输出的通道或状态无效。");
            SelectChannel(settings.Channel);
            try
            {
                Check(native.CloseAdvancedOutput(handle, (int)settings.Channel, (int)settings.OffMode), "设置关闭状态并关闭源表输出");
                outputChannels.Remove(settings.Channel); outputGenerations.Remove(settings.Channel);
                SetStatus(DeviceStatusType.Opened); return true;
            }
            catch { SetStatus(DeviceStatusType.Unknown); throw; }
        }, token, owner);
    }

    internal async Task<LocalSmuFlowLease> AcquireFlowLeaseAsync(object owner, Func<bool> failed, CancellationToken token = default)
    {
        LocalSmuFlowLease? created = null;
        try
        {
            return await RunAsync(() =>
            {
                lock (sync) return flowLease ??= created = new LocalSmuFlowLease(this, owner, failed);
            }, token, owner).ConfigureAwait(false);
        }
        catch
        {
            // Cancellation may win after the reservation was created but before its caller can register it.
            lock (sync) if (created != null && ReferenceEquals(flowLease, created)) flowLease = null;
            throw;
        }
    }

    internal Task ReleaseFlowLeaseAsync(LocalSmuFlowLease lease, bool failed) => RunAsync(() =>
    {
        lock (sync) if (!ReferenceEquals(flowLease, lease)) return false;
        try
        {
            List<Exception> failures = [];
            foreach (var channel in lease.Channels)
            {
                if (!(failed || channel.Value) || !outputChannels.Contains(channel.Key)) continue;
                try { CloseChannel(channel.Key); }
                catch (Exception ex) { failures.Add(ex); }
            }
            if (failures.Count != 0) throw new AggregateException("本地源表流程输出关闭失败。", failures);
            return true;
        }
        catch { SetStatus(DeviceStatusType.Unknown); throw; }
        finally { lock (sync) if (ReferenceEquals(flowLease, lease)) flowLease = null; }
    }, CancellationToken.None, lease.Owner);

    private void EnsureFlowOwner(object? owner)
    {
        if (flowLease != null && !ReferenceEquals(flowLease.Owner, owner))
            throw new InvalidOperationException("源表正在被流程使用，请等待流程结束后再操作。");
    }

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

    private async Task<T> RunAsync<T>(Func<T> work, CancellationToken token, object? flowOwner = null)
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
                lock (sync) EnsureFlowOwner(flowOwner);
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

    internal Task CloseAsync(object? flowOwner = null, bool force = false)
    {
        lock (sync)
        {
            if (!force) EnsureFlowOwner(flowOwner);
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
        SweepCapabilities = 0;
        LocalFamily = 0;
        lastParameters = null;
        advancedMode = false;
        SetStatus(DeviceStatusType.Closed);
    }
    public async ValueTask DisposeAsync()
    {
        lock (sync) { if (disposed) return; disposed = true; }
        await CloseAsync(force: true).ConfigureAwait(false);
    }
}

// This lease belongs to the entire flow, so the source stays on while downstream instruments measure.
internal sealed class LocalSmuFlowLease(LocalSmuSession session, object owner, Func<bool> failed) : IDisposable
{
    private readonly object sync = new();
    private readonly Dictionary<SMUChannelType, bool> channels = [];
    private bool disposed;
    internal object Owner => owner;
    internal KeyValuePair<SMUChannelType, bool>[] Channels { get { lock (sync) return channels.ToArray(); } }

    internal void ConfigureChannel(SMUChannelType channel, bool closeOnFinish)
    {
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            channels[channel] = closeOnFinish || channels.GetValueOrDefault(channel);
        }
    }

    public void Dispose()
    {
        lock (sync) { if (disposed) return; disposed = true; }
        try { session.ReleaseFlowLeaseAsync(this, failed()).GetAwaiter().GetResult(); }
        catch (Exception ex) { MQTTServiceBase.log.Error("本地源表流程输出清理失败", ex); }
    }
}
