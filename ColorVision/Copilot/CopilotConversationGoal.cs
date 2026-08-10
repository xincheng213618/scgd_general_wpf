using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace ColorVision.Copilot
{
    public enum CopilotConversationGoalState
    {
        Active = 0,
        Paused = 1,
        Achieved = 2,
        Blocked = 3,
        UsageLimited = 4,
        BudgetLimited = 5,
    }

    internal static class CopilotConversationGoalStateText
    {
        public static string Format(CopilotConversationGoalState state) => state switch
        {
            CopilotConversationGoalState.Active => "活动",
            CopilotConversationGoalState.Achieved => "已达成",
            CopilotConversationGoalState.Blocked => "受阻",
            CopilotConversationGoalState.UsageLimited => "用量受限",
            CopilotConversationGoalState.BudgetLimited => "预算受限",
            _ => "已暂停",
        };

        public static string FormatEnglish(CopilotConversationGoalState state) => state switch
        {
            CopilotConversationGoalState.Active => "Active",
            CopilotConversationGoalState.Achieved => "Achieved",
            CopilotConversationGoalState.Blocked => "Blocked",
            CopilotConversationGoalState.UsageLimited => "Usage limited",
            CopilotConversationGoalState.BudgetLimited => "Budget limited",
            _ => "Paused",
        };

        public static string FormatDisplayLabel(CopilotConversationGoalState state) => state switch
        {
            CopilotConversationGoalState.Active => "持续目标",
            CopilotConversationGoalState.Achieved => "目标已达成",
            CopilotConversationGoalState.Blocked => "目标受阻",
            CopilotConversationGoalState.UsageLimited => "目标用量受限",
            CopilotConversationGoalState.BudgetLimited => "目标预算受限",
            _ => "目标已暂停",
        };

        public static string FormatDescription(CopilotConversationGoalState state) => state switch
        {
            CopilotConversationGoalState.Active => "活动目标会绑定到后续新 Agent 任务，并在每轮后独立评估。",
            CopilotConversationGoalState.Achieved => "独立完成评估已确认该目标达成。",
            CopilotConversationGoalState.Blocked => "该目标正在等待阻塞条件解除，不会自动启动新任务。",
            CopilotConversationGoalState.UsageLimited => "该目标因模型用量限制停止，不会自动启动新任务。",
            CopilotConversationGoalState.BudgetLimited => "该目标因 Token 预算限制停止，不会自动启动新任务。",
            _ => "该目标已暂停，不会自动启动新任务。",
        };
    }

    internal static class CopilotConversationGoalUsageText
    {
        public static string FormatElapsed(long totalSeconds)
        {
            var (days, hours, minutes, seconds) = SplitElapsed(totalSeconds);
            if (days > 0)
                return hours > 0 ? $"{days:N0} 天 {hours} 小时" : $"{days:N0} 天";
            if (hours > 0)
                return minutes > 0 ? $"{hours:N0} 小时 {minutes} 分钟" : $"{hours:N0} 小时";
            if (minutes > 0)
                return seconds > 0 ? $"{minutes:N0} 分钟 {seconds} 秒" : $"{minutes:N0} 分钟";
            return $"{seconds:N0} 秒";
        }

        public static string FormatElapsedEnglish(long totalSeconds)
        {
            var (days, hours, minutes, seconds) = SplitElapsed(totalSeconds);
            if (days > 0)
                return hours > 0 ? $"{days:N0}d {hours}h" : $"{days:N0}d";
            if (hours > 0)
                return minutes > 0 ? $"{hours:N0}h {minutes}m" : $"{hours:N0}h";
            if (minutes > 0)
                return seconds > 0 ? $"{minutes:N0}m {seconds}s" : $"{minutes:N0}m";
            return $"{seconds:N0}s";
        }

        private static (long Days, long Hours, long Minutes, long Seconds) SplitElapsed(long totalSeconds)
        {
            var remaining = Math.Max(0, totalSeconds);
            var days = remaining / 86_400;
            remaining %= 86_400;
            var hours = remaining / 3_600;
            remaining %= 3_600;
            var minutes = remaining / 60;
            var seconds = remaining % 60;
            return (days, hours, minutes, seconds);
        }
    }

    internal static class CopilotConversationGoalScoreText
    {
        public static string Format(CopilotConversationGoal goal)
        {
            ArgumentNullException.ThrowIfNull(goal);
            if (!goal.LastProgressScore.HasValue && !goal.BestProgressScore.HasValue)
                return string.Empty;
            if (!goal.LastProgressScore.HasValue)
                return $"最近评分不可用 · 最佳 {goal.BestProgressScore}/100";

            var latest = goal.LastProgressScore.Value;
            var best = goal.BestProgressScore ?? latest;
            return latest == best
                ? $"评分 {latest}/100 · 当前最佳"
                : $"评分 {latest}/100 · 最佳 {best}/100 · 较最佳 -{best - latest}";
        }

        public static string FormatEnglish(CopilotConversationGoal goal)
        {
            ArgumentNullException.ThrowIfNull(goal);
            if (!goal.LastProgressScore.HasValue && !goal.BestProgressScore.HasValue)
                return string.Empty;
            if (!goal.LastProgressScore.HasValue)
                return $"latest score unavailable, best {goal.BestProgressScore}/100";

            var latest = goal.LastProgressScore.Value;
            var best = goal.BestProgressScore ?? latest;
            return latest == best
                ? $"score {latest}/100 (current best)"
                : $"score {latest}/100, best {best}/100, {best - latest} below best";
        }

        public static string FormatSuffix(CopilotConversationGoal goal)
        {
            var text = Format(goal);
            return text.Length == 0 ? string.Empty : " · " + text;
        }

        public static string FormatLine(CopilotConversationGoal goal)
        {
            var text = Format(goal);
            return text.Length == 0 ? string.Empty : Environment.NewLine + text;
        }
    }

    public sealed record CopilotConversationGoalProgressReport
    {
        public string Checkpoint { get; init; } = string.Empty;

        public string Verified { get; init; } = string.Empty;

        public string Remaining { get; init; } = string.Empty;

        public string NextStep { get; init; } = string.Empty;

        internal bool IsStructurallyValid(bool achieved) =>
            TryCreate(Checkpoint, Verified, Remaining, NextStep, achieved, out var normalized)
            && this == normalized;

        internal static bool TryCreate(
            string? checkpoint,
            string? verified,
            string? remaining,
            string? nextStep,
            bool achieved,
            out CopilotConversationGoalProgressReport report)
        {
            report = new CopilotConversationGoalProgressReport();
            if (!TryNormalizeField(checkpoint, allowEmpty: false, out var normalizedCheckpoint)
                || !TryNormalizeField(verified, allowEmpty: false, out var normalizedVerified)
                || !TryNormalizeField(remaining, allowEmpty: achieved, out var normalizedRemaining)
                || !TryNormalizeField(nextStep, allowEmpty: achieved, out var normalizedNextStep)
                || (achieved && (normalizedRemaining.Length > 0 || normalizedNextStep.Length > 0)))
            {
                return false;
            }

            report = new CopilotConversationGoalProgressReport
            {
                Checkpoint = normalizedCheckpoint,
                Verified = normalizedVerified,
                Remaining = normalizedRemaining,
                NextStep = normalizedNextStep,
            };
            return true;
        }

        private static bool TryNormalizeField(
            string? value,
            bool allowEmpty,
            out string normalized)
        {
            normalized = string.Empty;
            if (value == null
                || value.Length > CopilotConversationGoal.MaximumProgressReportFieldCharacters
                || value.Contains('\0'))
            {
                return false;
            }

            var builder = new StringBuilder(value.Length);
            var previousWasSpace = false;
            foreach (var character in value)
            {
                var normalizedCharacter = char.IsWhiteSpace(character) || char.IsControl(character)
                    ? ' '
                    : character;
                if (normalizedCharacter == ' ')
                {
                    if (previousWasSpace)
                        continue;
                    previousWasSpace = true;
                }
                else
                {
                    previousWasSpace = false;
                }
                builder.Append(normalizedCharacter);
            }

            normalized = builder.ToString().Trim();
            return allowEmpty || normalized.Length > 0;
        }
    }

    internal static class CopilotConversationGoalProgressReportText
    {
        private const int MaximumCompactFieldCharacters = 96;

        public static string Format(CopilotConversationGoalProgressReport? report)
        {
            if (report == null)
                return string.Empty;

            return "当前检查点：" + report.Checkpoint
                + Environment.NewLine
                + "已验证：" + report.Verified
                + Environment.NewLine
                + "剩余工作：" + (report.Remaining.Length == 0 ? "无" : report.Remaining)
                + Environment.NewLine
                + "下一步：" + (report.NextStep.Length == 0 ? "无（目标已达成）" : report.NextStep);
        }

        public static string FormatCompact(CopilotConversationGoalProgressReport? report)
        {
            if (report == null)
                return string.Empty;

            var next = report.NextStep.Length == 0
                ? string.Empty
                : " · 下一步 " + Preview(report.NextStep);
            return "检查点 " + Preview(report.Checkpoint) + next;
        }

        public static string FormatEnglishCompact(CopilotConversationGoalProgressReport? report)
        {
            if (report == null)
                return string.Empty;

            var next = report.NextStep.Length == 0
                ? string.Empty
                : ", next " + Preview(report.NextStep);
            return "checkpoint " + Preview(report.Checkpoint) + next;
        }

        private static string Preview(string value) =>
            value.Length <= MaximumCompactFieldCharacters
                ? value
                : value[..MaximumCompactFieldCharacters].TrimEnd() + "…";
    }

    public sealed class CopilotConversationGoalIteration
    {
        public int TurnNumber { get; init; }

        public int EvaluationNumber { get; init; }

        public CopilotConversationGoalState State { get; init; }

        public bool Evaluated { get; init; }

        public bool ContinuationCounted { get; init; }

        public int TurnTokensUsed { get; init; }

        public long TurnTimeUsedSeconds { get; init; }

        public int? ProgressScore { get; init; }

        public CopilotConversationGoalProgressReport? ProgressReport { get; init; }

        public string Reason { get; init; } = string.Empty;

        public DateTimeOffset CompletedAtUtc { get; init; }

        internal bool IsStructurallyValid(CopilotConversationGoal goal)
        {
            ArgumentNullException.ThrowIfNull(goal);
            return TurnNumber > 0
                && TurnNumber <= goal.TurnCount
                && EvaluationNumber >= 0
                && EvaluationNumber <= goal.EvaluationCount
                && (!Evaluated || EvaluationNumber > 0)
                && Enum.IsDefined(State)
                && TurnTokensUsed >= 0
                && TurnTimeUsedSeconds >= 0
                && (!ProgressScore.HasValue
                    || (Evaluated && CopilotConversationGoal.IsValidProgressScore(ProgressScore.Value)))
                && (ProgressReport == null
                    || (Evaluated
                        && ProgressScore.HasValue
                        && ProgressReport.IsStructurallyValid(
                            State == CopilotConversationGoalState.Achieved)))
                && Reason != null
                && Reason.Length <= CopilotConversationGoal.MaximumReasonCharacters
                && !Reason.Contains('\0')
                && CompletedAtUtc >= goal.CreatedAtUtc
                && CompletedAtUtc <= goal.UpdatedAtUtc;
        }
    }

    public sealed class CopilotConversationGoal
    {
        public const int MaximumObjectiveCharacters = 4_000;
        public const int MaximumReasonCharacters = 1_000;
        public const int MaximumProgressReportFieldCharacters = 320;
        public const int MaximumIterationLogEntries = 16;
        public const int MinimumProgressScore = 0;
        public const int MaximumProgressScore = 100;
        public const int CurrentStrategyVersion = 1;

        public int StrategyVersion { get; init; } = CurrentStrategyVersion;

        public string Id { get; init; } = string.Empty;

        public string Objective { get; init; } = string.Empty;

        public CopilotConversationGoalState State { get; init; }

        public DateTimeOffset CreatedAtUtc { get; init; }

        public DateTimeOffset UpdatedAtUtc { get; init; }

        public int TurnCount { get; init; }

        public int EvaluationCount { get; init; }

        public int? LastProgressScore { get; init; }

        public int? BestProgressScore { get; init; }

        public CopilotConversationGoalProgressReport? LastProgressReport { get; init; }

        public long TokensUsed { get; init; }

        public long TimeUsedSeconds { get; init; }

        public long TokenBudget { get; init; }

        public int ConsecutiveContinuationCount { get; init; }

        public string LastEvaluationReason { get; init; } = string.Empty;

        public DateTimeOffset? LastEvaluatedAtUtc { get; init; }

        public IReadOnlyList<CopilotConversationGoalIteration> IterationLog { get; init; } =
            Array.Empty<CopilotConversationGoalIteration>();

        [JsonIgnore]
        public bool IsActive => State == CopilotConversationGoalState.Active;

        [JsonIgnore]
        public bool IsAchieved => State == CopilotConversationGoalState.Achieved;

        [JsonIgnore]
        public bool HasTokenBudget => TokenBudget > 0;

        [JsonIgnore]
        public bool IsTokenBudgetExhausted => HasTokenBudget && TokensUsed >= TokenBudget;

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
                && (!LastProgressScore.HasValue || IsValidProgressScore(LastProgressScore.Value))
                && (!BestProgressScore.HasValue || IsValidProgressScore(BestProgressScore.Value))
                && (!LastProgressScore.HasValue || BestProgressScore.HasValue)
                && (!LastProgressScore.HasValue || LastProgressScore.Value <= BestProgressScore!.Value)
                && (LastProgressReport == null
                    || (LastProgressScore.HasValue
                        && LastProgressReport.IsStructurallyValid(
                            State == CopilotConversationGoalState.Achieved)))
                && (!LastProgressScore.HasValue
                    || (LastProgressScore.Value == MaximumProgressScore)
                        == (State == CopilotConversationGoalState.Achieved))
                && (!BestProgressScore.HasValue
                    || BestProgressScore.Value < MaximumProgressScore
                    || State == CopilotConversationGoalState.Achieved)
                && ((!LastProgressScore.HasValue && !BestProgressScore.HasValue)
                    || EvaluationCount > 0)
                && TokensUsed >= 0
                && TimeUsedSeconds >= 0
                && TokenBudget >= 0
                && ConsecutiveContinuationCount is >= 0 and <= int.MaxValue
                && ConsecutiveContinuationCount <= EvaluationCount
                && LastEvaluationReason != null
                && LastEvaluationReason.Length <= MaximumReasonCharacters
                && !LastEvaluationReason.Contains('\0')
                && (!LastEvaluatedAtUtc.HasValue
                    || (LastEvaluatedAtUtc.Value >= CreatedAtUtc
                        && LastEvaluatedAtUtc.Value <= UpdatedAtUtc))
                && HasValidIterationLog();
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
                LastProgressScore = LastProgressScore,
                BestProgressScore = BestProgressScore,
                LastProgressReport = LastProgressReport,
                TokensUsed = TokensUsed,
                TimeUsedSeconds = TimeUsedSeconds,
                TokenBudget = TokenBudget,
                ConsecutiveContinuationCount = state == CopilotConversationGoalState.Active
                    ? 0
                    : ConsecutiveContinuationCount,
                LastEvaluationReason = reason == null ? LastEvaluationReason : NormalizeReason(reason),
                LastEvaluatedAtUtc = LastEvaluatedAtUtc,
                IterationLog = CloneIterationLog(IterationLog),
            };
        }

        internal CopilotConversationGoal WithTurnOutcome(
            CopilotConversationGoalState state,
            CopilotTokenUsage usage,
            long elapsedSeconds,
            bool evaluated,
            bool continued,
            string? reason,
            DateTimeOffset now,
            int? progressScore = null,
            CopilotConversationGoalProgressReport? progressReport = null)
        {
            if (progressScore.HasValue
                && (!evaluated
                    || !IsValidProgressScore(progressScore.Value)
                    || (progressScore.Value == MaximumProgressScore)
                        != (state == CopilotConversationGoalState.Achieved)))
            {
                throw new ArgumentOutOfRangeException(
                    nameof(progressScore),
                    "A progress score requires a completed evaluation, must be between 0 and 100, and score 100 is reserved for an achieved goal.");
            }
            if (progressReport != null
                && (!evaluated
                    || !progressScore.HasValue
                    || !progressReport.IsStructurallyValid(
                        state == CopilotConversationGoalState.Achieved)))
            {
                throw new ArgumentException(
                    "A progress report requires a scored evaluation and must match whether the goal was achieved.",
                    nameof(progressReport));
            }

            var normalizedReason = NormalizeReason(reason);
            var effectiveNow = GetMonotonicUpdateTime(now);
            var nextTurnCount = Increment(TurnCount);
            var nextEvaluationCount = evaluated ? Increment(EvaluationCount) : EvaluationCount;
            var nextLastProgressScore = evaluated ? progressScore : LastProgressScore;
            var nextBestProgressScore = progressScore.HasValue
                ? Math.Max(BestProgressScore ?? MinimumProgressScore, progressScore.Value)
                : BestProgressScore;
            var nextLastProgressReport = evaluated ? progressReport : LastProgressReport;
            var iteration = new CopilotConversationGoalIteration
            {
                TurnNumber = nextTurnCount,
                EvaluationNumber = nextEvaluationCount,
                State = state,
                Evaluated = evaluated,
                ContinuationCounted = continued,
                TurnTokensUsed = Math.Max(0, usage.EffectiveTotalTokens),
                TurnTimeUsedSeconds = Math.Max(0, elapsedSeconds),
                ProgressScore = evaluated ? progressScore : null,
                ProgressReport = evaluated ? progressReport : null,
                Reason = normalizedReason,
                CompletedAtUtc = effectiveNow,
            };
            return new CopilotConversationGoal
            {
                StrategyVersion = StrategyVersion,
                Id = Id,
                Objective = Objective,
                State = state,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = effectiveNow,
                TurnCount = nextTurnCount,
                EvaluationCount = nextEvaluationCount,
                LastProgressScore = nextLastProgressScore,
                BestProgressScore = nextBestProgressScore,
                LastProgressReport = nextLastProgressReport,
                TokensUsed = AddTokens(TokensUsed, usage.EffectiveTotalTokens),
                TimeUsedSeconds = AddTime(TimeUsedSeconds, elapsedSeconds),
                TokenBudget = TokenBudget,
                ConsecutiveContinuationCount = continued
                    ? Increment(ConsecutiveContinuationCount)
                    : 0,
                LastEvaluationReason = normalizedReason,
                LastEvaluatedAtUtc = evaluated ? effectiveNow : LastEvaluatedAtUtc,
                IterationLog = AppendIteration(IterationLog, iteration),
            };
        }

        internal CopilotConversationGoal WithTokenBudget(long tokenBudget, DateTimeOffset now)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(tokenBudget);

            return new CopilotConversationGoal
            {
                StrategyVersion = StrategyVersion,
                Id = Id,
                Objective = Objective,
                State = State,
                CreatedAtUtc = CreatedAtUtc,
                UpdatedAtUtc = GetMonotonicUpdateTime(now),
                TurnCount = TurnCount,
                EvaluationCount = EvaluationCount,
                LastProgressScore = LastProgressScore,
                BestProgressScore = BestProgressScore,
                LastProgressReport = LastProgressReport,
                TokensUsed = TokensUsed,
                TimeUsedSeconds = TimeUsedSeconds,
                TokenBudget = tokenBudget,
                ConsecutiveContinuationCount = ConsecutiveContinuationCount,
                LastEvaluationReason = LastEvaluationReason,
                LastEvaluatedAtUtc = LastEvaluatedAtUtc,
                IterationLog = CloneIterationLog(IterationLog),
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
                TokenBudget = TokenBudget,
                LastEvaluationReason = State is CopilotConversationGoalState.Achieved
                    or CopilotConversationGoalState.Blocked
                    or CopilotConversationGoalState.UsageLimited
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

        private bool HasValidIterationLog()
        {
            if (IterationLog == null || IterationLog.Count > MaximumIterationLogEntries)
                return false;

            var previousTurnNumber = 0;
            DateTimeOffset? previousCompletedAtUtc = null;
            CopilotConversationGoalIteration? latestEvaluated = null;
            foreach (var iteration in IterationLog)
            {
                if (iteration == null
                    || !iteration.IsStructurallyValid(this)
                    || iteration.TurnNumber <= previousTurnNumber
                    || (previousCompletedAtUtc.HasValue
                        && iteration.CompletedAtUtc < previousCompletedAtUtc.Value))
                {
                    return false;
                }

                previousTurnNumber = iteration.TurnNumber;
                previousCompletedAtUtc = iteration.CompletedAtUtc;
                if (iteration.Evaluated)
                    latestEvaluated = iteration;
                if (iteration.ProgressScore.HasValue
                    && (!BestProgressScore.HasValue
                        || iteration.ProgressScore.Value > BestProgressScore.Value))
                {
                    return false;
                }
            }

            return latestEvaluated == null
                || latestEvaluated.EvaluationNumber != EvaluationCount
                || (latestEvaluated.ProgressScore == LastProgressScore
                    && latestEvaluated.ProgressReport == LastProgressReport);
        }

        internal static bool IsValidProgressScore(int value) =>
            value is >= MinimumProgressScore and <= MaximumProgressScore;

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

        private static CopilotConversationGoalIteration[] CloneIterationLog(
            IReadOnlyList<CopilotConversationGoalIteration>? source)
        {
            if (source == null || source.Count == 0)
                return Array.Empty<CopilotConversationGoalIteration>();

            var count = Math.Min(source.Count, MaximumIterationLogEntries);
            var start = source.Count - count;
            var result = new CopilotConversationGoalIteration[count];
            for (var index = 0; index < count; index++)
                result[index] = source[start + index];
            return result;
        }

        private static CopilotConversationGoalIteration[] AppendIteration(
            IReadOnlyList<CopilotConversationGoalIteration>? source,
            CopilotConversationGoalIteration iteration)
        {
            var eligibleSourceCount = source?.Count ?? 0;
            while (eligibleSourceCount > 0
                && source![eligibleSourceCount - 1].TurnNumber >= iteration.TurnNumber)
            {
                eligibleSourceCount--;
            }
            var sourceCount = Math.Min(
                eligibleSourceCount,
                MaximumIterationLogEntries - 1);
            var sourceStart = eligibleSourceCount - sourceCount;
            var result = new CopilotConversationGoalIteration[sourceCount + 1];
            for (var index = 0; index < sourceCount; index++)
                result[index] = source![sourceStart + index];
            result[^1] = iteration;
            return result;
        }

        private DateTimeOffset GetMonotonicUpdateTime(DateTimeOffset now) =>
            now < UpdatedAtUtc ? UpdatedAtUtc : now;

        private static long AddTokens(long current, int additional)
        {
            var boundedAdditional = Math.Max(0, additional);
            return current > long.MaxValue - boundedAdditional
                ? long.MaxValue
                : current + boundedAdditional;
        }

        private static long AddTime(long current, long additional)
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

    internal static class CopilotConversationGoalFeaturePolicy
    {
        public static bool CanManageWhileDisabled(string? arguments)
        {
            var normalized = (arguments ?? string.Empty).Trim();
            return normalized.Length == 0
                || string.Equals(normalized, "history", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "pause", StringComparison.OrdinalIgnoreCase)
                || string.Equals(normalized, "clear", StringComparison.OrdinalIgnoreCase);
        }
    }

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

            if (string.Equals(normalized, "history", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotConversationGoalCommandResult(
                    current,
                    false,
                    current == null
                        ? "当前会话没有可查看记录的持续目标。先用 /goal <目标> 设置一个。"
                        : FormatHistory(current));
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
                if (current.IsAchieved)
                    return new CopilotConversationGoalCommandResult(current, false, FormatStatus(current));
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
                if (current.IsAchieved)
                {
                    return new CopilotConversationGoalCommandResult(
                        current,
                        false,
                        "已达成目标不能原地恢复；重新输入同一目标可以创建一份新的目标与用量统计。\n"
                        + current.Objective);
                }
                if (current.IsTokenBudgetExhausted)
                {
                    return new CopilotConversationGoalCommandResult(
                        current,
                        false,
                        $"持续目标已使用 {current.TokensUsed:N0} / {current.TokenBudget:N0} Token；"
                        + "请先用 /goal budget <Token> 提高预算，或用 /goal budget clear 清除预算。\n"
                        + current.Objective);
                }
                if (current.State == CopilotConversationGoalState.Active)
                    return new CopilotConversationGoalCommandResult(current, false, FormatStatus(current));

                var resumed = current.WithState(CopilotConversationGoalState.Active, now);
                return new CopilotConversationGoalCommandResult(
                    resumed,
                    true,
                    "持续目标已恢复；即将启动新的 Agent 轮次。\n" + resumed.Objective,
                    StartsWork: true);
            }

            if (string.Equals(normalized, "budget", StringComparison.OrdinalIgnoreCase))
            {
                return new CopilotConversationGoalCommandResult(
                    current,
                    false,
                    current == null
                        ? "当前会话没有可设置预算的持续目标。先用 /goal <目标> 设置一个。"
                        : FormatStatus(current) + "\n用法：/goal budget <Token|clear>。");
            }

            if (normalized.StartsWith("budget ", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("budget\t", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("budget\r", StringComparison.OrdinalIgnoreCase)
                || normalized.StartsWith("budget\n", StringComparison.OrdinalIgnoreCase))
            {
                if (current == null)
                    return MissingGoal(current, "设置预算");

                var budgetText = normalized["budget".Length..].Trim();
                if (string.Equals(budgetText, "clear", StringComparison.OrdinalIgnoreCase))
                {
                    var unlimited = current.WithTokenBudget(0, now);
                    return new CopilotConversationGoalCommandResult(
                        unlimited,
                        current.TokenBudget != 0,
                        "持续目标 Token 预算已清除；既有用量统计保持不变。\n" + unlimited.Objective);
                }

                if (!long.TryParse(
                        budgetText,
                        NumberStyles.Integer | NumberStyles.AllowThousands,
                        CultureInfo.InvariantCulture,
                        out var tokenBudget)
                    || tokenBudget <= 0)
                {
                    return new CopilotConversationGoalCommandResult(
                        current,
                        false,
                        "Token 预算必须是正整数。用法：/goal budget <Token|clear>。");
                }

                var budgeted = current.WithTokenBudget(tokenBudget, now);
                if (budgeted.IsActive && budgeted.IsTokenBudgetExhausted)
                {
                    budgeted = budgeted.WithState(
                        CopilotConversationGoalState.BudgetLimited,
                        now,
                        $"持续目标已使用 {budgeted.TokensUsed:N0} / {budgeted.TokenBudget:N0} Token；目标已进入预算受限状态。");
                }
                return new CopilotConversationGoalCommandResult(
                    budgeted,
                    current.TokenBudget != tokenBudget || current.State != budgeted.State,
                    $"持续目标 Token 预算已设为 {tokenBudget:N0}；当前已使用 {budgeted.TokensUsed:N0}。"
                    + (budgeted.IsTokenBudgetExhausted
                        ? " 预算已经用尽，目标不会恢复自动续作；请提高或清除预算。"
                        : string.Empty)
                    + "\n"
                    + budgeted.Objective);
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
                if (current.IsTokenBudgetExhausted)
                {
                    return new CopilotConversationGoalCommandResult(
                        current,
                        false,
                        $"持续目标已使用 {current.TokensUsed:N0} / {current.TokenBudget:N0} Token；"
                        + "请先提高或清除预算，再继续同一目标。\n"
                        + current.Objective);
                }

                var continued = current.WithState(CopilotConversationGoalState.Active, now);
                return new CopilotConversationGoalCommandResult(
                    continued,
                    true,
                    "已继续当前持续目标并保留既有轮次、评估、Token 和耗时统计；即将启动新的 Agent 轮次。"
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
            var state = CopilotConversationGoalStateText.Format(goal.State);
            var tokenProgress = goal.HasTokenBudget
                ? $"{goal.TokensUsed:N0} / {goal.TokenBudget:N0} Token"
                : $"{goal.TokensUsed:N0} Token";
            var elapsed = "累计 " + CopilotConversationGoalUsageText.FormatElapsed(goal.TimeUsedSeconds);
            var progress = goal.TurnCount == 0
                ? goal.HasTokenBudget
                    ? $"尚未完成首轮 · {tokenProgress}"
                    : "尚未完成首轮"
                : $"{goal.TurnCount:N0} 轮 · {goal.EvaluationCount:N0} 次独立评估 · {tokenProgress} · {elapsed}";
            progress += CopilotConversationGoalScoreText.FormatSuffix(goal);
            var latest = string.IsNullOrWhiteSpace(goal.LastEvaluationReason)
                ? string.Empty
                : "\n最近判断：" + goal.LastEvaluationReason;
            var report = CopilotConversationGoalProgressReportText.Format(goal.LastProgressReport);
            var reportSection = report.Length == 0 ? string.Empty : "\n" + report;
            return $"持续目标 · {state} · {progress}\n{goal.Objective}{reportSection}{latest}\n"
                + "管理命令：/goal history、/goal edit <新目标>、/goal budget <Token|clear>、/goal pause、/goal resume、/goal clear。";
        }

        private static string FormatHistory(CopilotConversationGoal goal)
        {
            var entries = goal.IterationLog ?? Array.Empty<CopilotConversationGoalIteration>();
            if (entries.Count == 0)
            {
                return "持续目标尚无已完成的迭代记录。\n"
                    + goal.Objective;
            }

            var builder = new StringBuilder()
                .Append("持续目标迭代记录 · 最近 ")
                .Append(entries.Count.ToString("N0", CultureInfo.CurrentCulture))
                .Append(" / ")
                .Append(goal.TurnCount.ToString("N0", CultureInfo.CurrentCulture))
                .AppendLine(" 轮")
                .AppendLine(goal.Objective);
            var scoreSummary = CopilotConversationGoalScoreText.Format(goal);
            if (scoreSummary.Length > 0)
                builder.AppendLine(scoreSummary);
            foreach (var entry in entries)
            {
                builder.Append("第 ")
                    .Append(entry.TurnNumber.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" 轮 · ")
                    .Append(CopilotConversationGoalStateText.Format(entry.State))
                    .Append(entry.Evaluated
                        ? $" · 第 {entry.EvaluationNumber:N0} 次独立评估"
                        : " · 未独立评估")
                    .Append(" · ")
                    .Append(entry.TurnTokensUsed.ToString("N0", CultureInfo.CurrentCulture))
                    .Append(" Token · ")
                    .Append(CopilotConversationGoalUsageText.FormatElapsed(entry.TurnTimeUsedSeconds));
                if (entry.ProgressScore.HasValue)
                    builder.Append(" · 评分 ").Append(entry.ProgressScore.Value).Append("/100");
                var report = CopilotConversationGoalProgressReportText.Format(entry.ProgressReport);
                if (report.Length > 0)
                {
                    builder.AppendLine()
                        .Append("  ")
                        .Append(report.Replace(
                            Environment.NewLine,
                            Environment.NewLine + "  ",
                            StringComparison.Ordinal));
                }
                var reason = FormatReasonPreview(entry.Reason);
                if (reason.Length > 0)
                    builder.AppendLine().Append("  判断：").Append(reason);
                builder.AppendLine();
            }
            return builder.ToString().TrimEnd();
        }

        private static string FormatReasonPreview(string? reason)
        {
            const int maximumCharacters = 240;
            var normalized = (reason ?? string.Empty)
                .Replace("\r\n", " ", StringComparison.Ordinal)
                .Replace('\r', ' ')
                .Replace('\n', ' ')
                .Trim();
            return normalized.Length <= maximumCharacters
                ? normalized
                : normalized[..maximumCharacters].TrimEnd() + "…";
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
                if (conversation?.Goal?.IsActive != true
                    || conversation.IsGoalContinuationDeferred)
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
