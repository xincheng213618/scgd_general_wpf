using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace ColorVision.Copilot
{
    internal static class CopilotSubagentEvidencePolicy
    {
        internal static IReadOnlyList<string> FindUnobservedWorkspaceFileCitations(
            CopilotSubagentRoleDescriptor role,
            IReadOnlyList<CopilotAgentStepRecord> steps,
            string answer)
        {
            ArgumentNullException.ThrowIfNull(role);
            if (role.ContextScope != CopilotSubagentContextScope.WorkspaceReadOnly
                || !role.ReadCapabilities.HasFlag(CopilotSubagentReadCapabilities.ReadLocalFile)
                || string.IsNullOrWhiteSpace(answer))
            {
                return Array.Empty<string>();
            }

            var successfulReadSteps = (steps ?? Array.Empty<CopilotAgentStepRecord>())
                .Where(step =>
                    step?.Observation?.Success == true
                    && string.Equals(step.ToolCall?.ToolName, CopilotSharedAgentToolNames.ReadLocalFile, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            var successfullyReadPaths = successfulReadSteps
                .SelectMany(step => step.Observation.SuccessfullyReadLocalFilePaths ?? Array.Empty<string>())
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
            var readScopesByPath = successfulReadSteps
                .SelectMany(step => step.Observation.LocalFileReadScopes ?? Array.Empty<CopilotLocalFileReadScope>())
                .Where(scope => scope != null)
                .Select(scope => new
                {
                    Path = NormalizePath(scope.Path),
                    Scope = scope,
                })
                .Where(item => !string.IsNullOrWhiteSpace(item.Path)
                    && item.Scope.StartLine > 0
                    && item.Scope.EndLine >= item.Scope.StartLine)
                .GroupBy(item => item.Path, StringComparer.OrdinalIgnoreCase)
                .ToDictionary(
                    group => group.Key,
                    group => group.Select(item => item.Scope).ToArray(),
                    StringComparer.OrdinalIgnoreCase);

            var unobservedCitations = new List<string>();
            foreach (var path in CopilotLocalFileToolSupport.ExtractExplicitLocalFilePaths(answer)
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path) && !Directory.Exists(path))
                .Distinct(StringComparer.OrdinalIgnoreCase))
            {
                if (!successfullyReadPaths.Contains(path))
                {
                    unobservedCitations.Add(path);
                    continue;
                }

                var citedLineRanges = ExtractCitedLineRanges(answer, path);
                if (citedLineRanges.Length == 0)
                    continue;

                readScopesByPath.TryGetValue(path, out var readScopes);
                foreach (var citedRange in citedLineRanges)
                {
                    if (readScopes?.Any(scope =>
                        scope.StartLine <= citedRange.StartLine
                        && scope.EndLine >= citedRange.EndLine) == true)
                    {
                        continue;
                    }

                    unobservedCitations.Add(FormatCitation(path, citedRange));
                }
            }

            return unobservedCitations
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        private static CitedLineRange[] ExtractCitedLineRanges(string answer, string path)
        {
            var normalizedAnswer = answer.Replace('/', '\\');
            var normalizedPath = path.Replace('/', '\\');
            var ranges = new List<CitedLineRange>();
            var searchIndex = 0;
            while (searchIndex < normalizedAnswer.Length)
            {
                var pathIndex = normalizedAnswer.IndexOf(normalizedPath, searchIndex, StringComparison.OrdinalIgnoreCase);
                if (pathIndex < 0)
                    break;

                var suffixIndex = pathIndex + normalizedPath.Length;
                searchIndex = suffixIndex;
                if (suffixIndex >= normalizedAnswer.Length || normalizedAnswer[suffixIndex] != ':')
                    continue;

                var startDigits = suffixIndex + 1;
                var cursor = startDigits;
                while (cursor < normalizedAnswer.Length && char.IsAsciiDigit(normalizedAnswer[cursor]))
                    cursor++;
                if (cursor == startDigits
                    || !int.TryParse(normalizedAnswer.AsSpan(startDigits, cursor - startDigits), out var startLine)
                    || startLine < 1)
                {
                    continue;
                }

                var endLine = startLine;
                if (cursor < normalizedAnswer.Length && normalizedAnswer[cursor] == '-')
                {
                    var endDigits = cursor + 1;
                    cursor = endDigits;
                    while (cursor < normalizedAnswer.Length && char.IsAsciiDigit(normalizedAnswer[cursor]))
                        cursor++;
                    if (cursor == endDigits
                        || !int.TryParse(normalizedAnswer.AsSpan(endDigits, cursor - endDigits), out endLine)
                        || endLine < startLine)
                    {
                        continue;
                    }
                }

                ranges.Add(new CitedLineRange(startLine, endLine));
                searchIndex = cursor;
            }

            return ranges.Distinct().ToArray();
        }

        private static string FormatCitation(string path, CitedLineRange range)
        {
            return range.StartLine == range.EndLine
                ? $"{path}:{range.StartLine}"
                : $"{path}:{range.StartLine}-{range.EndLine}";
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path.Trim().Trim('`', '*', '_'));
            }
            catch
            {
                return string.Empty;
            }
        }

        private readonly record struct CitedLineRange(int StartLine, int EndLine);
    }
}
