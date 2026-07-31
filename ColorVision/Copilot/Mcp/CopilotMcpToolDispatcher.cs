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
    internal sealed partial class CopilotMcpToolDispatcher :
        ICopilotApplicationCapabilityInvoker,
        ICopilotScopedApplicationCapabilityInvoker,
        ICopilotApprovedApplicationCapabilityInvoker
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

        private static CopilotMcpToolCallResult GetCapabilityCatalog()
        {
            var snapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            return CopilotMcpToolCallResult.Ok(JsonSerializer.Serialize(snapshot, StructuredJsonOptions));
        }

        private CopilotMcpToolCallResult GetAgentTaskEvents(
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            int defaultMaxEvents = 50)
        {
            if (executionScope.SourceKind == CopilotExecutionSourceKind.ExternalMcp
                && string.IsNullOrWhiteSpace(executionScope.ConversationId))
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_scope_required",
                    "This MCP session is not bound to a Copilot conversation and cannot read the process-wide Agent task journal.");
            }

            var context = SafeInvoke(_environment.TaskEventJournalProvider);
            if (context?.IsStructurallyValid() != true)
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_unavailable",
                    "No saved Agent task event journal is available for the selected conversation.");
            }
            if (executionScope.SourceKind == CopilotExecutionSourceKind.InAppAgent
                && !string.IsNullOrWhiteSpace(executionScope.ConversationId)
                && !string.Equals(
                    executionScope.ConversationId,
                    context.ConversationId,
                    StringComparison.Ordinal))
            {
                return CopilotMcpToolCallResult.Fail(
                    "agent_task_events_scope_mismatch",
                    "The saved Agent task event journal belongs to a different conversation.");
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

        internal Task<CopilotMcpToolCallResult> CallAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CancellationToken cancellationToken)
        {
            return CallCoreAsync(
                toolName,
                arguments,
                CopilotExecutionScope.ForInProcess("in-process-external"),
                cancellationToken);
        }

        internal Task<CopilotMcpToolCallResult> CallExternalAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string callerSource,
            CancellationToken cancellationToken)
        {
            return CallCoreAsync(
                toolName,
                arguments,
                CopilotExecutionScope.ForExternalMcpSession(callerSource, callerSource),
                cancellationToken);
        }

        internal Task<CopilotMcpToolCallResult> CallExternalAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            return CallCoreAsync(toolName, arguments, executionScope, cancellationToken);
        }

        private async Task<CopilotMcpToolCallResult> CallCoreAsync(
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            executionScope = EnsureWorkspaceScope(executionScope);
            var normalizedToolName = NormalizeToolName(toolName);
            var stopwatch = Stopwatch.StartNew();
            CopilotMcpAuditLogger.ToolCallStarted(normalizedToolName, BuildArgumentSummary(arguments), executionScope);

            try
            {
                var result = await _router.DispatchAsync(normalizedToolName, arguments, executionScope, cancellationToken);

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

        public async Task<CopilotApplicationCapabilityCallResult> InvokeAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotApplicationCapabilityCaller caller,
            CancellationToken cancellationToken)
        {
            if (caller != CopilotApplicationCapabilityCaller.InAppAgent)
                throw new ArgumentOutOfRangeException(nameof(caller));
            var result = await CallCoreAsync(
                capabilityName,
                arguments,
                CopilotExecutionScope.ForAgentCaller(GetWorkspaceSnapshot().SolutionDirectoryPath),
                cancellationToken);
            return ToApplicationCapabilityResult(result);
        }

        async Task<CopilotApplicationCapabilityCallResult> ICopilotApprovedApplicationCapabilityInvoker.InvokeApprovedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(executionScope);
            var currentWorkspacePath = GetWorkspaceSnapshot().SolutionDirectoryPath;
            if (string.IsNullOrWhiteSpace(request.ConversationId)
                || string.IsNullOrWhiteSpace(request.TaskId)
                || !WorkspaceScopeMatches(request.WorkspacePath, currentWorkspacePath))
            {
                return new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_capability_scope_mismatch",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = "The approved application capability no longer matches the active Copilot task or workspace.",
                };
            }

            if (!executionScope.HasToolCallBinding
                || !executionScope.MatchesAuthorizationScope(request.RuntimeExecutionScope))
            {
                return new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_capability_binding_mismatch",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = "The approved application capability is not bound to the active provider tool call and execution scope.",
                };
            }

            if (!CopilotCapabilityRevisionAuthorization.TryValidate(
                executionScope,
                _environment.CapabilityRevisionProvider,
                out var rejectionReason))
            {
                return new CopilotApplicationCapabilityCallResult
                {
                    Success = false,
                    ErrorCode = "approved_capability_revision_changed",
                    FailureKind = CopilotToolFailureKind.Authorization,
                    Content = rejectionReason,
                };
            }

            var approvedExecutionScope = executionScope
                .WithWorkspace(currentWorkspacePath)
                .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.AgentFrameworkApproved);
            var result = await CallCoreAsync(capabilityName, arguments, approvedExecutionScope, cancellationToken);
            return ToApplicationCapabilityResult(result);
        }

        async Task<CopilotApplicationCapabilityCallResult> ICopilotScopedApplicationCapabilityInvoker.InvokeScopedAsync(
            string capabilityName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            CopilotAgentRequest request,
            CopilotExecutionScope executionScope,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(executionScope);
            var scopedExecutionScope = executionScope
                .WithWorkspace(GetWorkspaceSnapshot().SolutionDirectoryPath)
                .WithAuthorizationChannel(CopilotExecutionAuthorizationChannel.Standard);
            var result = await CallCoreAsync(capabilityName, arguments, scopedExecutionScope, cancellationToken);
            return ToApplicationCapabilityResult(result);
        }

        private static CopilotApplicationCapabilityCallResult ToApplicationCapabilityResult(
            CopilotMcpToolCallResult result)
        {
            return new CopilotApplicationCapabilityCallResult
            {
                Success = result.Success,
                Content = result.Text,
                ErrorCode = result.ErrorCode,
                FailureKind = result.FailureKind,
                Approval = result.RequiresApproval && !string.IsNullOrWhiteSpace(result.ApprovalActionId)
                    ? new CopilotToolApprovalInfo
                    {
                        ActionId = result.ApprovalActionId,
                        Title = result.ApprovalTitle,
                        RiskLevel = result.ApprovalRiskLevel,
                        ExpiresAtUtc = result.ApprovalExpiresAtUtc,
                        ExecuteOnApproval = result.ExecuteOnApproval,
                    }
                    : null,
            };
        }

        private CopilotMcpToolRouter CreateRouter()
        {
            return new CopilotMcpToolRouter()
                .RegisterScoped("get_server_status", (_, scope, _) => Task.FromResult(GetServerStatus(scope)))
                .RegisterScoped("get_enabled_tools", (_, _, _) => Task.FromResult(GetEnabledTools()))
                .RegisterScoped("get_audit_log", (arguments, scope, _) => Task.FromResult(GetAuditLog(arguments, scope)))
                .RegisterScoped("get_audit_summary", (arguments, scope, _) => Task.FromResult(GetAuditSummary(arguments, scope)))
                .RegisterScoped("get_last_tool_error", (_, scope, _) => Task.FromResult(GetLastToolError(scope)))
                .RegisterScoped("get_agent_task_events", (arguments, scope, _) => Task.FromResult(GetAgentTaskEvents(arguments, scope)))
                .RegisterScoped("get_runtime_environment_summary", (_, _, token) => GetRuntimeEnvironmentSummaryAsync(token))
                .RegisterScoped("get_diagnostic_bundle", (arguments, scope, token) => GetDiagnosticBundleAsync(arguments, scope, token))
                .RegisterScoped("get_live_context", (_, _, _) => Task.FromResult(GetLiveContext()))
                .RegisterScoped("get_workspace_context", (_, _, _) => Task.FromResult(GetWorkspaceContext()))
                .RegisterScoped("get_recent_log", (arguments, _, token) => GetRecentLogAsync(arguments, token))
                .RegisterScoped("search_docs", (arguments, _, token) => SearchDocsAsync(arguments, token))
                .RegisterScoped("search_files", (arguments, _, token) => Task.FromResult(SearchFiles(arguments, token)))
                .RegisterScoped("grep_text", (arguments, _, token) => Task.FromResult(GrepText(arguments, token)))
                .RegisterScoped("read_allowed_file", (arguments, _, token) => ReadAllowedFileAsync(arguments, token))
                .RegisterScoped("list_allowed_directory", (arguments, _, token) => Task.FromResult(ListAllowedDirectory(arguments, token)))
                .RegisterScoped("get_active_template_context", (_, _, _) => Task.FromResult(GetActiveTemplateContext()))
                .RegisterScoped("get_saved_template_context", (arguments, _, _) => Task.FromResult(GetSavedTemplateContext(arguments)))
                .RegisterScoped("get_template_type_context", (arguments, _, _) => Task.FromResult(GetTemplateTypeContext(arguments)))
                .RegisterScoped("get_flow_summary", (_, _, token) => GetFlowSummaryAsync(token))
                .RegisterScoped("get_flow_graph", (arguments, _, token) => GetFlowGraphAsync(arguments, token))
                .RegisterScoped("get_flow_node_catalog", (arguments, _, token) => GetFlowNodeCatalogAsync(arguments, token))
                .RegisterScoped("preview_flow_patch", (arguments, _, token) => PreviewFlowPatchAsync(arguments, token))
                .RegisterScoped("apply_flow_patch", (arguments, scope, token) => ApplyFlowPatchAsync(arguments, scope, token))
                .RegisterScoped("diagnose_flow_failure", (arguments, _, token) => DiagnoseFlowFailureAsync(arguments, token))
                .RegisterScoped("open_panel", (arguments, _, token) => OpenPanelAsync(arguments, token))
                .RegisterScoped("execute_menu", (arguments, scope, token) => ExecuteMenuAsync(arguments, scope, token))
                .RegisterScoped("create_flow", (arguments, scope, token) => CreateFlowAsync(arguments, scope, token))
                .RegisterScoped("confirm_action", ConfirmActionAsync)
                .RegisterScoped("preview_template_patch", (arguments, _, _) => Task.FromResult(PreviewTemplatePatch(arguments)))
                .RegisterScoped("suggest_template_patch", (arguments, _, token) => SuggestTemplatePatchAsync(arguments, token))
                .RegisterScoped("apply_template_patch", (arguments, scope, token) => ApplyTemplatePatchAsync(arguments, scope, token))
                .RegisterScoped("preview_flow_action", (arguments, _, token) => PreviewFlowActionAsync(arguments, token))
                .RegisterScoped("set_theme", (arguments, _, token) => SetThemeAsync(arguments, token))
                .RegisterScoped("set_language", (arguments, scope, token) => SetLanguageAsync(arguments, scope, token));
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



    }
}
