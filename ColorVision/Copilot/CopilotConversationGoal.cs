using Newtonsoft.Json;
using System;

namespace ColorVision.Copilot
{
    public enum CopilotConversationGoalState
    {
        Active,
        Paused,
        Achieved,
    }

    public sealed class CopilotConversationGoal
    {
        public const int MaximumObjectiveCharacters = 4_000;
        public const int MaximumReasonCharacters = 1_000;
        public const int CurrentStrategyVersion = 1;

        public int StrategyVersion { get; init; } = CurrentStrategyVersion;

        public string Id { get; init; } = string.Empty;

        public string Objective { get; init; } = string.Empty;

        public CopilotConversationGoalState State { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public int TurnCount { get; init; }

        public int EvaluationCount { get; init; }

        public long TokensUsed { get; init; }

        public int ConsecutiveContinuationCount { get; init; }

        public string LastEvaluationReason { get; init; } = string.Empty;

        public DateTimeOffset? LastEvaluatedAtUtc { get; init; }

        [JsonIgnore]
        public bool IsActive => State == CopilotConversationGoalState.Active;

        [JsonIgnore]
        public bool IsAchieved => State == CopilotConversationGoalState.Achieved;

        public bool IsStructurallyValid()
        {
            return StrategyVersion == CurrentStrategyVersion
                && Guid.TryParseExact(Id, "N", out _)
                && IsValidObjective(Objective)
                && Enum.IsDefined(State)
                && CreatedAtUtc != default
                && UpdatedAtUtc >= CreatedAtUtc
                && TurnCount >= 0
                && EvaluationCount is >= 0 and <= int.MaxValue
                && EvaluationCount <= TurnCount
                && TokensUsed >= 0
                && ConsecutiveContinuationCount is >= 0 and <= int.MaxValue
                && ConsecutiveContinuationCount <= EvaluationCount
                && LastEvaluationReason != null
                && LastEvaluationReason.Length <= MaximumReasonCharacters
                && !LastEvaluationReason.Contains('\0')
                && (!LastEvaluatedAtUtc.HasValue
                    || (LastEvaluatedAtUtc.Value >= CreatedAtUtc
                        && LastEvaluatedAtUtc.Value <= UpdatedAtUtc));
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

        internal CopilotConversationGoal WithState(
            CopilotConversationGoalState state,
            DateTimeOffset now,
            string? reason = null)
        {
            var effectiveNow = GetMonotonicUpdateTime(now);
            return new CopilotConversationGoal
            {
                StrategyVersion = StrategyVersion,
                Id = Id,
                Objective = Objective,
                State = state,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = effectiveNow,
                TurnCount = TurnCount,
                EvaluationCount = EvaluationCount,
                TokensUsed = TokensUsed,
                ConsecutiveContinuationCount = state == CopilotConversationGoalState.Active
                    ? 0
                    : ConsecutiveContinuationCount,
                LastEvaluationReason = reason == null ? LastEvaluationReason : NormalizeReason(reason),
                LastEvaluatedAtUtc = LastEvaluatedAtUtc,
            };
        }

        internal CopilotConversationGoal WithTurnOutcome(
            CopilotConversationGoalState state,
            CopilotTokenUsage usage,
            bool evaluated,
            bool continued,
            string? reason,
            DateTimeOffset now)
        {
            var normalizedReason = NormalizeReason(reason);
            var effectiveNow = GetMonotonicUpdateTime(now);
            return new CopilotConversationGoal
            {
                StrategyVersion = StrategyVersion,
                Id = Id,
                Objective = Objective,
                State = state,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = effectiveNow,
                TurnCount = Increment(TurnCount),
                EvaluationCount = evaluated ? Increment(EvaluationCount) : EvaluationCount,
                TokensUsed = AddTokens(TokensUsed, usage.EffectiveTotalTokens),
                ConsecutiveContinuationCount = continued
                    ? Increment(ConsecutiveContinuationCount)
                    : 0,
                LastEvaluationReason = normalizedReason,
                LastEvaluatedAtUtc = evaluated ? effectiveNow : LastEvaluatedAtUtc,
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
                LastEvaluationReason = State == CopilotConversationGoalState.Achieved
                    ? LastEvaluationReason
                    : string.Empty,
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

        internal static string NormalizeReason(string? reason)
        {
            var normalized = (reason ?? string.Empty)
                .Replace("\r\n", "\n", StringComparison.Ordinal)
                .Replace('\r', '\n')
                .Replace('\0', ' ')
                .Trim();
            return normalized.Length <= MaximumReasonCharacters
                ? normalized
                : normalized[..MaximumReasonCharacters].TrimEnd();
        }

        private static int Increment(int value) => value == int.MaxValue ? int.MaxValue : value + 1;

        private DateTimeOffset GetMonotonicUpdateTime(DateTimeOffset now) =>
            now < UpdatedAtUtc ? UpdatedAtUtc : now;

        private static long AddTokens(long current, int additional)
        {
            var boundedAdditional = Math.Max(0, additional);
            return current > long.MaxValue - boundedAdditional
                ? long.MaxValue
                : current + boundedAdditional;
        }
    }

    internal sealed record CopilotConversationGoalCommandResult(
        CopilotConversationGoal? Goal,
        bool Changed,
        string Message,
        bool StartsWork = false);

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
                    "持续目标已恢复；即将启动新的 Agent 轮次。\n" + resumed.Objective,
                    StartsWork: true);
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

                var edited = CopilotConversationGoal.Create(objective, now);
                return new CopilotConversationGoalCommandResult(
                    edited,
                    true,
                    "持续目标已更新并恢复为活动状态；即将启动首轮 Agent 工作。\n" + edited.Objective,
                    StartsWork: true);
            }

            if (!CopilotConversationGoal.TryNormalizeObjective(
                    normalized,
                    out var normalizedObjective,
                    out var errorMessage))
                return new CopilotConversationGoalCommandResult(current, false, errorMessage);

            if (current?.IsStructurallyValid() == true
                && !current.IsAchieved
                && string.Equals(current.Objective, normalizedObjective, StringComparison.Ordinal))
            {
                var continued = current.WithState(CopilotConversationGoalState.Active, now);
                return new CopilotConversationGoalCommandResult(
                    continued,
                    true,
                    "已继续当前持续目标并保留既有轮次、评估和 Token 统计；即将启动新的 Agent 轮次。"
                    + "\n"
                    + continued.Objective,
                    StartsWork: true);
            }

            var created = CopilotConversationGoal.Create(normalizedObjective, now);
            return new CopilotConversationGoalCommandResult(
                created,
                true,
                (current == null ? "已设置持续目标并即将启动首轮 Agent 工作。" : "已替换持续目标并即将启动首轮 Agent 工作。")
                + "\n"
                + created.Objective
                + "\n该目标约束后续任务的完成判定，但不授权写入、工具调用或外部副作用。",
                StartsWork: true);
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
            var state = goal.State switch
            {
                CopilotConversationGoalState.Active => "活动",
                CopilotConversationGoalState.Achieved => "已达成",
                _ => "已暂停",
            };
            var progress = goal.TurnCount == 0
                ? "尚未完成首轮"
                : $"{goal.TurnCount:N0} 轮 · {goal.EvaluationCount:N0} 次独立评估 · {goal.TokensUsed:N0} Token";
            var latest = string.IsNullOrWhiteSpace(goal.LastEvaluationReason)
                ? string.Empty
                : "\n最近判断：" + goal.LastEvaluationReason;
            return $"持续目标 · {state} · {progress}\n{goal.Objective}{latest}\n"
                + "管理命令：/goal edit <新目标>、/goal pause、/goal resume、/goal clear。";
        }
    }

    internal static class CopilotConversationGoalRecovery
    {
        public static bool PauseActiveGoalsAfterProcessRestart(
            CopilotChatState state,
            DateTimeOffset now)
        {
            ArgumentNullException.ThrowIfNull(state);
            var changed = false;
            foreach (var conversation in state.Conversations ?? [])
            {
                if (conversation?.Goal?.IsActive != true)
                    continue;

                conversation.Goal = conversation.Goal.WithState(
                    CopilotConversationGoalState.Paused,
                    now,
                    "应用进程已重新启动；先前的自动续作不再运行，目标已安全暂停。使用 /goal resume 重新开始。");
                changed = true;
            }
            return changed;
        }
    }
}
