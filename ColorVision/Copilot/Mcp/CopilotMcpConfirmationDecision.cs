using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot.Mcp
{
    internal readonly record struct CopilotConfirmationApprovalResult(
        bool Success,
        bool ExecutedImmediately,
        string Message);

    internal static class CopilotMcpConfirmationDecision
    {
        public static string BuildApprovalPrompt(ConfirmableAction action)
        {
            var builder = new StringBuilder();
            builder.AppendLine("Approve this Copilot action?");
            builder.AppendLine();
            builder.AppendLine(action.Title);
            builder.AppendLine($"Tool: {action.ToolName}");
            builder.AppendLine($"Risk: {action.RiskLevel}");
            builder.AppendLine($"Expires: {action.ExpiresAtLabel}");

            if (!string.IsNullOrWhiteSpace(action.ArgumentsSummary))
                builder.AppendLine($"Params: {action.ArgumentsSummary}");

            builder.AppendLine();
            builder.AppendLine("Only approve if the requested operation matches your intent.");
            builder.Append(action.ExecuteOnApproval
                ? "This in-app action will execute immediately after approval."
                : "The requesting MCP client must still call confirm_action after approval.");
            return builder.ToString();
        }

        public static async Task<CopilotConfirmationApprovalResult> ApproveAsync(
            CopilotMcpConfirmationStore store,
            ConfirmableAction action,
            CancellationToken cancellationToken)
        {
            if (action.ResumesAgentOnApproval)
            {
                var approved = store.Approve(action.ActionId, out var message);
                return new CopilotConfirmationApprovalResult(
                    approved,
                    ExecutedImmediately: false,
                    approved
                        ? $"{action.ActionId}: {message} The agent will resume in the same session."
                        : $"{action.ActionId}: {message}");
            }

            if (action.ExecuteOnApproval)
            {
                var executionResult = await store.ApproveAndExecuteAsync(action.ActionId, cancellationToken);
                return new CopilotConfirmationApprovalResult(
                    executionResult.Success,
                    ExecutedImmediately: true,
                    executionResult.Success
                        ? $"{action.ActionId}: approved and executed."
                        : $"{action.ActionId}: {executionResult.Text}");
            }

            var success = store.Approve(action.ActionId, out var approvalMessage);
            return new CopilotConfirmationApprovalResult(
                success,
                ExecutedImmediately: false,
                $"{action.ActionId}: {approvalMessage}");
        }
    }
}
