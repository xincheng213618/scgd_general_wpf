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
                SharedDefinition(CopilotSharedCapabilityCatalog.RecentLog, (arguments, _, token) => GetRecentLogAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.SearchDocs, (arguments, _, token) => SearchDocsAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.SearchFiles, (arguments, _, token) => Task.FromResult(SearchFiles(arguments, token))),
                SharedDefinition(CopilotSharedCapabilityCatalog.GrepText, (arguments, _, token) => Task.FromResult(GrepText(arguments, token))),
                SharedDefinition(CopilotSharedCapabilityCatalog.ReadAllowedFile, (arguments, _, token) => ReadAllowedFileAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.ListAllowedDirectory, (arguments, _, token) => Task.FromResult(ListAllowedDirectory(arguments, token))),
                Definition(Tool("get_active_template_context", "Return the active template editor context snapshot, if a template editor has published one.", EmptySchema(), "context", "read-only", "Call get_active_template_context before editing template JSON."), (_, _, _) => Task.FromResult(GetActiveTemplateContext())),
                SharedDefinition(CopilotSharedCapabilityCatalog.SavedTemplateContext, (arguments, _, _) => Task.FromResult(GetSavedTemplateContext(arguments))),
                SharedDefinition(CopilotSharedCapabilityCatalog.TemplateTypeContext, (arguments, _, _) => Task.FromResult(GetTemplateTypeContext(arguments))),
                Definition(Tool("get_flow_summary", "Return a read-only summary of the active ColorVision flow, nodes, and recent run state. This never starts or stops a flow.", EmptySchema(), "context", "read-only", "Call get_flow_summary to inspect the current flow."), (_, _, token) => GetFlowSummaryAsync(token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.FlowGraph, (arguments, _, token) => GetFlowGraphAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.FlowNodeCatalog, (arguments, _, token) => GetFlowNodeCatalogAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.PreviewFlowPatch, (arguments, _, token) => PreviewFlowPatchAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.ApplyFlowPatch, (arguments, scope, token) => ApplyFlowPatchAsync(arguments, scope, token)),
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
                SharedDefinition(CopilotSharedCapabilityCatalog.ExecuteMenu, (arguments, scope, token) => ExecuteMenuAsync(arguments, scope, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.CreateFlow, (arguments, scope, token) => CreateFlowAsync(arguments, scope, token)),
                Definition(Tool("confirm_action", "Execute a previously approved confirmation-required action. Required arguments: action_id, tool_name, arguments_digest.", Schema(new Dictionary<string, object>
                {
                    ["action_id"] = StringProperty("Confirmable action id returned by a previous tool call."),
                    ["tool_name"] = StringProperty("Original tool name for the confirmable action."),
                    ["arguments_digest"] = StringProperty("Opaque SHA-256 arguments_digest returned with the action. Copy it exactly; it binds approval to the complete original arguments."),
                    ["arguments_summary"] = StringProperty("Optional redacted display summary. It is not used to authorize execution."),
                }, "action_id", "tool_name", "arguments_digest"), "app-control", "confirmation-required", "Call confirm_action only after the user approves the action in ColorVision, using the exact returned arguments_digest."), ConfirmActionAsync),
                SharedDefinition(CopilotSharedCapabilityCatalog.PreviewTemplatePatch, (arguments, _, _) => Task.FromResult(PreviewTemplatePatch(arguments))),
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
                SharedDefinition(CopilotSharedCapabilityCatalog.ApplyTemplatePatch, (arguments, scope, token) => ApplyTemplatePatchAsync(arguments, scope, token)),
                Definition(Tool("preview_flow_action", "Preview a low-risk flow navigation/inspection action without running or stopping the flow. Required argument: action.", Schema(new Dictionary<string, object>
                {
                    ["action"] = StringProperty("Preview action: select_node, open_node_property, inspect_node_errors, explain_node, trace_recent_failure. start/stop/run requests are refused."),
                    ["node_id"] = StringProperty("Optional flow node id."),
                    ["node_name"] = StringProperty("Optional flow node name or title."),
                }, "action"), "context", "read-only", "Call preview_flow_action with { \"action\": \"inspect_node_errors\", \"node_name\": \"Camera\" }."), (arguments, _, token) => PreviewFlowActionAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.SetTheme, (arguments, _, token) => SetThemeAsync(arguments, token)),
                SharedDefinition(CopilotSharedCapabilityCatalog.SetLanguage, (arguments, scope, token) => SetLanguageAsync(arguments, scope, token)),
            };
        }

        private static CopilotMcpToolDefinition Definition(
            CopilotMcpToolDescriptor descriptor,
            CopilotScopedMcpToolHandler handler) => new(descriptor, handler);

        private static CopilotMcpToolDefinition SharedDefinition(
            CopilotSharedCapabilityDefinition capability,
            CopilotScopedMcpToolHandler handler) =>
            Definition(
                Tool(
                    capability.McpToolName,
                    capability.McpDescription,
                    capability.McpInputSchema.JsonSchema,
                    capability.McpMetadata.Category,
                    capability.McpRiskLevel,
                    capability.McpMetadata.UsageHint,
                    capability.AgentCapability.Idempotency,
                    capability.McpMetadata.DestructiveHint,
                    capability.McpMetadata.OpenWorldHint),
                handler);
    }
}
