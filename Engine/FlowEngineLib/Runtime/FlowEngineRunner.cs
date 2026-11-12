using FlowEngineLib.Base;
using FlowEngineLib.Start;
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
    private static readonly TimeSpan MaximumFiniteTimeout = TimeSpan.FromMilliseconds(uint.MaxValue - 1);
    private readonly FlowEngineControl _control;
    private int _isRunning;

    public FlowEngineRunner(FlowEngineControl control)
    {
        _control = control ?? throw new ArgumentNullException(nameof(control));
    }

    /// <summary>Validates optional flow timeouts before a caller prepares or starts a graph.</summary>
    public static void ValidateTimeouts(TimeSpan? readinessTimeout, TimeSpan? executionTimeout)
    {
        ValidateTimeout(readinessTimeout, nameof(readinessTimeout), allowInfinite: false);
        ValidateTimeout(executionTimeout, nameof(executionTimeout), allowInfinite: true);
    }

    private static void ValidateTimeout(TimeSpan? timeout, string parameterName, bool allowInfinite)
    {
        if (timeout is not TimeSpan value || (allowInfinite && value == Timeout.InfiniteTimeSpan))
            return;
        if (value <= TimeSpan.Zero || value > MaximumFiniteTimeout)
            throw new ArgumentOutOfRangeException(parameterName, value,
                $"Finite flow timeout must be positive and at most {MaximumFiniteTimeout.TotalMilliseconds:0} ms.");
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
        ValidateTimeouts(readinessTimeout, executionTimeout);
        serialNumber = ColorVision.Common.Utilities.Tool.SanitizeFileName(serialNumber);
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

        CVStartCFC? run = null;
        try
        {
            string? blockReason = _control.ExecutionBlockReason;
            if (blockReason != null)
                return CreateResult(false, FlowEngineRunTermination.StartRejected, startNodeName, serialNumber,
                    StatusTypeEnum.Failed, blockReason);
            bool ready;
            try
            {
                ready = await _control
                    .EnsureStartNodeReadyAsync(
                        startNodeName,
                        readinessTimeout,
                        cancellationToken)
                    .ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
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

            if (ready)
                run = _control.TryStartRun(startNodeName, serialNumber, trackCompletion: true);
            if (run == null)
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
            try
            {
                Task<FlowStartEventArgs> completion = run.TrackedCompletion.Task;
                FlowStartEventArgs data = executionTimeout.HasValue
                    && executionTimeout.Value != Timeout.InfiniteTimeSpan
                        ? await completion
                            .WaitAsync(
                                executionTimeout.Value,
                                cancellationToken)
                            .ConfigureAwait(false)
                        : await completion
                            .WaitAsync(cancellationToken)
                            .ConfigureAwait(false);
                return Completed(data);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                return FinishInterruptedRun(run, FlowEngineRunTermination.Canceled);
            }
            catch (TimeoutException)
            {
                return FinishInterruptedRun(run, FlowEngineRunTermination.TimedOut);
            }
        }
        finally
        {
            if (run != null)
                _control.RefreshRunningState();
            Interlocked.Exchange(ref _isRunning, 0);
        }

        FlowEngineRunResult Completed(FlowStartEventArgs data) => new(true, FlowEngineRunTermination.Completed,
            new FlowEngineEventArgs(startNodeName, data.SerialNumber, data.Status, data.TotalTime, data.Message, data.ErrorNodeName, data.ErrorNodeId));

        FlowEngineRunResult FinishInterruptedRun(CVStartCFC action, FlowEngineRunTermination termination)
        {
            // Stop competes with finalization on the same run. A committed
            // result survives even while its notification is still deferred.
            if (!_control.TryStopRun(action) && action.TrackedTerminalResult is { } terminal)
                return Completed(terminal);

            bool canceled = termination == FlowEngineRunTermination.Canceled;
            return CreateResult(true, termination, startNodeName, serialNumber,
                canceled ? StatusTypeEnum.Canceled : StatusTypeEnum.OverTime,
                canceled ? "Flow execution was canceled." : "Flow execution timed out.");
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
