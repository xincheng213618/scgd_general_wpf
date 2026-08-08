using System;

namespace ColorVision.Copilot
{
    internal enum CopilotCodexApprovalPolicyMode
    {
        Unspecified,
        Untrusted,
        OnRequest,
        Never,
        Granular,
    }

    internal sealed record CopilotCodexApprovalPolicy
    {
        public static CopilotCodexApprovalPolicy Unspecified { get; } = new();

        public CopilotCodexApprovalPolicyMode Mode { get; init; }

        public bool SandboxApproval { get; init; }

        public bool Rules { get; init; }

        public bool McpElicitations { get; init; }

        public bool RequestPermissions { get; init; }

        public bool SkillApproval { get; init; }

        public static CopilotCodexApprovalPolicy CreateScalar(CopilotCodexApprovalPolicyMode mode)
        {
            if (mode is not (CopilotCodexApprovalPolicyMode.Untrusted
                or CopilotCodexApprovalPolicyMode.OnRequest
                or CopilotCodexApprovalPolicyMode.Never))
            {
                throw new ArgumentOutOfRangeException(nameof(mode));
            }

            return new CopilotCodexApprovalPolicy { Mode = mode };
        }

        public static CopilotCodexApprovalPolicy CreateGranular(
            bool sandboxApproval,
            bool rules,
            bool mcpElicitations,
            bool requestPermissions,
            bool skillApproval) => new()
            {
                Mode = CopilotCodexApprovalPolicyMode.Granular,
                SandboxApproval = sandboxApproval,
                Rules = rules,
                McpElicitations = mcpElicitations,
                RequestPermissions = requestPermissions,
                SkillApproval = skillApproval,
            };
    }

    internal static class CopilotCodexApprovalPolicySelection
    {
        public static bool TryParseScalar(
            string? value,
            out CopilotCodexApprovalPolicy policy)
        {
            var mode = (value ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "untrusted" => CopilotCodexApprovalPolicyMode.Untrusted,
                "on-request" => CopilotCodexApprovalPolicyMode.OnRequest,
                "never" => CopilotCodexApprovalPolicyMode.Never,
                _ => CopilotCodexApprovalPolicyMode.Unspecified,
            };
            policy = mode == CopilotCodexApprovalPolicyMode.Unspecified
                ? CopilotCodexApprovalPolicy.Unspecified
                : CopilotCodexApprovalPolicy.CreateScalar(mode);
            return mode != CopilotCodexApprovalPolicyMode.Unspecified;
        }

        public static string GetConfigToken(CopilotCodexApprovalPolicy? policy)
        {
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return policy.Mode switch
            {
                CopilotCodexApprovalPolicyMode.Untrusted => "untrusted",
                CopilotCodexApprovalPolicyMode.OnRequest => "on-request",
                CopilotCodexApprovalPolicyMode.Never => "never",
                CopilotCodexApprovalPolicyMode.Granular =>
                    $"granular(sandbox_approval={Format(policy.SandboxApproval)}, rules={Format(policy.Rules)}, mcp_elicitations={Format(policy.McpElicitations)}, request_permissions={Format(policy.RequestPermissions)}, skill_approval={Format(policy.SkillApproval)})",
                _ => "未配置",
            };
        }

        public static string GetEffectiveLabel(CopilotCodexApprovalPolicy? policy)
        {
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return policy.Mode switch
            {
                CopilotCodexApprovalPolicyMode.Untrusted =>
                    "只读工具可直接运行；所有写工具和原生受保护工具均升级为逐调用审批",
                CopilotCodexApprovalPolicyMode.OnRequest =>
                    "保留 ColorVision 原生逐调用审批、临时授权与自动审查边界",
                CopilotCodexApprovalPolicyMode.Never =>
                    "不创建新审批提示；需要新审批的调用会自动拒绝，现有沙箱与本机权限不会扩大",
                CopilotCodexApprovalPolicyMode.Granular =>
                    $"按工具能力类别执行 granular 审批；交互类别：{GetGranularCategoryList(policy, enabled: true)}；自动拒绝：{GetGranularCategoryList(policy, enabled: false)}",
                _ => "未配置；保留 ColorVision 原生审批策略",
            };
        }

        public static bool AllowsApprovalPrompt(
            CopilotCodexApprovalPolicy? policy,
            CopilotApprovalPromptCategory category)
        {
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return policy.Mode switch
            {
                CopilotCodexApprovalPolicyMode.Never => false,
                CopilotCodexApprovalPolicyMode.Granular => IsGranularCategoryEnabled(policy, category),
                _ => true,
            };
        }

