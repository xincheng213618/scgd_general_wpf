using FlowEngineLib;
using FlowEngineLib.Base;
using FlowEngineLib.Runtime;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing;

public enum FlowHeadlessExecutionTermination
{
    Completed,
    StartRejected,
    Canceled,
    TimedOut,
    LoadFailed,
    Faulted
}

/// <summary>
/// Immutable execution input. Mutable STN, device and policy objects are
/// detached when the request is created and again when a host consumes it.
/// </summary>
public sealed class FlowHeadlessExecutionRequest
{
    private readonly byte[] _stnSnapshot;
    private readonly MQTTServiceInfo[] _services;
    private readonly FlowErrorRoute[] _errorRoutes;
    private readonly FlowNodeRetryPolicy[] _retryPolicies;

    public FlowHeadlessExecutionRequest(
        byte[] stnSnapshot,
        string startNodeName,
        string serialNumber,
        IEnumerable<MQTTServiceInfo>? services = null,
        IEnumerable<FlowErrorRoute>? errorRoutes = null,
        IEnumerable<FlowNodeRetryPolicy>? retryPolicies = null,
        TimeSpan? readinessTimeout = null,
        TimeSpan? executionTimeout = null)
    {
        ArgumentNullException.ThrowIfNull(stnSnapshot);
        if (stnSnapshot.Length == 0)
        {
            throw new ArgumentException(
                "STN snapshot cannot be empty.",
                nameof(stnSnapshot));
        }
        ArgumentException.ThrowIfNullOrWhiteSpace(startNodeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ValidateTimeout(
            readinessTimeout,
            nameof(readinessTimeout),
            allowInfinite: false);
        ValidateTimeout(
            executionTimeout,
            nameof(executionTimeout),
            allowInfinite: true);

        _stnSnapshot = (byte[])stnSnapshot.Clone();
        _services = CloneServices(services);
        _errorRoutes = CloneErrorRoutes(errorRoutes);
        _retryPolicies = CloneRetryPolicies(retryPolicies);
        StartNodeName = startNodeName;
        SerialNumber = serialNumber;
        ReadinessTimeout = readinessTimeout;
        ExecutionTimeout = executionTimeout;
        ContentHash = Convert.ToHexString(
                SHA256.HashData(_stnSnapshot))
            .ToLowerInvariant();
    }

    public string StartNodeName { get; }

    public string SerialNumber { get; }

    public TimeSpan? ReadinessTimeout { get; }

    public TimeSpan? ExecutionTimeout { get; }

    public string ContentHash { get; }

    public ReadOnlyMemory<byte> StnSnapshot =>
        new((byte[])_stnSnapshot.Clone());

    public IReadOnlyList<MQTTServiceInfo> Services =>
        new ReadOnlyCollection<MQTTServiceInfo>(
            CloneServices(_services));

    public IReadOnlyList<FlowErrorRoute> ErrorRoutes =>
        new ReadOnlyCollection<FlowErrorRoute>(
            CloneErrorRoutes(_errorRoutes));

    public IReadOnlyList<FlowNodeRetryPolicy> RetryPolicies =>
        new ReadOnlyCollection<FlowNodeRetryPolicy>(
            CloneRetryPolicies(_retryPolicies));

    internal byte[] CreateStnSnapshot()
    {
        return (byte[])_stnSnapshot.Clone();
    }

    internal MQTTServiceInfo[] CreateServices()
    {
        return CloneServices(_services);
    }

    internal FlowErrorRoute[] CreateErrorRoutes()
    {
        return CloneErrorRoutes(_errorRoutes);
    }

    internal FlowNodeRetryPolicy[] CreateRetryPolicies()
    {
        return CloneRetryPolicies(_retryPolicies);
    }

    private static void ValidateTimeout(
        TimeSpan? timeout,
        string parameterName,
        bool allowInfinite)
    {
        if (timeout.HasValue
            && ((!allowInfinite
                    && timeout.Value == Timeout.InfiniteTimeSpan)
                || (timeout.Value != Timeout.InfiniteTimeSpan
                    && timeout.Value <= TimeSpan.Zero)))
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }

    private static MQTTServiceInfo[] CloneServices(
        IEnumerable<MQTTServiceInfo>? services)
    {
        return services?
            .Select(service =>
            {
                ArgumentNullException.ThrowIfNull(service);
                var clone = new MQTTServiceInfo
                {
                    ServiceType = service.ServiceType,
                    ServiceCode = service.ServiceCode,
                    SubscribeTopic = service.SubscribeTopic,
                    PublishTopic = service.PublishTopic,
                    Token = service.Token
                };
                foreach (MQTTDeviceInfo device in service.Devices.Values)
                {
                    clone.AddDevice(
                        device.ID,
                        device.DeviceCode);
                }
                return clone;
            })
            .ToArray() ?? Array.Empty<MQTTServiceInfo>();
    }

    private static FlowErrorRoute[] CloneErrorRoutes(
        IEnumerable<FlowErrorRoute>? routes)
    {
        return routes?
            .Select(route =>
            {
                ArgumentNullException.ThrowIfNull(route);
                return new FlowErrorRoute
                {
                    SourceNodeId = route.SourceNodeId,
                    TargetNodeId = route.TargetNodeId,
                    TargetInputIndex = route.TargetInputIndex,
                    FailureKinds = route.FailureKinds?.ToArray()
                        ?? Array.Empty<FlowFailureKind>()
                };
            })
            .ToArray() ?? Array.Empty<FlowErrorRoute>();
    }

    private static FlowNodeRetryPolicy[] CloneRetryPolicies(
        IEnumerable<FlowNodeRetryPolicy>? policies)
    {
        return policies?
            .Select(policy =>
            {
                ArgumentNullException.ThrowIfNull(policy);
                return new FlowNodeRetryPolicy
                {
                    NodeId = policy.NodeId,
                    MaxAttempts = policy.MaxAttempts,
                    InitialDelayMs = policy.InitialDelayMs,
                    Backoff = policy.Backoff,
                    MaxDelayMs = policy.MaxDelayMs,
                    RetryableKinds = policy.RetryableKinds?.ToArray()
                        ?? Array.Empty<FlowFailureKind>()
                };
            })
            .ToArray() ?? Array.Empty<FlowNodeRetryPolicy>();
    }
}

public sealed record FlowHeadlessExecutionResult(
    bool Started,
    FlowHeadlessExecutionTermination Termination,
    string ContentHash,
    FlowEngineEventArgs Data,
    long ElapsedMilliseconds)
{
    public bool Succeeded =>
        Started
        && Termination == FlowHeadlessExecutionTermination.Completed
        && Data.Status == StatusTypeEnum.Completed;

    /// <summary>
    /// Preserves every graph-engine result field used by the regular UI
    /// execution path. Batch and post-processing remain an explicit caller
    /// responsibility.
    /// </summary>
    public FlowControlData ToFlowControlData()
    {
        return new FlowControlData
        {
            StartNodeName = Data.StartNodeName,
            SerialNumber = Data.SerialNumber,
            EventName = Data.Status.ToString(),
            Status = Data.Status,
            TotalTime = Data.TotalTime,
            Message = Data.Message,
            Params = Data.Message,
            ErrorNodeName = Data.ErrorNodeName,
            ErrorNodeId = Data.ErrorNodeId,
            HandledFailures = Data.HandledFailures.ToArray()
        };
    }
}

/// <summary>
/// Adapter seam shared by raw STN, published artifact and future subflow
/// execution entry points.
/// </summary>
public interface IFlowExecutionRunner
{
    Task<FlowHeadlessExecutionResult> RunAsync(
        FlowHeadlessExecutionRequest request,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Per-run observer for node diagnostics. It is attached only to the detached
/// runtime graph owned by one headless host and is never stored globally.
/// </summary>
public sealed class FlowHeadlessExecutionObserver
{
    private readonly FlowEngineNodeRunEvent? nodeRun;
    private readonly FlowEngineNodeEndEvent? nodeEnd;
    private int pendingNodeAttempts;

    public FlowHeadlessExecutionObserver(
        FlowEngineNodeRunEvent? nodeRun = null,
        FlowEngineNodeEndEvent? nodeEnd = null)
    {
        this.nodeRun = nodeRun;
        this.nodeEnd = nodeEnd;
    }

    internal void OnNodeRun(
        object sender,
        FlowEngineNodeRunEventArgs e)
    {
        Interlocked.Increment(ref pendingNodeAttempts);
        nodeRun?.Invoke(sender, e);
    }

    internal void OnNodeEnd(
        object sender,
        FlowEngineNodeEndEventArgs e)
    {
        try
        {
            nodeEnd?.Invoke(sender, e);
        }
        finally
        {
            int remaining =
                Interlocked.Decrement(
                    ref pendingNodeAttempts);
            if (remaining < 0)
            {
                Interlocked.Exchange(
                    ref pendingNodeAttempts,
                    0);
            }
        }
    }

    internal async Task WaitForPendingNodeEndsAsync(
        TimeSpan timeout)
    {
        Stopwatch stopwatch = Stopwatch.StartNew();
        while (Volatile.Read(ref pendingNodeAttempts) > 0
            && stopwatch.Elapsed < timeout)
        {
            await Task.Delay(20).ConfigureAwait(false);
        }
    }
}

/// <summary>
/// Executes one detached STN generation without an editor, WPF dispatcher,
/// batch database, pre-processing or post-processing.
/// </summary>
public sealed class FlowHeadlessExecutionService : IFlowExecutionRunner
{
    public static FlowHeadlessExecutionService Shared { get; } = new();

    public async Task<FlowHeadlessExecutionResult> RunAsync(
        FlowHeadlessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        return await RunCoreAsync(
                request,
                observer: null,
                cancellationToken)
            .ConfigureAwait(false);
    }

    public Task<FlowHeadlessExecutionResult> RunAsync(
        FlowHeadlessExecutionRequest request,
        FlowHeadlessExecutionObserver observer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(observer);
        return RunCoreAsync(
            request,
            observer,
            cancellationToken);
    }

    private static async Task<FlowHeadlessExecutionResult> RunCoreAsync(
        FlowHeadlessExecutionRequest request,
        FlowHeadlessExecutionObserver? observer,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        Stopwatch stopwatch = Stopwatch.StartNew();
        bool loaded = false;
        try
        {
            await using var host = new FlowRuntimeHost();
            CVBaseServerNode[] observedNodes = [];
            MQTTServiceInfo[] services =
                request.CreateServices();
            try
            {
                await host.LoadAsync(
                        request.CreateStnSnapshot(),
                        services,
                        cancellationToken)
                    .ConfigureAwait(false);
                loaded = true;
                if (services.Length == 0
                    && host.Nodes
                        .OfType<CVBaseServerNode>()
                        .Any())
                {
                    stopwatch.Stop();
                    return CreateFailure(
                        request,
                        FlowHeadlessExecutionTermination
                            .StartRejected,
                        StatusTypeEnum.Failed,
                        "The flow contains service nodes but no "
                        + "MQTT service snapshot was supplied.",
                        stopwatch.ElapsedMilliseconds);
                }
                if (observer != null)
                {
                    observedNodes = host.Nodes
                        .OfType<CVBaseServerNode>()
                        .ToArray();
                    foreach (CVBaseServerNode node in observedNodes)
                    {
                        node.nodeRunEvent += observer.OnNodeRun;
                        node.nodeEndEvent += observer.OnNodeEnd;
                    }
                }

                await host.ConfigureFailureRoutesAsync(
                        request.CreateErrorRoutes(),
                        cancellationToken)
                    .ConfigureAwait(false);
                await host.ConfigureRetryPoliciesAsync(
                        request.CreateRetryPolicies(),
                        cancellationToken)
                    .ConfigureAwait(false);

                FlowEngineRunResult run = await host.RunAsync(
                        request.StartNodeName,
                        request.SerialNumber,
                        request.ReadinessTimeout,
                        request.ExecutionTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                if (observer != null)
                {
                    await observer.WaitForPendingNodeEndsAsync(
                            TimeSpan.FromSeconds(1))
                        .ConfigureAwait(false);
                }
                stopwatch.Stop();
                return new FlowHeadlessExecutionResult(
                    run.Started,
                    MapTermination(run.Termination),
                    host.ContentHash ?? request.ContentHash,
                    CloneData(run.Data),
                    stopwatch.ElapsedMilliseconds);
            }
            finally
            {
                foreach (CVBaseServerNode node in observedNodes)
                {
                    node.nodeRunEvent -= observer!.OnNodeRun;
                    node.nodeEndEvent -= observer.OnNodeEnd;
                }
            }
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return CreateFailure(
                request,
                FlowHeadlessExecutionTermination.Canceled,
                StatusTypeEnum.Canceled,
                "Flow execution was canceled.",
                stopwatch.ElapsedMilliseconds);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            return CreateFailure(
                request,
                loaded
                    ? FlowHeadlessExecutionTermination.Faulted
                    : FlowHeadlessExecutionTermination.LoadFailed,
                StatusTypeEnum.Failed,
                ex.Message,
                stopwatch.ElapsedMilliseconds);
        }
    }

    private static FlowHeadlessExecutionTermination MapTermination(
        FlowEngineRunTermination termination)
    {
        return termination switch
        {
            FlowEngineRunTermination.Completed =>
                FlowHeadlessExecutionTermination.Completed,
            FlowEngineRunTermination.StartRejected =>
                FlowHeadlessExecutionTermination.StartRejected,
            FlowEngineRunTermination.Canceled =>
                FlowHeadlessExecutionTermination.Canceled,
            FlowEngineRunTermination.TimedOut =>
                FlowHeadlessExecutionTermination.TimedOut,
            _ => FlowHeadlessExecutionTermination.Faulted
        };
    }

    private static FlowHeadlessExecutionResult CreateFailure(
        FlowHeadlessExecutionRequest request,
        FlowHeadlessExecutionTermination termination,
        StatusTypeEnum status,
        string message,
        long elapsedMilliseconds)
    {
        return new FlowHeadlessExecutionResult(
            Started: false,
            termination,
            request.ContentHash,
            new FlowEngineEventArgs(
                request.StartNodeName,
                request.SerialNumber,
                status,
                elapsedMilliseconds,
                message),
            elapsedMilliseconds);
    }

    private static FlowEngineEventArgs CloneData(
        FlowEngineEventArgs data)
    {
        return new FlowEngineEventArgs(
            data.StartNodeName,
            data.SerialNumber,
            data.Status,
            data.TotalTime,
            data.Message,
            data.ErrorNodeName,
            data.ErrorNodeId,
            data.HandledFailures.ToArray());
    }
}
