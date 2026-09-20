#pragma warning disable CA2016,CA2024
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
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
        int ContinuationStartColumn)
    {
        public int? ObservedTotalLineCount { get; init; }
    }

    public static class CopilotLocalFileToolSupport
    {
        internal const int BinaryPreviewBytes = 4096;
        internal const string InvalidEncodingMessage = "The file encoding is not supported or contains invalid text bytes. Use UTF-8 or BOM-marked UTF-16/UTF-32.";
        internal const int MinimumReadCharacters = 1_000;
        public const int MaxReadCharacters = 20000;

        private static readonly Regex QuotedWindowsPathRegex = new("[\"\\u201C](?<path>[A-Za-z]:[\\\\/][^\"\\u201D\r\n]+)[\"\\u201D]", RegexOptions.Compiled);
        private static readonly Regex BareWindowsPathRegex = new(
            @"(?<![""\u201C])(?<path>[A-Za-z]:[\\/][^\s""\u201C\u201D<>|,:;!?)}\]\uFF0C\u3002\uFF1B\uFF1A\uFF01\uFF1F\uFF09\u3011\u300B\u3001]+)",
            RegexOptions.Compiled);
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

        public static Task<CopilotLocalFileReadResult> ReadTextFileAsync(
            string path,
            int? startLine,
            int? startColumn,
            int? endLine,
            CancellationToken cancellationToken)
        {
            return ReadTextFileAsync(
                path,
                startLine,
                startColumn,
                endLine,
                MaxReadCharacters,
                cancellationToken);
        }

        internal static async Task<CopilotLocalFileReadResult> ReadTextFileAsync(
            string path,
            int? startLine,
            int? startColumn,
            int? endLine,
            int maximumReadCharacters,
            CancellationToken cancellationToken)
        {
            if (maximumReadCharacters is < MinimumReadCharacters or > MaxReadCharacters)
                throw new ArgumentOutOfRangeException(nameof(maximumReadCharacters));

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

            if (CopilotWorkspaceSearchSupport.HasReparsePointInPath(fullPath))
            {
                return new CopilotLocalFileReadResult(
                    fullPath,
                    false,
                    false,
                    string.Empty,
                    "Reading through a file-system reparse point is not allowed.",
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
                var previewRead = await stream.ReadAtLeastAsync(previewBuffer.AsMemory(), previewLength, throwOnEndOfStream: false, cancellationToken);
                using var reader = CreateTextReader(stream, previewBuffer.AsSpan(0, previewRead));
                var normalizedStartLine = Math.Max(1, startLine ?? 1);
                var normalizedStartColumn = Math.Max(1, startColumn ?? 1);
                var normalizedEndLine = endLine.HasValue
                    ? Math.Max(normalizedStartLine, endLine.Value)
                    : int.MaxValue;
                var range = await ReadBoundedRangeAsync(
                    reader,
                    normalizedStartLine,
                    normalizedStartColumn,
                    normalizedEndLine,
                    maximumReadCharacters,
                    cancellationToken);

                if (range.ActualStartLine == 0 && range.TotalLineCount < normalizedStartLine)
                {
                    return new CopilotLocalFileReadResult(
                        fullPath,
                        false,
                        false,
                        string.Empty,
                        $"Requested start line {normalizedStartLine} is beyond the total file line count ({range.TotalLineCount} at read time).",
                        0,
                        0,
                        0,
                        0,
                        0,
                        0)
                    {
                        ObservedTotalLineCount = range.ReachedEndOfFile ? range.TotalLineCount : null,
                    };
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
                    content += Environment.NewLine + $"...<content truncated; kept the first {range.Content.Length} characters.>";
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
                    range.ContinuationStartColumn)
                {
                    ObservedTotalLineCount = range.ReachedEndOfFile ? range.TotalLineCount : null,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (DecoderFallbackException)
            {
                return new CopilotLocalFileReadResult(fullPath, false, false, string.Empty,
                    InvalidEncodingMessage,
                    0, 0, 0, 0, 0, 0);
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

        internal static StreamReader CreateTextReader(FileStream stream, ReadOnlySpan<byte> preview)
        {
            var encoding = ResolveTextEncoding(preview, out var preambleLength);
            if (preambleLength == 0 && preview.IndexOf((byte)0) >= 0)
                throw new InvalidDataException("The file contains NUL bytes and appears to be binary.");

            stream.Position = preambleLength;
            // Disable automatic detection so StreamReader cannot replace the strict decoder.
            return new StreamReader(stream, encoding, detectEncodingFromByteOrderMarks: false);
        }

        private static Encoding ResolveTextEncoding(ReadOnlySpan<byte> preview, out int preambleLength)
        {
            // UTF-32 LE shares UTF-16 LE's first two BOM bytes, so check it first.
            if (preview is [0xFF, 0xFE, 0x00, 0x00, ..])
            {
                preambleLength = 4;
                return new UTF32Encoding(false, false, true);
            }
            if (preview is [0x00, 0x00, 0xFE, 0xFF, ..])
            {
                preambleLength = 4;
                return new UTF32Encoding(true, false, true);
            }
            if (preview is [0xFF, 0xFE, ..])
            {
                preambleLength = 2;
                return new UnicodeEncoding(false, false, true);
            }
            if (preview is [0xFE, 0xFF, ..])
            {
                preambleLength = 2;
                return new UnicodeEncoding(true, false, true);
            }
            preambleLength = preview is [0xEF, 0xBB, 0xBF, ..] ? 3 : 0;
            return new UTF8Encoding(false, true);
        }

        private static async Task<BoundedTextRange> ReadBoundedRangeAsync(
            StreamReader reader,
            int startLine,
            int startColumn,
            int endLine,
            int maximumReadCharacters,
            CancellationToken cancellationToken)
        {
            var builder = new System.Text.StringBuilder(maximumReadCharacters);
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
            var hasCharactersOnLine = false;
            var pendingCarriageReturn = false;
            var wasTruncated = false;
            var reachedRequestedEnd = false;
            var reachedEndOfFile = false;

            void FinishLine()
            {
                var startColumnWasNotReached = currentLine == startLine && startColumn > 1 && actualStartLine == 0;
                totalLineCount = currentLine;
                hasCharactersOnLine = false;
                pendingCarriageReturn = false;
                currentLine++;
                currentColumn = 1;
                reachedRequestedEnd = currentLine > endLine || startColumnWasNotReached;
            }

            while (!wasTruncated && !reachedRequestedEnd)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken);
                if (read == 0)
                {
                    reachedEndOfFile = true;
                    break;
                }

                for (var index = 0; index < read; index++)
                {
                    var character = buffer[index];
                    // Defer CR until the next character, including across read buffers, so
                    // CRLF belongs to one source line while a lone CR also ends a line.
                    if (pendingCarriageReturn && character != '\n')
                    {
                        FinishLine();
                        if (reachedRequestedEnd)
                            break;
                    }
                    if (character == '\0')
                        throw new InvalidDataException("The file contains NUL characters and appears to be binary.");
                    hasCharactersOnLine = true;
                    var isWithinRequestedRange = currentLine >= startLine
                        && currentLine <= endLine
                        && (currentLine > startLine || currentColumn >= startColumn);
                    if (isWithinRequestedRange)
                    {
                        if (builder.Length >= maximumReadCharacters
                            || builder.Length == maximumReadCharacters - 1 && (char.IsHighSurrogate(character) || character == '\r'))
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
                        pendingCarriageReturn = character == '\r';
                        currentColumn++;
                        continue;
                    }

                    FinishLine();
                    if (reachedRequestedEnd)
                        break;
                }
            }

            if (!reachedRequestedEnd && !wasTruncated && hasCharactersOnLine)
                totalLineCount = currentLine;

            return new BoundedTextRange(
                builder.ToString(),
                wasTruncated,
                actualStartLine,
                actualStartColumn,
                actualEndLine,
                actualEndColumn,
                totalLineCount,
                reachedEndOfFile,
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
            bool ReachedEndOfFile,
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
