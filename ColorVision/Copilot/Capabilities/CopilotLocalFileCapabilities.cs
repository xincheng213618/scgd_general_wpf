using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public static class CopilotReadLocalFileCapability
    {
        private const int DefaultMaxFilesPerRequest = 3;

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

        public static async Task<CopilotCapabilityResult> ReadAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
            int? startColumn,
            int? endLine,
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
            CopilotLocalFileReadResult? lastSuccess = null;

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var useSelectedRange = !string.IsNullOrWhiteSpace(normalizedSelectedPath)
                    && string.Equals(path, normalizedSelectedPath, StringComparison.OrdinalIgnoreCase);
                var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(
                    path,
                    useSelectedRange ? startLine : null,
                    useSelectedRange ? startColumn : null,
                    useSelectedRange ? endLine : null,
                    cancellationToken);
                builder.AppendLine($"[File] {result.FullPath}");

                if (result.Success)
                {
                    if (result.StartLine > 0)
                        builder.AppendLine($"[Lines] {result.StartLine}-{result.EndLine}");

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

                    builder.AppendLine(result.Content);
                    successCount++;
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
                    ? BuildSuccessSummary(successCount, paths.Length, normalizedSelectedPath, lastSuccess)
                    : $"Failed to read any local files from {paths.Length} paths.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
            };
        }

        private static string BuildSuccessSummary(int successCount, int pathCount, string selectedPath, CopilotLocalFileReadResult? lastSuccess)
        {
            if (!string.IsNullOrWhiteSpace(selectedPath) && lastSuccess.HasValue)
            {
                var result = lastSuccess.Value;
                if (result.StartLine > 0)
                    return $"Read {Path.GetFileName(result.FullPath)} lines {result.StartLine}-{result.EndLine}.";

                return $"Read {Path.GetFileName(result.FullPath)}.";
            }

            return $"Read {successCount}/{pathCount} local files.";
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
