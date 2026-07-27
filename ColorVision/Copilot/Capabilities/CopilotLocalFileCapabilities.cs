using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public static class CopilotReadLocalFileCapability
    {
        private const int DefaultMaxFilesPerRequest = 3;
        internal const int MaximumTaskFocusedReadCharactersPerFile = 3_000;
        private const long MaximumTaskFocusScanBytes = 8L * 1024 * 1024;
        private const int MaximumTaskFocusTerms = 24;
        private const int TaskFocusClusterRadiusLines = 40;
        private static readonly Regex TaskEnglishTermRegex = new(
            @"(?<term>[A-Za-z_][A-Za-z0-9_]{2,80})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex TaskIdentifierPartRegex = new(
            @"[A-Z]+(?=[A-Z][a-z]|\b)|[A-Z]?[a-z]+|[A-Z]+|\d+",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly Regex TaskChinesePhraseRegex = new(
            @"(?<term>[\u4e00-\u9fff]{2,20})",
            RegexOptions.Compiled | RegexOptions.CultureInvariant);
        private static readonly HashSet<string> TaskFocusStopWords = new(StringComparer.OrdinalIgnoreCase)
        {
            "agent",
            "code",
            "copilot",
            "current",
            "exact",
            "file",
            "files",
            "from",
            "implementation",
            "inspect",
            "least",
            "line",
            "lines",
            "local",
            "only",
            "please",
            "read",
            "related",
            "return",
            "source",
            "that",
            "this",
            "use",
            "with",
            "workspace",
        };

        public static Task<CopilotCapabilityResult> ReadAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
            int? endLine,
            CancellationToken cancellationToken)
        {
            return ReadAsync(
                readableLocalFilePaths,
                selectedPath,
                preferBatchReadAll,
                startLine,
                startColumn: null,
                endLine,
                cancellationToken);
        }

        public static Task<CopilotCapabilityResult> ReadAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
            int? startColumn,
            int? endLine,
            CancellationToken cancellationToken)
        {
            return ReadCoreAsync(
                readableLocalFilePaths,
                selectedPath,
                preferBatchReadAll,
                startLine,
                startColumn,
                endLine,
                CopilotLocalFileToolSupport.MaxReadCharacters,
                taskFocusedRanges: null,
                cancellationToken);
        }

        internal static Task<CopilotCapabilityResult> ReadAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
            int? startColumn,
            int? endLine,
            int maximumReadCharacters,
            CancellationToken cancellationToken)
        {
            return ReadCoreAsync(
                readableLocalFilePaths,
                selectedPath,
                preferBatchReadAll,
                startLine,
                startColumn,
                endLine,
                maximumReadCharacters,
                taskFocusedRanges: null,
                cancellationToken);
        }

        internal static async Task<CopilotCapabilityResult> ReadTaskFocusedBatchAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? taskText,
            int maximumReadCharacters,
            CancellationToken cancellationToken)
        {
            if (maximumReadCharacters is < CopilotLocalFileToolSupport.MinimumReadCharacters
                or > CopilotLocalFileToolSupport.MaxReadCharacters)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumReadCharacters));
            }

            var paths = (readableLocalFilePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(DefaultMaxFilesPerRequest)
                .ToArray();
            var maximumWindowCharacters = Math.Min(
                maximumReadCharacters,
                MaximumTaskFocusedReadCharactersPerFile);
            var ranges = await ResolveTaskFocusedRangesAsync(
                paths,
                taskText,
                maximumWindowCharacters,
                cancellationToken);

            return await ReadCoreAsync(
                paths,
                selectedPath: null,
                preferBatchReadAll: true,
                startLine: null,
                startColumn: null,
                endLine: null,
                maximumWindowCharacters,
                ranges,
                cancellationToken);
        }

        private static async Task<CopilotCapabilityResult> ReadCoreAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
            int? startColumn,
            int? endLine,
            int maximumReadCharacters,
            IReadOnlyDictionary<string, TaskFocusedReadRange>? taskFocusedRanges,
            CancellationToken cancellationToken)
        {
            var allowedPaths = (readableLocalFilePaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            var normalizedSelectedPath = NormalizePath(selectedPath);
            var preferBatchRead = preferBatchReadAll && string.IsNullOrWhiteSpace(normalizedSelectedPath);
            string[] paths;

            if (!string.IsNullOrWhiteSpace(normalizedSelectedPath))
            {
                if (!allowedPaths.Contains(normalizedSelectedPath, StringComparer.OrdinalIgnoreCase))
                {
                    return new CopilotCapabilityResult
                    {
                        Success = false,
                        Summary = "The planner selected a local file outside the allowed list.",
                        ErrorMessage = $"The planner-selected path is not in the current allowed read list: {normalizedSelectedPath}",
                    };
                }

                paths = new[] { normalizedSelectedPath };
            }
            else
            {
                paths = preferBatchRead
                    ? allowedPaths
                    : allowedPaths.Take(DefaultMaxFilesPerRequest).ToArray();
            }

            if (paths.Length == 0)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "No readable local file paths are available for the current round.",
                    ErrorMessage = "No local file paths allowed for the current round were detected.",
                };
            }

            cancellationToken.ThrowIfCancellationRequested();

            var builder = new StringBuilder();
            var successCount = 0;
            var errors = new List<string>();
            var successfullyReadPaths = new List<string>();
            var readScopes = new List<CopilotLocalFileReadScope>();
            CopilotLocalFileReadResult? lastSuccess = null;
            var focusedRangeCount = 0;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var useSelectedRange = !string.IsNullOrWhiteSpace(normalizedSelectedPath)
                    && string.Equals(path, normalizedSelectedPath, StringComparison.OrdinalIgnoreCase);
                var taskFocusedRange = default(TaskFocusedReadRange);
                var useTaskFocusedRange = !useSelectedRange
                    && taskFocusedRanges?.TryGetValue(path, out taskFocusedRange) == true;
                var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(
                    path,
                    useSelectedRange ? startLine : useTaskFocusedRange ? taskFocusedRange.StartLine : null,
                    useSelectedRange ? startColumn : null,
                    useSelectedRange ? endLine : useTaskFocusedRange ? taskFocusedRange.EndLine : null,
                    maximumReadCharacters,
                    cancellationToken);
                builder.AppendLine($"[File] {result.FullPath}");

                if (result.Success)
                {
                    if (result.StartLine > 0)
                        builder.AppendLine($"[Lines] {result.StartLine}-{result.EndLine}");
                    if (useTaskFocusedRange)
                    {
                        focusedRangeCount++;
                        builder.AppendLine("[Selection] Task-focused evidence window; unrelated file text is intentionally omitted.");
                        builder.AppendLine($"[Matched Task Terms] {string.Join(", ", taskFocusedRange.MatchedTerms)}");
                    }

                    builder.AppendLine("[Read Scope]");
                    builder.AppendLine($"start_line: {result.StartLine}");
                    builder.AppendLine($"start_column: {result.StartColumn}");
                    builder.AppendLine($"end_line: {result.EndLine}");
                    builder.AppendLine($"end_column: {result.EndColumn}");
                    builder.AppendLine($"content_complete: {(!result.WasTruncated).ToString().ToLowerInvariant()}");
                    if (result.WasTruncated)
                    {
                        builder.AppendLine($"continuation_start_line: {result.ContinuationStartLine}");
                        builder.AppendLine($"continuation_start_column: {result.ContinuationStartColumn}");
                    }

                    if (result.WasTruncated)
                        builder.AppendLine("Note: The file content was long and was truncated before sending to the model.");

                    AppendLineNumberedContent(builder, result);
                    successCount++;
                    successfullyReadPaths.Add(result.FullPath);
                    readScopes.Add(new CopilotLocalFileReadScope
                    {
                        Path = result.FullPath,
                        StartLine = result.StartLine,
                        StartColumn = result.StartColumn,
                        EndLine = result.EndLine,
                        EndColumn = result.EndColumn,
                        WasTruncated = result.WasTruncated,
                        ContinuationStartLine = result.ContinuationStartLine,
                        ContinuationStartColumn = result.ContinuationStartColumn,
                    });
                    lastSuccess = result;
                }
                else
                {
                    builder.AppendLine(result.ErrorMessage);
                    errors.Add($"{result.FullPath}: {result.ErrorMessage}");
                }

                builder.AppendLine();
            }

            return new CopilotCapabilityResult
            {
                Success = successCount > 0,
                Summary = successCount > 0
                    ? BuildSuccessSummary(
                        successCount,
                        paths.Length,
                        normalizedSelectedPath,
                        lastSuccess,
                        readScopes.Count(scope => scope.WasTruncated),
                        focusedRangeCount)
                    : $"Failed to read any local files from {paths.Length} paths.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
                AttemptedLocalFilePaths = paths,
                SuccessfullyReadLocalFilePaths = successfullyReadPaths,
                LocalFileReadScopes = readScopes,
            };
        }

        private static void AppendLineNumberedContent(StringBuilder builder, CopilotLocalFileReadResult result)
        {
            builder.AppendLine("[Content with authoritative one-based line numbers]");
            if (string.IsNullOrEmpty(result.Content) || result.StartLine < 1)
            {
                builder.AppendLine("<empty>");
                return;
            }

            using var reader = new StringReader(result.Content);
            var lineNumber = result.StartLine;
            var columnNumber = Math.Max(1, result.StartColumn);
            while (reader.ReadLine() is { } line)
            {
                if (line.StartsWith("...<content truncated; kept the first ", StringComparison.Ordinal))
                {
                    builder.AppendLine(line);
                    continue;
                }

                builder.Append('L').Append(lineNumber);
                if (columnNumber > 1)
                    builder.Append(":C").Append(columnNumber);
                builder.Append(": ").AppendLine(line);
                lineNumber++;
                columnNumber = 1;
            }
        }

        private static string BuildSuccessSummary(
            int successCount,
            int pathCount,
            string selectedPath,
            CopilotLocalFileReadResult? lastSuccess,
            int truncatedCount,
            int focusedRangeCount)
        {
            if (!string.IsNullOrWhiteSpace(selectedPath) && lastSuccess.HasValue)
            {
                var result = lastSuccess.Value;
                if (result.WasTruncated)
                {
                    return $"Read {Path.GetFileName(result.FullPath)} lines {result.StartLine}-{result.EndLine}; content is partial. "
                        + $"Continue at line {result.ContinuationStartLine}, column {result.ContinuationStartColumn}.";
                }
                if (result.StartLine > 0)
                    return $"Read {Path.GetFileName(result.FullPath)} lines {result.StartLine}-{result.EndLine}.";

                return $"Read {Path.GetFileName(result.FullPath)}.";
            }

            if (focusedRangeCount > 0)
            {
                return truncatedCount > 0
                    ? $"Read {successCount}/{pathCount} local files using {focusedRangeCount} task-focused evidence window(s); {truncatedCount} result(s) are partial."
                    : $"Read {successCount}/{pathCount} local files using {focusedRangeCount} task-focused evidence window(s).";
            }

            return truncatedCount > 0
                ? $"Read {successCount}/{pathCount} local files; {truncatedCount} result(s) are partial and include continuation cursors."
                : $"Read {successCount}/{pathCount} local files.";
        }

        private static async Task<IReadOnlyDictionary<string, TaskFocusedReadRange>> ResolveTaskFocusedRangesAsync(
            IReadOnlyList<string> paths,
            string? taskText,
            int maximumWindowCharacters,
            CancellationToken cancellationToken)
        {
            var terms = ResolveTaskFocusTerms(taskText, paths);
            var ranges = new Dictionary<string, TaskFocusedReadRange>(StringComparer.OrdinalIgnoreCase);
            if (terms.Length == 0)
                return ranges;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var range = await SelectTaskFocusedRangeAsync(
                    path,
                    terms,
                    maximumWindowCharacters,
                    cancellationToken);
                if (range.HasValue)
                    ranges[path] = range.Value;
            }

            return ranges;
        }

        private static TaskFocusTerm[] ResolveTaskFocusTerms(
            string? taskText,
            IReadOnlyList<string> paths)
        {
            var source = taskText ?? string.Empty;
            foreach (var path in paths)
            {
                source = source.Replace(path, " ", StringComparison.OrdinalIgnoreCase);
                source = source.Replace(Path.GetFileName(path), " ", StringComparison.OrdinalIgnoreCase);
            }

            var weightedTerms = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            if (ContainsAny(source, "预算", "budget", "token"))
            {
                AddTaskFocusTerm(weightedTerms, "budget", 10);
                AddTaskFocusTerm(weightedTerms, "token", 8);
                AddTaskFocusTerm(weightedTerms, "reserve", 6);
                AddTaskFocusTerm(weightedTerms, "consumed", 5);
            }
            if (ContainsAny(source, "证据", "evidence", "citation", "grounded"))
            {
                AddTaskFocusTerm(weightedTerms, "evidence", 10);
                AddTaskFocusTerm(weightedTerms, "citation", 7);
                AddTaskFocusTerm(weightedTerms, "observed", 6);
                AddTaskFocusTerm(weightedTerms, "scope", 5);
                AddTaskFocusTerm(weightedTerms, "successful", 4);
            }
            if (ContainsAny(source, "收束", "收敛", "finalization", "finalize", "convergence"))
            {
                AddTaskFocusTerm(weightedTerms, "finalization", 10);
                AddTaskFocusTerm(weightedTerms, "finalize", 8);
                AddTaskFocusTerm(weightedTerms, "convergence", 7);
                AddTaskFocusTerm(weightedTerms, "completion", 5);
                AddTaskFocusTerm(weightedTerms, "complete", 3);
            }
            if (ContainsAny(source, "子 Agent", "子Agent", "subagent", "delegate"))
            {
                AddTaskFocusTerm(weightedTerms, "subagent", 5);
                AddTaskFocusTerm(weightedTerms, "delegate", 4);
                AddTaskFocusTerm(weightedTerms, "child", 3);
            }
            if (ContainsAny(source, "截断", "truncation", "truncated", "continuation"))
            {
                AddTaskFocusTerm(weightedTerms, "truncat", 9);
                AddTaskFocusTerm(weightedTerms, "continuation", 7);
                AddTaskFocusTerm(weightedTerms, "cursor", 5);
            }
            if (ContainsAny(source, "上下文", "context", "compaction", "compact"))
            {
                AddTaskFocusTerm(weightedTerms, "context", 9);
                AddTaskFocusTerm(weightedTerms, "compact", 6);
                AddTaskFocusTerm(weightedTerms, "summary", 4);
            }

            foreach (Match match in TaskEnglishTermRegex.Matches(source))
            {
                var identifier = match.Groups["term"].Value;
                AddTaskFocusTerm(weightedTerms, identifier, 3);
                foreach (Match part in TaskIdentifierPartRegex.Matches(identifier))
                    AddTaskFocusTerm(weightedTerms, part.Value, 3);
            }

            foreach (Match match in TaskChinesePhraseRegex.Matches(source))
            {
                var phrase = match.Groups["term"].Value;
                AddTaskFocusTerm(weightedTerms, phrase, 3);
                for (var index = 0; index < phrase.Length - 1; index++)
                    AddTaskFocusTerm(weightedTerms, phrase.Substring(index, 2), 2);
            }

            return weightedTerms
                .Select(pair => new TaskFocusTerm(pair.Key, pair.Value))
                .OrderByDescending(term => term.Weight)
                .ThenByDescending(term => term.Text.Length)
                .Take(MaximumTaskFocusTerms)
                .ToArray();
        }

        private static async Task<TaskFocusedReadRange?> SelectTaskFocusedRangeAsync(
            string path,
            IReadOnlyList<TaskFocusTerm> terms,
            int maximumWindowCharacters,
            CancellationToken cancellationToken)
        {
            try
            {
                var info = new FileInfo(path);
                if (!info.Exists || info.Length > MaximumTaskFocusScanBytes)
                    return null;

                var lines = await File.ReadAllLinesAsync(path, cancellationToken);
                if (lines.Length == 0)
                    return null;

                var scores = new int[lines.Length];
                for (var lineIndex = 0; lineIndex < lines.Length; lineIndex++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    foreach (var term in terms)
                    {
                        if (lines[lineIndex].Contains(term.Text, StringComparison.OrdinalIgnoreCase))
                            scores[lineIndex] += term.Weight;
                    }
                }

                var bestCenter = -1;
                var bestClusterScore = 0;
                var bestDirectScore = 0;
                for (var center = 0; center < scores.Length; center++)
                {
                    if (scores[center] == 0)
                        continue;

                    var clusterScore = 0;
                    var clusterStart = Math.Max(0, center - TaskFocusClusterRadiusLines);
                    var clusterEnd = Math.Min(scores.Length - 1, center + TaskFocusClusterRadiusLines);
                    for (var neighbor = clusterStart; neighbor <= clusterEnd; neighbor++)
                    {
                        var distance = Math.Abs(center - neighbor);
                        clusterScore += scores[neighbor] * (TaskFocusClusterRadiusLines + 1 - distance);
                    }

                    if (clusterScore > bestClusterScore
                        || (clusterScore == bestClusterScore && scores[center] > bestDirectScore))
                    {
                        bestCenter = center;
                        bestClusterScore = clusterScore;
                        bestDirectScore = scores[center];
                    }
                }

                if (bestCenter < 0)
                    return null;

                var start = bestCenter;
                var end = bestCenter;
                var selectedCharacters = GetLineCharacterCount(lines[bestCenter]);
                var precedingBudget = maximumWindowCharacters / 2;
                var precedingCharacters = 0;
                while (start > 0)
                {
                    var nextCharacters = GetLineCharacterCount(lines[start - 1]);
                    if (precedingCharacters + nextCharacters > precedingBudget
                        || selectedCharacters + nextCharacters > maximumWindowCharacters)
                    {
                        break;
                    }

                    start--;
                    precedingCharacters += nextCharacters;
                    selectedCharacters += nextCharacters;
                }

                while (end + 1 < lines.Length)
                {
                    var nextCharacters = GetLineCharacterCount(lines[end + 1]);
                    if (selectedCharacters + nextCharacters > maximumWindowCharacters)
                        break;

                    end++;
                    selectedCharacters += nextCharacters;
                }

                while (start > 0)
                {
                    var nextCharacters = GetLineCharacterCount(lines[start - 1]);
                    if (selectedCharacters + nextCharacters > maximumWindowCharacters)
                        break;

                    start--;
                    selectedCharacters += nextCharacters;
                }

                var matchedTerms = terms
                    .Where(term => Enumerable.Range(start, end - start + 1)
                        .Any(index => lines[index].Contains(term.Text, StringComparison.OrdinalIgnoreCase)))
                    .Select(term => term.Text)
                    .Take(8)
                    .ToArray();
                return new TaskFocusedReadRange(start + 1, end + 1, matchedTerms);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch
            {
                return null;
            }
        }

        private static int GetLineCharacterCount(string line)
        {
            return (line?.Length ?? 0) + Environment.NewLine.Length;
        }

        private static bool ContainsAny(string source, params string[] terms)
        {
            return terms.Any(term => source.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static void AddTaskFocusTerm(Dictionary<string, int> terms, string? value, int weight)
        {
            var term = (value ?? string.Empty).Trim();
            if ((term.Length < 3 && !IsChineseSearchTerm(term)) || TaskFocusStopWords.Contains(term))
                return;

            if (!terms.TryGetValue(term, out var existingWeight) || weight > existingWeight)
                terms[term] = weight;
        }

        private static bool IsChineseSearchTerm(string value)
        {
            return value.Length >= 2 && value.All(character => character is >= '\u4e00' and <= '\u9fff');
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }

        private readonly record struct TaskFocusTerm(string Text, int Weight);

        private readonly record struct TaskFocusedReadRange(
            int StartLine,
            int EndLine,
            IReadOnlyList<string> MatchedTerms);
    }

    public static class CopilotListDirectoryCapability
    {
        private const int MaxListedEntries = 60;
        private const int MaxScannedEntries = 20000;
        private const int MaxSuggestedReadableFiles = 10;

        private static readonly EnumerationOptions ListEnumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

        public static CopilotCapabilityResult List(
            IEnumerable<string> readableLocalDirectoryPaths,
            string? selectedPath,
            CancellationToken cancellationToken)
        {
            return List(readableLocalDirectoryPaths, selectedPath, cursor: null, cancellationToken);
        }

        public static CopilotCapabilityResult List(
            IEnumerable<string> readableLocalDirectoryPaths,
            string? selectedPath,
            string? cursor,
            CancellationToken cancellationToken)
        {
            var allowedDirectories = (readableLocalDirectoryPaths ?? Array.Empty<string>())
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Select(NormalizePath)
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (allowedDirectories.Length == 0)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "No listable local directories are available for the current round.",
                    ErrorMessage = "No local directory paths allowed for the current round were detected.",
                };
            }

            var selectedDirectory = NormalizePath(selectedPath);
            if (!string.IsNullOrWhiteSpace(selectedDirectory)
                && !allowedDirectories.Contains(selectedDirectory, StringComparer.OrdinalIgnoreCase))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "The planner selected a local directory outside the allowed list.",
                    ErrorMessage = $"The planner-selected directory is not in the current allowed access list: {selectedDirectory}",
                };
            }

            var directoryPath = !string.IsNullOrWhiteSpace(selectedDirectory)
                ? selectedDirectory
                : allowedDirectories[0];

            if (!Directory.Exists(directoryPath))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "The target directory does not exist.",
                    ErrorMessage = $"The target directory does not exist: {directoryPath}",
                };
            }

            BoundedDirectoryEntries entries;
            try
            {
                entries = EnumerateBounded(directoryPath, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "Failed to list directory.",
                    ErrorMessage = ex.Message,
                };
            }

            var revision = BuildDirectoryRevision(directoryPath, entries.Entries);
            if (!TryResolveCursor(cursor, revision, entries.Entries.Count, out var offset, out var cursorError))
            {
                return new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "The directory continuation cursor is invalid or stale.",
                    ErrorMessage = cursorError,
                };
            }

            var page = entries.Entries.Skip(offset).Take(MaxListedEntries).ToArray();
            var nextOffset = offset + page.Length;
            var hasMoreScannedEntries = nextOffset < entries.Entries.Count;
            var nextCursor = hasMoreScannedEntries ? $"{revision}:{nextOffset.ToString(CultureInfo.InvariantCulture)}" : string.Empty;
            var entriesComplete = entries.ScanComplete && !hasMoreScannedEntries;
            var scannedDirectoryCount = entries.Entries.Count(entry => entry.IsDirectory);
            var scannedFileCount = entries.Entries.Count - scannedDirectoryCount;

            var builder = new StringBuilder();
            builder.AppendLine("[Directory Page]");
            builder.AppendLine($"entries_scanned: {entries.Entries.Count}");
            builder.AppendLine($"scan_complete: {entries.ScanComplete.ToString().ToLowerInvariant()}");
            builder.AppendLine($"page_offset: {offset}");
            builder.AppendLine($"entries_returned: {page.Length}");
            builder.AppendLine($"entries_complete: {entriesComplete.ToString().ToLowerInvariant()}");
            if (!string.IsNullOrWhiteSpace(nextCursor))
                builder.AppendLine($"next_cursor: {nextCursor}");
            builder.AppendLine();
            builder.AppendLine($"[Directory] {directoryPath}");
            builder.AppendLine($"[Subdirectories Scanned] {scannedDirectoryCount}");
            builder.AppendLine($"[Files Scanned] {scannedFileCount}");
            builder.AppendLine();

            foreach (var entry in page)
            {
                builder.Append(entry.IsDirectory ? "[Directory] " : "[File] ")
                    .AppendLine(entry.Name);
            }

            if (!entriesComplete)
            {
                builder.AppendLine();
                builder.AppendLine(!string.IsNullOrWhiteSpace(nextCursor)
                    ? "...<more directory entries are available; call ListDirectory again with next_cursor.>"
                    : $"...<directory scan stopped at the {MaxScannedEntries}-entry safety limit; narrow the path before drawing a complete conclusion.>");
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = entriesComplete
                    ? $"Listed the complete {GetDirectoryLabel(directoryPath)} directory ({page.Length} entries on this page)."
                    : !string.IsNullOrWhiteSpace(nextCursor)
                        ? $"Listed {page.Length} entries from {GetDirectoryLabel(directoryPath)}; another stable page is available."
                        : $"Listed {page.Length} entries from an incomplete bounded scan of {GetDirectoryLabel(directoryPath)}.",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = page
                    .Where(entry => !entry.IsDirectory && CopilotWorkspaceSearchSupport.IsTextLikeFile(entry.FullPath))
                    .Select(entry => entry.FullPath)
                    .Take(MaxSuggestedReadableFiles)
                    .ToArray(),
            };
        }

        private static BoundedDirectoryEntries EnumerateBounded(string directoryPath, CancellationToken cancellationToken)
        {
            var entries = new List<DirectoryEntry>(Math.Min(1024, MaxScannedEntries));
            var scanComplete = AppendEntries(
                () => Directory.EnumerateDirectories(directoryPath, "*", ListEnumerationOptions),
                isDirectory: true,
                entries,
                cancellationToken);
            if (scanComplete)
            {
                scanComplete = AppendEntries(
                    () => Directory.EnumerateFiles(directoryPath, "*", ListEnumerationOptions),
                    isDirectory: false,
                    entries,
                    cancellationToken);
            }

            return new BoundedDirectoryEntries(
                entries
                    .OrderByDescending(entry => entry.IsDirectory)
                    .ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(entry => entry.FullPath, StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                scanComplete);
        }

        private static bool AppendEntries(
            Func<IEnumerable<string>> createEntries,
            bool isDirectory,
            List<DirectoryEntry> entries,
            CancellationToken cancellationToken)
        {
            using var enumerator = createEntries().GetEnumerator();
            while (enumerator.MoveNext())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (entries.Count >= MaxScannedEntries)
                    return false;

                entries.Add(new DirectoryEntry(enumerator.Current, isDirectory));
            }

            return true;
        }

        private static string BuildDirectoryRevision(string directoryPath, IReadOnlyList<DirectoryEntry> entries)
        {
            var builder = new StringBuilder(entries.Count * 24);
            builder.Append(directoryPath.ToUpperInvariant()).Append('\n');
            foreach (var entry in entries)
            {
                builder.Append(entry.IsDirectory ? 'D' : 'F')
                    .Append('|')
                    .Append(entry.Name.ToUpperInvariant())
                    .Append('\n');
            }

            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(builder.ToString())))[..16].ToLowerInvariant();
        }

        private static bool TryResolveCursor(string? cursor, string revision, int entryCount, out int offset, out string error)
        {
            offset = 0;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(cursor))
                return true;

            var parts = cursor.Trim().Split(':', 2, StringSplitOptions.None);
            if (parts.Length != 2
                || parts[0].Length != revision.Length
                || !int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out offset)
                || offset < 0
                || offset > entryCount)
            {
                error = "The directory cursor format or offset is invalid. Restart the listing without a cursor.";
                return false;
            }
            if (!string.Equals(parts[0], revision, StringComparison.OrdinalIgnoreCase))
            {
                error = "The directory changed after the previous page. Restart the listing without a cursor.";
                return false;
            }

            return true;
        }

        private readonly record struct DirectoryEntry(string FullPath, bool IsDirectory)
        {
            public string Name => Path.GetFileName(FullPath);
        }

        private readonly record struct BoundedDirectoryEntries(IReadOnlyList<DirectoryEntry> Entries, bool ScanComplete);

        private static string GetDirectoryLabel(string directoryPath)
        {
            var name = Path.GetFileName(directoryPath);
            return string.IsNullOrWhiteSpace(name) ? directoryPath : name;
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
