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
    public sealed class CopilotSearchFilesTool : ICopilotTool
    {
        private const int MaxFilesToScan = 20000;
        private const int MaxResults = 15;

        private static readonly string[] FileSearchKeywords =
        {
            "文件",
            "file",
            "filename",
            "路径",
            "path",
            "目录",
            "定位",
        };

        private static readonly Regex FileNameRegex = new(@"(?<term>[A-Za-z0-9_][A-Za-z0-9_.\-]{1,80}\.[A-Za-z0-9]{1,12})", RegexOptions.Compiled);
        private static readonly Regex IdentifierRegex = new(@"(?<term>[A-Za-z_][A-Za-z0-9_]{2,80})", RegexOptions.Compiled);

        public string Name => "SearchFiles";

        public string Description => "按文件名或路径片段在当前解决方案范围内查找候选文件。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.SearchRootPaths.Count == 0)
            {
                return false;
            }

            if (request.ReadableLocalFilePaths.Count > 0)
                return false;

            var terms = ExtractSearchTerms(request.UserText);
            return terms.Count > 0 && HasFileSearchIntent(request.UserText);
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var searchRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(request.SearchRootPaths);
            var terms = ResolveSearchTerms(request, toolInput);
            if (searchRoots.Count == 0 || terms.Count == 0)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "缺少可搜索的根目录或文件名关键字。",
                    ErrorMessage = "当前没有可用的搜索根，或未能从消息中提取文件名关键字。",
                });
            }

            var scannedFiles = 0;
            var matches = new List<(int Score, string RootPath, string FullPath)>();

            foreach (var entry in CopilotWorkspaceSearchSupport.EnumerateFiles(searchRoots, textFilesOnly: false, cancellationToken))
            {
                scannedFiles++;
                if (scannedFiles > MaxFilesToScan)
                    break;

                var score = ScoreCandidate(entry, terms);
                if (score > 0)
                    matches.Add((score, entry.RootPath, entry.FullPath));
            }

            var topMatches = matches
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxResults)
                .ToArray();

            if (topMatches.Length == 0)
            {
                return Task.FromResult(new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = $"扫描了 {scannedFiles} 个文件，但没有找到候选文件。",
                    ErrorMessage = $"搜索关键字：{string.Join(", ", terms)}",
                });
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[搜索关键字] {string.Join(", ", terms)}");
            builder.AppendLine($"[搜索根] {string.Join("；", searchRoots)}");
            builder.AppendLine($"[扫描文件数] {scannedFiles}");
            builder.AppendLine();

            for (var index = 0; index < topMatches.Length; index++)
            {
                var match = topMatches[index];
                builder.Append(index + 1)
                    .Append(". ")
                    .AppendLine(CopilotWorkspaceSearchSupport.GetDisplayPath(match.RootPath, match.FullPath));
            }

            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = $"扫描 {scannedFiles} 个文件，找到 {topMatches.Length} 个候选文件。",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = topMatches
                    .Select(item => item.FullPath)
                    .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToArray(),
            });
        }

        private static IReadOnlyList<string> ResolveSearchTerms(CopilotAgentRequest request, CopilotAgentToolInput toolInput)
        {
            var toolQuery = toolInput?.Query;
            if (!string.IsNullOrWhiteSpace(toolQuery))
                return ExtractSearchTerms(toolQuery);

            return ExtractSearchTerms(request.UserText);
        }

        private static bool HasFileSearchIntent(string text)
        {
            var source = text ?? string.Empty;
            return FileSearchKeywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || FileNameRegex.IsMatch(source);
        }

        private static IReadOnlyList<string> ExtractSearchTerms(string text)
        {
            var source = text ?? string.Empty;
            var terms = new List<string>();

            AddTerms(terms, FileNameRegex.Matches(source));

            if (terms.Count == 0 && HasFileSearchIntent(source))
                AddTerms(terms, IdentifierRegex.Matches(source));

            return terms
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
                .ToArray();
        }

        private static void AddTerms(List<string> terms, MatchCollection matches)
        {
            foreach (Match match in matches)
            {
                var term = (match.Groups["term"].Value ?? string.Empty).Trim();
                if (string.IsNullOrWhiteSpace(term))
                    continue;

                if (term.Length < 3)
                    continue;

                terms.Add(term);
            }
        }

        private static int ScoreCandidate(CopilotSearchFileEntry entry, IReadOnlyList<string> terms)
        {
            var fileName = Path.GetFileName(entry.FullPath);
            var fileNameWithoutExtension = Path.GetFileNameWithoutExtension(entry.FullPath);
            var displayPath = CopilotWorkspaceSearchSupport.GetDisplayPath(entry.RootPath, entry.FullPath);
            var score = 0;

            foreach (var term in terms)
            {
                if (string.Equals(fileName, term, StringComparison.OrdinalIgnoreCase))
                    score = Math.Max(score, 400);
                else if (string.Equals(fileNameWithoutExtension, term, StringComparison.OrdinalIgnoreCase))
                    score = Math.Max(score, 320);
                else if (fileName.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score = Math.Max(score, 220);
                else if (displayPath.Contains(term, StringComparison.OrdinalIgnoreCase))
                    score = Math.Max(score, 140);
            }

            return score;
        }
    }
}