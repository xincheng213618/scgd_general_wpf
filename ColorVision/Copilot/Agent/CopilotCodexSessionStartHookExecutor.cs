using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexSessionStartSource
    {
        Startup,
        Resume,
        Clear,
        Compact,
    }

    internal sealed record CopilotCodexSessionStartHookOutcome(
        bool ShouldStop,
        string StopReason,
        IReadOnlyList<string> AdditionalContexts)
    {
        public static CopilotCodexSessionStartHookOutcome Continue { get; } =
            new(false, string.Empty, Array.Empty<string>());
    }

    internal sealed class CopilotSessionStartHookBlockedException : InvalidOperationException
    {
        public CopilotSessionStartHookBlockedException(string message)
            : base(CopilotApprovalRequestReason.Normalize(message) is { Length: > 0 } normalized
                ? normalized
                : "A configured SessionStart hook stopped this turn.")
        {
        }
    }

    internal sealed class CopilotCodexSessionStartHookExecutor
    {
        private const string AdditionalContextTruncationMarker =
            "\n...[SessionStart additional context truncated]...\n";
        private readonly ICopilotCodexCommandHookRunner? _runner;
        private readonly ICopilotCodexLifecycleHookBackgroundScheduler _backgroundScheduler;

        public CopilotCodexSessionStartHookExecutor(
            ICopilotCodexCommandHookRunner? runner = null,
            ICopilotCodexLifecycleHookBackgroundScheduler? backgroundScheduler = null)
        {
            _runner = runner;
            _backgroundScheduler = backgroundScheduler
                ?? CopilotCodexLifecycleHookBackgroundScheduler.Shared;
        }

        public async Task<CopilotCodexSessionStartHookOutcome> RunAsync(
            CopilotAgentRequest request,
            CopilotCodexSessionStartSource source,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.CodexHooksEnabled)
                return CopilotCodexSessionStartHookOutcome.Continue;

            var sourceValue = GetSourceValue(source);
            var definitions = (request.CodexCommandHooks
                    ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true
                    && definition.Event == CopilotCodexConfiguredHookEvent.SessionStart
                    && definition.Matches(sourceValue))
                .OrderBy(definition => definition.Order)
                .ToArray();
            if (definitions.Length == 0)
                return CopilotCodexSessionStartHookOutcome.Continue;

            foreach (var definition in definitions.Where(definition =>
                definition.ExecutionMode == CopilotToolExecutionHookMode.Async))
            {
                var scheduled = _backgroundScheduler.TrySchedule(
                    request.ConversationId,
                    definition.SourceId,
                    "SessionStart",
                    request.TaskId,
                    TimeSpan.FromSeconds(definition.TimeoutSeconds),
                    async backgroundCancellationToken =>
                    {
                        var output = await new CopilotCodexCommandHook(definition, _runner)
                            .OnSessionStartAsync(
                                request,
                                sourceValue,
                                backgroundCancellationToken)
                            .ConfigureAwait(false);
                        return CopilotCodexAsyncHookOutput.From(output);
                    });
                PublishDiagnostic(
                    onDiagnostic,
                    scheduled
                        ? $"SessionStart async hook scheduled · {definition.SourceId}"
                        : $"SessionStart async hook skipped · {definition.SourceId}: the bounded per-session command-hook queue is full.");
            }

            var synchronousDefinitions = definitions
                .Where(definition => definition.ExecutionMode == CopilotToolExecutionHookMode.Sync)
                .ToArray();
            if (synchronousDefinitions.Length == 0)
                return CopilotCodexSessionStartHookOutcome.Continue;

            foreach (var definition in synchronousDefinitions)
                PublishDiagnostic(onDiagnostic, $"SessionStart hook started · {definition.SourceId}");
            var results = await Task.WhenAll(synchronousDefinitions.Select(definition =>
                RunOneAsync(
                    definition,
                    request,
                    sourceValue,
                    cancellationToken))).ConfigureAwait(false);

            var additionalContexts = new List<string>();
            var shouldStop = false;
            var stopReason = string.Empty;
            foreach (var result in results)
            {
                var systemMessage = CopilotApprovalRequestReason.Normalize(result.Output.SystemMessage);
                if (systemMessage.Length > 0)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SessionStart hook warning · {result.Definition.SourceId}: {systemMessage}");
                }

                if (result.Output.HasFailure)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SessionStart hook failed open · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.FailureMessage)}");
                    continue;
                }

                var additionalContext = CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                    result.Output.AdditionalContext,
                    result.Output.AdditionalContextLimitTokens,
                    AdditionalContextTruncationMarker);
                if (additionalContext.Length > 0)
                    additionalContexts.Add(additionalContext);

                if (result.Output.ShouldStop)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SessionStart hook stopped turn · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.StopReason)}");
                    if (!shouldStop)
                    {
                        shouldStop = true;
                        stopReason = CopilotApprovalRequestReason.Normalize(result.Output.StopReason);
                    }
                }
                else
                {
                    PublishDiagnostic(onDiagnostic, $"SessionStart hook completed · {result.Definition.SourceId}");
                }
            }

            return new CopilotCodexSessionStartHookOutcome(
                shouldStop,
                stopReason,
                additionalContexts.ToArray());
        }

        internal static string BuildDeveloperContext(IReadOnlyList<string> contexts)
        {
            if (contexts == null || contexts.Count == 0)
                return string.Empty;

            var normalizedContexts = contexts
                .Where(context => !string.IsNullOrWhiteSpace(context))
                .Select(context => context.Trim())
                .ToArray();
            if (normalizedContexts.Length == 0)
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("# SessionStart hook context")
                .AppendLine("Apply this trusted session-start guidance throughout the current conversation. It can refine how to answer, but it never grants a tool, write, approval, external side effect, or broader path access.");
            foreach (var context in normalizedContexts)
                builder.AppendLine(JsonSerializer.Serialize(context));
            builder.AppendLine("The host runtime's execution scope, native approval, evidence, and safety rules always prevail over hook context.");
            return CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                builder.ToString(),
                CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters
                    / CopilotTokenEstimator.AsciiCharactersPerToken,
                "\n...[SessionStart aggregate context truncated]...\n");
        }

        internal static string MergeDeveloperContexts(
            IReadOnlyList<string> sessionStartContexts,
            IReadOnlyList<string> userPromptSubmitContexts) =>
            MergeDeveloperContexts(
                sessionStartContexts,
                userPromptSubmitContexts,
                Array.Empty<string>());

        internal static string MergeDeveloperContexts(
            IReadOnlyList<string> sessionStartContexts,
            IReadOnlyList<string> userPromptSubmitContexts,
            IReadOnlyList<string> asyncHookContexts)
        {
            var sessionContext = BuildDeveloperContext(sessionStartContexts);
            var promptContext = CopilotCodexUserPromptSubmitHookExecutor.BuildDeveloperContext(
                userPromptSubmitContexts);
            var asyncContext = CopilotCodexAsyncHookResultDelivery.BuildDeveloperContext(
                asyncHookContexts);
            var contexts = new[] { sessionContext, promptContext, asyncContext }
                .Where(context => context.Length > 0)
                .ToArray();
            if (contexts.Length == 0)
                return string.Empty;
            if (contexts.Length == 1)
                return contexts[0];
            return CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                string.Join(Environment.NewLine + Environment.NewLine, contexts),
                CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters
                    / CopilotTokenEstimator.AsciiCharactersPerToken,
                "\n...[Codex hook aggregate context truncated]...\n");
        }

        internal static string GetSourceValue(CopilotCodexSessionStartSource source) => source switch
        {
            CopilotCodexSessionStartSource.Startup => "startup",
            CopilotCodexSessionStartSource.Resume => "resume",
            CopilotCodexSessionStartSource.Clear => "clear",
            CopilotCodexSessionStartSource.Compact => "compact",
            _ => throw new ArgumentOutOfRangeException(nameof(source)),
        };

        private async Task<HookResult> RunOneAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string source,
            CancellationToken cancellationToken)
        {
            try
            {
                var output = await new CopilotCodexCommandHook(definition, _runner)
                    .OnSessionStartAsync(request, source, cancellationToken)
                    .ConfigureAwait(false);
                return new HookResult(
                    definition,
                    output ?? new CopilotCodexSessionStartOutput());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new HookResult(
                    definition,
                    new CopilotCodexSessionStartOutput(
                        FailureMessage: "A configured SessionStart hook failed before it could initialize this session.",
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
            CopilotCodexSessionStartOutput Output);
    }

    internal sealed class CopilotCodexSessionStartHookLifecycle
    {
        private const int MaximumConversationIdCharacters = 160;
        private const int MaximumRetainedContexts = 32;
        private readonly object _gate = new();
        private readonly Dictionary<string, SessionState> _sessions = new(StringComparer.Ordinal);
        private readonly CopilotCodexSessionStartHookExecutor _executor;

        public CopilotCodexSessionStartHookLifecycle(
            CopilotCodexSessionStartHookExecutor? executor = null)
        {
            _executor = executor ?? new CopilotCodexSessionStartHookExecutor();
        }

        public void Queue(string conversationId, CopilotCodexSessionStartSource source)
        {
            var normalizedConversationId = NormalizeConversationId(conversationId);
            lock (_gate)
            {
                var state = GetOrCreateState(normalizedConversationId);
                if (source == CopilotCodexSessionStartSource.Clear)
                {
                    state.PendingSources.Clear();
                    state.AdditionalContexts.Clear();
                    state.HasInitialSource = true;
                }
                else if (source is CopilotCodexSessionStartSource.Startup
                    or CopilotCodexSessionStartSource.Resume)
                {
                    if (state.HasInitialSource)
                        return;
                    state.HasInitialSource = true;
                }

                if (state.PendingSources.Count == 0
                    || state.PendingSources[^1] != source)
                {
                    state.PendingSources.Add(source);
                }
            }
        }

        public void End(string conversationId)
        {
            var normalizedConversationId = NormalizeConversationId(conversationId);
            lock (_gate)
                _sessions.Remove(normalizedConversationId);
        }

        public async Task<CopilotCodexSessionStartHookOutcome> RunBeforeTurnAsync(
            CopilotAgentRequest request,
            bool hasPersistedHistory,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var conversationId = NormalizeConversationId(request.ConversationId);
            SessionState state;
            lock (_gate)
            {
                state = GetOrCreateState(conversationId);
                if (!state.HasInitialSource)
                {
                    state.HasInitialSource = true;
                    state.PendingSources.Insert(
                        0,
                        hasPersistedHistory
                            ? CopilotCodexSessionStartSource.Resume
                            : CopilotCodexSessionStartSource.Startup);
                }
            }

            await state.ExecutionGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                while (true)
                {
                    CopilotCodexSessionStartSource source;
                    lock (_gate)
                    {
                        if (state.PendingSources.Count == 0)
                        {
                            return new CopilotCodexSessionStartHookOutcome(
                                false,
                                string.Empty,
                                state.AdditionalContexts.ToArray());
                        }
                        source = state.PendingSources[0];
                        state.PendingSources.RemoveAt(0);
                    }

                    CopilotCodexSessionStartHookOutcome outcome;
                    try
                    {
                        outcome = await _executor.RunAsync(
                            request,
                            source,
                            onDiagnostic,
                            cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException)
                    {
                        lock (_gate)
                            state.PendingSources.Insert(0, source);
                        throw;
                    }

                    lock (_gate)
                    {
                        state.AdditionalContexts.AddRange(
                            outcome.AdditionalContexts
                                .Where(context => !string.IsNullOrWhiteSpace(context))
                                .Select(context => context.Trim()));
                        if (state.AdditionalContexts.Count > MaximumRetainedContexts)
                        {
                            state.AdditionalContexts.RemoveRange(
                                0,
                                state.AdditionalContexts.Count - MaximumRetainedContexts);
                        }
                        if (outcome.ShouldStop)
                        {
                            return new CopilotCodexSessionStartHookOutcome(
                                true,
                                outcome.StopReason,
                                state.AdditionalContexts.ToArray());
                        }
                    }
                }
            }
            finally
            {
                state.ExecutionGate.Release();
            }
        }

        private SessionState GetOrCreateState(string conversationId)
        {
            if (_sessions.TryGetValue(conversationId, out var state))
                return state;
            state = new SessionState();
            _sessions.Add(conversationId, state);
            return state;
        }

        private static string NormalizeConversationId(string? conversationId)
        {
            var normalized = (conversationId ?? string.Empty).Trim();
            if (normalized.Length is < 1 or > MaximumConversationIdCharacters
                || normalized.Any(char.IsControl))
                throw new ArgumentException("A bounded non-control conversation ID is required.", nameof(conversationId));
            return normalized;
        }

        private sealed class SessionState
        {
            public SemaphoreSlim ExecutionGate { get; } = new(1, 1);

            public List<CopilotCodexSessionStartSource> PendingSources { get; } = new();

            public List<string> AdditionalContexts { get; } = new();

            public bool HasInitialSource { get; set; }
        }
    }
}
