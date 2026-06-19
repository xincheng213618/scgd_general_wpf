using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

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

        public static CopilotRecentLogSnapshot Capture(
            string? query = null,
            CopilotRecentLogMode mode = CopilotRecentLogMode.RecentLines,
            int maxLines = DefaultMaxLogLines,
            int maxChars = DefaultMaxLogChars)
        {
            var safeMaxLines = Math.Max(1, maxLines);
            var safeMaxChars = Math.Max(256, maxChars);

            try
            {
                var latestLog = FindLatestLogFile();

                if (latestLog == null)
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

                var lines = File.ReadAllLines(latestLog.FullName);
                var selectedLines = mode == CopilotRecentLogMode.FullDay
                    ? lines
                    : lines.TakeLast(safeMaxLines).ToArray();

                var filteredLines = FilterLines(selectedLines, query);
                var linesToDisplay = filteredLines.Length > 0 ? filteredLines : selectedLines;

                var content = filteredLines.Length > 0
                    ? string.Join(Environment.NewLine, new[]
                    {
                        $"[Filter Keyword] {query}",
                        string.Join(Environment.NewLine, filteredLines),
                    })
                    : string.Join(Environment.NewLine, linesToDisplay);

                var contentWasTruncated = false;
                if (content.Length > safeMaxChars)
                {
                    content = content[..safeMaxChars] + Environment.NewLine + $"...<content truncated; kept the most recent {safeMaxChars} characters.>";
                    contentWasTruncated = true;
                }

                return new CopilotRecentLogSnapshot
                {
                    Success = true,
                    Mode = mode,
                    FilePath = latestLog.FullName,
                    Content = content,
                    Summary = BuildSummary(latestLog.Name, mode, safeMaxLines, lines.Length, linesToDisplay.Length, filteredLines.Length, query, contentWasTruncated),
                    TotalLineCount = lines.Length,
                    DisplayedLineCount = linesToDisplay.Length,
                    FilteredLineCount = filteredLines.Length,
                    RequestedRecentLineCount = safeMaxLines,
                    ContentWasTruncated = contentWasTruncated,
                };
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

        public static bool HasAvailableLogFile()
        {
            try
            {
                return FindLatestLogFile() != null;
            }
            catch
            {
                return false;
            }
        }

        private static FileInfo? FindLatestLogFile()
        {
            var logFiles = GetCandidateLogDirectories()
                .Where(Directory.Exists)
                .SelectMany(directory => Directory.GetFiles(directory, "*.txt", SearchOption.TopDirectoryOnly))
                .Select(path => new FileInfo(path))
                .ToArray();

            if (logFiles.Length == 0)
                return null;

            var todayFileName = DateTime.Now.ToString("yyyyMMdd'.txt'");
            return logFiles
                .OrderByDescending(file => string.Equals(file.Name, todayFileName, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(file => file.LastWriteTimeUtc)
                .FirstOrDefault();
        }

        private static string BuildSummary(
            string fileName,
            CopilotRecentLogMode mode,
            int requestedRecentLineCount,
            int totalLineCount,
            int displayedLineCount,
            int filteredLineCount,
            string? query,
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
                summary = $"Read recent log {fileName}; showing {displayedLineCount} of the latest {Math.Min(requestedRecentLineCount, totalLineCount)} lines.";
            }

            if (contentWasTruncated)
                summary += " Content was large and has been truncated.";

            return summary;
        }

        private static string[] FilterLines(IReadOnlyList<string> lines, string? query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<string>();

            return lines
                .Where(line => !string.IsNullOrWhiteSpace(line)
                    && line.Contains(query, StringComparison.OrdinalIgnoreCase))
                .Take(120)
                .ToArray();
        }

        public static IEnumerable<string> GetCandidateLogDirectories()
        {
            yield return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ColorVision", "Log");
            yield return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "log");
        }
    }
}
