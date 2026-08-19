using System;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotAgentEventProtocol
    {
        [Flags]
        private enum PayloadKind
        {
            None = 0,
            Text = 1 << 0,
            ToolResult = 1 << 1,
            ToolExecution = 1 << 2,
            ToolExecutionHookRuns = 1 << 3,
            ToolExecutionHook = 1 << 4,
            Progress = 1 << 5,
            Budget = 1 << 6,
            SessionCheckpoint = 1 << 7,
            TaskLedger = 1 << 8,
            TurnPlan = 1 << 9,
            UserQuestion = 1 << 10,
            SteeringMessages = 1 << 11,
            ProviderRetry = 1 << 12,
            ProviderConnectionRecovery = 1 << 13,
            ModelToolResult = 1 << 14,
        }

        public static void Validate(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (!Enum.IsDefined(agentEvent.Type))
                throw new InvalidOperationException("Copilot Agent emitted an unsupported event type.");
            if (agentEvent.Text == null
                || agentEvent.ModelToolResult == null
                || agentEvent.ToolExecutionHookRuns == null
                || agentEvent.SteeringMessages == null)
            {
                throw new InvalidOperationException("Copilot Agent event contains a null protocol collection or text value.");
            }

            var actual = GetPayloadKind(agentEvent);
            var (required, allowed) = GetPayloadContract(agentEvent.Type);
            if ((actual & required) != required || (actual & ~allowed) != PayloadKind.None)
            {
                throw new InvalidOperationException(
                    $"Copilot Agent {agentEvent.Type} event has an invalid payload shape.");
            }

            ValidateText(agentEvent);
            if (agentEvent.ModelToolResult.Length > CopilotCodeReviewSnapshot.MaximumModelObservationCharacters)
                throw new InvalidOperationException("Copilot Agent model tool result exceeds its protocol limit.");
            ValidateCorrelatedPayload(agentEvent);
        }

        private static PayloadKind GetPayloadKind(CopilotAgentEvent agentEvent)
        {
            var payload = PayloadKind.None;
            if (agentEvent.Text.Length > 0)
                payload |= PayloadKind.Text;
            if (agentEvent.ToolResult != null)
                payload |= PayloadKind.ToolResult;
            if (agentEvent.ToolExecution != null)
                payload |= PayloadKind.ToolExecution;
            if (agentEvent.ToolExecutionHookRuns.Count > 0)
                payload |= PayloadKind.ToolExecutionHookRuns;
            if (agentEvent.ToolExecutionHook != null)
                payload |= PayloadKind.ToolExecutionHook;
            if (agentEvent.Progress != null)
                payload |= PayloadKind.Progress;
            if (agentEvent.Budget != null)
                payload |= PayloadKind.Budget;
            if (agentEvent.SessionCheckpoint != null)
                payload |= PayloadKind.SessionCheckpoint;
            if (agentEvent.TaskLedger != null)
                payload |= PayloadKind.TaskLedger;
            if (agentEvent.TurnPlan != null)
                payload |= PayloadKind.TurnPlan;
            if (agentEvent.UserQuestion != null)
                payload |= PayloadKind.UserQuestion;
            if (agentEvent.SteeringMessages.Count > 0)
                payload |= PayloadKind.SteeringMessages;
            if (agentEvent.ProviderRetry != null)
                payload |= PayloadKind.ProviderRetry;
            if (agentEvent.ProviderConnectionRecovery != null)
                payload |= PayloadKind.ProviderConnectionRecovery;
            if (agentEvent.ModelToolResult.Length > 0)
                payload |= PayloadKind.ModelToolResult;
            return payload;
        }

        private static (PayloadKind Required, PayloadKind Allowed) GetPayloadContract(
            CopilotAgentEventType type)
        {
            var text = PayloadKind.Text;
            return type switch
            {
                CopilotAgentEventType.Status => (text, text),
                CopilotAgentEventType.RuntimeDiagnostic =>
                    (text, text | PayloadKind.ProviderRetry | PayloadKind.ProviderConnectionRecovery),
                CopilotAgentEventType.BudgetUpdated =>
                    (PayloadKind.Budget, PayloadKind.Budget),
                CopilotAgentEventType.ToolStarted =>
                    (text | PayloadKind.ToolExecution, text | PayloadKind.ToolExecution),
                CopilotAgentEventType.ToolProgress =>
                    (text | PayloadKind.ToolExecution,
                        text | PayloadKind.ToolExecution | PayloadKind.Progress),
                CopilotAgentEventType.HookStarted or CopilotAgentEventType.HookCompleted =>
                    (text | PayloadKind.ToolExecution | PayloadKind.ToolExecutionHook,
                        text | PayloadKind.ToolExecution | PayloadKind.ToolExecutionHook),
                CopilotAgentEventType.ToolResult =>
                    (PayloadKind.ToolResult | PayloadKind.ToolExecution,
                        text | PayloadKind.ToolResult | PayloadKind.ToolExecution
                            | PayloadKind.ToolExecutionHookRuns | PayloadKind.ModelToolResult),
                CopilotAgentEventType.ReasoningDelta or CopilotAgentEventType.AnswerDelta =>
                    (text, text),
                CopilotAgentEventType.AnswerReset
                    or CopilotAgentEventType.Completed
                    or CopilotAgentEventType.CheckpointReady =>
                    (PayloadKind.None, PayloadKind.None),
                CopilotAgentEventType.SteeringDelivered or CopilotAgentEventType.SteeringRecovery =>
                    (text | PayloadKind.SteeringMessages,
                        text | PayloadKind.SteeringMessages),
                CopilotAgentEventType.Error => (text, text),
                CopilotAgentEventType.CheckpointUpdated =>
                    (PayloadKind.SessionCheckpoint | PayloadKind.TaskLedger,
                        PayloadKind.SessionCheckpoint | PayloadKind.TaskLedger),
                CopilotAgentEventType.PlanUpdated =>
                    (PayloadKind.TurnPlan, PayloadKind.TurnPlan),
                CopilotAgentEventType.UserQuestionRequested
                    or CopilotAgentEventType.UserQuestionResolved =>
                    (PayloadKind.UserQuestion, PayloadKind.UserQuestion),
                _ => throw new InvalidOperationException("Copilot Agent emitted an unsupported event type."),
            };
        }

        private static void ValidateText(CopilotAgentEvent agentEvent)
        {
            if (agentEvent.Type is CopilotAgentEventType.ReasoningDelta
                or CopilotAgentEventType.AnswerDelta)
            {
                if (agentEvent.Text.Length == 0)
                    throw new InvalidOperationException("Copilot Agent emitted an empty stream delta.");
                return;
            }

            if (agentEvent.Type is CopilotAgentEventType.Status
                or CopilotAgentEventType.RuntimeDiagnostic
                or CopilotAgentEventType.ToolStarted
                or CopilotAgentEventType.ToolProgress
                or CopilotAgentEventType.HookStarted
                or CopilotAgentEventType.HookCompleted
                or CopilotAgentEventType.SteeringDelivered
                or CopilotAgentEventType.SteeringRecovery
                or CopilotAgentEventType.Error
                && string.IsNullOrWhiteSpace(agentEvent.Text))
            {
                throw new InvalidOperationException(
                    $"Copilot Agent {agentEvent.Type} event has empty display text.");
            }
        }

        private static void ValidateCorrelatedPayload(CopilotAgentEvent agentEvent)
        {
            if (agentEvent.ProviderRetry != null
                && agentEvent.ProviderConnectionRecovery != null)
            {
                throw new InvalidOperationException(
                    "Copilot Agent runtime diagnostic cannot describe both a bounded retry and connection recovery.");
            }

            switch (agentEvent.Type)
            {
                case CopilotAgentEventType.ToolStarted:
                    RequireMatchingText(agentEvent, agentEvent.ToolExecution!.ToolName, "tool start");
                    break;
                case CopilotAgentEventType.ToolResult:
                    RequireMatchingText(agentEvent, agentEvent.ToolResult!.Summary, "tool result");
                    ValidateToolResult(agentEvent);
                    break;
                case CopilotAgentEventType.HookStarted:
                case CopilotAgentEventType.HookCompleted:
                    RequireMatchingText(agentEvent, agentEvent.ToolExecutionHook!.SourceId, "tool hook");
                    break;
                case CopilotAgentEventType.RuntimeDiagnostic when agentEvent.ProviderRetry != null:
                    CopilotProviderRetryProtocol.ValidateDiagnostic(
                        agentEvent.ProviderRetry,
                        agentEvent.Text);
                    break;
                case CopilotAgentEventType.RuntimeDiagnostic when agentEvent.ProviderConnectionRecovery != null:
                    CopilotProviderConnectionRecoveryProtocol.ValidateDiagnostic(
                        agentEvent.ProviderConnectionRecovery,
                        agentEvent.Text);
                    break;
                case CopilotAgentEventType.SteeringDelivered:
                case CopilotAgentEventType.SteeringRecovery:
                    ValidateSteering(agentEvent);
                    break;
            }
        }

        private static void ValidateToolResult(CopilotAgentEvent agentEvent)
        {
            var result = agentEvent.ToolResult!;
            var execution = agentEvent.ToolExecution!;
            if (!CopilotToolExecutionInfoProtocol.IsStructurallyValid(execution))
            {
                throw new InvalidOperationException(
                    "Copilot Agent tool result has invalid execution metadata.");
            }

            if (string.IsNullOrWhiteSpace(result.ToolName)
                || !string.Equals(
                    result.ToolName,
                    execution.ToolName,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot Agent tool result identity did not match its execution payload.");
            }

            try
            {
                if (!CopilotToolResultContract.TryValidate(
                        execution.ToolName,
                        result,
                        out var violation))
                {
                    throw new InvalidOperationException(
                        $"Copilot Agent tool result violated its final contract: {violation}.");
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch
            {
                throw new InvalidOperationException(
                    "Copilot Agent tool result could not be validated safely.");
            }

            if (result.Approval != null
                && !string.Equals(
                    result.Approval.ActionId,
                    execution.ApprovalActionId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Copilot Agent approval result did not match its execution action identity.");
            }

            if (!CopilotToolExecutionInfoProtocol.HasValidResultState(
                    execution,
                    result))
            {
                throw new InvalidOperationException(
                    "Copilot Agent tool result contains invalid state metadata for its terminal execution.");
            }

            if (agentEvent.ToolExecutionHookRuns.Any(run =>
                    run?.IsStructurallyValid() != true))
            {
                throw new InvalidOperationException(
                    "Copilot Agent tool result contains an invalid hook audit entry.");
            }
        }

        private static void RequireMatchingText(
            CopilotAgentEvent agentEvent,
            string expected,
            string payloadName)
        {
            if (!string.Equals(agentEvent.Text, expected, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"Copilot Agent {payloadName} display text did not match its structured payload.");
            }
        }

        private static void ValidateSteering(CopilotAgentEvent agentEvent)
        {
            var messages = agentEvent.SteeringMessages;
            var normalized = CopilotSteeringMessagePolicy.SelectForRecovery(messages);
            if (normalized.Count != messages.Count)
            {
                throw new InvalidOperationException(
                    "Copilot Agent steering event contains an invalid or duplicate message.");
            }
            for (var index = 0; index < messages.Count; index++)
            {
                if (!string.Equals(
                        messages[index]?.MessageId,
                        normalized[index].MessageId,
                        StringComparison.Ordinal)
                    || !string.Equals(
                        messages[index]?.Text,
                        normalized[index].Text,
                        StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "Copilot Agent steering event contains an invalid or unnormalized message.");
                }
            }

            var expectedText = agentEvent.Type == CopilotAgentEventType.SteeringDelivered
                ? $"Agent provider acknowledged {messages.Count} queued user steering instruction(s)."
                : $"Agent stopped before delivering {messages.Count} queued user steering instruction(s); the input was returned to the conversation draft.";
            RequireMatchingText(agentEvent, expectedText, "steering event");
        }
    }
}
