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
            builder.AppendLine("是否批准这个受保护操作？");
            builder.AppendLine();
            builder.AppendLine(action.Title);
            builder.AppendLine($"来源：{action.RequesterLabel}");
            builder.AppendLine($"任务：{action.TaskScopeLabel}");
            builder.AppendLine($"工作区：{action.WorkspaceLabel}");
            builder.AppendLine($"影响：{action.ImpactLabel}");
            builder.AppendLine($"撤销：{action.ReversibilityLabel}");
            builder.AppendLine($"时限：{action.ReviewDeadlineLabel}");

            builder.AppendLine();
            builder.AppendLine("请仅在来源、任务、工作区和影响都符合你的意图时批准。");
            builder.Append(action.ExecuteOnApproval
                ? "批准后，这个应用内操作会立即执行。"
                : action.ResumesAgentOnApproval
                    ? "批准后，Agent 会在同一任务中继续执行。"
                    : "批准后，外部 MCP 调用方仍需提交 confirm_action 才会执行。");
            builder.AppendLine();
            builder.AppendLine();
            builder.AppendLine("技术详情");
            builder.AppendLine($"工具：{action.ToolName}");
            builder.AppendLine($"操作 ID：{action.ActionId}");
            if (!string.IsNullOrWhiteSpace(action.ArgumentsSummary))
                builder.AppendLine($"参数：{action.ArgumentsSummary}");
            builder.Append($"参数指纹（SHA-256）：{action.ArgumentsDigest}");
            return builder.ToString();
        }

        public static async Task<CopilotConfirmationApprovalResult> ApproveAsync(
            ICopilotApprovalStore store,
            ConfirmableAction action,
            CopilotConfirmationReviewContext reviewContext,
            CancellationToken cancellationToken)
        {
            if (action.ResumesAgentOnApproval)
            {
                var approved = store.Approve(action.ActionId, reviewContext, out var message);
                return new CopilotConfirmationApprovalResult(
                    approved,
                    ExecutedImmediately: false,
                    approved
                        ? $"{action.ActionId}：已批准，Agent 将在同一任务中继续执行。"
                        : $"{action.ActionId}: {message}");
            }

            if (action.ExecuteOnApproval)
            {
                var executionResult = await store.ApproveAndExecuteAsync(
                    action.ActionId,
                    reviewContext,
                    cancellationToken);
                return new CopilotConfirmationApprovalResult(
                    executionResult.Success,
                    ExecutedImmediately: true,
                    executionResult.Success
                        ? $"{action.ActionId}：已批准并执行。"
                        : $"{action.ActionId}: {executionResult.Text}");
            }

            var success = store.Approve(action.ActionId, reviewContext, out var approvalMessage);
            return new CopilotConfirmationApprovalResult(
                success,
                ExecutedImmediately: false,
                $"{action.ActionId}: {approvalMessage}");
        }
    }
}
