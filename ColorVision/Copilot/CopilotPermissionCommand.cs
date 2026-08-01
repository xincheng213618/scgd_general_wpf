using System;

namespace ColorVision.Copilot
{
    internal enum CopilotPermissionCommandAction
    {
        OpenSelector,
        ShowStatus,
        UseConfirmProtectedActions,
        UseTemporaryAutoReview,
        Invalid,
    }

    internal static class CopilotPermissionCommand
    {
        public const string Usage = "用法：/permissions [status|ask|auto]。省略参数时打开权限模式菜单。";

        public static CopilotPermissionCommandAction Resolve(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return CopilotPermissionCommandAction.OpenSelector;

            return normalized.ToLowerInvariant() switch
            {
                "status" or "show" or "current" or "状态" =>
                    CopilotPermissionCommandAction.ShowStatus,
                "ask" or "confirm" or "default" or "按需" or "确认" =>
                    CopilotPermissionCommandAction.UseConfirmProtectedActions,
                "auto" or "automatic" or "自动" =>
                    CopilotPermissionCommandAction.UseTemporaryAutoReview,
                _ => CopilotPermissionCommandAction.Invalid,
            };
        }
    }
}
