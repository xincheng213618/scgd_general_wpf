namespace ColorVision.Copilot
{
    internal enum CopilotAgentSkillCommandAction
    {
        Show,
        Reload,
        Invalid,
    }

    internal static class CopilotAgentSkillCommand
    {
        public const string Usage = "用法：/skills [reload]。省略参数时列出当前工作区 Skill 目录与本地使用证据；reload 强制从磁盘重扫目录。";

        public static CopilotAgentSkillCommandAction Resolve(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
                return CopilotAgentSkillCommandAction.Show;

            return normalized.ToLowerInvariant() switch
            {
                "reload" or "refresh" or "重载" or "刷新" => CopilotAgentSkillCommandAction.Reload,
                _ => CopilotAgentSkillCommandAction.Invalid,
            };
        }
    }
}
