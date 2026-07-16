using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotRecentLogMode
    {
        RecentLines,
        FullDay,
    }

    public sealed class CopilotRecentLogSnapshot
    {
        public bool Success { get; init; }

        public CopilotRecentLogMode Mode { get; init; }

        public string FilePath { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string Summary { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public int TotalLineCount { get; init; }

        public bool TotalLineCountIsExact { get; init; }

        public int DisplayedLineCount { get; init; }

        public int FilteredLineCount { get; init; }

        public int RequestedRecentLineCount { get; init; }

        public bool ContentWasTruncated { get; init; }
    }

    public static class CopilotRecentLogSupport
    {
        public const int DefaultMaxLogLines = 300;
        public const int DefaultMaxLogChars = 20000;
        public const int FullDayMaxLogChars = 120000;

        private const int MaxRetainedLogLines = 2000;
        private const int MaxFilteredLogLines = 120;
        private const int MaxLogLineCharacters = 8192;
        private const int MaxRecentLogScanBytes = 4 * 1024 * 1024;
        private const string TruncatedLineMarker = "...<line truncated>";

        private static readonly EnumerationOptions LogEnumerationOptions = new()
        {
            AttributesToSkip = FileAttributes.ReparsePoint,
            IgnoreInaccessible = true,
            RecurseSubdirectories = false,
            ReturnSpecialDirectories = false,
        };

        public static CopilotRecentLogSnapshot Capture(
            string? query = null,
            CopilotRecentLogMode mode = CopilotRecentLogMode.RecentLines,
            int maxLines = DefaultMaxLogLines,
            int maxChars = DefaultMaxLogChars)
        {
            return CaptureAsync(query, mode, maxLines, maxChars, CancellationToken.None).GetAwaiter().GetResult();
        }

        public static async Task<CopilotRecentLogSnapshot> CaptureAsync(
            string? query = null,
            CopilotRecentLogMode mode = CopilotRecentLogMode.RecentLines,
            int maxLines = DefaultMaxLogLines,
            int maxChars = DefaultMaxLogChars,
            CancellationToken cancellationToken = default)
        {
            var safeMaxLines = NormalizeMaxLines(maxLines);
            var latestLog = FindLatestLogFile(cancellationToken);
            if (latestLog == null)
                return CreateMissingSnapshot(mode, safeMaxLines);

            return await CaptureFileCoreAsync(
                latestLog.FullName,
                query,
                mode,
                safeMaxLines,
                NormalizeMaxChars(maxChars),
                cancellationToken).ConfigureAwait(false);
        }

        public static Task<CopilotRecentLogSnapshot> CaptureFileAsync(
            string filePath,
            string? query = null,
            CopilotRecentLogMode mode = CopilotRecentLogMode.RecentLines,
            int maxLines = DefaultMaxLogLines,
            int maxChars = DefaultMaxLogChars,
            CancellationToken cancellationToken = default)
        {
            return CaptureFileCoreAsync(
                filePath,
                query,
                mode,
                NormalizeMaxLines(maxLines),
                NormalizeMaxChars(maxChars),
                cancellationToken);
        }

        public static bool HasAvailableLogFile()
        {
            try
            {
                return FindLatestLogFile(CancellationToken.None) != null;
            }
            catch
            {
                return false;
            }
        }

        private static async Task<CopilotRecentLogSnapshot> CaptureFileCoreAsync(
            string filePath,
            string? query,
            CopilotRecentLogMode mode,
            int safeMaxLines,
            int safeMaxChars,
            CancellationToken cancellationToken)
        {
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var fullPath = Path.GetFullPath(filePath);
                if (!File.Exists(fullPath))
                {
                    return new CopilotRecentLogSnapshot
                    {
                        Success = false,
                        Mode = mode,
                        FilePath = fullPath,
                        Summary = "The selected log file is unavailable.",
                        ErrorMessage = "The selected log file does not exist.",
                        RequestedRecentLineCount = safeMaxLines,
                    };
                }

                var accumulator = new LogScanAccumulator(mode, query, safeMaxLines, safeMaxChars);
                if (mode == CopilotRecentLogMode.RecentLines)
                    await ScanRecentLogTailAsync(fullPath, accumulator, cancellationToken).ConfigureAwait(false);
                else
                    await ScanWholeLogFileAsync(fullPath, accumulator, cancellationToken).ConfigureAwait(false);
                var selection = accumulator.BuildSelection();
                var content = BuildBoundedContent(
                    selection.Lines,
                    query,
                    selection.FilteredLineCount,
                    safeMaxChars,
                    selection.ResultsWereTruncated,
                    out var contentWasTruncated);

                return new CopilotRecentLogSnapshot
                {
                    Success = true,
                    Mode = mode,
                    FilePath = fullPath,
                    Content = content,
                    Summary = BuildSummary(
                        Path.GetFileName(fullPath),
                        mode,
                        safeMaxLines,
                        accumulator.TotalLineCount,
                        selection.Lines.Count,
                        selection.FilteredLineCount,
                        query,
                        accumulator.TotalLineCountIsExact,
                        contentWasTruncated),
                    TotalLineCount = accumulator.TotalLineCount,
                    TotalLineCountIsExact = accumulator.TotalLineCountIsExact,
                    DisplayedLineCount = selection.Lines.Count,
                    FilteredLineCount = selection.FilteredLineCount,
                    RequestedRecentLineCount = safeMaxLines,
                    ContentWasTruncated = contentWasTruncated,
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CopilotRecentLogSnapshot
                {
                    Success = false,
                    Mode = mode,
                    Summary = "Failed to read recent logs.",
                    ErrorMessage = ex.Message,
                    RequestedRecentLineCount = safeMaxLines,
                };
            }
        }

        private static async Task ScanWholeLogFileAsync(
            string filePath,
            LogScanAccumulator accumulator,
            CancellationToken cancellationToken)
        {
            await using var stream = OpenLogStream(filePath);
            await ScanLogStreamAsync(stream, accumulator, initialLineWasTruncated: false, cancellationToken).ConfigureAwait(false);
        }

        private static async Task ScanRecentLogTailAsync(
            string filePath,
            LogScanAccumulator accumulator,
            CancellationToken cancellationToken)
        {
            await using var stream = OpenLogStream(filePath);
            if (stream.Length <= MaxRecentLogScanBytes)
            {
                await ScanLogStreamAsync(stream, accumulator, initialLineWasTruncated: false, cancellationToken).ConfigureAwait(false);
                return;
            }

            var tailStart = await FindTailStartAsync(stream, cancellationToken).ConfigureAwait(false);
            stream.Position = tailStart.Offset;
            accumulator.MarkPrefixOmitted();
            await ScanLogStreamAsync(stream, accumulator, tailStart.StartsInsideLine, cancellationToken).ConfigureAwait(false);
        }

        private static async Task<TailStart> FindTailStartAsync(FileStream stream, CancellationToken cancellationToken)
        {
            var candidateOffset = Math.Max(0, stream.Length - MaxRecentLogScanBytes);
            stream.Position = candidateOffset - 1;
            var previousByte = stream.ReadByte();
            if (previousByte == '\n')
                return new TailStart(candidateOffset, StartsInsideLine: false);

            stream.Position = candidateOffset;
            var buffer = new byte[4096];
            while (true)
            {
                var chunkOffset = stream.Position;
                var read = await stream.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    return new TailStart(candidateOffset, StartsInsideLine: true);

                var lineFeedIndex = buffer.AsSpan(0, read).IndexOf((byte)'\n');
                if (lineFeedIndex < 0)
                    continue;

                var boundary = chunkOffset + lineFeedIndex + 1;
                return boundary < stream.Length
                    ? new TailStart(boundary, StartsInsideLine: false)
                    : new TailStart(candidateOffset, StartsInsideLine: true);
            }
        }

        private static async Task ScanLogStreamAsync(
            FileStream stream,
            LogScanAccumulator accumulator,
            bool initialLineWasTruncated,
            CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, detectEncodingFromByteOrderMarks: true);
            var buffer = new char[4096];
            var lineBuilder = new StringBuilder(Math.Min(1024, MaxLogLineCharacters));
            var lineWasTruncated = initialLineWasTruncated;
            var hasCharactersInCurrentLine = false;

            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                    break;

                for (var index = 0; index < read; index++)
                {
                    var character = buffer[index];
                    if (character == '\n')
                    {
                        accumulator.AddLine(CreateBoundedLine(lineBuilder, lineWasTruncated), lineWasTruncated);
                        lineBuilder.Clear();
                        lineWasTruncated = false;
                        hasCharactersInCurrentLine = false;
                        continue;
                    }

                    hasCharactersInCurrentLine = true;
                    if (lineBuilder.Length < MaxLogLineCharacters)
                        lineBuilder.Append(character);
                    else
                        lineWasTruncated = true;
                }
            }

            if (hasCharactersInCurrentLine || lineWasTruncated)
                accumulator.AddLine(CreateBoundedLine(lineBuilder, lineWasTruncated), lineWasTruncated);
        }

        private static FileStream OpenLogStream(string filePath)
        {
            return new FileStream(
                filePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete,
                bufferSize: 4096,
                FileOptions.Asynchronous | FileOptions.SequentialScan);
        }

        private static string CreateBoundedLine(StringBuilder builder, bool wasTruncated)
        {
            var line = builder.ToString().TrimEnd('\r');
            return wasTruncated ? line + TruncatedLineMarker : line;
        }

        private static string BuildBoundedContent(
            IReadOnlyList<string> lines,
            string? query,
            int filteredLineCount,
            int safeMaxChars,
            bool alreadyTruncated,
            out bool contentWasTruncated)
        {
            var body = string.Join(Environment.NewLine, lines);
            var bodyWasTruncated = body.Length > safeMaxChars;
            if (bodyWasTruncated)
                body = body[^safeMaxChars..];

            contentWasTruncated = alreadyTruncated || bodyWasTruncated;
            var builder = new StringBuilder();
            if (filteredLineCount > 0)
                builder.Append("[Filter Keyword] ").AppendLine(query);

            if (alreadyTruncated)
                builder.AppendLine("...<older or oversized log entries omitted; showing the most recent available content.>");
            if (bodyWasTruncated)
                builder.AppendLine($"...<content truncated; kept the most recent {safeMaxChars} characters.>");

            builder.Append(body);
            return builder.ToString().TrimEnd();
        }

        private static FileInfo? FindLatestLogFile(CancellationToken cancellationToken)
        {
            FileInfo? latest = null;
            var todayFileName = DateTime.Now.ToString("yyyyMMdd'.txt'");

            foreach (var directory in GetCandidateLogDirectories().Where(Directory.Exists))
            {
                try
                {
                    foreach (var path in Directory.EnumerateFiles(directory, "*.txt", LogEnumerationOptions))
                    {
                        cancellationToken.ThrowIfCancellationRequested();
                        var candidate = new FileInfo(path);
                        if (latest == null || IsPreferredLog(candidate, latest, todayFileName))
                            latest = candidate;
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

            return latest;
        }

        private static bool IsPreferredLog(FileInfo candidate, FileInfo current, string todayFileName)
        {
            var candidateIsToday = string.Equals(candidate.Name, todayFileName, StringComparison.OrdinalIgnoreCase);
            var currentIsToday = string.Equals(current.Name, todayFileName, StringComparison.OrdinalIgnoreCase);
            return candidateIsToday != currentIsToday
                ? candidateIsToday
                : candidate.LastWriteTimeUtc > current.LastWriteTimeUtc;
        }

        private static CopilotRecentLogSnapshot CreateMissingSnapshot(CopilotRecentLogMode mode, int safeMaxLines)
        {
            return new CopilotRecentLogSnapshot
            {
                Success = false,
                Mode = mode,
                Summary = "No recent logs were found.",
                ErrorMessage = "No readable log files were found in the current environment.",
                RequestedRecentLineCount = safeMaxLines,
            };
        }

        private static int NormalizeMaxLines(int maxLines) => Math.Clamp(maxLines, 1, MaxRetainedLogLines);

        private static int NormalizeMaxChars(int maxChars) => Math.Clamp(maxChars, 256, FullDayMaxLogChars);

        private static string BuildSummary(
            string fileName,
            CopilotRecentLogMode mode,
            int requestedRecentLineCount,
            int totalLineCount,
            int displayedLineCount,
            int filteredLineCount,
            string? query,
            bool totalLineCountIsExact,
            bool contentWasTruncated)
        {
            string summary;
            if (filteredLineCount > 0)
            {
                summary = mode == CopilotRecentLogMode.FullDay
                    ? $"Read today's log {fileName}; keyword {query} matched {filteredLineCount} lines."
                    : $"Read recent log {fileName}; keyword {query} matched {filteredLineCount} lines.";
            }
            else if (mode == CopilotRecentLogMode.FullDay)
            {
                summary = $"Read today's log {fileName}; total lines: {totalLineCount}.";
            }
            else
            {
                summary = totalLineCountIsExact
                    ? $"Read recent log {fileName}; showing {displayedLineCount} of the latest {Math.Min(requestedRecentLineCount, totalLineCount)} lines."
                    : $"Read recent log {fileName}; showing {displayedLineCount} lines from a bounded tail scan.";
            }

            if (contentWasTruncated)
                summary += " Content was large and has been truncated.";

            return summary;
        }

        public static IEnumerable<string> GetCandidateLogDirectories()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Log");
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        }

        private sealed class LogScanAccumulator
        {
            private readonly CopilotRecentLogMode _mode;
            private readonly string _query;
            private readonly int _maxRecentLines;
            private readonly int _maxTailCharacters;
            private readonly Queue<LogLineEntry> _recentLines = new();
            private readonly Queue<LogLineEntry> _fullDayTailLines = new();
            private readonly Queue<LogLineEntry> _filteredLines = new();
            private int _fullDayTailCharacters;
            private bool _fullDayTailWasTruncated;

            public LogScanAccumulator(CopilotRecentLogMode mode, string? query, int maxRecentLines, int maxTailCharacters)
            {
                _mode = mode;
                _query = (query ?? string.Empty).Trim();
                _maxRecentLines = maxRecentLines;
                _maxTailCharacters = maxTailCharacters;
            }

            public int TotalLineCount { get; private set; }

            public int FilteredLineCount { get; private set; }

            public bool TotalLineCountIsExact { get; private set; } = true;

            public void MarkPrefixOmitted()
            {
                TotalLineCountIsExact = false;
            }

            public void AddLine(string line, bool lineWasTruncated)
            {
                TotalLineCount++;
                var entry = new LogLineEntry(line, lineWasTruncated);

                if (_mode == CopilotRecentLogMode.RecentLines)
                {
                    EnqueueBounded(_recentLines, entry, _maxRecentLines);
                    return;
                }

                AddFullDayTailLine(entry);
                if (string.IsNullOrWhiteSpace(_query)
                    || !line.Contains(_query, StringComparison.OrdinalIgnoreCase))
                {
                    return;
                }

                FilteredLineCount++;
                if (_filteredLines.Count >= MaxFilteredLogLines)
                    _filteredLines.Dequeue();
                _filteredLines.Enqueue(entry);
            }

            public LogSelection BuildSelection()
            {
                if (_mode == CopilotRecentLogMode.FullDay)
                {
                    var useFiltered = FilteredLineCount > 0;
                    var selected = useFiltered ? _filteredLines : _fullDayTailLines;
                    return new LogSelection(
                        selected.Select(entry => entry.Text).ToArray(),
                        FilteredLineCount,
                        selected.Any(entry => entry.WasTruncated) || (useFiltered
                            ? FilteredLineCount > _filteredLines.Count
                            : _fullDayTailWasTruncated));
                }

                if (string.IsNullOrWhiteSpace(_query))
                {
                    return new LogSelection(
                        _recentLines.Select(entry => entry.Text).ToArray(),
                        0,
                        _recentLines.Any(entry => entry.WasTruncated)
                            || (!TotalLineCountIsExact && _recentLines.Count < _maxRecentLines));
                }

                var filtered = new Queue<LogLineEntry>();
                var filteredCount = 0;
                foreach (var entry in _recentLines)
                {
                    if (!entry.Text.Contains(_query, StringComparison.OrdinalIgnoreCase))
                        continue;

                    filteredCount++;
                    EnqueueBounded(filtered, entry, MaxFilteredLogLines);
                }

                return filteredCount > 0
                    ? new LogSelection(
                        filtered.Select(entry => entry.Text).ToArray(),
                        filteredCount,
                        filtered.Any(entry => entry.WasTruncated) || filteredCount > filtered.Count)
                    : new LogSelection(
                        _recentLines.Select(entry => entry.Text).ToArray(),
                        0,
                        _recentLines.Any(entry => entry.WasTruncated)
                            || (!TotalLineCountIsExact && _recentLines.Count < _maxRecentLines));
            }

            private void AddFullDayTailLine(LogLineEntry entry)
            {
                if (_fullDayTailLines.Count > 0)
                    _fullDayTailCharacters += Environment.NewLine.Length;
                _fullDayTailLines.Enqueue(entry);
                _fullDayTailCharacters += entry.Text.Length;

                while (_fullDayTailCharacters > _maxTailCharacters && _fullDayTailLines.Count > 1)
                {
                    var removed = _fullDayTailLines.Dequeue();
                    _fullDayTailCharacters -= removed.Text.Length + Environment.NewLine.Length;
                    _fullDayTailWasTruncated = true;
                }
            }

            private static void EnqueueBounded(Queue<LogLineEntry> queue, LogLineEntry entry, int maximumCount)
            {
                if (queue.Count >= maximumCount)
                    queue.Dequeue();
                queue.Enqueue(entry);
            }
        }

        private readonly record struct LogLineEntry(string Text, bool WasTruncated);

        private readonly record struct TailStart(long Offset, bool StartsInsideLine);

        private readonly record struct LogSelection(
            IReadOnlyList<string> Lines,
            int FilteredLineCount,
            bool ResultsWereTruncated);
    }
}
