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
        int EndLine);

    public static class CopilotLocalFileToolSupport
    {
        private const int BinaryPreviewBytes = 4096;
        public const int MaxReadCharacters = 20000;

        private static readonly Regex QuotedWindowsPathRegex = new(@"[""“](?<path>[A-Za-z]:\\[^""”\r\n]+)[""”]", RegexOptions.Compiled);
        private static readonly Regex BareWindowsPathRegex = new(@"(?<path>[A-Za-z]:\\[^\s""“”<>|]+)", RegexOptions.Compiled);
        private static readonly char[] PathTrimCharacters = { '.', ',', ';', ':', '!', '?', ')', ']', '}', '>', '"', '\'', '，', '。', '；', '：', '！', '？', '）', '】', '》', '、' };

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
            return ReadTextFileAsync(path, null, null, cancellationToken);
        }

        public static async Task<CopilotLocalFileReadResult> ReadTextFileAsync(
            string path,
            int? startLine,
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
                    "文件路径为空。",
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
                    $"路径格式无效：{ex.Message}",
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
                    "目标路径是文件夹，不是文件。",
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
                    "文件不存在。",
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
                        "目标文件看起来不是可直接读取的文本文件。",
                        0,
                        0);
                }

                stream.Position = 0;
                using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
                var normalizedStartLine = Math.Max(1, startLine ?? 1);
                var normalizedEndLine = endLine.HasValue
                    ? Math.Max(normalizedStartLine, endLine.Value)
                    : int.MaxValue;
                var wasTruncated = false;
                var builder = new System.Text.StringBuilder();
                var currentLine = 0;
                var actualStartLine = 0;
                var actualEndLine = 0;

                while (!reader.EndOfStream)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    var line = await reader.ReadLineAsync() ?? string.Empty;
                    currentLine++;

                    if (currentLine < normalizedStartLine)
                        continue;

                    if (currentLine > normalizedEndLine)
                        break;

                    actualStartLine = actualStartLine == 0 ? currentLine : actualStartLine;
                    actualEndLine = currentLine;

                    var lineWithBreak = line + Environment.NewLine;
                    if (builder.Length + lineWithBreak.Length > MaxReadCharacters)
                    {
                        var remaining = Math.Max(0, MaxReadCharacters - builder.Length);
                        if (remaining > 0)
                            builder.Append(lineWithBreak[..Math.Min(remaining, lineWithBreak.Length)]);

                        wasTruncated = true;
                        break;
                    }

                    builder.Append(lineWithBreak);
                }

                if (actualStartLine == 0 && currentLine < normalizedStartLine)
                {
                    return new CopilotLocalFileReadResult(
                        fullPath,
                        false,
                        false,
                        string.Empty,
                        $"请求的起始行 {normalizedStartLine} 超出了文件总行数。",
                        0,
                        0);
                }

                var content = builder.ToString().TrimEnd();

                if (wasTruncated)
                {
                    content += Environment.NewLine + $"...<内容已截断，仅保留前 {MaxReadCharacters} 字符。>";
                }

                return new CopilotLocalFileReadResult(
                    fullPath,
                    true,
                    wasTruncated,
                    content.TrimEnd(),
                    string.Empty,
                    actualStartLine,
                    actualEndLine);
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
                    $"读取失败：{ex.Message}",
                    0,
                    0);
            }
        }

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