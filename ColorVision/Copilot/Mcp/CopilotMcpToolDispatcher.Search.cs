#pragma warning disable CA1822
using System;
using System.Collections.Generic;
using System.IO;
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
            var maxLines = CopilotRecentLogSupport.NormalizeToolMaxLines(GetInt(arguments, "max_lines"));
            var result = await _environment.RecentLogProvider(
                query,
                CopilotRecentLogMode.RecentLines,
                maxLines,
                CopilotRecentLogSupport.DefaultMaxLogChars,
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
                if (!TryResolveAllowedPath(path, out var fullPath, out var pathError))
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
            return ToMcpResult(result.ToCapabilityResult(), "file_search_failed");
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
                if (!TryResolveAllowedPath(path, out var fullPath, out var pathError))
                    return CopilotMcpToolCallResult.Fail("path_not_allowed", pathError);
                if (!Directory.Exists(fullPath) && !File.Exists(fullPath))
                    return CopilotMcpToolCallResult.Fail("path_not_found", $"The search file or directory does not exist: {fullPath}");
                searchRoots = [fullPath];
            }

            var cursor = GetString(arguments, "cursor");
            var result = CopilotGrepTextCapability.SearchWithinScope(searchRoots, roots, query, null, cursor, cancellationToken);
            return ToMcpResult(result.ToCapabilityResult(), "grep_failed");
        }

        private async Task<CopilotMcpToolCallResult> ReadAllowedFileAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var path = GetString(arguments, "path");
            if (string.IsNullOrWhiteSpace(path))
                return CopilotMcpToolCallResult.Fail("missing_path", "The read_allowed_file tool requires a non-empty path argument.");

            if (!TryResolveAllowedPath(path, out var fullPath, out var error))
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

            if (!TryResolveAllowedPath(path, out var fullPath, out var error))
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

        private bool TryResolveAllowedPath(string path, out string fullPath, out string error)
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

            var resolvedFullPath = fullPath;
            if (!CopilotWorkspaceSearchSupport.IsPathWithinRoots(resolvedFullPath, roots))
            {
                if (CopilotWorkspaceSearchSupport.HasReparsePointInPath(resolvedFullPath))
                {
                    error = $"The path crosses a file-system reparse point and is not allowed: {fullPath}";
                    return false;
                }

                error = $"The path is outside the allowed ColorVision workspace roots: {fullPath}";
                return false;
            }

            return true;
        }
    }
}
