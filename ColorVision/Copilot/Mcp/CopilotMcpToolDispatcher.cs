using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Copilot.Mcp
{
    public sealed class CopilotMcpToolDispatcher
    {
        private const int MaxSearchResults = 30;
        private const int MaxGrepMatches = 40;
        private const int MaxLogLines = 300;
        private const int MaxLogChars = 20000;
        private const int MaxAuditEntries = 80;
        private const string LiveContextResourceUri = "colorvision://live-context/current";
        private const string WorkspaceResourceUri = "colorvision://workspace/current";
        private const string LogsResourceUri = "colorvision://logs/recent";
        private const string TemplateResourceUri = "colorvision://template/current";
        private const string FlowResourceUri = "colorvision://flow/current";
        private const string AuditLogResourceUri = "colorvision://mcp/audit-log";

        private readonly CopilotMcpToolEnvironment _environment;

        public CopilotMcpToolDispatcher(CopilotMcpToolEnvironment? environment = null)
        {
            _environment = environment ?? new CopilotMcpToolEnvironment();
        }

        public IReadOnlyList<CopilotMcpToolDescriptor> ListTools()
        {
            return new[]
            {
                Tool("get_server_status", "Return ColorVision MCP server status for this authenticated request.", EmptySchema()),
                Tool("get_enabled_tools", "Return the MCP tools currently exposed by ColorVision.", EmptySchema()),
                Tool("get_audit_log", "Return recent ColorVision MCP tool-call audit entries. Optional argument: max_entries.", Schema(new Dictionary<string, object>
                {
                    ["max_entries"] = IntegerProperty("Maximum audit entries to return.", 1, 200),
                })),
                Tool("get_last_tool_error", "Return the most recent failed MCP tool call, if one is recorded.", EmptySchema()),
                Tool("get_runtime_environment_summary", "Return a safe summary of the MCP runtime environment, workspace roots, live context, logs, and flow availability.", EmptySchema()),
                Tool("get_live_context", "Return the current ColorVision live Copilot context snapshot, if one is published.", EmptySchema()),
                Tool("get_workspace_context", "Return the current ColorVision solution directory, active document, and allowed search roots.", EmptySchema()),
                Tool("get_recent_log", "Read recent ColorVision application log lines. Optional arguments: query, max_lines.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Optional case-insensitive filter text."),
                    ["max_lines"] = IntegerProperty("Maximum recent lines to inspect.", 1, 1000),
                })),
                Tool("search_docs", "Search the published ColorVision documentation index. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Documentation query text."),
                }, "query")),
                Tool("search_files", "Search file names and relative paths under allowed ColorVision workspace roots. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("File name or path fragment."),
                }, "query")),
                Tool("grep_text", "Search text under allowed ColorVision workspace roots using a literal case-insensitive query. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Literal text to search for."),
                }, "query")),
                Tool("read_allowed_file", "Read a text file only if it is under an allowed ColorVision workspace root. Required argument: path. Optional: start_line, end_line.", Schema(new Dictionary<string, object>
                {
                    ["path"] = StringProperty("Absolute path, or a path relative to an allowed root."),
                    ["start_line"] = IntegerProperty("1-based start line.", 1, int.MaxValue),
                    ["end_line"] = IntegerProperty("1-based end line.", 1, int.MaxValue),
                }, "path")),
                Tool("list_allowed_directory", "List a directory only if it is under an allowed ColorVision workspace root. Optional argument: path.", Schema(new Dictionary<string, object>
                {
                    ["path"] = StringProperty("Absolute path, or a path relative to an allowed root. If omitted, allowed roots are listed."),
                })),
                Tool("get_active_template_context", "Return the active template editor context snapshot, if a template editor has published one.", EmptySchema()),
                Tool("get_flow_summary", "Return a read-only summary of the active ColorVision flow, nodes, and recent run state. This never starts or stops a flow.", EmptySchema()),
                Tool("open_panel", "Open a low-risk ColorVision panel. Optional argument: panel. Defaults to copilot.", Schema(new Dictionary<string, object>
                {
                    ["panel"] = StringProperty("Panel id or alias. Use copilot for the ColorVision Copilot panel."),
                })),
                Tool("execute_menu", "Execute a visible main-window menu command by menu name or path. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Menu name or path to execute."),
                }, "query")),
                Tool("set_theme", "Set the ColorVision UI theme. Required argument: theme. Allowed values include system, light, dark, pink, cyan.", Schema(new Dictionary<string, object>
                {
                    ["theme"] = StringProperty("Target theme name."),
                }, "theme")),
                Tool("set_language", "Set the ColorVision UI language. Required argument: language. This may trigger the app's existing restart confirmation flow.", Schema(new Dictionary<string, object>
                {
                    ["language"] = StringProperty("Target language or culture name, for example en-US or zh-Hans."),
                }, "language")),
            };
        }

        public IReadOnlyList<CopilotMcpResourceDescriptor> ListResources()
        {
            return new[]
            {
                Resource(LiveContextResourceUri, "Current live context", "Current ColorVision Copilot live context snapshot."),
                Resource(WorkspaceResourceUri, "Current workspace", "Current solution directory, active document, and allowed search roots."),
                Resource(LogsResourceUri, "Recent logs", "Recent ColorVision application log lines."),
                Resource(TemplateResourceUri, "Current template", "Current active template JSON editor context, when available."),
                Resource(FlowResourceUri, "Current flow", "Current active flow snapshot and selected node summary, when available."),
                Resource(AuditLogResourceUri, "MCP audit log", "Recent ColorVision MCP tool-call audit entries."),
            };
        }

        public async Task<CopilotMcpToolCallResult> ReadResourceAsync(string uri, CancellationToken cancellationToken)
        {
            var normalizedUri = NormalizeResourceUri(uri);
            return normalizedUri switch
            {
                LiveContextResourceUri => GetLiveContext(),
                WorkspaceResourceUri => GetWorkspaceContext(),
                LogsResourceUri => GetRecentLog(null),
                TemplateResourceUri => GetActiveTemplateContext(),
                FlowResourceUri => await GetFlowSummaryAsync(cancellationToken),
                AuditLogResourceUri => GetAuditLog(null),
                _ => CopilotMcpToolCallResult.Fail("resource_not_found", $"Unknown ColorVision MCP resource: {uri}"),
            };
        }

        public async Task<CopilotMcpToolCallResult> CallAsync(string toolName, IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken, string callerSource = "")
        {
            var normalizedToolName = NormalizeToolName(toolName);
            var stopwatch = Stopwatch.StartNew();
            CopilotMcpAuditLogger.ToolCallStarted(normalizedToolName, BuildArgumentSummary(arguments), callerSource);

            try
            {
                var result = normalizedToolName switch
                {
                    "get_server_status" => GetServerStatus(callerSource),
                    "get_enabled_tools" => GetEnabledTools(),
                    "get_audit_log" => GetAuditLog(arguments),
                    "get_last_tool_error" => GetLastToolError(),
                    "get_runtime_environment_summary" => await GetRuntimeEnvironmentSummaryAsync(cancellationToken),
                    "get_live_context" => GetLiveContext(),
                    "get_workspace_context" => GetWorkspaceContext(),
                    "get_recent_log" => GetRecentLog(arguments),
                    "search_docs" => await SearchDocsAsync(arguments, cancellationToken),
                    "search_files" => SearchFiles(arguments, cancellationToken),
                    "grep_text" => GrepText(arguments, cancellationToken),
                    "read_allowed_file" => await ReadAllowedFileAsync(arguments, cancellationToken),
                    "list_allowed_directory" => ListAllowedDirectory(arguments, cancellationToken),
                    "get_active_template_context" => GetActiveTemplateContext(),
                    "get_flow_summary" => await GetFlowSummaryAsync(cancellationToken),
                    "open_panel" => await OpenPanelAsync(arguments, cancellationToken),
                    "execute_menu" => await ExecuteMenuAsync(arguments, cancellationToken),
                    "set_theme" => await SetThemeAsync(arguments, cancellationToken),
                    "set_language" => await SetLanguageAsync(arguments, cancellationToken),
                    _ => CopilotMcpToolCallResult.Fail("tool_not_found", $"Unknown MCP tool: {toolName}"),
                };

                CopilotMcpAuditLogger.ToolCallCompleted(normalizedToolName, result.Success, stopwatch.Elapsed, result.Success ? "OK" : result.Text);
                return result;
            }
            catch (OperationCanceledException)
            {
                CopilotMcpAuditLogger.ToolCallCompleted(normalizedToolName, false, stopwatch.Elapsed, "The MCP tool call was canceled.");
                throw;
            }
            catch (Exception ex)
            {
                CopilotMcpAuditLogger.ToolCallCompleted(normalizedToolName, false, stopwatch.Elapsed, ex.Message);
                return CopilotMcpToolCallResult.Fail("internal_error", $"The MCP tool call failed: {ex.Message}");
            }
        }

        private CopilotMcpToolCallResult GetServerStatus(string callerSource)
        {
            var settings = _environment.RuntimeSettingsProvider();
            var isRunning = SafeInvoke(_environment.ServerRunningProvider);
            var statusMessage = SafeInvoke(_environment.ServerStatusMessageProvider) ?? string.Empty;

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP server status");
            builder.AppendLine("ColorVision process: running");
            builder.AppendLine("Authentication: passed for this request");
            builder.AppendLine($"MCP enabled: {settings.Enabled}");
            builder.AppendLine($"Listener running: {isRunning}");
            builder.AppendLine($"Endpoint: {settings.Endpoint}");
            builder.AppendLine($"Host: {settings.Host}");
            builder.AppendLine($"Port: {settings.Port}");
            builder.AppendLine($"Caller/source: {EmptyLabel(callerSource)}");
            builder.AppendLine($"Status message: {EmptyLabel(statusMessage)}");
            builder.AppendLine("Safety boundary: no shell, no device control, no flow execution, no config mutation, no file deletion, and no arbitrary file read.");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetEnabledTools()
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP enabled tools");
            foreach (var tool in ListTools().OrderBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase))
                builder.AppendLine($"- {tool.Name}: {tool.Description}");

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetAuditLog(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var maxEntries = Math.Clamp(GetInt(arguments, "max_entries") ?? MaxAuditEntries, 1, 200);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(maxEntries);
            return CopilotMcpToolCallResult.Ok(FormatAuditEntries(entries, "ColorVision MCP audit log"));
        }

        private CopilotMcpToolCallResult GetLastToolError()
        {
            var entry = CopilotMcpAuditLogger.GetLastError();
            if (entry == null)
                return CopilotMcpToolCallResult.Ok("No failed MCP tool call is recorded.");

            return CopilotMcpToolCallResult.Ok(FormatAuditEntries(new[] { entry }, "Last ColorVision MCP tool error"));
        }

        private async Task<CopilotMcpToolCallResult> GetRuntimeEnvironmentSummaryAsync(CancellationToken cancellationToken)
        {
            var settings = _environment.RuntimeSettingsProvider();
            var workspace = GetWorkspaceSnapshot();
            var liveContext = _environment.LiveContextProvider();
            var flowSnapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            var logResult = _environment.RecentLogProvider(null, CopilotRecentLogMode.RecentLines, 20, 4000);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP runtime environment summary");
            builder.AppendLine($"MCP enabled: {settings.Enabled}");
            builder.AppendLine($"Endpoint: {settings.Endpoint}");
            builder.AppendLine($"Workspace solution directory: {EmptyLabel(workspace.SolutionDirectoryPath)}");
            builder.AppendLine($"Active document: {EmptyLabel(workspace.ActiveDocumentPath)}");
            builder.AppendLine($"Allowed search roots: {workspace.SearchRootPaths.Count}");
            builder.AppendLine($"Live context source: {EmptyLabel(liveContext?.SourceId)}");
            builder.AppendLine($"Live context title: {EmptyLabel(liveContext?.Title)}");
            builder.AppendLine($"Flow snapshot available: {flowSnapshot != null}");
            builder.AppendLine($"Flow running: {flowSnapshot?.IsRunning.ToString() ?? "(unknown)"}");
            builder.AppendLine($"Selected flow nodes: {flowSnapshot?.Nodes.Count(node => node.IsSelected) ?? 0}");
            builder.AppendLine($"Recent log available: {logResult.Success}");
            builder.AppendLine($"Recent audit entries: {CopilotMcpAuditLogger.GetRecentEntries(MaxAuditEntries).Count}");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetLiveContext()
        {
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null)
                return CopilotMcpToolCallResult.Ok("No live context is currently published.");

            return CopilotMcpToolCallResult.Ok(FormatLiveContext(liveContext));
        }

        private CopilotMcpToolCallResult GetWorkspaceContext()
        {
            var snapshot = GetWorkspaceSnapshot();
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision workspace context");
            builder.AppendLine($"Solution directory: {EmptyLabel(snapshot.SolutionDirectoryPath)}");
            builder.AppendLine($"Active document: {EmptyLabel(snapshot.ActiveDocumentPath)}");
            builder.AppendLine($"Allowed search roots: {snapshot.SearchRootPaths.Count}");
            foreach (var root in snapshot.SearchRootPaths)
                builder.AppendLine($"- {root}");

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetRecentLog(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var query = GetString(arguments, "query");
            var maxLines = Math.Clamp(GetInt(arguments, "max_lines") ?? MaxLogLines, 1, 1000);
            var result = _environment.RecentLogProvider(query, CopilotRecentLogMode.RecentLines, maxLines, MaxLogChars);
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

            var result = CopilotSearchFilesCapability.Search(roots, query, null, allowPlainSearchTerms: true, cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision file search results");
            builder.AppendLine($"Query: {query}");
            builder.AppendLine($"Allowed roots: {roots.Count}");
            builder.AppendLine($"Scanned files: {result.ScannedFileCount}");
            builder.AppendLine($"Matches: {result.Matches.Count}");
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

            var result = CopilotGrepTextCapability.Search(roots, query, null, cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision text search results");
            builder.AppendLine($"Query: {query}");
            builder.AppendLine($"Allowed roots: {roots.Count}");
            builder.AppendLine($"Scanned text files: {result.ScannedTextFileCount}");
            builder.AppendLine($"Matches: {result.Matches.Count}");
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
            var endLine = GetInt(arguments, "end_line");
            var result = await CopilotReadLocalFileCapability.ReadAsync(new[] { fullPath }, fullPath, false, startLine, endLine, cancellationToken);
            return ToMcpResult(result, "read_failed");
        }

        private CopilotMcpToolCallResult ListAllowedDirectory(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var path = GetString(arguments, "path");
            var roots = GetAllowedRoots();
            if (roots.Count == 0)
                return CopilotMcpToolCallResult.Fail("no_allowed_roots", "No allowed ColorVision workspace roots are available.");

            if (string.IsNullOrWhiteSpace(path))
            {
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

            var result = CopilotListDirectoryCapability.List(new[] { fullPath }, fullPath, cancellationToken);
            return ToMcpResult(result, "list_failed");
        }

        private CopilotMcpToolCallResult GetActiveTemplateContext()
        {
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null)
                return CopilotMcpToolCallResult.Ok("No active template context is currently published.");

            if (!liveContext.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return CopilotMcpToolCallResult.Ok("The current live context is not a template editor context.");

            return CopilotMcpToolCallResult.Ok(FormatLiveContext(liveContext));
        }

        private async Task<CopilotMcpToolCallResult> GetFlowSummaryAsync(CancellationToken cancellationToken)
        {
            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            if (snapshot == null)
                return CopilotMcpToolCallResult.Ok("No active ColorVision flow manager is available.");

            return CopilotMcpToolCallResult.Ok(FormatFlowSnapshot(snapshot));
        }

        private async Task<CopilotMcpToolCallResult> OpenPanelAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var panel = GetString(arguments, "panel");
            if (string.IsNullOrWhiteSpace(panel))
                panel = "copilot";

            if (_environment.OpenPanelHandler != null)
                return await _environment.OpenPanelHandler(panel, cancellationToken);

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (string.Equals(panel, "copilot", StringComparison.OrdinalIgnoreCase))
                    CopilotPanelService.GetInstance().ShowPanel();
                else
                    WorkspaceManager.LayoutManager?.ShowPanel(panel);
            });

            return CopilotMcpToolCallResult.Ok($"Panel open request was scheduled: {panel}");
        }

        private async Task<CopilotMcpToolCallResult> ExecuteMenuAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The execute_menu tool requires a non-empty query argument.");

            if (_environment.ExecuteMenuHandler != null)
                return await _environment.ExecuteMenuHandler(query, cancellationToken);

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            var result = await CopilotApplicationCapability.ExecuteMenuAsync(query, cancellationToken);
            return ToMcpResult(result, "menu_execution_failed");
        }

        private async Task<CopilotMcpToolCallResult> SetThemeAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var themeQuery = FirstNonEmpty(GetString(arguments, "theme"), GetString(arguments, "query"));
            if (string.IsNullOrWhiteSpace(themeQuery))
                return CopilotMcpToolCallResult.Fail("missing_theme", "The set_theme tool requires a non-empty theme argument.");

            if (_environment.SetThemeHandler != null)
                return await _environment.SetThemeHandler(themeQuery, cancellationToken);

            var result = await CopilotApplicationCapability.SetThemeAsync(themeQuery, cancellationToken);
            return ToMcpResult(result, "theme_change_failed");
        }

        private async Task<CopilotMcpToolCallResult> SetLanguageAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var languageQuery = FirstNonEmpty(GetString(arguments, "language"), GetString(arguments, "query"));
            if (string.IsNullOrWhiteSpace(languageQuery))
                return CopilotMcpToolCallResult.Fail("missing_language", "The set_language tool requires a non-empty language argument.");

            if (_environment.SetLanguageHandler != null)
                return await _environment.SetLanguageHandler(languageQuery, cancellationToken);

            var result = await CopilotApplicationCapability.SetLanguageAsync(languageQuery, cancellationToken);
            return ToMcpResult(result, "language_change_failed");
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

        private static string FormatLiveContext(CopilotLiveContext liveContext)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision live context");
            builder.AppendLine($"Source id: {EmptyLabel(liveContext.SourceId)}");
            builder.AppendLine($"Title: {EmptyLabel(liveContext.Title)}");
            builder.AppendLine($"Summary: {EmptyLabel(liveContext.Summary)}");
            builder.AppendLine($"Snapshot items: {liveContext.SnapshotItems.Count}");

            foreach (var item in liveContext.SnapshotItems)
            {
                builder.AppendLine();
                builder.AppendLine($"## {EmptyLabel(item.Title)}");
                if (!string.IsNullOrWhiteSpace(item.Summary))
                    builder.AppendLine($"Summary: {item.Summary}");
                if (!string.IsNullOrWhiteSpace(item.Content))
                    builder.AppendLine(item.Content.Trim());
            }

            return builder.ToString().TrimEnd();
        }

        private static CopilotMcpToolCallResult ToMcpResult(CopilotCapabilityResult result, string errorCode)
        {
            var text = string.Join(Environment.NewLine, new[]
            {
                result.Summary,
                result.Content,
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            if (result.Success)
                return CopilotMcpToolCallResult.Ok(text);

            return CopilotMcpToolCallResult.Fail(
                errorCode,
                string.IsNullOrWhiteSpace(result.ErrorMessage) ? text : result.ErrorMessage);
        }

        private static string FormatFlowSnapshot(CopilotFlowContextSnapshot snapshot)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow summary");
            builder.AppendLine($"Flow name: {EmptyLabel(snapshot.FlowName)}");
            builder.AppendLine($"Template name: {EmptyLabel(snapshot.TemplateName)}");
            builder.AppendLine($"Template id: {EmptyLabel(snapshot.TemplateId)}");
            builder.AppendLine($"Status: {EmptyLabel(snapshot.Status)}");
            builder.AppendLine($"Is running: {snapshot.IsRunning}");
            builder.AppendLine($"Batch serial number: {EmptyLabel(snapshot.BatchSerialNumber)}");
            builder.AppendLine($"Batch status: {EmptyLabel(snapshot.BatchStatus)}");
            builder.AppendLine($"Batch result: {EmptyLabel(snapshot.BatchResult)}");
            builder.AppendLine($"Batch progress: {EmptyLabel(snapshot.BatchProgress)}");
            builder.AppendLine($"Last node: {EmptyLabel(snapshot.LastNodeSummary)}");
            builder.AppendLine($"Recent failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
            builder.AppendLine($"Node count: {snapshot.Nodes.Count}");

            if (!string.IsNullOrWhiteSpace(snapshot.RecentRunMessage))
            {
                builder.AppendLine();
                builder.AppendLine("Recent run message:");
                builder.AppendLine(TrimLong(snapshot.RecentRunMessage, 4000));
            }

            foreach (var node in snapshot.Nodes.Take(60))
            {
                builder.AppendLine();
                builder.AppendLine($"Node: {EmptyLabel(node.Title)}");
                builder.AppendLine($"- Type: {EmptyLabel(node.NodeType)}");
                builder.AppendLine($"- Name: {EmptyLabel(node.NodeName)}");
                builder.AppendLine($"- Device code: {EmptyLabel(node.DeviceCode)}");
                builder.AppendLine($"- Node id: {EmptyLabel(node.NodeId)}");
                builder.AppendLine($"- Position: {EmptyLabel(node.Position)}");
                builder.AppendLine($"- Active: {node.IsActive}");
                builder.AppendLine($"- Selected: {node.IsSelected}");
                AppendList(builder, "- Inputs", node.Inputs);
                AppendList(builder, "- Outputs", node.Outputs);
                if (node.Parameters.Count > 0)
                    builder.AppendLine($"- Parameters: {string.Join(", ", node.Parameters.Select(item => $"{item.Name}={item.Value}"))}");
                if (!string.IsNullOrWhiteSpace(node.Mark))
                    builder.AppendLine($"- Mark: {node.Mark}");
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendList(StringBuilder builder, string label, IReadOnlyList<string> values)
        {
            if (values.Count == 0)
                return;

            builder.Append(label).Append(": ").AppendLine(string.Join("; ", values));
        }

        private static CopilotMcpToolDescriptor Tool(string name, string description, object inputSchema) => new()
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
        };

        private static CopilotMcpResourceDescriptor Resource(string uri, string name, string description) => new()
        {
            Uri = uri,
            Name = name,
            Description = description,
            MimeType = "text/plain",
        };

        private static object EmptySchema() => Schema(new Dictionary<string, object>());

        private static object Schema(Dictionary<string, object> properties, params string[] required)
        {
            return new Dictionary<string, object>
            {
                ["type"] = "object",
                ["properties"] = properties,
                ["required"] = required,
                ["additionalProperties"] = false,
            };
        }

        private static object StringProperty(string description) => new Dictionary<string, object>
        {
            ["type"] = "string",
            ["description"] = description,
        };

        private static object IntegerProperty(string description, int minimum, int maximum) => new Dictionary<string, object>
        {
            ["type"] = "integer",
            ["description"] = description,
            ["minimum"] = minimum,
            ["maximum"] = maximum,
        };

        private static string NormalizeToolName(string? toolName)
        {
            return (toolName ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string NormalizeResourceUri(string? uri)
        {
            return (uri ?? string.Empty).Trim().ToLowerInvariant();
        }

        private static string FormatAuditEntries(IReadOnlyList<CopilotMcpAuditEntry> entries, string title)
        {
            var builder = new StringBuilder();
            builder.AppendLine(title);
            builder.AppendLine($"Entries: {entries.Count}");

            foreach (var entry in entries)
            {
                builder.AppendLine();
                builder.AppendLine($"- Timestamp UTC: {entry.TimestampUtc:O}");
                builder.AppendLine($"  Tool: {EmptyLabel(entry.ToolName)}");
                builder.AppendLine($"  Arguments: {EmptyLabel(entry.ArgumentSummary)}");
                builder.AppendLine($"  Success: {entry.Success}");
                builder.AppendLine($"  Duration ms: {entry.DurationMs}");
                builder.AppendLine($"  Error: {EmptyLabel(entry.ErrorMessage)}");
                builder.AppendLine($"  Caller/source: {EmptyLabel(entry.CallerSource)}");
            }

            return builder.ToString().TrimEnd();
        }

        private static string BuildArgumentSummary(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return "{}";

            return string.Join(", ", arguments.Select(pair => $"{pair.Key}={TrimLong(CopilotMcpAuditLogger.RedactArgument(pair.Key, pair.Value.ToString()), 160)}"));
        }

        private static string GetString(IReadOnlyDictionary<string, JsonElement>? arguments, params string[] names)
        {
            if (arguments == null)
                return string.Empty;

            foreach (var name in names)
            {
                if (!arguments.TryGetValue(name, out var value))
                    continue;

                return value.ValueKind switch
                {
                    JsonValueKind.String => value.GetString() ?? string.Empty,
                    JsonValueKind.Number => value.ToString(),
                    JsonValueKind.True => "true",
                    JsonValueKind.False => "false",
                    _ => value.ToString(),
                };
            }

            return string.Empty;
        }

        private static int? GetInt(IReadOnlyDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments == null || !arguments.TryGetValue(name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
                return number;

            return null;
        }

        private static string EmptyLabel(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim();
        }

        private static string TrimLong(string? value, int maxLength)
        {
            var text = value ?? string.Empty;
            return text.Length <= maxLength ? text : text[..maxLength] + "...";
        }

        private static string FirstNonEmpty(params string[] values)
        {
            return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
        }

        private static T? SafeInvoke<T>(Func<T> provider)
        {
            try
            {
                return provider();
            }
            catch
            {
                return default;
            }
        }
    }
}