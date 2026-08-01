using ColorVision.Common.MVVM;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatMessage
    {
        public IReadOnlyList<CopilotAgentBlockerSnapshot> AgentBlockers
        {
            get => _agentBlockers;
            set
            {
                var normalized = (value ?? Array.Empty<CopilotAgentBlockerSnapshot>())
                    .Where(item => item?.IsStructurallyValid() == true)
                    .Take(8)
                    .ToArray();
                if (SetProperty(ref _agentBlockers, normalized))
                    OnAgentTaskStateChanged();
            }
        }
        private IReadOnlyList<CopilotAgentBlockerSnapshot> _agentBlockers = Array.Empty<CopilotAgentBlockerSnapshot>();

        public bool ShouldSerializeAgentBlockers() => AgentBlockers?.Count > 0;

        [JsonIgnore]
        public CopilotUserQuestionSnapshot? UserQuestion
        {
            get => _userQuestion;
            set
            {
                var normalized = value?.IsStructurallyValid() == true ? value : null;
                if (SetProperty(ref _userQuestion, normalized))
                {
                    OnPropertyChanged(nameof(HasUserQuestion));
                    OnPropertyChanged(nameof(HasPendingUserQuestion));
                    OnPropertyChanged(nameof(HasResolvedUserQuestion));
                    OnPropertyChanged(nameof(UserQuestionStatusText));
                }
            }
        }
        private CopilotUserQuestionSnapshot? _userQuestion;

        [JsonIgnore]
        public bool HasUserQuestion => !IsUser && UserQuestion != null;

        [JsonIgnore]
        public bool HasPendingUserQuestion => HasUserQuestion && UserQuestion!.IsPending;

        [JsonIgnore]
        public bool HasResolvedUserQuestion => HasUserQuestion && !UserQuestion!.IsPending;

        [JsonIgnore]
        public string UserQuestionStatusText => UserQuestion?.Resolution switch
        {
            CopilotUserQuestionResolution.Answered => "已回答：" + UserQuestion.Answer,
            CopilotUserQuestionResolution.Cancelled => "问题已取消",
            _ => "可选择一个选项，或在输入框中直接回答。",
        };

        [JsonIgnore]
        public CopilotAgentRecoveryRequest? RecoveryRequest { get; set; }

        public bool IsAgentRecoveryDismissed
        {
            get => _isAgentRecoveryDismissed;
            set
            {
                if (SetProperty(ref _isAgentRecoveryDismissed, value))
                    OnAgentTaskStateChanged();
            }
        }
        private bool _isAgentRecoveryDismissed;

        public bool ShouldSerializeIsAgentRecoveryDismissed() => IsAgentRecoveryDismissed;

        [JsonIgnore]
        public bool HasAgentTaskLedger => !IsUser && AgentTaskLedger.TotalCount > 0;

        [JsonIgnore]
        public bool HasAgentTaskState => !IsUser && (HasAgentTaskLedger || HasAgentBlockers || HasRecoverableAgentTasks || HasCompletedPlan);

        [JsonIgnore]
        public bool HasCompletedPlan => CopilotPlanHandoff.IsCompletedPlan(this);

        [JsonIgnore]
        public bool HasIncompleteAgentTasks => HasAgentTaskLedger && AgentTaskLedger.RemainingCount > 0;

        [JsonIgnore]
        public bool HasRecoverableFinalAnswer => !IsAgentRecoveryDismissed
            && !HasIncompleteAgentTasks
            && ((WasResponseInterrupted && AgentStopReason == CopilotAgentStopReason.Completed)
                || AgentStopReason == CopilotAgentStopReason.Interrupted
                || (AgentStopReason is (CopilotAgentStopReason.IncompleteOutput
                        or CopilotAgentStopReason.BudgetExhausted
                        or CopilotAgentStopReason.ProviderFailure)
                    && AgentBlockers.Any(blocker => blocker?.Kind == CopilotAgentBlockerKind.ProviderOutput)));

        [JsonIgnore]
        public bool HasRecoverableAgentTasks => !IsAgentRecoveryDismissed
            && ((!IsUser && AgentStopReason == CopilotAgentStopReason.Paused)
                || (HasIncompleteAgentTasks
                    && AgentStopReason is CopilotAgentStopReason.BudgetExhausted
                        or CopilotAgentStopReason.TaskPassLimit
                        or CopilotAgentStopReason.Paused
                        or CopilotAgentStopReason.ProviderFailure)
                || (HasIncompleteAgentTasks && AgentStopReason == CopilotAgentStopReason.Interrupted)
                || HasRecoverableFinalAnswer);

        [JsonIgnore]
        public string AgentRecoveryActionLabel => HasRecoverableFinalAnswer
                ? "重试最终回答"
                : AgentTraceEntries?.LastOrDefault(entry => entry != null
            && entry.IsFailure
            && entry.RetryEligible
            && entry.Access == CopilotToolAccess.ReadOnly
            && entry.Idempotency == CopilotToolIdempotency.Idempotent) != null
                ? "重试只读检查"
                : "继续任务";

        [JsonIgnore]
        public string AgentRecoveryToolTip => HasRecoverableFinalAnswer
            ? "仅使用已保存的上下文和证据生成最终回答；不会再次调用工具"
            : "从当前 AgentSession 继续未完成任务；写操作仍需重新审批";

        [JsonIgnore]
        public bool HasAgentBlockers => !IsUser && AgentBlockers.Count > 0;

        [JsonIgnore]
        public string AgentBlockerLabel
        {
            get
            {
                if (AgentBlockers.Count == 0)
                    return string.Empty;
                var blocker = AgentBlockers[0];
                return blocker.Kind switch
                {
                    CopilotAgentBlockerKind.UserDecision => "需要您的决定",
                    CopilotAgentBlockerKind.Approval => "操作未获批准",
                    CopilotAgentBlockerKind.ProviderOutput when blocker.Code == "provider_interrupted" => "模型连接中断",
                    CopilotAgentBlockerKind.ProviderOutput => "模型未返回最终回答",
                    _ when !string.IsNullOrWhiteSpace(blocker.ToolName) => $"{blocker.ToolName} 无法继续",
                    _ => "任务暂时受阻",
                };
            }
        }

        [JsonIgnore]
        public string AgentTaskModeLabel => string.Equals(AgentTaskLedger.Mode, "plan", StringComparison.OrdinalIgnoreCase) ? "计划" : "执行";

        [JsonIgnore]
        public string AgentTaskProgressLabel => RequestMode == CopilotAgentMode.Plan
            ? $"{AgentTaskLedger.TotalCount} 个计划步骤"
            : $"{AgentTaskLedger.CompletedCount}/{AgentTaskLedger.TotalCount} 已完成";

        [JsonIgnore]
        public string AgentStopReasonLabel => AgentStopReason switch
        {
            CopilotAgentStopReason.None when IsExecutionInProgress => "任务执行中",
            CopilotAgentStopReason.None when HasIncompleteAgentTasks => "任务尚未完成",
            CopilotAgentStopReason.Completed when RequestMode == CopilotAgentMode.Plan => "计划已生成",
            CopilotAgentStopReason.Completed => "任务完成",
            CopilotAgentStopReason.AwaitingUser => "等待用户决定",
            CopilotAgentStopReason.ApprovalDenied => "审批未通过",
            CopilotAgentStopReason.BudgetExhausted => "本轮预算已用尽",
            CopilotAgentStopReason.TaskPassLimit => "达到本轮继续上限",
            CopilotAgentStopReason.Blocked => "任务受阻",
            CopilotAgentStopReason.Paused => "任务已暂停",
            CopilotAgentStopReason.Cancelled => "任务已取消",
            CopilotAgentStopReason.IncompleteOutput => "未收到最终回答",
            CopilotAgentStopReason.ProviderFailure => "模型连接中断",
            CopilotAgentStopReason.Interrupted => "应用中断后可恢复",
            _ => "Agent 已停止",
        };

        [JsonIgnore]
        public string AgentTaskSummaryToolTip => $"Agent 任务 · {AgentTaskModeLabel} · {AgentTaskProgressLabel}{Environment.NewLine}{AgentStopReasonLabel}";

        [JsonIgnore]
        public bool HasAgentRunMetrics => !IsUser
            && (AgentRunBudget.ProviderCalls > 0
                || AgentRunBudget.ToolCalls > 0
                || AgentRunBudget.ConsumedTokens > 0
                || AgentRunBudget.PeakEstimatedInputTokens > 0
                || AgentRunBudget.ProviderRetryCount > 0
                || AgentRunBudget.ContextRecoveryCount > 0
                || AgentRunBudget.ReportedTotalTokens > 0
                || AgentRunBudget.ElapsedMs > 0
                || AgentRunBudget.UsedDelegatedDirectAnswer
                || AgentRunBudget.RegisteredToolCount > 0
                || AgentRunBudget.AvailableToolCount > 0
                || AgentRunBudget.AvailableToolDefinitionCharacters > 0
                || AgentRunBudget.HarnessInstructionCharacters > 0);

        [JsonIgnore]
        public string AgentRunCompactLabel
        {
            get
            {
                if (!HasAgentRunMetrics)
                    return string.Empty;

                var parts = new List<string>();
                var delegatedProviderCalls = GetDelegatedProviderCalls();
                var totalProviderCalls = Math.Max(AgentRunBudget.ProviderCalls, delegatedProviderCalls);
                if (totalProviderCalls > 0)
                {
                    parts.Add(delegatedProviderCalls > 0
                        ? $"父 {Math.Max(0, totalProviderCalls - delegatedProviderCalls)} / 子 {delegatedProviderCalls}"
                        : $"模型 {totalProviderCalls}");
                }
                var totalTokens = Math.Max(AgentRunBudget.ConsumedTokens, GetDelegatedConsumedTokens());
                if (totalTokens > 0)
                    parts.Add($"{FormatTokenCount(totalTokens)} tokens");
                if (AgentRunBudget.UsedDelegatedDirectAnswer)
                    parts.Add("委派直返");
                return string.Join(" · ", parts);
            }
        }

        [JsonIgnore]
        public string AgentRunMetricsToolTip
        {
            get
            {
                if (!HasAgentRunMetrics)
                    return string.Empty;

                var delegatedProviderCalls = GetDelegatedProviderCalls();
                var totalProviderCalls = Math.Max(AgentRunBudget.ProviderCalls, delegatedProviderCalls);
                var parentProviderCalls = Math.Max(0, totalProviderCalls - delegatedProviderCalls);
                var delegatedTokens = GetDelegatedConsumedTokens();
                var totalTokens = Math.Max(AgentRunBudget.ConsumedTokens, delegatedTokens);
                var parentTokens = Math.Max(0, totalTokens - delegatedTokens);
                var delegatedToolSurface = GetDelegatedToolSurfacePeak();
                var hasDelegatedToolSurface = delegatedToolSurface.RegisteredToolCount > 0
                    || delegatedToolSurface.AvailableToolCount > 0
                    || delegatedToolSurface.AvailableToolDefinitionCharacters > 0
                    || delegatedToolSurface.HarnessInstructionCharacters > 0;
                var builder = new StringBuilder();
                builder.Append("模型调用：").Append(totalProviderCalls);
                if (delegatedProviderCalls > 0)
                {
                    builder.Append("（父 ").Append(parentProviderCalls)
                        .Append(" / 子 ").Append(delegatedProviderCalls).Append('）');
                }
                builder.AppendLine();
                builder.Append("令牌：").Append(totalTokens.ToString("N0"));
                if (delegatedTokens > 0)
                {
                    builder.Append("（父 ").Append(parentTokens.ToString("N0"))
                        .Append(" / 子 ").Append(delegatedTokens.ToString("N0")).Append('）');
                }
                if (AgentRunBudget.RequestTokenBudget > 0)
                    builder.Append(" / ").Append(AgentRunBudget.RequestTokenBudget.ToString("N0"));
                if (AgentRunBudget.UsedEstimatedUsage)
                    builder.Append("（包含估算）");
                builder.AppendLine();
                if (AgentRunBudget.ReportedInputTokens > 0
                    || AgentRunBudget.ReportedOutputTokens > 0
                    || AgentRunBudget.ReportedTotalTokens > 0)
                {
                    builder.Append("提供商用量：输入 ")
                        .Append(AgentRunBudget.ReportedInputTokens.ToString("N0"))
                        .Append(" · 输出 ")
                        .Append(AgentRunBudget.ReportedOutputTokens.ToString("N0"))
                        .Append(" · 总计 ")
                        .Append(AgentRunBudget.ReportedTotalTokens.ToString("N0"));
                    if (AgentRunBudget.ReportedCachedInputTokens.HasValue)
                    {
                        var cachedInputTokens = Math.Clamp(
                            AgentRunBudget.ReportedCachedInputTokens.Value,
                            0,
                            AgentRunBudget.ReportedInputTokens);
                        builder.Append(" · 缓存输入 ")
                            .Append(cachedInputTokens.ToString("N0"));
                        if (AgentRunBudget.ReportedInputTokens > 0)
                        {
                            builder.Append('（')
                                .Append((cachedInputTokens * 100d / AgentRunBudget.ReportedInputTokens).ToString("0.#"))
                                .Append("%）");
                        }
                    }
                    else
                    {
                        builder.Append(" · 缓存未上报");
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderRetryCount > 0)
                {
                    builder.Append("提供商重试：")
                        .Append(AgentRunBudget.ProviderRetryCount.ToString("N0"))
                        .Append(" 次");
                    if (AgentRunBudget.ProviderRetryDelayMs > 0)
                    {
                        builder.Append(" · 计划等待 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderRetryDelayMs));
                    }
                    if (AgentRunBudget.ProviderRateLimitRetryCount > 0)
                    {
                        builder.Append(" · 限流 ")
                            .Append(AgentRunBudget.ProviderRateLimitRetryCount.ToString("N0"))
                            .Append(" 次");
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderFirstContentTimeoutCount > 0
                    || AgentRunBudget.ProviderStreamInactivityTimeoutCount > 0)
                {
                    builder.Append("模型停顿中止：");
                    if (AgentRunBudget.ProviderFirstContentTimeoutCount > 0)
                    {
                        builder.Append("首内容 ")
                            .Append(AgentRunBudget.ProviderFirstContentTimeoutCount.ToString("N0"))
                            .Append(" 次");
                    }
                    if (AgentRunBudget.ProviderStreamInactivityTimeoutCount > 0)
                    {
                        if (AgentRunBudget.ProviderFirstContentTimeoutCount > 0)
                            builder.Append(" · ");
                        builder.Append("流式输出 ")
                            .Append(AgentRunBudget.ProviderStreamInactivityTimeoutCount.ToString("N0"))
                            .Append(" 次");
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderResponseCount > 0
                    || AgentRunBudget.ProviderCallDurationTotalMs > 0)
                {
                    builder.Append("模型延迟：");
                    var hasLatencyValue = false;
                    if (AgentRunBudget.ProviderResponseCount > 0)
                    {
                        var averageFirstResponseLatencyMs =
                            AgentRunBudget.ProviderFirstResponseLatencyTotalMs
                            / AgentRunBudget.ProviderResponseCount;
                        builder.Append("首响应平均 ")
                            .Append(FormatTraceDuration(averageFirstResponseLatencyMs))
                            .Append(" · 最慢 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderFirstResponseLatencyMaxMs));
                        hasLatencyValue = true;
                        if (AgentRunBudget.ProviderResponseCount < totalProviderCalls)
                        {
                            builder.Append(" · 有效响应 ")
                                .Append(AgentRunBudget.ProviderResponseCount)
                                .Append(" / ")
                                .Append(totalProviderCalls);
                        }
                    }
                    if (AgentRunBudget.ProviderCallDurationTotalMs > 0)
                    {
                        if (hasLatencyValue)
                            builder.Append(" · ");
                        builder.Append("调用累计 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderCallDurationTotalMs));
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ProviderStreamChunkCount > 0)
                {
                    builder.Append("流式输出：")
                        .Append(AgentRunBudget.ProviderStreamChunkCount.ToString("N0"))
                        .Append(" 个内容片段");
                    if (AgentRunBudget.ProviderStreamInterChunkLatencyCount > 0)
                    {
                        var averageInterChunkLatencyMs =
                            AgentRunBudget.ProviderStreamInterChunkLatencyTotalMs
                            / AgentRunBudget.ProviderStreamInterChunkLatencyCount;
                        builder.Append(" · 片段间平均 ")
                            .Append(FormatTraceDuration(averageInterChunkLatencyMs))
                            .Append(" · 最慢 ")
                            .Append(FormatTraceDuration(AgentRunBudget.ProviderStreamInterChunkLatencyMaxMs));
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.PeakEstimatedInputTokens > 0)
                {
                    builder.Append("峰值输入（估算）：")
                        .Append(AgentRunBudget.PeakEstimatedInputTokens.ToString("N0"));
                    if (AgentRunBudget.InputBudgetTokens > 0)
                    {
                        builder.Append(" / ")
                            .Append(AgentRunBudget.InputBudgetTokens.ToString("N0"));
                    }
                    builder.AppendLine();
                }
                if (AgentRunBudget.ContextRecoveryCount > 0)
                {
                    builder.Append("窗口恢复：")
                        .Append(AgentRunBudget.ContextRecoveryCount.ToString("N0"))
                        .Append(" 次");
                    var recoveryInputTokensBefore = Math.Max(
                        0,
                        AgentRunBudget.ContextRecoveryEstimatedInputTokensBefore);
                    if (recoveryInputTokensBefore > 0)
                    {
                        var recoveryInputTokensAfter = Math.Clamp(
                            AgentRunBudget.ContextRecoveryEstimatedInputTokensAfter,
                            0,
                            recoveryInputTokensBefore);
                        builder.Append(" · 累计输入（估算）")
                            .Append(recoveryInputTokensBefore.ToString("N0"))
                            .Append(" → ")
                            .Append(recoveryInputTokensAfter.ToString("N0"))
                            .Append(" tokens（缩减 ")
                            .Append(((recoveryInputTokensBefore - recoveryInputTokensAfter) * 100d
                                / recoveryInputTokensBefore).ToString("0.#"))
                            .Append("%）");
                    }
                    builder.AppendLine();
                }
                var delegatedToolCalls = GetDelegatedToolCalls();
                builder.Append("工具调用：");
                if (delegatedToolCalls > 0)
                    builder.Append("父 ");
                builder.Append(AgentRunBudget.ToolCalls);
                if (AgentRunBudget.MaxToolCalls > 0)
                    builder.Append(" / ").Append(AgentRunBudget.MaxToolCalls);
                if (delegatedToolCalls > 0)
                    builder.Append(" · 子 ").Append(delegatedToolCalls);
                if (AgentRunBudget.RegisteredToolCount > 0
                    || AgentRunBudget.AvailableToolCount > 0
                    || AgentRunBudget.AvailableToolDefinitionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append(hasDelegatedToolSurface ? "父工具面：" : "工具面：")
                        .Append(AgentRunBudget.AvailableToolCount)
                        .Append(" / ")
                        .Append(AgentRunBudget.RegisteredToolCount);
                    if (AgentRunBudget.AvailableToolDefinitionCharacters > 0)
                    {
                        builder.Append(" · 定义 ")
                            .Append(AgentRunBudget.AvailableToolDefinitionCharacters.ToString("N0"))
                            .Append(" 字符");
                    }
                }
                if (delegatedToolSurface.RegisteredToolCount > 0
                    || delegatedToolSurface.AvailableToolCount > 0
                    || delegatedToolSurface.AvailableToolDefinitionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append("子工具面（峰值）：")
                        .Append(delegatedToolSurface.AvailableToolCount)
                        .Append(" / ")
                        .Append(delegatedToolSurface.RegisteredToolCount);
                    if (delegatedToolSurface.AvailableToolDefinitionCharacters > 0)
                    {
                        builder.Append(" · 定义 ")
                            .Append(delegatedToolSurface.AvailableToolDefinitionCharacters.ToString("N0"))
                            .Append(" 字符");
                    }
                }
                if (AgentRunBudget.HarnessInstructionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append(hasDelegatedToolSurface ? "父运行指令：" : "运行指令：")
                        .Append(AgentRunBudget.HarnessInstructionCharacters.ToString("N0"))
                        .Append(" 字符");
                }
                if (delegatedToolSurface.HarnessInstructionCharacters > 0)
                {
                    builder.AppendLine();
                    builder.Append("子运行指令（峰值）：")
                        .Append(delegatedToolSurface.HarnessInstructionCharacters.ToString("N0"))
                        .Append(" 字符");
                }
                if (AgentRunBudget.ElapsedMs > 0)
                {
                    builder.AppendLine();
                    builder.Append("运行耗时：").Append(FormatTraceDuration(AgentRunBudget.ElapsedMs));
                    if (AgentRunBudget.TotalDurationMs > 0)
                        builder.Append(" / ").Append(FormatTraceDuration(AgentRunBudget.TotalDurationMs));
                }
                if (AgentRunBudget.UsedDelegatedDirectAnswer)
                    builder.AppendLine().Append("委派直返：是（省略第二次父级模型调用）");
                return builder.ToString();
            }
        }

    }
}
