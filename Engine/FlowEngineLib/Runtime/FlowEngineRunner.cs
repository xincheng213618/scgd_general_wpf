using FlowEngineLib.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FlowEngineLib.Runtime;

public enum FlowEngineRunTermination
{
    Completed,
    StartRejected,
    Canceled,
    TimedOut
}

public sealed record FlowEngineRunResult(
    bool Started,
    FlowEngineRunTermination Termination,
    FlowEngineEventArgs Data);

/// <summary>
/// UI-independent start/wait/stop owner for one execution. It consumes only
/// FlowEngineControl and never accesses Application, Dispatcher, or a window.
/// </summary>
public sealed class FlowEngineRunner
{
    private readonly FlowEngineControl _control;
    private int _isRunning;

    public FlowEngineRunner(FlowEngineControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    public async Task<FlowEngineRunResult> RunAsync(
        string startNodeName,
        string serialNumber,
        TimeSpan readinessTimeout,
        TimeSpan? executionTimeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(startNodeName);
        ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            readinessTimeout,
            TimeSpan.Zero);
        if (executionTimeout.HasValue
            && executionTimeout.Value != Timeout.InfiniteTimeSpan)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                executionTimeout.Value,
                TimeSpan.Zero,
                nameof(executionTimeout));
        }
        if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
        {
            return CreateResult(
                started: false,
                FlowEngineRunTermination.StartRejected,
                startNodeName,
                serialNumber,
                StatusTypeEnum.Failed,
                "A flow execution is already active.");
        }

        var completion = new TaskCompletionSource<FlowEngineEventArgs>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        FlowEngineEventHandler? handler = null;
        handler = (_, data) =>
        {
            if (string.Equals(
                    data.StartNodeName,
                    startNodeName,
                    StringComparison.Ordinal)
                && string.Equals(
                    data.SerialNumber,
                    serialNumber,
                    StringComparison.Ordinal))
            {
                completion.TrySetResult(data);
            }
        };
        _control.Finished += handler;

        bool started = false;
        try
        {
            bool ready;
            try
            {
                ready = await _control
                    .EnsureStartNodeReadyAsync(
                        startNodeName,
                        readinessTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return CreateResult(
                    started: false,
                    FlowEngineRunTermination.Canceled,
                    startNodeName,
                    serialNumber,
                    StatusTypeEnum.Canceled,
                    "Flow execution was canceled before it started.");
            }

            if (!ready || !_control.TryStartNode(startNodeName, serialNumber))
            {
                return CreateResult(
                    started: false,
                    FlowEngineRunTermination.StartRejected,
                    startNodeName,
                    serialNumber,
                    StatusTypeEnum.Failed,
                    ready
                        ? "Flow start was rejected."
                        : "The selected start node did not become ready.");
            }
            started = true;

            try
            {
                FlowEngineEventArgs data = executionTimeout.HasValue
                    && executionTimeout.Value != Timeout.InfiniteTimeSpan
                        ? await completion.Task
                            .WaitAsync(
                                executionTimeout.Value,
                                cancellationToken)
                            .ConfigureAwait(false)
                        : await completion.Task
                            .WaitAsync(cancellationToken)
                            .ConfigureAwait(false);
                return new FlowEngineRunResult(
                    Started: true,
                    FlowEngineRunTermination.Completed,
                    data);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                _control.StopNode(startNodeName, serialNumber);
                return CreateResult(
                    started: true,
                    FlowEngineRunTermination.Canceled,
                    startNodeName,
                    serialNumber,
                    StatusTypeEnum.Canceled,
                    "Flow execution was canceled.");
            }
            catch (TimeoutException)
            {
                _control.StopNode(startNodeName, serialNumber);
                return CreateResult(
                    started: true,
                    FlowEngineRunTermination.TimedOut,
                    startNodeName,
                    serialNumber,
                    StatusTypeEnum.OverTime,
                    "Flow execution timed out.");
            }
        }
        finally
        {
            if (started && cancellationToken.IsCancellationRequested)
                _control.StopNode(startNodeName, serialNumber);
            _control.Finished -= handler;
            Interlocked.Exchange(ref _isRunning, 0);
        }
    }

    private static FlowEngineRunResult CreateResult(
        bool started,
        FlowEngineRunTermination termination,
        string startNodeName,
        string serialNumber,
        StatusTypeEnum status,
        string message)
    {
        return new FlowEngineRunResult(
            started,
            termination,
            new FlowEngineEventArgs(
                startNodeName,
                serialNumber,
                status,
                0,
                message));
    }
}
