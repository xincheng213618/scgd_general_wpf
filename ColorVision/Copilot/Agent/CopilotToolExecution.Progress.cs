using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotToolExecutor
    {
        private async Task PublishToolQueueProgressAsync(
            CopilotToolInvocation invocation,
            DateTimeOffset startedAt,
            TimeSpan timeout,
            Stopwatch queueStopwatch,
            Stopwatch totalStopwatch,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            try
            {
                using var timer = new PeriodicTimer(_progressInterval);
                while (await timer.WaitForNextTickAsync(cancellationToken))
                {
                    var queueDurationMs = Math.Max(0, queueStopwatch.ElapsedMilliseconds);
                    var execution = CreateExecutionInfo(
                        invocation,
                        CopilotToolExecutionState.Pending,
                        startedAt,
                        completedAt: null,
                        Math.Max(0, totalStopwatch.ElapsedMilliseconds),
                        timeout,
                        queueDurationMs: queueDurationMs);
                    onEvent(CopilotAgentEvent.ToolProgress(
                        execution,
                        $"{invocation.Tool.Name} is waiting for an execution slot · {FormatElapsed(queueDurationMs)} queued."));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Warn($"Copilot tool queue progress reporting stopped unexpectedly. Tool={invocation.Tool.Name} CallId={invocation.CallId}", ex);
            }
        }

        private async Task PublishToolProgressAsync(
            CopilotToolInvocation invocation,
            DateTimeOffset startedAt,
            TimeSpan timeout,
            long queueDurationMs,
            Stopwatch stopwatch,
            CopilotToolProgressContext progressContext,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            try
            {
                var hasPublishedStructuredProgress = false;
                var lastStructuredProgressAt = TimeSpan.Zero;
                var lastPublishedProgressVersion = 0L;
                while (true)
                {
                    var waitResult = await progressContext.WaitForUpdateAsync(
                        _progressInterval,
                        cancellationToken);
                    if (waitResult == CopilotToolProgressWaitResult.Completed)
                        return;

                    CopilotToolProgressUpdate? reportedProgress;
                    if (waitResult == CopilotToolProgressWaitResult.Updated)
                    {
                        if (hasPublishedStructuredProgress)
                        {
                            var remainingDelay = MinimumStructuredProgressInterval
                                - (stopwatch.Elapsed - lastStructuredProgressAt);
                            if (remainingDelay > TimeSpan.Zero)
                                await Task.Delay(remainingDelay, cancellationToken);
                        }

                        progressContext.DrainUpdateNotifications();
                        var progressSnapshot = progressContext.GetLatestSnapshot();
                        reportedProgress = progressSnapshot.Update;
                        if (reportedProgress == null
                            || progressSnapshot.Version <= lastPublishedProgressVersion)
                            continue;
                        lastStructuredProgressAt = stopwatch.Elapsed;
                        hasPublishedStructuredProgress = true;
                        lastPublishedProgressVersion = progressSnapshot.Version;
                    }
                    else
                    {
                        var progressSnapshot = progressContext.GetLatestSnapshot();
                        reportedProgress = progressSnapshot.Update;
                        lastPublishedProgressVersion = Math.Max(
                            lastPublishedProgressVersion,
                            progressSnapshot.Version);
                    }

                    if (!stopwatch.IsRunning)
                        return;

                    var elapsedMs = Math.Max(0, stopwatch.ElapsedMilliseconds);
                    var execution = CreateExecutionInfo(
                        invocation,
                        CopilotToolExecutionState.Running,
                        startedAt,
                        completedAt: null,
                        elapsedMs,
                        timeout,
                        queueDurationMs: queueDurationMs);
                    var progressText = FormatReportedProgress(reportedProgress);
                    onEvent(CopilotAgentEvent.ToolProgress(
                        execution,
                        string.IsNullOrWhiteSpace(progressText)
                            ? $"{invocation.Tool.Name} is still running · {FormatElapsed(elapsedMs)} elapsed."
                            : $"{invocation.Tool.Name} · {progressText} · {FormatElapsed(elapsedMs)} elapsed.",
                        reportedProgress));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
            {
                Log.Warn($"Copilot tool progress reporting stopped unexpectedly. Tool={invocation.Tool.Name} CallId={invocation.CallId}", ex);
            }
        }

        private static string FormatReportedProgress(CopilotToolProgressUpdate? progress)
        {
            if (progress == null)
                return string.Empty;

            var count = progress.Completed.HasValue && progress.Total.HasValue
                ? $"{progress.Completed.Value}/{progress.Total.Value}"
                : progress.Completed?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(count) && !string.IsNullOrWhiteSpace(progress.Unit))
                count += " " + progress.Unit;
            if (string.IsNullOrWhiteSpace(progress.Message))
                return count;
            return string.IsNullOrWhiteSpace(count)
                ? progress.Message
                : $"{count} · {progress.Message}";
        }
    }
}
