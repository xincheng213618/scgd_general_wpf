using System;

namespace ColorVision.Copilot
{
    internal enum CopilotComposerEnterAction
    {
        InsertLine,
        Submit,
    }

    internal static class CopilotMultilineComposerPreference
    {
        internal const string Usage =
            "用法：/multiline [on|off]。省略参数时切换；多行模式用 Enter 换行、Shift+Enter 或 Ctrl+Enter 发送。";

        internal static bool TryResolve(
            string? arguments,
            bool currentlyEnabled,
            out bool enabled)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                enabled = !currentlyEnabled;
                return true;
            }

            if (string.Equals(normalized, "on", StringComparison.OrdinalIgnoreCase))
            {
                enabled = true;
                return true;
            }

            if (string.Equals(normalized, "off", StringComparison.OrdinalIgnoreCase))
            {
                enabled = false;
                return true;
            }

            enabled = currentlyEnabled;
            return false;
        }

        internal static CopilotComposerEnterAction ResolveEnterAction(
            bool multilineEnabled,
            bool shiftPressed,
            bool controlPressed)
        {
            return multilineEnabled
                ? shiftPressed || controlPressed
                    ? CopilotComposerEnterAction.Submit
                    : CopilotComposerEnterAction.InsertLine
                : shiftPressed
                    ? CopilotComposerEnterAction.InsertLine
                    : CopilotComposerEnterAction.Submit;
        }
    }
}
