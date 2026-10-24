using FlowEngineLib.Base;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing
{
    internal enum FlowRunTermination
    {
        EngineCompleted,
        StartRejected,
        Canceled,
        TimedOut
    }

    internal sealed record FlowRunExecutionResult(
        bool Started,
        FlowRunTermination Termination,
        FlowControlData Data);

    /// <summary>
    /// Owns the start/wait/stop lifecycle for one engine run.
    /// Template loading, preprocessing, persistence, and UI presentation remain
    /// the responsibility of the caller.
    /// </summary>
    internal sealed class FlowRunExecutor
    {
        private readonly FlowControl _flowControl;
        private int _isRunning;

        public FlowRunExecutor(FlowControl flowControl)
        {
            _flowControl = flowControl ?? throw new ArgumentNullException(nameof(flowControl));
        }

        public async Task<FlowRunExecutionResult> RunAsync(
            string startNodeName,
            string serialNumber,
            TimeSpan? executionTimeout,
            CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(startNodeName);
            ArgumentException.ThrowIfNullOrWhiteSpace(serialNumber);
            serialNumber = ColorVision.Common.Utilities.Tool.SanitizeFileName(serialNumber);
            if (executionTimeout.HasValue
                && executionTimeout.Value != Timeout.InfiniteTimeSpan
                && executionTimeout.Value <= TimeSpan.Zero)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(executionTimeout),
                    executionTimeout,
                    "Execution timeout must be positive, infinite, or null.");
            }

            if (Interlocked.CompareExchange(ref _isRunning, 1, 0) != 0)
            {
                return CreateResult(
                    started: false,
                    FlowRunTermination.StartRejected,
                    startNodeName,
                    serialNumber,
                    StatusTypeEnum.Failed,
                    EngineLocalization.Format($"未能从“{startNodeName}”启动流程：已有流程正在运行，请等待完成或停止后重试。"));
            }

            var completion = new TaskCompletionSource<FlowControlData>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            EventHandler<FlowControlData>? completedHandler = null;
            completedHandler = (_, data) =>
            {
                if (!string.Equals(data.StartNodeName, startNodeName, StringComparison.Ordinal)
                    || !string.Equals(data.SerialNumber, serialNumber, StringComparison.Ordinal))
                {
                    return;
                }

                completion.TrySetResult(data);
            };

            _flowControl.FlowCompleted += completedHandler;
            bool started = false;
            try
            {
                try
                {
                    started = await _flowControl
                        .TryStartAsync(startNodeName, serialNumber, cancellationToken)
                        .ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    return CreateResult(
                        started: false,
                        FlowRunTermination.Canceled,
                        startNodeName,
                        serialNumber,
                        StatusTypeEnum.Canceled,
                        "Flow execution was canceled before it started.");
                }

                if (!started)
                {
                    return CreateResult(
                        started: false,
                        FlowRunTermination.StartRejected,
                        startNodeName,
                        serialNumber,
                        StatusTypeEnum.Failed,
                        EngineLocalization.Format($"未能从“{startNodeName}”启动流程。请检查该起点是否已连接后续节点、流程是否仍在运行，以及设备或服务连接是否就绪。"));
                }

                try
                {
                    FlowControlData data = executionTimeout.HasValue
                        && executionTimeout.Value != Timeout.InfiniteTimeSpan
                            ? await completion.Task
                                .WaitAsync(executionTimeout.Value, cancellationToken)
                                .ConfigureAwait(false)
                            : await completion.Task
                                .WaitAsync(cancellationToken)
                                .ConfigureAwait(false);
                    return new FlowRunExecutionResult(
                        Started: true,
                        FlowRunTermination.EngineCompleted,
                        data);
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    _flowControl.Stop();
                    return CreateResult(
                        started: true,
                        FlowRunTermination.Canceled,
                        startNodeName,
                        serialNumber,
                        StatusTypeEnum.Canceled,
                        "Flow execution was canceled.");
                }
                catch (TimeoutException)
                {
                    _flowControl.Stop();
                    return CreateResult(
                        started: true,
                        FlowRunTermination.TimedOut,
                        startNodeName,
                        serialNumber,
                        StatusTypeEnum.OverTime,
                        "Flow execution timed out.");
                }
            }
            finally
            {
                _flowControl.FlowCompleted -= completedHandler;
                Interlocked.Exchange(ref _isRunning, 0);
            }
        }

        private static FlowRunExecutionResult CreateResult(
            bool started,
            FlowRunTermination termination,
            string startNodeName,
            string serialNumber,
            StatusTypeEnum status,
            string message)
        {
            return new FlowRunExecutionResult(
                started,
                termination,
                new FlowControlData
                {
                    StartNodeName = startNodeName,
                    SerialNumber = serialNumber,
                    EventName = status.ToString(),
                    Status = status,
                    Message = message,
                    Params = message
                });
        }
    }
}
