using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public static class CopilotReadLocalFileCapability
    {
        private const int DefaultMaxFilesPerRequest = 3;

        public static async Task<CopilotCapabilityResult> ReadAsync(
            IEnumerable<string> readableLocalFilePaths,
            string? selectedPath,
            bool preferBatchReadAll,
            int? startLine,
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
                    useSelectedRange ? endLine : null,
                    cancellationToken);
                builder.AppendLine($"[File] {result.FullPath}");

                if (result.Success)
                {
                    if (result.StartLine > 0)
                        builder.AppendLine($"[Lines] {result.StartLine}-{result.EndLine}");

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

            BoundedDirectoryEntries subDirectories;
            BoundedDirectoryEntries files;
            try
            {
                subDirectories = EnumerateBounded(
                    () => Directory.EnumerateDirectories(directoryPath, "*", ListEnumerationOptions),
                    cancellationToken);
                files = EnumerateBounded(
                    () => Directory.EnumerateFiles(directoryPath, "*", ListEnumerationOptions),
                    cancellationToken);
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

            var builder = new StringBuilder();
            builder.AppendLine($"[Directory] {directoryPath}");
            builder.AppendLine($"[Subdirectories] {FormatEntryCount(subDirectories)}");
            builder.AppendLine($"[Files] {FormatEntryCount(files)}");
            builder.AppendLine();

            var listedCount = 0;
            foreach (var subDirectory in subDirectories.Paths)
            {
                if (listedCount >= MaxListedEntries)
                    break;

                builder.Append("[Directory] ")
                    .AppendLine(Path.GetFileName(subDirectory));
                listedCount++;
            }

            foreach (var file in files.Paths)
            {
                if (listedCount >= MaxListedEntries)
                    break;

                builder.Append("[File] ")
                    .AppendLine(Path.GetFileName(file));
                listedCount++;
            }

            if (subDirectories.HasMore || files.HasMore || subDirectories.Paths.Count + files.Paths.Count > listedCount)
            {
                builder.AppendLine();
                builder.AppendLine($"...<directory content truncated; showing the first {listedCount} entries.>");
            }

            return new CopilotCapabilityResult
            {
                Success = true,
                Summary = $"Listed {GetDirectoryLabel(directoryPath)} with {FormatEntryCount(subDirectories)} subdirectories and {FormatEntryCount(files)} files.",
                Content = builder.ToString().TrimEnd(),
                SuggestedReadableLocalFilePaths = files.Paths
                    .Where(CopilotWorkspaceSearchSupport.IsTextLikeFile)
                    .Take(MaxSuggestedReadableFiles)
                    .ToArray(),
            };
        }

        private static BoundedDirectoryEntries EnumerateBounded(
            Func<IEnumerable<string>> createEntries,
            CancellationToken cancellationToken)
        {
            var paths = new List<string>(MaxListedEntries + 1);
            using var enumerator = createEntries().GetEnumerator();
            while (paths.Count <= MaxListedEntries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!enumerator.MoveNext())
                    break;

                paths.Add(enumerator.Current);
            }

            var hasMore = paths.Count > MaxListedEntries;
            return new BoundedDirectoryEntries(
                paths.Take(MaxListedEntries)
                    .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .ToArray(),
                hasMore);
        }

        private static string FormatEntryCount(BoundedDirectoryEntries entries)
        {
            return entries.HasMore ? $"{entries.Paths.Count}+" : entries.Paths.Count.ToString();
        }

        private readonly record struct BoundedDirectoryEntries(IReadOnlyList<string> Paths, bool HasMore);

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
