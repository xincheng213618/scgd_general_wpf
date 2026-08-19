using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnHookLifecycleState
    {
        private readonly IReadOnlyDictionary<string, CopilotToolExecutionHookRun> _active;
        private readonly IReadOnlyDictionary<string, CopilotToolExecutionHookRun> _completed;
        private readonly IReadOnlySet<string> _terminalCalls;

        private CopilotTurnHookLifecycleState(
            IReadOnlyDictionary<string, CopilotToolExecutionHookRun> active,
            IReadOnlyDictionary<string, CopilotToolExecutionHookRun> completed,
            IReadOnlySet<string> terminalCalls)
        {
            _active = active;
            _completed = completed;
            _terminalCalls = terminalCalls;
        }

        public static CopilotTurnHookLifecycleState Empty { get; } = new(
            new Dictionary<string, CopilotToolExecutionHookRun>(StringComparer.Ordinal),
            new Dictionary<string, CopilotToolExecutionHookRun>(StringComparer.Ordinal),
            new HashSet<string>(StringComparer.Ordinal));

        public CopilotTurnHookLifecycleState Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            return agentEvent.Type switch
            {
                CopilotAgentEventType.HookStarted => ObserveStarted(agentEvent),
                CopilotAgentEventType.HookCompleted => ObserveCompleted(agentEvent),
                CopilotAgentEventType.ToolResult => ObserveToolResult(agentEvent),
                CopilotAgentEventType.Completed => ObserveAgentCompleted(),
                _ => this,
            };
        }

        private CopilotTurnHookLifecycleState ObserveStarted(CopilotAgentEvent agentEvent)
        {
            var execution = RequireExecution(agentEvent, "start");
            var hook = agentEvent.ToolExecutionHook;
            if (hook?.IsStructurallyValid(requireCompleted: false) != true)
                throw new InvalidOperationException("Copilot Agent emitted an invalid tool hook start.");

            var callKey = BuildCallKey(execution);
            var hookKey = BuildHookKey(callKey, hook.SourceId, hook.Phase);
            if (_terminalCalls.Contains(callKey))
                throw new InvalidOperationException("Copilot Agent started a tool hook after the tool reached a terminal state.");
            if (_active.ContainsKey(hookKey) || _completed.ContainsKey(hookKey))
                throw new InvalidOperationException("Copilot Agent started the same tool hook more than once.");

            var active = new Dictionary<string, CopilotToolExecutionHookRun>(_active, StringComparer.Ordinal)
            {
                [hookKey] = CopilotToolExecutionHookRun.Create(
                    hook.SourceId,
                    hook.Phase,
                    CopilotToolExecutionHookState.Completed,
                    durationMs: 0),
            };
            return new CopilotTurnHookLifecycleState(active, _completed, _terminalCalls);
        }

        private CopilotTurnHookLifecycleState ObserveCompleted(CopilotAgentEvent agentEvent)
        {
            var execution = RequireExecution(agentEvent, "completion");
            var hook = agentEvent.ToolExecutionHook;
            if (hook?.IsStructurallyValid(requireCompleted: true) != true)
                throw new InvalidOperationException("Copilot Agent emitted an invalid tool hook completion.");

            var callKey = BuildCallKey(execution);
            var hookKey = BuildHookKey(callKey, hook.SourceId, hook.Phase);
            if (_terminalCalls.Contains(callKey))
                throw new InvalidOperationException("Copilot Agent completed a tool hook after the tool reached a terminal state.");
            if (!_active.ContainsKey(hookKey))
                throw new InvalidOperationException("Copilot Agent completed a tool hook before it started.");
            if (_completed.ContainsKey(hookKey))
                throw new InvalidOperationException("Copilot Agent completed the same tool hook more than once.");

            var active = new Dictionary<string, CopilotToolExecutionHookRun>(_active, StringComparer.Ordinal);
            active.Remove(hookKey);
            var completed = new Dictionary<string, CopilotToolExecutionHookRun>(_completed, StringComparer.Ordinal)
            {
                [hookKey] = hook.Result!.CreateSnapshot(),
            };
            return new CopilotTurnHookLifecycleState(active, completed, _terminalCalls);
        }

        private CopilotTurnHookLifecycleState ObserveToolResult(CopilotAgentEvent agentEvent)
        {
            var execution = agentEvent.ToolExecution;
            if (execution == null || string.IsNullOrWhiteSpace(execution.CallId))
                return this;

            var finalHookRuns = agentEvent.ToolExecutionHookRuns ?? Array.Empty<CopilotToolExecutionHookRun>();
            var finalHookIdentities = new HashSet<string>(StringComparer.Ordinal);
            foreach (var finalHookRun in finalHookRuns)
            {
                if (finalHookRun?.IsStructurallyValid() != true
                    || !finalHookIdentities.Add(BuildHookIdentity(finalHookRun)))
                {
                    throw new InvalidOperationException("Copilot Agent emitted an invalid or duplicate hook in a tool result.");
                }
            }

            var callKey = BuildCallKey(execution);
            if (_active.Keys.Any(key => key.StartsWith(callKey + "\u001f", StringComparison.Ordinal)))
                throw new InvalidOperationException("Copilot Agent emitted a tool result before its active hook completed.");

            var completedForCall = _completed
                .Where(item => item.Key.StartsWith(callKey + "\u001f", StringComparison.Ordinal))
                .Select(item => item.Value)
                .ToArray();
            foreach (var observed in completedForCall)
            {
                if (!finalHookRuns.Any(final => AreEquivalent(observed, final)))
                    throw new InvalidOperationException("Copilot Agent tool result did not reconcile a completed hook.");
            }

            if (execution.State == CopilotToolExecutionState.AwaitingApproval)
                return this;
            if (_terminalCalls.Contains(callKey))
                throw new InvalidOperationException("Copilot Agent emitted more than one terminal result for a tool call.");

            var terminalCalls = new HashSet<string>(_terminalCalls, StringComparer.Ordinal)
            {
                callKey,
            };
            return new CopilotTurnHookLifecycleState(_active, _completed, terminalCalls);
        }

        private CopilotTurnHookLifecycleState ObserveAgentCompleted()
        {
            if (_active.Count > 0)
                throw new InvalidOperationException("Copilot Agent completed while a tool hook was still active.");
            return this;
        }

        private static CopilotToolExecutionInfo RequireExecution(
            CopilotAgentEvent agentEvent,
            string lifecycleStage)
        {
            var execution = agentEvent.ToolExecution;
            if (!CopilotToolExecutionInfoProtocol.HasValidActiveState(
                    execution,
                    allowPending: true))
            {
                throw new InvalidOperationException(
                    $"Copilot Agent tool hook {lifecycleStage} has invalid execution metadata.");
            }

            return execution;
        }

        private static string BuildCallKey(CopilotToolExecutionInfo execution) =>
            $"{execution.CallId}\u001f{execution.Attempt}\u001f{execution.ToolName}";

        private static string BuildHookKey(
            string callKey,
            string sourceId,
            CopilotToolExecutionHookPhase phase) =>
            $"{callKey}\u001f{(int)phase}\u001f{sourceId}";

        private static string BuildHookIdentity(CopilotToolExecutionHookRun hookRun) =>
            $"{(int)hookRun.Phase}\u001f{hookRun.SourceId}";

        private static bool AreEquivalent(
            CopilotToolExecutionHookRun expected,
            CopilotToolExecutionHookRun? actual)
        {
            return actual?.IsStructurallyValid() == true
                && expected.Phase == actual.Phase
                && expected.ExecutionMode == actual.ExecutionMode
                && expected.State == actual.State
                && expected.DurationMs == actual.DurationMs
                && string.Equals(expected.SourceId, actual.SourceId, StringComparison.Ordinal)
                && string.Equals(expected.FailureCode, actual.FailureCode, StringComparison.Ordinal);
        }
    }
}
