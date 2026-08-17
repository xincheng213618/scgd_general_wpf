using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpToolDispatcher
    {
        public IReadOnlyList<CopilotMcpToolDescriptor> ListTools()
        {
            return _toolDefinitions.Select(definition => definition.Descriptor).ToArray();
        }

        private CopilotMcpToolDefinition[] CreateToolDefinitions()
        {
            return new[]
            {
                Definition(Tool("get_server_status", "Return ColorVision MCP server status for this authenticated request.", EmptySchema(), "status", "read-only", "Call get_server_status with no arguments."), (_, scope, _) => Task.FromResult(GetServerStatus(scope))),
                Definition(Tool("get_enabled_tools", "Return the MCP tools currently exposed by ColorVision.", EmptySchema(), "status", "read-only", "Call get_enabled_tools with no arguments."), (_, _, _) => Task.FromResult(GetEnabledTools())),
                Definition(Tool("get_audit_log", "Return recent ColorVision MCP tool-call audit entries. Optional arguments: max_entries, tool, failed_only.", Schema(new Dictionary<string, object>
                {
                    ["max_entries"] = IntegerProperty("Maximum audit entries to return.", 1, 200),
                    ["tool"] = StringProperty("Optional tool name filter."),
                    ["failed_only"] = BooleanProperty("When true, return only failed entries."),
                }), "audit", "read-only", "Call get_audit_log with { \"max_entries\": 20, \"failed_only\": true }."), (arguments, scope, _) => Task.FromResult(GetAuditLog(arguments, scope))),
                Definition(Tool("get_audit_summary", "Return a compact MCP audit summary with recent counts, last failure, callers, and pending approvals. Optional argument: max_entries.", Schema(new Dictionary<string, object>
                {
                    ["max_entries"] = IntegerProperty("Maximum recent audit entries to summarize. Defaults to 50.", 1, 200),
                }), "audit", "read-only", "Call get_audit_summary with { \"max_entries\": 50 }."), (arguments, scope, _) => Task.FromResult(GetAuditSummary(arguments, scope))),
                Definition(Tool("get_last_tool_error", "Return the most recent failed MCP tool call, if one is recorded.", EmptySchema(), "audit", "read-only", "Call get_last_tool_error with no arguments."), (_, scope, _) => Task.FromResult(GetLastToolError(scope))),
                Definition(Tool("get_agent_task_events", "Query the latest saved Agent task event journal. Use only when the user asks to inspect Agent execution, tools, approvals, steering, replanning, or stop reasons.", Schema(new Dictionary<string, object>
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
                }), "audit", "read-only", "Call get_agent_task_events with { \"event_types\": [\"toolCompleted\", \"runStopped\"], \"max_events\": 50 }."), (arguments, scope, _) => Task.FromResult(GetAgentTaskEvents(arguments, scope))),
                Definition(Tool("get_runtime_environment_summary", "Return a safe summary of the MCP runtime environment, workspace roots, live context, logs, and flow availability.", EmptySchema(), "status", "read-only", "Call get_runtime_environment_summary before diagnostics."), (_, _, token) => GetRuntimeEnvironmentSummaryAsync(token)),
                Definition(Tool("get_diagnostic_bundle", "Return a size-limited redacted diagnostic bundle with server status, runtime, last error, recent log, live context, and flow summary.", Schema(new Dictionary<string, object>
                {
                    ["max_chars"] = IntegerProperty("Maximum characters to return. Defaults to 12000.", 1000, MaxDiagnosticBundleChars),
                }), "status", "read-only", "Call get_diagnostic_bundle with { \"max_chars\": 12000 } before reporting diagnostics."), (arguments, scope, token) => GetDiagnosticBundleAsync(arguments, scope, token)),
                Definition(Tool("get_live_context", "Return the current ColorVision live Copilot context snapshot, if one is published.", EmptySchema(), "context", "read-only", "Call get_live_context with no arguments."), (_, _, _) => Task.FromResult(GetLiveContext())),
                Definition(Tool("get_workspace_context", "Return the current ColorVision solution directory, active document, and allowed search roots.", EmptySchema(), "context", "read-only", "Call get_workspace_context to understand allowed roots."), (_, _, _) => Task.FromResult(GetWorkspaceContext())),
                Definition(Tool(CopilotSharedCapabilityCatalog.RecentLog.McpToolName, "Read recent ColorVision application log lines. Optional arguments: query, max_lines.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Optional case-insensitive filter text."),
                    ["max_lines"] = IntegerProperty("Maximum recent lines to inspect.", 1, 1000),
                }), "search", "read-only", "Call get_recent_log with { \"query\": \"error\", \"max_lines\": 200 }."), (arguments, _, token) => GetRecentLogAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.SearchDocs.McpToolName, "Search the published ColorVision documentation index. Required argument: query.", CopilotSharedCapabilityCatalog.SearchDocs.SharedInputSchema!.JsonSchema, "search", "read-only", "Call search_docs with { \"query\": \"plugin development\" }."), (arguments, _, token) => SearchDocsAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.SearchFiles.McpToolName, "Search one stable bounded page of file names and relative paths under allowed ColorVision workspace roots. Required argument: query. Optional: path, cursor.", CopilotSharedCapabilityCatalog.SearchFiles.SharedInputSchema!.JsonSchema, "search", "read-only", "Call search_files with { \"query\": \"DeviceCamera\", \"path\": \"ColorVision\" }; pass its next_cursor unchanged for another page."), (arguments, _, token) => Task.FromResult(SearchFiles(arguments, token))),
                Definition(Tool(CopilotSharedCapabilityCatalog.GrepText.McpToolName, "Search one stable bounded page of text matches under allowed ColorVision workspace roots using a literal case-insensitive query. The optional path may identify one file or directory. Required argument: query. Optional: path, cursor.", CopilotSharedCapabilityCatalog.GrepText.SharedInputSchema!.JsonSchema, "search", "read-only", "Call grep_text with { \"query\": \"FlowEngineManager\", \"path\": \"ColorVision/Copilot\" }; pass its next_cursor unchanged for another page."), (arguments, _, token) => Task.FromResult(GrepText(arguments, token))),
                Definition(Tool(CopilotSharedCapabilityCatalog.ReadAllowedFile.McpToolName, "Read a text file only if it is under an allowed ColorVision workspace root. Required argument: path. Optional: start_line, start_column, end_line.", Schema(new Dictionary<string, object>
                {
                    ["path"] = StringProperty("Absolute path, or a path relative to an allowed root."),
                    ["start_line"] = IntegerProperty("1-based start line.", 1, int.MaxValue),
                    ["start_column"] = IntegerProperty("1-based character column within start_line. Use the exact continuation cursor returned by a truncated read.", 1, int.MaxValue),
                    ["end_line"] = IntegerProperty("1-based end line.", 1, int.MaxValue),
                }, "path"), "file", "read-only", "Call read_allowed_file with { \"path\": \"README.md\", \"start_line\": 1, \"start_column\": 1, \"end_line\": 40 }."), (arguments, _, token) => ReadAllowedFileAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.ListAllowedDirectory.McpToolName, "List one stable bounded directory page only if it is under an allowed ColorVision workspace root. Optional arguments: path, cursor.", Schema(new Dictionary<string, object>
                {
                    ["path"] = StringProperty("Absolute path, or a path relative to an allowed root. If omitted, allowed roots are listed."),
                    ["cursor"] = StringProperty("Opaque next_cursor returned by the preceding page for the same directory. Never invent or modify it."),
                }), "file", "read-only", "Call list_allowed_directory with { \"path\": \"Engine\" }; pass its next_cursor unchanged to request another page."), (arguments, _, token) => Task.FromResult(ListAllowedDirectory(arguments, token))),
                Definition(Tool("get_active_template_context", "Return the active template editor context snapshot, if a template editor has published one.", EmptySchema(), "context", "read-only", "Call get_active_template_context before editing template JSON."), (_, _, _) => Task.FromResult(GetActiveTemplateContext())),
                Definition(Tool(CopilotSharedCapabilityCatalog.SavedTemplateContext.McpToolName, "Return a bounded redacted read-only snapshot of one already loaded saved ColorVision template. Required arguments: template_code, template_name.", CopilotSharedCapabilityCatalog.SavedTemplateContext.SharedInputSchema!.JsonSchema, "context", "read-only", "Call get_saved_template_context with { \"template_code\": \"SFR\", \"template_name\": \"Default\" } after the user references a saved template."), (arguments, _, _) => Task.FromResult(GetSavedTemplateContext(arguments))),
                Definition(Tool(CopilotSharedCapabilityCatalog.TemplateTypeContext.McpToolName, "Return bounded read-only metadata for one already loaded ColorVision template type, including saved names and parameter field schema but never parameter values. Required argument: template_code.", CopilotSharedCapabilityCatalog.TemplateTypeContext.SharedInputSchema!.JsonSchema, "context", "read-only", "Call get_template_type_context with { \"template_code\": \"SFR\" } after the user references a template type."), (arguments, _, _) => Task.FromResult(GetTemplateTypeContext(arguments))),
                Definition(Tool("get_flow_summary", "Return a read-only summary of the active ColorVision flow, nodes, and recent run state. This never starts or stops a flow.", EmptySchema(), "context", "read-only", "Call get_flow_summary to inspect the current flow."), (_, _, token) => GetFlowSummaryAsync(token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.FlowGraph.McpToolName, "Return the active ColorVision flow as a bounded structured graph with a revision, stable node ids, runtime type keys, ports, and edges. Use this instead of reading the binary .stn file.", CopilotSharedCapabilityCatalog.FlowGraph.SharedInputSchema!.JsonSchema, "context", "read-only", "Call get_flow_graph with { \"max_nodes\": 80 } before planning a flow edit."), (arguments, _, token) => GetFlowGraphAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.FlowNodeCatalog.McpToolName, "Search the node types loaded by the active Flow editor. Returns exact runtime type keys, categories, default device codes, and writable property schemas; do not guess a camera node type.", CopilotSharedCapabilityCatalog.FlowNodeCatalog.SharedInputSchema!.JsonSchema, "context", "read-only", "Call get_flow_node_catalog with { \"query\": \"相机\", \"max_results\": 30 }."), (arguments, _, token) => GetFlowNodeCatalogAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.PreviewFlowPatch.McpToolName, "Validate one bounded Flow graph change without editing: add_node, set_property, or connect. Use exact ids/type keys from the Flow graph and node catalog.", CopilotSharedCapabilityCatalog.PreviewFlowPatch.SharedInputSchema!.JsonSchema, "context", "read-only", "Call preview_flow_patch with one exact operation and the current graph revision."), (arguments, _, token) => PreviewFlowPatchAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.ApplyFlowPatch.McpToolName, "Apply one previously previewed add_node, set_property, or connect change after explicit approval. Rechecks the graph revision and never saves or runs the flow.", CopilotSharedCapabilityCatalog.ApplyFlowPatch.SharedInputSchema!.JsonSchema, "app-control", "confirmation-required", "Call apply_flow_patch with the exact operation and values used by preview_flow_patch, then wait for approval."), (arguments, scope, token) => ApplyFlowPatchAsync(arguments, scope, token)),
                Definition(Tool("diagnose_flow_failure", "Build a read-only failure diagnosis from the active flow, matched node, template context, and recent logs. This never runs a flow.", Schema(new Dictionary<string, object>
                {
                    ["node_id"] = StringProperty("Optional flow node id to focus the diagnosis."),
                    ["node_name"] = StringProperty("Optional flow node name or title to focus the diagnosis."),
                    ["query"] = StringProperty("Optional log query. Defaults to error."),
                    ["max_log_lines"] = IntegerProperty("Maximum recent log lines to inspect.", 1, 300),
                }), "context", "read-only", "Call diagnose_flow_failure with { \"node_name\": \"Camera\", \"query\": \"timeout\" } before suggesting template edits."), (arguments, _, token) => DiagnoseFlowFailureAsync(arguments, token)),
                Definition(Tool("open_panel", "Open a low-risk ColorVision panel. Optional argument: panel. Defaults to copilot.", Schema(new Dictionary<string, object>
                {
                    ["panel"] = StringProperty("Panel id or alias. Supported aliases: copilot, log, config, solution, template, device."),
                }), "app-control", "low-risk-action", "Call open_panel with { \"panel\": \"copilot\" }."), (arguments, _, token) => OpenPanelAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.ExecuteMenu.McpToolName, "Execute a visible main-window menu command by menu name or path. Required argument: query.", Schema(new Dictionary<string, object>
                {
                    ["query"] = StringProperty("Menu name or path to execute."),
                    ["dry_run"] = BooleanProperty("When true, resolve the menu and report risk without executing it."),
                }, "query"), "app-control", "confirmation-required", "Call execute_menu with { \"query\": \"View > Copilot\", \"dry_run\": true } first."), (arguments, scope, token) => ExecuteMenuAsync(arguments, scope, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.CreateFlow.McpToolName, "Create a new empty ColorVision flow after explicit user approval. Optional argument: name; a timestamped name is generated when omitted.", Schema(new Dictionary<string, object>
                {
                    ["name"] = StringProperty("Optional new flow name."),
                }), "app-control", "confirmation-required", "Call create_flow with { \"name\": \"CalibrationFlow\" }, then wait for approval in ColorVision."), (arguments, scope, token) => CreateFlowAsync(arguments, scope, token)),
                Definition(Tool("confirm_action", "Execute a previously approved confirmation-required action. Required arguments: action_id, tool_name, arguments_digest.", Schema(new Dictionary<string, object>
                {
                    ["action_id"] = StringProperty("Confirmable action id returned by a previous tool call."),
                    ["tool_name"] = StringProperty("Original tool name for the confirmable action."),
                    ["arguments_digest"] = StringProperty("Opaque SHA-256 arguments_digest returned with the action. Copy it exactly; it binds approval to the complete original arguments."),
                    ["arguments_summary"] = StringProperty("Optional redacted display summary. It is not used to authorize execution."),
                }, "action_id", "tool_name", "arguments_digest"), "app-control", "confirmation-required", "Call confirm_action only after the user approves the action in ColorVision, using the exact returned arguments_digest."), ConfirmActionAsync),
                Definition(Tool(CopilotSharedCapabilityCatalog.PreviewTemplatePatch.McpToolName, "Preview a proposed template JSON patch without saving it. Required arguments: template_identifier, proposed_changes. Optional: current_json.", Schema(new Dictionary<string, object>
                {
                    ["template_identifier"] = StringProperty("Template name, id, key, or editor identifier."),
                    ["proposed_changes"] = new Dictionary<string, object>
                    {
                        ["description"] = "Object containing proposed top-level JSON changes, or a JSON object string.",
                    },
                    ["current_json"] = StringProperty("Optional current template JSON. If omitted, the active template editor context is used."),
                }, "template_identifier", "proposed_changes"), "context", "read-only", "Call preview_template_patch with { \"template_identifier\": \"Default\", \"proposed_changes\": { \"Exposure\": 12 } }."), (arguments, _, _) => Task.FromResult(PreviewTemplatePatch(arguments))),
                Definition(Tool("suggest_template_patch", "Prepare a read-only template patch suggestion from the active template, diagnosis, and optional proposed changes. This never applies or saves.", Schema(new Dictionary<string, object>
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
                }), "context", "read-only", "Call suggest_template_patch with { \"intent\": \"Camera timeout\", \"node_name\": \"Camera\" }, then preview_template_patch."), (arguments, _, token) => SuggestTemplatePatchAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.ApplyTemplatePatch.McpToolName, "Create a user-confirmed action that applies a prior preview_template_patch result to the active template JSON editor. Required argument: preview_id.", Schema(new Dictionary<string, object>
                {
                    ["preview_id"] = StringProperty("Preview id returned by preview_template_patch."),
                }, "preview_id"), "app-control", "confirmation-required", "Call preview_template_patch first, then apply_template_patch with the returned preview_id."), (arguments, scope, token) => ApplyTemplatePatchAsync(arguments, scope, token)),
                Definition(Tool("preview_flow_action", "Preview a low-risk flow navigation/inspection action without running or stopping the flow. Required argument: action.", Schema(new Dictionary<string, object>
                {
                    ["action"] = StringProperty("Preview action: select_node, open_node_property, inspect_node_errors, explain_node, trace_recent_failure. start/stop/run requests are refused."),
                    ["node_id"] = StringProperty("Optional flow node id."),
                    ["node_name"] = StringProperty("Optional flow node name or title."),
                }, "action"), "context", "read-only", "Call preview_flow_action with { \"action\": \"inspect_node_errors\", \"node_name\": \"Camera\" }."), (arguments, _, token) => PreviewFlowActionAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.SetTheme.McpToolName, "Set the ColorVision UI theme. Required argument: theme. Allowed values include system, light, dark, pink, cyan.", Schema(new Dictionary<string, object>
                {
                    ["theme"] = StringProperty("Target theme name."),
                }, "theme"), "app-control", "low-risk-action", "Call set_theme with { \"theme\": \"dark\" }."), (arguments, _, token) => SetThemeAsync(arguments, token)),
                Definition(Tool(CopilotSharedCapabilityCatalog.SetLanguage.McpToolName, "Set the ColorVision UI language. Required argument: language. This may trigger the app's existing restart confirmation flow.", Schema(new Dictionary<string, object>
                {
                    ["language"] = StringProperty("Target language or culture name, for example en-US or zh-Hans."),
                }, "language"), "app-control", "confirmation-required", "Call set_language with { \"language\": \"en-US\" } and expect user confirmation."), (arguments, scope, token) => SetLanguageAsync(arguments, scope, token)),
            };
        }

        private static CopilotMcpToolDefinition Definition(
            CopilotMcpToolDescriptor descriptor,
            CopilotScopedMcpToolHandler handler) => new(descriptor, handler);
    }
}
