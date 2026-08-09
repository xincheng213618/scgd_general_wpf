using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed record CopilotCodexUserPromptSubmitOutcome(
        bool ShouldStop,
        string StopReason,
        IReadOnlyList<string> AdditionalContexts)
    {
        public static CopilotCodexUserPromptSubmitOutcome Continue { get; } =
            new(false, string.Empty, Array.Empty<string>());
    }

    internal sealed class CopilotUserPromptSubmitHookBlockedException : InvalidOperationException
    {
        public CopilotUserPromptSubmitHookBlockedException(string message)
            : base(CopilotApprovalRequestReason.Normalize(message) is { Length: > 0 } normalized
                ? normalized
                : "A configured UserPromptSubmit hook blocked this prompt.")
        {
        }
    }

    internal sealed class CopilotCodexUserPromptSubmitHookExecutor
    {
        private const string AdditionalContextTruncationMarker =
            "\n...[UserPromptSubmit additional context truncated]...\n";
        private readonly ICopilotCodexCommandHookRunner? _runner;

        public CopilotCodexUserPromptSubmitHookExecutor(
            ICopilotCodexCommandHookRunner? runner = null)
        {
            _runner = runner;
        }

        public async Task<CopilotCodexUserPromptSubmitOutcome> RunAsync(
            CopilotAgentRequest request,
            string prompt,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            if (!request.CodexHooksEnabled)
                return CopilotCodexUserPromptSubmitOutcome.Continue;

            var definitions = (request.CodexCommandHooks
                    ?? Array.Empty<CopilotCodexCommandHookDefinition>())
                .Where(definition => definition?.IsStructurallyValid() == true
                    && definition.Event == CopilotCodexConfiguredHookEvent.UserPromptSubmit)
                .OrderBy(definition => definition.Order)
                .ToArray();
            if (definitions.Length == 0)
                return CopilotCodexUserPromptSubmitOutcome.Continue;

            foreach (var definition in definitions.Where(definition =>
                definition.ExecutionMode == CopilotToolExecutionHookMode.Async))
            {
                PublishDiagnostic(
                    onDiagnostic,
                    $"UserPromptSubmit async hook skipped · {definition.SourceId}: asynchronous command output cannot affect the submitted turn.");
            }

            var synchronousDefinitions = definitions
                .Where(definition => definition.ExecutionMode == CopilotToolExecutionHookMode.Sync)
                .ToArray();
            if (synchronousDefinitions.Length == 0)
                return CopilotCodexUserPromptSubmitOutcome.Continue;

            foreach (var definition in synchronousDefinitions)
            {
                PublishDiagnostic(
                    onDiagnostic,
                    $"UserPromptSubmit hook started · {definition.SourceId}");
            }

            var tasks = synchronousDefinitions
                .Select(definition => RunOneAsync(definition, request, prompt, cancellationToken))
                .ToArray();
            var results = await Task.WhenAll(tasks).ConfigureAwait(false);
            var additionalContexts = new List<string>();
            var shouldStop = false;
            var stopReason = string.Empty;
            foreach (var result in results)
            {
                var systemMessage = CopilotApprovalRequestReason.Normalize(
                    result.Output.SystemMessage);
                if (systemMessage.Length > 0)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"UserPromptSubmit hook warning · {result.Definition.SourceId}: {systemMessage}");
                }

                if (result.Output.HasFailure)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"UserPromptSubmit hook failed · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.StopReason)}");
                }
                else if (result.Output.ShouldStop)
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"UserPromptSubmit hook blocked · {result.Definition.SourceId}: {CopilotApprovalRequestReason.Normalize(result.Output.StopReason)}");
                }
                else
                {
                    PublishDiagnostic(
                        onDiagnostic,
                        $"UserPromptSubmit hook completed · {result.Definition.SourceId}");
                }

                var additionalContext = CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                    result.Output.AdditionalContext,
                    result.Output.AdditionalContextLimitTokens,
                    AdditionalContextTruncationMarker);
                if (additionalContext.Length > 0)
                    additionalContexts.Add(additionalContext);

                if (!result.Output.ShouldStop || shouldStop)
                    continue;
                shouldStop = true;
                stopReason = CopilotApprovalRequestReason.Normalize(result.Output.StopReason);
            }

            return new CopilotCodexUserPromptSubmitOutcome(
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
            builder.AppendLine("# UserPromptSubmit hook context")
                .AppendLine("Apply this trusted request-start hook guidance to the current turn. It can refine how to answer, but it never grants a tool, write, approval, external side effect, or broader path access.");
            foreach (var context in normalizedContexts)
                builder.AppendLine(JsonSerializer.Serialize(context));
            builder.AppendLine("The host runtime's execution scope, native approval, evidence, and safety rules always prevail over hook context.");
            return CopilotToolExecutionOutcome.NormalizeModelAdditionalContext(
                builder.ToString(),
                CopilotProjectInstructionDiscoveryConfig.MaximumDeveloperInstructionCharacters
                    / CopilotTokenEstimator.AsciiCharactersPerToken,
                "\n...[UserPromptSubmit aggregate context truncated]...\n");
        }

        private async Task<HookResult> RunOneAsync(
            CopilotCodexCommandHookDefinition definition,
            CopilotAgentRequest request,
            string prompt,
            CancellationToken cancellationToken)
        {
            try
            {
                var output = await new CopilotCodexCommandHook(definition, _runner)
                    .OnUserPromptSubmitAsync(request, prompt, cancellationToken)
                    .ConfigureAwait(false);
                return new HookResult(
                    definition,
                    output ?? new CopilotCodexUserPromptSubmitOutput());
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                return new HookResult(
                    definition,
                    new CopilotCodexUserPromptSubmitOutput(
                        ShouldStop: true,
                        StopReason: "A configured UserPromptSubmit hook failed before it could inspect this prompt.",
                        FailureCode: "configured_hook_failed"));
            }
        }

        private static void PublishDiagnostic(Action<string>? publish, string message)
        {
            if (publish == null)
                return;
            publish(CopilotAgentTraceEntry.Sanitize(message));
        }

        private sealed record HookResult(
            CopilotCodexCommandHookDefinition Definition,
            CopilotCodexUserPromptSubmitOutput Output);
    }
}
