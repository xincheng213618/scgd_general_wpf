using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnToolLifecycleState
    {
        private readonly IReadOnlyDictionary<string, ToolCallSnapshot> _calls;

        private CopilotTurnToolLifecycleState(IReadOnlyDictionary<string, ToolCallSnapshot> calls)
        {
            _calls = calls;
        }

        public static CopilotTurnToolLifecycleState Empty { get; } = new(
            new Dictionary<string, ToolCallSnapshot>(StringComparer.Ordinal));

        public CopilotTurnToolLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            return agentEvent.Type switch
            {
                CopilotAgentEventType.ToolStarted => ObserveStarted(agentEvent),
                CopilotAgentEventType.ToolProgress => ObserveProgress(agentEvent),
                CopilotAgentEventType.ToolResult => ObserveResult(agentEvent),
                CopilotAgentEventType.Completed => ObserveAgentCompleted(),
                _ => this,
            };
        }

        private CopilotTurnToolLifecycleState ObserveStarted(CopilotAgentEvent agentEvent)
        {
            var execution = RequireRunningExecution(
                agentEvent,
                "start");
            var callKey = BuildCallKey(execution);
            if (!_calls.TryGetValue(callKey, out var current))
                return Store(ToolCallSnapshot.Create(execution, ToolCallStage.Running));

            RequireMatchingExecution(current, execution, allowApprovalBinding: false);
            if (current.Stage == ToolCallStage.Running)
                throw new InvalidOperationException("Copilot Agent started the same tool call more than once.");
            if (current.Stage == ToolCallStage.AwaitingDeferredApproval)
                throw new InvalidOperationException("Copilot Agent resumed a deferred approval inside the completed tool item.");
            if (current.Stage == ToolCallStage.Terminal)
                throw new InvalidOperationException("Copilot Agent started a tool call after its terminal result.");

            return Store(current.Advance(execution, ToolCallStage.Running));
        }

        private CopilotTurnToolLifecycleState ObserveProgress(CopilotAgentEvent agentEvent)
        {
            var execution = RequireProgressExecution(agentEvent);
            RequireProgressPayload(agentEvent);
            var callKey = BuildCallKey(execution);
            if (!_calls.TryGetValue(callKey, out var current))
            {
                if (execution.State == CopilotToolExecutionState.Running)
                    throw new InvalidOperationException("Copilot Agent emitted running tool progress before the tool started.");
                return Store(ToolCallSnapshot.Create(execution, ToolCallStage.Queued));
            }

            RequireMatchingExecution(current, execution, allowApprovalBinding: false);
            if (current.Stage == ToolCallStage.Terminal)
                throw new InvalidOperationException("Copilot Agent emitted tool progress after the terminal result.");
            if (current.Stage is ToolCallStage.AwaitingTurnApproval or ToolCallStage.AwaitingDeferredApproval)
                throw new InvalidOperationException("Copilot Agent emitted tool progress while approval was pending.");
            if (execution.State == CopilotToolExecutionState.Pending
                && current.Stage != ToolCallStage.Queued)
            {
                throw new InvalidOperationException("Copilot Agent moved a running tool call back to pending progress.");
            }
            if (execution.State == CopilotToolExecutionState.Running
                && current.Stage != ToolCallStage.Running)
            {
                throw new InvalidOperationException("Copilot Agent emitted running tool progress before the tool started.");
            }
            if (execution.DurationMs < current.MaximumObservedDurationMs
                || execution.QueueDurationMs < current.MaximumObservedQueueDurationMs)
            {
                throw new InvalidOperationException("Copilot Agent tool progress moved backwards.");
            }

            return Store(current.Advance(
                execution,
                execution.State == CopilotToolExecutionState.Pending
                    ? ToolCallStage.Queued
                    : ToolCallStage.Running));
        }

        private CopilotTurnToolLifecycleState ObserveResult(CopilotAgentEvent agentEvent)
        {
            var execution = RequireResult(agentEvent);
            var callKey = BuildCallKey(execution);
            _calls.TryGetValue(callKey, out var current);
            if (current != null)
            {
                if (current.Stage == ToolCallStage.Terminal)
                    throw new InvalidOperationException("Copilot Agent emitted more than one terminal result for a tool call.");
                if (current.Stage is ToolCallStage.AwaitingTurnApproval or ToolCallStage.AwaitingDeferredApproval
                    && execution.State == CopilotToolExecutionState.AwaitingApproval)
                {
                    throw new InvalidOperationException("Copilot Agent emitted the same approval result more than once.");
                }

                RequireMatchingExecution(
                    current,
                    execution,
                    allowApprovalBinding: execution.State == CopilotToolExecutionState.AwaitingApproval);
                if (execution.DurationMs < current.MaximumObservedDurationMs
                    || execution.QueueDurationMs < current.MaximumObservedQueueDurationMs)
                {
                    throw new InvalidOperationException("Copilot Agent tool result moved backwards from observed progress.");
                }
            }

            var stage = execution.State == CopilotToolExecutionState.AwaitingApproval
                ? agentEvent.ToolResult!.Approval!.ResumesAgentOnApproval
                    ? ToolCallStage.AwaitingTurnApproval
                    : ToolCallStage.AwaitingDeferredApproval
                : ToolCallStage.Terminal;
            return Store(current == null
                ? ToolCallSnapshot.Create(execution, stage)
                : current.Advance(execution, stage));
        }

        private CopilotTurnToolLifecycleState ObserveAgentCompleted()
        {
            foreach (var call in _calls.Values)
            {
                if (call.Stage is ToolCallStage.Queued
                    or ToolCallStage.Running
                    or ToolCallStage.AwaitingTurnApproval)
                {
                    throw new InvalidOperationException("Copilot Agent completed while a tool item was still active.");
                }
            }

            return this;
        }

        private CopilotTurnToolLifecycleState Store(ToolCallSnapshot snapshot)
        {
            var calls = new Dictionary<string, ToolCallSnapshot>(_calls, StringComparer.Ordinal)
            {
                [snapshot.CallKey] = snapshot,
            };
            return new CopilotTurnToolLifecycleState(calls);
        }

        private static CopilotToolExecutionInfo RequireRunningExecution(
            CopilotAgentEvent agentEvent,
            string lifecycleStage)
        {
            var execution = agentEvent.ToolExecution;
            if (!CopilotToolExecutionInfoProtocol.HasValidActiveState(
                    execution,
                    allowPending: false))
            {
                throw new InvalidOperationException(
                    $"Copilot Agent tool {lifecycleStage} has invalid execution metadata.");
            }

            return execution;
        }

        private static CopilotToolExecutionInfo RequireProgressExecution(CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution;
            if (!CopilotToolExecutionInfoProtocol.HasValidActiveState(
                    execution,
                    allowPending: true))
            {
                throw new InvalidOperationException("Copilot Agent tool progress has invalid execution metadata.");
            }

            return execution;
        }

        private static CopilotToolExecutionInfo RequireResult(CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution;
            var result = agentEvent.ToolResult;
            if (!HasValidExecution(execution)
                || result == null
                || !string.Equals(result.ToolName, execution!.ToolName, StringComparison.Ordinal)
                || execution.State is CopilotToolExecutionState.Pending or CopilotToolExecutionState.Running)
            {
                throw new InvalidOperationException("Copilot Agent tool result has invalid execution metadata.");
            }

            if (!CopilotToolExecutionInfoProtocol.HasValidResultState(
                    execution,
                    result))
            {
                throw new InvalidOperationException(
                    execution.State == CopilotToolExecutionState.AwaitingApproval
                        ? "Copilot Agent approval result has invalid action metadata."
                        : "Copilot Agent terminal tool result has invalid state metadata.");
            }

            return execution;
        }

        private static void RequireProgressPayload(CopilotAgentEvent agentEvent)
        {
            if (string.IsNullOrWhiteSpace(agentEvent.Text)
                || !CopilotToolProgressProtocol.IsStructurallyValid(
                    agentEvent.Progress))
            {
                throw new InvalidOperationException("Copilot Agent emitted an invalid tool progress payload.");
            }
        }

        private static bool HasValidExecution(
            CopilotToolExecutionInfo? execution) =>
            CopilotToolExecutionInfoProtocol.IsStructurallyValid(execution);

        private static void RequireMatchingExecution(
            ToolCallSnapshot expected,
            CopilotToolExecutionInfo actual,
            bool allowApprovalBinding)
        {
            var approvalMatches = string.Equals(
                expected.ApprovalActionId,
                actual.ApprovalActionId,
                StringComparison.Ordinal);
            if (allowApprovalBinding && expected.ApprovalActionId.Length == 0)
                approvalMatches = !string.IsNullOrWhiteSpace(actual.ApprovalActionId);

            if (!approvalMatches
                || !string.Equals(expected.CallId, actual.CallId, StringComparison.Ordinal)
                || expected.Round != actual.Round
                || expected.Attempt != actual.Attempt
                || expected.MaxAttempts != actual.MaxAttempts
                || !string.Equals(expected.RuntimeName, actual.RuntimeName, StringComparison.Ordinal)
                || !string.Equals(expected.ToolName, actual.ToolName, StringComparison.Ordinal)
                || expected.Access != actual.Access
                || expected.RiskLevel != actual.RiskLevel
                || expected.ApprovalMode != actual.ApprovalMode
                || expected.Idempotency != actual.Idempotency
                || expected.ConcurrencyMode != actual.ConcurrencyMode
                || !string.Equals(expected.ConcurrencyKey, actual.ConcurrencyKey, StringComparison.Ordinal)
                || !string.Equals(expected.ArgumentSummary, actual.ArgumentSummary, StringComparison.Ordinal)
                || expected.TimeoutMs != actual.TimeoutMs)
            {
                throw new InvalidOperationException("Copilot Agent tool event did not match the active tool call.");
            }
        }

        private static string BuildCallKey(CopilotToolExecutionInfo execution) =>
            $"{execution.CallId}\u001f{execution.Attempt}\u001f{execution.ToolName}";

        private enum ToolCallStage
        {
            Queued,
            Running,
            AwaitingTurnApproval,
            AwaitingDeferredApproval,
            Terminal,
        }

        private sealed record ToolCallSnapshot(
            string CallKey,
            string CallId,
            int Round,
            int Attempt,
            int MaxAttempts,
            string RuntimeName,
            string ToolName,
            CopilotToolAccess Access,
            CopilotToolRiskLevel RiskLevel,
            CopilotToolApprovalMode ApprovalMode,
            CopilotToolIdempotency Idempotency,
            CopilotToolConcurrencyMode ConcurrencyMode,
            string ConcurrencyKey,
            string ApprovalActionId,
            string ArgumentSummary,
            long TimeoutMs,
            long MaximumObservedDurationMs,
            long MaximumObservedQueueDurationMs,
            ToolCallStage Stage)
        {
            public static ToolCallSnapshot Create(
                CopilotToolExecutionInfo execution,
                ToolCallStage stage) => new(
                    BuildCallKey(execution),
                    execution.CallId,
                    execution.Round,
                    execution.Attempt,
                    execution.MaxAttempts,
                    execution.RuntimeName,
                    execution.ToolName,
                    execution.Access,
                    execution.RiskLevel,
                    execution.ApprovalMode,
                    execution.Idempotency,
                    execution.ConcurrencyMode,
                    execution.ConcurrencyKey,
                    execution.ApprovalActionId,
                    execution.ArgumentSummary,
                    execution.TimeoutMs,
                    execution.DurationMs,
                    execution.QueueDurationMs,
                    stage);

            public ToolCallSnapshot Advance(
                CopilotToolExecutionInfo execution,
                ToolCallStage stage) => this with
                {
                    ApprovalActionId = execution.ApprovalActionId,
                    MaximumObservedDurationMs = Math.Max(
                        MaximumObservedDurationMs,
                        execution.DurationMs),
                    MaximumObservedQueueDurationMs = Math.Max(
                        MaximumObservedQueueDurationMs,
                        execution.QueueDurationMs),
                    Stage = stage,
                };
        }
    }
}
