using System;
using System.Collections.Generic;
using System.Linq;
using System.Security;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotCodexStopHookOutcome(
        bool ShouldStop,
        string StopReason,
        bool ShouldContinue,
        string ContinuationPrompt)
    {
        public static CopilotCodexStopHookOutcome Complete { get; } =
            new(false, string.Empty, false, string.Empty);
    }

    internal sealed class CopilotCodexStopHookExecutor
    {
        internal const int MaximumConsecutiveContinuations = 8;

        private readonly ICopilotCodexCommandHookRunner? _runner;
        private readonly ICopilotCodexLifecycleHookBackgroundScheduler _backgroundScheduler;

        public CopilotCodexStopHookExecutor(
            ICopilotCodexCommandHookRunner? runner = null,
            ICopilotCodexLifecycleHookBackgroundScheduler? backgroundScheduler = null)
        {
            _runner = runner;
            _backgroundScheduler = backgroundScheduler
                ?? CopilotCodexLifecycleHookBackgroundScheduler.Shared;
        }

        public async Task<CopilotCodexStopHookOutcome> RunAsync(
            CopilotAgentRequest request,
            bool stopHookActive,
            string? lastAssistantMessage,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.CodexHooksEnabled)
                return CopilotCodexStopHookOutcome.Complete;

            var subagent = request.CodexSubagentHookContext?.IsStructurallyValid() == true
                ? request.CodexSubagentHookContext
                : null;
            var hookEvent = subagent == null
                ? CopilotCodexConfiguredHookEvent.Stop
                : CopilotCodexConfiguredHookEvent.SubagentStop;
            var eventName = hookEvent.ToString();

            var definitions = (request.CodexCommandHooks
                    ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true
                    && definition.Event == hookEvent
                    && (subagent == null || definition.Matches(subagent.AgentType)))
                .OrderBy(definition => definition.Order)
                .ToArray();
            if (definitions.Length == 0)
                return CopilotCodexStopHookOutcome.Complete;

            foreach (var definition in definitions.Where(definition =>
                definition.ExecutionMode == CopilotToolExecutionHookMode.Async))
            {
                var scheduled = _backgroundScheduler.TrySchedule(
                    definition.SourceId,
                    eventName,
                    subagent?.TurnId ?? request.TaskId,
                    TimeSpan.FromSeconds(definition.TimeoutSeconds),
                    async backgroundCancellationToken =>
                    {
                        var output = await new CopilotCodexCommandHook(definition, _runner)
                            .RunStopEventAsync(
                                request,
                                subagent,
                                stopHookActive,
                                lastAssistantMessage,
                                backgroundCancellationToken)
                            .ConfigureAwait(false);
                        if (output?.HasFailure == true)
                            throw new InvalidOperationException(output.StopReason);
                    });
                PublishDiagnostic(
                    onDiagnostic,
                    scheduled
                        ? $"{eventName} async hook scheduled · {definition.SourceId}"
                        : $"{eventName} async hook skipped · {definition.SourceId}: the bounded lifecycle-hook queue is full.");
            }

            var synchronousDefinitions = definitions
                .Where(definition => definition.ExecutionMode == CopilotToolExecutionHookMode.Sync)
                .ToArray();
            if (synchronousDefinitions.Length == 0)
                return CopilotCodexStopHookOutcome.Complete;

            foreach (var definition in synchronousDefinitions)
                PublishDiagnostic(onDiagnostic, $"{eventName} hook started · {definition.SourceId}");

            var results = await Task.WhenAll(synchronousDefinitions.Select(definition =>
                RunOneAsync(
                    definition,
                    request,
                    subagent,
                    stopHookActive,
                    lastAssistantMessage,
                    cancellationToken))).ConfigureAwait(false);
            var shouldStop = false;
            var stopReason = string.Empty;
            var continuationFragments = new List<string>();
            foreach (var result in results)
            {
                var systemMessage = CopilotApprovalRequestReason.Normalize(result.Output.SystemMessage);
                if (systemMessage.Length > 0)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"{eventName} hook warning · {result.Definition.SourceId}: {systemMessage}");
                }

                if (result.Output.HasFailure)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"{eventName} hook failed open · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.StopReason)}");
                }
                else if (result.Output.ShouldStop)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"{eventName} hook stopped continuation · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.StopReason)}");
                }
                else if (result.Output.ShouldContinue)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"{eventName} hook requested continuation · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.ContinuationReason)}");
                }
                else
                {
                    PublishDiagnostic(onDiagnostic, $"{eventName} hook completed · {result.Definition.SourceId}");
                }

                if (result.Output.ShouldStop && !shouldStop)
                {
                    shouldStop = true;
                    stopReason = CopilotApprovalRequestReason.Normalize(result.Output.StopReason);
                }
                if (!result.Output.ShouldContinue)
                    continue;
                var reason = CopilotApprovalRequestReason.Normalize(result.Output.ContinuationReason);
                if (reason.Length > 0)
                {
                    continuationFragments.Add(
                        $"<hook_prompt hook_run_id=\"{SecurityElement.Escape(result.Definition.SourceId)}\">"
                        + SecurityElement.Escape(reason)
                        + "</hook_prompt>");
                }
            }

            if (shouldStop)
                return new CopilotCodexStopHookOutcome(true, stopReason, false, string.Empty);
            var continuationPrompt = string.Join(Environment.NewLine + Environment.NewLine, continuationFragments);
            return continuationPrompt.Length == 0
                ? CopilotCodexStopHookOutcome.Complete
                : new CopilotCodexStopHookOutcome(false, string.Empty, true, continuationPrompt);
        }

        private async Task<HookResult> RunOneAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext? subagent,
            bool stopHookActive,
            string? lastAssistantMessage,
            CancellationToken cancellationToken)
        {
            try
            {
                var output = await new CopilotCodexCommandHook(definition, _runner)
                    .RunStopEventAsync(
                        request,
                        subagent,
                        stopHookActive,
                        lastAssistantMessage,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new HookResult(definition, output ?? new CopilotCodexStopOutput());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new HookResult(
                    definition,
                    new CopilotCodexStopOutput(
                        StopReason: $"A configured {(subagent == null ? "Stop" : "SubagentStop")} hook failed before it could inspect this answer.",
                        FailureCode: "configured_hook_failed"));
            }
        }

        private static void PublishDiagnostic(Action<string>? publish, string message)
        {
            if (publish != null)
                publish(CopilotAgentTraceEntry.Sanitize(message));
        }

        private sealed record HookResult(
            CopilotCodexCommandHookDefinition Definition,
            CopilotCodexStopOutput Output);
    }
}
