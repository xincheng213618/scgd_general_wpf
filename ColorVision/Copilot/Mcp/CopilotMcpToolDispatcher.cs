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

        private CopilotMcpToolCallResult CreateConfirmableActionResult(
            string title,
            string description,
            string toolName,
            IReadOnlyDictionary<string, JsonElement>? arguments,
            string previewText,
            Func<CancellationToken, Task<CopilotMcpToolCallResult>> executor,
            bool executeOnApproval = false,
            CopilotExecutionScope? executionScope = null)
        {
            executionScope ??= CopilotExecutionScope.ForInProcess("in-process-external");
            if (ContainsSensitiveArgumentValues(arguments))
                return CopilotMcpToolCallResult.Fail("sensitive_arguments_not_allowed", "ColorVision MCP refuses to create confirmable actions that contain token, api key, password, authorization, or bearer secret values.");

            var argumentsSummary = BuildArgumentSummary(arguments);
            var exactArgumentsBinding = BuildExactArgumentBinding(arguments);
            var normalizedToolName = NormalizeToolName(toolName);
            var requestContext = CreateConfirmationRequestContext(
                normalizedToolName,
                title,
                previewText,
                executionScope);
            var action = CopilotMcpConfirmationStore.Instance.Create(
                title,
                description,
                "confirmation-required",
                normalizedToolName,
                argumentsSummary,
                executor,
                executeOnApproval,
                requestContext: requestContext,
                exactArgumentsBinding: exactArgumentsBinding);

            var builder = new StringBuilder();
            builder.AppendLine("confirmation_required");
            builder.AppendLine("execution_status: pending_user_confirmation");
            builder.AppendLine($"action_id: {action.ActionId}");
            builder.AppendLine($"title: {action.Title}");
            builder.AppendLine($"description: {action.Description}");
            builder.AppendLine($"risk_level: {action.RiskLevel}");
            builder.AppendLine($"tool_name: {action.ToolName}");
            builder.AppendLine($"arguments_summary: {action.ArgumentsSummary}");
            builder.AppendLine($"arguments_digest: {action.ArgumentsDigest}");
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

        private CopilotConfirmationRequestContext CreateConfirmationRequestContext(
            string toolName,
            string title,
            string impactSummary,
            CopilotExecutionScope executionScope)
        {
            var inAppAgent = IsInAppAgent(executionScope);
            var reversibility = ResolveApprovalReversibility(toolName, out var reversibilitySummary);
            return new CopilotConfirmationRequestContext
            {
                SourceKind = inAppAgent
                    ? CopilotApprovalSourceKind.InAppAgent
                    : CopilotApprovalSourceKind.ExternalMcp,
                RequestSource = executionScope.CallerIdentity,
                Scope = executionScope,
                TaskLabel = inAppAgent ? "当前 Copilot 任务" : title,
                WorkspacePath = executionScope.WorkspacePath,
                ImpactSummary = impactSummary,
                Reversibility = reversibility,
                ReversibilitySummary = reversibilitySummary,
            };
        }

        private static CopilotApprovalReversibility ResolveApprovalReversibility(
            string toolName,
            out string summary)
        {
            switch (NormalizeToolName(toolName))
            {
                case "apply_template_patch":
                    summary = "修改只应用到当前编辑器；保存前可通过重新加载模板手动恢复。";
                    return CopilotApprovalReversibility.ManualOnly;
                case "apply_flow_patch":
                    summary = "修改不会自动保存或运行流程；如需恢复，必须在编辑器中手动撤销。";
                    return CopilotApprovalReversibility.ManualOnly;
                case "set_language":
                    summary = "可在设置中再次切换语言，但本操作没有自动回滚步骤。";
                    return CopilotApprovalReversibility.ManualOnly;
                case "create_flow":
                    summary = "新建流程不会自动删除；如需恢复，必须手动关闭或移除。";
                    return CopilotApprovalReversibility.ManualOnly;
                default:
                    summary = "所选命令未声明自动撤销能力；请在批准前核对影响。";
                    return CopilotApprovalReversibility.Unknown;
            }
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

        private static bool IsInAppAgent(CopilotExecutionScope executionScope)
        {
            return executionScope.SourceKind == CopilotExecutionSourceKind.InAppAgent;
        }

        private CopilotExecutionScope EnsureWorkspaceScope(CopilotExecutionScope? executionScope)
        {
            executionScope ??= CopilotExecutionScope.Empty;
            if (executionScope.WorkspacePath.Length > 0)
                return executionScope;

            return executionScope.WithWorkspace(GetWorkspaceSnapshot().SolutionDirectoryPath);
        }

        private static bool AuditEntryMatchesScope(
            CopilotMcpAuditEntry entry,
            CopilotExecutionScope executionScope)
        {
            if (executionScope.SourceKind == CopilotExecutionSourceKind.ExternalMcp)
            {
                return executionScope.SessionIdentity.Length > 0
                    && string.Equals(
                        entry.SessionIdentity,
                        executionScope.SessionIdentity,
                        StringComparison.Ordinal);
            }

            if (executionScope.SourceKind == CopilotExecutionSourceKind.InAppAgent
                && executionScope.RunId.Length > 0)
            {
                return string.Equals(entry.RunId, executionScope.RunId, StringComparison.Ordinal);
            }

            return true;
        }

        private static IReadOnlyList<ConfirmableAction> GetPendingActionsForScope(
            CopilotExecutionScope executionScope)
        {
            var actions = CopilotMcpConfirmationStore.Instance.GetPendingActions();
            if (executionScope.SourceKind is not (
                    CopilotExecutionSourceKind.ExternalMcp
                    or CopilotExecutionSourceKind.InAppAgent))
            {
                return actions;
            }

            return actions
                .Where(action => action.RequestContext.ResolveExecutionScope()
                    .MatchesAuthorizationScope(executionScope))
                .ToArray();
        }

        private static bool WorkspaceScopeMatches(string expectedPath, string currentPath)
        {
            var expected = NormalizeWorkspaceScope(expectedPath);
            var current = NormalizeWorkspaceScope(currentPath);
            return string.Equals(expected, current, StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeWorkspaceScope(string? workspacePath)
        {
            var normalized = (workspacePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;
            try
            {
                return Path.GetFullPath(normalized)
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalized.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            }
        }

        private static bool IsInAppAgentFrameworkApproved(CopilotExecutionScope executionScope)
        {
            return executionScope.SourceKind == CopilotExecutionSourceKind.InAppAgent
                && executionScope.AuthorizationChannel == CopilotExecutionAuthorizationChannel.AgentFrameworkApproved;
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
                string.IsNullOrWhiteSpace(result.ErrorMessage) ? text : result.ErrorMessage,
                result.FailureKind);
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

            return payload.ToJsonString(StructuredJsonOptions);
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
                builder.AppendLine($"  Approval event: {!string.IsNullOrWhiteSpace(entry.ActionId)}");
                builder.AppendLine($"  Arguments: {EmptyLabel(entry.ArgumentSummary)}");
                builder.AppendLine($"  Success: {entry.Success}");
                builder.AppendLine($"  Duration ms: {entry.DurationMs}");
                builder.AppendLine($"  Error: {EmptyLabel(entry.ErrorMessage)}");
                builder.AppendLine($"  Caller/source: {EmptyLabel(entry.CallerSource)}");
                builder.AppendLine($"  Scope id: {EmptyLabel(entry.ScopeId)}");
                builder.AppendLine($"  Trace id: {EmptyLabel(entry.TraceId)}");
                builder.AppendLine($"  Run id: {EmptyLabel(entry.RunId)}");
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
            var approvalEvent = string.IsNullOrWhiteSpace(entry.ActionId)
                ? string.Empty
                : "; approval_event=true";
            return $"{entry.TimestampUtc:O}; tool={EmptyLabel(entry.ToolName)}; result={result}; duration_ms={entry.DurationMs}; caller={EmptyLabel(entry.CallerSource)}; scope={EmptyLabel(entry.ScopeId)}{approvalEvent}{error}";
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

        private static string BuildExactArgumentBinding(IReadOnlyDictionary<string, JsonElement>? arguments)
        {
            using var stream = new MemoryStream();
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                if (arguments != null)
                {
                    foreach (var pair in arguments.OrderBy(item => item.Key, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(pair.Key);
                        WriteCanonicalJsonElement(writer, pair.Value);
                    }
                }
                writer.WriteEndObject();
            }

            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void WriteCanonicalJsonElement(Utf8JsonWriter writer, JsonElement value)
        {
            switch (value.ValueKind)
            {
                case JsonValueKind.Object:
                    writer.WriteStartObject();
                    foreach (var property in value.EnumerateObject().OrderBy(item => item.Name, StringComparer.Ordinal))
                    {
                        writer.WritePropertyName(property.Name);
                        WriteCanonicalJsonElement(writer, property.Value);
                    }
                    writer.WriteEndObject();
                    return;
                case JsonValueKind.Array:
                    writer.WriteStartArray();
                    foreach (var item in value.EnumerateArray())
                        WriteCanonicalJsonElement(writer, item);
                    writer.WriteEndArray();
                    return;
                case JsonValueKind.Undefined:
                    writer.WriteNullValue();
                    return;
                default:
                    value.WriteTo(writer);
                    return;
            }
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
