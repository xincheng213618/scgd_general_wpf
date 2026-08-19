using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    public enum CopilotAgentEventType
    {
        Status,
        RuntimeDiagnostic,
        BudgetUpdated,
        ToolStarted,
        ToolProgress,
        HookStarted,
        HookCompleted,
        ToolResult,
        ReasoningDelta,
        AnswerDelta,
        AnswerReset,
        SteeringDelivered,
        SteeringRecovery,
        Error,
        Completed,
        CheckpointReady,
        CheckpointUpdated,
        PlanUpdated,
        UserQuestionRequested,
        UserQuestionResolved,
    }

    public sealed class CopilotAgentEvent
    {
        public CopilotAgentEventType Type { get; init; }

        public string Text { get; init; } = string.Empty;

        public CopilotToolResult? ToolResult { get; init; }

        public CopilotToolExecutionInfo? ToolExecution { get; init; }

        internal string ModelToolResult { get; init; } = string.Empty;

        public IReadOnlyList<CopilotToolExecutionHookRun> ToolExecutionHookRuns { get; init; } =
            Array.Empty<CopilotToolExecutionHookRun>();

        public CopilotToolExecutionHookLifecycle? ToolExecutionHook { get; init; }

        public CopilotToolProgressUpdate? Progress { get; init; }

        public CopilotAgentBudgetSnapshot? Budget { get; init; }

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }

        public CopilotAgentTaskLedgerSnapshot? TaskLedger { get; init; }

        internal CopilotTurnPlanSnapshot? TurnPlan { get; init; }

        public CopilotUserQuestionSnapshot? UserQuestion { get; init; }

        public IReadOnlyList<CopilotSteeringMessageSnapshot> SteeringMessages { get; init; } =
            Array.Empty<CopilotSteeringMessageSnapshot>();

        internal CopilotProviderRetryInfo? ProviderRetry { get; init; }

        internal CopilotProviderConnectionRecoveryInfo? ProviderConnectionRecovery { get; init; }

        public static CopilotAgentEvent Status(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Status,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent ToolStarted(CopilotToolExecutionInfo execution)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolStarted,
                Text = execution?.ToolName ?? string.Empty,
                ToolExecution = execution,
            };
        }

        public static CopilotAgentEvent ToolProgress(
            CopilotToolExecutionInfo execution,
            string text,
            CopilotToolProgressUpdate? progress = null)
        {
            ArgumentNullException.ThrowIfNull(execution);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolProgress,
                Text = text ?? string.Empty,
                ToolExecution = execution,
                Progress = progress,
            };
        }

        public static CopilotAgentEvent RuntimeDiagnostic(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.RuntimeDiagnostic,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent BudgetUpdated(CopilotAgentBudgetSnapshot budget)
        {
            ArgumentNullException.ThrowIfNull(budget);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.BudgetUpdated,
                Budget = budget,
            };
        }

        internal static CopilotAgentEvent FromProviderRetry(CopilotProviderRetryInfo retry)
        {
            ArgumentNullException.ThrowIfNull(retry);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.RuntimeDiagnostic,
                Text = retry.ToDiagnosticText(),
                ProviderRetry = retry,
            };
        }

        internal static CopilotAgentEvent FromProviderConnectionRecovery(
            CopilotProviderConnectionRecoveryInfo recovery)
        {
            ArgumentNullException.ThrowIfNull(recovery);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.RuntimeDiagnostic,
                Text = recovery.ToDiagnosticText(),
                ProviderConnectionRecovery = recovery,
            };
        }

        public static CopilotAgentEvent FromToolResult(
            CopilotToolResult result,
            CopilotToolExecutionInfo? execution = null,
            IReadOnlyList<CopilotToolExecutionHookRun>? hookRuns = null)
        {
            ArgumentNullException.ThrowIfNull(result);
            return FromToolResult(
                CopilotToolResultContract.CreateSnapshot(result),
                execution,
                hookRuns,
                string.Empty);
        }

        internal static CopilotAgentEvent FromToolResult(
            CopilotToolResult result,
            CopilotToolExecutionInfo? execution,
            IReadOnlyList<CopilotToolExecutionHookRun>? hookRuns,
            string? modelToolResult)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolResult,
                Text = result?.Summary ?? string.Empty,
                ToolResult = result,
                ToolExecution = execution,
                ToolExecutionHookRuns = hookRuns == null || hookRuns.Count == 0
                    ? Array.Empty<CopilotToolExecutionHookRun>()
                    : Array.AsReadOnly(hookRuns.ToArray()),
                ModelToolResult = modelToolResult ?? string.Empty,
            };
        }

        public static CopilotAgentEvent HookStarted(
            CopilotToolExecutionInfo execution,
            string sourceId,
            CopilotToolExecutionHookPhase phase)
        {
            ArgumentNullException.ThrowIfNull(execution);
            var hook = CopilotToolExecutionHookLifecycle.Started(sourceId, phase);
            if (!hook.IsStructurallyValid(requireCompleted: false))
                throw new ArgumentException("The tool hook start is not structurally valid.", nameof(sourceId));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.HookStarted,
                Text = hook.SourceId,
                ToolExecution = execution,
                ToolExecutionHook = hook,
            };
        }

        public static CopilotAgentEvent HookCompleted(
            CopilotToolExecutionInfo execution,
            CopilotToolExecutionHookRun result)
        {
            ArgumentNullException.ThrowIfNull(execution);
            var hook = CopilotToolExecutionHookLifecycle.Completed(result);
            if (!hook.IsStructurallyValid(requireCompleted: true))
                throw new ArgumentException("The completed tool hook is not structurally valid.", nameof(result));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.HookCompleted,
                Text = hook.SourceId,
                ToolExecution = execution,
                ToolExecutionHook = hook,
            };
        }

        public static CopilotAgentEvent ReasoningDelta(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ReasoningDelta,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent AnswerDelta(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.AnswerDelta,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent Error(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Error,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent Completed()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Completed,
            };
        }

        public static CopilotAgentEvent AnswerReset()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.AnswerReset,
            };
        }

        public static CopilotAgentEvent CheckpointReady()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.CheckpointReady,
            };
        }

        public static CopilotAgentEvent CheckpointUpdated(
            CopilotAgentSessionCheckpoint sessionCheckpoint,
            CopilotAgentTaskLedgerSnapshot taskLedger)
        {
            ArgumentNullException.ThrowIfNull(sessionCheckpoint);
            ArgumentNullException.ThrowIfNull(taskLedger);
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.CheckpointUpdated,
                SessionCheckpoint = sessionCheckpoint,
                TaskLedger = CopilotAgentTaskLedgerSnapshot.CreateSnapshot(
                    taskLedger,
                    normalize: false),
            };
        }

        internal static CopilotAgentEvent PlanUpdated(CopilotTurnPlanSnapshot plan)
        {
            ArgumentNullException.ThrowIfNull(plan);
            if (!plan.IsStructurallyValid())
                throw new ArgumentException("Turn plan snapshot is invalid.", nameof(plan));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.PlanUpdated,
                TurnPlan = plan,
            };
        }

        public static CopilotAgentEvent SteeringDelivered(IEnumerable<CopilotSteeringMessageSnapshot> messages)
        {
            var deliveredMessages = CreateSteeringMessages(messages, nameof(messages));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.SteeringDelivered,
                Text = $"Agent provider acknowledged {deliveredMessages.Count} queued user steering instruction(s).",
                SteeringMessages = deliveredMessages,
            };
        }

        public static CopilotAgentEvent SteeringRecovery(IEnumerable<CopilotSteeringMessageSnapshot> messages)
        {
            var recoveryMessages = CreateSteeringMessages(messages, nameof(messages));

            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.SteeringRecovery,
                Text = $"Agent stopped before delivering {recoveryMessages.Count} queued user steering instruction(s); the input was returned to the conversation draft.",
                SteeringMessages = recoveryMessages,
            };
        }

        private static IReadOnlyList<CopilotSteeringMessageSnapshot> CreateSteeringMessages(
            IEnumerable<CopilotSteeringMessageSnapshot> messages,
            string parameterName)
        {
            ArgumentNullException.ThrowIfNull(messages);
            var recoveryMessages = CopilotSteeringMessagePolicy.SelectForRecovery(messages);
            if (recoveryMessages.Count == 0)
                throw new ArgumentException("Steering events require at least one bounded message.", parameterName);
            return recoveryMessages;
        }

        public static CopilotAgentEvent UserQuestionRequested(CopilotUserQuestionSnapshot question)
        {
            ArgumentNullException.ThrowIfNull(question);
            if (!question.IsPending
                || !CopilotUserQuestionSnapshot.TryCreateSnapshot(question, out var snapshot))
                throw new ArgumentException("The user question request is not structurally valid.", nameof(question));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.UserQuestionRequested,
                UserQuestion = snapshot,
            };
        }

        public static CopilotAgentEvent UserQuestionResolved(CopilotUserQuestionSnapshot question)
        {
            ArgumentNullException.ThrowIfNull(question);
            if (question.IsPending
                || !CopilotUserQuestionSnapshot.TryCreateSnapshot(question, out var snapshot))
                throw new ArgumentException("The resolved user question is not structurally valid.", nameof(question));
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.UserQuestionResolved,
                UserQuestion = snapshot,
            };
        }
    }

    internal static class CopilotSteeringMessagePolicy
    {
        internal const int MaximumMessageCharacters = 16_000;
        internal const int MaximumIdentifierCharacters = 128;
        internal const int MaximumPendingMessages = 8;
        internal const int MaximumPendingCharacters = 32_000;

        internal static IReadOnlyList<string> SelectForRecovery(IEnumerable<string>? messages)
        {
            var selected = new List<string>(MaximumPendingMessages);
            var characterCount = 0;
            foreach (var message in messages ?? Array.Empty<string>())
            {
                var normalized = (message ?? string.Empty).Trim();
                if (normalized.Length == 0 || normalized.Length > MaximumMessageCharacters)
                    continue;
                if (selected.Count >= MaximumPendingMessages
                    || characterCount + normalized.Length > MaximumPendingCharacters)
                {
                    break;
                }

                selected.Add(normalized);
                characterCount += normalized.Length;
            }
            return selected.Count == 0
                ? Array.Empty<string>()
                : Array.AsReadOnly(selected.ToArray());
        }

        internal static IReadOnlyList<CopilotSteeringMessageSnapshot> SelectForRecovery(
            IEnumerable<CopilotSteeringMessageSnapshot>? messages)
        {
            var selected = new List<CopilotSteeringMessageSnapshot>(MaximumPendingMessages);
            var seenMessageIds = new HashSet<string>(StringComparer.Ordinal);
            var characterCount = 0;
            foreach (var message in messages ?? Array.Empty<CopilotSteeringMessageSnapshot>())
            {
                var messageId = (message?.MessageId ?? string.Empty).Trim();
                var text = (message?.Text ?? string.Empty).Trim();
                if (messageId.Length is 0 or > MaximumIdentifierCharacters
                    || text.Length is 0 or > MaximumMessageCharacters
                    || !seenMessageIds.Add(messageId))
                {
                    continue;
                }
                if (selected.Count >= MaximumPendingMessages
                    || characterCount + text.Length > MaximumPendingCharacters)
                {
                    break;
                }

                selected.Add(new CopilotSteeringMessageSnapshot(messageId, text));
                characterCount += text.Length;
            }
            return selected.Count == 0
                ? Array.Empty<CopilotSteeringMessageSnapshot>()
                : Array.AsReadOnly(selected.ToArray());
        }
    }

}
