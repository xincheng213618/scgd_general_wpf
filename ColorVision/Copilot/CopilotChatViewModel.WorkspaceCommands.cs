#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using ColorVision.Solution;
using ColorVision.Solution.Workspace;
using ColorVision.Copilot.Mcp;
using ColorVision.Common.MVVM;
using ColorVision.UI;
using ColorVision.UI.Desktop.Feedback;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private void OpenComposerMention(CopilotLocalCommand command, string query)
        {
            if (!CopilotComposerReferenceCatalog.TryCreateMentionInput(
                    query,
                    out var mentionInput,
                    out var errorMessage))
            {
                ShowLocalCommandResult(
                    command,
                    $"{errorMessage}{Environment.NewLine}用法：{command.Usage}");
                return;
            }

            DismissLocalCommandResult();
            InputText = mentionInput;
        }

        private void StartWorkspaceReview(CopilotLocalCommand command, string focusInstructions)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再开始审查。");
                return;
            }

            if (!CopilotWorkspaceReviewRequest.TryParse(focusInstructions, out var reviewRequest, out var error))
            {
                ShowLocalCommandResult(
                    command,
                    $"{error}{Environment.NewLine}用法：{command.Usage}");
                return;
            }

            DismissLocalCommandResult();
            SetPendingRequestModeOverride(CopilotAgentMode.Review);
            SetPendingWorkspaceReviewTarget(reviewRequest.CreateTargetContext());
            InputText = reviewRequest.BuildPrompt();
            RunUiOperation(SendAsync, "开始工作区审查");
        }

        private void StartWorkspaceVerification(CopilotLocalCommand command, string focusInstructions)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再验证工作区。");
                return;
            }

            DismissLocalCommandResult();
            SetPendingRequestModeOverride(CopilotAgentMode.Review);
            SetPendingWorkspaceReviewTarget(CopilotWorkspaceReviewTargetContext.WorkingTree());
            InputText = CopilotWorkspaceVerification.BuildPrompt(focusInstructions);
            RunUiOperation(SendAsync, "验证工作区改动");
        }

        private void StartProjectInitialization(CopilotLocalCommand command)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再初始化项目指令。");
                return;
            }

            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var plan = CopilotProjectInitialization.Create(
                turnSnapshot.SolutionDirectoryPath,
                turnSnapshot.ProjectInstructionDiscoveryOptions);
            if (!plan.CanStart)
            {
                ShowLocalCommandResult(command, plan.Message);
                return;
            }

            DismissLocalCommandResult();
            RunUiOperation(
                () => SendAsync(plan.VisiblePrompt, CopilotAgentMode.Code, plan.ModelPrompt),
                "初始化项目指令");
        }

        private void StartPlanRequest(CopilotLocalCommand command, string task)
        {
            if (IsBusy)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再进入计划模式。");
                return;
            }

            SetPendingRequestModeOverride(CopilotAgentMode.Plan);
            if (string.IsNullOrWhiteSpace(task))
            {
                ShowLocalCommandResult(command, "下一条请求将使用计划模式：Copilot 只读取和分析相关证据，生成可执行计划，不会修改文件或应用状态。");
                return;
            }

            DismissLocalCommandResult();
            InputText = task.Trim();
            RunUiOperation(SendAsync, "生成执行计划");
        }

        private void ViewLatestCompletedPlan(CopilotLocalCommand command)
        {
            var plan = CopilotConversationPlanNavigation.FindLatestCompletedPlan(SelectedConversation);
            if (plan == null)
            {
                ShowLocalCommandResult(
                    command,
                    "当前会话没有已完成的计划。输入 /plan [任务] 可以先生成一份只读计划。");
                return;
            }

            DismissLocalCommandResult();
            MessageNavigationRequested?.Invoke(
                this,
                new CopilotChatMessageNavigationRequestedEventArgs(plan));
        }

        private void ManageConversationGoal(CopilotLocalCommand command, string arguments)
        {
            var conversation = SelectedConversation;
            if (conversation == null)
            {
                ShowLocalCommandResult(command, "当前没有可管理的会话。请先新建会话。");
                return;
            }

            var normalizedArguments = (arguments ?? string.Empty).Trim();
            var goalsEnabled = CaptureHostedTurnSnapshot(
                conversation,
                attachmentOverride: conversation.Attachments)
                .ProjectInstructionDiscoveryOptions
                .ConfiguredGoalsEnabled;
            if (!goalsEnabled
                && !CopilotConversationGoalFeaturePolicy.CanManageWhileDisabled(normalizedArguments))
            {
                ShowLocalCommandResult(
                    command,
                    "Codex features.goals=false 已暂停持续目标功能：不会向新请求注入目标、记录目标轮次、执行独立完成评估或自动续作。"
                    + Environment.NewLine
                    + "已有目标记录保持不变；仍可用 /goal 查看、/goal pause 暂停或 /goal clear 清除。修改配置后可再次恢复。");
                return;
            }
            if (IsBusy
                && normalizedArguments.Length > 0
                && !string.Equals(normalizedArguments, "pause", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(normalizedArguments, "clear", StringComparison.OrdinalIgnoreCase))
            {
                ShowLocalCommandResult(
                    command,
                    "当前 Agent 任务运行中；此时可以查看、暂停或清除持续目标。请在当前轮结束后再设置、编辑或恢复目标。");
                return;
            }

            var result = CopilotConversationGoalCommand.Execute(
                conversation.Goal,
                arguments,
                DateTimeOffset.UtcNow);
            if (result.Changed)
            {
                conversation.Goal = result.Goal;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                RefreshComposerTokenEstimate();
            }

            ShowLocalCommandResult(command, result.Message);
            if (result.StartsWork && result.Goal?.IsActive == true)
            {
                RunUiOperation(
                    () => SendAsync(
                        result.Goal.Objective,
                        CopilotAgentMode.Auto,
                        result.Goal.Objective),
                    "执行持续目标");
            }
        }

        private async Task ShowGitDiffAsync(CopilotLocalCommand command, string scope)
        {
            if (_isInspectingGitDiff)
            {
                ShowLocalCommandResult(command, "Git 变更快照正在生成，请稍候。");
                return;
            }

            _isInspectingGitDiff = true;
            var cancellation = BeginAuxiliaryOperation();
            ShowLocalCommandResult(command, "正在读取本地 Git 变更…不会调用模型，也不会修改文件。");
            try
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                var searchRoots = CopilotAgentRequestFactory.BuildSearchRootPaths(turnSnapshot, Array.Empty<string>());
                var result = await _localGitDiffService.ExecuteAsync(searchRoots, scope, cancellation.Token);
                ShowLocalCommandResult(command, result.Report);
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                ShowLocalCommandResult(command, "Git 变更快照失败：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            finally
            {
                CompleteAuxiliaryOperation(cancellation);
                _isInspectingGitDiff = false;
            }
        }

        private async Task CompactConversationAsync(
            CopilotLocalCommand command,
            string focusInstructions,
            bool includeFocusInResult = true,
            CopilotAgentDefaultsConfig? agentDefaults = null,
            CopilotProjectInstructionDiscoveryOptions? codexConfigOptions = null)
        {
            agentDefaults ??= _config.AgentDefaults;
            var conversation = SelectedConversation;
            var profile = SelectedProfile;
            if (IsBusy || _isCompactingConversation)
            {
                ShowLocalCommandResult(command, "当前有请求正在执行，请完成或停止后再压缩上下文。");
                return;
            }
            if (conversation == null || profile?.IsConfigured != true)
            {
                ShowLocalCommandResult(command, "请先选择并配置可用模型。");
                return;
            }
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                CreateCurrentConversationRequestProfile(profile, conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                    _currentCodexConfigOptions.ConfiguredHooksEnabled,
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled,
                    _currentCodexConfigOptions.ConfiguredCommandHooks)))
            {
                var latestAssistant = conversation.Messages.LastOrDefault(message => message != null && !message.IsUser);
                var isFinalAnswerRecovery = latestAssistant?.HasRecoverableFinalAnswer == true;
                ShowLocalCommandResult(
                    command,
                    isFinalAnswerRecovery
                        ? "当前会话的 Agent 工作已完成，但最终回答尚未完整返回。请先使用“重试最终回答”，或在任务列表中明确放弃这条恢复项，再压缩上下文；本次压缩未开始，checkpoint 已保留。"
                        : $"当前会话还有可安全继续的 Agent 任务。请先使用“{latestAssistant?.AgentRecoveryActionLabel ?? "继续任务"}”处理它，或在任务列表中明确放弃它，再压缩上下文；本次压缩未开始，checkpoint 已保留。");
                return;
            }

            var compactionConfig = codexConfigOptions
                ?? CaptureHostedTurnSnapshot(
                    conversation.Attachments).ProjectInstructionDiscoveryOptions;

            var sourceMessages = conversation.Messages
                .Where(message => !string.IsNullOrWhiteSpace(message.ModelContent))
                .ToArray();
            var newMessageCount = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
            if (sourceMessages.Length < 2 || newMessageCount < 2)
            {
                var reason = conversation.Compaction == null
                    ? "至少需要一轮完整对话后才能压缩。"
                    : "上次压缩后还没有足够的新对话，不需要重复压缩。";
                ShowLocalCommandResult(command, reason);
                return;
            }

            var summaryMaximumWeight = ResolveConversationHistoryLimits(
                profile,
                compactionConfig,
                agentDefaults).MaximumContentCharacters;
            var compactProfile = profile.Clone();
            compactProfile.UseSystemPromptOverride(CopilotConversationCompactionPrompt.SystemPrompt);
            compactProfile.MaxTokens = Math.Min(compactProfile.MaxTokens, CompactSummaryOutputTokens);
            compactProfile.Temperature = 0.1;

            var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(
                focusInstructions,
                compactionConfig.CompactPrompt);
            var historyLimits = ResolveConversationHistoryLimits(
                compactProfile,
                compactionConfig,
                agentDefaults);
            compactProfile.MaxTokens = Math.Min(
                compactProfile.MaxTokens,
                ResolveCompactSummaryOutputTokens(summaryMaximumWeight));
            CopilotConversationCompactionPlan compactionPlan;
            try
            {
                compactionPlan = CopilotConversationCompactionPlanner.Create(conversation, historyLimits, compactRequest);
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(command, "压缩未开始：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
                return;
            }
            var request = compactionPlan.SourceMessages
                .Append(new CopilotRequestMessage("user", compactRequest))
                .ToArray();

            using var cancellation = new CopilotNonBlockingCancellationSource();
            _compactConversationCts = cancellation;
            _isCompactingConversation = true;
            IsBusy = true;
            ShowLocalCommandResult(command, "正在压缩当前对话…完整聊天记录会继续保留在本地。");
            try
            {
                var reply = await _chatService.CompleteReplyDetailedAsync(compactProfile, request, cancellation.Token);
                if (CanApplyAuxiliaryConversationResult(conversation))
                {
                    conversation.RecordCompactionUsage(reply.Usage, DateTimeOffset.UtcNow);
                    PersistState();
                }
                else if (Volatile.Read(ref _disposeState) == 1)
                {
                    return;
                }
                cancellation.Token.ThrowIfCancellationRequested();
                if (reply.IsIncomplete)
                    throw new InvalidOperationException(BuildIncompleteCompactionMessage(reply));
                var summary = NormalizeCompactSummary(reply.Content, summaryMaximumWeight);
                if (summary.Length == 0)
                    throw new InvalidOperationException("模型没有返回可用的压缩摘要。");
                compactionPlan.TerminalEvidence.EnsurePreserved(summary);
                if (!Conversations.Contains(conversation) || !conversation.Messages.Contains(compactionPlan.BoundaryMessage))
                    throw new InvalidOperationException("压缩期间会话已发生变化，结果未应用。");

                conversation.Compaction = new CopilotConversationCompaction
                {
                    StrategyVersion = CopilotConversationCompaction.CurrentStrategyVersion,
                    Summary = summary,
                    ThroughMessageId = compactionPlan.BoundaryMessage.Id,
                    CreatedAtUtc = DateTimeOffset.UtcNow,
                    SourceMessageCount = compactionPlan.TotalSourceMessageCount,
                    SourceCharacters = compactionPlan.TotalSourceCharacters,
                };
                conversation.AgentSessionCheckpoint = null;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();

                var retainedAfterBoundary = CopilotConversationCompactionContext.CountMessagesAfterBoundary(conversation);
                ShowLocalCommandResult(
                    command,
                    $"已将最早 {compactionPlan.NewSourceMessageCount:N0} 条完整上下文、{compactionPlan.NewSourceCharacters:N0} 个字符合并进延续摘要。\n"
                    + $"后续请求将使用 {summary.Length:N0} 字符摘要，并保留边界后的 {retainedAfterBoundary:N0} 条新消息；界面中的完整对话未删除。\n"
                    + FormatCompactionUsage(reply.Usage)
                    + (!includeFocusInResult || string.IsNullOrWhiteSpace(focusInstructions)
                        ? string.Empty
                        : "\n聚焦要求：" + focusInstructions.Trim()));
                RefreshComposerTokenEstimate();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                ShowLocalCommandResult(
                    command,
                    "上下文压缩已取消，原有对话和压缩摘要均未改变；若 Provider 已完成响应，其 Token 元数据仍会计入本会话用量。");
            }
            catch (Exception ex)
            {
                ShowLocalCommandResult(command, "压缩失败：" + CopilotUserFacingErrorFormatter.Sanitize(ex.Message));
            }
            finally
            {
                if (ReferenceEquals(_compactConversationCts, cancellation))
                    _compactConversationCts = null;
                _isCompactingConversation = false;
                IsBusy = _taskHost.IsActive;
            }
        }

        private async Task<CopilotAutomaticCompactionOutcome> TryAutoCompactConversationAsync(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            string pendingPrompt,
            CopilotProjectInstructionDiscoveryOptions codexConfigOptions,
            CopilotAgentDefaultsConfig agentDefaults)
        {
            if (IsBusy || _taskHost.IsActive || _isCompactingConversation || IsEditingMessage)
                return CopilotAutomaticCompactionOutcome.NotNeeded;
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                requestProfile,
                CopilotCapabilityCatalog.Shared.GetSnapshot(
                    codexConfigOptions.ConfiguredPluginsEnabled),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                    codexConfigOptions.ConfiguredHooksEnabled,
                    codexConfigOptions.ConfiguredPluginsEnabled,
                    codexConfigOptions.ConfiguredCommandHooks)))
            {
                return CopilotAutomaticCompactionOutcome.NotNeeded;
            }

            var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
                conversation,
                ResolveConversationHistoryLimits(requestProfile, codexConfigOptions, agentDefaults),
                pendingPrompt,
                new CopilotConversationAutoCompactionOptions(
                    agentDefaults.AutoCompactConversationHistory,
                    agentDefaults.AutoCompactThresholdPercent,
                    codexConfigOptions.HasModelAutoCompactTokenLimitOverride
                        ? codexConfigOptions.ConfiguredModelAutoCompactTokenLimit
                        : null,
                    codexConfigOptions.EffectiveModelAutoCompactTokenLimitScope));
            if (!decision.ShouldCompact)
                return CopilotAutomaticCompactionOutcome.NotNeeded;

            var command = CopilotLocalCommandCatalog.FindExact("/compact");
            if (command == null)
                return CopilotAutomaticCompactionOutcome.Failed;

            var previousCompaction = conversation.Compaction;
            await CompactConversationAsync(
                command,
                CopilotConversationCompactionPrompt.BuildAutomaticFocus(
                    agentDefaults.AutoCompactInstructions),
                includeFocusInResult: false,
                agentDefaults,
                codexConfigOptions);
            var applied = !ReferenceEquals(previousCompaction, conversation.Compaction)
                && conversation.Compaction?.IsStructurallyValid() == true;
            if (!applied)
            {
                LocalCommandResultTitle = "自动压缩未完成";
                LocalCommandResultText = (LocalCommandResultText ?? string.Empty).Trim()
                    + Environment.NewLine
                    + "原请求尚未发送，输入和附件均已保留；请重试 /compact，或在设置中调整自动压缩策略。";
                return CopilotAutomaticCompactionOutcome.Failed;
            }

            var triggerText = decision.Trigger switch
            {
                CopilotConversationAutoCompactionTrigger.MessageCount =>
                    $"消息数达到 {decision.UsagePercent:N0}%",
                CopilotConversationAutoCompactionTrigger.ConfiguredTokenLimit =>
                    $"Codex {CopilotModelAutoCompactTokenLimitScopeSelection.GetConfigToken(decision.TokenLimitScope)} 计量达到 {decision.EvaluatedTokens:N0}/{decision.ThresholdTokens:N0} Token",
                _ => $"估算上下文达到 {decision.UsagePercent:N0}%",
            };
            var customFocusText = agentDefaults.AutoCompactInstructions.Length > 0
                ? $"已应用 {agentDefaults.AutoCompactInstructions.Length:N0} 字符的自定义长期重点。"
                : "已应用内置默认保留重点。";
            LocalCommandResultTitle = "/compact · 自动压缩";
            LocalCommandResultText = $"{triggerText}，已在发送前自动压缩早期对话。"
                + Environment.NewLine
                + customFocusText
                + Environment.NewLine
                + LocalCommandResultText;
            return CopilotAutomaticCompactionOutcome.Applied;
        }

        private void CompactConversationFromUi()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/compact");
            if (command == null)
                return;

            RunUiOperation(() => CompactConversationAsync(command, string.Empty), "压缩上下文");
        }

        private static string NormalizeCompactSummary(string summary, int maximumWeight)
        {
            var normalized = (summary ?? string.Empty).Trim();
            if (normalized.Length > CopilotConversationCompaction.MaximumSummaryCharacters)
            {
                throw new InvalidOperationException(
                    $"模型返回的压缩摘要超过 {CopilotConversationCompaction.MaximumSummaryCharacters:N0} 字符安全上限，未应用结果。请缩小聚焦范围后重试。");
            }
            if (CopilotTokenEstimator.EstimateTextWeight(normalized) > maximumWeight)
            {
                throw new InvalidOperationException(
                    "模型返回的压缩摘要超过当前会话可安全保留的单条历史预算，未应用结果。请缩小聚焦范围后重试。");
            }

            return normalized;
        }

        private static int ResolveCompactSummaryOutputTokens(int maximumWeight)
        {
            return Math.Clamp(
                maximumWeight / CopilotTokenEstimator.AsciiCharactersPerToken,
                32,
                CompactSummaryOutputTokens);
        }

        private static string BuildIncompleteCompactionMessage(CopilotCompletedReplyResult reply)
        {
            if (reply.IsContentTruncated)
                return "压缩摘要超过应用可安全保留的长度，未应用不完整结果；请缩小聚焦范围后重试。";

            return reply.StreamResult.FinishKind switch
            {
                CopilotChatFinishKind.LengthLimit => "模型因输出长度上限提前结束，未应用不完整摘要；请缩小聚焦范围后重试。",
                CopilotChatFinishKind.ContentFiltered => "提供商的内容安全策略提前停止了压缩，未应用不完整摘要。",
                CopilotChatFinishKind.ToolRequested => "模型在压缩过程中请求了工具，未应用不完整摘要。",
                _ => "提供商未正常完成压缩，未应用不完整摘要。",
            };
        }

        private static string FormatCompactionUsage(CopilotTokenUsage usage)
        {
            if (!usage.HasAny)
                return "本次压缩模型用量：Provider 未返回 Token 元数据。";

            var cache = usage.CachedInputTokens.HasValue
                ? $" · 缓存输入 {usage.EffectiveCachedInputTokens:N0}"
                : string.Empty;
            return $"本次压缩模型用量：输入 {Math.Max(0, usage.InputTokens):N0} · 输出 {Math.Max(0, usage.OutputTokens):N0} · 总计 {usage.EffectiveTotalTokens:N0}{cache}";
        }

    }
}
