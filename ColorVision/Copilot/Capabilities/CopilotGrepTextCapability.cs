#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace ColorVision.Copilot
{
    public sealed class CopilotTextSearchMatch
    {
        public string RootPath { get; init; } = string.Empty;

        public string FullPath { get; init; } = string.Empty;

        public int LineNumber { get; init; }

        public string LineText { get; init; } = string.Empty;

        public string DisplayPath => CopilotWorkspaceSearchSupport.GetDisplayPath(RootPath, FullPath);

        public string AgentLine => $"[Match] {DisplayPath}:{LineNumber} {CopilotWorkspaceSearchSupport.TruncateLine(LineText, 220)}";
    }

    public sealed class CopilotTextSearchResult
    {
        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public IReadOnlyList<string> SearchRoots { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Patterns { get; init; } = Array.Empty<string>();

        public int ScannedTextFileCount { get; init; }

        public IReadOnlyList<CopilotTextSearchMatch> Matches { get; init; } = Array.Empty<CopilotTextSearchMatch>();

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

    public static class CopilotGrepTextCapability
    {
        private const int MaxFilesToScan = 5000;
        private const int MaxMatches = 40;

        private static readonly Regex QuotedPatternRegex = new("[`\"\\u201C](?<term>[^`\"\\u201D\r\n]{2,100})[`\"\\u201D]", RegexOptions.Compiled);
        private static readonly Regex IdentifierRegex = new(@"(?<term>[A-Za-z_][A-Za-z0-9_\.]{2,80})", RegexOptions.Compiled);
        private static readonly Regex ChinesePhraseRegex = new(@"(?<term>[\u4e00-\u9fff]{2,30})", RegexOptions.Compiled);
        private static readonly string[] ChineseQuestionWords =
        {
            "怎么",
            "如何",
            "怎样",
            "为什么",
            "什么",
            "哪里",
            "哪个",
            "是否",
            "能否",
            "可以",
            "一下",
            "实现",
            "原理",
            "介绍",
            "说明",
            "请问",
            "帮我",
            "这个",
            "那个",
            "的是",
            "的吗",
            "是",
            "的",
            "了",
            "吗",
            "呢",
            "啊",
        };

        public static CopilotTextSearchResult Search(
            IEnumerable<string> searchRootPaths,
            string? query,
            string? fallbackText,
            CancellationToken cancellationToken)
        {
            return SearchWithinScope(
                searchRootPaths,
                searchRootPaths,
                query,
                fallbackText,
                cancellationToken);
        }

        public static CopilotTextSearchResult SearchWithinScope(
            IEnumerable<string> searchRootPaths,
            IEnumerable<string> displayRootPaths,
            string? query,
            string? fallbackText,
            CancellationToken cancellationToken)
        {
            var searchRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(searchRootPaths);
            var displayRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(displayRootPaths);
            var displayRootMap = searchRoots.ToDictionary(
                root => root,
                root => displayRoots.FirstOrDefault(displayRoot =>
                    CopilotWorkspaceSearchSupport.IsPathWithinRoots(root, [displayRoot])) ?? root,
                StringComparer.OrdinalIgnoreCase);
            var patterns = ResolvePatterns(query, fallbackText);
            if (searchRoots.Count == 0 || patterns.Count == 0)
            {
                return new CopilotTextSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Patterns = patterns,
                    Summary = "Missing searchable roots or keywords.",
                    ErrorMessage = "No search root is available, or no text-search keyword could be extracted from the message.",
                };
            }

            var scannedFiles = 0;
            var matches = new List<CopilotTextSearchMatch>();
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

                        matches.Add(new CopilotTextSearchMatch
                        {
                            RootPath = displayRootMap[entry.RootPath],
                            FullPath = entry.FullPath,
                            LineNumber = lineNumber,
                            LineText = line,
                        });
                        matchedFilePaths.Add(entry.FullPath);
                        if (matches.Count >= MaxMatches)
                            break;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch
                {
                }
            }

            if (matches.Count == 0)
            {
                return new CopilotTextSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Patterns = patterns,
                    ScannedTextFileCount = scannedFiles,
                    Matches = matches,
                    Summary = $"Scanned {scannedFiles} text files, but no keyword matches were found.",
                    ErrorMessage = $"Search keywords: {string.Join(", ", patterns)}",
                };
            }

            var builder = new StringBuilder();
            builder.AppendLine($"[Search Keywords] {string.Join(", ", patterns)}");
            builder.AppendLine($"[Search Roots] {string.Join("; ", searchRoots)}");
            builder.AppendLine($"[Scanned Text Files] {scannedFiles}");
            builder.AppendLine();

            foreach (var match in matches)
                builder.AppendLine(match.AgentLine);

            return new CopilotTextSearchResult
            {
                Success = true,
                SearchRoots = searchRoots,
                Patterns = patterns,
                ScannedTextFileCount = scannedFiles,
                Matches = matches,
                Summary = $"Scanned {scannedFiles} text files and found {matches.Count} matches.",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = matchedFilePaths
                    .Take(3)
                    .ToArray(),
            };
        }

        public static IReadOnlyList<string> ResolvePatterns(string? query, string? fallbackText)
        {
            if (!string.IsNullOrWhiteSpace(query))
                return ExtractPatterns(query);

            return ExtractPatterns(fallbackText);
        }

        private static IReadOnlyList<string> ExtractPatterns(string? text)
        {
            var source = text ?? string.Empty;
            var patterns = new List<string>();

            AddPatterns(patterns, QuotedPatternRegex.Matches(source));
            AddPatterns(patterns, IdentifierRegex.Matches(source));
            AddChinesePatterns(patterns, source);

            return patterns
                .OrderByDescending(pattern => pattern.Length)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(5)
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

        private static void AddChinesePatterns(List<string> patterns, string source)
        {
            foreach (Match match in ChinesePhraseRegex.Matches(source ?? string.Empty))
            {
                var term = NormalizeChineseSearchTerm(match.Groups["term"].Value);
                if (term.Length < 2)
                    continue;

                patterns.Add(term);
                AddChineseBigrams(patterns, term);
            }
        }

        private static string NormalizeChineseSearchTerm(string value)
        {
            var term = (value ?? string.Empty).Trim();
            if (term.Length == 0)
                return string.Empty;

            foreach (var word in ChineseQuestionWords)
                term = term.Replace(word, string.Empty, StringComparison.Ordinal);

            return term.Trim();
        }

        private static void AddChineseBigrams(List<string> patterns, string term)
        {
            if (string.IsNullOrWhiteSpace(term) || term.Length <= 2)
                return;

            for (var index = 0; index < term.Length - 1; index++)
                patterns.Add(term.Substring(index, 2));
        }
    }
}
