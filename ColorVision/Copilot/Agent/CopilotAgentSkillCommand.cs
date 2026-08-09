using System;

namespace ColorVision.Copilot
{
    internal enum CopilotAgentSkillCommandAction
    {
        Show,
        Reload,
        Disable,
        Enable,
        Invalid,
    }

    internal sealed record CopilotAgentSkillCommandRequest(
        CopilotAgentSkillCommandAction Action,
        int CatalogIndex = 0);

    internal static class CopilotAgentSkillCommand
    {
        public const string Usage = "用法：/skills [reload|off N|enable N]。省略参数时列出当前工作区 Skill 目录与本地使用证据；off/enable 按列表编号精确修改对应 SKILL.md 路径，从下一次请求开始生效。";

        public static CopilotAgentSkillCommandAction Resolve(string? arguments)
        {
            return Parse(arguments).Action;
        }

        public static CopilotAgentSkillCommandRequest Parse(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return new CopilotAgentSkillCommandRequest(CopilotAgentSkillCommandAction.Show);

            if (normalized.Equals("reload", StringComparison.OrdinalIgnoreCase)
                || normalized.Equals("refresh", StringComparison.OrdinalIgnoreCase)
                || normalized is "重载" or "刷新")
            {
                return new CopilotAgentSkillCommandRequest(CopilotAgentSkillCommandAction.Reload);
            }

            var parts = normalized.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 2
                || !int.TryParse(parts[1], out var catalogIndex)
                || catalogIndex <= 0)
            {
                return new CopilotAgentSkillCommandRequest(CopilotAgentSkillCommandAction.Invalid);
            }

            var action = parts[0].ToLowerInvariant() switch
            {
                "off" or "disable" or "关闭" or "禁用" => CopilotAgentSkillCommandAction.Disable,
                "on" or "enable" or "开启" or "启用" => CopilotAgentSkillCommandAction.Enable,
                _ => CopilotAgentSkillCommandAction.Invalid,
            };
            return new CopilotAgentSkillCommandRequest(action, catalogIndex);
        }
    }
}
