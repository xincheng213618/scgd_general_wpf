#pragma warning disable MAAI001
#pragma warning disable CA1859
using Anthropic;
using Anthropic.Core;
using ColorVision.Copilot.Mcp;
using ColorVision.Solution;
using Microsoft.Agents.AI;
using Microsoft.Agents.AI.Compaction;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AIChatFinishReason = Microsoft.Extensions.AI.ChatFinishReason;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotMicrosoftAgentFrameworkRuntime
    {
        internal static CopilotAgentStopReason DetermineStopReason(
            CopilotAgentTaskLedgerSnapshot taskLedger,
            CopilotAgentBudgetSnapshot budget,
            IReadOnlyList<CopilotAgentStepRecord> steps,
            bool hasModelFinalAnswer,
            CopilotAgentMode requestMode = CopilotAgentMode.Auto)
        {
            var requestOrTimeBudgetExhausted = budget.RequestTokenBudgetExhausted
                || budget.TimeBudgetExhausted
                || (budget.BudgetExhausted && !budget.ToolBudgetExhausted);
            var completedNarrowEvidenceRequest = budget.NarrowEvidenceResultLimit > 0
                && hasModelFinalAnswer
                && taskLedger.RemainingCount == 0;
            if (requestOrTimeBudgetExhausted
                || (budget.ToolBudgetExhausted
                    && !completedNarrowEvidenceRequest))
            {
                return CopilotAgentStopReason.BudgetExhausted;
            }
            if (requestMode == CopilotAgentMode.Plan)
            {
                var denied = steps.LastOrDefault(
                    step => step.Execution.State == CopilotToolExecutionState.Denied);
                if (denied != null)
                {
                    return CopilotToolFailureCode.HasApprovalProvenance(
                            denied.Observation.FailureCode)
                        ? CopilotAgentStopReason.ApprovalDenied
                        : CopilotAgentStopReason.Blocked;
                }
                return hasModelFinalAnswer
                    ? CopilotAgentStopReason.Completed
                    : CopilotAgentStopReason.IncompleteOutput;
            }
            if (taskLedger.RemainingCount == 0)
                return hasModelFinalAnswer ? CopilotAgentStopReason.Completed : CopilotAgentStopReason.IncompleteOutput;
            var latestDenied = steps.LastOrDefault(
                step => step.Execution.State == CopilotToolExecutionState.Denied);
            if (latestDenied != null)
            {
                return CopilotToolFailureCode.HasApprovalProvenance(
                        latestDenied.Observation.FailureCode)
                    ? CopilotAgentStopReason.ApprovalDenied
                    : CopilotAgentStopReason.Blocked;
            }
            if (string.Equals(taskLedger.Mode, "plan", StringComparison.OrdinalIgnoreCase))
                return CopilotAgentStopReason.AwaitingUser;
            return CopilotAgentStopReason.TaskPassLimit;
        }

        internal static string ResolveInitialHarnessMode(CopilotAgentMode requestMode)
        {
            return requestMode == CopilotAgentMode.Plan ? "plan" : "execute";
        }

        private static async Task<CopilotAgentTaskLedgerSnapshot> CaptureTaskLedgerAsync(
            TodoProvider? todoProvider,
            AgentModeProvider? modeProvider,
            AgentSession session,
            bool resumedFromCheckpoint,
            CancellationToken cancellationToken)
        {
            var mode = modeProvider == null
                ? "execute"
                : await modeProvider.GetModeAsync(session, cancellationToken);
            if (todoProvider == null)
            {
                return new CopilotAgentTaskLedgerSnapshot
                {
                    Mode = mode,
                    ResumedFromCheckpoint = resumedFromCheckpoint,
                };
            }

            var todos = await todoProvider.GetAllTodosAsync(session, cancellationToken);
            return new CopilotAgentTaskLedgerSnapshot
            {
                Mode = mode,
                ResumedFromCheckpoint = resumedFromCheckpoint,
                Items = todos.Select(item => new CopilotAgentTaskItem
                {
                    Id = item.Id,
                    Title = item.Title ?? string.Empty,
                    Description = item.Description ?? string.Empty,
                    IsComplete = item.IsComplete,
                }).ToArray(),
            };
        }

        private static string FormatTaskLedgerDiagnostic(string prefix, CopilotAgentTaskLedgerSnapshot ledger)
        {
            var summary = $"{prefix} · {ledger.CompletedCount}/{ledger.TotalCount} complete · mode {ledger.Mode}";
            var remaining = ledger.Items.Where(item => !item.IsComplete).Take(3).Select(item => $"[{item.Id}] {SanitizeTaskTitle(item.Title)}").ToArray();
            return remaining.Length == 0 ? summary + "." : summary + " · open: " + string.Join("; ", remaining) + ".";
        }

        private static string FormatCapabilityReplanDiagnostic(CopilotAgentCheckpointCompatibility compatibility)
        {
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.ProfileChanged)
                return "Persisted Agent session belongs to a different model profile; its task plan was discarded and Agent Framework will re-plan against the current profile and tools.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.CapabilitySnapshotMissing)
                return "Persisted Agent session predates capability tracking; its task plan was discarded and Agent Framework will re-plan against current tools.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.ToolSurfaceSnapshotMissing)
                return "Persisted Agent session predates request-scoped tool tracking; its internal task state was discarded and Agent Framework will re-plan from visible conversation history and current tools.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.ToolSurfaceDrift)
                return $"Agent request tool surface changed · {compatibility.RemovedToolNames.Count} previously available tool(s) removed ({string.Join(", ", compatibility.RemovedToolNames.Take(4))}). Persisted internal task state was discarded and Agent Framework will re-plan from visible conversation history and current tools.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.EnvironmentSnapshotMissing)
                return "Persisted Agent session predates runtime environment tracking; its internal task state was discarded and Agent Framework will re-plan against the current host and workspace.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.EnvironmentDrift)
                return "Agent runtime environment changed (workspace, active document, shell, time zone, or Git state). Persisted internal task state was discarded and Agent Framework will re-plan from visible conversation history in the current environment.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.HookSurfaceSnapshotMissing)
                return "Persisted Agent session predates tool-hook surface tracking; its internal task state was discarded and Agent Framework will re-plan under the current authorization hooks.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.HookSurfaceDrift)
                return "Agent tool-hook surface changed. Persisted internal task state was discarded and Agent Framework will re-plan before any further tool authorization.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.ProjectInstructionSnapshotMissing)
                return "Persisted Agent session predates project-instruction tracking; its internal task state was discarded and Agent Framework will re-plan under the current personal and project instructions.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.ProjectInstructionDrift)
                return "Personal or project instructions changed. Persisted internal task state was discarded and Agent Framework will re-plan under the current instruction documents.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome)
                return "The latest Agent run contains a started tool call without an authoritative terminal outcome. The persisted provider session was discarded so it cannot replay that call; Agent Framework will re-plan from bounded conversation and attempted-tool evidence.";
            if (compatibility.Kind == CopilotAgentCheckpointCompatibilityKind.UnresolvedProviderToolCall)
                return "The latest Agent run contains a provider-persisted tool request without a matching provider-persisted result. The provider session was discarded so it cannot resume with a dangling tool call; Agent Framework will re-plan from bounded conversation and attempted-tool evidence.";

            var removed = compatibility.RemovedCapabilityIds.Count;
            var changed = compatibility.ChangedCapabilityIds.Count;
            return $"Agent capability drift detected · catalog revision {compatibility.PreviousCatalogRevision} -> {compatibility.CurrentCatalogRevision}"
                + $" · {removed} removed · {changed} changed. Persisted task plan was discarded and Agent Framework will re-plan against current tools.";
        }

        private static IReadOnlyList<CopilotRequestMessage> InsertEvidenceMessageBeforeCurrentUser(
            IReadOnlyList<CopilotRequestMessage> messages,
            string content)
        {
            var recoveryMessage = new CopilotRequestMessage("user", content);
            if (messages.Count == 0)
                return [recoveryMessage];

            return messages.Take(messages.Count - 1)
                .Append(recoveryMessage)
                .Append(messages[^1])
                .ToArray();
        }

        private static string[] CreateDeferredBackgroundOutputMessages(
            IReadOnlyList<CopilotDeferredBackgroundShellOutputEvent> events,
            string conversationId)
        {
            return events
                .Select(deferredEvent =>
                    CopilotBackgroundShellCommandAgentEvent
                        .TryCreateDeferredOutputMessage(
                            deferredEvent,
                            conversationId,
                            out var message)
                        ? message
                        : string.Empty)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
        }

        private static string[] CreateDeferredBackgroundCompletionMessages(
            IReadOnlyList<CopilotDeferredBackgroundShellCompletion> completions,
            string conversationId)
        {
            return completions
                .Select(completion =>
                    CopilotBackgroundShellCommandAgentEvent.TryCreateMessage(
                        completion.Snapshot,
                        conversationId,
                        out var message)
                        ? message
                        : string.Empty)
                .Where(message => !string.IsNullOrWhiteSpace(message))
                .ToArray();
        }

        private static string SanitizeTaskTitle(string title)
        {
            var sanitized = Regex.Replace(title ?? string.Empty, @"\s+", " ").Trim();
            return sanitized.Length <= 60 ? sanitized : sanitized[..57] + "...";
        }

        private static string FormatDuration(TimeSpan duration)
        {
            if (duration.TotalSeconds < 1)
                return $"{Math.Max(1, duration.TotalMilliseconds):0}ms";
            if (duration.TotalMinutes < 1)
                return $"{duration.TotalSeconds:0.#}s";
            return $"{(int)duration.TotalMinutes}m {duration.Seconds}s";
        }
    }
}
