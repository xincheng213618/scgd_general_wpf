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
    internal readonly record struct CopilotFrameworkToolResultFormatResult(
        string Content,
        bool ContentTruncated,
        bool ErrorTruncated,
        bool ArchiveReferenceIncluded);

    public static class CopilotFrameworkToolResultFormatter
    {
        public const int MaxContentCharacters = 12_000;
        public const int MaxSerializedCharacters = 16_000;
        public const int MaxSummaryCharacters = 800;
        public const int MaxErrorCharacters = 1_200;

        internal const int MinimumConfiguredTokenLimit = 0;
        internal const int MaximumConfiguredTokenLimit = int.MaxValue;

        private const int MaximumConfiguredSerializedCharacters =
            CopilotCodeReviewSnapshot.MaximumModelObservationCharacters;
        private const int MaximumConfiguredContentCharacters =
            MaximumConfiguredSerializedCharacters;
        private const int MaxPreservedSections = 24;
        private static readonly Regex WebPageSectionRegex = new(
            @"(?m)^(?=\[Web Fetch Scope\]|\[Web Page (?:Fetched|Fetch Failed)\])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex WebSearchSectionRegex = new(
            @"(?m)^(?=\[Web Search Results\]|\[Web Fetch Scope\]|\[Web Page (?:Fetched|Fetch Failed)\])",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex FileSectionRegex = new(
            @"(?m)^(?=\[File\]\s)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex AttachmentFileSectionRegex = new(
            @"(?m)^(?=\[Attachment Read Scope\]|\[File\]\s)",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        };

        public static string Format(CopilotToolExecutionOutcome outcome)
            => Format(outcome, toolOutputTokenLimit: null);

        public static string Format(CopilotToolExecutionOutcome outcome, int? toolOutputTokenLimit)
            => FormatDetailed(outcome, toolOutputTokenLimit).Content;

        internal static CopilotFrameworkToolResultFormatResult FormatDetailed(
            CopilotToolExecutionOutcome outcome,
            int? toolOutputTokenLimit)
        {
            ArgumentNullException.ThrowIfNull(outcome);
            var result = outcome.EffectiveModelResult;
            if (result.SuppressModelOutput)
                return CreateResult(outcome, string.Empty, contentTruncated: false);
            var execution = outcome.Execution ?? new CopilotToolExecutionInfo();
            var content = SanitizeMultiline(result.Content);
            var budget = ResolveBudget(toolOutputTokenLimit);
            if (budget.MaximumSerializedWeight == 0)
                return CreateResult(
                    outcome,
                    string.Empty,
                    contentTruncated: content.Length > 0);
            var maximumContentBudget = Math.Min(budget.MaximumContentCharacters, content.Length);
            var maximumContent = CompactContent(execution.ToolName, content, maximumContentBudget);
            var maximumContentTruncated = maximumContent.Length < content.Length;
            var maximumSerialized = Serialize(
                outcome,
                maximumContent,
                content.Length,
                maximumContentTruncated);
            if (budget.Fits(maximumSerialized))
                return CreateResult(outcome, maximumSerialized, maximumContentTruncated);

            var lowerBound = 0;
            var upperBound = maximumContentBudget - 1;
            string? best = null;
            var bestContentTruncated = content.Length > 0;
            while (lowerBound <= upperBound)
            {
                var contentBudget = lowerBound + ((upperBound - lowerBound) / 2);
                var compactedContent = CompactContent(execution.ToolName, content, contentBudget);
                var contentTruncated = compactedContent.Length < content.Length;
                var serialized = Serialize(outcome, compactedContent, content.Length, contentTruncated);
                if (budget.Fits(serialized))
                {
                    best = serialized;
                    bestContentTruncated = contentTruncated;
                    lowerBound = contentBudget + 1;
                }
                else
                {
                    upperBound = contentBudget - 1;
                }
            }

            return best != null
                ? CreateResult(outcome, best, bestContentTruncated)
                : CreateResult(
                    outcome,
                    SerializeEssential(outcome, content.Length, budget),
                    contentTruncated: content.Length > 0);
        }

        public static string FormatRejected(string toolName, string error)
        {
            return FormatRejected(toolName, error, string.Empty, CopilotToolFailureKind.None, toolOutputTokenLimit: null);
        }

        public static string FormatRejected(string toolName, string error, string failureCode, CopilotToolFailureKind failureKind)
            => FormatRejected(toolName, error, failureCode, failureKind, toolOutputTokenLimit: null);

        public static string FormatRejected(
            string toolName,
            string error,
            string failureCode,
            CopilotToolFailureKind failureKind,
            int? toolOutputTokenLimit)
        {
            var budget = ResolveBudget(toolOutputTokenLimit);
            if (budget.MaximumSerializedWeight == 0)
                return string.Empty;
            foreach (var textLimits in new[]
            {
                (Tool: 120, Summary: MaxSummaryCharacters, Error: MaxErrorCharacters),
                (Tool: 80, Summary: 200, Error: 400),
                (Tool: 40, Summary: 80, Error: 160),
                (Tool: 24, Summary: 0, Error: 0),
            })
            {
                var serialized = SerializeRejected(
                    toolName,
                    error,
                    failureCode,
                    failureKind,
                    textLimits.Tool,
                    textLimits.Summary,
                    textLimits.Error);
                if (budget.Fits(serialized))
                    return serialized;
            }

            const string fallback = "{\"success\":false}";
            return budget.Fits(fallback) ? fallback : "{}";
        }

        private static string SerializeRejected(
            string toolName,
            string error,
            string failureCode,
            CopilotToolFailureKind failureKind,
            int maximumToolCharacters,
            int maximumSummaryCharacters,
            int maximumErrorCharacters)
        {
            var payload = new Dictionary<string, object?>
            {
                ["tool"] = SanitizeInline(toolName, maximumToolCharacters),
                ["success"] = false,
                ["retry_allowed"] = false,
            };
            if (maximumSummaryCharacters > 0)
                payload["summary"] = SanitizeInline($"{toolName} was not executed.", maximumSummaryCharacters);
            if (maximumErrorCharacters > 0)
                payload["error"] = SanitizeInline(error, maximumErrorCharacters);
            if (failureKind != CopilotToolFailureKind.None)
                payload["failure_kind"] = CopilotToolFailureKindProtocol.Format(failureKind);
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
            var result = outcome.EffectiveModelResult;
            var execution = outcome.Execution ?? new CopilotToolExecutionInfo();
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
                payload["failure_kind"] = CopilotToolFailureKindProtocol.Format(execution.FailureKind);
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
            AddArchiveReference(payload, outcome.ToolOutputArchive);
            if (result.DelegatedRunUsage != null)
            {
                var delegated = result.DelegatedRunUsage;
                var delegatedRun = new Dictionary<string, object?>
                {
                    ["role"] = SanitizeInline(delegated.RoleId, 40),
                    ["run_id"] = SanitizeInline(delegated.RunId, 120),
                    ["stop_reason"] = delegated.StopReason.ToString().ToLowerInvariant(),
                    ["request_token_budget"] = Math.Max(0, delegated.RequestTokenBudget),
                    ["provider_calls"] = Math.Max(0, delegated.ProviderCalls),
                    ["tool_calls"] = Math.Max(0, delegated.ToolCalls),
                    ["steering_delivered"] = Math.Max(0, delegated.DeliveredSteeringCount),
                    ["steering_undelivered"] = Math.Max(0, delegated.UndeliveredSteeringCount),
                    ["queue_ms"] = Math.Max(0, delegated.QueueDurationMs),
                    ["consumed_tokens"] = Math.Max(0, delegated.ConsumedTokens),
                    ["input_tokens"] = Math.Max(0, delegated.Usage.InputTokens),
                    ["output_tokens"] = Math.Max(0, delegated.Usage.OutputTokens),
                    ["total_tokens"] = Math.Max(0, delegated.Usage.EffectiveTotalTokens),
                    ["includes_estimates"] = delegated.UsedEstimatedUsage,
                };
                if (!string.IsNullOrWhiteSpace(delegated.ResumeFromRunId))
                    delegatedRun["resumed_from"] = SanitizeInline(delegated.ResumeFromRunId, 120);
                if (!string.IsNullOrWhiteSpace(delegated.AgentName))
                    delegatedRun["agent"] = SanitizeInline(delegated.AgentName, CopilotCodexCustomSubagentDefinition.MaximumNameCharacters);
                if (!string.IsNullOrWhiteSpace(delegated.Model))
                    delegatedRun["model"] = SanitizeInline(delegated.Model, CopilotConfiguredModelSelection.MaximumModelCharacters);
                if (!string.IsNullOrWhiteSpace(delegated.ReasoningEffort))
                    delegatedRun["reasoning_effort"] = SanitizeInline(delegated.ReasoningEffort, 32);
                payload["delegated_run"] = delegatedRun;
            }

            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        private static string SerializeEssential(
            CopilotToolExecutionOutcome outcome,
            int originalContentCharacters,
            FormatterBudget budget)
        {
            foreach (var limits in new[]
            {
                (Tool: 120, ApprovalAction: 120, IncludeAttempt: true, IncludeCounts: true),
                (Tool: 60, ApprovalAction: 60, IncludeAttempt: true, IncludeCounts: true),
                (Tool: 32, ApprovalAction: 32, IncludeAttempt: false, IncludeCounts: false),
            })
            {
                var result = outcome.EffectiveModelResult;
                var execution = outcome.Execution ?? new CopilotToolExecutionInfo();
                var payload = new Dictionary<string, object?>
                {
                    ["tool"] = SanitizeInline(execution.ToolName, limits.Tool),
                    ["success"] = result.Success,
                    ["retry_allowed"] = execution.RetryEligible,
                };
                if (limits.IncludeAttempt)
                {
                    payload["attempt"] = new Dictionary<string, int>
                    {
                        ["current"] = Math.Max(1, execution.Attempt),
                        ["maximum"] = Math.Max(Math.Max(1, execution.Attempt), execution.MaxAttempts),
                    };
                }
                if (execution.FailureKind != CopilotToolFailureKind.None)
                    payload["failure_kind"] = CopilotToolFailureKindProtocol.Format(execution.FailureKind);
                var failureCode = result.Success ? string.Empty : CopilotToolFailureCode.Normalize(result.FailureCode);
                if (!string.IsNullOrWhiteSpace(failureCode))
                    payload["failure_code"] = failureCode;
                if (result.Approval != null)
                {
                    payload["status"] = "awaiting_approval";
                    payload["approval"] = new Dictionary<string, object?>
                    {
                        ["action_id"] = SanitizeInline(result.Approval.ActionId, limits.ApprovalAction),
                    };
                }
                if (originalContentCharacters > 0)
                {
                    payload["content_truncated"] = true;
                    if (limits.IncludeCounts)
                    {
                        payload["content_original_characters"] = originalContentCharacters;
                        payload["content_returned_characters"] = 0;
                    }
                }
                AddArchiveReference(payload, outcome.ToolOutputArchive);

                var serialized = JsonSerializer.Serialize(payload, JsonOptions);
                if (budget.Fits(serialized))
                    return serialized;
            }

            var successFallback = JsonSerializer.Serialize(
                new Dictionary<string, object?> { ["success"] = outcome.EffectiveModelResult.Success },
                JsonOptions);
            return budget.Fits(successFallback) ? successFallback : "{}";
        }

        private static void AddArchiveReference(
            IDictionary<string, object?> payload,
            CopilotToolOutputArchiveSnapshot? archive)
        {
            if (archive == null)
                return;

            payload["content_archive"] = new Dictionary<string, object?>
            {
                ["archive_id"] = archive.Id,
                ["retrieval_tool"] = CopilotToolOutputArchivePolicy.RetrievalToolName,
                ["archived_characters"] = archive.ArchivedCharacters,
                ["archive_truncated"] = archive.ArchiveTruncated,
                ["content_redacted"] = true,
            };
        }

        private static CopilotFrameworkToolResultFormatResult CreateResult(
            CopilotToolExecutionOutcome outcome,
            string content,
            bool contentTruncated)
        {
            var archive = outcome.ToolOutputArchive;
            return new CopilotFrameworkToolResultFormatResult(
                content,
                contentTruncated,
                IsErrorTruncated(outcome, content),
                archive != null
                    && content.Contains(archive.Id, StringComparison.Ordinal));
        }

        private static bool IsErrorTruncated(
            CopilotToolExecutionOutcome outcome,
            string serialized)
        {
            var fullError = SanitizeInline(
                outcome.EffectiveModelResult.ErrorMessage,
                int.MaxValue);
            if (fullError.Length == 0)
                return false;
            if (serialized.Length == 0)
                return true;

            try
            {
                using var document = JsonDocument.Parse(serialized);
                return !document.RootElement.TryGetProperty("error", out var error)
                    || error.ValueKind != JsonValueKind.String
                    || !string.Equals(
                        error.GetString(),
                        fullError,
                        StringComparison.Ordinal);
            }
            catch (JsonException)
            {
                return true;
            }
        }

        private static FormatterBudget ResolveBudget(int? configuredTokenLimit)
        {
            if (!configuredTokenLimit.HasValue
                || configuredTokenLimit.Value < MinimumConfiguredTokenLimit
                || configuredTokenLimit.Value > MaximumConfiguredTokenLimit)
            {
                return new FormatterBudget(
                    MaxContentCharacters,
                    MaxSerializedCharacters,
                    MaximumSerializedWeight: null);
            }

            var maximumWeight = (long)configuredTokenLimit.Value * CopilotTokenEstimator.AsciiCharactersPerToken;
            return new FormatterBudget(
                Math.Min(MaximumConfiguredContentCharacters, (int)Math.Min(int.MaxValue, maximumWeight)),
                Math.Min(MaximumConfiguredSerializedCharacters, (int)Math.Min(int.MaxValue, maximumWeight)),
                maximumWeight);
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
                CopilotSharedAgentToolNames.ReadLocalFile => FileSectionRegex,
                "ReadAttachedFile" => AttachmentFileSectionRegex,
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
                    section = TakePrefixWithoutSplittingSurrogatePair(section, remainingCharacters);
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
                return TakePrefixWithoutSplittingSurrogatePair(value, maximumCharacters);

            var availableCharacters = maximumCharacters - marker.Length;
            var tailCharacters = (int)Math.Round(availableCharacters * Math.Clamp(tailRatio, 0.05, 0.9));
            var headCharacters = availableCharacters - tailCharacters;
            var safeHeadLength = GetSafePrefixLength(value, headCharacters);
            var safeTailStart = GetSafeSuffixStart(value, tailCharacters);
            return value[..safeHeadLength] + marker + value[safeTailStart..];
        }

        private static string TakePrefixWithoutSplittingSurrogatePair(string value, int maximumCharacters)
        {
            var safeLength = GetSafePrefixLength(value, maximumCharacters);
            return value[..safeLength];
        }

        private static int GetSafePrefixLength(string value, int maximumCharacters)
        {
            var length = Math.Clamp(maximumCharacters, 0, value.Length);
            return length > 0
                && length < value.Length
                && char.IsHighSurrogate(value[length - 1])
                && char.IsLowSurrogate(value[length])
                    ? length - 1
                    : length;
        }

        private static int GetSafeSuffixStart(string value, int maximumCharacters)
        {
            var start = value.Length - Math.Clamp(maximumCharacters, 0, value.Length);
            return start > 0
                && start < value.Length
                && char.IsHighSurrogate(value[start - 1])
                && char.IsLowSurrogate(value[start])
                    ? start + 1
                    : start;
        }

        private static double GetTailRatio(string toolName)
        {
            return toolName switch
            {
                CopilotSharedAgentToolNames.GetRecentLog => 0.7,
                "RunWorkspaceValidation" => 0.7,
                "RunShellCommand" => 0.7,
                "FetchUrl" => 0.12,
                CopilotSharedAgentToolNames.ReadLocalFile or "ReadAttachedFile" => 0.2,
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
            if (maximumCharacters <= 0)
                return string.Empty;
            var text = SanitizeMultiline(value)
                .Replace('\r', ' ')
                .Replace('\n', ' ');
            while (text.Contains("  ", StringComparison.Ordinal))
                text = text.Replace("  ", " ", StringComparison.Ordinal);
            return text.Length <= maximumCharacters
                ? text
                : TakePrefixWithoutSplittingSurrogatePair(text, Math.Max(0, maximumCharacters - 3)) + "...";
        }

        private readonly record struct FormatterBudget(
            int MaximumContentCharacters,
            int MaximumSerializedCharacters,
            long? MaximumSerializedWeight)
        {
            public bool Fits(string value)
            {
                return value.Length <= MaximumSerializedCharacters
                    && (!MaximumSerializedWeight.HasValue
                        || CopilotTokenEstimator.EstimateTextWeight(value) <= MaximumSerializedWeight.Value);
            }
        }
    }
}
