using System;

namespace ColorVision.Copilot
{
    public enum CopilotFollowUpBehavior
    {
        Steer,
        Queue,
    }

    internal static class CopilotFollowUpPreference
    {
        internal const string Usage =
            "用法：/follow-up [steer|queue]。steer 会把 Enter 作为当前运行调整；queue 会把 Enter 作为下一轮请求；Tab 始终执行另一种动作。";

        internal static bool TryResolve(
            string? arguments,
            CopilotFollowUpBehavior current,
            out CopilotFollowUpBehavior behavior)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                behavior = Normalize(current);
                return true;
            }

            if (string.Equals(normalized, "steer", StringComparison.OrdinalIgnoreCase))
            {
                behavior = CopilotFollowUpBehavior.Steer;
                return true;
            }

            if (string.Equals(normalized, "queue", StringComparison.OrdinalIgnoreCase))
            {
                behavior = CopilotFollowUpBehavior.Queue;
                return true;
            }

            behavior = Normalize(current);
            return false;
        }

        internal static CopilotFollowUpBehavior Alternate(CopilotFollowUpBehavior behavior) =>
            Normalize(behavior) == CopilotFollowUpBehavior.Queue
                ? CopilotFollowUpBehavior.Steer
                : CopilotFollowUpBehavior.Queue;

        internal static CopilotFollowUpBehavior Normalize(CopilotFollowUpBehavior behavior) =>
            Enum.IsDefined(behavior) ? behavior : CopilotFollowUpBehavior.Steer;
    }
}
