#pragma warning disable CA1822,CA1826,CA1859,CA1861
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
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
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
        private const int DefaultDiagnosticBundleChars = 12000;
        private const int MaxDiagnosticBundleChars = 60000;
        public const string InAppAgentCallerSource = "in-app-agent";

        internal const string InAppAgentFrameworkApprovedCallerSource = "in-app-agent-framework-approved";
        private const string LiveContextResourceUri = "colorvision://live-context/current";
        private const string WorkspaceResourceUri = "colorvision://workspace/current";
        private const string LogsResourceUri = "colorvision://logs/recent";
        private const string TemplateResourceUri = "colorvision://template/current";
        private const string FlowResourceUri = "colorvision://flow/current";
        private const string AuditSummaryResourceUri = "colorvision://mcp/audit-summary";
        private const string AuditLogResourceUri = "colorvision://mcp/audit-log";
        private const string CapabilityCatalogResourceUri = "colorvision://copilot/capabilities";
        private const string TaskEventJournalResourceUri = "colorvision://copilot/task-events";
        private static readonly JsonSerializerOptions StructuredJsonOptions = new(JsonSerializerDefaults.Web)
        {
            WriteIndented = true,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
        };
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
        private readonly CopilotMcpToolRouter _router;

        private readonly record struct CopilotPanelTarget(string Alias, string TargetId);

        private sealed class TemplatePatchComputation
        {
            public string TemplateIdentifier { get; init; } = string.Empty;

            public string SourceId { get; init; } = string.Empty;

            public string CurrentJson { get; init; } = string.Empty;

            public string ProposedChangesJson { get; init; } = string.Empty;

            public string PatchedJson { get; init; } = string.Empty;

            public IReadOnlyList<string> Changes { get; init; } = Array.Empty<string>();

            public bool IsApplyEligible => !string.IsNullOrWhiteSpace(SourceId);
        }

        public CopilotMcpToolDispatcher(CopilotMcpToolEnvironment? environment = null)
        {
            _environment = environment ?? new CopilotMcpToolEnvironment();
            _router = CreateRouter();
            ValidateRouterMatchesDescriptors();
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
                Tool("get_audit_summary", "Return a compact MCP audit summary with recent counts, last failure, callers, and pending approvals. Optional argument: max_entries.", Schema(new Dictionary<string, object>
                {
                    ["max_entries"] = IntegerProperty("Maximum recent audit entries to summarize. Defaults to 50.", 1, 200),
                }), "audit", "read-only", "Call get_audit_summary with { \"max_entries\": 50 }."),
                Tool("get_last_tool_error", "Return the most recent failed MCP tool call, if one is recorded.", EmptySchema(), "audit", "read-only", "Call get_last_tool_error with no arguments."),
                Tool("get_agent_task_events", "Query the latest saved Agent task event journal. Use only when the user asks to inspect Agent execution, tools, approvals, steering, replanning, or stop reasons.", Schema(new Dictionary<string, object>
                {
                    ["event_types"] = new Dictionary<string, object>
                    {
                        ["type"] = "array",
                        ["description"] = "Optional event type filters, for example toolCompleted, approvalDenied, or runStopped.",
                        ["items"] = new Dictionary<string, object>
                        {
                            ["type"] = "string",
                            ["enum"] = Enum.GetNames<CopilotAgentTaskEventType>().Select(JsonNamingPolicy.CamelCase.ConvertName).ToArray(),
                        },
                        ["maxItems"] = Enum.GetValues<CopilotAgentTaskEventType>().Length,
                    },
                    ["run_id"] = StringProperty("Optional exact run: identifier."),
                    ["tool"] = StringProperty("Optional exact tool name filter."),
                    ["related_id"] = StringProperty("Optional exact subject or related identifier."),
                    ["before_sequence"] = new Dictionary<string, object>
                    {
                        ["type"] = "integer",
                        ["description"] = "Return events with a sequence lower than this cursor.",
                        ["minimum"] = 1L,
                    },
                    ["max_events"] = IntegerProperty("Maximum events to return. Defaults to 50.", 1, CopilotAgentTaskEventJournal.MaxQueryLimit),
                }), "audit", "read-only", "Call get_agent_task_events with { \"event_types\": [\"toolCompleted\", \"runStopped\"], \"max_events\": 50 }."),
                Tool("get_runtime_environment_summary", "Return a safe summary of the MCP runtime environment, workspace roots, live context, logs, and flow availability.", EmptySchema(), "status", "read-only", "Call get_runtime_environment_summary before diagnostics."),
                Tool("get_diagnostic_bundle", "Return a size-limited redacted diagnostic bundle with server status, runtime, last error, recent log, live context, and flow summary.", Schema(new Dictionary<string, object>
                {
                    ["max_chars"] = IntegerProperty("Maximum characters to return. Defaults to 12000.", 1000, MaxDiagnosticBundleChars),
                }), "status", "read-only", "Call get_diagnostic_bundle with { \"max_chars\": 12000 } before reporting diagnostics."),
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
                Tool("diagnose_flow_failure", "Build a read-only failure diagnosis from the active flow, matched node, template context, and recent logs. This never runs a flow.", Schema(new Dictionary<string, object>
                {
                    ["node_id"] = StringProperty("Optional flow node id to focus the diagnosis."),
                    ["node_name"] = StringProperty("Optional flow node name or title to focus the diagnosis."),
                    ["query"] = StringProperty("Optional log query. Defaults to error."),
                    ["max_log_lines"] = IntegerProperty("Maximum recent log lines to inspect.", 1, 300),
                }), "context", "read-only", "Call diagnose_flow_failure with { \"node_name\": \"Camera\", \"query\": \"timeout\" } before suggesting template edits."),
                Tool("open_panel", "Open a low-risk ColorVision panel. Optional argument: panel. Defaults to copilot.", Schema(new Dictionary<string, object>
                {
                    ["panel"] = StringProperty("Panel id or alias. Supported aliases: copilot, log, config, solution, template, flow, device."),
                }), "app-control", "low-risk-action", "Call open_panel with { \"panel\": \"copilot\" }."),
                Tool("execute_menu", "Execute a visible main-window menu command by menu name or path. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Menu name or path to execute."),
                    ["dry_run"] = BooleanProperty("When true, resolve the menu and report risk without executing it."),
                }, "query"), "app-control", "confirmation-required", "Call execute_menu with { \"query\": \"View > Copilot\", \"dry_run\": true } first."),
                Tool("create_flow", "Create a new empty ColorVision flow after explicit user approval. Optional argument: name; a timestamped name is generated when omitted.", Schema(new Dictionary<string, object>
                {
                    ["name"] = StringProperty("Optional new flow name."),
                }), "app-control", "confirmation-required", "Call create_flow with { \"name\": \"CalibrationFlow\" }, then wait for approval in ColorVision."),
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
                Tool("suggest_template_patch", "Prepare a read-only template patch suggestion from the active template, diagnosis, and optional proposed changes. This never applies or saves.", Schema(new Dictionary<string, object>
                {
                    ["template_identifier"] = StringProperty("Template name, id, key, or editor identifier. Defaults to active template context when possible."),
                    ["intent"] = StringProperty("Requested adjustment intent or failure diagnosis summary."),
                    ["node_id"] = StringProperty("Optional related flow node id."),
                    ["node_name"] = StringProperty("Optional related flow node name or title."),
                    ["proposed_changes"] = new Dictionary<string, object>
                    {
                        ["description"] = "Optional object containing proposed top-level JSON changes, or a JSON object string.",
                    },
                    ["current_json"] = StringProperty("Optional current template JSON. If omitted, the active template editor context is used."),
                }), "context", "read-only", "Call suggest_template_patch with { \"intent\": \"Camera timeout\", \"node_name\": \"Camera\" }, then preview_template_patch."),
                Tool("apply_template_patch", "Create a user-confirmed action that applies a prior preview_template_patch result to the active template JSON editor. Required argument: preview_id.", Schema(new Dictionary<string, object>
                {
                    ["preview_id"] = StringProperty("Preview id returned by preview_template_patch."),
                }, "preview_id"), "app-control", "confirmation-required", "Call preview_template_patch first, then apply_template_patch with the returned preview_id."),
                Tool("preview_flow_action", "Preview a low-risk flow navigation/inspection action without running or stopping the flow. Required argument: action.", Schema(new Dictionary<string, object>
                {
                    ["action"] = StringProperty("Preview action: select_node, open_node_property, inspect_node_errors, explain_node, trace_recent_failure. start/stop/run requests are refused."),
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
                Resource(AuditSummaryResourceUri, "MCP audit summary", "Compact ColorVision MCP audit and pending approval summary."),
                Resource(AuditLogResourceUri, "MCP audit log", "Recent ColorVision MCP tool-call audit entries."),
                Resource(CapabilityCatalogResourceUri, "Copilot capability catalog", "Versioned read-only catalog of built-in and discovered Copilot capabilities.", "application/json"),
                Resource(TaskEventJournalResourceUri, "Copilot Agent task events", "Latest saved bounded and redacted Agent task event journal.", "application/json"),
            };
        }

        public string GetResourceMimeType(string uri)
        {
            var normalizedUri = NormalizeResourceUri(uri);
            return ListResources().FirstOrDefault(resource => string.Equals(resource.Uri, normalizedUri, StringComparison.OrdinalIgnoreCase))?.MimeType
                ?? "text/plain";
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
                AuditSummaryResourceUri => GetAuditSummary(null),
                AuditLogResourceUri => GetAuditLog(null),
                CapabilityCatalogResourceUri => GetCapabilityCatalog(),
                TaskEventJournalResourceUri => GetAgentTaskEvents(null, CopilotAgentTaskEventJournal.MaxQueryLimit),
                _ => CopilotMcpToolCallResult.Fail("resource_not_found", $"Unknown ColorVision MCP resource: {uri}"),
            };
        }

        private static CopilotMcpToolCallResult GetCapabilityCatalog()
        {
            var snapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(snapshot, StructuredJsonOptions));
        }

        private CopilotMcpToolCallResult GetAgentTaskEvents(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            int defaultMaxEvents = 50)
        {
            var context = SafeInvoke(_environment.TaskEventJournalProvider);
            if (context?.IsStructurallyValid() != true)
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_unavailable",
                    "No saved Agent task event journal is available for the selected conversation.");
            }

            if (!TryGetTaskEventTypes(arguments, out var eventTypes, out var eventTypesError))
                return CopilotMcpToolCallResult.Fail("invalid_arguments", eventTypesError);

            var beforeSequence = GetLong(arguments, "before_sequence");
            if (arguments?.ContainsKey("before_sequence") == true && beforeSequence is null or <= 0)
                return CopilotMcpToolCallResult.Fail("invalid_arguments", "before_sequence must be a positive integer cursor.");
            var maxEvents = GetInt(arguments, "max_events");
            if (arguments?.ContainsKey("max_events") == true
                && (maxEvents is null or <= 0 || maxEvents > CopilotAgentTaskEventJournal.MaxQueryLimit))
            {
                return CopilotMcpToolCallResult.Fail(
                    "invalid_arguments",
                    $"max_events must be between 1 and {CopilotAgentTaskEventJournal.MaxQueryLimit}.");
            }

            var query = new CopilotAgentTaskEventQuery
            {
                Types = eventTypes,
                RunId = GetString(arguments, "run_id"),
                ToolName = GetString(arguments, "tool"),
                SubjectOrRelatedId = GetString(arguments, "related_id"),
                BeforeSequence = beforeSequence ?? long.MaxValue,
                Limit = maxEvents ?? defaultMaxEvents,
            };
            var result = CopilotAgentTaskEventJournal.Query(context.Journal, query);
            var payload = new
            {
                context.ConversationId,
                context.PublishedAtUtc,
                context.Journal.SchemaVersion,
                Events = result.Events,
                result.HasMore,
                result.NextBeforeSequence,
            };
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(payload, StructuredJsonOptions));
        }

        private static bool TryGetTaskEventTypes(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out IReadOnlyCollection<CopilotAgentTaskEventType> eventTypes,
            out string error)
        {
            eventTypes = Array.Empty<CopilotAgentTaskEventType>();
            error = string.Empty;
            if (arguments == null || !arguments.TryGetValue("event_types", out var value))
                return true;
            if (value.ValueKind != JsonValueKind.Array)
            {
                error = "event_types must be an array of Agent task event type names.";
                return false;
            }
            if (value.GetArrayLength() > Enum.GetValues<CopilotAgentTaskEventType>().Length)
            {
                error = "event_types contains more entries than the supported Agent task event type set.";
                return false;
            }

            var parsed = new HashSet<CopilotAgentTaskEventType>();
            foreach (var item in value.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String
                    || !Enum.TryParse<CopilotAgentTaskEventType>(item.GetString(), ignoreCase: true, out var eventType)
                    || !Enum.IsDefined(eventType))
                {
                    error = $"Unknown Agent task event type: {item}.";
                    return false;
                }
                parsed.Add(eventType);
            }
            eventTypes = parsed;
            return true;
        }

        public async Task<CopilotMcpToolCallResult> CallAsync(string toolName, IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken, string callerSource = "")
        {
            var normalizedToolName = NormalizeToolName(toolName);
            var stopwatch = Stopwatch.StartNew();
            CopilotMcpAuditLogger.ToolCallStarted(normalizedToolName, BuildArgumentSummary(arguments), callerSource);

            try
            {
                var result = await _router.DispatchAsync(normalizedToolName, arguments, callerSource, cancellationToken);

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

        private CopilotMcpToolRouter CreateRouter()
        {
            return new CopilotMcpToolRouter()
                .Register("get_server_status", (_, caller, _) => Task.FromResult(GetServerStatus(caller)))
                .Register("get_enabled_tools", (_, _, _) => Task.FromResult(GetEnabledTools()))
                .Register("get_audit_log", (arguments, _, _) => Task.FromResult(GetAuditLog(arguments)))
                .Register("get_audit_summary", (arguments, _, _) => Task.FromResult(GetAuditSummary(arguments)))
                .Register("get_last_tool_error", (_, _, _) => Task.FromResult(GetLastToolError()))
                .Register("get_agent_task_events", (arguments, _, _) => Task.FromResult(GetAgentTaskEvents(arguments)))
                .Register("get_runtime_environment_summary", (_, _, token) => GetRuntimeEnvironmentSummaryAsync(token))
                .Register("get_diagnostic_bundle", (arguments, caller, token) => GetDiagnosticBundleAsync(arguments, caller, token))
                .Register("get_live_context", (_, _, _) => Task.FromResult(GetLiveContext()))
                .Register("get_workspace_context", (_, _, _) => Task.FromResult(GetWorkspaceContext()))
                .Register("get_recent_log", (arguments, _, _) => Task.FromResult(GetRecentLog(arguments)))
                .Register("search_docs", (arguments, _, token) => SearchDocsAsync(arguments, token))
                .Register("search_files", (arguments, _, token) => Task.FromResult(SearchFiles(arguments, token)))
                .Register("grep_text", (arguments, _, token) => Task.FromResult(GrepText(arguments, token)))
                .Register("read_allowed_file", (arguments, _, token) => ReadAllowedFileAsync(arguments, token))
                .Register("list_allowed_directory", (arguments, _, token) => Task.FromResult(ListAllowedDirectory(arguments, token)))
                .Register("get_active_template_context", (_, _, _) => Task.FromResult(GetActiveTemplateContext()))
                .Register("get_flow_summary", (_, _, token) => GetFlowSummaryAsync(token))
                .Register("diagnose_flow_failure", (arguments, _, token) => DiagnoseFlowFailureAsync(arguments, token))
                .Register("open_panel", (arguments, _, token) => OpenPanelAsync(arguments, token))
                .Register("execute_menu", (arguments, caller, token) => ExecuteMenuAsync(arguments, caller, token))
                .Register("create_flow", (arguments, caller, token) => CreateFlowAsync(arguments, caller, token))
                .Register("confirm_action", (arguments, _, token) => ConfirmActionAsync(arguments, token))
                .Register("preview_template_patch", (arguments, _, _) => Task.FromResult(PreviewTemplatePatch(arguments)))
                .Register("suggest_template_patch", (arguments, _, token) => SuggestTemplatePatchAsync(arguments, token))
                .Register("apply_template_patch", (arguments, caller, token) => ApplyTemplatePatchAsync(arguments, caller, token))
                .Register("preview_flow_action", (arguments, _, token) => PreviewFlowActionAsync(arguments, token))
                .Register("set_theme", (arguments, _, token) => SetThemeAsync(arguments, token))
                .Register("set_language", (arguments, caller, token) => SetLanguageAsync(arguments, caller, token));
        }

        private void ValidateRouterMatchesDescriptors()
        {
            var descriptorNames = ListTools().Select(tool => NormalizeToolName(tool.Name)).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var routeNames = _router.ToolNames.Select(NormalizeToolName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (descriptorNames.SetEquals(routeNames))
                return;

            var missingRoutes = descriptorNames.Except(routeNames, StringComparer.OrdinalIgnoreCase);
            var missingDescriptors = routeNames.Except(descriptorNames, StringComparer.OrdinalIgnoreCase);
            throw new InvalidOperationException(
                $"MCP tool descriptors and handlers are out of sync. Missing routes: {string.Join(", ", missingRoutes)}. Missing descriptors: {string.Join(", ", missingDescriptors)}.");
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

        private CopilotMcpToolCallResult GetAuditSummary(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            var maxEntries = Math.Clamp(GetInt(arguments, "max_entries") ?? 50, 1, 200);
            var entries = CopilotMcpAuditLogger.GetRecentEntries(maxEntries);
            var pendingActions = CopilotMcpConfirmationStore.Instance.GetPendingActions();
            var unsuccessfulEntries = entries.Where(entry => !entry.Success).ToArray();
            var approvalFlowEntries = unsuccessfulEntries.Where(IsApprovalFlowAuditEntry).ToArray();
            var failedEntries = unsuccessfulEntries.Where(IsRealFailureAuditEntry).ToArray();
            var lastEntry = entries.LastOrDefault();
            var lastFailure = failedEntries.LastOrDefault();
            var lastApprovalFlowEntry = approvalFlowEntries.LastOrDefault();
            var topFailures = failedEntries
                .GroupBy(entry => string.IsNullOrWhiteSpace(entry.ToolName) ? "(unknown)" : entry.ToolName, StringComparer.OrdinalIgnoreCase)
                .Select(group => new
                {
                    ToolName = group.Key,
                    Count = group.Count(),
                    LastFailure = group.Last(),
                })
                .OrderByDescending(item => item.Count)
                .ThenByDescending(item => item.LastFailure.TimestampUtc)
                .Take(5)
                .ToArray();
            var callers = entries
                .Select(entry => EmptyLabel(entry.CallerSource))
                .Where(caller => !string.Equals(caller, "(none)", StringComparison.OrdinalIgnoreCase))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(8)
                .ToArray();

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP audit summary");
            builder.AppendLine($"Entries summarized: {entries.Count}");
            builder.AppendLine($"Successful entries: {entries.Count(entry => entry.Success)}");
            builder.AppendLine($"Raw unsuccessful entries: {unsuccessfulEntries.Length}");
            builder.AppendLine($"Real failure entries: {failedEntries.Length}");
            builder.AppendLine($"Approval-flow entries: {approvalFlowEntries.Length}");
            builder.AppendLine($"Pending approvals: {pendingActions.Count}");
            builder.AppendLine($"Last entry: {FormatAuditEntryOneLine(lastEntry)}");
            builder.AppendLine($"Last real failure: {FormatAuditEntryOneLine(lastFailure)}");
            builder.AppendLine($"Last approval-flow event: {FormatAuditEntryOneLine(lastApprovalFlowEntry)}");
            builder.AppendLine($"Recent callers: {(callers.Length == 0 ? "(none)" : string.Join(", ", callers))}");

            builder.AppendLine();
            builder.AppendLine("Top failures");
            if (topFailures.Length == 0)
            {
                builder.AppendLine("- None");
            }
            else
            {
                foreach (var failure in topFailures)
                {
                    builder.AppendLine($"- {failure.ToolName}: {failure.Count} failure(s); latest {failure.LastFailure.TimestampUtc:O}; error={EmptyLabel(failure.LastFailure.ErrorMessage)}");
                }
            }

            builder.AppendLine();
            builder.AppendLine("Pending approvals");
            if (pendingActions.Count == 0)
            {
                builder.AppendLine("- None");
            }
            else
            {
                foreach (var action in pendingActions.Take(8))
                {
                    builder.AppendLine($"- action_id={action.ActionId}; tool={action.ToolName}; risk={action.RiskLevel}; status={action.StatusLabel}; expires_at={action.ExpiresAt:O}; title={action.Title}");
                }

                if (pendingActions.Count > 8)
                    builder.AppendLine($"- ... {pendingActions.Count - 8} more pending approval(s)");
            }

            builder.AppendLine();
            builder.AppendLine("Next step hints");
            if (pendingActions.Count > 0)
                builder.AppendLine("- Ask the ColorVision user to approve or reject pending actions before calling confirm_action.");
            if (approvalFlowEntries.Length > 0 && pendingActions.Count == 0)
                builder.AppendLine("- Approval-flow entries are not counted as real failures; inspect pending approvals or get_audit_log when reviewing user decisions.");
            if (lastFailure != null)
                builder.AppendLine("- Call get_last_tool_error or get_audit_log with failed_only=true for failure details.");
            if (entries.Count == 0)
                builder.AppendLine("- No MCP activity has been recorded yet.");

            return CopilotMcpToolCallResult.Ok(builder.ToString().TrimEnd());
        }

        private CopilotMcpToolCallResult GetLastToolError()
        {
            var entry = CopilotMcpAuditLogger.GetRecentEntries(200).LastOrDefault(IsRealFailureAuditEntry);
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

        private async Task<CopilotMcpToolCallResult> GetDiagnosticBundleAsync(IReadOnlyDictionary<string, JsonElement>? arguments, string callerSource, CancellationToken cancellationToken)
        {
            var maxChars = Math.Clamp(GetInt(arguments, "max_chars") ?? DefaultDiagnosticBundleChars, 1000, MaxDiagnosticBundleChars);
            var recentLog = GetRecentLog(new Dictionary<string, JsonElement>
            {
                ["max_lines"] = JsonSerializer.SerializeToElement(120),
            });

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision MCP diagnostic bundle");
            builder.AppendLine($"Generated UTC: {DateTimeOffset.UtcNow:O}");
            builder.AppendLine($"Max chars: {maxChars}");
            AppendDiagnosticSection(builder, "server_status", GetServerStatus(callerSource).Text);
            AppendDiagnosticSection(builder, "runtime_environment_summary", (await GetRuntimeEnvironmentSummaryAsync(cancellationToken)).Text);
            AppendDiagnosticSection(builder, "last_tool_error", GetLastToolError().Text);
            AppendDiagnosticSection(builder, "recent_log", recentLog.Text);
            AppendDiagnosticSection(builder, "live_context", GetLiveContext().Text);
            AppendDiagnosticSection(builder, "flow_summary", (await GetFlowSummaryAsync(cancellationToken)).Text);

            var redacted = RedactForDiagnostics(builder.ToString().TrimEnd());
            return CopilotMcpToolCallResult.Ok(TruncateWithLimit(redacted, maxChars));
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

        private async Task<CopilotMcpToolCallResult> DiagnoseFlowFailureAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
            var nodeQuery = FirstNonEmpty(GetString(arguments, "node_id"), GetString(arguments, "node_name"), GetString(arguments, "node"));
            var logQuery = FirstNonEmpty(GetString(arguments, "query"), GetString(arguments, "log_query"), "error");
            var maxLogLines = Math.Clamp(GetInt(arguments, "max_log_lines") ?? 120, 1, 300);
            var logResult = _environment.RecentLogProvider(logQuery, CopilotRecentLogMode.RecentLines, maxLogLines, 12000);
            var liveContext = _environment.LiveContextProvider();
            var templateJson = ExtractCurrentTemplateJson();
            var matchedNode = snapshot == null ? null : FindFlowNode(snapshot, nodeQuery);
            var evidence = BuildFailureEvidenceText(snapshot, matchedNode, logResult, liveContext, templateJson);

            var builder = new StringBuilder();
            builder.AppendLine("ColorVision flow failure diagnosis");
            builder.AppendLine("Mode: read-only diagnosis");
            builder.AppendLine("Would execute: False");
            builder.AppendLine("Flow execution allowed: False");
            builder.AppendLine($"Requested node: {EmptyLabel(nodeQuery)}");
            builder.AppendLine($"Log query: {EmptyLabel(logQuery)}");
            builder.AppendLine($"Inspected log lines: {maxLogLines}");

            builder.AppendLine();
            builder.AppendLine("## Observed Symptoms");
            if (snapshot == null)
            {
                builder.AppendLine("- No active flow snapshot is available.");
            }
            else
            {
                builder.AppendLine($"- Flow: {EmptyLabel(snapshot.FlowName)}");
                builder.AppendLine($"- Status: {EmptyLabel(snapshot.Status)}");
                builder.AppendLine($"- Running: {snapshot.IsRunning}");
                builder.AppendLine($"- Batch status: {EmptyLabel(snapshot.BatchStatus)}");
                builder.AppendLine($"- Batch result: {EmptyLabel(snapshot.BatchResult)}");
                builder.AppendLine($"- Last node: {EmptyLabel(snapshot.LastNodeSummary)}");
                builder.AppendLine($"- Recent failure: {EmptyLabel(snapshot.RecentFailureSummary)}");
            }

            builder.AppendLine();
            builder.AppendLine("## Related Node");
            if (matchedNode == null)
            {
                builder.AppendLine(string.IsNullOrWhiteSpace(nodeQuery)
                    ? "- No node was selected or requested."
                    : "- No node matched the requested node id/name.");
            }
            else
            {
                builder.AppendLine($"- Title: {EmptyLabel(FirstNonEmpty(matchedNode.Title, matchedNode.NodeName, matchedNode.NodeId))}");
                builder.AppendLine($"- Id: {EmptyLabel(matchedNode.NodeId)}");
                builder.AppendLine($"- Type: {EmptyLabel(matchedNode.NodeType)}");
                builder.AppendLine($"- Active: {matchedNode.IsActive}");
                builder.AppendLine($"- Selected: {matchedNode.IsSelected}");
                builder.AppendLine($"- Mark: {EmptyLabel(matchedNode.Mark)}");
                if (matchedNode.Parameters.Count > 0)
                    builder.AppendLine($"- Parameters: {RedactForDisplay(string.Join(", ", matchedNode.Parameters.Select(item => $"{item.Name}={item.Value}")))}");
            }

            builder.AppendLine();
            builder.AppendLine("## Template Context");
            if (string.IsNullOrWhiteSpace(templateJson))
            {
                builder.AppendLine("- No active template JSON editor context is available.");
            }
            else
            {
                builder.AppendLine($"- Active template source: {EmptyLabel(liveContext?.SourceId)}");
                AppendTemplateFieldHints(builder, templateJson, evidence);
            }

            builder.AppendLine();
            builder.AppendLine("## Recent Log Clues");
            if (logResult.Success)
            {
                builder.AppendLine(RedactForDisplay(TrimLong(string.Join(Environment.NewLine, new[] { logResult.Summary, logResult.Content }.Where(value => !string.IsNullOrWhiteSpace(value))), 5000)));
            }
            else
            {
                builder.AppendLine($"- Recent log unavailable: {EmptyLabel(logResult.ErrorMessage ?? logResult.Summary)}");
            }

            builder.AppendLine();
            builder.AppendLine("## Likely Causes");
            foreach (var cause in BuildLikelyFailureCauses(evidence))
                builder.AppendLine("- " + cause);

            builder.AppendLine();
            builder.AppendLine("## Suggested Next MCP Calls");
            if (matchedNode != null)
                builder.AppendLine($"- preview_flow_action {{ \"action\": \"trace_recent_failure\", \"node_id\": \"{EscapeForInlineJson(matchedNode.NodeId)}\" }}");
            builder.AppendLine("- get_diagnostic_bundle { \"max_chars\": 12000 }");
            builder.AppendLine("- suggest_template_patch { \"intent\": \"summarize the suspected parameter adjustment\", \"node_name\": \"" + EscapeForInlineJson(FirstNonEmpty(matchedNode?.Title ?? string.Empty, nodeQuery)) + "\" }");
            builder.AppendLine("- preview_template_patch only after choosing explicit proposed_changes.");
            builder.AppendLine("No flow was started, stopped, run, rerun, or modified.");

            return CopilotMcpToolCallResult.Ok(RedactForDiagnostics(builder.ToString().TrimEnd()));
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

        private async Task<CopilotMcpToolCallResult> ExecuteMenuAsync(IReadOnlyDictionary<string, JsonElement>? arguments, string callerSource, CancellationToken cancellationToken)
        {
            var query = GetString(arguments, "query");
            if (string.IsNullOrWhiteSpace(query))
                return CopilotMcpToolCallResult.Fail("missing_query", "The execute_menu tool requires a non-empty query argument.");

            var dryRun = GetBool(arguments, "dry_run") ?? true;
            var frameworkApproved = IsInAppAgentFrameworkApproved(callerSource);
            var inAppAgent = IsInAppAgent(callerSource);

            if (inAppAgent && !dryRun)
            {
                if (_environment.ExecuteMenuHandler != null)
                {
                    var handlerPreview = await _environment.ExecuteMenuHandler(query, true, cancellationToken);
                    if (!handlerPreview.Success)
                        return handlerPreview;

                    return CreateConfirmableActionResult(
                        "Confirm menu command",
                        $"Execute ColorVision menu command: {query}",
                        "execute_menu",
                        arguments,
                        handlerPreview.Text,
                        token => _environment.ExecuteMenuHandler(query, false, token),
                        executeOnApproval: true);
                }

                if (Application.Current == null)
                    return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

                var applicationPreview = await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: true, allowConfirmationRequired: false, cancellationToken);
                if (!applicationPreview.Success)
                    return ToMcpResult(applicationPreview, "menu_preview_failed");

                return CreateConfirmableActionResult(
                    "Confirm menu command",
                    $"Execute ColorVision menu command: {query}",
                    "execute_menu",
                    arguments,
                    string.Join(Environment.NewLine, new[] { applicationPreview.Summary, applicationPreview.Content }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    async token => ToMcpResult(await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: false, allowConfirmationRequired: true, token), "menu_execution_failed"),
                    executeOnApproval: true);
            }

            if (_environment.ExecuteMenuHandler != null)
            {
                var handlerResult = await _environment.ExecuteMenuHandler(query, dryRun, cancellationToken);
                if (!dryRun && IsConfirmationRequiredResult(handlerResult))
                {
                    if (frameworkApproved)
                        return await _environment.ExecuteMenuHandler(query, false, cancellationToken);

                    return CreateConfirmableActionResult(
                        "Confirm menu command",
                        $"Execute ColorVision menu command: {query}",
                        "execute_menu",
                        arguments,
                        handlerResult.Text,
                        token => _environment.ExecuteMenuHandler(query, false, token),
                        executeOnApproval: IsInAppAgent(callerSource));
                }

                return handlerResult;
            }

            if (Application.Current == null)
                return CopilotMcpToolCallResult.Fail("application_unavailable", "The WPF application is not available.");

            var result = await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun, allowConfirmationRequired: frameworkApproved, cancellationToken);
            if (!dryRun && IsConfirmationRequiredResult(result))
            {
                return CreateConfirmableActionResult(
                    "Confirm menu command",
                    $"Execute ColorVision menu command: {query}",
                    "execute_menu",
                    arguments,
                    string.Join(Environment.NewLine, new[] { result.Summary, result.Content, result.ErrorMessage }.Where(value => !string.IsNullOrWhiteSpace(value))),
                    async token => ToMcpResult(await CopilotApplicationCapability.ExecuteMenuAsync(query, dryRun: false, allowConfirmationRequired: true, token), "menu_execution_failed"),
                    executeOnApproval: IsInAppAgent(callerSource));
            }

            return ToMcpResult(result, "menu_execution_failed");
        }

        private async Task<CopilotMcpToolCallResult> CreateFlowAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string callerSource,
            CancellationToken cancellationToken)
        {
            var flowName = CopilotFlowCreationSupport.ResolveFlowName(null, GetString(arguments, "name"));
            var normalizedArguments = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase)
            {
                ["name"] = JsonSerializer.SerializeToElement(flowName),
            };

            if (string.Equals(callerSource, InAppAgentFrameworkApprovedCallerSource, StringComparison.OrdinalIgnoreCase))
                return await _environment.CreateFlowHandler(flowName, cancellationToken);

            return CreateConfirmableActionResult(
                "Confirm new flow creation",
                $"Create a new empty ColorVision flow: {flowName}",
                "create_flow",
                normalizedArguments,
                $"Flow name: {flowName}{Environment.NewLine}The flow will be created but will not be opened or executed automatically.",
                token => _environment.CreateFlowHandler(flowName, token),
                executeOnApproval: IsInAppAgent(callerSource));
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
            if (!TryBuildTemplatePatchComputation(arguments, out var computation, out var errorCode, out var errorMessage))
                return CopilotMcpToolCallResult.Fail(errorCode, errorMessage);

            CopilotMcpTemplatePatchPreview? storedPreview = null;
            if (computation.IsApplyEligible)
            {
                storedPreview = CopilotMcpTemplatePatchPreviewStore.Instance.Create(
                    computation.TemplateIdentifier,
                    computation.SourceId,
                    computation.CurrentJson,
                    computation.ProposedChangesJson,
                    computation.PatchedJson,
                    computation.Changes);
            }

            return CopilotMcpToolCallResult.Ok(BuildTemplatePatchPreviewText(computation, storedPreview));
        }

        private async Task<CopilotMcpToolCallResult> SuggestTemplatePatchAsync(IReadOnlyDictionary<string, JsonElement>? arguments, CancellationToken cancellationToken)
        {
            var intent = FirstNonEmpty(
                GetString(arguments, "intent"),
                GetString(arguments, "diagnosis"),
                GetString(arguments, "goal"),
                GetString(arguments, "target"));
            var nodeQuery = FirstNonEmpty(GetString(arguments, "node_id"), GetString(arguments, "node_name"), GetString(arguments, "node"));
            var templateIdentifier = FirstNonEmpty(GetString(arguments, "template_identifier"), GetString(arguments, "template"), GetString(arguments, "identifier"), "active-template");
            var currentJson = GetString(arguments, "current_json");
            var sourceId = string.Empty;
            if (string.IsNullOrWhiteSpace(currentJson))
                TryGetActiveTemplateSourceAndJson(out sourceId, out currentJson);

            if (string.IsNullOrWhiteSpace(currentJson))
                return CopilotMcpToolCallResult.Fail("template_context_unavailable", "No active template JSON is available. Open a template JSON editor or provide current_json before suggesting a template patch.");

            try
            {
                using var currentDocument = JsonDocument.Parse(currentJson);
                if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                    return CopilotMcpToolCallResult.Fail("invalid_template_json", $"The current template JSON root must be an object, but was {currentDocument.RootElement.ValueKind}.");

                var hasProposedChanges = arguments != null && arguments.ContainsKey("proposed_changes");
                JsonDocument? proposedDocument = null;
                try
                {
                    if (hasProposedChanges)
                    {
                        if (!TryGetJsonArgument(arguments, "proposed_changes", out var proposedChangesJson, out var proposedChangesError))
                            return CopilotMcpToolCallResult.Fail("missing_proposed_changes", proposedChangesError);

                        proposedDocument = JsonDocument.Parse(proposedChangesJson);
                        if (proposedDocument.RootElement.ValueKind != JsonValueKind.Object)
                            return CopilotMcpToolCallResult.Fail("invalid_proposed_changes", $"The proposed_changes root must be an object, but was {proposedDocument.RootElement.ValueKind}.");

                        if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                            return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", $"suggest_template_patch refuses to suggest sensitive fields: {sensitivePath}.");
                    }

                    var snapshot = await _environment.FlowSnapshotProvider(cancellationToken);
                    var matchedNode = snapshot == null ? null : FindFlowNode(snapshot, nodeQuery);
                    var builder = new StringBuilder();
                    builder.AppendLine("ColorVision template patch suggestion");
                    builder.AppendLine("Mode: read-only suggestion");
                    builder.AppendLine("Would apply: False");
                    builder.AppendLine("Would save: False");
                    builder.AppendLine($"Template identifier: {templateIdentifier.Trim()}");
                    builder.AppendLine($"Template source: {EmptyLabel(sourceId)}");
                    builder.AppendLine($"Intent: {EmptyLabel(intent)}");
                    builder.AppendLine($"Related node: {EmptyLabel(FirstNonEmpty(matchedNode?.Title ?? string.Empty, matchedNode?.NodeName ?? string.Empty, nodeQuery))}");

                    builder.AppendLine();
                    builder.AppendLine("## Candidate Fields");
                    foreach (var field in BuildTemplatePatchCandidateFields(currentDocument.RootElement, intent, matchedNode).Take(20))
                        builder.AppendLine("- " + field);

                    if (proposedDocument != null)
                    {
                        var changes = BuildTemplatePatchChangeLines(currentDocument.RootElement, proposedDocument.RootElement);
                        var warnings = BuildTemplatePatchWarningLines(currentDocument.RootElement, proposedDocument.RootElement);

                        builder.AppendLine();
                        builder.AppendLine("## Proposed Changes");
                        builder.AppendLine(proposedDocument.RootElement.ToString());
                        builder.AppendLine();
                        builder.AppendLine("## Change Summary");
                        if (changes.Count == 0)
                            builder.AppendLine("- No top-level changes detected.");
                        foreach (var change in changes.Take(80))
                            builder.AppendLine(change);

                        builder.AppendLine();
                        builder.AppendLine("## Safety Warnings");
                        if (warnings.Count == 0)
                            builder.AppendLine("- No type-change, null, or unknown-key warnings were detected.");
                        foreach (var warning in warnings.Take(80))
                            builder.AppendLine(warning);

                        builder.AppendLine();
                        builder.AppendLine("## Next MCP Call");
                        builder.AppendLine("Call preview_template_patch with this payload, then review the returned diff and preview_id:");
                        builder.AppendLine("```json");
                        builder.AppendLine(BuildPreviewTemplatePatchPayload(templateIdentifier, proposedDocument.RootElement));
                        builder.AppendLine("```");
                    }
                    else
                    {
                        builder.AppendLine();
                        builder.AppendLine("## Suggested Patch Shape");
                        builder.AppendLine("No proposed_changes were supplied. Choose explicit top-level fields from Candidate Fields, then call suggest_template_patch again with proposed_changes or call preview_template_patch directly.");
                        builder.AppendLine("```json");
                        builder.AppendLine("{");
                        builder.AppendLine("  \"template_identifier\": \"" + EscapeForInlineJson(templateIdentifier) + "\",");
                        builder.AppendLine("  \"proposed_changes\": {");
                        builder.AppendLine("    \"FieldName\": \"new value\"");
                        builder.AppendLine("  }");
                        builder.AppendLine("}");
                        builder.AppendLine("```");
                    }

                    builder.AppendLine();
                    builder.AppendLine("No template JSON was applied, saved, or mutated.");
                    return CopilotMcpToolCallResult.Ok(RedactForDiagnostics(builder.ToString().TrimEnd()));
                }
                finally
                {
                    proposedDocument?.Dispose();
                }
            }
            catch (JsonException ex)
            {
                return CopilotMcpToolCallResult.Fail("invalid_template_patch_json", $"Template patch suggestion failed JSON validation: {ex.Message}");
            }
        }

        private async Task<CopilotMcpToolCallResult> ApplyTemplatePatchAsync(IReadOnlyDictionary<string, JsonElement>? arguments, string callerSource, CancellationToken cancellationToken)
        {
            var previewId = GetString(arguments, "preview_id");
            if (!CopilotMcpTemplatePatchPreviewStore.Instance.TryGet(previewId, out var preview, out var previewMessage))
                return CopilotMcpToolCallResult.Fail("template_patch_preview_required", previewMessage);

            var validationResult = ValidateTemplatePatchPreviewCanApply(preview);
            if (!validationResult.Success)
                return validationResult;

            if (IsInAppAgentFrameworkApproved(callerSource))
                return await ExecuteTemplatePatchPreviewAsync(preview.PreviewId, cancellationToken);

            return CreateConfirmableActionResult(
                "Confirm template patch",
                $"Apply previewed JSON changes to active template editor: {preview.TemplateIdentifier}",
                "apply_template_patch",
                arguments,
                BuildTemplatePatchConfirmationPreview(preview),
                token => ExecuteTemplatePatchPreviewAsync(preview.PreviewId, token),
                executeOnApproval: string.Equals(callerSource, InAppAgentCallerSource, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<CopilotMcpToolCallResult> ExecuteTemplatePatchPreviewAsync(string previewId, CancellationToken cancellationToken)
        {
            if (!CopilotMcpTemplatePatchPreviewStore.Instance.TryGet(previewId, out var preview, out var previewMessage))
                return CopilotMcpToolCallResult.Fail("template_patch_preview_expired", previewMessage);

            var validationResult = ValidateTemplatePatchPreviewCanApply(preview);
            if (!validationResult.Success)
                return validationResult;

            using var proposedDocument = JsonDocument.Parse(preview.ProposedChangesJson);
            if (!TryFindSensitiveJsonProperty(proposedDocument.RootElement, out _))
            {
                var request = new CopilotTemplatePatchApplyRequest
                {
                    PreviewId = preview.PreviewId,
                    TemplateIdentifier = preview.TemplateIdentifier,
                    SourceId = preview.SourceId,
                    ExpectedCurrentJson = preview.CurrentJson,
                    PatchedJson = preview.PatchedJson,
                };

                return await _environment.ApplyTemplatePatchHandler(request, cancellationToken);
            }

            return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", "apply_template_patch refuses to modify sensitive template fields.");
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
                    "risk_level: confirmation-required\nwould_execute: False\nexecution_status: not_supported_current_stage\nFlow start/stop/run requests are intentionally not executed by ColorVision MCP. Suggested next step: inspect the flow summary and preview a node-specific diagnostic action instead.");
            }

            if (action != "select_node" && action != "open_node_property" && action != "inspect_node_errors" && action != "explain_node" && action != "trace_recent_failure")
            {
                return CopilotMcpToolCallResult.Fail("unsupported_flow_preview_action", "Supported preview actions: select_node, open_node_property, inspect_node_errors, explain_node, trace_recent_failure. Flow start/stop/run is not supported.");
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
                if (action == "inspect_node_errors" || action == "trace_recent_failure")
                {
                    builder.AppendLine($"Node mark: {EmptyLabel(matchedNode.Mark)}");
                    builder.AppendLine($"Recent flow failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
                }
                if (action == "explain_node")
                {
                    builder.AppendLine($"Node active: {matchedNode.IsActive}");
                    AppendList(builder, "Node inputs", matchedNode.Inputs);
                    AppendList(builder, "Node outputs", matchedNode.Outputs);
                    if (matchedNode.Parameters.Count > 0)
                        builder.AppendLine($"Node parameters: {RedactForDisplay(string.Join(", ", matchedNode.Parameters.Select(item => $"{item.Name}={item.Value}")))}");
                }
            }
            else if (!string.IsNullOrWhiteSpace(nodeQuery))
            {
                builder.AppendLine("Matched node: (none)");
                builder.AppendLine("Available nodes:");
                foreach (var node in snapshot.Nodes.Take(20))
                    builder.AppendLine($"- {EmptyLabel(FirstNonEmpty(node.Title, node.NodeName, node.NodeId))} [{EmptyLabel(node.NodeId)}]");
            }

            if (action == "trace_recent_failure")
            {
                builder.AppendLine($"Recent failure summary: {EmptyLabel(snapshot.RecentFailureSummary)}");
                builder.AppendLine($"Last node: {EmptyLabel(snapshot.LastNodeSummary)}");
                if (!string.IsNullOrWhiteSpace(snapshot.RecentRunMessage))
                {
                    builder.AppendLine("Recent run message:");
                    builder.AppendLine(TrimLong(RedactForDisplay(snapshot.RecentRunMessage), 2000));
                }
            }

            builder.AppendLine("Suggested next steps:");
            foreach (var suggestion in BuildFlowPreviewSuggestions(action, matchedNode, snapshot))
                builder.AppendLine("- " + suggestion);

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

        private async Task<CopilotMcpToolCallResult> SetLanguageAsync(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string callerSource,
            CancellationToken cancellationToken)
        {
            var languageQuery = FirstNonEmpty(GetString(arguments, "language"), GetString(arguments, "query"));
            if (string.IsNullOrWhiteSpace(languageQuery))
                return CopilotMcpToolCallResult.Fail("missing_language", "The set_language tool requires a non-empty language argument.");

            if (IsInAppAgentFrameworkApproved(callerSource))
            {
                return _environment.SetLanguageHandler != null
                    ? await _environment.SetLanguageHandler(languageQuery, cancellationToken)
                    : ToMcpResult(await CopilotApplicationCapability.SetLanguageAsync(languageQuery, cancellationToken), "language_change_failed");
            }

            if (_environment.SetLanguageHandler != null)
            {
                return CreateConfirmableActionResult(
                    "Confirm language change",
                    $"Change ColorVision UI language: {languageQuery}",
                    "set_language",
                    arguments,
                    "Changing language may affect UI state and can trigger the existing restart confirmation flow.",
                    token => _environment.SetLanguageHandler(languageQuery, token),
                    executeOnApproval: IsInAppAgent(callerSource));
            }

            return CreateConfirmableActionResult(
                "Confirm language change",
                $"Change ColorVision UI language: {languageQuery}",
                "set_language",
                arguments,
                "Changing language may affect UI state and can trigger the existing restart confirmation flow.",
                async token => ToMcpResult(await CopilotApplicationCapability.SetLanguageAsync(languageQuery, token), "language_change_failed"),
                executeOnApproval: IsInAppAgent(callerSource));
        }

        private CopilotMcpToolCallResult CreateConfirmableActionResult(
            string title,
            string description,
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string previewText,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor,
            bool executeOnApproval = false)
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
                executor,
                executeOnApproval);

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
            builder.AppendLine(executeOnApproval
                ? "User must approve this action in the ColorVision Copilot Pending Actions area; the in-app action executes immediately after approval."
                : "User must approve this action in the ColorVision Copilot Pending Actions area before confirm_action can execute it.");
            if (!string.IsNullOrWhiteSpace(previewText))
            {
                builder.AppendLine();
                builder.AppendLine("Preview:");
                builder.AppendLine(TrimLong(RedactForDisplay(previewText), 8000));
            }

            return CopilotMcpToolCallResult.ApprovalRequired(builder.ToString().TrimEnd(), action);
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

        private static bool IsInAppAgent(string callerSource)
        {
            return string.Equals(callerSource, InAppAgentCallerSource, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsInAppAgentFrameworkApproved(string callerSource)
        {
            return string.Equals(callerSource, InAppAgentFrameworkApprovedCallerSource, StringComparison.OrdinalIgnoreCase);
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

        private bool TryBuildTemplatePatchComputation(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            out TemplatePatchComputation computation,
            out string errorCode,
            out string errorMessage)
        {
            computation = new TemplatePatchComputation();
            errorCode = string.Empty;
            errorMessage = string.Empty;

            var templateIdentifier = FirstNonEmpty(GetString(arguments, "template_identifier"), GetString(arguments, "template"), GetString(arguments, "identifier"));
            if (string.IsNullOrWhiteSpace(templateIdentifier))
            {
                errorCode = "missing_template_identifier";
                errorMessage = "The preview_template_patch tool requires template_identifier.";
                return false;
            }

            if (!TryGetJsonArgument(arguments, "proposed_changes", out var proposedChangesJson, out var proposedChangesError))
            {
                errorCode = "missing_proposed_changes";
                errorMessage = proposedChangesError;
                return false;
            }

            var currentJson = GetString(arguments, "current_json");
            var sourceId = string.Empty;
            if (string.IsNullOrWhiteSpace(currentJson))
            {
                if (!TryGetActiveTemplateSourceAndJson(out sourceId, out currentJson))
                {
                    errorCode = "template_context_unavailable";
                    errorMessage = "No current active template JSON editor context is available. Open a template JSON editor or provide current_json for preview-only use.";
                    return false;
                }
            }

            try
            {
                using var currentDocument = JsonDocument.Parse(currentJson);
                using var proposedDocument = JsonDocument.Parse(proposedChangesJson);

                if (currentDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errorCode = "invalid_template_json";
                    errorMessage = $"The current template JSON root must be an object, but was {currentDocument.RootElement.ValueKind}.";
                    return false;
                }

                if (proposedDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    errorCode = "invalid_proposed_changes";
                    errorMessage = $"The proposed_changes root must be an object, but was {proposedDocument.RootElement.ValueKind}.";
                    return false;
                }

                if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                {
                    errorCode = "sensitive_template_field_not_allowed";
                    errorMessage = $"preview_template_patch refuses to modify sensitive fields: {sensitivePath}.";
                    return false;
                }

                var changes = BuildTemplatePatchChangeLines(currentDocument.RootElement, proposedDocument.RootElement);
                computation = new TemplatePatchComputation
                {
                    TemplateIdentifier = templateIdentifier.Trim(),
                    SourceId = sourceId,
                    CurrentJson = currentJson,
                    ProposedChangesJson = proposedChangesJson,
                    PatchedJson = CreatePatchedTemplateJson(currentJson, proposedChangesJson),
                    Changes = changes,
                };
                return true;
            }
            catch (JsonException ex)
            {
                errorCode = "invalid_template_patch_json";
                errorMessage = $"Template patch preview failed JSON validation: {ex.Message}";
                return false;
            }
        }

        private CopilotMcpToolCallResult ValidateTemplatePatchPreviewCanApply(CopilotMcpTemplatePatchPreview preview)
        {
            if (string.IsNullOrWhiteSpace(preview.SourceId))
                return CopilotMcpToolCallResult.Fail("template_patch_not_applyable", "The preview was not created from the active template editor. Re-run preview_template_patch without current_json while the template editor is active.");

            if (!TryGetActiveTemplateSourceAndJson(out var activeSourceId, out var activeJson))
                return CopilotMcpToolCallResult.Fail("template_context_unavailable", "No active template JSON editor context is available.");

            if (!string.Equals(activeSourceId, preview.SourceId, StringComparison.OrdinalIgnoreCase))
                return CopilotMcpToolCallResult.Fail("template_context_mismatch", "The active template editor is not the same editor that created the preview. Re-run preview_template_patch for the current editor.");

            if (!JsonSemanticallyEquals(activeJson, preview.CurrentJson, out var compareError))
                return CopilotMcpToolCallResult.Fail("template_patch_conflict", string.IsNullOrWhiteSpace(compareError)
                    ? "The active template JSON changed after preview_template_patch. Re-run preview_template_patch before applying."
                    : compareError);

            try
            {
                using var proposedDocument = JsonDocument.Parse(preview.ProposedChangesJson);
                if (TryFindSensitiveJsonProperty(proposedDocument.RootElement, out var sensitivePath))
                    return CopilotMcpToolCallResult.Fail("sensitive_template_field_not_allowed", $"apply_template_patch refuses to modify sensitive fields: {sensitivePath}.");

                using var patchedDocument = JsonDocument.Parse(preview.PatchedJson);
                if (patchedDocument.RootElement.ValueKind != JsonValueKind.Object)
                    return CopilotMcpToolCallResult.Fail("invalid_patched_template_json", "The patched template JSON root is not an object.");
            }
            catch (JsonException ex)
            {
                return CopilotMcpToolCallResult.Fail("invalid_patched_template_json", $"The patched template JSON is invalid: {ex.Message}");
            }

            return CopilotMcpToolCallResult.Ok("ok");
        }

        private bool TryGetActiveTemplateSourceAndJson(out string sourceId, out string currentJson)
        {
            sourceId = string.Empty;
            currentJson = string.Empty;
            var liveContext = _environment.LiveContextProvider();
            if (liveContext == null || !liveContext.SourceId.StartsWith("template-json-editor:", StringComparison.OrdinalIgnoreCase))
                return false;

            foreach (var item in liveContext.SnapshotItems)
            {
                var json = ExtractFencedJson(item.Content);
                if (string.IsNullOrWhiteSpace(json))
                    continue;

                sourceId = liveContext.SourceId;
                currentJson = json;
                return true;
            }

            return false;
        }

        private static IReadOnlyList<string> BuildTemplatePatchChangeLines(JsonElement currentRoot, JsonElement proposedRoot)
        {
            var changes = new List<string>();
            foreach (var proposedProperty in proposedRoot.EnumerateObject())
            {
                currentRoot.TryGetProperty(proposedProperty.Name, out var currentValue);
                var currentText = currentValue.ValueKind == JsonValueKind.Undefined ? "(missing)" : DescribeJsonValue(currentValue);
                var proposedText = DescribeJsonValue(proposedProperty.Value);
                if (string.Equals(currentText, proposedText, StringComparison.Ordinal))
                    continue;

                changes.Add($"- {proposedProperty.Name}: {currentText} -> {proposedText}");
            }

            return changes;
        }

        private static IReadOnlyList<string> BuildTemplatePatchWarningLines(JsonElement currentRoot, JsonElement proposedRoot)
        {
            var warnings = new List<string>();
            foreach (var proposedProperty in proposedRoot.EnumerateObject())
            {
                if (!currentRoot.TryGetProperty(proposedProperty.Name, out var currentValue))
                {
                    warnings.Add($"- Warning: {proposedProperty.Name} is a new top-level key. Confirm the template schema supports it.");
                    continue;
                }

                if (proposedProperty.Value.ValueKind == JsonValueKind.Null)
                    warnings.Add($"- Warning: {proposedProperty.Name} is set to null. Confirm this does not disable or remove required behavior.");

                if (!JsonKindsCompatible(currentValue.ValueKind, proposedProperty.Value.ValueKind))
                    warnings.Add($"- Warning: {proposedProperty.Name} changes type from {currentValue.ValueKind} to {proposedProperty.Value.ValueKind}.");
            }

            return warnings;
        }

        private static string CreatePatchedTemplateJson(string currentJson, string proposedChangesJson)
        {
            var currentObject = JsonNode.Parse(currentJson)?.AsObject()
                ?? throw new JsonException("Current template JSON root must be an object.");
            var proposedObject = JsonNode.Parse(proposedChangesJson)?.AsObject()
                ?? throw new JsonException("Proposed changes root must be an object.");

            foreach (var property in proposedObject)
                currentObject[property.Key] = property.Value?.DeepClone();

            return currentObject.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static string BuildTemplatePatchPreviewText(TemplatePatchComputation computation, CopilotMcpTemplatePatchPreview? storedPreview)
        {
            var builder = new StringBuilder();
            builder.AppendLine("ColorVision template patch preview");
            builder.AppendLine($"Template identifier: {computation.TemplateIdentifier}");
            builder.AppendLine("Mode: preview only");
            builder.AppendLine("Would save: False");
            builder.AppendLine("Current JSON valid: True");
            builder.AppendLine("Proposed changes valid: True");
            builder.AppendLine($"Apply eligible: {computation.IsApplyEligible}");
            if (storedPreview != null)
            {
                builder.AppendLine($"preview_id: {storedPreview.PreviewId}");
                builder.AppendLine($"source_id: {storedPreview.SourceId}");
                builder.AppendLine($"current_json_hash: {storedPreview.CurrentJsonHash}");
                builder.AppendLine($"preview_expires_at: {storedPreview.ExpiresAt:O}");
                builder.AppendLine("Next step: call apply_template_patch with this preview_id to create a user-confirmed action.");
            }
            else
            {
                builder.AppendLine("Next step: open the target template JSON editor and call preview_template_patch without current_json before applying.");
            }
            builder.AppendLine($"Changed key fields: {computation.Changes.Count}");
            foreach (var change in computation.Changes.Take(80))
                builder.AppendLine(change);
            foreach (var warning in BuildTemplatePatchWarnings(computation).Take(80))
                builder.AppendLine(warning);
            builder.AppendLine("No template file was saved or mutated.");
            return builder.ToString().TrimEnd();
        }

        private static IReadOnlyList<string> BuildTemplatePatchWarnings(TemplatePatchComputation computation)
        {
            try
            {
                using var currentDocument = JsonDocument.Parse(computation.CurrentJson);
                using var proposedDocument = JsonDocument.Parse(computation.ProposedChangesJson);
                return BuildTemplatePatchWarningLines(currentDocument.RootElement, proposedDocument.RootElement);
            }
            catch (JsonException)
            {
                return Array.Empty<string>();
            }
        }

        private static string BuildTemplatePatchConfirmationPreview(CopilotMcpTemplatePatchPreview preview)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Template patch ready for user confirmation");
            builder.AppendLine($"preview_id: {preview.PreviewId}");
            builder.AppendLine($"template_identifier: {preview.TemplateIdentifier}");
            builder.AppendLine($"source_id: {preview.SourceId}");
            builder.AppendLine($"current_json_hash: {preview.CurrentJsonHash}");
            builder.AppendLine($"changed_key_fields: {preview.Changes.Count}");
            foreach (var change in preview.Changes.Take(80))
                builder.AppendLine(change);
            builder.AppendLine("The active template JSON will be revalidated and conflict-checked again when confirm_action executes.");
            return builder.ToString().TrimEnd();
        }

        private static bool JsonSemanticallyEquals(string leftJson, string rightJson, out string error)
        {
            error = string.Empty;
            try
            {
                using var leftDocument = JsonDocument.Parse(leftJson);
                using var rightDocument = JsonDocument.Parse(rightJson);
                if (leftDocument.RootElement.ValueKind != JsonValueKind.Object || rightDocument.RootElement.ValueKind != JsonValueKind.Object)
                {
                    error = "Template JSON root must be an object.";
                    return false;
                }

                return string.Equals(
                    JsonSerializer.Serialize(leftDocument.RootElement),
                    JsonSerializer.Serialize(rightDocument.RootElement),
                    StringComparison.Ordinal);
            }
            catch (JsonException ex)
            {
                error = $"Template JSON validation failed during conflict check: {ex.Message}";
                return false;
            }
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

        private static IReadOnlyList<string> BuildFlowPreviewSuggestions(string action, CopilotFlowNodeContextSnapshot? matchedNode, CopilotFlowContextSnapshot snapshot)
        {
            return action switch
            {
                "select_node" => matchedNode == null
                    ? new[] { "Choose one of the listed node_id values and preview select_node again." }
                    : new[] { "Use open_node_property to inspect the matched node in ColorVision.", "Review get_flow_summary before changing any template parameters." },
                "open_node_property" => matchedNode == null
                    ? new[] { "Provide node_id or node_name for the node whose properties should be inspected." }
                    : new[] { "Open the node property panel in ColorVision for manual review.", "Use explain_node for a read-only parameter summary before editing templates." },
                "inspect_node_errors" => new[] { "Review node mark and recent failure summary.", "Use trace_recent_failure to correlate the last node and recent run message." },
                "explain_node" => new[] { "Compare the node parameters with the active template JSON.", "Use preview_template_patch for any proposed template change before applying it." },
                "trace_recent_failure" => string.IsNullOrWhiteSpace(snapshot.RecentFailureSummary)
                    ? new[] { "No recent failure summary is available; capture get_recent_log with an error query if needed." }
                    : new[] { "Inspect the matched or last node before editing parameters.", "Use get_diagnostic_bundle for a compact shareable diagnostic snapshot." },
                _ => new[] { "Use get_flow_summary for read-only flow context." },
            };
        }

        private static string BuildFailureEvidenceText(
            CopilotFlowContextSnapshot? snapshot,
            CopilotFlowNodeContextSnapshot? matchedNode,
            CopilotCapabilityResult logResult,
            CopilotLiveContext? liveContext,
            string templateJson)
        {
            var builder = new StringBuilder();
            if (snapshot != null)
            {
                builder.AppendLine(snapshot.FlowName);
                builder.AppendLine(snapshot.Status);
                builder.AppendLine(snapshot.BatchStatus);
                builder.AppendLine(snapshot.BatchResult);
                builder.AppendLine(snapshot.LastNodeSummary);
                builder.AppendLine(snapshot.RecentFailureSummary);
                builder.AppendLine(snapshot.RecentRunMessage);
            }

            if (matchedNode != null)
            {
                builder.AppendLine(matchedNode.Title);
                builder.AppendLine(matchedNode.NodeName);
                builder.AppendLine(matchedNode.NodeType);
                builder.AppendLine(matchedNode.DeviceCode);
                builder.AppendLine(matchedNode.Mark);
                foreach (var parameter in matchedNode.Parameters)
                    builder.AppendLine(parameter.Name + "=" + parameter.Value);
            }

            builder.AppendLine(liveContext?.Title);
            builder.AppendLine(liveContext?.Summary);
            builder.AppendLine(logResult.Summary);
            builder.AppendLine(logResult.Content);
            builder.AppendLine(templateJson);
            return RedactForDiagnostics(builder.ToString());
        }

        private static IReadOnlyList<string> BuildLikelyFailureCauses(string evidence)
        {
            var lower = (evidence ?? string.Empty).ToLowerInvariant();
            var causes = new List<string>();

            if (lower.Contains("timeout"))
                causes.Add("Timeout evidence is present. Check acquisition latency, trigger timing, exposure duration, retry/delay settings, and device connectivity.");

            if (lower.Contains("camera") || lower.Contains("image") || lower.Contains("acquire"))
                causes.Add("Camera/acquisition evidence is present. Compare the related node parameters with template fields such as Exposure, Gain, Timeout, ROI, Width, and Height.");

            if (lower.Contains("exposure") || lower.Contains("gain") || lower.Contains("brightness"))
                causes.Add("Image brightness or acquisition-parameter evidence is present. Treat exposure/gain changes as a template patch candidate, then preview the JSON diff first.");

            if (lower.Contains("threshold") || lower.Contains("limit") || lower.Contains("min") || lower.Contains("max") || lower.Contains("ng"))
                causes.Add("Threshold/limit evidence is present. Review min/max/threshold fields before proposing any template patch.");

            if (lower.Contains("template") || lower.Contains("json") || lower.Contains("parameter"))
                causes.Add("Template/parameter evidence is present. Use suggest_template_patch to turn the diagnosis into explicit proposed_changes, then preview_template_patch.");

            if (lower.Contains("mqtt") || lower.Contains("connect") || lower.Contains("socket") || lower.Contains("network"))
                causes.Add("Communication evidence is present. Prefer log and device-panel inspection before changing template parameters.");

            if (causes.Count == 0)
                causes.Add("No strong keyword pattern was detected. Inspect the matched node, recent log, and active template fields before changing parameters.");

            return causes;
        }

        private static void AppendTemplateFieldHints(StringBuilder builder, string templateJson, string evidence)
        {
            try
            {
                using var document = JsonDocument.Parse(templateJson);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    builder.AppendLine($"- Template JSON root: {document.RootElement.ValueKind}");
                    return;
                }

                var topLevelKeys = document.RootElement.EnumerateObject()
                    .Where(property => !IsSensitiveDisplayKey(property.Name))
                    .Take(40)
                    .Select(property => property.Name)
                    .ToArray();
                builder.AppendLine($"- Top-level keys: {string.Join(", ", topLevelKeys)}");

                var candidates = BuildTemplatePatchCandidateFields(document.RootElement, evidence, null).Take(12).ToArray();
                if (candidates.Length == 0)
                    builder.AppendLine("- Related adjustable fields: none detected from current evidence.");
                else
                {
                    builder.AppendLine("- Related adjustable fields:");
                    foreach (var candidate in candidates)
                        builder.AppendLine("  - " + candidate);
                }
            }
            catch (JsonException ex)
            {
                builder.AppendLine($"- Template JSON parse failed: {ex.Message}");
            }
        }

        private static IReadOnlyList<string> BuildTemplatePatchCandidateFields(JsonElement currentRoot, string intent, CopilotFlowNodeContextSnapshot? matchedNode)
        {
            var terms = BuildPatchIntentTerms(intent, matchedNode);
            var lines = new List<string>();
            foreach (var property in currentRoot.EnumerateObject())
            {
                if (IsSensitiveDisplayKey(property.Name) || !IsScalarJsonKind(property.Value.ValueKind))
                    continue;

                var isCommonField = IsCommonTemplateAdjustmentField(property.Name);
                var matchesIntent = terms.Any(term => property.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
                if (!isCommonField && !matchesIntent)
                    continue;

                var reason = matchesIntent ? "matches diagnosis/node wording" : "common adjustable template field";
                lines.Add($"{property.Name} ({property.Value.ValueKind}, current={DescribeJsonValue(property.Value)}) - {reason}");
            }

            if (matchedNode?.Parameters.Count > 0)
            {
                foreach (var parameter in matchedNode.Parameters)
                {
                    if (IsSensitiveDisplayKey(parameter.Name))
                        continue;

                    var matchingTemplateField = currentRoot.EnumerateObject()
                        .FirstOrDefault(property => string.Equals(property.Name, parameter.Name, StringComparison.OrdinalIgnoreCase));
                    var relation = matchingTemplateField.Value.ValueKind == JsonValueKind.Undefined
                        ? "node parameter; no same-name top-level template field detected"
                        : "node parameter; same-name top-level template field exists";
                    lines.Add($"{parameter.Name} (node parameter, value={TrimLong(RedactForDisplay(parameter.Value), 120)}) - {relation}");
                }
            }

            if (lines.Count == 0)
            {
                foreach (var property in currentRoot.EnumerateObject().Where(property => !IsSensitiveDisplayKey(property.Name) && IsScalarJsonKind(property.Value.ValueKind)).Take(12))
                    lines.Add($"{property.Name} ({property.Value.ValueKind}, current={DescribeJsonValue(property.Value)}) - available scalar template field");
            }

            return lines.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static IReadOnlyList<string> BuildPatchIntentTerms(string intent, CopilotFlowNodeContextSnapshot? matchedNode)
        {
            var text = string.Join(" ", new[]
            {
                intent,
                matchedNode?.Title,
                matchedNode?.NodeName,
                matchedNode?.NodeType,
                matchedNode?.Mark,
                matchedNode == null ? string.Empty : string.Join(" ", matchedNode.Parameters.Select(parameter => parameter.Name)),
            }.Where(value => !string.IsNullOrWhiteSpace(value)));

            var terms = text
                .Split(new[] { ' ', ',', ';', ':', '.', '/', '\\', '-', '_', '[', ']', '(', ')', '\r', '\n', '\t' }, StringSplitOptions.RemoveEmptyEntries)
                .Where(term => term.Length >= 3)
                .Select(term => term.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (text.Contains("timeout", StringComparison.OrdinalIgnoreCase))
                terms.AddRange(new[] { "timeout", "delay", "retry", "exposure" });
            if (text.Contains("camera", StringComparison.OrdinalIgnoreCase))
                terms.AddRange(new[] { "camera", "exposure", "gain", "roi", "width", "height" });
            if (text.Contains("threshold", StringComparison.OrdinalIgnoreCase) || text.Contains("ng", StringComparison.OrdinalIgnoreCase))
                terms.AddRange(new[] { "threshold", "limit", "min", "max" });

            return terms.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static bool IsCommonTemplateAdjustmentField(string name)
        {
            return new[]
            {
                "exposure",
                "gain",
                "timeout",
                "delay",
                "retry",
                "threshold",
                "limit",
                "min",
                "max",
                "roi",
                "width",
                "height",
                "offset",
                "scale",
            }.Any(term => name.Contains(term, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsScalarJsonKind(JsonValueKind kind)
        {
            return kind is JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False;
        }

        private static bool JsonKindsCompatible(JsonValueKind currentKind, JsonValueKind proposedKind)
        {
            if (currentKind == proposedKind)
                return true;

            return IsBooleanKind(currentKind) && IsBooleanKind(proposedKind);
        }

        private static bool IsBooleanKind(JsonValueKind kind)
        {
            return kind is JsonValueKind.True or JsonValueKind.False;
        }

        private static string BuildPreviewTemplatePatchPayload(string templateIdentifier, JsonElement proposedChanges)
        {
            var payload = new JsonObject
            {
                ["template_identifier"] = templateIdentifier.Trim(),
                ["proposed_changes"] = JsonNode.Parse(proposedChanges.GetRawText()),
            };

            return payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        }

        private static string EscapeForInlineJson(string? value)
        {
            return (value ?? string.Empty).Replace("\\", "\\\\").Replace("\"", "\\\"");
        }

        private static void AppendDiagnosticSection(StringBuilder builder, string title, string content)
        {
            builder.AppendLine();
            builder.AppendLine("## " + title);
            builder.AppendLine(string.IsNullOrWhiteSpace(content) ? "(empty)" : content.Trim());
        }

        private static string RedactForDiagnostics(string text)
        {
            return CopilotMcpAuditLogger.RedactText(RedactForDisplay(text));
        }

        private static string TruncateWithLimit(string text, int maxChars)
        {
            if (text.Length <= maxChars)
                return text;

            var suffix = $"{Environment.NewLine}...<diagnostic bundle truncated to max_chars={maxChars}>";
            if (suffix.Length >= maxChars)
                return text[..maxChars];

            return text[..(maxChars - suffix.Length)] + suffix;
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

        private static CopilotMcpResourceDescriptor Resource(string uri, string name, string description, string mimeType = "text/plain") => new()
        {
            Uri = uri,
            Name = name,
            Description = description,
            MimeType = mimeType,
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

        private static string FormatAuditEntryOneLine(CopilotMcpAuditEntry? entry)
        {
            if (entry == null)
                return "(none)";

            var result = entry.Success ? "success" : "failed";
            var error = entry.Success || string.IsNullOrWhiteSpace(entry.ErrorMessage)
                ? string.Empty
                : $"; error={entry.ErrorMessage}";
            var actionId = string.IsNullOrWhiteSpace(entry.ActionId)
                ? string.Empty
                : $"; action_id={entry.ActionId}";
            return $"{entry.TimestampUtc:O}; tool={EmptyLabel(entry.ToolName)}; result={result}; duration_ms={entry.DurationMs}; caller={EmptyLabel(entry.CallerSource)}{actionId}{error}";
        }

        private static bool IsRealFailureAuditEntry(CopilotMcpAuditEntry entry)
        {
            return !entry.Success && !IsApprovalFlowAuditEntry(entry);
        }

        private static bool IsApprovalFlowAuditEntry(CopilotMcpAuditEntry entry)
        {
            if (entry.Success)
                return false;

            var toolName = entry.ToolName ?? string.Empty;
            if (string.Equals(toolName, "action_rejected", StringComparison.OrdinalIgnoreCase)
                || string.Equals(toolName, "action_expired", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            var error = entry.ErrorMessage ?? string.Empty;
            return error.Contains("confirmation_required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("pending_user_confirmation", StringComparison.OrdinalIgnoreCase)
                || error.Contains("risk_level: confirmation-required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("risk_level=confirmation-required", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_pending", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_not_approved", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_rejected", StringComparison.OrdinalIgnoreCase)
                || error.Contains("action_expired", StringComparison.OrdinalIgnoreCase);
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

        private static long? GetLong(IReadOnlyDictionary<string, JsonElement>? arguments, string name)
        {
            if (arguments == null || !arguments.TryGetValue(name, out var value))
                return null;

            if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var number))
                return number;

            if (value.ValueKind == JsonValueKind.String && long.TryParse(value.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out number))
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
