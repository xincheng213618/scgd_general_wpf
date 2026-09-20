#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
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

        public string PartialResultMessage { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public IReadOnlyList<string> SearchRoots { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> Patterns { get; init; } = Array.Empty<string>();

        public int ScannedTextFileCount { get; init; }

        public bool ScanComplete { get; init; }

        public bool ResultsComplete { get; init; }

        public bool ResultsTruncated { get; init; }

        public string NextCursor { get; init; } = string.Empty;

        public IReadOnlyList<CopilotTextSearchMatch> Matches { get; init; } = Array.Empty<CopilotTextSearchMatch>();

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public CopilotCapabilityResult ToCapabilityResult()
        {
            return new CopilotCapabilityResult
            {
                Success = Success,
                Summary = Summary,
                PartialResultMessage = PartialResultMessage,
                Content = Content,
                ErrorMessage = ErrorMessage,
                SuggestedReadableLocalFilePaths = SuggestedReadableLocalFilePaths,
                LocalObservationScopePaths = Success ? SearchRoots : Array.Empty<string>(),
            };
        }
    }

    public static class CopilotGrepTextCapability
    {
        private const int MaxFilesToScan = 5000;
        private const int MaxMatches = 40;
        private const int MaxExplicitPatternCharacters = 256;

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
            return SearchWithinScope(
                searchRootPaths,
                displayRootPaths,
                query,
                fallbackText,
                cursor: null,
                cancellationToken);
        }

        public static CopilotTextSearchResult SearchWithinScope(
            IEnumerable<string> searchRootPaths,
            IEnumerable<string> displayRootPaths,
            string? query,
            string? fallbackText,
            string? cursor,
            CancellationToken cancellationToken)
        {
            var searchRoots = CopilotWorkspaceSearchSupport.NormalizeSearchScopes(searchRootPaths);
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
                    ErrorMessage = searchRoots.Count == 0
                        ? "No search root is available."
                        : !string.IsNullOrWhiteSpace(query)
                            ? $"The explicit text query must be a single-line literal of at most {MaxExplicitPatternCharacters} characters."
                            : "No text-search keyword could be extracted from the message.",
                };
            }

            var incompletePathCount = 0;
            var warnings = new List<string>(5);
            void AddWarning(string path, string reason)
            {
                if (warnings.Count < 5)
                    warnings.Add($"{CopilotWorkspaceSearchSupport.TruncateLine(path, 260)}: {reason}");
            }
            var candidateFiles = CopilotWorkspaceSearchSupport
                .EnumerateFiles(searchRoots, textFilesOnly: true, cancellationToken, (path, reason) =>
                {
                    incompletePathCount++;
                    AddWarning(path, reason);
                })
                .Take(MaxFilesToScan + 1)
                .ToArray();
            var fileListComplete = candidateFiles.Length <= MaxFilesToScan;
            var orderedFiles = candidateFiles
                .Take(MaxFilesToScan)
                .OrderBy(entry => CopilotWorkspaceSearchSupport.GetDisplayPath(displayRootMap[entry.RootPath], entry.FullPath), StringComparer.OrdinalIgnoreCase)
                .ThenBy(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var revision = BuildSearchRevision(searchRoots, patterns, orderedFiles);
            if (!TryResolveCursor(cursor, revision, orderedFiles.Length, out var startFileIndex, out var startLineNumber, out var previousReadFailure, out var cursorError))
            {
                return new CopilotTextSearchResult
                {
                    Success = false,
                    SearchRoots = searchRoots,
                    Patterns = patterns,
                    Summary = "The text-search continuation cursor is invalid or stale.",
                    ErrorMessage = cursorError,
                };
            }

            var scannedFiles = 0;
            var matches = new List<CopilotTextSearchMatch>(MaxMatches);
            var matchedFilePaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var readFailureCount = 0;
            var hasMoreMatches = false;
            var pageEndFileIndex = 0;
            var pageEndLineNumber = 0;

            for (var fileIndex = startFileIndex; fileIndex < orderedFiles.Length && !hasMoreMatches; fileIndex++)
            {
                var entry = orderedFiles[fileIndex];
                scannedFiles++;
                var fileMatches = new List<CopilotTextSearchMatch>();
                try
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    using var stream = new FileStream(entry.FullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    var preview = new byte[(int)Math.Min(CopilotLocalFileToolSupport.BinaryPreviewBytes, stream.Length)];
                    var previewRead = stream.ReadAtLeast(preview, preview.Length, throwOnEndOfStream: false);
                    using var reader = CopilotLocalFileToolSupport.CreateTextReader(stream, preview.AsSpan(0, previewRead));
                    var lineNumber = 0;
                    while (reader.ReadLine() is { } line)
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        if (line.Contains('\0'))
                            throw new InvalidDataException("The file contains NUL characters and appears to be binary.");
                        lineNumber++;
                        if (fileIndex == startFileIndex && lineNumber <= startLineNumber)
                            continue;
                        if (!patterns.Any(pattern => line.Contains(pattern, StringComparison.OrdinalIgnoreCase)))
                            continue;
                        if (matches.Count + fileMatches.Count >= MaxMatches)
                        {
                            hasMoreMatches = true;
                            break;
                        }

                        fileMatches.Add(new CopilotTextSearchMatch
                        {
                            RootPath = displayRootMap[entry.RootPath],
                            FullPath = entry.FullPath,
                            LineNumber = lineNumber,
                            LineText = line,
                        });
                    }
                    // A decoding/read failure invalidates this file's tentative matches, while
                    // independently read files remain usable. A full page still stops promptly.
                    matches.AddRange(fileMatches);
                    if (fileMatches.Count > 0)
                    {
                        matchedFilePaths.Add(entry.FullPath);
                        pageEndFileIndex = fileIndex;
                        pageEndLineNumber = fileMatches[^1].LineNumber;
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception exception)
                {
                    readFailureCount++;
                    AddWarning(entry.FullPath, exception switch
                    {
                        DecoderFallbackException => CopilotLocalFileToolSupport.InvalidEncodingMessage,
                        InvalidDataException => "The file contains NUL bytes or characters and appears to be binary.",
                        _ => "The file could not be read. Check access or retry after it becomes available.",
                    });
                }
            }

            var readFailureEncountered = previousReadFailure || readFailureCount > 0;
            var nextCursor = hasMoreMatches && fileListComplete
                ? $"{revision}:{pageEndFileIndex.ToString(CultureInfo.InvariantCulture)}:{pageEndLineNumber.ToString(CultureInfo.InvariantCulture)}:{(readFailureEncountered ? "1" : "0")}"
                : string.Empty;
            var scanComplete = fileListComplete && incompletePathCount == 0 && !readFailureEncountered && !hasMoreMatches;
            var resultsComplete = scanComplete;
            var resultsTruncated = hasMoreMatches;
            var coverageNotes = new List<string>();
            if (!fileListComplete)
                coverageNotes.Add($"已达到 {MaxFilesToScan} 个文件的搜索上限，请缩小范围后重试。");
            if (incompletePathCount > 0)
                coverageNotes.Add($"有 {incompletePathCount} 个路径因格式、大小或访问限制未能检查，不能据此判断没有匹配项。");
            if (readFailureCount > 0)
                coverageNotes.Add($"本页有 {readFailureCount} 个文件无法读取，不能据此判断没有匹配项。");
            if (previousReadFailure)
                coverageNotes.Add("之前的分页中有文件无法读取，当前结果仍不完整。");
            if (hasMoreMatches)
                coverageNotes.Add(!string.IsNullOrWhiteSpace(nextCursor)
                    ? $"本页显示 {matches.Count} 条匹配，仍有更多结果可继续读取。"
                    : $"仅显示 {matches.Count} 条匹配，请缩小搜索范围。");
            var partialResultMessage = string.Join(" ", coverageNotes);
            var builder = new StringBuilder();
            builder.AppendLine($"[Search Keywords] {string.Join(", ", patterns)}");
            builder.AppendLine($"[Search Roots] {string.Join("; ", searchRoots)}");
            builder.AppendLine($"[Candidate Text Files] {orderedFiles.Length}");
            builder.AppendLine($"[Scanned Text Files] {scannedFiles}");
            builder.AppendLine("[Search Policy] Text extensions only; generated/dependency directories and reparse points are excluded.");
            builder.AppendLine($"[Scope Inspection Failures] {incompletePathCount}");
            builder.AppendLine($"[Unreadable Text Files This Page] {readFailureCount}");
            builder.AppendLine($"[Matches Shown] {matches.Count}");
            builder.AppendLine($"[Scan Complete] {scanComplete.ToString().ToLowerInvariant()}");
            builder.AppendLine($"[Results Complete] {resultsComplete.ToString().ToLowerInvariant()}");
            if (!string.IsNullOrWhiteSpace(nextCursor))
                builder.AppendLine($"next_cursor: {nextCursor}");
            if (!fileListComplete)
                builder.AppendLine($"[Search Warning] The {MaxFilesToScan}-file scan limit was reached. Narrow the path before concluding that a query is absent.");
            if (previousReadFailure)
                builder.AppendLine("[Search Warning] At least one file on an earlier page could not be read. This continuation cannot prove a complete search; inspect the earlier warnings and restart or narrow the scope.");
            if (incompletePathCount > 0 || readFailureCount > 0)
                builder.AppendLine("[Search Warning] Some paths could not be inspected. The matches below are partial evidence; no absence conclusion is supported for unread paths.");
            foreach (var warning in warnings)
                builder.AppendLine($"[Search Warning] {warning}");
            builder.AppendLine();

            if (matches.Count == 0)
            {
                builder.AppendLine(scanComplete
                    ? "[Result] No text lines matched the literal query in the completed search scope."
                    : "[Search Warning] No match was found in the inspected text files, but a scan limit or unreadable file left the result partial.");
                return new CopilotTextSearchResult
                {
                    Success = true,
                    SearchRoots = searchRoots,
                    Patterns = patterns,
                    ScannedTextFileCount = scannedFiles,
                    ScanComplete = scanComplete,
                    ResultsComplete = resultsComplete,
                    ResultsTruncated = resultsTruncated,
                    PartialResultMessage = partialResultMessage,
                    Matches = matches,
                    Summary = scanComplete
                        ? $"Scanned {scannedFiles} text files; no lines matched the literal query."
                        : $"Scanned {scannedFiles} text files without a match; the result is partial because the scope was not fully inspected.",
                    Content = builder.ToString().TrimEnd(),
                };
            }

            foreach (var match in matches)
                builder.AppendLine(match.AgentLine);

            return new CopilotTextSearchResult
            {
                Success = true,
                SearchRoots = searchRoots,
                Patterns = patterns,
                ScannedTextFileCount = scannedFiles,
                ScanComplete = scanComplete,
                ResultsComplete = resultsComplete,
                ResultsTruncated = resultsTruncated,
                PartialResultMessage = partialResultMessage,
                NextCursor = nextCursor,
                Matches = matches,
                Summary = resultsComplete
                    ? $"Scanned {scannedFiles} text files and found {matches.Count} matches."
                    : resultsTruncated
                        ? !string.IsNullOrWhiteSpace(nextCursor)
                            ? $"Found more than {MaxMatches} matches; showing a stable page of {matches.Count}."
                            : $"Found more than {MaxMatches} matches in an incomplete file scope; showing {matches.Count}."
                        : $"Found {matches.Count} matches after inspecting {scannedFiles} text files, but the search scope was not fully inspected.",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = matchedFilePaths
                    .Take(3)
                    .ToArray(),
            };
        }

        private static string BuildSearchRevision(
            IReadOnlyList<string> searchRoots,
            IReadOnlyList<string> patterns,
            IReadOnlyList<CopilotSearchFileEntry> files)
        {
            var builder = new StringBuilder(files.Count * 48);
            foreach (var root in searchRoots)
                builder.Append("R|").Append(root.ToUpperInvariant()).Append('\n');
            foreach (var pattern in patterns)
                builder.Append("Q|").Append(pattern.ToUpperInvariant()).Append('\n');
            foreach (var file in files)
            {
                builder.Append("F|").Append(file.FullPath.ToUpperInvariant());
                try
                {
                    var info = new FileInfo(file.FullPath);
                    builder.Append('|').Append(info.Length).Append('|').Append(info.LastWriteTimeUtc.Ticks);
                }
                catch
                {
                    builder.Append("|unavailable");
                }
                builder.Append('\n');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16].ToLowerInvariant();
        }

        private static bool TryResolveCursor(
            string? cursor,
            string revision,
            int fileCount,
            out int fileIndex,
            out int lineNumber,
            out bool previousReadFailure,
            out string error)
        {
            fileIndex = 0;
            lineNumber = 0;
            previousReadFailure = false;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(cursor))
                return true;

            var parts = cursor.Trim().Split(':', 5, StringSplitOptions.None);
            if (parts.Length != 4
                || parts[0].Length != revision.Length
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out fileIndex)
                || !int.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out lineNumber)
                || fileIndex < 0
                || fileIndex >= fileCount
                || lineNumber < 1
                || parts[3] is not ("0" or "1"))
            {
                error = "The text-search cursor format or position is invalid. Restart the search without a cursor.";
                return false;
            }
            previousReadFailure = parts[3] == "1";
            if (!string.Equals(parts[0], revision, StringComparison.OrdinalIgnoreCase))
            {
                error = "The search query, scope, or candidate files changed after the previous page. Restart the search without a cursor.";
                return false;
            }

            return true;
        }

        public static IReadOnlyList<string> ResolvePatterns(string? query, string? fallbackText)
        {
            if (!string.IsNullOrWhiteSpace(query))
            {
                var literal = query.Trim();
                return literal.Length <= MaxExplicitPatternCharacters && !ContainsLineBreak(literal)
                    ? [literal]
                    : Array.Empty<string>();
            }

            return ExtractPatterns(fallbackText);
        }

        private static bool ContainsLineBreak(string value) => value.Contains('\r') || value.Contains('\n');

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
