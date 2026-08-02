using System;
using System.Linq;

namespace ColorVision.Copilot.Mcp
{
    internal sealed partial class CopilotMcpConfirmationStore
    {
        private static string Sanitize(string? value)
        {
            var text = CopilotMcpAuditLogger.RedactText(value ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return text.Length <= 1000 ? text : text[..1000] + "...";
        }

        private static string NormalizeReviewDetails(string? value)
        {
            var text = value ?? string.Empty;
            if (text.Contains('\0'))
                throw new ArgumentException("Approval review details cannot contain NUL characters.", nameof(value));
            if (text.Length > MaximumReviewDetailsCharacters)
            {
                throw new ArgumentException(
                    $"Approval review details cannot exceed {MaximumReviewDetailsCharacters} characters.",
                    nameof(value));
            }

            return text;
        }

        private static CopilotConfirmationRequestContext NormalizeRequestContext(
            CopilotConfirmationRequestContext? context)
        {
            context ??= new CopilotConfirmationRequestContext();
            return new CopilotConfirmationRequestContext
            {
                Scope = context.ResolveExecutionScope(),
                SourceKind = Enum.IsDefined(context.SourceKind)
                    ? context.SourceKind
                    : CopilotApprovalSourceKind.Unknown,
                RequestSource = Sanitize(context.RequestSource),
                ConversationId = Sanitize(context.ConversationId),
                TaskId = Sanitize(context.TaskId),
                TaskLabel = Sanitize(context.TaskLabel),
                WorkspacePath = Sanitize(context.WorkspacePath),
                ImpactSummary = Sanitize(context.ImpactSummary),
                Reversibility = Enum.IsDefined(context.Reversibility)
                    ? context.Reversibility
                    : CopilotApprovalReversibility.Unknown,
                ReversibilitySummary = Sanitize(context.ReversibilitySummary),
            };
        }

        private static bool ValidateReviewContextNoLock(
            ConfirmableAction action,
            CopilotConfirmationReviewContext reviewContext,
            out string message)
        {
            var requestContext = action.RequestContext;
            var reviewConversationId = (reviewContext.ConversationId ?? string.Empty).Trim();
            var reviewTaskId = (reviewContext.TaskId ?? string.Empty).Trim();
            var reviewWorkspacePath = NormalizeWorkspaceForComparison(reviewContext.WorkspacePath);
            var actionWorkspacePath = NormalizeWorkspaceForComparison(requestContext.WorkspacePath);

            if (requestContext.SourceKind == CopilotApprovalSourceKind.InAppAgent
                && (string.IsNullOrWhiteSpace(requestContext.ConversationId)
                    || string.IsNullOrWhiteSpace(requestContext.TaskId)
                    || !string.Equals(requestContext.ConversationId, reviewConversationId, StringComparison.Ordinal)
                    || !string.Equals(requestContext.TaskId, reviewTaskId, StringComparison.Ordinal)))
            {
                message = "This approval belongs to a different or no-longer-active Copilot task.";
                return false;
            }

            if (requestContext.SourceKind == CopilotApprovalSourceKind.ColorVisionUi
                && !string.IsNullOrWhiteSpace(requestContext.ConversationId)
                && !string.Equals(requestContext.ConversationId, reviewConversationId, StringComparison.Ordinal))
            {
                message = "This approval belongs to a different Copilot conversation.";
                return false;
            }

            if (requestContext.SourceKind is CopilotApprovalSourceKind.InAppAgent
                    or CopilotApprovalSourceKind.ExternalMcp
                    or CopilotApprovalSourceKind.ColorVisionUi
                && !string.Equals(actionWorkspacePath, reviewWorkspacePath, StringComparison.OrdinalIgnoreCase))
            {
                message = "The active workspace changed after this approval request was created.";
                return false;
            }

            message = string.Empty;
            return true;
        }

        private static string NormalizeWorkspaceForComparison(string? workspacePath)
        {
            var normalized = (workspacePath ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(normalized))
                return string.Empty;

            try
            {
                return System.IO.Path.GetFullPath(normalized)
                    .TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
            catch
            {
                return normalized.TrimEnd(System.IO.Path.DirectorySeparatorChar, System.IO.Path.AltDirectorySeparatorChar);
            }
        }

        private static string FirstNonEmpty(params string?[] values) =>
            values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty;
    }
}
