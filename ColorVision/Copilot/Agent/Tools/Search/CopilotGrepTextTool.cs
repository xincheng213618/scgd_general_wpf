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
    public sealed class CopilotGrepTextTool : ICopilotTool
    {
        private const int MaxFilesToScan = 5000;
        private const int MaxMatches = 40;

        private static readonly string[] GrepKeywords =
        {
            "grep",
            "search",
            "find",
            "定义",
            "实现",
            "调用",
            "引用",
            "搜索",
            "查找",
            "在哪",
            "哪里",
            "包含",
            "关键字",
            "符号",
            "函数",
            "方法",
            "类",
            "属性",
        };

        private static readonly Regex QuotedPatternRegex = new("[`\"“](?<term>[^`\"”\\r\\n]{2,100})[`\"”]", RegexOptions.Compiled);
        private static readonly Regex IdentifierRegex = new(@"(?<term>[A-Za-z_][A-Za-z0-9_\.]{2,80})", RegexOptions.Compiled);

        public string Name => "GrepText";

        public string Description => "按关键字或标识符在当前解决方案文本文件中查找命中行。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || request.SearchRootPaths.Count == 0)
                return false;

            return true;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var searchRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.SearchRootPaths);
            var patterns = ResolvePatterns(request, toolInput);
            if (searchRoots.Count == 0 || patterns.Count == 0)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "缺少可搜索的根目录或关键字。",
                    ErrorMessage = "当前没有可用的搜索根，或未能从消息中提取文本搜索关键字。",
                });
            }

            var scannedFiles = 0;
            var matches = new List<string>();
            var matchedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in CopilotWorkspaceSearchSupport.EnumerateFiles(searchRoots, textFilesOnly: true, cancellationToken))
            {
                scannedFiles++;
                if (scannedFiles > MaxFilesToScan || matches.Count >= MaxMatches)
                    break;

                try
                {
                    var lineNumber = 0;
                    foreach (var line in File.ReadLines(entry.FullPath))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        lineNumber++;

                        if (!patterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                            continue;

                        matches.Add($"[命中] {CopilotWorkspaceSearchSupport.GetDisplayPath(entry.RootPath, entry.FullPath)}:{lineNumber} {CopilotWorkspaceSearchSupport.TruncateLine(line, 220)}");
                        matchedFilePaths.Add(entry.FullPath);
                        if (matches.Count >= MaxMatches)
                            break;
                    }
                }
                catch
                {
                }
            }

            if (matches.Count == 0)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = $"扫描了 {scannedFiles} 个文本文件，但没有找到关键字命中。",
                    ErrorMessage = $"搜索关键字：{string.Join(", ", patterns)}",
                });
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[搜索关键字] {string.Join(", ", patterns)}");
            builder.AppendLine($"[搜索根] {string.Join("；", searchRoots)}");
            builder.AppendLine($"[扫描文本文件数] {scannedFiles}");
            builder.AppendLine();

            foreach (var match in matches)
            {
                builder.AppendLine(match);
            }

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"扫描 {scannedFiles} 个文本文件，找到 {matches.Count} 条命中。",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = matchedFilePaths
                    .Take(3)
                    .ToArray(),
            });
        }

        private static IReadOnlyList<string> ResolvePatterns(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            var toolQuery = toolInput?.Query;
            if (!string.IsNullOrWhiteSpace(toolQuery))
                return ExtractPatterns(toolQuery);

            return ExtractPatterns(request.UserText);
        }

        private static bool HasGrepIntent(string text)
        {
            var source = text ?? string.Empty;
            return GrepKeywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<string> ExtractPatterns(string text)
        {
            var source = text ?? string.Empty;
            var patterns = new List<string>();

            AddPatterns(patterns, QuotedPatternRegex.Matches(source));
            AddPatterns(patterns, IdentifierRegex.Matches(source));

            return patterns
                .OrderByDescending(pattern => pattern.Length)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(3)
                .ToArray();
        }

        private static void AddPatterns(List<string> patterns, MatchCollection matches)
        {
            foreach (Match match in matches)
            {
                var term = (match.Groups["term"].Value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(term) || term.Length < 3)
                    continue;

                if (term.Contains('\\') || term.Contains('/') || term.Contains(':'))
                    continue;

                patterns.Add(term);
            }
        }
    }
}