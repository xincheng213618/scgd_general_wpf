using ColorVision.UI;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotSubagentRunPhase
    {
        Exploration,
        Finalization,
    }

    public interface ICopilotSubagentRunner
    {
        Task<CopilotSubagentResult> RunAsync(
            CopilotAgentRequest parentRequest,
            CopilotSubagentRoleDescriptor role,
            CopilotSubagentRunRequest runRequest,
            CancellationToken cancellationToken);
    }

    public sealed class CopilotSubagentRunRequest
    {
        public string RunId { get; init; } = string.Empty;

        public string ResumeFromRunId { get; init; } = string.Empty;

        public CopilotAgentSessionCheckpoint? ResumeCheckpoint { get; init; }

        public string Task { get; init; } = string.Empty;

        public string Model { get; init; } = string.Empty;

        public string ReasoningEffort { get; init; } = string.Empty;

        public int RequestTokenBudget { get; init; }

        public long QueueDurationMs { get; init; }

        internal Action<CopilotSubagentRunPhase, CopilotAgentBudgetSnapshot, string?>? ProgressUpdated { get; set; }

        internal void ReportProgress(
            CopilotSubagentRunPhase phase,
            CopilotAgentBudgetSnapshot budget,
            string? activeToolName = null)
        {
            ArgumentNullException.ThrowIfNull(budget);
            try
            {
                ProgressUpdated?.Invoke(phase, budget, activeToolName);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning(
                    "Copilot subagent progress observer failed: {0}",
                    CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
        }
    }

    internal sealed class CopilotSubagentToolActivityTracker
    {
        private const int MaximumToolNameLength = 120;
        private readonly List<(string Key, string ToolName)> _activeTools = [];

        internal string ActiveToolName => _activeTools.Count == 0
            ? string.Empty
            : _activeTools[^1].ToolName;

        internal bool Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (agentEvent.Type is not (CopilotAgentEventType.ToolStarted
                or CopilotAgentEventType.ToolProgress
                or CopilotAgentEventType.ToolResult))
            {
                return false;
            }

            var execution = agentEvent.ToolExecution;
            var toolName = NormalizeToolName(execution?.ToolName);
            if (toolName.Length == 0)
                return false;

            var key = string.IsNullOrWhiteSpace(execution?.CallId)
                ? toolName
                : execution.CallId.Trim();
            var existingIndex = _activeTools.FindIndex(item =>
                string.Equals(item.Key, key, StringComparison.Ordinal));
            if (existingIndex >= 0)
                _activeTools.RemoveAt(existingIndex);

            if (agentEvent.Type is CopilotAgentEventType.ToolStarted or CopilotAgentEventType.ToolProgress)
                _activeTools.Add((key, toolName));
            return true;
        }

        private static string NormalizeToolName(string? value)
        {
            var sanitized = CopilotAgentTraceEntry.Sanitize(value);
            var toolName = string.Join(" ", sanitized.Split(
                (char[]?)null,
                StringSplitOptions.RemoveEmptyEntries));
            return toolName.Length <= MaximumToolNameLength
                ? toolName
                : toolName[..MaximumToolNameLength];
        }
    }

    internal sealed class CopilotSubagentSteeringMetrics
    {
        internal int DeliveredCount { get; private set; }

        internal int UndeliveredCount { get; private set; }

        internal void Observe(CopilotAgentEvent agentEvent)
        {
            ArgumentNullException.ThrowIfNull(agentEvent);
            if (agentEvent.Type == CopilotAgentEventType.SteeringDelivered)
                DeliveredCount += agentEvent.SteeringMessages.Count;
            else if (agentEvent.Type == CopilotAgentEventType.SteeringRecovery)
                UndeliveredCount += agentEvent.SteeringMessages.Count;
        }
    }

    public sealed class CopilotSubagentResult
    {
        public string RoleId { get; init; } = string.Empty;

        public string RunId { get; init; } = string.Empty;

        public int RequestTokenBudget { get; init; }

        public long QueueDurationMs { get; init; }

        public string Answer { get; init; } = string.Empty;

        public CopilotAgentStopReason StopReason { get; init; }

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;

        public CopilotAgentBudgetSnapshot Budget { get; init; } = new();

        public IReadOnlyList<string> ToolNames { get; init; } = Array.Empty<string>();

        public bool WasTruncated { get; init; }

        public bool UsedBudgetFinalization { get; init; }

        public bool UsedPreselectedEvidence { get; init; }

        public bool HasSuccessfulEvidence { get; init; }

        public bool SessionResumed { get; init; }

        public int DeliveredSteeringCount { get; init; }

        public int UndeliveredSteeringCount { get; init; }

        public string ResumeFailureReason { get; init; } = string.Empty;

        public CopilotAgentSessionCheckpoint? SessionCheckpoint { get; init; }
    }

}
