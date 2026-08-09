using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnApprovalLifecycleState
    {
        private readonly IReadOnlyDictionary<string, ApprovalRequestSnapshot> _pendingByCall;
        private readonly IReadOnlyDictionary<string, ApprovalRequestSnapshot> _resolvedByAction;

        private CopilotTurnApprovalLifecycleState(
            IReadOnlyDictionary<string, ApprovalRequestSnapshot> pendingByCall,
            IReadOnlyDictionary<string, ApprovalRequestSnapshot> resolvedByAction)
        {
            _pendingByCall = pendingByCall;
            _resolvedByAction = resolvedByAction;
        }

        public static CopilotTurnApprovalLifecycleState Empty { get; } = new(
            new Dictionary<string, ApprovalRequestSnapshot>(StringComparer.Ordinal),
            new Dictionary<string, ApprovalRequestSnapshot>(StringComparer.Ordinal));

        public CopilotTurnApprovalLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            return agentEvent.Type switch
            {
                CopilotAgentEventType.ToolStarted => ObserveToolStarted(agentEvent),
                CopilotAgentEventType.ToolResult => ObserveToolResult(agentEvent),
                CopilotAgentEventType.Completed => ObserveAgentCompleted(),
                _ => this,
            };
        }

        private CopilotTurnApprovalLifecycleState ObserveToolStarted(CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution;
            if (execution == null || string.IsNullOrWhiteSpace(execution.ApprovalActionId))
                return this;

            RequireExecution(execution, CopilotToolExecutionState.Running, "continuation");
            var callKey = BuildCallKey(execution);
            if (!_pendingByCall.TryGetValue(callKey, out var pending))
            {
                if (_resolvedByAction.ContainsKey(execution.ApprovalActionId))
                    throw new InvalidOperationException("Copilot Agent started an approved tool call more than once.");
                if (ContainsPendingAction(execution.ApprovalActionId))
                    throw new InvalidOperationException("Copilot Agent continued an approval request with a different tool call.");
                throw new InvalidOperationException("Copilot Agent continued an approval request that was never emitted.");
            }

            RequireMatchingExecution(pending, execution);
            return Resolve(pending);
        }

        private CopilotTurnApprovalLifecycleState ObserveToolResult(CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution;
            if (execution?.State == CopilotToolExecutionState.AwaitingApproval)
                return ObserveRequested(agentEvent, execution);
            if (execution == null)
                return this;

            var hasApprovalAction = !string.IsNullOrWhiteSpace(execution.ApprovalActionId);
            var hasValidCallIdentity = HasValidCallIdentity(execution);
            var callKey = hasValidCallIdentity ? BuildCallKey(execution) : string.Empty;
            if (callKey.Length > 0 && _pendingByCall.TryGetValue(callKey, out var pending))
            {
                RequireTerminalExecution(execution);
                RequireTerminalResult(agentEvent, execution);
                RequireMatchingExecution(pending, execution);
                return Resolve(pending);
            }
            if (!hasApprovalAction)
                return this;

            RequireTerminalExecution(execution);
            RequireTerminalResult(agentEvent, execution);

            if (_resolvedByAction.TryGetValue(execution.ApprovalActionId, out var resolved))
            {
                RequireMatchingExecution(resolved, execution);
                return this;
            }
            if (ContainsPendingAction(execution.ApprovalActionId))
                throw new InvalidOperationException("Copilot Agent resolved an approval request with a different tool call.");

            throw new InvalidOperationException("Copilot Agent resolved an approval request that was never emitted.");
        }

        private CopilotTurnApprovalLifecycleState ObserveRequested(
            CopilotAgentEvent agentEvent,
            CopilotToolExecutionInfo execution)
        {
            RequireExecution(execution, CopilotToolExecutionState.AwaitingApproval, "request");
            var result = agentEvent.ToolResult;
            var approval = result?.Approval;
            if (result == null
                || approval == null
                || string.IsNullOrWhiteSpace(approval.ActionId)
                || string.IsNullOrWhiteSpace(approval.Title)
                || string.IsNullOrWhiteSpace(approval.RiskLevel)
                || approval.ExpiresAtUtc == default
                || !string.Equals(result.ToolName, execution.ToolName, StringComparison.Ordinal)
                || !string.Equals(approval.ActionId, execution.ApprovalActionId, StringComparison.Ordinal)
                || (approval.ExecuteOnApproval && approval.ResumesAgentOnApproval))
            {
                throw new InvalidOperationException("Copilot Agent emitted an invalid approval request.");
            }

            if (!approval.ResumesAgentOnApproval)
                return this;
            if (!result.Success || approval.ExecuteOnApproval)
                throw new InvalidOperationException("Copilot Agent emitted an invalid turn-blocking approval request.");

            var snapshot = ApprovalRequestSnapshot.Create(execution);
            var callKey = BuildCallKey(execution);
            if (_pendingByCall.ContainsKey(callKey))
                throw new InvalidOperationException("Copilot Agent emitted the same approval request more than once.");
            if (ContainsPendingAction(snapshot.ActionId))
                throw new InvalidOperationException("Copilot Agent reused an active approval action ID.");
            if (_resolvedByAction.ContainsKey(snapshot.ActionId))
                throw new InvalidOperationException("Copilot Agent reused a resolved approval action ID.");

            var pendingByCall = new Dictionary<string, ApprovalRequestSnapshot>(_pendingByCall, StringComparer.Ordinal)
            {
                [callKey] = snapshot,
            };
            return new CopilotTurnApprovalLifecycleState(pendingByCall, _resolvedByAction);
        }

        private CopilotTurnApprovalLifecycleState ObserveAgentCompleted()
        {
            if (_pendingByCall.Count > 0)
                throw new InvalidOperationException("Copilot Agent completed while a turn-blocking approval request was still pending.");
            return this;
        }

        private CopilotTurnApprovalLifecycleState Resolve(ApprovalRequestSnapshot snapshot)
        {
            var pendingByCall = new Dictionary<string, ApprovalRequestSnapshot>(_pendingByCall, StringComparer.Ordinal);
            pendingByCall.Remove(snapshot.CallKey);
            var resolvedByAction = new Dictionary<string, ApprovalRequestSnapshot>(_resolvedByAction, StringComparer.Ordinal)
            {
                [snapshot.ActionId] = snapshot,
            };
            return new CopilotTurnApprovalLifecycleState(pendingByCall, resolvedByAction);
        }

        private bool ContainsPendingAction(string actionId)
        {
            foreach (var pending in _pendingByCall.Values)
            {
                if (string.Equals(pending.ActionId, actionId, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private static void RequireExecution(
            CopilotToolExecutionInfo execution,
            CopilotToolExecutionState expectedState,
            string lifecycleStage)
        {
            if (!HasValidCallIdentity(execution)
                || execution.Round < 1
                || execution.MaxAttempts < execution.Attempt
                || string.IsNullOrWhiteSpace(execution.RuntimeName)
                || string.IsNullOrWhiteSpace(execution.ApprovalActionId)
                || execution.StartedAtUtc == default
                || execution.TimeoutMs < 1
                || execution.State != expectedState)
            {
                throw new InvalidOperationException(
                    $"Copilot Agent approval {lifecycleStage} has invalid execution metadata.");
            }
        }

        private static void RequireTerminalExecution(CopilotToolExecutionInfo execution)
        {
            if (!HasValidCallIdentity(execution)
                || execution.Round < 1
                || execution.MaxAttempts < execution.Attempt
                || string.IsNullOrWhiteSpace(execution.RuntimeName)
                || string.IsNullOrWhiteSpace(execution.ApprovalActionId)
                || execution.StartedAtUtc == default
                || execution.TimeoutMs < 1
                || !Enum.IsDefined(execution.State)
                || execution.State is CopilotToolExecutionState.Pending
                    or CopilotToolExecutionState.Running
                    or CopilotToolExecutionState.AwaitingApproval)
            {
                throw new InvalidOperationException("Copilot Agent approval resolution has invalid execution metadata.");
            }
        }

        private static void RequireTerminalResult(
            CopilotAgentEvent agentEvent,
            CopilotToolExecutionInfo execution)
        {
            if (agentEvent.ToolResult == null
                || agentEvent.ToolResult.Approval != null
                || !string.Equals(agentEvent.ToolResult.ToolName, execution.ToolName, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Copilot Agent emitted an invalid approval resolution.");
            }
        }

        private static void RequireMatchingExecution(
            ApprovalRequestSnapshot expected,
            CopilotToolExecutionInfo actual)
        {
            if (!string.Equals(expected.ActionId, actual.ApprovalActionId, StringComparison.Ordinal)
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
                throw new InvalidOperationException("Copilot Agent approval continuation did not match the requested tool call and action.");
            }
        }

        private static bool HasValidCallIdentity(CopilotToolExecutionInfo execution) =>
            !string.IsNullOrWhiteSpace(execution.CallId)
            && !string.IsNullOrWhiteSpace(execution.ToolName)
            && execution.Attempt >= 1;

        private static string BuildCallKey(CopilotToolExecutionInfo execution) =>
            $"{execution.CallId}\u001f{execution.Attempt}\u001f{execution.ToolName}";

        private sealed record ApprovalRequestSnapshot(
            string CallKey,
            string ActionId,
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
            string ArgumentSummary,
            long TimeoutMs)
        {
            public static ApprovalRequestSnapshot Create(CopilotToolExecutionInfo execution) => new(
                BuildCallKey(execution),
                execution.ApprovalActionId,
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
                execution.ArgumentSummary,
                execution.TimeoutMs);
        }
    }
}
