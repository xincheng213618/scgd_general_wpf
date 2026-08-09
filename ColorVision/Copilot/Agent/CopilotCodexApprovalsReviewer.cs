using System;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexApprovalsReviewer
    {
        Unspecified,
        User,
        AutoReview,
    }

    internal static class CopilotCodexApprovalsReviewerSelection
    {
        public static bool TryParse(string? value, out CopilotCodexApprovalsReviewer reviewer)
        {
            reviewer = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "user" => CopilotCodexApprovalsReviewer.User,
                "auto_review" => CopilotCodexApprovalsReviewer.AutoReview,
                _ => CopilotCodexApprovalsReviewer.Unspecified,
            };
            return reviewer != CopilotCodexApprovalsReviewer.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexApprovalsReviewer reviewer) => reviewer switch
        {
            CopilotCodexApprovalsReviewer.User => "user",
            CopilotCodexApprovalsReviewer.AutoReview => "auto_review",
            _ => "未配置（兼容 ColorVision 当前审批路由）",
        };

        public static string GetEffectiveLabel(CopilotCodexApprovalsReviewer reviewer) => reviewer switch
        {
            CopilotCodexApprovalsReviewer.User =>
                "符合条件的原生审批由 ColorVision 用户复核；不启动自动审查器。",
            CopilotCodexApprovalsReviewer.AutoReview =>
                "on-request 或启用 sandbox_approval 的 granular 审批由独立审查器复核；它不扩大沙箱或工具权限。",
            _ =>
                "未覆盖现有 ColorVision 行为：临时任务授权仍可对不支持临时完整访问的受保护工具启动自动复核。",
        };

        public static bool IsExplicitAutoReview(CopilotAgentRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);
            return request.CodexGuardianApprovalEnabled
                && request.CodexApprovalsReviewer == CopilotCodexApprovalsReviewer.AutoReview;
        }

        public static string GetModelInstruction(
            CopilotCodexApprovalsReviewer reviewer,
            bool guardianApprovalEnabled = true)
        {
            if (!guardianApprovalEnabled)
            {
                return "Codex features.guardian_approval=false is frozen for this submitted turn. Automatic approval review is unavailable; eligible native approval prompts must be reviewed by the ColorVision user. This does not change approval_policy, sandbox, or tool permissions.";
            }

            return reviewer switch
            {
                CopilotCodexApprovalsReviewer.User =>
                    "Codex approvals_reviewer=user is frozen for this submitted turn. Eligible native approval prompts are reviewed by the ColorVision user; never claim that an automatic reviewer approved them.",
                CopilotCodexApprovalsReviewer.AutoReview =>
                    "Codex approvals_reviewer=auto_review is frozen for this submitted turn. Eligible native approval prompts are routed to an independent reviewer instead of the user. A denial is not authorization to retry indirectly: use a materially safer path or explain that user direction is required.",
                _ => string.Empty,
            };
        }
    }
}
