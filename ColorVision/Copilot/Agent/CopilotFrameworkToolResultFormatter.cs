using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    public static class CopilotFrameworkToolResultFormatter
    {
        public const int MaxContentCharacters = 12_000;
        public const int MaxSerializedCharacters = 16_000;
        public const int MaxSummaryCharacters = 800;
        public const int MaxErrorCharacters = 1_200;

        private const int MinimumContentCharacters = 256;
        private const int MaxPreservedSections = 24;
        private static readonly Regex WebPageSectionRegex = new(
            @"(?m)^(?=\[Web Page (?:Fetched|Fetch Failed)\])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex WebSearchSectionRegex = new(
            @"(?m)^(?=\[Web Search Results\]|\[Web Page (?:Fetched|Fetch Failed)\])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex FileSectionRegex = new(
            @"(?m)^(?=\[File\]\s)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Format(CopilotToolExecutionOutcome outcome)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            var result = outcome.Result ?? new CopilotToolResult();
            var execution = outcome.Execution ?? new CopilotToolExecutionInfo();
            var content = SanitizeMultiline(result.Content);
            var contentBudget = Math.Min(MaxContentCharacters, content.Length);

            for (var pass = 0; pass < 8; pass++)
            {
                var compactedContent = CompactContent(execution.ToolName, content, contentBudget);
                var serialized = Serialize(outcome, compactedContent, content.Length, compactedContent.Length < content.Length);
                if (serialized.Length <= MaxSerializedCharacters)
                    return serialized;

                if (contentBudget <= MinimumContentCharacters)
                    break;

                var excess = serialized.Length - MaxSerializedCharacters;
                contentBudget = Math.Max(MinimumContentCharacters, contentBudget - Math.Max(256, excess));
            }

            var minimalContent = content.Length == 0
                ? string.Empty
                : CompactHeadTail(content, MinimumContentCharacters, GetTailRatio(execution.ToolName));
            var minimal = Serialize(outcome, minimalContent, content.Length, minimalContent.Length < content.Length);
            if (minimal.Length <= MaxSerializedCharacters)
                return minimal;

            return Serialize(outcome, string.Empty, content.Length, content.Length > 0);
        }

        public static string FormatRejected(string toolName, string error)
        {
            return FormatRejected(toolName, error, string.Empty, CopilotToolFailureKind.None);
        }

        public static string FormatRejected(string toolName, string error, string failureCode, CopilotToolFailureKind failureKind)
        {
            var payload = new Dictionary<string, object?>
            {
                ["tool"] = SanitizeInline(toolName, 120),
                ["success"] = false,
                ["retry_allowed"] = false,
                ["summary"] = SanitizeInline($"{toolName} was not executed.", MaxSummaryCharacters),
                ["error"] = SanitizeInline(error, MaxErrorCharacters),
            };
            if (failureKind != CopilotToolFailureKind.None)
                payload["failure_kind"] = failureKind.ToString().ToLowerInvariant();
            var normalizedFailureCode = CopilotToolFailureCode.Normalize(failureCode);
            if (!string.IsNullOrWhiteSpace(normalizedFailureCode))
                payload["failure_code"] = normalizedFailureCode;
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static string Serialize(
            CopilotToolExecutionOutcome outcome,
            string content,
            int originalContentCharacters,
            bool contentTruncated)
        {
            var result = outcome.Result;
            var execution = outcome.Execution;
            var payload = new Dictionary<string, object?>
            {
                ["tool"] = SanitizeInline(execution.ToolName, 120),
                ["success"] = result.Success,
                ["attempt"] = new Dictionary<string, int>
                {
                    ["current"] = Math.Max(1, execution.Attempt),
                    ["maximum"] = Math.Max(Math.Max(1, execution.Attempt), execution.MaxAttempts),
                },
                ["retry_allowed"] = execution.RetryEligible,
            };

            if (execution.FailureKind != CopilotToolFailureKind.None)
                payload["failure_kind"] = execution.FailureKind.ToString().ToLowerInvariant();
            var failureCode = result.Success ? string.Empty : CopilotToolFailureCode.Normalize(result.FailureCode);
            if (!string.IsNullOrWhiteSpace(failureCode))
                payload["failure_code"] = failureCode;
            if (result.Approval != null)
            {
                payload["status"] = "awaiting_approval";
                payload["approval"] = new Dictionary<string, object?>
                {
                    ["action_id"] = SanitizeInline(result.Approval.ActionId, 120),
                    ["title"] = SanitizeInline(result.Approval.Title, 300),
                    ["risk"] = SanitizeInline(result.Approval.RiskLevel, 60),
                    ["expires_at_utc"] = result.Approval.ExpiresAtUtc.ToString("O"),
                };
            }

            if (!string.IsNullOrWhiteSpace(result.Summary))
                payload["summary"] = SanitizeInline(result.Summary, MaxSummaryCharacters);
            if (originalContentCharacters > 0)
            {
                payload["content"] = content;
                payload["content_truncated"] = contentTruncated;
                if (contentTruncated)
                {
                    payload["content_original_characters"] = originalContentCharacters;
                    payload["content_returned_characters"] = content.Length;
                }
            }
            if (!string.IsNullOrWhiteSpace(result.ErrorMessage))
                payload["error"] = SanitizeInline(result.ErrorMessage, MaxErrorCharacters);
            if (result.DelegatedRunUsage != null)
            {
                var delegated = result.DelegatedRunUsage;
                payload["delegated_run"] = new Dictionary<string, object?>
                {
                    ["role"] = SanitizeInline(delegated.RoleId, 40),
                    ["run_id"] = SanitizeInline(delegated.RunId, 120),
                    ["stop_reason"] = delegated.StopReason.ToString().ToLowerInvariant(),
                    ["request_token_budget"] = Math.Max(0, delegated.RequestTokenBudget),
                    ["provider_calls"] = Math.Max(0, delegated.ProviderCalls),
                    ["tool_calls"] = Math.Max(0, delegated.ToolCalls),
                    ["queue_ms"] = Math.Max(0, delegated.QueueDurationMs),
                    ["consumed_tokens"] = Math.Max(0, delegated.ConsumedTokens),
                    ["input_tokens"] = Math.Max(0, delegated.Usage.InputTokens),
                    ["output_tokens"] = Math.Max(0, delegated.Usage.OutputTokens),
                    ["total_tokens"] = Math.Max(0, delegated.Usage.EffectiveTotalTokens),
                    ["includes_estimates"] = delegated.UsedEstimatedUsage,
                };
            }

            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static string CompactContent(string toolName, string content, int maximumCharacters)
        {
            if (maximumCharacters <= 0 || content.Length == 0)
                return string.Empty;
            if (content.Length <= maximumCharacters)
                return content;

            var sections = SplitSections(toolName, content);
            if (sections.Count < 2)
                return CompactHeadTail(content, maximumCharacters, GetTailRatio(toolName));

            return CompactSections(sections, maximumCharacters, GetTailRatio(toolName));
        }

        private static List<string> SplitSections(string toolName, string content)
        {
            var regex = toolName switch
            {
                "FetchUrl" => WebPageSectionRegex,
                "WebSearch" => WebSearchSectionRegex,
                "ReadLocalFile" or "ReadAttachedFile" => FileSectionRegex,
                _ => null,
            };
            if (regex == null)
                return [];

            var matches = regex.Matches(content).Cast<Match>().ToArray();
            if (matches.Length < 2)
                return [];

            var selected = matches.Length <= MaxPreservedSections
                ? matches
                : matches.Take(MaxPreservedSections / 2)
                    .Concat(matches.TakeLast(MaxPreservedSections / 2))
                    .ToArray();
            var sections = new List<string>(selected.Length + 1);
            var previousOriginalIndex = -1;
            foreach (var match in selected)
            {
                var originalIndex = Array.IndexOf(matches, match);
                if (previousOriginalIndex >= 0 && originalIndex > previousOriginalIndex + 1)
                    sections.Add($"...<{originalIndex - previousOriginalIndex - 1} middle tool section(s) omitted>...");

                var end = originalIndex + 1 < matches.Length ? matches[originalIndex + 1].Index : content.Length;
                sections.Add(content[match.Index..end].TrimEnd());
                previousOriginalIndex = originalIndex;
            }
            return sections;
        }

        private static string CompactSections(List<string> sections, int maximumCharacters, double tailRatio)
        {
            var builder = new StringBuilder(Math.Min(maximumCharacters, MaxContentCharacters));
            var remainingCharacters = maximumCharacters;
            for (var index = 0; index < sections.Count && remainingCharacters > 0; index++)
            {
                if (builder.Length > 0)
                {
                    var separator = Environment.NewLine + Environment.NewLine;
                    if (separator.Length >= remainingCharacters)
                        break;
                    builder.Append(separator);
                    remainingCharacters -= separator.Length;
                }

                var remainingSections = sections.Count - index;
                var sectionBudget = Math.Max(1, remainingCharacters / remainingSections);
                var section = CompactHeadTail(sections[index], sectionBudget, tailRatio);
                if (section.Length > remainingCharacters)
                    section = section[..remainingCharacters];
                builder.Append(section);
                remainingCharacters -= section.Length;
            }

            return builder.ToString();
        }

        private static string CompactHeadTail(string value, int maximumCharacters, double tailRatio)
        {
            if (maximumCharacters <= 0 || value.Length == 0)
                return string.Empty;
            if (value.Length <= maximumCharacters)
                return value;

            const string marker = "\n...<tool content compacted>...\n";
            if (maximumCharacters <= marker.Length + 16)
                return value[..maximumCharacters];

            var availableCharacters = maximumCharacters - marker.Length;
            var tailCharacters = (int)Math.Round(availableCharacters * Math.Clamp(tailRatio, 0.05, 0.9));
            var headCharacters = availableCharacters - tailCharacters;
            return value[..headCharacters] + marker + value[^tailCharacters..];
        }

        private static double GetTailRatio(string toolName)
        {
            return toolName switch
            {
                "GetRecentLog" => 0.7,
                "RunWorkspaceValidation" => 0.7,
                "RunShellCommand" => 0.7,
                "FetchUrl" => 0.12,
                "ReadLocalFile" or "ReadAttachedFile" => 0.2,
                _ => 0.25,
            };
        }

        private static string SanitizeMultiline(string? value)
        {
            return CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace("\0", string.Empty, StringComparison.Ordinal)
                .Trim();
        }

        private static string SanitizeInline(string? value, int maximumCharacters)
        {
            var text = SanitizeMultiline(value)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ", StringComparison.Ordinal);
            return text.Length <= maximumCharacters ? text : text[..Math.Max(0, maximumCharacters - 3)] + "...";
        }
    }
}
