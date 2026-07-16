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

        public int MatchedFileCount { get; init; }

        public bool ScanComplete { get; init; }

        public bool ResultsComplete { get; init; }

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
        private const int MaxExplicitQueryCharacters = 256;

        private static readonly string[] FileSearchKeywords =
        {
            "\u6587\u4ef6",
            "file",
            "filename",
            "\u8def\u5f84",
            "path",
            "\u76ee\u5f55",
            "\u5b9a\u4f4d",
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
            return SearchWithinScope(
                searchRootPaths,
                searchRootPaths,
                query,
                fallbackText,
                allowPlainSearchTerms,
                cancellationToken);
        }

        public static CopilotFileSearchResult SearchWithinScope(
            IEnumerable<string> searchRootPaths,
            IEnumerable<string> displayRootPaths,
            string? query,
            string? fallbackText,
            bool allowPlainSearchTerms,
            CancellationToken cancellationToken)
        {
            var searchRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(searchRootPaths);
            var displayRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots(displayRootPaths);
            var displayRootMap = searchRoots.ToDictionary(
                root => root,
                root => displayRoots.FirstOrDefault(displayRoot =>
                    CopilotWorkspaceSearchSupport.IsPathWithinRoots(root, [displayRoot])) ?? root,
                StringComparer.OrdinalIgnoreCase);
            var terms = ResolveSearchTerms(query, fallbackText, allowPlainSearchTerms);
            if (searchRoots.Count == 0 || terms.Count == 0)
            {
                return new CopilotFileSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Terms = terms,
                    Summary = "Missing searchable roots or file-name keywords.",
                    ErrorMessage = searchRoots.Count == 0
                        ? "No search root is available."
                        : !string.IsNullOrWhiteSpace(query)
                            ? $"The explicit file query must be a single-line literal of at most {MaxExplicitQueryCharacters} characters."
                            : "No file-name keyword could be extracted from the message.",
                };
            }

            var scannedFiles = 0;
            var matches = new List<CopilotFileSearchMatch>();
            var scanComplete = true;

            foreach (var entry in CopilotWorkspaceSearchSupport.EnumerateFiles(searchRoots, textFilesOnly: false, cancellationToken))
            {
                if (scannedFiles >= MaxFilesToScan)
                {
                    scanComplete = false;
                    break;
                }
                scannedFiles++;

                var displayEntry = new CopilotSearchFileEntry(displayRootMap[entry.RootPath], entry.FullPath);
                var score = ScoreCandidate(displayEntry, terms);
                if (score > 0)
                {
                    matches.Add(new CopilotFileSearchMatch
                    {
                        Score = score,
                        RootPath = displayEntry.RootPath,
                        FullPath = entry.FullPath,
                    });
                }
            }

            var topMatches = matches
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.FullPath, StringComparer.OrdinalIgnoreCase)
                .Take(MaxResults)
                .ToArray();
            var resultsComplete = scanComplete && matches.Count <= MaxResults;

            var builder = new StringBuilder();
            builder.AppendLine($"[Search Terms] {string.Join(", ", terms)}");
            builder.AppendLine($"[Search Roots] {string.Join("; ", searchRoots)}");
            builder.AppendLine($"[Scanned Files] {scannedFiles}");
            builder.AppendLine($"[Matched Files] {matches.Count}");
            builder.AppendLine($"[Results Shown] {topMatches.Length}");
            builder.AppendLine($"[Scan Complete] {scanComplete.ToString().ToLowerInvariant()}");
            builder.AppendLine($"[Results Complete] {resultsComplete.ToString().ToLowerInvariant()}");
            builder.AppendLine();

            if (topMatches.Length == 0)
            {
                return new CopilotFileSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Terms = terms,
                    ScannedFileCount = scannedFiles,
                    MatchedFileCount = matches.Count,
                    ScanComplete = scanComplete,
                    ResultsComplete = resultsComplete,
                    Matches = topMatches,
                    Summary = scanComplete
                        ? $"Scanned {scannedFiles} files, but no candidate files were found."
                        : $"Scanned the first {scannedFiles} files without a match, but the search scope was not exhausted.",
                    Content = builder.ToString().TrimEnd(),
                    ErrorMessage = scanComplete
                        ? $"Search terms: {string.Join(", ", terms)}"
                        : "The file scan limit was reached. Narrow the path before concluding that no matching file exists.",
                };
            }

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
                MatchedFileCount = matches.Count,
                ScanComplete = scanComplete,
                ResultsComplete = resultsComplete,
                Matches = topMatches,
                Summary = resultsComplete
                    ? $"Scanned {scannedFiles} files and found {topMatches.Length} candidate files."
                    : $"Found {matches.Count}{(scanComplete ? string.Empty : "+")} candidate files in an incomplete search; showing {topMatches.Length}.",
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
            {
                var literal = query.Trim().Replace('\\', '/');
                return literal.Length <= MaxExplicitQueryCharacters && !ContainsLineBreak(literal)
                    ? [literal]
                    : Array.Empty<string>();
            }

            return ExtractSearchTerms(fallbackText, allowPlainSearchTerms: false);
        }

        private static bool ContainsLineBreak(string value) => value.Contains('\r') || value.Contains('\n');

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
