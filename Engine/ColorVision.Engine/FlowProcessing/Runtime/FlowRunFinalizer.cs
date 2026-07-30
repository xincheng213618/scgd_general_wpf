using ColorVision.Database;
using ColorVision.Engine.FlowProcessing.Diagnostics;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.UI;
using log4net;
using SqlSugar;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing
{
    internal sealed record FlowRunFinalizationRequest(
        FlowControlData EngineResult,
        MeasureBatchModel? Batch,
        string FlowName,
        long ElapsedMilliseconds);

    internal interface IFlowPostProcessExecutor
    {
        Task<IReadOnlyList<PostProcessExecutionResult>> ExecuteAsync(
            MeasureBatchModel batch,
            string flowName);
    }

    internal interface IFlowRunFinalizationPersistence
    {
        void ApplyRequiredPostProcessFailure(
            MeasureBatchModel batch,
            IReadOnlyList<PostProcessExecutionResult> postProcessResults);

        void RecordFallbackRun(
            MeasureBatchModel batch,
            string flowName,
            FlowControlData engineResult,
            FlowStatus status,
            long elapsedMilliseconds);
    }

    /// <summary>
    /// Owns the business-finalization phase after the graph engine stops.
    /// It deliberately does not access ViewFlow, STNodeEditor, selection,
    /// progress controls, or the active graph.
    /// </summary>
    internal sealed class FlowRunFinalizer
    {
        private static readonly ILog log =
            LogManager.GetLogger(typeof(FlowRunFinalizer));
        private readonly IFlowPostProcessExecutor postProcessExecutor;
        private readonly IFlowRunFinalizationPersistence persistence;

        public FlowRunFinalizer()
            : this(
                new DefaultFlowPostProcessExecutor(),
                new DefaultFlowRunFinalizationPersistence())
        {
        }

        internal FlowRunFinalizer(
            IFlowPostProcessExecutor postProcessExecutor,
            IFlowRunFinalizationPersistence persistence)
        {
            this.postProcessExecutor = postProcessExecutor
                ?? throw new ArgumentNullException(
                    nameof(postProcessExecutor));
            this.persistence = persistence
                ?? throw new ArgumentNullException(nameof(persistence));
        }

        public async Task<FlowRunFinalizedData> FinalizeAsync(
            FlowRunFinalizationRequest request,
            FlowExecutionJournalScope? journalScope)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(request.EngineResult);
            ArgumentOutOfRangeException.ThrowIfNegative(
                request.ElapsedMilliseconds);

            IReadOnlyList<PostProcessExecutionResult> postProcessResults =
                Array.Empty<PostProcessExecutionResult>();
            if (request.Batch != null)
            {
                journalScope?.TryAppendEvent(
                    "post-process-started",
                    "PostProcessStarted",
                    message: "流程后处理已开始。");
                try
                {
                    postProcessResults =
                        await postProcessExecutor.ExecuteAsync(
                            request.Batch,
                            request.FlowName);
                }
                catch (Exception ex)
                {
                    log.Error("调度流程后处理失败。", ex);
                    DateTime failedTimeUtc = DateTime.UtcNow;
                    postProcessResults = new[]
                    {
                        new PostProcessExecutionResult(
                            "后处理调度",
                            string.Empty,
                            PostProcessFailurePolicy.Warning,
                            PostProcessExecutionStatus.ThrewException,
                            ex.Message,
                            failedTimeUtc,
                            failedTimeUtc,
                            ex)
                    };
                }
            }

            journalScope?.TryAppendEvent(
                "post-process-completed",
                "PostProcessCompleted",
                code: postProcessResults.All(result => result.Succeeded)
                    ? "Succeeded"
                    : "CompletedWithFailures",
                message:
                    $"后处理共 {postProcessResults.Count} 项，失败 "
                    + $"{postProcessResults.Count(result => !result.Succeeded)} 项。");
            RecordPostProcessIncidents(
                postProcessResults,
                journalScope);

            FlowFinalOutcome finalOutcome =
                FlowFinalOutcomeResolver.Resolve(
                    request.EngineResult,
                    postProcessResults);
            if (request.Batch != null
                && request.EngineResult.FlowStatus == FlowStatus.Completed
                && HasRequiredPostProcessFailure(postProcessResults))
            {
                persistence.ApplyRequiredPostProcessFailure(
                    request.Batch,
                    postProcessResults);
            }

            FlowStatus recordedStatus =
                ResolveRecordedStatus(finalOutcome);
            journalScope?.TryAppendEvent(
                "run-finalized",
                "RunFinalized",
                code: finalOutcome.ToString(),
                message: $"流程最终结果为 {finalOutcome}。");
            if (journalScope != null)
            {
                journalScope.TryCompleteRun(
                    recordedStatus,
                    request.ElapsedMilliseconds,
                    finalOutcome);
            }
            else if (request.Batch != null)
            {
                persistence.RecordFallbackRun(
                    request.Batch,
                    request.FlowName,
                    request.EngineResult,
                    recordedStatus,
                    request.ElapsedMilliseconds);
            }

            return new FlowRunFinalizedData(
                request.EngineResult,
                finalOutcome,
                postProcessResults,
                DateTime.UtcNow);
        }

        internal static FlowStatus ResolveRecordedStatus(
            FlowFinalOutcome finalOutcome)
        {
            return finalOutcome switch
            {
                FlowFinalOutcome.Succeeded => FlowStatus.Completed,
                FlowFinalOutcome.SucceededWithWarnings =>
                    FlowStatus.Completed,
                FlowFinalOutcome.Canceled => FlowStatus.Canceled,
                FlowFinalOutcome.TimedOut => FlowStatus.OverTime,
                _ => FlowStatus.Failed,
            };
        }

        internal static bool HasRequiredPostProcessFailure(
            IEnumerable<PostProcessExecutionResult> results)
        {
            ArgumentNullException.ThrowIfNull(results);
            return results.Any(result =>
                !result.Succeeded
                && result.FailurePolicy
                    == PostProcessFailurePolicy.Required);
        }

        private static void RecordPostProcessIncidents(
            IReadOnlyList<PostProcessExecutionResult> results,
            FlowExecutionJournalScope? journalScope)
        {
            PostProcessExecutionResult[] failures = results
                .Where(result => !result.Succeeded)
                .ToArray();
            for (int index = 0; index < failures.Length; index++)
            {
                PostProcessExecutionResult failure = failures[index];
                journalScope?.TryCreateIncident(
                    $"post-process:{index + 1}",
                    "PostProcessFailed",
                    failure.FailurePolicy
                        == PostProcessFailurePolicy.Required
                            ? "Error"
                            : "Warning",
                    $"{failure.Name}: {failure.Message}",
                    detailsJson: failure.Exception?.ToString());
            }
        }
    }

    internal sealed class DefaultFlowPostProcessExecutor :
        IFlowPostProcessExecutor
    {
        private static readonly ILog log =
            LogManager.GetLogger(typeof(DefaultFlowPostProcessExecutor));

        public async Task<IReadOnlyList<PostProcessExecutionResult>>
            ExecuteAsync(
                MeasureBatchModel batch,
                string flowName)
        {
            ArgumentNullException.ThrowIfNull(batch);
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
                return ExecuteCore(batch, flowName);

            return await dispatcher.InvokeAsync(
                () => ExecuteCore(batch, flowName));
        }

        private static IReadOnlyList<PostProcessExecutionResult>
            ExecuteCore(
                MeasureBatchModel batch,
                string flowName)
        {
            List<PostProcessMeta> matchingMetas =
                PostProcessManager.GetInstance().ProcessMetas
                    .Where(meta => string.Equals(
                        meta.TemplateName,
                        flowName,
                        StringComparison.OrdinalIgnoreCase))
                    .ToList();
            if (matchingMetas.Count == 0)
                return Array.Empty<PostProcessExecutionResult>();

            log.Info(
                $"匹配到 {matchingMetas.Count} 个自定义流程处理 "
                + flowName);
            var context = new PostProcessContext
            {
                Batch = batch,
                FlowName = flowName,
            };
            IReadOnlyList<PostProcessExecutionResult> results =
                PostProcessExecutionRunner.Execute(
                    matchingMetas,
                    context);
            foreach (PostProcessExecutionResult result in results)
            {
                if (result.Succeeded)
                {
                    log.Info(
                        $"自定义流程 {result.Name} -> "
                        + $"{result.ProcessTypeName} 执行成功");
                }
                else if (result.Exception != null)
                {
                    log.Error(
                        $"自定义流程 {result.Name} -> "
                        + $"{result.ProcessTypeName} 执行异常，策略 "
                        + result.FailurePolicy,
                        result.Exception);
                }
                else
                {
                    log.Warn(
                        $"自定义流程 {result.Name} -> "
                        + $"{result.ProcessTypeName} 执行失败，策略 "
                        + $"{result.FailurePolicy}：{result.Message}");
                }
            }
            return results;
        }
    }

    internal sealed class DefaultFlowRunFinalizationPersistence :
        IFlowRunFinalizationPersistence
    {
        private static readonly ILog log = LogManager.GetLogger(
            typeof(DefaultFlowRunFinalizationPersistence));

        public void ApplyRequiredPostProcessFailure(
            MeasureBatchModel batch,
            IReadOnlyList<PostProcessExecutionResult> postProcessResults)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(postProcessResults);
            string summary =
                CreateRequiredPostProcessFailureSummary(
                    postProcessResults);
            batch.FlowStatus = FlowStatus.Failed;
            batch.Result = string.IsNullOrWhiteSpace(batch.Result)
                ? summary
                : $"{batch.Result}{Environment.NewLine}{summary}";

            try
            {
                using var db = new SqlSugarClient(
                    new ConnectionConfig
                    {
                        ConnectionString =
                            MySqlControl.GetConnectionString(),
                        DbType = SqlSugar.DbType.MySql,
                        IsAutoCloseConnection = true,
                    });
                db.Updateable(batch).ExecuteCommand();
            }
            catch (Exception ex)
            {
                log.Error(
                    "更新必需后处理失败的批次状态失败。",
                    ex);
            }
        }

        public void RecordFallbackRun(
            MeasureBatchModel batch,
            string flowName,
            FlowControlData engineResult,
            FlowStatus status,
            long elapsedMilliseconds)
        {
            ArgumentNullException.ThrowIfNull(batch);
            ArgumentNullException.ThrowIfNull(engineResult);
            FlowNodeRecordDataBaseHelper.RecordFlowRun(
                batch.TId ?? 0,
                flowName,
                engineResult.SerialNumber,
                status,
                elapsedMilliseconds);
        }

        internal static string
            CreateRequiredPostProcessFailureSummary(
                IEnumerable<PostProcessExecutionResult> results)
        {
            ArgumentNullException.ThrowIfNull(results);
            string details = string.Join(
                "; ",
                results
                    .Where(result =>
                        !result.Succeeded
                        && result.FailurePolicy
                            == PostProcessFailurePolicy.Required)
                    .Select(result =>
                        string.IsNullOrWhiteSpace(result.Message)
                            ? result.Name
                            : $"{result.Name}（{result.Message}）"));
            return $"必需后处理失败: {details}";
        }
    }
}
