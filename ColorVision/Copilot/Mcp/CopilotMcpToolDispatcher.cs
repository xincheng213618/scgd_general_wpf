using ColorVision.Engine.Templates.Flow;
using ColorVision.Solution.Workspace;
using ColorVision.Themes;
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
        private static readonly string[] SupportedPanelAliases =
        {
            "copilot",
            "log",
            "config",
            "solution",
            "template",
            "flow",
            "device",
        };

        private readonly CopilotMcpToolEnvironment _environment;

        private readonly record struct CopilotPanelTarget(string Alias, string TargetId);

        public CopilotMcpToolDispatcher(CopilotMcpToolEnvironment? environment = null)
        {
            _environment = environment ?? new CopilotMcpToolEnvironment();
        }

        public IReadOnlyList<CopilotMcpToolDescriptor> ListTools()
        {
            return new[]
            {
                Tool("get_server_status", "Return ColorVision MCP server status for this authenticated request.", EmptySchema(), "status", "read-only", "Call get_server_status with no arguments."),
                Tool("get_enabled_tools", "Return the MCP tools currently exposed by ColorVision.", EmptySchema(), "status", "read-only", "Call get_enabled_tools with no arguments."),
                Tool("get_audit_log", "Return recent ColorVision MCP tool-call audit entries. Optional arguments: max_entries, tool, failed_only.", Schema(new Dictionary<string, object>
                {
                    ["max_entries"] = IntegerProperty("Maximum audit entries to return.", 1, 200),
                    ["tool"] = StringProperty("Optional tool name filter."),
                    ["action_id"] = StringProperty("Optional confirmable action id filter."),
                    ["failed_only"] = BooleanProperty("When true, return only failed entries."),
                }), "audit", "read-only", "Call get_audit_log with { \"max_entries\": 20, \"failed_only\": true }."),
                Tool("get_last_tool_error", "Return the most recent failed MCP tool call, if one is recorded.", EmptySchema(), "audit", "read-only", "Call get_last_tool_error with no arguments."),
                Tool("get_runtime_environment_summary", "Return a safe summary of the MCP runtime environment, workspace roots, live context, logs, and flow availability.", EmptySchema(), "status", "read-only", "Call get_runtime_environment_summary before diagnostics."),
                Tool("get_live_context", "Return the current ColorVision live Copilot context snapshot, if one is published.", EmptySchema(), "context", "read-only", "Call get_live_context with no arguments."),
                Tool("get_workspace_context", "Return the current ColorVision solution directory, active document, and allowed search roots.", EmptySchema(), "context", "read-only", "Call get_workspace_context to understand allowed roots."),
                Tool("get_recent_log", "Read recent ColorVision application log lines. Optional arguments: query, max_lines.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Optional case-insensitive filter text."),
                    ["max_lines"] = IntegerProperty("Maximum recent lines to inspect.", 1, 1000),
                }), "search", "read-only", "Call get_recent_log with { \"query\": \"error\", \"max_lines\": 200 }."),
                Tool("search_docs", "Search the published ColorVision documentation index. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Documentation query text."),
                }, "query"), "search", "read-only", "Call search_docs with { \"query\": \"plugin development\" }."),
                Tool("search_files", "Search file names and relative paths under allowed ColorVision workspace roots. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("File name or path fragment."),
                }, "query"), "search", "read-only", "Call search_files with { \"query\": \"DeviceCamera\" }."),
                Tool("grep_text", "Search text under allowed ColorVision workspace roots using a literal case-insensitive query. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Literal text to search for."),
                }, "query"), "search", "read-only", "Call grep_text with { \"query\": \"FlowEngineManager\" }."),
                Tool("read_allowed_file", "Read a text file only if it is under an allowed ColorVision workspace root. Required argument: path. Optional: start_line, end_line.", Schema(new Dictionary<string, object>
                {
                    ["path"] = StringProperty("Absolute path, or a path relative to an allowed root."),
                    ["start_line"] = IntegerProperty("1-based start line.", 1, int.MaxValue),
                    ["end_line"] = IntegerProperty("1-based end line.", 1, int.MaxValue),
                }, "path"), "file", "read-only", "Call read_allowed_file with { \"path\": \"README.md\", \"start_line\": 1, \"end_line\": 40 }."),
                Tool("list_allowed_directory", "List a directory only if it is under an allowed ColorVision workspace root. Optional argument: path.", Schema(new Dictionary<string, object>
                {
                    ["path"] = StringProperty("Absolute path, or a path relative to an allowed root. If omitted, allowed roots are listed."),
                }), "file", "read-only", "Call list_allowed_directory with { \"path\": \"Engine\" }."),
                Tool("get_active_template_context", "Return the active template editor context snapshot, if a template editor has published one.", EmptySchema(), "context", "read-only", "Call get_active_template_context before editing template JSON."),
                Tool("get_flow_summary", "Return a read-only summary of the active ColorVision flow, nodes, and recent run state. This never starts or stops a flow.", EmptySchema(), "context", "read-only", "Call get_flow_summary to inspect the current flow."),
                Tool("open_panel", "Open a low-risk ColorVision panel. Optional argument: panel. Defaults to copilot.", Schema(new Dictionary<string, object>
                {
                    ["panel"] = StringProperty("Panel id or alias. Supported aliases: copilot, log, config, solution, template, flow, device."),
                }), "app-control", "low-risk-action", "Call open_panel with { \"panel\": \"copilot\" }."),
                Tool("execute_menu", "Execute a visible main-window menu command by menu name or path. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Menu name or path to execute."),
                    ["dry_run"] = BooleanProperty("When true, resolve the menu and report risk without executing it."),
                }, "query"), "app-control", "confirmation-required", "Call execute_menu with { \"query\": \"View > Copilot\", \"dry_run\": true } first."),
                Tool("confirm_action", "Execute a previously approved confirmation-required action. Required arguments: action_id, tool_name, arguments_summary.", Schema(new Dictionary<string, object>
                {
                    ["action_id"] = StringProperty("Confirmable action id returned by a previous tool call."),
                    ["tool_name"] = StringProperty("Original tool name for the confirmable action."),
                    ["arguments_summary"] = StringProperty("Original redacted arguments_summary returned with the action."),
                }, "action_id", "tool_name", "arguments_summary"), "app-control", "confirmation-required", "Call confirm_action only after the user approves the action in ColorVision."),
                Tool("preview_template_patch", "Preview a proposed template JSON patch without saving it. Required arguments: template_identifier, proposed_changes. Optional: current_json.", Schema(new Dictionary<string, object>
                {
                    ["template_identifier"] = StringProperty("Template name, id, key, or editor identifier."),
                    ["proposed_changes"] = new Dictionary<string, object>
                    {
                        ["description"] = "Object containing proposed top-level JSON changes, or a JSON object string.",
                    },
                    ["current_json"] = StringProperty("Optional current template JSON. If omitted, the active template editor context is used."),
                }, "template_identifier", "proposed_changes"), "context", "read-only", "Call preview_template_patch with { \"template_identifier\": \"Default\", \"proposed_changes\": { \"Exposure\": 12 } }."),
                Tool("preview_flow_action", "Preview a low-risk flow navigation/inspection action without running or stopping the flow. Required argument: action.", Schema(new Dictionary<string, object>
                {
                    ["action"] = StringProperty("Preview action: select_node, open_node_property, inspect_node_errors. start/stop/run requests are refused."),
                    ["node_id"] = StringProperty("Optional flow node id."),
                    ["node_name"] = StringProperty("Optional flow node name or title."),
                }, "action"), "context", "read-only", "Call preview_flow_action with { \"action\": \"inspect_node_errors\", \"node_name\": \"Camera\" }."),
                Tool("set_theme", "Set the ColorVision UI theme. Required argument: theme. Allowed values include system, light, dark, pink, cyan.", Schema(new Dictionary<string, object>
                {
                    ["theme"] = StringProperty("Target theme name."),
                }, "theme"), "app-control", "low-risk-action", "Call set_theme with { \"theme\": \"dark\" }."),
                Tool("set_language", "Set the ColorVision UI language. Required argument: language. This may trigger the app's existing restart confirmation flow.", Schema(new Dictionary<string, object>
                {
                    ["language"] = StringProperty("Target language or culture name, for example en-US or zh-Hans."),
                }, "language"), "app-control", "confirmation-required", "Call set_language with { \"language\": \"en-US\" } and expect user confirmation."),
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
                    "confirm_action" => await ConfirmActionAsync(arguments, cancellationToken),
                    "preview_template_patch" => PreviewTemplatePatch(arguments),
                    "preview_flow_action" => await PreviewFlowActionAsync(arguments, cancellationToken),
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
            builder.AppendLine($"Pending actions: {CopilotMcpConfirmationStore.Instance.PendingCount}");
            builder.AppendLine("Safety boundary: no shell, no device control, no flow execution, no config mutation, no file deletion, and no arbitrary file read.");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetEnabledTools()
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP enabled tools");
            var categoryOrder = new[] { "status", "context", "search", "file", "app-control", "audit" };
            var tools = ListTools()
                .OrderBy(tool => Array.IndexOf(categoryOrder, tool.Category) < 0 ? int.MaxValue : Array.IndexOf(categoryOrder, tool.Category))
                .ThenBy(tool => tool.Name, StringComparer.OrdinalIgnoreCase)
                .GroupBy(tool => string.IsNullOrWhiteSpace(tool.Category) ? "other" : tool.Category, StringComparer.OrdinalIgnoreCase);

            foreach (var group in tools)
            {
                builder.AppendLine();
                builder.AppendLine($"## {group.Key}");
                foreach (var tool in group)
                {
                    builder.AppendLine($"- {tool.Name} [{tool.RiskLevel}]: {tool.Description}");
                    builder.AppendLine($"  Example: {tool.UsageExample}");
                }
            }

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetAuditLog(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var maxEntries = Math.Clamp(GetInt(arguments, "max_entries") ?? MaxAuditEntries, 1, 200);
            var toolFilter = NormalizeToolName(GetString(arguments, "tool"));
            var actionIdFilter = GetString(arguments, "action_id").Trim();
            var failedOnly = GetBool(arguments, "failed_only") ?? false;
            var entries = CopilotMcpAuditLogger.GetRecentEntries(200)
                .Where(entry => string.IsNullOrWhiteSpace(toolFilter) || string.Equals(entry.ToolName, toolFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry => string.IsNullOrWhiteSpace(actionIdFilter) || string.Equals(entry.ActionId, actionIdFilter, StringComparison.OrdinalIgnoreCase))
                .Where(entry => !failedOnly || !entry.Success)
                .TakeLast(maxEntries)
                .ToArray();
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
            using var process = Process.GetCurrentProcess();
            var appDataDirectory = SafeInvoke(() => Environments.DirAppData) ?? string.Empty;
            var configDirectory = string.IsNullOrWhiteSpace(appDataDirectory) ? string.Empty : Path.Combine(appDataDirectory, "Config");
            var logFilePath = SafeInvoke(() => Environments.DirLog) ?? string.Empty;
            var theme = SafeInvoke(() => ThemeConfig.Instance.Theme.ToString()) ?? "(unknown)";
            var logDirectories = SafeInvoke(() => CopilotRecentLogSupport.GetCandidateLogDirectories()
                .Where(directory => !string.IsNullOrWhiteSpace(directory))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray()) ?? Array.Empty<string>();

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP runtime environment summary");
            builder.AppendLine($"ColorVision version: {EmptyLabel(typeof(CopilotMcpToolDispatcher).Assembly.GetName().Version?.ToString())}");
            builder.AppendLine($"Process: {process.ProcessName} ({process.Id})");
            builder.AppendLine($"Process start time: {EmptyLabel(SafeInvoke(() => process.StartTime.ToString("O", CultureInfo.InvariantCulture)))}");
            builder.AppendLine($"Base directory: {EmptyLabel(AppDomain.CurrentDomain.BaseDirectory)}");
            builder.AppendLine($"Config directory: {EmptyLabel(configDirectory)}");
            builder.AppendLine($"AppData directory: {EmptyLabel(appDataDirectory)}");
            builder.AppendLine($"Log file path: {EmptyLabel(logFilePath)}");
            builder.AppendLine($"Candidate log directories: {logDirectories.Length}");
            foreach (var directory in logDirectories)
                builder.AppendLine($"- {directory}");
            builder.AppendLine($"Current UI culture: {EmptyLabel(Thread.CurrentThread.CurrentUICulture.Name)}");
            builder.AppendLine($"Current culture: {EmptyLabel(Thread.CurrentThread.CurrentCulture.Name)}");
            builder.AppendLine($"Theme: {theme}");
            builder.AppendLine($"MCP enabled: {settings.Enabled}");
            builder.AppendLine($"MCP listener running: {SafeInvoke(_environment.ServerRunningProvider)}");
            builder.AppendLine($"Endpoint: {settings.Endpoint}");
            builder.AppendLine($"MCP status message: {EmptyLabel(SafeInvoke(_environment.ServerStatusMessageProvider))}");
            builder.AppendLine($"Workspace solution directory: {EmptyLabel(workspace.SolutionDirectoryPath)}");
            builder.AppendLine($"Active document: {EmptyLabel(workspace.ActiveDocumentPath)}");
            builder.AppendLine($"Allowed search roots: {workspace.SearchRootPaths.Count}");
            foreach (var root in workspace.SearchRootPaths)
                builder.AppendLine($"- {root}");
            builder.AppendLine($"Live context source: {EmptyLabel(liveContext?.SourceId)}");
            builder.AppendLine($"Live context title: {EmptyLabel(liveContext?.Title)}");
            builder.AppendLine($"Flow snapshot available: {flowSnapshot != null}");
            builder.AppendLine($"Flow running: {flowSnapshot?.IsRunning.ToString() ?? "(unknown)"}");
            builder.AppendLine($"Selected flow nodes: {flowSnapshot?.Nodes.Count(node => node.IsSelected) ?? 0}");
            builder.AppendLine($"Recent log available: {logResult.Success}");
            builder.AppendLine($"Recent audit entries: {CopilotMcpAuditLogger.GetRecentEntries(MaxAuditEntries).Count}");
            builder.AppendLine($"Pending actions: {CopilotMcpConfirmationStore.Instance.PendingCount}");
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

            return CopilotMcpToolCallResult.Ok(FormatTemplateLiveContext(liveContext));
        }

        private async Task<CopilotMcpToolCallResult> GetFlowSummaryAsync(CancellationToken cancellationToken)
        {
            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            if (snapshot == null)
                return CopilotMcpToolCallResult.Ok("No active flow is available.");

            return CopilotMcpToolCallResult.Ok(FormatFlowSnapshot(snapshot));
        }

        private async Task<CopilotMcpToolCallResult> OpenPanelAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var panel = GetString(arguments, "panel");
            if (string.IsNullOrWhiteSpace(panel))
                panel = "copilot";

            var panelTarget = ResolvePanelTarget(panel);
            if (panelTarget == null)
            {
                return CopilotMcpToolCallResult.Fail(
                    "panel_alias_not_supported",
                    $"Unsupported panel alias: {panel}. Supported aliases: {string.Join(", ", SupportedPanelAliases)}.");
            }

            if (_environment.OpenPanelHandler != null)
                return await _environment.OpenPanelHandler(panelTarget.Value.Alias, cancellationToken);

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            var layoutManager = WorkspaceManager.LayoutManager;
            if (layoutManager == null)
                return CopilotMcpToolCallResult.Fail("layout_unavailable", "The ColorVision docking layout manager is not available.");

            if (!string.Equals(panelTarget.Value.TargetId, CopilotPanelService.PanelId, StringComparison.OrdinalIgnoreCase)
                && !layoutManager.GetRegisteredPanelIds().Contains(panelTarget.Value.TargetId, StringComparer.OrdinalIgnoreCase))
            {
                return CopilotMcpToolCallResult.Fail(
                    "panel_not_registered",
                    $"Panel alias '{panelTarget.Value.Alias}' resolved to '{panelTarget.Value.TargetId}', but that panel is not registered. Supported aliases: {string.Join(", ", SupportedPanelAliases)}.");
            }

            await Application.Current.Dispatcher.InvokeAsync(() =>
            {
                if (string.Equals(panelTarget.Value.TargetId, CopilotPanelService.PanelId, StringComparison.OrdinalIgnoreCase))
                    CopilotPanelService.GetInstance().ShowPanel();
                else
                    layoutManager.ShowPanel(panelTarget.Value.TargetId);
            });

            return CopilotMcpToolCallResult.Ok($"Panel open request was scheduled: alias={panelTarget.Value.Alias}, target={panelTarget.Value.TargetId}, risk=low-risk-action.");
        }

        private static CopilotPanelTarget? ResolvePanelTarget(string panel)
        {
            var alias = (panel ?? string.Empty).Trim();
            var normalizedAlias = alias.ToLowerInvariant();
            var targetId = normalizedAlias switch
            {
                "" => CopilotPanelService.PanelId,
                "copilot" => CopilotPanelService.PanelId,
                "log" => "LogPanel",
                "solution" => "ProjectPanel",
                "config" => "ProjectPanel",
                "template" => "ProjectPanel",
                "flow" => FlowNodePropertyPanel.PanelId,
                "device" => "AcquirePanel",
                _ => string.Empty,
            };

            if (string.IsNullOrWhiteSpace(targetId))
                return null;

            return new CopilotPanelTarget(string.IsNullOrWhiteSpace(normalizedAlias) ? "copilot" : normalizedAlias, targetId);
        }

        private async Task<CopilotMcpToolCallResult> ExecuteMenuAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The execute_menu tool requires a non-empty query argument.");

            var dryRun = GetBool(arguments, "dry_run") ?? true;

            if (_environment.ExecuteMenuHandler != null)
            {
                var handlerResult = await _environment.ExecuteMenuHandler(query, dryRun, cancellationToken);
                if (!dryRun && IsConfirmationRequiredResult(handlerResult))
                    return CreateConfirmableActionResult(
                        "Confirm menu command",
                        $"Execute ColorVision menu command: {query}",
                        "execute_menu",
                        arguments,
                        handlerResult.Text,
                        token => _environment.ExecuteMenuHandler(query, false, token));

                return handlerResult;
            }

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            var result = await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun, allowConfirmationRequired: false, cancellationToken);
            if (!dryRun && IsConfirmationRequiredResult(result))
            {
                return CreateConfirmableActionResult(
                    "Confirm menu command",
                    $"Execute ColorVision menu command: {query}",
                    "execute_menu",
                    arguments,
                    string.Join(Environment.NewLine, new[] { result.Summary, result.Content, result.ErrorMessage }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    async token => ToMcpResult(await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: false, allowConfirmationRequired: true, token), "menu_execution_failed"));
            }

            return ToMcpResult(result, "menu_execution_failed");
        }

        private async Task<CopilotMcpToolCallResult> ConfirmActionAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var actionId = GetString(arguments, "action_id");
            var toolName = NormalizeToolName(GetString(arguments, "tool_name"));
            var argumentsSummary = GetString(arguments, "arguments_summary");

            if (string.IsNullOrWhiteSpace(actionId))
                return CopilotMcpToolCallResult.Fail("missing_action_id", "The confirm_action tool requires action_id.");

            if (string.IsNullOrWhiteSpace(toolName))
                return CopilotMcpToolCallResult.Fail("missing_tool_name", "The confirm_action tool requires tool_name.");

            if (string.IsNullOrWhiteSpace(argumentsSummary))
                return CopilotMcpToolCallResult.Fail("missing_arguments_summary", "The confirm_action tool requires the original arguments_summary.");

            return await CopilotMcpConfirmationStore.Instance.ExecuteApprovedAsync(actionId, toolName, argumentsSummary, cancellationToken);
        }

        private CopilotMcpToolCallResult PreviewTemplatePatch(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var templateIdentifier = FirstNonEmpty(GetString(arguments, "template_identifier"), GetString(arguments, "template"), GetString(arguments, "identifier"));
            if (string.IsNullOrWhiteSpace(templateIdentifier))
                return CopilotMcpToolCallResult.Fail("missing_template_identifier", "The preview_template_patch tool requires template_identifier.");

            if (!TryGetJsonArgument(arguments, "proposed_changes", out var proposedChangesJson, out var proposedChangesError))
                return CopilotMcpToolCallResult.Fail("missing_proposed_changes", proposedChangesError);

            var currentJson = GetString(arguments, "current_json");
            if (string.IsNullOrWhiteSpace(currentJson))
                currentJson = ExtractCurrentTemplateJson();

            if (string.IsNullOrWhiteSpace(currentJson))
                return CopilotMcpToolCallResult.Fail("template_context_unavailable", "No current template JSON is available. Provide current_json or open a template JSON editor context.");

            try
            {
                using var currentDocument = JsonDocument.Parse(currentJson);
                using var proposedDocument = JsonDocument.Parse(proposedChangesJson);

                if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                    return CopilotMcpToolCallResult.Fail("invalid_template_json", $"The current template JSON root must be an object, but was {currentDocument.RootElement.ValueKind}.");

                if (proposedDocument.RootElement.ValueKind != JsonValueKind.Object)
                    return CopilotMcpToolCallResult.Fail("invalid_proposed_changes", $"The proposed_changes root must be an object, but was {proposedDocument.RootElement.ValueKind}.");

                if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                    return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", $"preview_template_patch refuses to modify sensitive fields: {sensitivePath}.");

                var changes = new List<string>();
                foreach (var proposedProperty in proposedDocument.RootElement.EnumerateObject())
                {
                    currentDocument.RootElement.TryGetProperty(proposedProperty.Name, out var currentValue);
                    var currentText = currentValue.ValueKind == JsonValueKind.Undefined ? "(missing)" : DescribeJsonValue(currentValue);
                    var proposedText = DescribeJsonValue(proposedProperty.Value);
                    if (string.Equals(currentText, proposedText, StringComparison.Ordinal))
                        continue;

                    changes.Add($"- {proposedProperty.Name}: {currentText} -> {proposedText}");
                }

                var builder = new StringBuilder();
                builder.AppendLine("ColorVision template patch preview");
                builder.AppendLine($"Template identifier: {templateIdentifier.Trim()}");
                builder.AppendLine("Mode: preview only");
                builder.AppendLine("Would save: False");
                builder.AppendLine("Current JSON valid: True");
                builder.AppendLine("Proposed changes valid: True");
                builder.AppendLine($"Changed key fields: {changes.Count}");
                foreach (var change in changes.Take(80))
                    builder.AppendLine(change);
                builder.AppendLine("No template file was saved or mutated.");
                return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
            }
            catch (JsonException ex)
            {
                return CopilotMcpToolCallResult.Fail("invalid_template_patch_json", $"Template patch preview failed JSON validation: {ex.Message}");
            }
        }

        private async Task<CopilotMcpToolCallResult> PreviewFlowActionAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var action = NormalizeActionName(GetString(arguments, "action"));
            if (string.IsNullOrWhiteSpace(action))
                return CopilotMcpToolCallResult.Fail("missing_flow_action", "The preview_flow_action tool requires action.");

            if (IsForbiddenFlowExecutionAction(action))
            {
                return CopilotMcpToolCallResult.Fail(
                    "flow_execution_not_supported",
                    "risk_level: confirmation-required\nwould_execute: False\nexecution_status: not_supported_current_stage\nFlow start/stop/run requests are intentionally not executed by ColorVision MCP v3.");
            }

            if (action != "select_node" && action != "open_node_property" && action != "inspect_node_errors")
            {
                return CopilotMcpToolCallResult.Fail("unsupported_flow_preview_action", "Supported preview actions: select_node, open_node_property, inspect_node_errors. Flow start/stop/run is not supported.");
            }

            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            if (snapshot == null)
                return CopilotMcpToolCallResult.Ok("No active flow is available. would_execute: False");

            var nodeQuery = FirstNonEmpty(GetString(arguments, "node_id"), GetString(arguments, "node_name"), GetString(arguments, "node"));
            var matchedNode = FindFlowNode(snapshot, nodeQuery);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow action preview");
            builder.AppendLine($"Action: {action}");
            builder.AppendLine("Mode: preview only");
            builder.AppendLine("Would execute: False");
            builder.AppendLine("Flow execution allowed: False");
            builder.AppendLine($"Flow name: {EmptyLabel(snapshot.FlowName)}");
            builder.AppendLine($"Node count: {snapshot.Nodes.Count}");
            builder.AppendLine($"Requested node: {EmptyLabel(nodeQuery)}");

            if (matchedNode != null)
            {
                builder.AppendLine($"Matched node: {EmptyLabel(FirstNonEmpty(matchedNode.Title, matchedNode.NodeName, matchedNode.NodeId))}");
                builder.AppendLine($"Matched node id: {EmptyLabel(matchedNode.NodeId)}");
                builder.AppendLine($"Matched node type: {EmptyLabel(matchedNode.NodeType)}");
                builder.AppendLine($"Matched node selected: {matchedNode.IsSelected}");
                if (action == "inspect_node_errors")
                {
                    builder.AppendLine($"Node mark: {EmptyLabel(matchedNode.Mark)}");
                    builder.AppendLine($"Recent flow failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(nodeQuery))
            {
                builder.AppendLine("Matched node: (none)");
                builder.AppendLine("Available nodes:");
                foreach (var node in snapshot.Nodes.Take(20))
                    builder.AppendLine($"- {EmptyLabel(FirstNonEmpty(node.Title, node.NodeName, node.NodeId))} [{EmptyLabel(node.NodeId)}]");
            }

            builder.AppendLine("No flow was started, stopped, run, rerun, or modified.");
            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
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
            {
                return CreateConfirmableActionResult(
                    "Confirm language change",
                    $"Change ColorVision UI language: {languageQuery}",
                    "set_language",
                    arguments,
                    "Changing language may affect UI state and can trigger the existing restart confirmation flow.",
                    token => _environment.SetLanguageHandler(languageQuery, token));
            }

            return CreateConfirmableActionResult(
                "Confirm language change",
                $"Change ColorVision UI language: {languageQuery}",
                "set_language",
                arguments,
                "Changing language may affect UI state and can trigger the existing restart confirmation flow.",
                async token => ToMcpResult(await CopilotApplicationCapability.SetLanguageAsync(languageQuery, token), "language_change_failed"));
        }

        private CopilotMcpToolCallResult CreateConfirmableActionResult(
            string title,
            string description,
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string previewText,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor)
        {
            if (ContainsSensitiveArgumentValues(arguments))
                return CopilotMcpToolCallResult.Fail("sensitive_arguments_not_allowed", "ColorVision MCP refuses to create confirmable actions that contain token, api key, password, authorization, or bearer secret values.");

            var argumentsSummary = BuildArgumentSummary(arguments);
            var action = CopilotMcpConfirmationStore.Instance.Create(
                title,
                description,
                "confirmation-required",
                NormalizeToolName(toolName),
                argumentsSummary,
                executor);

            var builder = new StringBuilder();
            builder.AppendLine("confirmation_required");
            builder.AppendLine("execution_status: pending_user_confirmation");
            builder.AppendLine($"action_id: {action.ActionId}");
            builder.AppendLine($"title: {action.Title}");
            builder.AppendLine($"description: {action.Description}");
            builder.AppendLine($"risk_level: {action.RiskLevel}");
            builder.AppendLine($"tool_name: {action.ToolName}");
            builder.AppendLine($"arguments_summary: {action.ArgumentsSummary}");
            builder.AppendLine($"created_at: {action.CreatedAt:O}");
            builder.AppendLine($"expires_at: {action.ExpiresAt:O}");
            builder.AppendLine("User must approve this action in the ColorVision Copilot Pending Actions area before confirm_action can execute it.");
            if (!string.IsNullOrWhiteSpace(previewText))
            {
                builder.AppendLine();
                builder.AppendLine("Preview:");
                builder.AppendLine(TrimLong(RedactForDisplay(previewText), 8000));
            }

            return CopilotMcpToolCallResult.Fail("confirmation_required", builder.ToString().TrimEnd());
        }

        private static bool IsConfirmationRequiredResult(CopilotMcpToolCallResult result)
        {
            return !result.Success
                && (string.Equals(result.ErrorCode, "confirmation_required", StringComparison.OrdinalIgnoreCase)
                    || result.Text.Contains("confirmation_required", StringComparison.OrdinalIgnoreCase)
                    || result.Text.Contains("risk_level=confirmation-required", StringComparison.OrdinalIgnoreCase)
                    || result.Text.Contains("risk_level: confirmation-required", StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsConfirmationRequiredResult(CopilotCapabilityResult result)
        {
            return !result.Success
                && ((result.ErrorMessage ?? string.Empty).Contains("确认", StringComparison.OrdinalIgnoreCase)
                    || (result.Content ?? string.Empty).Contains("execution_status: confirmation_required", StringComparison.OrdinalIgnoreCase)
                    || (result.Content ?? string.Empty).Contains("risk_level: confirmation-required", StringComparison.OrdinalIgnoreCase)
                    || (result.Content ?? string.Empty).Contains("risk_level=confirmation-required", StringComparison.OrdinalIgnoreCase));
        }

        private static bool ContainsSensitiveArgumentValues(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            if (arguments == null || arguments.Count == 0)
                return false;

            foreach (var pair in arguments)
            {
                var rawValue = pair.Value.ToString();
                var redactedValue = CopilotMcpAuditLogger.RedactArgument(pair.Key, rawValue);
                if (!string.Equals(rawValue, redactedValue, StringComparison.Ordinal))
                    return true;
            }

            return false;
        }

        private bool TryGetJsonArgument(IReadOnlyDictionary<string, JsonElement>? arguments, string name, out string json, out string error)
        {
            json = string.Empty;
            error = string.Empty;

            if (arguments == null || !arguments.TryGetValue(name, out var value))
            {
                error = $"The preview_template_patch tool requires {name}.";
                return false;
            }

            json = value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? string.Empty
                : value.GetRawText();

            if (!string.IsNullOrWhiteSpace(json))
                return true;

            error = $"The {name} argument must not be empty.";
            return false;
        }

        private string ExtractCurrentTemplateJson()
        {
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null || !liveContext.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return string.Empty;

            foreach (var item in liveContext.SnapshotItems)
            {
                var json = ExtractFencedJson(item.Content);
                if (!string.IsNullOrWhiteSpace(json))
                    return json;
            }

            return string.Empty;
        }

        private static bool TryFindSensitiveJsonProperty(JsonElement element, out string path)
        {
            return TryFindSensitiveJsonProperty(element, "$", out path);
        }

        private static bool TryFindSensitiveJsonProperty(JsonElement element, string pathPrefix, out string path)
        {
            if (element.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in element.EnumerateObject())
                {
                    var propertyPath = $"{pathPrefix}.{property.Name}";
                    if (IsSensitiveDisplayKey(property.Name))
                    {
                        path = propertyPath;
                        return true;
                    }

                    if (TryFindSensitiveJsonProperty(property.Value, propertyPath, out path))
                        return true;
                }
            }
            else if (element.ValueKind == JsonValueKind.Array)
            {
                var index = 0;
                foreach (var item in element.EnumerateArray())
                {
                    if (TryFindSensitiveJsonProperty(item, $"{pathPrefix}[{index}]", out path))
                        return true;
                    index++;
                }
            }

            path = string.Empty;
            return false;
        }

        private static string DescribeJsonValue(JsonElement value)
        {
            if (value.ValueKind == JsonValueKind.Undefined)
                return "(missing)";

            return RedactForDisplay(TrimLong(value.GetRawText(), 240));
        }

        private static string NormalizeActionName(string? action)
        {
            return (action ?? string.Empty).Trim().ToLowerInvariant().Replace('-', '_').Replace(' ', '_');
        }

        private static bool IsForbiddenFlowExecutionAction(string action)
        {
            return action is "start" or "stop" or "run" or "rerun" or "execute" or "start_flow" or "stop_flow" or "run_flow" or "execute_flow";
        }

        private static CopilotFlowNodeContextSnapshot? FindFlowNode(CopilotFlowContextSnapshot snapshot, string nodeQuery)
        {
            if (string.IsNullOrWhiteSpace(nodeQuery))
                return snapshot.Nodes.FirstOrDefault(node => node.IsSelected) ?? snapshot.Nodes.FirstOrDefault();

            var query = nodeQuery.Trim();
            return snapshot.Nodes.FirstOrDefault(node =>
                    string.Equals(node.NodeId, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.NodeName, query, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(node.Title, query, StringComparison.OrdinalIgnoreCase))
                ?? snapshot.Nodes.FirstOrDefault(node =>
                    node.NodeId.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || node.NodeName.Contains(query, StringComparison.OrdinalIgnoreCase)
                    || node.Title.Contains(query, StringComparison.OrdinalIgnoreCase));
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
                    builder.AppendLine(RedactForDisplay(item.Content.Trim()));
            }

            return builder.ToString().TrimEnd();
        }

        private static string FormatTemplateLiveContext(CopilotLiveContext liveContext)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision active template context");
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

                AppendTemplateMetadata(builder, item.Content);

                if (!string.IsNullOrWhiteSpace(item.Content))
                {
                    builder.AppendLine();
                    builder.AppendLine("Snapshot content:");
                    builder.AppendLine(RedactForDisplay(TrimLong(item.Content.Trim(), 12000)));
                }
            }

            return builder.ToString().TrimEnd();
        }

        private static void AppendTemplateMetadata(StringBuilder builder, string? content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            var lines = content.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            AppendFirstLineWithPrefix(builder, lines, "Surface:");
            AppendFirstLineWithPrefix(builder, lines, "Template name:");
            AppendFirstLineWithPrefix(builder, lines, "Current selection:");
            AppendFirstLineWithPrefix(builder, lines, "Window title:");
            AppendFirstLineWithPrefix(builder, lines, "Editor mode:");
            AppendFirstLineWithPrefix(builder, lines, "Unsaved changes:");
            AppendFirstLineWithPrefix(builder, lines, "JSON validation:");
            AppendFirstLineWithPrefix(builder, lines, "JSON line count:");

            var json = ExtractFencedJson(content);
            if (string.IsNullOrWhiteSpace(json))
                return;

            try
            {
                using var document = JsonDocument.Parse(json);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    builder.AppendLine($"Template JSON root: {document.RootElement.ValueKind}");
                    return;
                }

                var properties = document.RootElement.EnumerateObject().Take(40).ToArray();
                builder.AppendLine($"Template JSON root: object");
                builder.AppendLine($"Template JSON top-level keys: {string.Join(", ", properties.Select(property => property.Name))}");

                var templateType = FirstJsonScalar(document.RootElement, "$type", "Type", "TemplateType", "ParamType", "ModelType");
                if (!string.IsNullOrWhiteSpace(templateType))
                    builder.AppendLine($"Template type: {TrimLong(templateType, 160)}");

                var templateName = FirstJsonScalar(document.RootElement, "Name", "TemplateName", "Key", "Code");
                if (!string.IsNullOrWhiteSpace(templateName))
                    builder.AppendLine($"Template name from JSON: {TrimLong(templateName, 160)}");

                var keyParameters = properties
                    .Where(property => property.Value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    .Where(property => !IsSensitiveDisplayKey(property.Name))
                    .Take(20)
                    .Select(property => $"{property.Name}={TrimLong(property.Value.ToString(), 120)}")
                    .ToArray();
                if (keyParameters.Length > 0)
                    builder.AppendLine($"Key parameter summary: {string.Join(", ", keyParameters)}");

                foreach (var key in new[] { "Id", "ID", "Name", "Key", "Type", "TemplateType", "Code" })
                {
                    if (document.RootElement.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number)
                        builder.AppendLine($"Template JSON {key}: {TrimLong(value.ToString(), 160)}");
                }
            }
            catch (JsonException ex)
            {
                builder.AppendLine($"Template JSON parse: failed ({ex.Message})");
            }
        }

        private static void AppendFirstLineWithPrefix(StringBuilder builder, IReadOnlyList<string> lines, string prefix)
        {
            var line = lines.FirstOrDefault(item => item.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(line))
                builder.AppendLine(line.Trim());
        }

        private static string ExtractFencedJson(string content)
        {
            const string fence = "```";
            var jsonFenceStart = content.IndexOf("```json", StringComparison.OrdinalIgnoreCase);
            if (jsonFenceStart < 0)
                return string.Empty;

            var jsonStart = content.IndexOf('\n', jsonFenceStart);
            if (jsonStart < 0)
                return string.Empty;

            var jsonEnd = content.IndexOf(fence, jsonStart + 1, StringComparison.Ordinal);
            if (jsonEnd < 0)
                return string.Empty;

            return content[(jsonStart + 1)..jsonEnd].Trim();
        }

        private static string FirstJsonScalar(JsonElement element, params string[] keys)
        {
            foreach (var key in keys)
            {
                if (element.TryGetProperty(key, out var value) && value.ValueKind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False)
                    return value.ToString();
            }

            return string.Empty;
        }

        private static string RedactForDisplay(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var sensitiveTerms = SensitiveDisplayTerms;
            var lines = text.Split(new[] { "\r\n", "\n" }, StringSplitOptions.None);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index];
                if (!sensitiveTerms.Any(term => line.Contains(term, StringComparison.OrdinalIgnoreCase)))
                    continue;

                var separatorIndex = line.IndexOf(':');
                if (separatorIndex < 0)
                    separatorIndex = line.IndexOf('=');

                lines[index] = separatorIndex >= 0
                    ? line[..(separatorIndex + 1)] + " <redacted>"
                    : "<redacted>";
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static readonly string[] SensitiveDisplayTerms =
        {
            "password",
            "passwd",
            "pwd",
            "secret",
            "token",
            "api_key",
            "apikey",
            "access_key",
            "private_key",
            "authorization",
            "bearer",
        };

        private static bool IsSensitiveDisplayKey(string? key)
        {
            return !string.IsNullOrWhiteSpace(key)
                && SensitiveDisplayTerms.Any(term => key.Contains(term, StringComparison.OrdinalIgnoreCase));
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
            var selectedNodes = snapshot.Nodes.Where(node => node.IsSelected).ToArray();
            builder.AppendLine($"Selected node count: {selectedNodes.Length}");
            if (selectedNodes.Length > 0)
                builder.AppendLine($"Selected nodes: {string.Join(", ", selectedNodes.Select(node => EmptyLabel(FirstNonEmpty(node.Title, node.NodeName, node.NodeId))))}");

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
                    builder.AppendLine($"- Parameters: {RedactForDisplay(string.Join(", ", node.Parameters.Select(item => $"{item.Name}={item.Value}")))}");
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

        private static CopilotMcpToolDescriptor Tool(string name, string description, object inputSchema, string category, string riskLevel, string usageExample) => new()
        {
            Name = name,
            Description = description,
            InputSchema = inputSchema,
            Category = category,
            RiskLevel = riskLevel,
            UsageExample = usageExample,
            Annotations = BuildToolAnnotations(riskLevel),
        };

        private static IReadOnlyDictionary<string, object> BuildToolAnnotations(string riskLevel)
        {
            var isReadOnly = string.Equals(riskLevel, "read-only", StringComparison.OrdinalIgnoreCase);
            return new Dictionary<string, object>
            {
                ["readOnlyHint"] = isReadOnly,
                ["destructiveHint"] = false,
                ["idempotentHint"] = isReadOnly,
                ["openWorldHint"] = false,
                ["riskLevel"] = riskLevel,
            };
        }

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

        private static object BooleanProperty(string description) => new Dictionary<string, object>
        {
            ["type"] = "boolean",
            ["description"] = description,
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
                builder.AppendLine($"  Action id: {EmptyLabel(entry.ActionId)}");
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

        private static bool? GetBool(IReadOnlyDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments == null || !arguments.TryGetValue(name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.True)
                return true;

            if (value.ValueKind == JsonValueKind.False)
                return false;

            if (value.ValueKind == JsonValueKind.String && bool.TryParse(value.GetString(), out var parsed))
                return parsed;

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