        public static bool AllowsAutomaticReview(
            CopilotCodexApprovalPolicy? policy,
            CopilotApprovalPromptCategory category)
        {
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return policy.Mode switch
            {
                CopilotCodexApprovalPolicyMode.Unspecified => true,
                CopilotCodexApprovalPolicyMode.OnRequest => true,
                CopilotCodexApprovalPolicyMode.Granular => IsGranularCategoryEnabled(policy, category),
                _ => false,
            };
        }

        public static bool RequiresNativeApproval(
            CopilotCodexApprovalPolicy? policy,
            ICopilotTool tool)
        {
            ArgumentNullException.ThrowIfNull(tool);
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return tool.Capability.RequiresNativeApproval
                || policy.Mode == CopilotCodexApprovalPolicyMode.Untrusted
                    && tool.Capability.Access == CopilotToolAccess.Write;
        }

        public static string GetApprovalDenialReason(
            CopilotCodexApprovalPolicy? policy,
            CopilotApprovalPromptCategory category)
        {
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return policy.Mode == CopilotCodexApprovalPolicyMode.Never
                ? "Codex approval_policy=never disables new approval prompts for this submitted turn; the protected tool call was not authorized."
                : $"Codex granular approval_policy disables {GetConfigCategoryName(category)} prompts for this submitted turn; the protected tool call was not authorized.";
        }

        public static string GetModelInstruction(CopilotCodexApprovalPolicy? policy)
        {
            policy ??= CopilotCodexApprovalPolicy.Unspecified;
            return policy.Mode switch
            {
                CopilotCodexApprovalPolicyMode.Untrusted =>
                    "Codex approval_policy=untrusted is frozen for this submitted turn. Read-only tools may run normally, but every write-capable or otherwise protected tool call must complete the exact ColorVision approval path before execution; never treat the current request itself as approval.",
                CopilotCodexApprovalPolicyMode.OnRequest =>
                    "Codex approval_policy=on-request is frozen for this submitted turn. Request approval only when the ColorVision tool boundary requires it, and never treat the current request itself as approval.",
                CopilotCodexApprovalPolicyMode.Never =>
                    "Codex approval_policy=never is frozen for this submitted turn. Never request or claim a new approval; if a protected tool is unavailable, continue with operations already permitted by the sandbox and ColorVision native policy.",
                CopilotCodexApprovalPolicyMode.Granular =>
                    $"Codex granular approval_policy is frozen for this submitted turn. The host routes exact protected calls by approval category; enabled categories are {GetGranularCategoryList(policy, enabled: true)}, and disabled categories are automatically denied ({GetGranularCategoryList(policy, enabled: false)}). Never treat the current request itself as approval or claim that a denied category ran.",
                _ => string.Empty,
            };
        }

        private static bool IsGranularCategoryEnabled(
            CopilotCodexApprovalPolicy policy,
            CopilotApprovalPromptCategory category)
        {
            return category switch
            {
                CopilotApprovalPromptCategory.SandboxApproval => policy.SandboxApproval,
                CopilotApprovalPromptCategory.Rules => policy.Rules,
                CopilotApprovalPromptCategory.McpElicitations => policy.McpElicitations,
                CopilotApprovalPromptCategory.RequestPermissions => policy.RequestPermissions,
                CopilotApprovalPromptCategory.SkillApproval => policy.SkillApproval,
                _ => false,
            };
        }

        private static string GetGranularCategoryList(
            CopilotCodexApprovalPolicy policy,
            bool enabled)
        {
            var categories = new[]
            {
                CopilotApprovalPromptCategory.SandboxApproval,
                CopilotApprovalPromptCategory.Rules,
                CopilotApprovalPromptCategory.McpElicitations,
                CopilotApprovalPromptCategory.RequestPermissions,
                CopilotApprovalPromptCategory.SkillApproval,
            };
            var selected = Array.FindAll(
                categories,
                category => IsGranularCategoryEnabled(policy, category) == enabled);
            return selected.Length == 0
                ? "none"
                : string.Join(", ", Array.ConvertAll(selected, GetConfigCategoryName));
        }

        private static string GetConfigCategoryName(CopilotApprovalPromptCategory category)
        {
            return category switch
            {
                CopilotApprovalPromptCategory.SandboxApproval => "sandbox_approval",
                CopilotApprovalPromptCategory.Rules => "rules",
                CopilotApprovalPromptCategory.McpElicitations => "mcp_elicitations",
                CopilotApprovalPromptCategory.RequestPermissions => "request_permissions",
                CopilotApprovalPromptCategory.SkillApproval => "skill_approval",
                _ => "unknown",
            };
        }

        private static string Format(bool value) => value ? "true" : "false";
    }
}
