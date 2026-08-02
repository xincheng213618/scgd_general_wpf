using ColorVision.Engine.Services.RC;
using log4net;
using Quartz;
using System;
using System.Globalization;
using System.Threading.Tasks;

namespace ColorVision.Engine.FlowProcessing.Scheduling;

/// <summary>
/// Explicit scheduler entry point for saved, editor-independent flows.
/// The existing <see cref="FlowJob"/> keeps its UI batch and finalization
/// compatibility semantics.
/// </summary>
[DisallowConcurrentExecution]
public sealed class HeadlessFlowJob : IJob
{
    public const string FlowKeyDataKey = "FlowKey";
    public const string StartNodeDataKey = "StartNode";
    public const string SerialNumberDataKey = "SerialNumber";
    public const string ReadinessTimeoutMsDataKey =
        "ReadinessTimeoutMs";
    public const string ExecutionTimeoutMsDataKey =
        "ExecutionTimeoutMs";

    private static readonly ILog log =
        LogManager.GetLogger(typeof(HeadlessFlowJob));

    public async Task Execute(IJobExecutionContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        string flowKey = string.Empty;
        try
        {
            flowKey = GetRequired(
                context,
                FlowKeyDataKey);
            string startNodeName = GetRequired(
                context,
                StartNodeDataKey);
            string serialNumber = GetOptional(
                    context,
                    SerialNumberDataKey)
                ?? $"{context.JobDetail.Key.Name}-"
                    + DateTime.UtcNow.ToString(
                        "yyyyMMddTHHmmss.fffffff",
                        CultureInfo.InvariantCulture);
            TimeSpan? readinessTimeout = GetTimeout(
                context,
                ReadinessTimeoutMsDataKey);
            TimeSpan? executionTimeout = GetTimeout(
                context,
                ExecutionTimeoutMsDataKey);

            FlowHeadlessExecutionResult result =
                await FlowExecutionCoordinator.Instance
                    .RunSavedFlowHeadlessAsync(
                        flowKey,
                        startNodeName,
                        serialNumber,
                        MqttRCService.GetInstance().ServiceTokens,
                        readinessTimeout,
                        executionTimeout,
                        context.CancellationToken);
            context.Result = new FlowJobResult
            {
                Success = result.Succeeded,
                Status = result.Termination.ToString(),
                Message = result.Data.Message,
                TotalTimeMs = result.Data.TotalTime > 0
                    ? result.Data.TotalTime
                    : result.ElapsedMilliseconds
            };
            log.Info(
                $"HeadlessFlowJob 完成: {flowKey}, "
                + $"{result.Termination}, "
                + $"SN={result.Data.SerialNumber}");
        }
        catch (Exception ex)
        {
            log.Error(
                $"HeadlessFlowJob 执行失败: {flowKey}",
                ex);
            context.Result = new FlowJobResult
            {
                Success = false,
                Status = "HeadlessStartupException",
                Message = ex.Message
            };
        }
    }

    private static string GetRequired(
        IJobExecutionContext context,
        string key)
    {
        string? value = GetOptional(context, key);
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException(
                $"Quartz JobDataMap 缺少 {key}。");
        }
        return value;
    }

    private static string? GetOptional(
        IJobExecutionContext context,
        string key)
    {
        return context.MergedJobDataMap.TryGetValue(
                key,
                out object? value)
            ? Convert.ToString(
                value,
                CultureInfo.InvariantCulture)
            : null;
    }

    private static TimeSpan? GetTimeout(
        IJobExecutionContext context,
        string key)
    {
        string? raw = GetOptional(context, key);
        if (string.IsNullOrWhiteSpace(raw))
            return null;
        if (!long.TryParse(
                raw,
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long milliseconds)
            || milliseconds <= 0)
        {
            throw new InvalidOperationException(
                $"Quartz JobDataMap 的 {key} 必须是正整数毫秒。");
        }
        return TimeSpan.FromMilliseconds(milliseconds);
    }
}
