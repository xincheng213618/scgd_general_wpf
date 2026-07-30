using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public enum CopilotConversationGoalState
    {
        Active,
        Paused,
    }

    public sealed class CopilotConversationGoal
    {
        public const int MaximumObjectiveCharacters = 4_000;
        public const int CurrentStrategyVersion = 1;

        public int StrategyVersion { get; init; } = CurrentStrategyVersion;

        public string Id { get; init; } = string.Empty;

        public string Objective { get; init; } = string.Empty;

        public CopilotConversationGoalState State { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }

        [JsonIgnore]
        public bool IsActive => State == CopilotConversationGoalState.Active;

        public bool IsStructurallyValid()
        {
            return StrategyVersion == CurrentStrategyVersion
                && Guid.TryParseExact(Id, "N", out _)
                && IsValidObjective(Objective)
                && Enum.IsDefined(State)
                && CreatedAtUtc != default
                && UpdatedAtUtc >= CreatedAtUtc;
        }

        internal static CopilotConversationGoal Create(string objective, DateTimeOffset now)
        {
            if (!TryNormalizeObjective(objective, out var normalized, out var errorMessage))
                throw new ArgumentException(errorMessage, nameof(objective));

            return new CopilotConversationGoal
            {
                Id = Guid.NewGuid().ToString("N"),
                Objective = normalized,
                State = CopilotConversationGoalState.Active,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }

        internal CopilotConversationGoal WithObjective(string objective, DateTimeOffset now)
        {
            if (!TryNormalizeObjective(objective, out var normalized, out var errorMessage))
                throw new ArgumentException(errorMessage, nameof(objective));

            return new CopilotConversationGoal
            {
                StrategyVersion = StrategyVersion,
                Id = Id,
                Objective = normalized,
                State = CopilotConversationGoalState.Active,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = now < CreatedAtUtc ? CreatedAtUtc : now,
            };
        }

        internal CopilotConversationGoal WithState(CopilotConversationGoalState state, DateTimeOffset now)
        {
            return new CopilotConversationGoal
            {
                StrategyVersion = StrategyVersion,
                Id = Id,
                Objective = Objective,
                State = state,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = now < CreatedAtUtc ? CreatedAtUtc : now,
            };
        }

        internal CopilotConversationGoal CopyForBranch(DateTimeOffset now)
        {
            return new CopilotConversationGoal
            {
                Id = Guid.NewGuid().ToString("N"),
                Objective = Objective,
                State = State,
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
            };
        }

        internal static bool TryNormalizeObjective(
            string? objective,
            out string normalized,
            out string errorMessage)
        {
            normalized = (objective ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                errorMessage = "目标不能为空。用法：/goal <目标>。";
                return false;
            }
            if (normalized.Length > MaximumObjectiveCharacters)
            {
                errorMessage = $"目标最多支持 {MaximumObjectiveCharacters:N0} 个字符；请把更长的细节放入文件并在目标中引用。";
                return false;
            }
            if (normalized.Contains('\0'))
            {
                errorMessage = "目标包含无效控制字符。";
                return false;
            }

            errorMessage = string.Empty;
            return true;
        }

        private static bool IsValidObjective(string? objective)
        {
            return !string.IsNullOrWhiteSpace(objective)
                && string.Equals(objective, objective.Trim(), StringComparison.Ordinal)
                && objective.Length <= MaximumObjectiveCharacters
                && !objective.Contains('\0');
        }
    }

    internal sealed record CopilotConversationGoalCommandResult(
        CopilotConversationGoal? Goal,
        bool Changed,
        string Message);

    internal static class CopilotConversationGoalCommand
    {
        public static CopilotConversationGoalCommandResult Execute(
            CopilotConversationGoal? current,
            string? arguments,
            DateTimeOffset now)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            if (normalized.Length == 0)
            {
                return new CopilotConversationGoalCommandResult(
                    current,
                    false,
                    current == null
                        ? "当前会话没有持续目标。用 /goal <目标> 设置一个。"
                        : FormatStatus(current));
            }

            if (string.Equals(normalized, "clear", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotConversationGoalCommandResult(
                    null,
                    current != null,
                    current == null ? "当前会话没有可清除的持续目标。" : "当前会话的持续目标已清除。");
            }

            if (string.Equals(normalized, "pause", StringComparison.OrdinalIgnoreCase))
            {
                if (current == null)
                    return MissingGoal(current, "暂停");
                if (current.State == CopilotConversationGoalState.Paused)
                    return new CopilotConversationGoalCommandResult(current, false, FormatStatus(current));

                var paused = current.WithState(CopilotConversationGoalState.Paused, now);
                return new CopilotConversationGoalCommandResult(
                    paused,
                    true,
                    "持续目标已暂停；后续新任务不会注入该目标。\n" + paused.Objective);
            }

            if (string.Equals(normalized, "resume", StringComparison.OrdinalIgnoreCase))
            {
                if (current == null)
                    return MissingGoal(current, "恢复");
                if (current.State == CopilotConversationGoalState.Active)
                    return new CopilotConversationGoalCommandResult(current, false, FormatStatus(current));

                var resumed = current.WithState(CopilotConversationGoalState.Active, now);
                return new CopilotConversationGoalCommandResult(
                    resumed,
                    true,
                    "持续目标已恢复；后续新 Agent 任务会重新绑定该目标。\n" + resumed.Objective);
            }

            if (string.Equals(normalized, "edit", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotConversationGoalCommandResult(
                    current,
                    false,
                    "用法：/goal edit <新目标>。");
            }

            if (normalized.StartsWith("edit ", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("edit\t", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("edit\r", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("edit\n", StringComparison.OrdinalIgnoreCase))
            {
                if (current == null)
                    return MissingGoal(current, "编辑");

                var objective = normalized[4..].Trim();
                if (!CopilotConversationGoal.TryNormalizeObjective(objective, out _, out var editError))
                    return new CopilotConversationGoalCommandResult(current, false, editError);

                var edited = current.WithObjective(objective, now);
                return new CopilotConversationGoalCommandResult(
                    edited,
                    true,
                    "持续目标已更新并恢复为活动状态。\n" + edited.Objective);
            }

            if (!CopilotConversationGoal.TryNormalizeObjective(normalized, out _, out var errorMessage))
                return new CopilotConversationGoalCommandResult(current, false, errorMessage);

            var created = CopilotConversationGoal.Create(normalized, now);
            return new CopilotConversationGoalCommandResult(
                created,
                true,
                (current == null ? "已设置持续目标。" : "已替换持续目标。")
                + "\n"
                + created.Objective
                + "\n该目标约束后续任务的完成判定，但不授权写入、工具调用或外部副作用。");
        }

        private static CopilotConversationGoalCommandResult MissingGoal(
            CopilotConversationGoal? current,
            string action)
        {
            return new CopilotConversationGoalCommandResult(
                current,
                false,
                $"当前会话没有可{action}的持续目标。先用 /goal <目标> 设置一个。");
        }

        private static string FormatStatus(CopilotConversationGoal goal)
        {
            var state = goal.IsActive ? "活动" : "已暂停";
            return $"持续目标 · {state}\n{goal.Objective}\n"
                + "管理命令：/goal edit <新目标>、/goal pause、/goal resume、/goal clear。";
        }
    }
}
