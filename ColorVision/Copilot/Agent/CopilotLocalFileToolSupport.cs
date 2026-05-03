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
        string ErrorMessage);

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

        public static async Task<CopilotLocalFileReadResult> ReadTextFileAsync(string path, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return new CopilotLocalFileReadResult(
                    string.Empty,
                    false,
                    false,
                    string.Empty,
                    "文件路径为空。");
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
                    $"路径格式无效：{ex.Message}");
            }

            if (Directory.Exists(fullPath))
            {
                return new CopilotLocalFileReadResult(
                    fullPath,
                    false,
                    false,
                    string.Empty,
                    "目标路径是文件夹，不是文件。");
            }

            if (!File.Exists(fullPath))
            {
                return new CopilotLocalFileReadResult(
                    fullPath,
                    false,
                    false,
                    string.Empty,
                    "文件不存在。");
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
                        "目标文件看起来不是可直接读取的文本文件。");
                }

                stream.Position = 0;
                using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
                var content = await reader.ReadToEndAsync();
                var wasTruncated = false;

                if (content.Length > MaxReadCharacters)
                {
                    content = content[..MaxReadCharacters] + Environment.NewLine + $"...<内容已截断，仅保留前 {MaxReadCharacters} 字符。>";
                    wasTruncated = true;
                }

                return new CopilotLocalFileReadResult(
                    fullPath,
                    true,
                    wasTruncated,
                    content.TrimEnd(),
                    string.Empty);
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
                    $"读取失败：{ex.Message}");
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