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

            var prompt = new StringBuilder("Review the current uncommitted workspace changes. Do not modify files or apply fixes.");
            if (!string.IsNullOrWhiteSpace(focusInstructions))
                prompt.Append(" Focus: ").Append(focusInstructions.Trim());

            DismissLocalCommandResult();
            SetPendingRequestModeOverride(CopilotAgentMode.Review);
            InputText = prompt.ToString();
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

            var workspaceRoot = CaptureHostedTurnSnapshot(Attachments).SolutionDirectoryPath;
            var plan = CopilotProjectInitialization.Create(workspaceRoot);
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
            bool includeFocusInResult = true)
        {
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
                CreateConversationRequestProfile(profile, conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
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

            var summaryMaximumWeight = ResolveConversationHistoryLimits(profile).MaximumContentCharacters;
            var compactProfile = profile.Clone();
            compactProfile.UseSystemPromptOverride(CopilotConversationCompactionPrompt.SystemPrompt);
            compactProfile.MaxTokens = Math.Min(compactProfile.MaxTokens, CompactSummaryOutputTokens);
            compactProfile.Temperature = 0.1;

            var compactRequest = CopilotConversationCompactionPrompt.BuildRequest(focusInstructions);
            var historyLimits = ResolveConversationHistoryLimits(compactProfile);
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
                    + $"后续请求将使用 {summary.Length:N0} 字符摘要，并保留边界后的 {retainedAfterBoundary:N0} 条新消息；界面中的完整对话未删除。"
                    + (!includeFocusInResult || string.IsNullOrWhiteSpace(focusInstructions)
                        ? string.Empty
                        : "\n聚焦要求：" + focusInstructions.Trim()));
                RefreshComposerTokenEstimate();
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                ShowLocalCommandResult(command, "上下文压缩已取消，原有对话和压缩状态均未改变。");
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
            string pendingPrompt)
        {
            if (IsBusy || _taskHost.IsActive || _isCompactingConversation || IsEditingMessage)
                return CopilotAutomaticCompactionOutcome.NotNeeded;
            if (CopilotAgentTaskContinuityPolicy.HasAvailableStructuredRecovery(
                conversation,
                requestProfile,
                CopilotCapabilityCatalog.Shared.GetSnapshot()))
            {
                return CopilotAutomaticCompactionOutcome.NotNeeded;
            }

            var decision = CopilotConversationAutoCompactionPolicy.Evaluate(
                conversation,
                ResolveConversationHistoryLimits(requestProfile),
                pendingPrompt,
                _config.AgentDefaults.AutoCompactConversationHistory,
                _config.AgentDefaults.AutoCompactThresholdPercent);
            if (!decision.ShouldCompact)
                return CopilotAutomaticCompactionOutcome.NotNeeded;

            var command = CopilotLocalCommandCatalog.FindExact("/compact");
            if (command == null)
                return CopilotAutomaticCompactionOutcome.Failed;

            var previousCompaction = conversation.Compaction;
            await CompactConversationAsync(
                command,
                CopilotConversationCompactionPrompt.BuildAutomaticFocus(
                    _config.AgentDefaults.AutoCompactInstructions),
                includeFocusInResult: false);
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

            var triggerText = decision.Trigger == CopilotConversationAutoCompactionTrigger.MessageCount
                ? $"消息数达到 {decision.UsagePercent:N0}%"
                : $"估算上下文达到 {decision.UsagePercent:N0}%";
            var customFocusText = _config.AgentDefaults.AutoCompactInstructions.Length > 0
                ? $"已应用 {_config.AgentDefaults.AutoCompactInstructions.Length:N0} 字符的自定义长期重点。"
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

        private string BuildAgentSkillDiagnosticsReport()
        {
            var agentDefaults = _config.AgentDefaults;
            return CopilotAgentSkillDiagnostics.FormatReport(
                CopilotAgentSkillUsageStore.Shared.GetSnapshot(),
                CopilotAgentSkills.ResolveMetadataCharacterBudget(agentDefaults.ContextWindowTokens),
                agentDefaults.CreateSkillOverrideSnapshot());
        }

        private string BuildPermissionDiagnosticsReport()
        {
            var mode = ResolveComposerRequestMode();
            var turnSnapshot = SelectedConversation == null
                ? CaptureHostedTurnSnapshot(Attachments)
                : CaptureHostedTurnSnapshot(
                    SelectedConversation,
                    attachmentOverride: Attachments);
            var requestPlan = CopilotAgentRequestFactory.Prepare(string.Empty, mode, turnSnapshot);
            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            return CopilotPermissionDiagnostics.Format(new CopilotPermissionDiagnosticSnapshot
            {
                Mode = mode,
                AccessMode = ComposerAccessMode,
                SearchRootPaths = requestPlan.SearchRootPaths,
                TrustedProjectRootPaths = requestPlan.TrustedProjectRootPaths,
                WritableRootPaths = requestPlan.WritableLocalRootPaths,
                WritableFilePaths = requestPlan.WritableLocalFilePaths,
                CapabilityCatalogRevision = capabilitySnapshot.Revision,
                Capabilities = capabilitySnapshot.Capabilities,
                ExternalMcpServers = _config.ExternalMcpServers,
                PendingApprovals = CopilotMcpConfirmationStore.Instance.PendingCount,
            });
        }

        private void HandlePermissionsCommand(CopilotLocalCommand command, string arguments)
        {
            switch (CopilotPermissionCommand.Resolve(arguments))
            {
                case CopilotPermissionCommandAction.OpenSelector:
                    DismissLocalCommandResult();
                    AccessModeSelectionRequested?.Invoke(this, EventArgs.Empty);
                    break;
                case CopilotPermissionCommandAction.ShowStatus:
                    ShowLocalCommandResult(command, BuildPermissionDiagnosticsReport());
                    break;
                case CopilotPermissionCommandAction.UseConfirmProtectedActions:
                    DismissLocalCommandResult();
                    SetComposerAccessMode(CopilotAgentAccessMode.ConfirmProtectedActions);
                    break;
                case CopilotPermissionCommandAction.UseTemporaryAutoReview:
                    DismissLocalCommandResult();
                    SetComposerAccessMode(CopilotAgentAccessMode.FullAccess);
                    break;
                default:
                    ShowLocalCommandResult(command, CopilotPermissionCommand.Usage);
                    break;
            }
        }

        private void HandleAdditionalDirectoryCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotAdditionalDirectoryCommand.Parse(arguments);
            var conversation = SelectedConversation ?? EnsureConversation();
            var currentPaths = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                conversation.AdditionalReadRootPaths);
            switch (request.Action)
            {
                case CopilotAdditionalDirectoryCommandAction.List:
                    ShowLocalCommandResult(
                        command,
                        CopilotAdditionalDirectoryCommand.Format(currentPaths));
                    return;
                case CopilotAdditionalDirectoryCommandAction.Clear:
                    if (!conversation.ReplaceAdditionalReadRootPaths(Array.Empty<string>()))
                    {
                        ShowLocalCommandResult(command, "当前会话没有附加只读目录。");
                        return;
                    }

                    UpdateConversationMetadata(conversation, touch: true);
                    PersistState(immediate: true);
                    ShowLocalCommandResult(
                        command,
                        "已清空当前会话的附加只读目录；后续 Agent 请求只使用工作区、活动文档、附件和请求中显式写出的路径。");
                    return;
                case CopilotAdditionalDirectoryCommandAction.Remove:
                    if (request.Ordinal > currentPaths.Length)
                    {
                        ShowLocalCommandResult(
                            command,
                            $"没有编号 {request.Ordinal:N0} 的附加目录。{Environment.NewLine}{Environment.NewLine}"
                            + CopilotAdditionalDirectoryCommand.Format(currentPaths));
                        return;
                    }

                    var removedPath = currentPaths[request.Ordinal - 1];
                    conversation.ReplaceAdditionalReadRootPaths(
                        currentPaths.Where((_, index) => index != request.Ordinal - 1));
                    UpdateConversationMetadata(conversation, touch: true);
                    PersistState(immediate: true);
                    ShowLocalCommandResult(
                        command,
                        $"已移除附加只读目录：{removedPath}{Environment.NewLine}{Environment.NewLine}"
                        + CopilotAdditionalDirectoryCommand.Format(conversation.AdditionalReadRootPaths));
                    return;
                case CopilotAdditionalDirectoryCommandAction.Add:
                    if (!CopilotAdditionalDirectoryCommand.TryNormalizeExistingDirectory(
                            request.Path,
                            out var addedPath,
                            out var errorMessage))
                    {
                        ShowLocalCommandResult(command, errorMessage);
                        return;
                    }

                    var workspaceRoot = CaptureHostedTurnSnapshot(
                        conversation.Attachments).SolutionDirectoryPath;
                    var workspaceRoots = CopilotWorkspaceSearchSupport.NormalizeSearchRoots([workspaceRoot]);
                    if (CopilotWorkspaceSearchSupport.IsPathWithinRoots(addedPath, workspaceRoots))
                    {
                        ShowLocalCommandResult(
                            command,
                            "该目录已经位于当前工作区读取范围内，无需重复添加：" + addedPath);
                        return;
                    }
                    if (CopilotWorkspaceSearchSupport.IsPathWithinRoots(addedPath, currentPaths))
                    {
                        ShowLocalCommandResult(
                            command,
                            "该目录已经被现有附加目录覆盖：" + addedPath);
                        return;
                    }

                    var mergedPaths = CopilotAdditionalDirectoryCommand.NormalizeStoredPaths(
                        currentPaths.Append(addedPath));
                    if (!mergedPaths.Contains(addedPath, StringComparer.OrdinalIgnoreCase))
                    {
                        ShowLocalCommandResult(
                            command,
                            $"当前会话最多保留 {CopilotAdditionalDirectoryCommand.MaximumDirectories:N0} 个附加目录；请先使用 /add-dir remove N 移除一个。");
                        return;
                    }

                    conversation.ReplaceAdditionalReadRootPaths(mergedPaths);
                    UpdateConversationMetadata(conversation, touch: true);
                    PersistState(immediate: true);
                    ShowLocalCommandResult(
                        command,
                        $"已添加附加只读目录：{addedPath}{Environment.NewLine}"
                        + "它只对后续新 Agent 请求生效，不会扩大写入范围或加载其中的配置。"
                        + Environment.NewLine
                        + Environment.NewLine
                        + CopilotAdditionalDirectoryCommand.Format(conversation.AdditionalReadRootPaths));
                    return;
                default:
                    ShowLocalCommandResult(
                        command,
                        $"用法：{CopilotAdditionalDirectoryCommand.Usage}");
                    return;
            }
        }

        private static string BuildHookDiagnosticsReport()
        {
            var extensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            return CopilotHookDiagnostics.Format(new CopilotHookDiagnosticSnapshot
            {
                HookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot(),
                ExtensionSources = extensionSnapshot.Sources,
                ExtensionIssues = extensionSnapshot.Issues,
                RecentToolExecutions = CopilotToolExecutionAuditLogger.GetRecentEntries(30),
            });
        }

        private void ShowLocalCommandResult(CopilotLocalCommand command, string report)
        {
            LocalCommandResultTitle = $"{command.Name} · 本地快照";
            LocalCommandResultText = report;
        }

        public void ShowKeyboardShortcutHelp()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/shortcuts");
            if (command != null)
                ShowLocalCommandResult(command, CopilotKeyboardShortcutHelp.Format());
        }

        private void ShowContextDiagnosticsFromUi()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/context");
            if (command != null)
                ShowLocalCommandResult(command, BuildContextDiagnosticsReport());
        }

        private void ShowUsageDiagnosticsFromUi()
        {
            var command = CopilotLocalCommandCatalog.FindExact("/usage");
            if (command == null)
                return;

            ShowLocalCommandResult(
                command,
                CopilotUsageCommand.Format(
                    SelectedConversation,
                    Conversations,
                    DateTimeOffset.Now,
                    "session",
                    CopilotProviderRateLimitTracker.GetSnapshot(SelectedProfile?.Id)));
        }

        private string BuildContextDiagnosticsReport()
        {
            var mode = ResolveComposerRequestMode();
            var agentContextEnabled = mode != CopilotAgentMode.Chat;
            var selectedProfile = SelectedProfile;
            var conversation = SelectedConversation;
            var requestProfile = selectedProfile == null
                ? null
                : CreateConversationRequestProfile(selectedProfile, conversation);
            var historyLimits = ResolveConversationHistoryLimits(requestProfile);
            var history = CopilotConversationRequestBuilder.CaptureHistorySelection(conversation, historyLimits);
            var projectInstructions = Array.Empty<CopilotProjectInstructionDocument>();
            var trustedProjectRoots = Array.Empty<string>();
            CopilotAgentSkillUsageSnapshot? skillUsage = null;
            if (agentContextEnabled)
            {
                var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
                trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot).ToArray();
                projectInstructions = CopilotAgentProjectInstructions.Discover(
                    trustedProjectRoots,
                    turnSnapshot.ActiveDocumentPath,
                    turnSnapshot.Attachments
                        .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                        .Select(attachment => attachment.Value))
                    .ToArray();
                skillUsage = CopilotAgentSkillUsageStore.Shared.GetSnapshot();
            }

            var capabilitySnapshot = CopilotCapabilityCatalog.Shared.GetSnapshot();
            var agentExtensionSnapshot = CopilotAgentExtensionBridge.Shared.GetSnapshot();
            var toolHookSurface = CopilotToolExecutor.GetSharedHookSurfaceSnapshot();
            var agentDefaults = _config.AgentDefaults;
            var retainedHistoryWeight = history.Messages.Sum(message => CopilotTokenEstimator.EstimateTextWeight(message.Content));
            var compaction = conversation?.Compaction;
            return CopilotContextDiagnostics.Format(new CopilotContextDiagnosticSnapshot
            {
                ProfileLabel = requestProfile?.DisplayLabel ?? string.Empty,
                Mode = mode,
                ResponsePersonality = conversation?.ResponsePersonality ?? CopilotResponsePersonality.None,
                SystemPromptCharacters = requestProfile?.EffectiveSystemPrompt.Length ?? 0,
                SourceHistoryMessages = history.SourceMessageCount,
                RetainedHistoryMessages = history.Messages.Length,
                SourceHistoryCharacters = history.SourceCharacters,
                RetainedHistoryCharacters = history.RetainedCharacters,
                RetainedHistoryEstimatedTokens = history.Messages.Length == 0
                    ? 0
                    : CopilotTokenEstimator.WeightToTokenEstimate(retainedHistoryWeight),
                HistoryMaximumMessages = historyLimits.MaximumMessages,
                HistoryMaximumCharacters = historyLimits.MaximumCharacters,
                HistoryMaximumContentCharacters = historyLimits.MaximumContentCharacters,
                HistoryMaximumEstimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(historyLimits.MaximumCharacters),
                HistoryMaximumContentEstimatedTokens = CopilotTokenEstimator.WeightToTokenEstimate(historyLimits.MaximumContentCharacters),
                HistoryContextWindowTokens = agentDefaults.ContextWindowTokens,
                AutoCompactConversationHistory = agentDefaults.AutoCompactConversationHistory,
                AutoCompactThresholdPercent = agentDefaults.AutoCompactThresholdPercent,
                AutoCompactInstructionsCharacters = agentDefaults.AutoCompactInstructions.Length,
                CompactedSourceMessages = compaction?.SourceMessageCount ?? 0,
                CompactionSummaryCharacters = compaction?.Summary.Length ?? 0,
                ConversationGoalCharacters = conversation?.Goal?.Objective.Length ?? 0,
                ConversationGoalActive = conversation?.Goal?.IsActive == true,
                ConversationGoalAchieved = conversation?.Goal?.IsAchieved == true,
                AttachmentCount = Attachments.Count,
                FileAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.File),
                ImageAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.Image),
                WebAttachmentCount = Attachments.Count(item => item.Type == CopilotAttachmentType.WebPage),
                HasLiveWindowContext = HasCurrentLiveContext,
                AgentContextEnabled = agentContextEnabled,
                ProjectInstructionDocuments = projectInstructions.Length,
                ProjectInstructionPromptCharacters = CopilotAgentProjectInstructions.BuildPromptBlock(projectInstructions).Length,
                TrustedProjectRootPaths = trustedProjectRoots,
                ProjectInstructions = projectInstructions,
                RecordedSkillRuns = skillUsage?.RecordedRuns ?? 0,
                TrackedSkills = skillUsage?.Entries.Count ?? 0,
                HistoricalExplicitOnlySkills = skillUsage?.HistoricalExplicitOnlySkills.Count ?? 0,
                ManualSkillOverrides = agentDefaults.SkillOverrides.Count,
                SkillMetadataCharacterBudget = CopilotAgentSkills.ResolveMetadataCharacterBudget(
                    agentDefaults.ContextWindowTokens),
                AgentContextWindowTokens = agentDefaults.ContextWindowTokens,
                AgentRequestTokenBudget = agentDefaults.RequestTokenBudget,
                AgentMaxToolCalls = agentDefaults.MaxToolCalls,
                AgentMaxPasses = agentDefaults.MaxAgentPasses,
                AgentTimeoutSeconds = agentDefaults.TimeoutSeconds,
                RegisteredCapabilities = capabilitySnapshot.Capabilities.Count,
                EnabledExternalMcpServers = _config.ExternalMcpServers.Count(server => server?.Enabled == true),
                ToolHookSurface = toolHookSurface,
                AgentExtensions = agentExtensionSnapshot.Sources,
                AgentExtensionIssues = agentExtensionSnapshot.Issues,
            });
        }

        private void HandleProjectInstructionCommand(
            CopilotLocalCommand command,
            string arguments)
        {
            var request = CopilotProjectInstructionDiagnostics.ParseCommand(arguments);
            var snapshot = CaptureProjectInstructionSnapshot();
            if (request.Action == CopilotProjectInstructionCommandAction.List)
            {
                ShowLocalCommandResult(
                    command,
                    CopilotProjectInstructionDiagnostics.Format(
                        snapshot,
                        ActiveHostedRun?.IsAgent == true));
                return;
            }
            if (request.Action == CopilotProjectInstructionCommandAction.Invalid)
            {
                ShowLocalCommandResult(command, CopilotProjectInstructionDiagnostics.Usage);
                return;
            }

            var document = CopilotProjectInstructionDiagnostics.FindByPosition(
                snapshot.Documents,
                request.Position);
            if (document == null)
            {
                ShowLocalCommandResult(
                    command,
                    $"当前生效项目指令中没有 #{request.Position:N0}。输入 /memory 查看实时顺序；目标文件或规则可能已变化。");
                return;
            }

            var errorMessage = string.Empty;
            if (!CopilotLocalFileLinkNavigator.TryResolve(document.Path, out var target)
                || !CopilotLocalFileLinkNavigator.TryOpen(target, out errorMessage))
            {
                ShowLocalCommandResult(
                    command,
                    string.IsNullOrWhiteSpace(errorMessage)
                        ? "该指令文件已不存在、不在当前工作区内，或当前没有可用编辑器。"
                        : CopilotUserFacingErrorFormatter.Sanitize(errorMessage));
                return;
            }

            ShowLocalCommandResult(
                command,
                $"已在内置编辑器中打开 #{request.Position:N0} · {Path.GetFileName(document.Path)}。"
                + Environment.NewLine
                + (ActiveHostedRun?.IsAgent == true
                    ? "当前运行中的任务仍使用请求启动时捕获的指令快照；保存后的内容从后续请求开始生效。"
                    : "保存后的内容会在下一次需要工作区证据的 Agent 请求启动时重新发现并加载。"));
        }

        private CopilotProjectInstructionSnapshot CaptureProjectInstructionSnapshot()
        {
            var turnSnapshot = CaptureHostedTurnSnapshot(Attachments);
            var trustedProjectRoots = CopilotAgentRequestFactory.BuildTrustedProjectRootPaths(turnSnapshot);
            var documents = CopilotAgentProjectInstructions.Discover(
                trustedProjectRoots,
                turnSnapshot.ActiveDocumentPath,
                turnSnapshot.Attachments
                    .Where(attachment => attachment.Type == CopilotAttachmentType.File)
                    .Select(attachment => attachment.Value));
            return new CopilotProjectInstructionSnapshot(
                trustedProjectRoots.Count > 0
                    ? trustedProjectRoots[0]
                    : turnSnapshot.SolutionDirectoryPath,
                turnSnapshot.ActiveDocumentPath,
                documents);
        }

        private void DismissLocalCommandResult()
        {
            LocalCommandResultTitle = string.Empty;
            LocalCommandResultText = string.Empty;
        }

        private void RunUiOperation(Func<Task> operation, string operationName, Action<string>? onError = null)
        {
            CopilotUiTaskObserver.Run(
                operation,
                operationName,
                onError ?? (message =>
                {
                    LocalCommandResultTitle = operationName + " · 失败";
                    LocalCommandResultText = message;
                }));
        }

    }
}
