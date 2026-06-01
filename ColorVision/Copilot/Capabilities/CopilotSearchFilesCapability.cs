using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Copilot
{
    public sealed class CopilotFileSearchMatch
    {
        public int Score { get; init; }

        public string RootPath { get; init; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;

        public string DisplayPath => CopilotWorkspaceSearchSupport.GetDisplayPath(RootPath, FullPath);
    }

    public sealed class CopilotFileSearchResult
    {
        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public IReadOnlyList<string> SearchRoots { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Terms { get; init; } = Array.Empty<string>();

        public int ScannedFileCount { get; init; }

        public IReadOnlyList<CopilotFileSearchMatch> Matches { get; init; } = Array.Empty<CopilotFileSearchMatch>();

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public CopilotCapabilityResult ToCapabilityResult()
        {
            return new CopilotCapabilityResult
            {
                Success = Success,
                Summary = Summary,
                Content = Content,
                ErrorMessage = ErrorMessage,
                SuggestedReadableLocalFilePaths = SuggestedReadableLocalFilePaths,
            };
        }
    }

    public static class CopilotSearchFilesCapability
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

        public static CopilotFileSearchResult Search(
            IEnumerable<string> searchRootPaths,
            string? query,
            string? fallbackText,
            bool allowPlainSearchTerms,
            CancellationToken cancellationToken)
        {
            var searchRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(searchRootPaths);
            var terms = ResolveSearchTerms(query, fallbackText, allowPlainSearchTerms);
            if (searchRoots.Count == 0 || terms.Count == 0)
            {
                return new CopilotFileSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Terms = terms,
                    Summary = "缺少可搜索的根目录或文件名关键字。",
                    ErrorMessage = "当前没有可用的搜索根，或未能从消息中提取文件名关键字。",
                };
            }

            var scannedFiles = 0;
            var matches = new List<CopilotFileSearchMatch>();

            foreach (var entry in CopilotWorkspaceSearchSupport.EnumerateFiles(searchRoots, textFilesOnly: false, cancellationToken))
            {
                scannedFiles++;
                if (scannedFiles > MaxFilesToScan)
                    break;

                var score = ScoreCandidate(entry, terms);
                if (score > 0)
                {
                    matches.Add(new CopilotFileSearchMatch
                    {
                        Score = score,
                        RootPath = entry.RootPath,
                        FullPath = entry.FullPath,
                    });
                }
            }

            var topMatches = matches
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxResults)
                .ToArray();

            if (topMatches.Length == 0)
            {
                return new CopilotFileSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Terms = terms,
                    ScannedFileCount = scannedFiles,
                    Matches = topMatches,
                    Summary = $"扫描了 {scannedFiles} 个文件，但没有找到候选文件。",
                    ErrorMessage = $"搜索关键字：{string.Join(", ", terms)}",
                };
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[搜索关键字] {string.Join(", ", terms)}");
            builder.AppendLine($"[搜索根] {string.Join("；", searchRoots)}");
            builder.AppendLine($"[扫描文件数] {scannedFiles}");
            builder.AppendLine();

            for (var index = 0; index < topMatches.Length; index++)
            {
                builder.Append(index + 1)
                    .Append(". ")
                    .AppendLine(topMatches[index].DisplayPath);
            }

            return new CopilotFileSearchResult
            {
                Success = true,
                SearchRoots = searchRoots,
                Terms = terms,
                ScannedFileCount = scannedFiles,
                Matches = topMatches,
                Summary = $"扫描 {scannedFiles} 个文件，找到 {topMatches.Length} 个候选文件。",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = topMatches
                    .Select(item => item.FullPath)
                    .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Take(3)
                    .ToArray(),
            };
        }

        public static IReadOnlyList<string> ResolveSearchTerms(string? query, string? fallbackText, bool allowPlainSearchTerms)
        {
            if (!string.IsNullOrWhiteSpace(query))
                return ExtractSearchTerms(query, allowPlainSearchTerms);

            return ExtractSearchTerms(fallbackText, allowPlainSearchTerms: false);
        }

        private static bool HasFileSearchIntent(string text)
        {
            var source = text ?? string.Empty;
            return FileSearchKeywords.Any(keyword => source.Contains(keyword, StringComparison.OrdinalIgnoreCase))
                || FileNameRegex.IsMatch(source);
        }

        private static IReadOnlyList<string> ExtractSearchTerms(string? text, bool allowPlainSearchTerms)
        {
            var source = text ?? string.Empty;
            var terms = new List<string>();

            AddTerms(terms, FileNameRegex.Matches(source));

            if (terms.Count == 0 && (allowPlainSearchTerms || HasFileSearchIntent(source)))
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