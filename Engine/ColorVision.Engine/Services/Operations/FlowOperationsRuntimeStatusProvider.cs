using ColorVision.Engine.FlowProcessing;
using ColorVision.UI.Desktop.Operations;
using System;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Engine.Services.Operations
{
    public sealed class FlowOperationsRuntimeStatusProvider : IOperationsFlowRuntimeStatusProvider
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
    }
}
