using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.Services.Devices.Sensor.Local;

/// <summary>One connection and one exchange at a time. Closing cancels active/queued work and never reconnects.</summary>
internal sealed class LocalSensorSession : IDisposable
{
    internal const int MaxResponseBytes = 1024 * 1024;
    private readonly object sync = new();
    private static readonly object EndpointsSync = new();
    private static readonly HashSet<string> Endpoints = new(StringComparer.Ordinal);
    private string? endpoint;
    private TaskCompletionSource closed = CompletedClose();
    private static TaskCompletionSource CompletedClose() { var result = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously); result.SetResult(); return result; }
    private readonly SemaphoreSlim operations = new(1, 1);
    private readonly Func<LocalSensorConnectionConfig, ILocalSensorTransport> createTransport;
    private ILocalSensorTransport? transport;
    private CancellationTokenSource? connection;
    private Exchange? pending;
    private long generation;
    private long closeRequests;
    private long failedGeneration = -1;
    private Exception? connectionFailure;
    private bool disposed;
    private DeviceStatusType status = DeviceStatusType.Closed;
    public event Action<DeviceStatusType>? StatusChanged;
    public DeviceStatusType Status { get { lock (sync) return status; } }
    internal string? EndpointIdentity { get { lock (sync) return endpoint; } }
    public bool IsOpen => Status is DeviceStatusType.Opened or DeviceStatusType.Busy;

    public LocalSensorSession(Func<LocalSensorConnectionConfig, ILocalSensorTransport>? createTransport = null)
        => this.createTransport = createTransport ?? (config => config.IsNet ? new LocalSensorTcpTransport() : new LocalSensorSerialTransport());

    internal static string EndpointKey(ConfigSensor config) => config.IsNet
        ? $"tcp:{config.Addr?.Trim().ToUpperInvariant()}:{config.Port}"
        : $"serial:{config.Addr?.Trim().ToUpperInvariant()}";

    public Task OpenAsync(LocalSensorConnectionConfig config, CancellationToken token = default)
    {
        long requestedClose;
        lock (sync) requestedClose = closeRequests;
        return OpenCoreAsync(config, token, requestedClose);
    }

    private async Task OpenCoreAsync(LocalSensorConnectionConfig config, CancellationToken token, long requestedClose)
    {
        config.ValidateLocalConnection();
        long requested;
        lock (sync) { ObjectDisposedException.ThrowIf(disposed, this); requested = generation; }
        await operations.WaitAsync(token).ConfigureAwait(false);
        long version = 0;
        ILocalSensorTransport? candidate = null;
        try
        {
            CancellationTokenSource lifetime;
            CancellationToken lifetimeToken;
            lock (sync)
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                if (requested != generation || requestedClose != closeRequests) throw new OperationCanceledException("传感器打开请求已被关闭操作取消。");
                if (status == DeviceStatusType.Closing) throw new InvalidOperationException("传感器正在关闭，请等待关闭完成。");
                if (transport != null) { if (IsOpen) return; throw new InvalidOperationException("传感器正在连接或关闭。"); }
                string key = EndpointKey(config);
                lock (EndpointsSync)
                {
                    if (!Endpoints.Add(key)) throw new InvalidOperationException("另一个本地传感器正在使用此连接。");
                    endpoint = key;
                }
                version = ++generation;
                lifetime = connection = new CancellationTokenSource();
                lifetimeToken = lifetime.Token;
                candidate = transport = createTransport(config);
                status = DeviceStatusType.Opening;
            }
            Notify();
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token, lifetimeToken);
            deadline.CancelAfter(config.ConnectTimeout);
            try { await candidate.OpenAsync(config, deadline.Token).ConfigureAwait(false); }
            catch (Exception) when (deadline.IsCancellationRequested && !token.IsCancellationRequested && !lifetime.IsCancellationRequested)
            { throw new TimeoutException("本地传感器连接超时。"); }
            lock (sync)
            {
                if (generation != version || lifetime.IsCancellationRequested) throw new OperationCanceledException("传感器连接已关闭。");
                status = DeviceStatusType.Opened;
            }
            Notify();
            _ = ReadLoopAsync(candidate, version, lifetimeToken);
        }
        catch { if (version != 0) Abort(version, new IOException("本地传感器连接未完成。")); throw; }
        finally { operations.Release(); }
    }

    public async Task<IReadOnlyList<LocalSensorCommandResult>> ExecuteAsync(IReadOnlyList<LocalSensorCommand> commands, CancellationToken token = default)
    {
        var snapshot = commands.Select(command => command.Snapshot()).ToArray();
        if (snapshot.Length == 0) throw new ArgumentException("模板没有启用的传感器指令。");
        for (int index = 0; index < snapshot.Length; index++)
        {
            if (string.IsNullOrWhiteSpace(snapshot[index].Name)) snapshot[index].Name = "Command " + (index + 1);
            snapshot[index].Validate();
        }
        CancellationToken lifetime;
        long version;
        lock (sync)
        {
            ObjectDisposedException.ThrowIf(disposed, this);
            if (!IsOpen || connection == null) throw new InvalidOperationException("本地传感器未打开，请先连接设备。");
            lifetime = connection.Token;
            version = generation;
        }
        using var cancel = CancellationTokenSource.CreateLinkedTokenSource(token, lifetime);
        await operations.WaitAsync(cancel.Token).ConfigureAwait(false);
        try
        {
            lock (sync)
            {
                if (generation != version || !IsOpen) throw new InvalidOperationException("本地传感器连接已改变，请重新执行。");
                status = DeviceStatusType.Busy;
            }
            Notify();
            var results = new List<LocalSensorCommandResult>();
            foreach (var command in snapshot)
            {
                cancel.Token.ThrowIfCancellationRequested();
                results.Add(await ExecuteOneAsync(command, version, cancel.Token).ConfigureAwait(false));
                if (command.Delay > 0) await Task.Delay(command.Delay, cancel.Token).ConfigureAwait(false);
            }
            return results;
        }
        catch (OperationCanceledException) { Abort(version, new OperationCanceledException("本地传感器操作已取消。")); throw; }
        finally
        {
            lock (sync) { if (generation == version && transport != null) status = DeviceStatusType.Opened; }
            Notify();
            operations.Release();
        }
    }

    private async Task<LocalSensorCommandResult> ExecuteOneAsync(LocalSensorCommand command, long version, CancellationToken token)
    {
        byte[] request = command.Encode(command.Request);
        var elapsed = Stopwatch.StartNew();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        if (command.Timeout > 0) deadline.CancelAfter(command.Timeout);
        int attempts = Math.Max(1, command.RetryCount);
        Exchange? exchange = null;
        try
        {
            for (int attempt = 1; attempt <= attempts; attempt++)
            {
                ILocalSensorTransport current;
                exchange = new Exchange(command);
                lock (sync)
                {
                    if (generation != version || transport == null) throw new IOException("本地传感器已断开。");
                    current = transport;
                    pending = exchange;
                }
                await current.WriteAsync(request, deadline.Token).ConfigureAwait(false);
                byte[] response = [];
                if (command.Timeout > 0)
                {
                    try { response = await exchange.Completion.Task.WaitAsync(deadline.Token).ConfigureAwait(false); }
                    catch (OperationCanceledException) when (!token.IsCancellationRequested && exchange.IsReceiveWindow && exchange.Count > 0)
                    { response = exchange.Bytes; }
                }
                lock (sync) { if (ReferenceEquals(pending, exchange)) pending = null; }
                if (!exchange.Matches(response))
                {
                    if (attempt < attempts) continue;
                    throw new InvalidDataException($"传感器指令 {command.Name} 回包不匹配：预期 {command.Response}，实际 {command.Decode(response)}。");
                }
                return new LocalSensorCommandResult { Name = command.Name, RequestHex = Convert.ToHexString(request), ResponseHex = Convert.ToHexString(response), ResponseText = command.Decode(response), Attempts = attempt, ElapsedMilliseconds = elapsed.ElapsedMilliseconds };
            }
            throw new InvalidOperationException("传感器指令未执行。");
        }
        catch (Exception) when (deadline.IsCancellationRequested && !token.IsCancellationRequested)
        {
            var failure = new TimeoutException($"传感器指令超时：{command.Name}；已收到 {command.Decode(exchange?.Bytes ?? [])}。");
            Abort(version, failure);
            throw failure;
        }
        catch (Exception ex)
        {
            Exception failure;
            lock (sync) failure = failedGeneration == version && connectionFailure != null ? connectionFailure : ex;
            Abort(version, failure);
            Task closure;
            lock (sync) closure = closed.Task;
            await closure.ConfigureAwait(false);
            if (failure is OperationCanceledException) throw new OperationCanceledException("本地传感器操作已取消。", failure, token);
            if (failure is IOException) throw new IOException($"传感器指令 {command.Name} 通信失败：{failure.Message}；已收到 {command.Decode(exchange?.Bytes ?? [])}。", failure);
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
            throw;
        }
        finally { lock (sync) { if (ReferenceEquals(pending, exchange)) pending = null; } }
    }

    private async Task ReadLoopAsync(ILocalSensorTransport current, long version, CancellationToken token)
    {
        byte[] bytes = new byte[4096];
        try
        {
            while (!token.IsCancellationRequested)
            {
                int count = await current.ReadAsync(bytes, token).ConfigureAwait(false);
                if (count == 0) throw new IOException("本地传感器连接已中断。");
                lock (sync)
                {
                    if (version != generation) return;
                    pending?.Receive(bytes.AsSpan(0, count));
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { Abort(version, ex); }
    }

    private void Abort(long version, Exception failure)
    {
        ILocalSensorTransport? previous;
        CancellationTokenSource? cancellation;
        TaskCompletionSource closing;
        string? previousEndpoint;
        lock (sync)
        {
            if (version != generation || status == DeviceStatusType.Closing) return;
            failedGeneration = version; connectionFailure = failure;
            ++generation;
            previous = transport; transport = null;
            cancellation = connection; connection = null;
            previousEndpoint = endpoint;
            closing = closed = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pending?.Completion.TrySetException(failure); pending = null;
            status = DeviceStatusType.Closing;
        }
        try
        {
            Notify();
            try { cancellation?.Cancel(); }
            finally { previous?.Dispose(); }
        }
        finally
        {
            lock (EndpointsSync) { if (previousEndpoint != null) Endpoints.Remove(previousEndpoint); }
            cancellation?.Dispose();
            lock (sync) { if (generation == version + 1) { endpoint = null; status = DeviceStatusType.Closed; } }
            closing.TrySetResult();
            Notify();
        }
    }

    public Task CloseAsync()
    {
        lock (sync) ++closeRequests;
        return CloseConnectionAsync();
    }

    public async Task ReopenAsync(LocalSensorConnectionConfig config, CancellationToken token = default)
    {
        long requestedClose;
        lock (sync) requestedClose = ++closeRequests;
        await CloseConnectionAsync().ConfigureAwait(false);
        token.ThrowIfCancellationRequested();
        await OpenCoreAsync(config, token, requestedClose).ConfigureAwait(false);
    }

    private async Task CloseConnectionAsync()
    {
        long version;
        lock (sync) version = generation;
        Abort(version, new OperationCanceledException("本地传感器已关闭。"));
        Task completion;
        lock (sync) completion = closed.Task;
        await completion.ConfigureAwait(false);
        await operations.WaitAsync().ConfigureAwait(false);
        operations.Release();
    }

    private void Notify()
    {
        var current = Status;
        foreach (var handler in StatusChanged?.GetInvocationList() ?? Array.Empty<Delegate>())
        {
            try { ((Action<DeviceStatusType>)handler)(current); }
            catch (Exception ex) { MQTTServiceBase.log.Error("本地传感器状态订阅失败。", ex); }
        }
    }

    public void Dispose()
    {
        long version;
        lock (sync) { if (disposed) return; disposed = true; version = generation; }
        Abort(version, new ObjectDisposedException(nameof(LocalSensorSession)));
    }

    internal sealed class Exchange(LocalSensorCommand command)
    {
        private readonly object receiveSync = new();
        private readonly List<byte> received = [];
        private readonly byte[] expected = command.Encode(command.Response);
        public TaskCompletionSource<byte[]> Completion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool IsReceiveWindow => expected.Length == 0;
        public int Count { get { lock (receiveSync) return received.Count; } }
        public byte[] Bytes { get { lock (receiveSync) return received.ToArray(); } }

        public void Receive(ReadOnlySpan<byte> bytes)
        {
            lock (receiveSync)
            {
                if (Completion.Task.IsCompleted || command.Timeout <= 0) return;
                if (received.Count + bytes.Length > MaxResponseBytes) throw new IOException("传感器回包超过 1048576 字节限制。");
                received.AddRange(bytes.ToArray());
                byte[] buffer = Bytes;
                int length = expected.Length;
                if (length > 0 && buffer.Length >= length) Completion.TrySetResult(buffer[..length]);
            }
        }

        public bool Matches(byte[] bytes) => command.Timeout <= 0 || expected.Length == 0 || bytes.AsSpan().SequenceEqual(expected);
    }
}
