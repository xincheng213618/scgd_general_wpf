using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexCompactHookTrigger
    {
        Manual,
        Auto,
    }

    internal sealed record CopilotCodexCompactHookOutcome(
        bool ShouldStop,
        string StopReason)
    {
        public static CopilotCodexCompactHookOutcome Continue { get; } =
            new(false, string.Empty);
    }

    internal sealed record CopilotCodexCompactionHookLifecycleOutcome(
        bool CompactionApplied,
        CopilotCodexCompactHookOutcome PreCompact,
        CopilotCodexCompactHookOutcome PostCompact);

    internal sealed class CopilotCodexCompactionHookLifecycle
    {
        private readonly CopilotCodexCompactHookExecutor _executor;

        public CopilotCodexCompactionHookLifecycle(
            CopilotCodexCompactHookExecutor? executor = null)
        {
            _executor = executor ?? new CopilotCodexCompactHookExecutor();
        }

        public async Task<CopilotCodexCompactionHookLifecycleOutcome> RunAsync(
            CopilotAgentRequest request,
            CopilotCodexCompactHookTrigger trigger,
            Func<CancellationToken, Task<bool>> compactAsync,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(compactAsync);
            var preCompact = await _executor.RunAsync(
                request,
                CopilotCodexConfiguredHookEvent.PreCompact,
                trigger,
                onDiagnostic,
                cancellationToken).ConfigureAwait(false);
            if (preCompact.ShouldStop)
            {
                return new CopilotCodexCompactionHookLifecycleOutcome(
                    false,
                    preCompact,
                    CopilotCodexCompactHookOutcome.Continue);
            }

            var compactionApplied = await compactAsync(cancellationToken).ConfigureAwait(false);
            if (!compactionApplied)
            {
                return new CopilotCodexCompactionHookLifecycleOutcome(
                    false,
                    preCompact,
                    CopilotCodexCompactHookOutcome.Continue);
            }
            var postCompact = await _executor.RunAsync(
                request,
                CopilotCodexConfiguredHookEvent.PostCompact,
                trigger,
                onDiagnostic,
                cancellationToken).ConfigureAwait(false);
            return new CopilotCodexCompactionHookLifecycleOutcome(
                true,
                preCompact,
                postCompact);
        }
    }

    internal sealed class CopilotCodexCompactHookExecutor
    {
        private readonly ICopilotCodexCommandHookRunner? _runner;
        private readonly ICopilotCodexLifecycleHookBackgroundScheduler _backgroundScheduler;

        public CopilotCodexCompactHookExecutor(
            ICopilotCodexCommandHookRunner? runner = null,
            ICopilotCodexLifecycleHookBackgroundScheduler? backgroundScheduler = null)
        {
            _runner = runner;
            _backgroundScheduler = backgroundScheduler
                ?? CopilotCodexLifecycleHookBackgroundScheduler.Shared;
        }

        public async Task<CopilotCodexCompactHookOutcome> RunAsync(
            CopilotAgentRequest request,
            CopilotCodexConfiguredHookEvent hookEvent,
            CopilotCodexCompactHookTrigger trigger,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (hookEvent is not (CopilotCodexConfiguredHookEvent.PreCompact
                or CopilotCodexConfiguredHookEvent.PostCompact))
            {
                throw new ArgumentOutOfRangeException(nameof(hookEvent));
            }
            if (!request.CodexHooksEnabled)
                return CopilotCodexCompactHookOutcome.Continue;

            var triggerValue = trigger switch
            {
                CopilotCodexCompactHookTrigger.Manual => "manual",
                CopilotCodexCompactHookTrigger.Auto => "auto",
                _ => throw new ArgumentOutOfRangeException(nameof(trigger)),
            };
            var eventName = hookEvent.ToString();
            var definitions = (request.CodexCommandHooks
                    ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true
                    && definition.Event == hookEvent
                    && definition.Matches(triggerValue))
                .OrderBy(definition => definition.Order)
                .ToArray();
            if (definitions.Length == 0)
                return CopilotCodexCompactHookOutcome.Continue;

            foreach (var definition in definitions.Where(definition =>
                definition.ExecutionMode == CopilotToolExecutionHookMode.Async))
            {
                var scheduled = _backgroundScheduler.TrySchedule(
                    definition.SourceId,
                    eventName,
                    request.TaskId,
                    TimeSpan.FromSeconds(definition.TimeoutSeconds),
                    async backgroundCancellationToken =>
                    {
                        var output = await new CopilotCodexCommandHook(definition, _runner)
                            .OnCompactAsync(
                                request,
                                hookEvent,
                                triggerValue,
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
                return CopilotCodexCompactHookOutcome.Continue;

            foreach (var definition in synchronousDefinitions)
                PublishDiagnostic(onDiagnostic, $"{eventName} hook started · {definition.SourceId}");
            var results = await Task.WhenAll(synchronousDefinitions.Select(definition =>
                RunOneAsync(
                    definition,
                    request,
                    hookEvent,
                    triggerValue,
                    cancellationToken))).ConfigureAwait(false);

            var shouldStop = false;
            var stopReason = string.Empty;
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
                        $"{eventName} hook stopped compaction · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.StopReason)}");
                    if (!shouldStop)
                    {
                        shouldStop = true;
                        stopReason = CopilotApprovalRequestReason.Normalize(result.Output.StopReason);
                    }
                }
                else
                {
                    PublishDiagnostic(onDiagnostic, $"{eventName} hook completed · {result.Definition.SourceId}");
                }
            }

            return shouldStop
                ? new CopilotCodexCompactHookOutcome(true, stopReason)
                : CopilotCodexCompactHookOutcome.Continue;
        }

        private async Task<HookResult> RunOneAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            CopilotCodexConfiguredHookEvent hookEvent,
            string trigger,
            CancellationToken cancellationToken)
        {
            try
            {
                var output = await new CopilotCodexCommandHook(definition, _runner)
                    .OnCompactAsync(
                        request,
                        hookEvent,
                        trigger,
                        cancellationToken)
                    .ConfigureAwait(false);
                return new HookResult(definition, output ?? new CopilotCodexCompactOutput());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new HookResult(
                    definition,
                    new CopilotCodexCompactOutput(
                        StopReason: $"A configured {hookEvent} hook failed before it could inspect this compaction.",
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
            CopilotCodexCompactOutput Output);
    }
}
