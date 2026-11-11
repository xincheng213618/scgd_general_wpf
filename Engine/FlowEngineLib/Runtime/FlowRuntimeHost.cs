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
    // Kept alive for callers that entered the gate while disposal was in progress.
    private readonly SemaphoreSlim _lifecycleGate = new(1, 1);
    private readonly CVNodeContainer _container;
    private readonly FlowNodeManager _nodeManager;
    private readonly FlowRuntimeServiceResolver _serviceResolver;
    private readonly FlowEngineControl _control;
    private readonly FlowEngineRunner _runner;
    private ActiveRun? _activeRun;
    private Task? _disposal;
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

    public bool PersistResults { get => _control.PersistResults; set => _control.PersistResults = value; }

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
            // Connection handlers capture topics while the graph is being loaded.
            using (_serviceResolver.EnterLoadScope())
                _control.Load(snapshot, waitReady: false, reuseLoadedCanvas: false);
            if (serviceSnapshot.Length > 0)
                _nodeManager.UpdateDevice(serviceSnapshot.ToList());
            ContentHash = Convert.ToHexString(
                    SHA256.HashData(snapshot))
                .ToLowerInvariant();
            _state = FlowRuntimeHostState.Ready;
        }
        catch when (_state == FlowRuntimeHostState.Loading)
        {
            // Rejecting a replacement must not change the active run's lifecycle.
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
        ActiveRun run;
        try
        {
            ThrowIfDisposed();
            if (_state != FlowRuntimeHostState.Ready)
            {
                throw new InvalidOperationException(
                    $"The runtime host is not ready. Current state: {_state}.");
            }

            run = new ActiveRun(cancellationToken);
            _state = FlowRuntimeHostState.Running;
            run.Runner = _runner.RunAsync(
                startNodeName,
                serialNumber,
                readinessTimeout ?? TimeSpan.FromSeconds(2),
                executionTimeout,
                run.Source.Token);
            _activeRun = run;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        try
        {
            return await run.Runner.ConfigureAwait(false);
        }
        finally
        {
            await SettleRunAsync(run).ConfigureAwait(false);
        }
    }

    private async Task SettleRunAsync(ActiveRun run)
    {
        Task cancellation;
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try { cancellation = run.SealCancellation(); }
        finally { _lifecycleGate.Release(); }

        // The stop/dispose caller observes callback errors. The run retains its own outcome.
        try { await cancellation.ConfigureAwait(false); }
        catch (Exception) { }
        run.Source.Dispose();

        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            _activeRun = null;
            if (!_disposed) _state = FlowRuntimeHostState.Ready;
            run.Settled.SetResult();
        }
        finally { _lifecycleGate.Release(); }
    }

    public async Task StopAsync(
        TimeSpan? waitTimeout = null,
        CancellationToken cancellationToken = default)
    {
        Task stop;
        await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (_activeRun is not { } run)
                return;

            _state = FlowRuntimeHostState.Stopping;
            stop = Task.WhenAll(run.RequestCancellation(), run.Runner, run.Settled.Task);
        }
        finally
        {
            _lifecycleGate.Release();
        }

        TimeSpan timeout = waitTimeout ?? TimeSpan.FromSeconds(5);
        await stop.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    public async ValueTask DisposeAsync()
    {
        TaskCompletionSource? owner = null;
        Task? activeSettlement = null;
        Task cancellation = Task.CompletedTask;
        Task disposal;
        await _lifecycleGate.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposal == null)
            {
                owner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _disposal = owner.Task;
                _disposed = true;
                _state = FlowRuntimeHostState.Disposed;
                if (_activeRun is { } run)
                {
                    activeSettlement = run.Settled.Task;
                    cancellation = run.RequestCancellation();
                }
            }
            disposal = _disposal;
        }
        finally
        {
            _lifecycleGate.Release();
        }

        if (owner != null)
        {
            try
            {
                // A wait timeout cannot transfer ownership of a graph still used by the runner.
                if (activeSettlement != null) await activeSettlement.ConfigureAwait(false);
                try { _control.FlowClear(); }
                finally
                {
                    try { _control.Dispose(); }
                    finally { _container.Dispose(); }
                }
                await cancellation.ConfigureAwait(false);
                GC.SuppressFinalize(this);
                owner.SetResult();
            }
            catch (Exception error)
            {
                owner.SetException(error);
            }
        }
        await disposal.ConfigureAwait(false);
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private sealed class ActiveRun
    {
        private Task? _cancellation;
        private bool _settling;

        public ActiveRun(CancellationToken cancellationToken)
        {
            Source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        }

        public CancellationTokenSource Source { get; }
        public Task<FlowEngineRunResult> Runner { get; set; } = null!;
        public TaskCompletionSource Settled { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        // Both transitions run under the host gate. CancelAsync dispatches callbacks outside it.
        public Task RequestCancellation() => _cancellation ??= _settling ? Task.CompletedTask : Source.CancelAsync();

        public Task SealCancellation()
        {
            _settling = true;
            return _cancellation ?? Task.CompletedTask;
        }
    }
}
