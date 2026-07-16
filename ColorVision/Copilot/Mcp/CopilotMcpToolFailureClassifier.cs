using System;

namespace ColorVision.Copilot.Mcp
{
    internal static class CopilotMcpToolFailureClassifier
    {
        public static CopilotToolFailureKind Classify(string? errorCode)
        {
            var code = (errorCode ?? string.Empty).Trim().ToLowerInvariant();
            if (code.Length == 0)
                return CopilotToolFailureKind.Unspecified;

            if (code.Contains("cancel", StringComparison.Ordinal))
                return CopilotToolFailureKind.Cancelled;

            if (code.Contains("not_allowed", StringComparison.Ordinal)
                || code.Contains("rejected", StringComparison.Ordinal)
                || code.Contains("not_approved", StringComparison.Ordinal)
                || code is "framework_approval_only" or "action_requires_client_confirmation")
            {
                return CopilotToolFailureKind.Authorization;
            }

            if (code.Contains("conflict", StringComparison.Ordinal)
                || code.Contains("mismatch", StringComparison.Ordinal)
                || code.Contains("already_executed", StringComparison.Ordinal)
                || code is "flow_name_exists" or "action_pending")
            {
                return CopilotToolFailureKind.Conflict;
            }

            if (code.StartsWith("missing_", StringComparison.Ordinal)
                || code.StartsWith("invalid_", StringComparison.Ordinal)
                || code.StartsWith("unsupported_", StringComparison.Ordinal)
                || code.EndsWith("_required", StringComparison.Ordinal)
                || code is "panel_alias_not_supported" or "template_patch_not_applyable" or "flow_execution_not_supported" or "action_invalid_risk")
            {
                return CopilotToolFailureKind.Validation;
            }

            if (code.Contains("timeout", StringComparison.Ordinal)
                || code.Contains("temporary", StringComparison.Ordinal)
                || code.Contains("busy", StringComparison.Ordinal)
                || code.Contains("throttl", StringComparison.Ordinal))
            {
                return CopilotToolFailureKind.Transient;
            }

            if (code.Contains("not_found", StringComparison.Ordinal)
                || code.EndsWith("_expired", StringComparison.Ordinal)
                || code.EndsWith("_context_unavailable", StringComparison.Ordinal)
                || code is "no_allowed_roots" or "flow_unavailable" or "agent_task_events_unavailable" or "panel_not_registered")
            {
                return CopilotToolFailureKind.NotFound;
            }

            if (code.EndsWith("_failed", StringComparison.Ordinal)
                || code.EndsWith("_executor_missing", StringComparison.Ordinal)
                || code is "internal_error" or "application_unavailable" or "layout_unavailable")
            {
                return CopilotToolFailureKind.Internal;
            }

            return CopilotToolFailureKind.Unspecified;
        }
    }
}
