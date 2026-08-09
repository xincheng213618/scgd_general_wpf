using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotCodexSubagentHookContext(
        string AgentId,
        string AgentType,
        string TurnId)
    {
        private const int MaximumIdentifierCharacters = 256;

        public bool IsStructurallyValid() =>
            IsValidIdentifier(AgentId)
            && IsValidIdentifier(AgentType)
            && IsValidIdentifier(TurnId);

        public CopilotCodexSubagentHookContext CreateSnapshot() => this with { };

        private static bool IsValidIdentifier(string? value) =>
            !string.IsNullOrWhiteSpace(value)
            && string.Equals(value, value.Trim(), StringComparison.Ordinal)
            && value.Length <= MaximumIdentifierCharacters
            && !value.Any(char.IsControl);
    }

    internal sealed record CopilotCodexSubagentStartHookOutcome(
        IReadOnlyList<string> AdditionalContexts)
    {
        public static CopilotCodexSubagentStartHookOutcome Continue { get; } =
            new(Array.Empty<string>());
    }

    internal sealed class CopilotCodexSubagentStartHookExecutor
    {
        private const string AdditionalContextTruncationMarker =
            "\n...[SubagentStart additional context truncated]...\n";
        private readonly ICopilotCodexCommandHookRunner? _runner;
        private readonly ICopilotCodexLifecycleHookBackgroundScheduler _backgroundScheduler;

        public CopilotCodexSubagentStartHookExecutor(
            ICopilotCodexCommandHookRunner? runner = null,
            ICopilotCodexLifecycleHookBackgroundScheduler? backgroundScheduler = null)
        {
            _runner = runner;
            _backgroundScheduler = backgroundScheduler
                ?? CopilotCodexLifecycleHookBackgroundScheduler.Shared;
        }

        public async Task<CopilotCodexSubagentStartHookOutcome> RunAsync(
            CopilotAgentRequest request,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            var subagent = request.CodexSubagentHookContext;
            if (!request.CodexHooksEnabled || subagent?.IsStructurallyValid() != true)
                return CopilotCodexSubagentStartHookOutcome.Continue;

            var definitions = (request.CodexCommandHooks
                    ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true
                    && definition.Event == CopilotCodexConfiguredHookEvent.SubagentStart
                    && definition.Matches(subagent.AgentType))
                .OrderBy(definition => definition.Order)
                .ToArray();
            if (definitions.Length == 0)
                return CopilotCodexSubagentStartHookOutcome.Continue;

            foreach (var definition in definitions.Where(definition =>
                definition.ExecutionMode == CopilotToolExecutionHookMode.Async))
            {
                var scheduled = _backgroundScheduler.TrySchedule(
                    definition.SourceId,
                    "SubagentStart",
                    subagent.TurnId,
                    TimeSpan.FromSeconds(definition.TimeoutSeconds),
                    async backgroundCancellationToken =>
                    {
                        var output = await new CopilotCodexCommandHook(definition, _runner)
                            .OnSubagentStartAsync(
                                request,
                                subagent,
                                backgroundCancellationToken)
                            .ConfigureAwait(false);
                        if (output?.HasFailure == true)
                            throw new InvalidOperationException(output.FailureMessage);
                    });
                PublishDiagnostic(
                    onDiagnostic,
                    scheduled
                        ? $"SubagentStart async hook scheduled · {definition.SourceId}"
                        : $"SubagentStart async hook skipped · {definition.SourceId}: the bounded lifecycle-hook queue is full.");
            }

            var synchronousDefinitions = definitions
                .Where(definition => definition.ExecutionMode == CopilotToolExecutionHookMode.Sync)
                .ToArray();
            if (synchronousDefinitions.Length == 0)
                return CopilotCodexSubagentStartHookOutcome.Continue;

            foreach (var definition in synchronousDefinitions)
                PublishDiagnostic(onDiagnostic, $"SubagentStart hook started · {definition.SourceId}");
            var results = await Task.WhenAll(synchronousDefinitions.Select(definition =>
                RunOneAsync(definition, request, subagent, cancellationToken))).ConfigureAwait(false);

            var additionalContexts = new List<string>();
            foreach (var result in results)
            {
                var systemMessage = CopilotApprovalRequestReason.Normalize(result.Output.SystemMessage);
                if (systemMessage.Length > 0)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SubagentStart hook warning · {result.Definition.SourceId}: {systemMessage}");
                }

                if (result.Output.HasFailure)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"SubagentStart hook failed open · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.FailureMessage)}");
                    continue;
                }

                var additionalContext = CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                    result.Output.AdditionalContext,
                    result.Output.AdditionalContextLimitTokens,
                    AdditionalContextTruncationMarker);
                if (additionalContext.Length > 0)
                    additionalContexts.Add(additionalContext);
                PublishDiagnostic(onDiagnostic, $"SubagentStart hook completed · {result.Definition.SourceId}");
            }

            return new CopilotCodexSubagentStartHookOutcome(additionalContexts.ToArray());
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
            builder.AppendLine("# SubagentStart hook context")
                .AppendLine("Apply this trusted subagent-start guidance to the delegated run. It can refine how the subagent investigates or answers, but it never grants a tool, write, approval, external side effect, broader path access, or authority beyond the parent request.");
            foreach (var context in normalizedContexts)
                builder.AppendLine(JsonSerializer.Serialize(context));
            builder.AppendLine("The host runtime's selected subagent role, execution scope, native approval, evidence, and safety rules always prevail over hook context.");
            return CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                builder.ToString(),
                CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters
                    / CopilotTokenEstimator.AsciiCharactersPerToken,
                "\n...[SubagentStart aggregate context truncated]...\n");
        }

        internal static string MergeDeveloperInstructions(
            string? configuredDeveloperInstructions,
            IReadOnlyList<string> contexts)
        {
            var hookContext = BuildDeveloperContext(contexts);
            var configured = configuredDeveloperInstructions?.Trim() ?? string.Empty;
            if (hookContext.Length == 0)
                return configured;
            if (configured.Length == 0)
                return hookContext;

            var availableConfiguredCharacters = Math.Max(
                0,
                CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters
                    - hookContext.Length
                    - Environment.NewLine.Length * 2);
            if (configured.Length > availableConfiguredCharacters)
                configured = configured[..availableConfiguredCharacters].TrimEnd();
            return configured.Length == 0
                ? hookContext
                : string.Join(Environment.NewLine + Environment.NewLine, hookContext, configured);
        }

        private async Task<HookResult> RunOneAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            CopilotCodexSubagentHookContext subagent,
            CancellationToken cancellationToken)
        {
            try
            {
                var output = await new CopilotCodexCommandHook(definition, _runner)
                    .OnSubagentStartAsync(request, subagent, cancellationToken)
                    .ConfigureAwait(false);
                return new HookResult(
                    definition,
                    output ?? new CopilotCodexSubagentStartOutput());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new HookResult(
                    definition,
                    new CopilotCodexSubagentStartOutput(
                        FailureMessage: "A configured SubagentStart hook failed before it could inspect this subagent.",
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
            CopilotCodexSubagentStartOutput Output);
    }
}
