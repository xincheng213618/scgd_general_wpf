using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatMessage
    {
        private static CopilotAgentBudgetSnapshot NormalizeAgentRunBudget(CopilotAgentBudgetSnapshot? budget)
        {
            budget ??= new CopilotAgentBudgetSnapshot();
            var maxToolCalls = Math.Max(0, budget.MaxToolCalls);
            var toolCalls = Math.Max(0, budget.ToolCalls);
            if (maxToolCalls > 0)
                toolCalls = Math.Min(toolCalls, maxToolCalls);
            var reportedInputTokens = Math.Max(0, budget.ReportedInputTokens);
            var reportedOutputTokens = Math.Max(0, budget.ReportedOutputTokens);
            var reportedTotalTokens = (int)Math.Clamp(
                Math.Max(
                    (long)Math.Max(0, budget.ReportedTotalTokens),
                    (long)reportedInputTokens + reportedOutputTokens),
                0,
                int.MaxValue);
            var contextRecoveryEstimatedInputTokensBefore = Math.Max(
                0,
                budget.ContextRecoveryEstimatedInputTokensBefore);
            var providerCalls = Math.Max(0, budget.ProviderCalls);
            var providerRetryCount = Math.Clamp(
                budget.ProviderRetryCount,
                0,
                providerCalls);
            var providerFirstContentTimeoutCount = Math.Clamp(
                budget.ProviderFirstContentTimeoutCount,
                0,
                providerCalls);
            var providerStreamInactivityTimeoutCount = Math.Clamp(
                budget.ProviderStreamInactivityTimeoutCount,
                0,
                providerCalls - providerFirstContentTimeoutCount);
            var providerResponseCount = Math.Clamp(
                budget.ProviderResponseCount,
                0,
                providerCalls);
            var providerFirstResponseLatencyTotalMs = providerResponseCount > 0
                ? Math.Max(0, budget.ProviderFirstResponseLatencyTotalMs)
                : 0;
            var providerStreamChunkCount = providerResponseCount > 0
                ? Math.Max(0, budget.ProviderStreamChunkCount)
                : 0;
            var providerStreamInterChunkLatencyCount = Math.Clamp(
                budget.ProviderStreamInterChunkLatencyCount,
                0,
                Math.Max(0, providerStreamChunkCount - 1));
            var providerStreamInterChunkLatencyTotalMs = providerStreamInterChunkLatencyCount > 0
                ? Math.Max(0, budget.ProviderStreamInterChunkLatencyTotalMs)
                : 0;

            return new CopilotAgentBudgetSnapshot
            {
                CompactionEnabled = budget.CompactionEnabled,
                ContextWindowTokens = Math.Max(0, budget.ContextWindowTokens),
                InputBudgetTokens = Math.Max(0, budget.InputBudgetTokens),
                RequestTokenBudget = Math.Max(0, budget.RequestTokenBudget),
                ConsumedTokens = Math.Max(0, budget.ConsumedTokens),
                ProviderCalls = providerCalls,
                PeakEstimatedInputTokens = Math.Max(0, budget.PeakEstimatedInputTokens),
                ProviderRetryCount = providerRetryCount,
                ProviderRateLimitRetryCount = Math.Clamp(
                    budget.ProviderRateLimitRetryCount,
                    0,
                    providerRetryCount),
                ProviderRetryDelayMs = providerRetryCount > 0
                    ? Math.Max(0, budget.ProviderRetryDelayMs)
                    : 0,
                ProviderFirstContentTimeoutCount = providerFirstContentTimeoutCount,
                ProviderStreamInactivityTimeoutCount =
                    providerStreamInactivityTimeoutCount,
                ProviderResponseCount = providerResponseCount,
                ProviderFirstResponseLatencyTotalMs = providerFirstResponseLatencyTotalMs,
                ProviderFirstResponseLatencyMaxMs = Math.Clamp(
                    budget.ProviderFirstResponseLatencyMaxMs,
                    0,
                    providerFirstResponseLatencyTotalMs),
                ProviderCallDurationTotalMs = providerCalls > 0
                    ? Math.Max(
                        providerFirstResponseLatencyTotalMs,
                        budget.ProviderCallDurationTotalMs)
                    : 0,
                ProviderStreamChunkCount = providerStreamChunkCount,
                ProviderStreamInterChunkLatencyCount = providerStreamInterChunkLatencyCount,
                ProviderStreamInterChunkLatencyTotalMs = providerStreamInterChunkLatencyTotalMs,
                ProviderStreamInterChunkLatencyMaxMs = Math.Clamp(
                    budget.ProviderStreamInterChunkLatencyMaxMs,
                    0,
                    providerStreamInterChunkLatencyTotalMs),
                ContextRecoveryCount = Math.Max(0, budget.ContextRecoveryCount),
                ContextRecoveryEstimatedInputTokensBefore = contextRecoveryEstimatedInputTokensBefore,
                ContextRecoveryEstimatedInputTokensAfter = Math.Clamp(
                    budget.ContextRecoveryEstimatedInputTokensAfter,
                    0,
                    contextRecoveryEstimatedInputTokensBefore),
                ReportedInputTokens = reportedInputTokens,
                ReportedOutputTokens = reportedOutputTokens,
                ReportedTotalTokens = reportedTotalTokens,
                ReportedCachedInputTokens = reportedInputTokens > 0
                    && budget.ReportedCachedInputTokens.HasValue
                    ? Math.Clamp(budget.ReportedCachedInputTokens.Value, 0, reportedInputTokens)
                    : null,
                UsedEstimatedUsage = budget.UsedEstimatedUsage,
                UsedDelegatedDirectAnswer = budget.UsedDelegatedDirectAnswer,
                BudgetExhausted = budget.BudgetExhausted,
                RequestTokenBudgetExhausted = budget.RequestTokenBudgetExhausted,
                MaxToolCalls = maxToolCalls,
                ToolCalls = toolCalls,
                ToolBudgetExhausted = budget.ToolBudgetExhausted,
                RegisteredToolCount = Math.Max(0, budget.RegisteredToolCount),
                AvailableToolCount = Math.Clamp(
                    budget.AvailableToolCount,
                    0,
                    Math.Max(0, budget.RegisteredToolCount)),
                AvailableToolDefinitionCharacters = Math.Max(0, budget.AvailableToolDefinitionCharacters),
                HarnessInstructionCharacters = Math.Max(0, budget.HarnessInstructionCharacters),
                NarrowEvidenceResultLimit = Math.Max(0, budget.NarrowEvidenceResultLimit),
                MaxAgentPasses = Math.Max(0, budget.MaxAgentPasses),
                TotalDurationMs = Math.Max(0, budget.TotalDurationMs),
                ElapsedMs = Math.Max(0, budget.ElapsedMs),
                TimeBudgetExhausted = budget.TimeBudgetExhausted,
            };
        }

        private static bool AgentRunBudgetsEqual(
            CopilotAgentBudgetSnapshot? left,
            CopilotAgentBudgetSnapshot? right)
        {
            if (ReferenceEquals(left, right))
                return true;
            if (left == null || right == null)
                return false;

            return left.CompactionEnabled == right.CompactionEnabled
                && left.ContextWindowTokens == right.ContextWindowTokens
                && left.InputBudgetTokens == right.InputBudgetTokens
                && left.RequestTokenBudget == right.RequestTokenBudget
                && left.ConsumedTokens == right.ConsumedTokens
                && left.ProviderCalls == right.ProviderCalls
                && left.PeakEstimatedInputTokens == right.PeakEstimatedInputTokens
                && left.ProviderRetryCount == right.ProviderRetryCount
                && left.ProviderRateLimitRetryCount == right.ProviderRateLimitRetryCount
                && left.ProviderRetryDelayMs == right.ProviderRetryDelayMs
                && left.ProviderFirstContentTimeoutCount == right.ProviderFirstContentTimeoutCount
                && left.ProviderStreamInactivityTimeoutCount == right.ProviderStreamInactivityTimeoutCount
                && left.ProviderResponseCount == right.ProviderResponseCount
                && left.ProviderFirstResponseLatencyTotalMs == right.ProviderFirstResponseLatencyTotalMs
                && left.ProviderFirstResponseLatencyMaxMs == right.ProviderFirstResponseLatencyMaxMs
                && left.ProviderCallDurationTotalMs == right.ProviderCallDurationTotalMs
                && left.ProviderStreamChunkCount == right.ProviderStreamChunkCount
                && left.ProviderStreamInterChunkLatencyCount == right.ProviderStreamInterChunkLatencyCount
                && left.ProviderStreamInterChunkLatencyTotalMs == right.ProviderStreamInterChunkLatencyTotalMs
                && left.ProviderStreamInterChunkLatencyMaxMs == right.ProviderStreamInterChunkLatencyMaxMs
                && left.ContextRecoveryCount == right.ContextRecoveryCount
                && left.ContextRecoveryEstimatedInputTokensBefore == right.ContextRecoveryEstimatedInputTokensBefore
                && left.ContextRecoveryEstimatedInputTokensAfter == right.ContextRecoveryEstimatedInputTokensAfter
                && left.ReportedInputTokens == right.ReportedInputTokens
                && left.ReportedOutputTokens == right.ReportedOutputTokens
                && left.ReportedTotalTokens == right.ReportedTotalTokens
                && left.ReportedCachedInputTokens == right.ReportedCachedInputTokens
                && left.UsedEstimatedUsage == right.UsedEstimatedUsage
                && left.UsedDelegatedDirectAnswer == right.UsedDelegatedDirectAnswer
                && left.BudgetExhausted == right.BudgetExhausted
                && left.RequestTokenBudgetExhausted == right.RequestTokenBudgetExhausted
                && left.MaxToolCalls == right.MaxToolCalls
                && left.ToolCalls == right.ToolCalls
                && left.ToolBudgetExhausted == right.ToolBudgetExhausted
                && left.RegisteredToolCount == right.RegisteredToolCount
                && left.AvailableToolCount == right.AvailableToolCount
                && left.AvailableToolDefinitionCharacters == right.AvailableToolDefinitionCharacters
                && left.HarnessInstructionCharacters == right.HarnessInstructionCharacters
                && left.NarrowEvidenceResultLimit == right.NarrowEvidenceResultLimit
                && left.MaxAgentPasses == right.MaxAgentPasses
                && left.TotalDurationMs == right.TotalDurationMs
                && left.ElapsedMs == right.ElapsedMs
                && left.TimeBudgetExhausted == right.TimeBudgetExhausted;
        }

        private static string BuildThinkingContent(string? executionContent, string? reasoningContent)
        {
            var builder = new StringBuilder();
            var execution = FilterDisplayableExecutionContent(executionContent);
            var reasoning = (reasoningContent ?? string.Empty).Trim();

            if (!string.IsNullOrWhiteSpace(execution))
                builder.AppendLine(execution);

            if (!string.IsNullOrWhiteSpace(reasoning))
            {
                if (builder.Length > 0)
                    builder.AppendLine().AppendLine();

                builder.AppendLine(CopilotUiText.ThinkingDetailsHeader);
                builder.AppendLine(reasoning);
            }

            return builder.ToString().TrimEnd();
        }

        private string FormatCompletedProcessingElapsed()
        {
            var startedAt = ThinkingStartedAt == default ? CreatedAt : ThinkingStartedAt;
            if (IsThinkingInProgress || startedAt == default || ThinkingCompletedAt == default || ThinkingCompletedAt < startedAt)
                return string.Empty;

            var elapsed = ThinkingCompletedAt - startedAt;
            if (elapsed.TotalSeconds < 1)
                return "<1s";

            var totalSeconds = Math.Max(1, (int)Math.Floor(elapsed.TotalSeconds));
            var hours = totalSeconds / 3600;
            var minutes = totalSeconds % 3600 / 60;
            var seconds = totalSeconds % 60;

            if (hours > 0)
                return $"{hours}h {minutes}m {seconds}s";

            return minutes > 0 ? $"{minutes}m {seconds}s" : $"{seconds}s";
        }

        private static string FilterDisplayableExecutionContent(string? content)
        {
            var text = (content ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var blocks = text.Split(ExecutionBlockSeparators, StringSplitOptions.RemoveEmptyEntries);
            var keptBlocks = blocks
                .Select(FilterExecutionBlock)
                .Where(block => !string.IsNullOrWhiteSpace(block))
                .ToArray();

            return string.Join(Environment.NewLine + Environment.NewLine, keptBlocks).Trim();
        }

        private static string FilterExecutionBlock(string block)
        {
            var lines = block
                .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Trim())
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .ToArray();

            if (lines.Length == 0 || IsHiddenExecutionBlock(lines))
                return string.Empty;

            var keptLines = lines.Where(line => !IsHiddenExecutionLine(line)).ToArray();
            return string.Join(Environment.NewLine, keptLines).Trim();
        }

        private static bool IsHiddenExecutionBlock(string[] lines)
        {
            if (IsFailedSearchExecutionBlock(lines))
                return true;

            return lines.All(IsHiddenExecutionLine);
        }

        private static bool IsFailedSearchExecutionBlock(string[] lines)
        {
            var mentionsSearchTool = lines.Any(line =>
                line.Contains("SearchFiles", StringComparison.OrdinalIgnoreCase)
                || line.Contains("GrepText", StringComparison.OrdinalIgnoreCase)
                || line.Contains("SearchDocs", StringComparison.OrdinalIgnoreCase)
                || line.Contains("WebSearch", StringComparison.OrdinalIgnoreCase));
            if (!mentionsSearchTool)
                return false;

            return lines.Any(line =>
                line.StartsWith("Status: Failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("] Failed", StringComparison.OrdinalIgnoreCase)
                || line.Contains("] Timed out", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsHiddenExecutionLine(string line)
        {
            return line.Equals("Analyzing task...", StringComparison.OrdinalIgnoreCase)
                || line.Equals("Generating answer...", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Round ", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Tool phase converged", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("No extra tools are needed", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Reused the context", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skills enabled", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skills selected", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("Agent Skill history", StringComparison.OrdinalIgnoreCase)
                || line.StartsWith("MCP client", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildExecutionSummary(string? content, bool isInProgress)
        {
            var text = content ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
                return isInProgress ? "Starting" : string.Empty;

            var (toolCount, failedCount, latestTool) = AnalyzeExecutionTrace(text);

            if (toolCount == 0)
            {
                var firstLine = text
                    .Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries)
                    .Select(line => line.Trim())
                    .FirstOrDefault(line => !string.IsNullOrWhiteSpace(line));
                return isInProgress
                    ? TrimForInline(string.IsNullOrWhiteSpace(firstLine) ? "Running" : firstLine)
                    : "Trace available";
            }

            var builder = new StringBuilder(isInProgress ? "Running" : "Completed");
            builder.Append(" - ").Append(toolCount).Append(toolCount == 1 ? " tool" : " tools");

            if (failedCount > 0)
                builder.Append(" - ").Append(failedCount).Append(" failed");

            if (!string.IsNullOrWhiteSpace(latestTool))
                builder.Append(" - latest ").Append(TrimForInline(latestTool));

            return builder.ToString();
        }

        private static (int ToolCount, int FailedCount, string LatestTool) AnalyzeExecutionTrace(string? content)
        {
            var toolCount = 0;
            var failedCount = 0;
            var latestTool = string.Empty;

            foreach (var rawLine in (content ?? string.Empty).Split(ExecutionLineSeparators, StringSplitOptions.RemoveEmptyEntries))
            {
                var line = rawLine.Trim();
                if (line.Length > 2 && line[0] == '[')
                {
                    var closeIndex = line.IndexOf(']');
                    if (closeIndex > 1)
                    {
                        latestTool = line[1..closeIndex].Trim();
                        toolCount++;
                    }
                }

                if (line.StartsWith("Status:", StringComparison.OrdinalIgnoreCase)
                    && line.Contains("Failed", StringComparison.OrdinalIgnoreCase))
                {
                    failedCount++;
                }
            }

            return (toolCount, failedCount, latestTool);
        }

        private static string TrimForInline(string value)
        {
            var text = (value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 48 ? text : text[..48] + "...";
        }

        private static string TrimForTooltip(string value)
        {
            var text = (value ?? string.Empty).Trim();
            return text.Length <= 1600 ? text : text[..1600] + Environment.NewLine + "...";
        }

        internal static string BoundAssistantDelta(
            int existingLength,
            string? delta,
            string truncationMarker,
            out bool truncated)
        {
            truncated = false;
            var value = delta ?? string.Empty;
            if (value.Length == 0)
                return string.Empty;

            var payloadLimit = MaximumAssistantTextCharacters - truncationMarker.Length;
            var remaining = Math.Max(0, payloadLimit - existingLength);
            if (value.Length <= remaining)
                return value;

            truncated = true;
            var retainedLength = Math.Min(remaining, value.Length);
            if (retainedLength > 0
                && retainedLength < value.Length
                && char.IsHighSurrogate(value[retainedLength - 1])
                && char.IsLowSurrogate(value[retainedLength]))
            {
                retainedLength--;
            }

            return value[..retainedLength] + truncationMarker;
        }

        private static string TruncateAssistantText(string value, string truncationMarker)
        {
            var retainedLength = MaximumAssistantTextCharacters - truncationMarker.Length;
            if (retainedLength > 0
                && retainedLength < value.Length
                && char.IsHighSurrogate(value[retainedLength - 1])
                && char.IsLowSurrogate(value[retainedLength]))
            {
                retainedLength--;
            }

            return value[..retainedLength].TrimEnd() + truncationMarker;
        }
    }
}

