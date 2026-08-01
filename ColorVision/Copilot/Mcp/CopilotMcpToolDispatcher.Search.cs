#pragma warning disable CA1822
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        private async Task<CopilotMcpToolCallResult> GetRecentLogAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            var maxLines = Math.Clamp(GetInt(arguments, "max_lines") ?? MaxLogLines, 1, 1000);
            var result = await _environment.RecentLogProvider(
                query,
                CopilotRecentLogMode.RecentLines,
                maxLines,
                MaxLogChars,
                cancellationToken);
            return ToMcpResult(result, "log_unavailable");
        }

        private async Task<CopilotMcpToolCallResult> SearchDocsAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The search_docs tool requires a non-empty query argument.");

            var result = await CopilotDocsCapability.SearchAsync(query, cancellationToken);
            return ToMcpResult(result, "docs_search_failed");
        }

        private CopilotMcpToolCallResult SearchFiles(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The search_files tool requires a non-empty query argument.");

            var roots = GetAllowedRoots();
            if (roots.Count == 0)
                return CopilotMcpToolCallResult.Fail("no_allowed_roots", "No allowed ColorVision workspace roots are available.");

            IReadOnlyList<string> searchRoots = roots;
            var path = GetString(arguments, "path");
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!TryResolveAllowedPath(path, requireExisting: false, out var fullPath, out var pathError))
                    return CopilotMcpToolCallResult.Fail("path_not_allowed", pathError);
                if (!Directory.Exists(fullPath))
                    return CopilotMcpToolCallResult.Fail("directory_not_found", $"The search directory does not exist: {fullPath}");
                searchRoots = [fullPath];
            }

            var cursor = GetString(arguments, "cursor");
            var result = CopilotSearchFilesCapability.SearchWithinScope(
                searchRoots,
                roots,
                query,
                fallbackText: null,
                allowPlainSearchTerms: true,
                cursor,
                cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision file search results");
            builder.AppendLine($"Query: {query}");
            builder.AppendLine($"Allowed roots: {roots.Count}");
            builder.AppendLine($"Scanned files: {result.ScannedFileCount}");
            builder.AppendLine($"Matched files: {result.MatchedFileCount}");
            builder.AppendLine($"Matches shown: {result.Matches.Count}");
            builder.AppendLine($"Scan complete: {result.ScanComplete.ToString().ToLowerInvariant()}");
            builder.AppendLine($"Results complete: {result.ResultsComplete.ToString().ToLowerInvariant()}");
            if (!string.IsNullOrWhiteSpace(result.NextCursor))
                builder.AppendLine($"Next cursor: {result.NextCursor}");
            builder.AppendLine();

            foreach (var match in result.Matches.Take(MaxSearchResults))
                builder.AppendLine($"- {match.DisplayPath}");

            return result.Success
                ? CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd())
                : CopilotMcpToolCallResult.Fail("file_search_failed", string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.Summary : result.ErrorMessage);
        }

        private CopilotMcpToolCallResult GrepText(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The grep_text tool requires a non-empty query argument.");

            var roots = GetAllowedRoots();
            if (roots.Count == 0)
                return CopilotMcpToolCallResult.Fail("no_allowed_roots", "No allowed ColorVision workspace roots are available.");

            IReadOnlyList<string> searchRoots = roots;
            var path = GetString(arguments, "path");
            if (!string.IsNullOrWhiteSpace(path))
            {
                if (!TryResolveAllowedPath(path, requireExisting: false, out var fullPath, out var pathError))
                    return CopilotMcpToolCallResult.Fail("path_not_allowed", pathError);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                    return CopilotMcpToolCallResult.Fail("path_not_found", $"The search file or directory does not exist: {fullPath}");
                searchRoots = [fullPath];
            }

            var cursor = GetString(arguments, "cursor");
            var result = CopilotGrepTextCapability.SearchWithinScope(searchRoots, roots, query, null, cursor, cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision text search results");
            builder.AppendLine($"Query: {query}");
            builder.AppendLine($"Allowed roots: {roots.Count}");
            builder.AppendLine($"Scanned text files: {result.ScannedTextFileCount}");
            builder.AppendLine($"Matches shown: {result.Matches.Count}");
            builder.AppendLine($"Scan complete: {result.ScanComplete.ToString().ToLowerInvariant()}");
            builder.AppendLine($"Results complete: {result.ResultsComplete.ToString().ToLowerInvariant()}");
            if (!string.IsNullOrWhiteSpace(result.NextCursor))
                builder.AppendLine($"Next cursor: {result.NextCursor}");
            builder.AppendLine();
            foreach (var match in result.Matches.Take(MaxGrepMatches))
                builder.AppendLine($"- {match.DisplayPath}:{match.LineNumber}: {CopilotWorkspaceSearchSupport.TruncateLine(match.LineText, 220)}");

            return result.Success
                ? CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd())
                : CopilotMcpToolCallResult.Fail("grep_failed", string.IsNullOrWhiteSpace(result.ErrorMessage) ? result.Summary : result.ErrorMessage);
        }

        private async Task<CopilotMcpToolCallResult> ReadAllowedFileAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var path = GetString(arguments, "path");
            if (string.IsNullOrWhiteSpace(path))
                return CopilotMcpToolCallResult.Fail("missing_path", "The read_allowed_file tool requires a non-empty path argument.");

            if (!TryResolveAllowedPath(path, requireExisting: false, out var fullPath, out var error))
                return CopilotMcpToolCallResult.Fail("path_not_allowed", error);

            if (!File.Exists(fullPath))
                return CopilotMcpToolCallResult.Fail("file_not_found", $"The file does not exist: {fullPath}");

            if (!CopilotWorkspaceSearchSupport.IsTextLikeFile(fullPath))
                return CopilotMcpToolCallResult.Fail("unsupported_file_type", "The file extension is not in the ColorVision MCP text allow-list.");

            var startLine = GetInt(arguments, "start_line");
            var startColumn = GetInt(arguments, "start_column");
            var endLine = GetInt(arguments, "end_line");
            if (startColumn.HasValue && !startLine.HasValue)
                return CopilotMcpToolCallResult.Fail("invalid_range", "The start_column argument requires start_line.");

            var result = await CopilotReadLocalFileCapability.ReadAsync(new[] { fullPath }, fullPath, false, startLine, startColumn, endLine, cancellationToken);
            return ToMcpResult(result, "read_failed");
        }

        private CopilotMcpToolCallResult ListAllowedDirectory(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var path = GetString(arguments, "path");
            var cursor = GetString(arguments, "cursor");
            var roots = GetAllowedRoots();
            if (roots.Count == 0)
                return CopilotMcpToolCallResult.Fail("no_allowed_roots", "No allowed ColorVision workspace roots are available.");

            if (string.IsNullOrWhiteSpace(path))
            {
                if (!string.IsNullOrWhiteSpace(cursor))
                    return CopilotMcpToolCallResult.Fail("missing_path", "A directory cursor requires the same non-empty path used for the preceding page.");

                var rootBuilder = new StringBuilder();
                rootBuilder.AppendLine("ColorVision allowed directory roots");
                foreach (var root in roots)
                    rootBuilder.AppendLine($"- {root}");
                return CopilotMcpToolCallResult.Ok(rootBuilder.ToString().TrimEnd());
            }

            if (!TryResolveAllowedPath(path, requireExisting: false, out var fullPath, out var error))
                return CopilotMcpToolCallResult.Fail("path_not_allowed", error);

            if (!Directory.Exists(fullPath))
                return CopilotMcpToolCallResult.Fail("directory_not_found", $"The directory does not exist: {fullPath}");

            var result = CopilotListDirectoryCapability.List(new[] { fullPath }, fullPath, cursor, cancellationToken);
            return ToMcpResult(result, "list_failed");
        }

        private CopilotMcpWorkspaceSnapshot GetWorkspaceSnapshot()
        {
            return _environment.WorkspaceSnapshotProvider() ?? new CopilotMcpWorkspaceSnapshot();
        }

        private IReadOnlyList<string> GetAllowedRoots()
        {
            return CopilotWorkspaceSearchSupport.NormalizeSearchRoots(GetWorkspaceSnapshot().SearchRootPaths);
        }

        private bool TryResolveAllowedPath(string path, bool requireExisting, out string fullPath, out string error)
        {
            fullPath = string.Empty;
            error = string.Empty;

            var roots = GetAllowedRoots();
            if (roots.Count == 0)
            {
                error = "No allowed ColorVision workspace roots are available.";
                return false;
            }

            try
            {
                fullPath = Path.IsPathRooted(path)
                    ? Path.GetFullPath(path)
                    : Path.GetFullPath(Path.Combine(roots[0], path));
            }
            catch (Exception ex)
            {
                error = $"The path is invalid: {ex.Message}";
                return false;
            }

            if (requireExisting && !File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                error = $"The path does not exist: {fullPath}";
                return false;
            }

            var resolvedFullPath = fullPath;
            if (!roots.Any(root => IsPathInsideRoot(resolvedFullPath, root)))
            {
                error = $"The path is outside the allowed ColorVision workspace roots: {fullPath}";
                return false;
            }

            if (CopilotWorkspaceSearchSupport.HasReparsePointInPath(resolvedFullPath))
            {
                error = $"The path crosses a file-system reparse point and is not allowed: {fullPath}";
                return false;
            }

            return true;
        }

        private static bool IsPathInsideRoot(string path, string root)
        {
            if (string.Equals(path.TrimEnd(Path.DirectorySeparatorChar), root.TrimEnd(Path.DirectorySeparatorChar), StringComparison.OrdinalIgnoreCase))
                return true;

            var rootWithSeparator = root.EndsWith(Path.DirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
            return path.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }
    }
}
