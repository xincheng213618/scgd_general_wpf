using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal enum CopilotApprovalEligibilityReason
    {
        None,
        ActionNotFound,
        ActionExpired,
        ContextMismatch,
        ActionNotPending,
    }

    internal readonly record struct CopilotApprovalEligibility(
        bool CanReview,
        CopilotApprovalEligibilityReason Reason,
        string Message)
    {
        public static CopilotApprovalEligibility Allowed => new(
            CanReview: true,
            CopilotApprovalEligibilityReason.None,
            string.Empty);

        public static CopilotApprovalEligibility Denied(
            CopilotApprovalEligibilityReason reason,
            string message) => new(
                CanReview: false,
                reason,
                message ?? string.Empty);
    }

    internal interface ICopilotApprovalStore
    {
        event EventHandler? ActionsChanged;

        event EventHandler<ConfirmableActionChangedEventArgs>? ActionStatusChanged;

        int PendingCount { get; }

        IReadOnlyList<ConfirmableAction> GetPendingActionsForConversation(string? conversationId);

        CopilotApprovalEligibility ValidateForReview(
            string actionId,
            CopilotConfirmationReviewContext reviewContext);

        bool Approve(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            out string message);

        bool Reject(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            out string message);

        Task<CopilotMcpToolCallResult> ApproveAndExecuteAsync(
            string actionId,
            CopilotConfirmationReviewContext reviewContext,
            CancellationToken cancellationToken);
    }
}
