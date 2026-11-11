#pragma warning disable CS8602,CS8603,CS8601
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Properties;
using ColorVision.Engine.Utilities;
using ColorVision.Scheduler;
using log4net;
using Quartz;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;


namespace ColorVision.Engine.FlowProcessing.Scheduling
{
    /// <summary>
    /// FlowJob 执行结果，存入 IJobExecutionContext.Result 供 TaskExecutionListener 读取
    /// </summary>
    public class FlowJobResult
    {
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
        public long TotalTimeMs { get; set; }
        public string FlowName { get; set; } = string.Empty;
        public string SerialNumber { get; set; } = string.Empty;

        public override string ToString()
        {
            string statusText = Status switch
            {
                nameof(FlowFinalOutcome.Succeeded) => EngineLocalization.Get("成功"),
                nameof(FlowFinalOutcome.SucceededWithWarnings) => EngineLocalization.Get("完成（有警告）"),
                nameof(FlowFinalOutcome.Failed) => Resources.ExecutionFailed,
                nameof(FlowFinalOutcome.Canceled) => EngineLocalization.Get("已取消"),
                nameof(FlowFinalOutcome.TimedOut) => Resources.Timeout,
                _ => Status
            };
            string text = $"{statusText} | {Resources.Flow_Elapsed}{TotalTimeMs}ms";
            if (!string.IsNullOrWhiteSpace(SerialNumber))
                text = $"{text} | {Resources.BatchNumber} {SerialNumber}";
            if (!string.IsNullOrWhiteSpace(FlowName))
                text = $"{Resources.FlowTemplate} {FlowName} | {text}";
            if (!string.IsNullOrEmpty(Message))
                text += $" | {Message}";
            return text;
        }
    }

    [LocalizedDisplayName(typeof(Resources), "执行流程")]
    public class FlowJob : IJob, IConfigurableJob
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(FlowJob));

        public Type ConfigType => typeof(FlowJobConfig);

        public IJobConfig CreateDefaultConfig() => new FlowJobConfig();

        public async ValueTask Execute(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            log.Info($"FlowJob 开始执行: {context.JobDetail.Key.Name}");

            FlowRunFinalizedData? result = null;

            try
            {
                context.MergedJobDataMap.TryGetValue("SchedulerInfo", out object? value);
                result = await RunScheduledFlowAsync(value as SchedulerInfo, cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                context.Result = new FlowJobResult
                {
                    Success = false,
                    Status = nameof(FlowFinalOutcome.Canceled),
                    Message = Resources.ExecutionCancelled
                };
                return;
            }
            catch (Exception ex)
            {
                log.Error("FlowJob 启动流程异常", ex);
                context.Result = new FlowJobResult
                {
                    Success = false,
                    Status = Resources.Flow_StartupException,
                    Message = ex.Message
                };
                return;
            }

            if (result == null)
            {
                log.Warn("FlowJob 流程未能启动（验证失败或正在运行中）");
                context.Result = new FlowJobResult
                {
                    Success = false,
                    Status = Resources.Flow_NotStarted,
                    Message = Resources.Flow_FlowNotStartedMessage
                };
                return;
            }

            FlowJobResult jobResult = CreateJobResult(result);
            context.Result = jobResult;

            log.Info(
                $"FlowJob 流程最终完成: {result.FinalOutcome}, 耗时: {result.EngineResult.TotalTime}ms, 成功: {jobResult.Success}");
        }

        private static Task<FlowRunFinalizedData?> RunScheduledFlowAsync(SchedulerInfo? job, CancellationToken cancellationToken)
        {
            Dispatcher dispatcher = Application.Current?.Dispatcher
                ?? throw new InvalidOperationException("The WPF application is not available.");
            TimeSpan? executionTimeout = job is { TimeoutSeconds: > 0 } ? TimeSpan.FromSeconds(job.TimeoutSeconds) : null;

            return dispatcher.InvokeAsync(() =>
            {
                if (job?.Config is FlowJobConfig config)
                {
                    var template = FlowJobConfig.FindSavedTemplate(config.FlowKey)
                        ?? throw new InvalidOperationException(EngineLocalization.Get("任务绑定的流程模板不可用，请编辑任务并重新选择。"));
                    return FlowEngineManager.GetInstance().View.RunFlowAndWaitForFinalizationAsync(template,
                        reportStartupFailures: true, executionTimeout: executionTimeout, cancellationToken: cancellationToken);
                }
                return FlowEngineManager.GetInstance().View.RunFlowAndWaitForFinalizationAsync(
                    executionTimeout: executionTimeout, cancellationToken: cancellationToken);
            }, DispatcherPriority.Normal, cancellationToken).Task.Unwrap();
        }

        internal static FlowJobResult CreateJobResult(FlowRunFinalizedData result)
        {
            ArgumentNullException.ThrowIfNull(result);

            bool success = result.FinalOutcome is
                FlowFinalOutcome.Succeeded or
                FlowFinalOutcome.SucceededWithWarnings;
            string message = string.IsNullOrWhiteSpace(result.EngineResult.Params)
                ? result.EngineResult.Message ?? string.Empty
                : result.EngineResult.Params;
            string postProcessFailures = string.Join(
                "; ",
                result.PostProcessResults
                    .Where(postProcessResult => !postProcessResult.Succeeded)
                    .Select(postProcessResult =>
                        string.IsNullOrWhiteSpace(postProcessResult.Message)
                            ? postProcessResult.Name
                            : $"{postProcessResult.Name}: {postProcessResult.Message}"));
            if (!string.IsNullOrWhiteSpace(postProcessFailures))
            {
                string postProcessMessage = $"后处理: {postProcessFailures}";
                message = string.IsNullOrWhiteSpace(message)
                    ? postProcessMessage
                    : $"{message} | {postProcessMessage}";
            }

            return new FlowJobResult
            {
                Success = success,
                Status = result.FinalOutcome.ToString(),
                Message = message,
                TotalTimeMs = result.EngineResult.TotalTime,
                FlowName = result.FlowName,
                SerialNumber = result.EngineResult.SerialNumber ?? string.Empty
            };
        }
    }
}
