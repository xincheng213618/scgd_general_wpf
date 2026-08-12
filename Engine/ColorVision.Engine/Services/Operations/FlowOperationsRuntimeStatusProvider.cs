using ColorVision.Engine.FlowProcessing;
using ColorVision.UI.Desktop.Operations;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class FlowOperationsRuntimeStatusProvider : IOperationsFlowRuntimeStatusProvider, IOperationsFlowRuntimeController
    {
        private const int DispatcherTimeoutMilliseconds = 1000;

        public OperationsFlowRuntimeStatus Capture()
        {
            FlowRuntimeActivitySnapshot aggregate = FlowRuntimeActivityRegistry.Capture();
            FlowEngineManager? manager = FlowEngineManager.Current;
            if (manager == null)
                return CaptureAggregateOnly(aggregate);

            Dispatcher? dispatcher = manager.View?.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return CaptureAggregateOnly(aggregate);
            if (dispatcher.CheckAccess())
                return CaptureOnDispatcher(manager, aggregate);

            DispatcherOperation<OperationsFlowRuntimeStatus> operation =
                dispatcher.InvokeAsync(() => CaptureOnDispatcher(manager, aggregate), DispatcherPriority.Send);
            if (!operation.Task.Wait(DispatcherTimeoutMilliseconds))
            {
                operation.Abort();
                return CaptureAggregateOnly(aggregate);
            }
            return operation.Task.GetAwaiter().GetResult();
        }

        public OperationsFlowCancelResult RequestCancelCurrentFlow()
        {
            FlowEngineManager? manager = FlowEngineManager.Current;
            if (manager == null)
                return new(false, "flow_not_configured", "The primary flow workspace is not available.");

            Dispatcher? dispatcher = manager.View?.Dispatcher ?? Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
                return new(false, "flow_control_unavailable", "The primary flow dispatcher is unavailable.");
            if (dispatcher.CheckAccess())
                return RequestCancelOnDispatcher(manager);

            DispatcherOperation<OperationsFlowCancelResult> operation =
                dispatcher.InvokeAsync(() => RequestCancelOnDispatcher(manager), DispatcherPriority.Send);
            if (!operation.Task.Wait(DispatcherTimeoutMilliseconds))
            {
                operation.Abort();
                return new(false, "flow_control_timeout", "The primary flow workspace did not respond in time.");
            }
            return operation.Task.GetAwaiter().GetResult();
        }

        private static OperationsFlowRuntimeStatus CaptureOnDispatcher(
            FlowEngineManager manager,
            FlowRuntimeActivitySnapshot aggregate)
        {
            MeasureBatchModel? batch = manager.Batch;
            bool mainLifecycleActive = manager.View.IsExecutionActive;
            bool mainBatchActive = manager.View.IsCurrentBatchActive;
            bool hasConfiguredFlow = manager.SelectedFlowParam != null
                || manager.TemplateFlowParamsIndex >= 0
                    && manager.TemplateFlowParamsIndex < manager.FlowParams.Count;
            DateTimeOffset? mainBatchCreatedAt = batch?.CreateDate is DateTime createdAt
                ? new DateTimeOffset(createdAt)
                : null;
            return OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                HasConfiguredFlow = hasConfiguredFlow || aggregate.EngineRunning,
                LifecycleActive = mainLifecycleActive || aggregate.EngineRunning,
                EngineRunning = aggregate.EngineRunning || manager.FlowControl.IsFlowRun,
                BatchIsCurrentLifecycle = mainBatchActive,
                ProgressAvailable = mainLifecycleActive,
                CancelAvailable = mainLifecycleActive,
                BatchStatus = batch?.FlowStatus.ToString() ?? string.Empty,
                ProgressPercent = manager.BatchProgress,
                BatchCreatedAt = mainBatchActive ? mainBatchCreatedAt : aggregate.EngineStartedAt,
                BatchDurationMilliseconds = Math.Max(0, batch?.TotalTime ?? 0),
                LastRunStatus = aggregate.LastRunStatus,
                LastRunDurationMilliseconds = aggregate.LastRunDurationMilliseconds,
            });
        }

        private static OperationsFlowRuntimeStatus CaptureAggregateOnly(
            FlowRuntimeActivitySnapshot aggregate) =>
            OperationsFlowRuntimeStatusFactory.Create(new OperationsFlowRuntimeSourceSnapshot
            {
                Available = true,
                HasConfiguredFlow = aggregate.EngineRunning,
                LifecycleActive = aggregate.EngineRunning,
                EngineRunning = aggregate.EngineRunning,
                ProgressAvailable = false,
                BatchCreatedAt = aggregate.EngineStartedAt,
                LastRunStatus = aggregate.LastRunStatus,
                LastRunDurationMilliseconds = aggregate.LastRunDurationMilliseconds,
            });

        private static OperationsFlowCancelResult RequestCancelOnDispatcher(FlowEngineManager manager)
        {
            if (!manager.View.IsExecutionActive)
                return new(false, "flow_not_active", "There is no active primary flow to cancel.");

            if (!manager.View.StopFlowCommand.CanExecute(null))
                return new(false, "flow_cancel_unavailable", "The primary flow cannot be canceled in its current state.");

            manager.View.StopFlowCommand.Execute(null);
            return new(true, "flow_cancel_requested", "Cancellation was requested through the primary flow execution session.");
        }
    }
}
