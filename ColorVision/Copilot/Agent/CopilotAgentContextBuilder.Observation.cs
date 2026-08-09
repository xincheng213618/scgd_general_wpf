#pragma warning disable CA1822,CA1859,CA1861
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using ColorVision.UI;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotAgentContextBuilder
    {
        private static string[] BuildObservationContentExcerpts(
            IReadOnlyList<CopilotAgentStepRecord> steps,
            bool includeContent,
            int maxContentChars,
            int maxTotalContentChars)
        {
            var excerpts = new string[steps.Count];
            if (!includeContent || maxTotalContentChars <= 0)
                return excerpts;

            var remainingCharacters = maxTotalContentChars;
            for (var index = steps.Count - 1; index >= 0 && remainingCharacters > 0; index--)
            {
                var content = steps[index].EffectiveModelObservation.Content.TrimEnd();
                if (content.Length == 0)
                    continue;

                var limit = Math.Min(maxContentChars, remainingCharacters);
                excerpts[index] = SerializeObservationContentToMaximum(steps[index], content, limit);
                remainingCharacters -= excerpts[index].Length;
            }

            return excerpts;
        }

        private static string SerializeObservationContentToMaximum(
            CopilotAgentStepRecord step,
            string content,
            int maxCharacters)
        {
            var serialized = JsonSerializer.Serialize(content);
            if (serialized.Length <= maxCharacters)
                return serialized;

            var observation = step.EffectiveModelObservation;
            if (!string.Equals(step.ToolCall?.ToolName, "ReadLocalFile", StringComparison.OrdinalIgnoreCase)
                || observation.AttemptedLocalFilePaths.Count < 2
                || !TrySplitLocalFileObservation(
                    content,
                    observation.AttemptedLocalFilePaths,
                    out var fileSections))
            {
                return SerializeContentToMaximum(content, maxCharacters);
            }

            return SerializeBalancedLocalFileSectionsToMaximum(fileSections, maxCharacters);
        }

        private static bool TrySplitLocalFileObservation(
            string content,
            IReadOnlyList<string> attemptedPaths,
            out string[] fileSections)
        {
            fileSections = Array.Empty<string>();
            var sectionStarts = new int[attemptedPaths.Count];
            var searchIndex = 0;
            for (var index = 0; index < attemptedPaths.Count; index++)
            {
                var marker = "[File] " + attemptedPaths[index];
                var markerIndex = FindLineMarker(content, marker, searchIndex);
                if (markerIndex < 0 || (index == 0 && markerIndex != 0))
                    return false;

                sectionStarts[index] = markerIndex;
                searchIndex = markerIndex + marker.Length;
            }

            var sections = new string[sectionStarts.Length];
            for (var index = 0; index < sectionStarts.Length; index++)
            {
                var end = index + 1 < sectionStarts.Length
                    ? sectionStarts[index + 1]
                    : content.Length;
                sections[index] = content[sectionStarts[index]..end].TrimEnd();
            }

            fileSections = sections;
            return sections.All(section => !string.IsNullOrWhiteSpace(section));
        }

        private static int FindLineMarker(string content, string marker, int startIndex)
        {
            var markerIndex = Math.Max(0, startIndex);
            while (markerIndex < content.Length)
            {
                markerIndex = content.IndexOf(marker, markerIndex, StringComparison.OrdinalIgnoreCase);
                if (markerIndex < 0)
                    return -1;

                var startsLine = markerIndex == 0 || content[markerIndex - 1] == '\n';
                var markerEnd = markerIndex + marker.Length;
                var endsLine = markerEnd == content.Length
                    || content[markerEnd] == '\r'
                    || content[markerEnd] == '\n';
                if (startsLine && endsLine)
                    return markerIndex;

                markerIndex++;
            }

            return -1;
        }

        private static string SerializeBalancedLocalFileSectionsToMaximum(
            IReadOnlyList<string> fileSections,
            int maxCharacters)
        {
            var lowerBound = 0;
            var upperBound = fileSections.Max(section => section.Length);
            var best = string.Empty;
            while (lowerBound <= upperBound)
            {
                var perSectionCharacters = lowerBound + (upperBound - lowerBound) / 2;
                var candidate = string.Join(
                    Environment.NewLine + Environment.NewLine,
                    fileSections.Select(section => BuildBalancedLocalFileSection(section, perSectionCharacters)));
                var serialized = JsonSerializer.Serialize(candidate);
                if (serialized.Length <= maxCharacters)
                {
                    best = serialized;
                    lowerBound = perSectionCharacters + 1;
                }
                else
                {
                    upperBound = perSectionCharacters - 1;
                }
            }

            return string.IsNullOrEmpty(best)
                ? SerializeContentToMaximum(string.Join(Environment.NewLine + Environment.NewLine, fileSections), maxCharacters)
                : best;
        }

        private static string BuildBalancedLocalFileSection(string section, int maxCharacters)
        {
            if (maxCharacters <= 0 || string.IsNullOrEmpty(section))
                return string.Empty;
            if (section.Length <= maxCharacters)
                return section;

            var separatorCharacters = Environment.NewLine.Length * 2;
            var retainedCharacters = maxCharacters - BalancedBatchOmissionMarker.Length - separatorCharacters;
            if (retainedCharacters <= 0)
                return section[..GetSafePrefixLength(section, maxCharacters)];

            var prefix = RetainPrefixAtLineBoundary(section, (retainedCharacters + 1) / 2);
            var suffix = RetainSuffixAtLineBoundary(section, retainedCharacters - prefix.Length);
            return prefix
                + Environment.NewLine
                + BalancedBatchOmissionMarker
                + Environment.NewLine
                + suffix;
        }

        private static string RetainPrefixAtLineBoundary(string value, int maxCharacters)
        {
            var retainedLength = GetSafePrefixLength(value, maxCharacters);
            if (retainedLength >= value.Length)
                return value;

            var lineEnd = value.LastIndexOf('\n', retainedLength - 1, retainedLength);
            if (lineEnd <= 0)
                return value[..retainedLength];

            return value[..lineEnd].TrimEnd('\r', '\n');
        }

        private static string RetainSuffixAtLineBoundary(string value, int maxCharacters)
        {
            if (maxCharacters <= 0)
                return string.Empty;
            if (maxCharacters >= value.Length)
                return value;

            var startIndex = value.Length - maxCharacters;
            if (startIndex > 0
                && startIndex < value.Length
                && char.IsHighSurrogate(value[startIndex - 1])
                && char.IsLowSurrogate(value[startIndex]))
            {
                startIndex++;
            }

            var lineStart = value.IndexOf('\n', startIndex);
            if (lineStart < 0 || lineStart + 1 >= value.Length)
                return value[startIndex..];

            return value[(lineStart + 1)..].TrimStart('\r', '\n');
        }

        private static string TruncateInlineText(string value, int maxCharacters)
        {
            var normalized = string.Join(" ", (value ?? string.Empty)
                .Split(new[] { "\r\n", "\n", "\r" }, StringSplitOptions.RemoveEmptyEntries))
                .Trim();
            if (normalized.Length <= maxCharacters)
                return normalized;
            if (maxCharacters <= 1)
                return maxCharacters == 1 ? "…" : string.Empty;
            return normalized[..(maxCharacters - 1)] + "…";
        }

        private static string SerializeContentToMaximum(string value, int maxCharacters)
        {
            var content = value ?? string.Empty;
            if (maxCharacters <= 0 || content.Length == 0)
                return string.Empty;

            var serialized = JsonSerializer.Serialize(content);
            if (serialized.Length <= maxCharacters)
                return serialized;

            const string marker = "\n...<content truncated.>";
            var best = string.Empty;
            var lowerBound = 0;
            var upperBound = content.Length;
            while (lowerBound <= upperBound)
            {
                var length = lowerBound + (upperBound - lowerBound) / 2;
                var candidate = JsonSerializer.Serialize(content[..length] + marker);
                if (candidate.Length <= maxCharacters)
                {
                    best = candidate;
                    lowerBound = length + 1;
                }
                else
                {
                    upperBound = length - 1;
                }
            }

            return best;
        }

        private static string TruncateContent(string value, int maxCharacters)
        {
            var content = value ?? string.Empty;
            if (content.Length <= maxCharacters)
                return content;

            var retainedLength = GetSafePrefixLength(content, maxCharacters);
            return content[..retainedLength] + Environment.NewLine + $"...<content truncated; kept the first {retainedLength} characters.>";
        }

        private static int GetSafePrefixLength(string value, int maximumLength)
        {
            var retainedLength = Math.Clamp(maximumLength, 0, value.Length);
            if (retainedLength > 0
                && retainedLength < value.Length
                && char.IsHighSurrogate(value[retainedLength - 1])
                && char.IsLowSurrogate(value[retainedLength]))
            {
                retainedLength--;
            }

            return retainedLength;
        }
    }
}
