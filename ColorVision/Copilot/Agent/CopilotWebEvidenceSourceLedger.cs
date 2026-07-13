using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ColorVision.Copilot
{
    public static class CopilotWebEvidenceSourceLedger
    {
        public const int MaxAppendedSources = 3;
        private static readonly Regex FetchedPageUrlRegex = new(
            @"(?m)^\[Web Page Fetched\]\s+(?<url>https?://\S+)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);
        private static readonly Regex SearchResultUrlRegex = new(
            @"(?m)^\s*URL:\s*(?<url>https?://\S+)\s*$",
            RegexOptions.Compiled | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        public static string BuildMissingSourceAppendix(
            IReadOnlyList<CopilotAgentStepRecord> steps,
            IReadOnlyList<ICopilotTool> availableTools,
            string answer)
        {
            if (string.IsNullOrWhiteSpace(answer))
                return string.Empty;

            var webToolNames = (availableTools ?? Array.Empty<ICopilotTool>())
                .Where(CopilotToolIntentPolicy.IsWebEvidenceTool)
                .Select(tool => tool.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (webToolNames.Count == 0)
                return string.Empty;

            var evidenceUrls = new List<string>();
            foreach (var step in steps ?? Array.Empty<CopilotAgentStepRecord>())
            {
                var toolName = string.IsNullOrWhiteSpace(step?.Execution.ToolName)
                    ? step?.ToolCall.ToolName ?? string.Empty
                    : step.Execution.ToolName;
                if (step?.Observation.Success != true
                    || !webToolNames.Contains(toolName)
                    || string.IsNullOrWhiteSpace(step.Observation.Content))
                {
                    continue;
                }

                foreach (var url in ExtractEvidenceUrls(toolName, step.Observation.Content))
                {
                    var normalized = NormalizePublicSourceUrl(url);
                    if (!string.IsNullOrWhiteSpace(normalized)
                        && !evidenceUrls.Contains(normalized, StringComparer.OrdinalIgnoreCase))
                    {
                        evidenceUrls.Add(normalized);
                    }
                }
            }

            if (evidenceUrls.Count == 0)
                return string.Empty;

            var citedUrls = CopilotWebPageToolSupport.ExtractHttpUrls(answer)
                .Select(NormalizePublicSourceUrl)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (evidenceUrls.Any(citedUrls.Contains))
                return string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("来源：");
            foreach (var url in evidenceUrls.Take(MaxAppendedSources))
                builder.Append("- <").Append(url).AppendLine(">");
            return builder.ToString().TrimEnd();
        }

        private static IReadOnlyList<string> ExtractEvidenceUrls(string toolName, string content)
        {
            var matches = string.Equals(toolName, "FetchUrl", StringComparison.OrdinalIgnoreCase)
                ? FetchedPageUrlRegex.Matches(content)
                : string.Equals(toolName, "WebSearch", StringComparison.OrdinalIgnoreCase)
                    ? SearchResultUrlRegex.Matches(content)
                    : null;
            if (matches == null)
                return CopilotWebPageToolSupport.ExtractHttpUrls(content);

            return matches.Cast<Match>()
                .Select(match => match.Groups["url"].Value)
                .Where(url => !string.IsNullOrWhiteSpace(url))
                .ToArray();
        }

        private static string NormalizePublicSourceUrl(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri)
                || (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    && !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                || uri.IsLoopback
                || string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || !string.IsNullOrWhiteSpace(uri.UserInfo))
            {
                return string.Empty;
            }

            return uri.AbsoluteUri;
        }
    }
}
