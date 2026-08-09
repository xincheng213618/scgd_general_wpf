using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotCodexSessionEndHookOutcome(
        bool WasAlreadyEnded,
        int MatchedHookCount,
        int FailedHookCount)
    {
        public static CopilotCodexSessionEndHookOutcome NotRun { get; } =
            new(false, 0, 0);

        public static CopilotCodexSessionEndHookOutcome AlreadyEnded { get; } =
            new(true, 0, 0);
    }

    internal sealed class CopilotCodexSessionEndHookExecutor
    {
        private const string Reason = "other";
        private readonly ICopilotCodexCommandHookRunner? _runner;

        public CopilotCodexSessionEndHookExecutor(
            ICopilotCodexCommandHookRunner? runner = null)
        {
            _runner = runner;
        }

        public async Task<CopilotCodexSessionEndHookOutcome> RunAsync(
            CopilotAgentRequest request,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.CodexHooksEnabled)
                return CopilotCodexSessionEndHookOutcome.NotRun;

            var definitions = (request.CodexCommandHooks
                    ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true
                    && definition.Event == CopilotCodexConfiguredHookEvent.SessionEnd
                    && definition.Matches(Reason))
                .OrderBy(definition => definition.Order)
                .ToArray();
            if (definitions.Length == 0)
                return CopilotCodexSessionEndHookOutcome.NotRun;

            foreach (var definition in definitions)
                PublishDiagnostic(onDiagnostic, $"SessionEnd hook started · {definition.SourceId}");

            var results = await Task.WhenAll(definitions.Select(definition =>
                RunOneAsync(definition, request, cancellationToken))).ConfigureAwait(false);
            var failedHookCount = 0;
            foreach (var result in results)
            {
                if (result.Output.HasFailure)
                {
                    failedHookCount++;
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SessionEnd hook failed · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.FailureMessage)}");
                }
                else
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SessionEnd hook completed · {result.Definition.SourceId}");
                }
            }

            return new CopilotCodexSessionEndHookOutcome(
                false,
                definitions.Length,
                failedHookCount);
        }

        private async Task<HookResult> RunOneAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            CancellationToken cancellationToken)
        {
            try
            {
                var output = await new CopilotCodexCommandHook(definition, _runner)
                    .OnSessionEndAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                return new HookResult(
                    definition,
                    output ?? new CopilotCodexSessionEndOutput());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new HookResult(
                    definition,
                    new CopilotCodexSessionEndOutput(
                        "A configured SessionEnd hook failed while the session was closing.",
                        "configured_hook_failed"));
            }
        }

        private static void PublishDiagnostic(Action<string>? publish, string message)
        {
            if (publish != null)
                publish(CopilotAgentTraceEntry.Sanitize(message));
        }

        private sealed record HookResult(
            CopilotCodexCommandHookDefinition Definition,
            CopilotCodexSessionEndOutput Output);
    }

    internal sealed class CopilotCodexSessionEndHookLifecycle
    {
        private const int MaximumConversationIdCharacters = 160;
        private readonly object _gate = new();
        private readonly Dictionary<string, Task<CopilotCodexSessionEndHookOutcome>> _running =
            new(StringComparer.Ordinal);
        private readonly HashSet<string> _ended = new(StringComparer.Ordinal);
        private readonly CopilotCodexSessionEndHookExecutor _executor;

        public CopilotCodexSessionEndHookLifecycle(
            CopilotCodexSessionEndHookExecutor? executor = null)
        {
            _executor = executor ?? new CopilotCodexSessionEndHookExecutor();
        }

        public Task<CopilotCodexSessionEndHookOutcome> EndAsync(
            CopilotAgentRequest request,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var conversationId = NormalizeConversationId(request.ConversationId);
            Task<CopilotCodexSessionEndHookOutcome> run;
            TaskCompletionSource<CopilotCodexSessionEndHookOutcome>? owner = null;
            lock (_gate)
            {
                if (_ended.Contains(conversationId))
                {
                    return Task.FromResult(
                        CopilotCodexSessionEndHookOutcome.AlreadyEnded);
                }
                if (!_running.TryGetValue(conversationId, out run!))
                {
                    owner = new TaskCompletionSource<CopilotCodexSessionEndHookOutcome>(
                        TaskCreationOptions.RunContinuationsAsynchronously);
                    run = owner.Task;
                    _running.Add(conversationId, run);
                }
            }

            if (owner != null)
            {
                _ = RunAndCompleteAsync(
                    conversationId,
                    request,
                    onDiagnostic,
                    owner);
            }

            return cancellationToken.CanBeCanceled
                ? run.WaitAsync(cancellationToken)
                : run;
        }

        public void Reopen(string conversationId)
        {
            var normalizedConversationId = NormalizeConversationId(conversationId);
            lock (_gate)
            {
                if (_running.ContainsKey(normalizedConversationId))
                    return;
                _ended.Remove(normalizedConversationId);
            }
        }

        private async Task RunAndCompleteAsync(
            string conversationId,
            CopilotAgentRequest request,
            Action<string>? onDiagnostic,
            TaskCompletionSource<CopilotCodexSessionEndHookOutcome> completion)
        {
            CopilotCodexSessionEndHookOutcome outcome;
            try
            {
                outcome = await _executor.RunAsync(
                    request,
                    onDiagnostic,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception)
            {
                PublishLifecycleFailure(onDiagnostic, exception);
                outcome = new CopilotCodexSessionEndHookOutcome(
                    false,
                    0,
                    1);
            }
            finally
            {
                lock (_gate)
                {
                    _running.Remove(conversationId);
                    _ended.Add(conversationId);
                }
            }
            completion.TrySetResult(outcome);
        }

        private static void PublishLifecycleFailure(
            Action<string>? publish,
            Exception exception)
        {
            if (publish == null)
                return;
            var message = exception is OperationCanceledException
                ? "SessionEnd hook lifecycle was canceled while the session was closing."
                : "SessionEnd hook lifecycle failed open while the session was closing.";
            publish(CopilotAgentTraceEntry.Sanitize(message));
        }

        private static string NormalizeConversationId(string? conversationId)
        {
            var normalized = (conversationId ?? string.Empty).Trim();
            if (normalized.Length is < 1 or > MaximumConversationIdCharacters
                || normalized.Any(char.IsControl))
            {
                throw new ArgumentException(
                    "A bounded non-control conversation ID is required.",
                    nameof(conversationId));
            }
            return normalized;
        }
    }
}
