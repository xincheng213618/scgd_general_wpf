using ST.Library.UI.NodeContainer;
using ST.Library.UI.NodeEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace FlowEngineLib.Runtime;

public enum FlowRuntimeHostState
{
    Empty,
    Loading,
    Ready,
    Running,
    Stopping,
    Faulted,
    Disposed
}

/// <summary>
/// Owns one isolated, non-visual runtime graph generation.
/// </summary>
public sealed class FlowRuntimeHost : IDisposable, IAsyncDisposable
{
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CVNodeContainer _container;
    private readonly FlowNodeManager _nodeManager;
    private readonly FlowRuntimeServiceResolver _serviceResolver;
    private readonly FlowEngineControl _control;
    private readonly FlowEngineRunner _runner;
    private CancellationTokenSource? _activeRunCts;
    private Task<FlowEngineRunResult>? _activeRun;
    private FlowRuntimeHostState _state;
    private bool _disposed;

    public FlowRuntimeHost()
        : this(new FlowNodeManager())
    {
    }

    public FlowRuntimeHost(FlowNodeManager nodeManager)
    {
        _nodeManager = nodeManager
            ?? throw new ArgumentNullException(nameof(nodeManager));
        _container = new CVNodeContainer();
        _serviceResolver = new FlowRuntimeServiceResolver();
        _control = new FlowEngineControl(
            _container,
            isAutoStartName: false,
            _nodeManager,
            _serviceResolver);
        _runner = new FlowEngineRunner(_control);
    }

    public FlowRuntimeHostState State => _state;

    public string? ContentHash { get; private set; }

    public IReadOnlyList<STNode> Nodes => _container.Nodes
        .Cast<STNode>()
        .ToArray();

    public async Task LoadAsync(
        byte[] canvasData,
        IEnumerable<MQTTServiceInfo>? services = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvasData);
        byte[] snapshot = (byte[])canvasData.Clone();
        MQTTServiceInfo[] serviceSnapshot =
            FlowRuntimeServiceResolver.CreateSnapshot(services);
        FlowRuntimeServiceCatalog nextServiceCatalog =
            FlowRuntimeServiceResolver.CreateCatalog(serviceSnapshot);
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        FlowRuntimeHostState previousState = _state;
        FlowRuntimeServiceCatalog? previousServiceCatalog = null;
        try
        {
            ThrowIfDisposed();
            if (_state is FlowRuntimeHostState.Running
                or FlowRuntimeHostState.Stopping)
            {
                throw new InvalidOperationException(
                    "A running flow graph cannot be replaced.");
            }

            _state = FlowRuntimeHostState.Loading;
            cancellationToken.ThrowIfCancellationRequested();
            previousServiceCatalog =
                _serviceResolver.Replace(nextServiceCatalog);
            using (_serviceResolver.EnterLoadScope())
                _control.Load(snapshot, waitReady: false);
            if (serviceSnapshot.Length > 0)
                _nodeManager.UpdateDevice(serviceSnapshot.ToList());
            ContentHash = Convert.ToHexString(
                    SHA256.HashData(snapshot))
                .ToLowerInvariant();
            _state = FlowRuntimeHostState.Ready;
        }
        catch
        {
            if (previousServiceCatalog != null)
                _serviceResolver.Replace(previousServiceCatalog);
            _state = previousState == FlowRuntimeHostState.Ready
                ? FlowRuntimeHostState.Ready
                : FlowRuntimeHostState.Faulted;
            throw;
        }
        finally
        {
            _lifecycleGate.Release();
        }
    }

    public Task LoadBase64Async(
        string canvasBase64,
        IEnumerable<MQTTServiceInfo>? services = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(canvasBase64);
        return LoadAsync(
            Convert.FromBase64String(canvasBase64),
            services,
            cancellationToken);
    }

    public async Task<FlowEngineRunResult> RunAsync(
        string startNodeName,
        string serialNumber,
        TimeSpan? readinessTimeout = null,
        TimeSpan? executionTimeout = null,
        CancellationToken cancellationToken = default)
    {
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        Task<FlowEngineRunResult> runTask;
        try
        {
            ThrowIfDisposed();
            if (_state != FlowRuntimeHostState.Ready)
            {
                throw new InvalidOperationException(
                    $"The runtime host is not ready. Current state: {_state}.");
            }

            _state = FlowRuntimeHostState.Running;
            _activeRunCts = CancellationTokenSource.CreateLinkedTokenSource(
                cancellationToken);
            runTask = _runner.RunAsync(
                startNodeName,
                serialNumber,
                readinessTimeout ?? TimeSpan.FromSeconds(2),
                executionTimeout,
                _activeRunCts.Token);
            _activeRun = runTask;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        try
        {
            return await runTask.ConfigureAwait(false);
        }
        finally
        {
            await _lifecycleGate
                .WaitAsync(CancellationToken.None)
                .ConfigureAwait(false);
            try
            {
                _activeRunCts?.Dispose();
                _activeRunCts = null;
                _activeRun = null;
                if (!_disposed)
                    _state = FlowRuntimeHostState.Ready;
            }
            finally
            {
                _lifecycleGate.Release();
            }
        }
    }

    public async Task StopAsync(
        TimeSpan? waitTimeout = null,
        CancellationToken cancellationToken = default)
    {
        Task<FlowEngineRunResult>? activeRun;
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_state != FlowRuntimeHostState.Running)
                return;

            _state = FlowRuntimeHostState.Stopping;
            _activeRunCts?.Cancel();
            activeRun = _activeRun;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (activeRun == null)
            return;

        TimeSpan timeout = waitTimeout ?? TimeSpan.FromSeconds(5);
        await activeRun.WaitAsync(timeout, cancellationToken)
            .ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        Task<FlowEngineRunResult>? activeRun;
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
                return;

            _disposed = true;
            _state = FlowRuntimeHostState.Disposed;
            _activeRunCts?.Cancel();
            activeRun = _activeRun;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (activeRun != null)
        {
            try
            {
                await activeRun.WaitAsync(TimeSpan.FromSeconds(5))
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }

        _control.FlowClear();
        _control.Dispose();
        _container.Dispose();
        _activeRunCts?.Dispose();
        _lifecycleGate.Dispose();
        GC.SuppressFinalize(this);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }
}
