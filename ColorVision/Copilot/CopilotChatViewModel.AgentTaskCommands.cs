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
        private bool CanContinueAgentTasks(CopilotChatMessage? message)
        {
            if (IsEditingMessage || !CanScheduleComposerRequest(CopilotAgentMode.Auto) || message == null || message.IsUser || !message.HasRecoverableAgentTasks)
                return false;
            if (SelectedConversation?.AgentSessionCheckpoint == null || SelectedProfile?.IsConfigured != true)
                return false;

            var latestAssistant = SelectedConversation.Messages.LastOrDefault(candidate => !candidate.IsUser);
            if (!ReferenceEquals(latestAssistant, message))
                return false;

            return CopilotAgentRecoveryPolicy.Evaluate(
                message,
                SelectedConversation.AgentSessionCheckpoint,
                CreateCurrentConversationRequestProfile(SelectedProfile, SelectedConversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                    _currentCodexConfigOptions.ConfiguredHooksEnabled,
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled)).IsAvailable;
        }

        private void ContinueAgentTasks(CopilotChatMessage? message)
        {
            TryContinueAgentTasks(message);
        }

        private bool TryContinueAgentTasks(CopilotChatMessage? message)
        {
            if (!CanContinueAgentTasks(message))
                return false;

            var conversation = SelectedConversation!;
            var profile = SelectedProfile!;
            var decision = CopilotAgentRecoveryPolicy.Evaluate(
                message,
                conversation.AgentSessionCheckpoint,
                CreateCurrentConversationRequestProfile(profile, conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                    _currentCodexConfigOptions.ConfiguredHooksEnabled,
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled));
            if (!decision.IsAvailable)
                return false;

            SetPendingRequestModeOverride(CopilotAgentMode.Auto);
            InputText = decision.UserMessage;
            _pendingAgentRecoveryRequest = new PendingAgentRecoveryRequest(
                conversation.Id, decision.UserMessage, decision.Request!);
            RunUiOperation(SendAsync, "继续 Agent 任务");
            return true;
        }

        private bool CanExecuteApprovedPlan(CopilotChatMessage? message)
        {
            return CanUseCompletedPlan(message, CopilotAgentMode.Auto)
                && CopilotPlanHandoff.TryCreateExecutionRequest(message, out _);
        }

        private void ExecuteApprovedPlan(CopilotChatMessage? message)
        {
            if (!CanExecuteApprovedPlan(message)
                || !CopilotPlanHandoff.TryCreateExecutionRequest(message, out var request))
            {
                return;
            }

            RunUiOperation(
                () => SendAsync(request.VisiblePrompt, CopilotAgentMode.Auto, request.ModelPrompt),
                "执行批准的计划");
        }

        private bool CanContinuePlanning(CopilotChatMessage? message)
        {
            return CanUseCompletedPlan(message, CopilotAgentMode.Plan);
        }

        private void ContinuePlanning(CopilotChatMessage? message)
        {
            if (!CanContinuePlanning(message))
                return;

            SetPendingRequestModeOverride(CopilotAgentMode.Plan);
            if (IsInputEmpty)
                InputText = CopilotPlanHandoff.ContinuePlanningPrompt;
        }

        private bool CanUseCompletedPlan(CopilotChatMessage? message, CopilotAgentMode nextMode)
        {
            if (IsEditingMessage
                || SelectedProfile?.IsConfigured != true
                || !CanScheduleComposerRequest(nextMode)
                || message?.HasCompletedPlan != true
                || SelectedConversation == null)
            {
                return false;
            }

            var latestAssistant = SelectedConversation.Messages.LastOrDefault(candidate => !candidate.IsUser);
            return ReferenceEquals(latestAssistant, message);
        }

        private bool CanRequestWorkspaceRollback(CopilotAgentTraceEntry? trace)
        {
            return trace?.CanRequestWorkspaceRollback == true
                && !IsBusy
                && !IsEditingMessage
                && SelectedConversation?.Messages.Any(message => message.AgentTraceEntries.Contains(trace)) == true
                && !HasActiveWorkspaceRollback(trace.WorkspaceChangeSetId);
        }

        private void RequestWorkspaceRollback(CopilotAgentTraceEntry? trace)
        {
            if (trace?.CanRequestWorkspaceRollback != true)
            {
                LocalCommandResultTitle = "无法撤销文件修改";
                LocalCommandResultText = "这次修改的安全回滚记录已失效、已被使用，或与当前会话及工作区状态不再匹配。";
                return;
            }

            RunUiOperation(
                () => RequestWorkspaceRollbackAsync(trace),
                "撤销文件修改");
        }

        private void RollbackWorkspaceFromCommand(
            CopilotLocalCommand command,
            string requestedOrdinal)
        {
            var conversation = SelectedConversation;
            if (conversation == null || IsBusy || IsEditingMessage)
            {
                ShowLocalCommandResult(command, "当前状态不能撤销文件修改；请先结束正在运行的请求或消息编辑。");
                return;
            }

            if (string.IsNullOrWhiteSpace(requestedOrdinal))
            {
                ShowLocalCommandResult(command, CopilotWorkspaceRollbackPointService.Format(conversation));
                return;
            }
            if (!CopilotWorkspaceRollbackPointService.TryResolve(
                    conversation,
                    requestedOrdinal,
                    out var point))
            {
                ShowLocalCommandResult(
                    command,
                    "回滚序号必须对应一组仍有效的精确文件修改，例如 /rollback 1。输入 /rollback 可查看可用回滚点。");
                return;
            }
            if (!CanRequestWorkspaceRollback(point.Trace))
            {
                ShowLocalCommandResult(
                    command,
                    "这组文件修改正在回滚，或其安全回滚记录刚刚失效；未创建重复请求。");
                return;
            }

            RequestWorkspaceRollback(point.Trace);
        }

        private async Task RequestWorkspaceRollbackAsync(CopilotAgentTraceEntry trace)
        {
            var conversation = SelectedConversation;
            var assistantMessage = conversation?.Messages.FirstOrDefault(message =>
                message.AgentTraceEntries.Contains(trace));
            if (conversation == null || assistantMessage == null)
            {
                LocalCommandResultTitle = "无法撤销文件修改";
                LocalCommandResultText = "这条修改记录不属于当前会话，未创建回滚请求。";
                return;
            }

            var workspacePath = CaptureHostedTurnSnapshot(conversation.Attachments).SolutionDirectoryPath;
            var result = await _turnRuntime.RequestWorkspaceRollbackAsync(
                new CopilotWorkspaceRollbackActionRequest(
                    conversation.Id,
                    workspacePath,
                    trace.WorkspaceChangeSetId),
                agentEvent => ApplyDirectWorkspaceRollbackEvent(
                    conversation,
                    assistantMessage,
                    trace.WorkspaceChangeSetId,
                    agentEvent),
                CancellationToken.None);
            if (!result.Success || result.Action == null)
            {
                LocalCommandResultTitle = "无法撤销文件修改";
                LocalCommandResultText = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "安全回滚请求未能创建。"
                    : result.ErrorMessage;
                return;
            }

            SetPendingActionFeedback("已创建精确绑定的工作区回滚审批；无需再次调用模型。");
            await ApprovePendingActionAsync(result.Action);
        }

        private void ApplyDirectWorkspaceRollbackEvent(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string changeSetId,
            CopilotAgentEvent agentEvent)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                var presentationResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(
                    assistantMessage,
                    agentEvent);
                var rolledBack = agentEvent.Type == CopilotAgentEventType.ToolResult
                    && agentEvent.ToolResult?.Success == true
                    && string.Equals(
                        agentEvent.ToolExecution?.ToolName,
                        "RollbackWorkspacePatchEnvelope",
                        StringComparison.Ordinal)
                    && conversation.MarkWorkspaceChangeSetRolledBack(changeSetId);
                if (rolledBack
                    || presentationResult.PersistenceMode != CopilotAgentEventPersistenceMode.None)
                {
                    PersistState(immediate: rolledBack);
                }
                CommandManager.InvalidateRequerySuggested();
            });
        }

        private bool HasActiveWorkspaceRollback(string changeSetId)
        {
            if (string.IsNullOrWhiteSpace(changeSetId) || SelectedConversation == null)
                return false;

            return SelectedConversation.Messages
                .SelectMany(message => message.AgentTraceEntries)
                .Any(entry =>
                    string.Equals(
                        entry.ToolName,
                        "RollbackWorkspacePatchEnvelope",
                        StringComparison.Ordinal)
                    && string.Equals(
                        entry.WorkspaceChangeSetId,
                        changeSetId,
                        StringComparison.Ordinal)
                    && entry.State is CopilotToolExecutionState.Pending
                        or CopilotToolExecutionState.Running
                        or CopilotToolExecutionState.AwaitingApproval);
        }

        private static bool CanOpenWorkspaceChangeFile(CopilotWorkspaceChangeFile? file)
        {
            return file != null && CopilotLocalFileLinkNavigator.TryResolve(file.FilePath, out _);
        }

        private void OpenWorkspaceChangeFile(CopilotWorkspaceChangeFile? file)
        {
            var errorMessage = string.Empty;
            if (file != null
                && CopilotLocalFileLinkNavigator.TryResolve(file.FilePath, out var target)
                && CopilotLocalFileLinkNavigator.TryOpen(target, out errorMessage))
            {
                return;
            }

            LocalCommandResultTitle = "无法打开修改文件";
            LocalCommandResultText = string.IsNullOrWhiteSpace(errorMessage)
                ? "文件已不存在或不在当前工作区内。"
                : CopilotUserFacingErrorFormatter.Sanitize(errorMessage);
        }

        private void OpenAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task == null || !CanSwitchConversation || !Conversations.Contains(task.Conversation))
                return;

            SelectConversation(task.Conversation, persist: true, preferredProfileId: task.Conversation.ProfileId);
        }

        private void ToggleAgentTaskPanel()
        {
            if (!HasAgentTasks)
                return;

            _state.ToggleAgentTaskPanelExpanded();
            OnPropertyChanged(nameof(IsAgentTaskPanelExpanded));
            OnPropertyChanged(nameof(IsAgentTaskListVisible));
            OnPropertyChanged(nameof(AgentTaskPanelToggleGlyph));
            OnPropertyChanged(nameof(AgentTaskPanelToolTip));
            PersistState();
        }

        private void ChangeMessageTimestampVisibility(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotMessageTimestampPreference.TryResolve(
                    arguments,
                    ShowMessageTimestamps,
                    out var show))
            {
                ShowLocalCommandResult(command, CopilotMessageTimestampPreference.Usage);
                return;
            }

            if (_state.SetShowMessageTimestamps(show))
            {
                OnPropertyChanged(nameof(ShowMessageTimestamps));
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                $"消息时间戳已{(show ? "显示" : "隐藏")}。\n\n"
                + "该偏好只改变本地界面，不修改聊天内容，也不调用模型或工具。");
        }

        private void ChangePromptSuggestionPreference(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotPromptSuggestionPreference.TryResolve(
                    arguments,
                    PromptHistoryCompletionsEnabled,
                    out var enabled))
            {
                ShowLocalCommandResult(command, CopilotPromptSuggestionPreference.Usage);
                return;
            }

            if (_state.SetEnablePromptHistoryCompletions(enabled))
            {
                OnPropertyChanged(nameof(PromptHistoryCompletionsEnabled));
                NotifyPromptHistoryPrefixCompletionChanged();
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                $"本地历史提示补全已{(enabled ? "开启" : "关闭")}。\n\n"
                + "该偏好只控制当前设备上的输入提示；不会调用模型，不会修改或删除历史消息。");
        }

        private void ChangeCompactMessageLayout(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotCompactMessageLayout.TryResolvePreference(
                    arguments,
                    UseCompactMessageLayout,
                    out var useCompactLayout))
            {
                ShowLocalCommandResult(command, CopilotCompactMessageLayout.Usage);
                return;
            }

            if (_state.SetUseCompactMessageLayout(useCompactLayout))
            {
                OnPropertyChanged(nameof(UseCompactMessageLayout));
                OnPropertyChanged(nameof(MessageListPadding));
                OnPropertyChanged(nameof(MessageItemMargin));
                OnPropertyChanged(nameof(UserMessagePadding));
                OnPropertyChanged(nameof(AssistantActionsMargin));
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                $"消息布局已切换为{(useCompactLayout ? "紧凑" : "标准")}间距。\n\n"
                + "该偏好只改变本地消息密度；不会压缩会话上下文，也不调用模型或工具。");
        }

        private void ChangeMultilineComposerPreference(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotMultilineComposerPreference.TryResolve(
                    arguments,
                    UseMultilineComposer,
                    out var enabled))
            {
                ShowLocalCommandResult(command, CopilotMultilineComposerPreference.Usage);
                return;
            }

            if (_state.SetUseMultilineComposer(enabled))
            {
                OnPropertyChanged(nameof(UseMultilineComposer));
                OnPropertyChanged(nameof(ComposerInputToolTip));
                OnPropertyChanged(nameof(InputPlaceholder));
                OnPropertyChanged(nameof(SteerActionToolTip));
                OnPropertyChanged(nameof(QueueFollowUpToolTip));
                OnPropertyChanged(nameof(FollowUpQueueHintText));
                PersistState(immediate: true);
            }

            ShowLocalCommandResult(
                command,
                enabled
                    ? "多行输入模式已开启：Enter 插入换行，Shift+Enter 发送；Ctrl+Enter 空闲时发送、Agent 运行中立即接管。\n\n该偏好只改变当前设备的输入按键，不修改消息、模型或权限。"
                    : "多行输入模式已关闭：Enter 发送，Shift+Enter 插入换行；Ctrl+Enter 空闲时发送、Agent 运行中立即接管。\n\n该偏好只改变当前设备的输入按键，不修改消息、模型或权限。");
        }

        private void ChangeFollowUpBehavior(CopilotLocalCommand command, string arguments)
        {
            if (!CopilotFollowUpPreference.TryResolve(
                    arguments,
                    DefaultFollowUpBehavior,
                    out var behavior))
            {
                ShowLocalCommandResult(command, CopilotFollowUpPreference.Usage);
                return;
            }

            if (_state.SetDefaultFollowUpBehavior(behavior))
            {
                OnPropertyChanged(nameof(DefaultFollowUpBehavior));
                OnPropertyChanged(nameof(InputPlaceholder));
                OnPropertyChanged(nameof(SteerActionToolTip));
                OnPropertyChanged(nameof(QueueFollowUpToolTip));
                OnPropertyChanged(nameof(FollowUpQueueHintText));
                PersistState(immediate: true);
            }

            var primaryAction = behavior == CopilotFollowUpBehavior.Queue
                ? "排到当前任务完成后的下一轮"
                : "加入当前 Agent 运行并调整方向";
            var alternateAction = behavior == CopilotFollowUpBehavior.Queue
                ? "调整当前 Agent 运行"
                : "排到当前任务完成后的下一轮";
            ShowLocalCommandResult(
                command,
                $"运行期间的默认后续行为已设为：{ComposerSubmitShortcutLabel} {primaryAction}；Tab {alternateAction}。\n\n"
                + "该偏好只影响普通后续消息；澄清问题答案和本地 / 命令仍按各自语义执行。");
        }

        private bool CanResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task?.CanResume != true
                || !Conversations.Contains(task.Conversation)
                || !CanScheduleConversationRequest(task.Conversation.Id, CopilotAgentMode.Auto))
                return false;

            var profile = ResolveProfile(task.Conversation.ProfileId);
            return profile?.IsConfigured == true && CopilotAgentRecoveryPolicy.Evaluate(
                task.Message,
                task.Conversation.AgentSessionCheckpoint,
                CreateCurrentConversationRequestProfile(profile, task.Conversation),
                CopilotCapabilityCatalog.Shared.GetSnapshot(
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled),
                CopilotToolExecutor.GetSharedHookSurfaceSnapshot(
                    _currentCodexConfigOptions.ConfiguredHooksEnabled,
                    _currentCodexConfigOptions.ConfiguredPluginsEnabled)).IsAvailable;
        }

        private void ResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            TryResumeAgentTask(task);
        }

        private bool TryResumeAgentTask(CopilotAgentTaskSummary? task)
        {
            if (!CanResumeAgentTask(task) || task == null)
                return false;

            SelectConversation(task.Conversation, persist: true, preferredProfileId: task.Conversation.ProfileId);
            if (!ReferenceEquals(SelectedConversation, task.Conversation))
                return false;

            return TryContinueAgentTasks(task.Message);
        }

        private void DismissAgentTask(CopilotAgentTaskSummary? task)
        {
            TryDismissAgentTask(task);
        }

        private bool TryDismissAgentTask(CopilotAgentTaskSummary? task)
        {
            if (task == null || IsBusy || !Conversations.Contains(task.Conversation))
                return false;

            if (MessageBox.Show(
                Application.Current.GetActiveWindow(),
                task.DismissConfirmationText,
                "ColorVision",
                MessageBoxButton.YesNo,
                MessageBoxImage.Question) != MessageBoxResult.Yes)
            {
                return false;
            }

            if (!CopilotAgentTaskIndex.Dismiss(task))
                return false;
            if (ReferenceEquals(task.Conversation, SelectedConversation))
                PublishSelectedTaskEventJournal();
            PersistState();
            RefreshAgentTasks();
            RefreshConversationActivityView();
            return true;
        }

        private sealed record PendingAgentRecoveryRequest(
            string ConversationId,
            string Prompt,
            CopilotAgentRecoveryRequest Request);

        private CopilotAgentRecoveryRequest? CapturePendingAgentRecoveryRequest(CopilotComposerCaptureSnapshot composer)
        {
            var pending = _pendingAgentRecoveryRequest;
            if (pending != null
                && string.Equals(pending.ConversationId, composer.ConversationId, StringComparison.Ordinal)
                && string.Equals(pending.Prompt, composer.Text, StringComparison.Ordinal))
            {
                return pending.Request;
            }

            _pendingAgentRecoveryRequest = null;
            return null;
        }

        private void CommitPendingAgentRecoveryRequest(CopilotAgentRecoveryRequest? captured)
        {
            if (ReferenceEquals(_pendingAgentRecoveryRequest?.Request, captured))
                _pendingAgentRecoveryRequest = null;
        }
    }
}
