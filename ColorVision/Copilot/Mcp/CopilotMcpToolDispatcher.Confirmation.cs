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
            var normalizedToolName = NormalizeToolName(toolName);
            if (CopilotSharedCapabilityCatalog.TryResolveMcpTool(
                    normalizedToolName,
                    out var definition)
                && definition.ApprovalMetadata.HasPresentation)
            {
                summary = definition.ApprovalMetadata.ReversibilitySummary;
                return definition.ApprovalMetadata.Reversibility;
            }

            summary = "所选命令未声明自动撤销能力；请在批准前核对影响。";
            return CopilotApprovalReversibility.Unknown;
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
    }
}
