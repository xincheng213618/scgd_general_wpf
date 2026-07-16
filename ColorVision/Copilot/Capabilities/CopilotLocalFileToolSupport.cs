#pragma warning disable CA2016,CA2024
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public readonly record struct CopilotLocalFileReadResult(
        string FullPath,
        bool Success,
        bool WasTruncated,
        string Content,
        string ErrorMessage,
        int StartLine,
        int StartColumn,
        int EndLine,
        int EndColumn,
        int ContinuationStartLine,
        int ContinuationStartColumn);

    public static class CopilotLocalFileToolSupport
    {
        private const int BinaryPreviewBytes = 4096;
        public const int MaxReadCharacters = 20000;

        private static readonly Regex QuotedWindowsPathRegex = new("[\"\\u201C](?<path>[A-Za-z]:\\\\[^\"\\u201D\r\n]+)[\"\\u201D]", RegexOptions.Compiled);
        private static readonly Regex BareWindowsPathRegex = new(@"(?<path>[A-Za-z]:\\[^\s""\u201C\u201D<>|]+)", RegexOptions.Compiled);
        private static readonly char[] PathTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '\uFF0C', '\u3002', '\uFF1B', '\uFF1A', '\uFF01', '\uFF1F', '\uFF09', '\u3011', '\u300B', '\u3001' };

        public static IReadOnlyList<string> ExtractExplicitLocalFilePaths(string text)
        {
            var results = new List<string>();
            if (string.IsNullOrWhiteSpace(text))
                return results;

            AddMatches(results, QuotedWindowsPathRegex.Matches(text));
            AddMatches(results, BareWindowsPathRegex.Matches(text));
            return results;
        }

        public static Task<CopilotLocalFileReadResult> ReadTextFileAsync(string path, CancellationToken cancellationToken)
        {
            return ReadTextFileAsync(path, null, null, null, cancellationToken);
        }

        public static Task<CopilotLocalFileReadResult> ReadTextFileAsync(
            string path,
            int? startLine,
            int? endLine,
            CancellationToken cancellationToken)
        {
            return ReadTextFileAsync(path, startLine, startColumn: null, endLine, cancellationToken);
        }

        public static async Task<CopilotLocalFileReadResult> ReadTextFileAsync(
            string path,
            int? startLine,
            int? startColumn,
            int? endLine,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new CopilotLocalFileReadResult(
                    string.Empty,
                    false,
                    false,
                    string.Empty,
                    "File path is empty.",
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            string fullPath;
            try
            {
                fullPath = Path.GetFullPath(path);
            }
            catch (Exception ex)
            {
                return new CopilotLocalFileReadResult(
                    path,
                    false,
                    false,
                    string.Empty,
                    $"Invalid path format: {ex.Message}",
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            if (Directory.Exists(fullPath))
            {
                return new CopilotLocalFileReadResult(
                    fullPath,
                    false,
                    false,
                    string.Empty,
                    "The target path is a directory, not a file.",
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            if (!File.Exists(fullPath))
            {
                return new CopilotLocalFileReadResult(
                    fullPath,
                    false,
                    false,
                    string.Empty,
                    "File does not exist.",
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }

            try
            {
                await using var stream = new FileStream(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                var previewLength = (int)Math.Min(BinaryPreviewBytes, stream.Length);
                var previewBuffer = new byte[previewLength];
                var previewRead = await stream.ReadAsync(previewBuffer.AsMemory(0, previewLength), cancellationToken);

                if (previewBuffer.AsSpan(0, previewRead).IndexOf((byte)0) >= 0)
                {
                    return new CopilotLocalFileReadResult(
                        fullPath,
                        false,
                        false,
                        string.Empty,
                        "The target file does not appear to be a directly readable text file.",
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                }

                stream.Position = 0;
                var normalizedStartLine = Math.Max(1, startLine ?? 1);
                var normalizedStartColumn = Math.Max(1, startColumn ?? 1);
                var normalizedEndLine = endLine.HasValue
                    ? Math.Max(normalizedStartLine, endLine.Value)
                    : int.MaxValue;
                using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
                var range = await ReadBoundedRangeAsync(
                    reader,
                    normalizedStartLine,
                    normalizedStartColumn,
                    normalizedEndLine,
                    cancellationToken);

                if (range.ActualStartLine == 0 && range.TotalLineCount < normalizedStartLine)
                {
                    return new CopilotLocalFileReadResult(
                        fullPath,
                        false,
                        false,
                        string.Empty,
                        $"Requested start line {normalizedStartLine} is beyond the total file line count.",
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                }

                if (range.ActualStartLine == 0 && normalizedStartColumn > 1)
                {
                    return new CopilotLocalFileReadResult(
                        fullPath,
                        false,
                        false,
                        string.Empty,
                        $"Requested start column {normalizedStartColumn} is beyond line {normalizedStartLine}.",
                        0,
                        0,
                        0,
                        0,
                        0,
                        0);
                }

                var content = range.Content.TrimEnd();

                if (range.WasTruncated)
                {
                    content += Environment.NewLine + $"...<content truncated; kept the first {MaxReadCharacters} characters.>";
                }

                return new CopilotLocalFileReadResult(
                    fullPath,
                    true,
                    range.WasTruncated,
                    content.TrimEnd(),
                    string.Empty,
                    range.ActualStartLine,
                    range.ActualStartColumn,
                    range.ActualEndLine,
                    range.ActualEndColumn,
                    range.ContinuationStartLine,
                    range.ContinuationStartColumn);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CopilotLocalFileReadResult(
                    fullPath,
                    false,
                    false,
                    string.Empty,
                    $"Read failed: {ex.Message}",
                    0,
                    0,
                    0,
                    0,
                    0,
                    0);
            }
        }

        private static async Task<BoundedTextRange> ReadBoundedRangeAsync(
            StreamReader reader,
            int startLine,
            int startColumn,
            int endLine,
            CancellationToken cancellationToken)
        {
            var builder = new System.Text.StringBuilder(MaxReadCharacters);
            var buffer = new char[4096];
            var currentLine = 1;
            var currentColumn = 1;
            var totalLineCount = 0;
            var actualStartLine = 0;
            var actualStartColumn = 0;
            var actualEndLine = 0;
            var actualEndColumn = 0;
            var continuationStartLine = 0;
            var continuationStartColumn = 0;
            var hasCharactersSinceLastLineFeed = false;
            var wasTruncated = false;
            var reachedRequestedEnd = false;

            while (!wasTruncated && !reachedRequestedEnd)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                    break;

                for (var index = 0; index < read; index++)
                {
                    var character = buffer[index];
                    hasCharactersSinceLastLineFeed = true;
                    var isWithinRequestedRange = currentLine >= startLine
                        && currentLine <= endLine
                        && (currentLine > startLine || currentColumn >= startColumn);
                    if (isWithinRequestedRange)
                    {
                        if (builder.Length >= MaxReadCharacters)
                        {
                            wasTruncated = true;
                            continuationStartLine = currentLine;
                            continuationStartColumn = currentColumn;
                            break;
                        }
                        if (actualStartLine == 0)
                        {
                            actualStartLine = currentLine;
                            actualStartColumn = currentColumn;
                        }
                        actualEndLine = currentLine;
                        actualEndColumn = currentColumn;
                        builder.Append(character);
                    }

                    if (character != '\n')
                    {
                        currentColumn++;
                        continue;
                    }

                    totalLineCount = currentLine;
                    hasCharactersSinceLastLineFeed = false;
                    currentLine++;
                    currentColumn = 1;
                    if (currentLine > endLine)
                    {
                        reachedRequestedEnd = true;
                        break;
                    }
                }
            }

            if (!reachedRequestedEnd && !wasTruncated && hasCharactersSinceLastLineFeed)
                totalLineCount = currentLine;

            return new BoundedTextRange(
                builder.ToString(),
                wasTruncated,
                actualStartLine,
                actualStartColumn,
                actualEndLine,
                actualEndColumn,
                totalLineCount,
                continuationStartLine,
                continuationStartColumn);
        }

        private readonly record struct BoundedTextRange(
            string Content,
            bool WasTruncated,
            int ActualStartLine,
            int ActualStartColumn,
            int ActualEndLine,
            int ActualEndColumn,
            int TotalLineCount,
            int ContinuationStartLine,
            int ContinuationStartColumn);

        private static void AddMatches(List<string> results, MatchCollection matches)
        {
            foreach (Match match in matches)
            {
                var candidate = NormalizeLocalFilePath(match.Groups["path"].Value);
                if (string.IsNullOrWhiteSpace(candidate))
                    continue;

                if (!results.Contains(candidate, StringComparer.OrdinalIgnoreCase))
                    results.Add(candidate);
            }
        }

        private static string NormalizeLocalFilePath(string value)
        {
            var candidate = (value ?? string.Empty).Trim().Trim(PathTrimCharacters);
            if (string.IsNullOrWhiteSpace(candidate))
                return string.Empty;

            try
            {
                return Path.GetFullPath(candidate);
            }
            catch
            {
                return candidate;
            }
        }
    }
}
