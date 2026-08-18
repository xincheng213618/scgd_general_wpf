#pragma warning disable CA1001,CA1822,CA1859,CA1861,CA1870,CS4014
using System;
using System.Linq;
using System.Threading;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotChatViewModel
    {
        private void TaskHost_Changed(object? sender, CopilotAgentTaskHostChangedEventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => TaskHost_Changed(sender, e)));
                return;
            }

            IsBusy = _taskHost.IsActive;
            if (e.Kind == CopilotAgentTaskHostChangeKind.ControlRequested
                && e.Run.HasStarted
                && e.Run.IsAgent
                && e.Run.State == CopilotHostedRunState.CancelRequested
                && e.Run.RunControl?.Intent == CopilotAgentControlIntent.Cancel)
            {
                var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, e.Run.ConversationId, StringComparison.Ordinal));
                if (conversation?.AgentSessionCheckpoint != null)
                {
                    conversation.CompleteOpenAgentRun(
                        CopilotAgentStopReason.Cancelled,
                        CopilotAgentControlIntent.Cancel);
                    PersistState(immediate: true);
                }
            }
            if (e.Kind == CopilotAgentTaskHostChangeKind.Completed)
            {
                CaptureCompletedAgentRunNotice(e.Run);
                RefreshAgentTasks();
            }
            var queueChange = _followUpQueue.HandleTaskHostChanged(e);
            if (queueChange.RecoveryChanged)
                PersistState(immediate: true);
            NotifyHostedRunStateChanged();
            CommandManager.InvalidateRequerySuggested();
        }

        private void NotifyHostedRunStateChanged()
        {
            RefreshConversationRunStatuses();
            OnPropertyChanged(nameof(CanSwitchConversation));
            OnPropertyChanged(nameof(IsAnsweringUserQuestion));
            OnPropertyChanged(nameof(CanSubmitUserQuestionAnswer));
            OnPropertyChanged(nameof(CanSteerCurrentRun));
            OnPropertyChanged(nameof(CanQueueCurrentRunFollowUp));
            OnPropertyChanged(nameof(PrimaryActionGlyph));
            OnPropertyChanged(nameof(PrimaryActionToolTip));
            OnPropertyChanged(nameof(InputPlaceholder));
            OnPropertyChanged(nameof(LocalCommandSuggestionHeader));
            RefreshLocalCommandSuggestions();
            RefreshAgentRunNotice();
        }

        private void NotifyUserQuestionStateChanged()
        {
            RefreshConversationRunStatuses();
            OnPropertyChanged(nameof(IsAnsweringUserQuestion));
            OnPropertyChanged(nameof(CanSubmitUserQuestionAnswer));
            OnPropertyChanged(nameof(CanSteerCurrentRun));
            OnPropertyChanged(nameof(CanQueueCurrentRunFollowUp));
            OnPropertyChanged(nameof(InputPlaceholder));
            OnPropertyChanged(nameof(LocalCommandSuggestionHeader));
            RefreshLocalCommandSuggestions();
            CommandManager.InvalidateRequerySuggested();
        }

        private void RefreshConversationRunStatuses()
        {
            var activeRun = ActiveHostedRun;
            var activeNeedsInput = activeRun?.IsAgent == true
                && (ActiveUserQuestion?.IsPending == true
                    || _approvalCoordinator.HasPendingActionsForConversation(activeRun.ConversationId));
            CopilotAgentRunStatusSynchronizer.Refresh(
                Conversations,
                activeRun?.IsAgent == true ? activeRun.ConversationId : null,
                activeRun?.IsAgent == true ? activeRun.State : null,
                activeNeedsInput,
                _taskHost.QueuedRuns.Where(run => run.IsAgent).Select(run => run.ConversationId).ToArray());
            RefreshConversationActivityView();
        }

        private void RefreshAgentRunNotice()
        {
            var selectedRun = SelectedHostedRun;
            if (selectedRun?.State == CopilotHostedRunState.Queued)
            {
                var position = _taskHost.GetQueuePosition(selectedRun.Id);
                _agentRunNoticeConversationId = selectedRun.ConversationId;
                AgentRunNoticeText = position > 0
                    ? $"Agent 已排队 · 前面 {position} 个任务"
                    : "Agent 已排队";
                return;
            }

            var run = ActiveHostedRun;
            if (run?.IsAgent == true
                && !string.Equals(run.ConversationId, SelectedConversation?.Id, StringComparison.Ordinal))
            {
                var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, run.ConversationId, StringComparison.Ordinal));
                if (conversation == null)
                {
                    ClearAgentRunNotice();
                    return;
                }

                _agentRunNoticeConversationId = conversation.Id;
                var status = run.State switch
                {
                    CopilotHostedRunState.Queued => "已排队",
                    CopilotHostedRunState.PauseRequested => "正在暂停",
                    CopilotHostedRunState.CancelRequested => "正在取消",
                    _ => "正在运行",
                };
                AgentRunNoticeText = $"{conversation.Title} · {status}";
                return;
            }

            if (string.Equals(
                    _completedAgentRunNoticeConversationId,
                    SelectedConversation?.Id,
                    StringComparison.Ordinal))
            {
                ClearCompletedAgentRunNotice();
            }
            if (!string.IsNullOrWhiteSpace(_completedAgentRunNoticeConversationId))
            {
                var completedConversation = Conversations.FirstOrDefault(item => string.Equals(
                    item.Id,
                    _completedAgentRunNoticeConversationId,
                    StringComparison.Ordinal));
                if (completedConversation != null)
                {
                    _agentRunNoticeConversationId = completedConversation.Id;
                    AgentRunNoticeText = _completedAgentRunNoticeText;
                    return;
                }

                ClearCompletedAgentRunNotice();
            }

            ClearAgentRunNotice();
        }

        private void CaptureCompletedAgentRunNotice(CopilotHostedAgentRun run)
        {
            var conversation = Conversations.FirstOrDefault(item =>
                string.Equals(item.Id, run.ConversationId, StringComparison.Ordinal));
            var activity = CopilotAgentRunActivityPolicy.CreateCompletionActivity(run, conversation);
            var isSelectedConversation = conversation != null
                && string.Equals(conversation.Id, SelectedConversation?.Id, StringComparison.Ordinal);
            var visibleActivity = activity?.State == CopilotConversationActivityState.NeedsInput
                    || !isSelectedConversation
                ? activity
                : null;
            if (conversation?.ReplaceAgentActivity(visibleActivity) == true)
                PersistState(immediate: true);

            var notice = CopilotAgentRunCompletionNoticePolicy.Create(
                run,
                conversation,
                SelectedConversation?.Id);
            if (notice == null)
                return;

            _completedAgentRunNoticeConversationId = notice.ConversationId;
            _completedAgentRunNoticeText = notice.Text;
        }

        private void CaptureSubagentCompletionNotice(
            CopilotConversationRecord conversation,
            CopilotToolResult result)
        {
            var snapshot = CopilotSubagentCompletionNoticePolicy.CreateSnapshot(
                result,
                conversation.Id);
            if (snapshot == null)
                return;
            if (!_completionNoticeCenter.CaptureSubagent(
                    snapshot,
                    conversation,
                    SelectedConversation?.Id))
            {
                return;
            }

            RefreshCompletionNotice();
        }

        private void RefreshCompletionNotice()
        {
            var notice = _completionNoticeCenter.GetCurrent(
                Conversations,
                SelectedConversation?.Id);
            if (notice == null)
            {
                ClearCompletionNotice();
                return;
            }

            _completionNotice = notice;
            CompletionNoticeText = notice.Text;
            CommandManager.InvalidateRequerySuggested();
        }

        private void OpenCompletionNotice()
        {
            var notice = _completionNotice;
            if (notice == null)
                return;

            var conversation = Conversations.FirstOrDefault(item => string.Equals(
                item.Id,
                notice.ConversationId,
                StringComparison.Ordinal));
            if (conversation == null)
            {
                _completionNoticeCenter.Acknowledge(
                    notice.Kind,
                    notice.ConversationId,
                    notice.ItemId);
                RefreshCompletionNotice();
                return;
            }
            if (!ReferenceEquals(conversation, SelectedConversation)
                && !CanSwitchConversation)
            {
                return;
            }
            if (!ReferenceEquals(conversation, SelectedConversation))
            {
                SelectConversation(
                    conversation,
                    persist: true,
                    preferredProfileId: conversation.ProfileId);
            }

            switch (notice.Kind)
            {
                case CopilotCompletionNoticeKind.Subagent:
                    ShowSubagentCompletionNoticeDetails(conversation, notice.ItemId);
                    break;
                case CopilotCompletionNoticeKind.BackgroundCommand:
                    ShowBackgroundCommandCompletionNoticeDetails(conversation, notice.ItemId);
                    break;
            }

            _completionNoticeCenter.Acknowledge(
                notice.Kind,
                notice.ConversationId,
                notice.ItemId);
            RefreshCompletionNotice();
        }

        private bool CanOpenCompletionNotice()
        {
            var notice = _completionNotice;
            return notice != null
                && (string.Equals(
                        notice.ConversationId,
                        SelectedConversation?.Id,
                        StringComparison.Ordinal)
                    || CanSwitchConversation);
        }

        private void ShowSubagentCompletionNoticeDetails(
            CopilotConversationRecord conversation,
            string runId)
        {
            var command = CopilotLocalCommandCatalog.FindExact("/agents");
            if (command == null)
                return;

            ShowLocalCommandResult(
                command,
                CopilotSubagentDiagnostics.Format(conversation, "show " + runId));
        }

        private void ShowBackgroundCommandCompletionNoticeDetails(
            CopilotConversationRecord conversation,
            string backgroundId)
        {
            var snapshots = CopilotBackgroundShellCommandRegistry.Shared.GetSnapshots(
                conversation.Id);
            var indexedSnapshot = snapshots
                .Select((snapshot, index) => new { snapshot, position = index + 1 })
                .FirstOrDefault(item => string.Equals(
                    item.snapshot.Id,
                    backgroundId,
                    StringComparison.Ordinal));
            var command = CopilotLocalCommandCatalog.FindExact("/ps");
            if (command == null)
                return;

            ShowLocalCommandResult(
                command,
                indexedSnapshot != null
                    ? CopilotBackgroundShellCommandDiagnostics.FormatDetails(
                        indexedSnapshot.snapshot,
                        indexedSnapshot.position,
                        DateTimeOffset.UtcNow)
                    : CopilotBackgroundShellCommandDiagnostics.FormatList(
                        conversation,
                        snapshots,
                        DateTimeOffset.UtcNow));
        }

        private void AcknowledgeSubagentCompletionNotice(
            string? conversationId,
            string? runId)
        {
            if (_completionNoticeCenter.Acknowledge(
                    CopilotCompletionNoticeKind.Subagent,
                    conversationId,
                    runId))
            {
                RefreshCompletionNotice();
            }
        }

        private void AcknowledgeSubagentCompletionNotices(string? conversationId)
        {
            if (_completionNoticeCenter.AcknowledgeConversation(
                    CopilotCompletionNoticeKind.Subagent,
                    conversationId))
            {
                RefreshCompletionNotice();
            }
        }

        private void BackgroundShellCommandRegistry_CommandCompleted(
            object? sender,
            CopilotBackgroundShellCommandCompletedEventArgs e)
        {
            HandleBackgroundShellCommandCompletion(e, offerToActiveAgent: true);
        }

        private void HandleBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandCompletedEventArgs e,
            bool offerToActiveAgent)
        {
            if (Volatile.Read(ref _disposeState) == 1)
                return;

            if (offerToActiveAgent
                && !e.TerminalObservationWasPendingAtCompletion)
            {
                _turnRuntime.TryEnqueueBackgroundShellCommandCompletion(
                    e.Snapshot);
            }

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.BeginInvoke(new Action(() =>
                    HandleBackgroundShellCommandCompletion(
                        e,
                        offerToActiveAgent: false)));
                return;
            }

            var conversation = Conversations.FirstOrDefault(item =>
                string.Equals(
                    item.Id,
                    e.Snapshot.ConversationId,
                    StringComparison.Ordinal));
            if (!_completionNoticeCenter.CaptureBackgroundCommand(
                    e.Snapshot,
                    conversation,
                    SelectedConversation?.Id))
            {
                return;
            }

            RefreshCompletionNotice();
        }

        private void BackgroundShellCommandRegistry_OutputMonitorEvent(
            object? sender,
            CopilotBackgroundShellOutputMonitorEventArgs e)
        {
            if (Volatile.Read(ref _disposeState) == 1)
                return;
            _turnRuntime.TryEnqueueBackgroundShellCommandOutput(e);
        }

        private void AcknowledgeBackgroundCommandNotices(string? conversationId)
        {
            if (_completionNoticeCenter.AcknowledgeConversation(
                    CopilotCompletionNoticeKind.BackgroundCommand,
                    conversationId))
            {
                RefreshCompletionNotice();
            }
        }

        private void AcknowledgeCompletionNotices(string? conversationId)
        {
            if (_completionNoticeCenter.AcknowledgeConversation(conversationId))
                RefreshCompletionNotice();
        }

        private void ClearCompletionNotice()
        {
            _completionNotice = null;
            CompletionNoticeText = string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        private void ClearCompletedAgentRunNotice()
        {
            _completedAgentRunNoticeConversationId = string.Empty;
            _completedAgentRunNoticeText = string.Empty;
        }

        private void ClearAgentRunNoticeForConversation(string conversationId)
        {
            if (string.Equals(
                    _completedAgentRunNoticeConversationId,
                    conversationId,
                    StringComparison.Ordinal))
            {
                ClearCompletedAgentRunNotice();
            }
            if (!string.Equals(_agentRunNoticeConversationId, conversationId, StringComparison.Ordinal))
                return;

            ClearAgentRunNotice();
        }

        private void OpenAgentRunNotice()
        {
            var conversation = Conversations.FirstOrDefault(item => string.Equals(item.Id, _agentRunNoticeConversationId, StringComparison.Ordinal));
            if (conversation != null && CanSwitchConversation)
                SelectConversation(conversation, persist: true, preferredProfileId: conversation.ProfileId);

            if (conversation != null
                && string.Equals(
                    conversation.Id,
                    _completedAgentRunNoticeConversationId,
                    StringComparison.Ordinal))
            {
                ClearCompletedAgentRunNotice();
            }
            RefreshAgentRunNotice();
        }

        private void ClearAgentRunNotice()
        {
            _agentRunNoticeConversationId = string.Empty;
            AgentRunNoticeText = string.Empty;
            CommandManager.InvalidateRequerySuggested();
        }

        private void InitializeStateRecoveryNotice()
        {
            if (_stateStore is not CopilotChatStateStore stateStore)
                return;

            var loadNotice = stateStore.LastLoadStatus.Source switch
            {
                CopilotChatStateLoadSource.FutureVersion =>
                    $"会话记录由更高版本创建（Schema {stateStore.LastLoadStatus.SchemaVersion ?? 0}，当前支持 {CopilotChatState.CurrentSchemaVersion}）；"
                    + "当前版本已停止写入以保护历史记录，请更新应用后重新打开。",
                _ when stateStore.IsManagedAttachmentCleanupProtected => "此前的会话状态无法完整恢复；托管附件已保护，自动清理暂停。",
                CopilotChatStateLoadSource.Temporary => "已从写入中断前的临时快照恢复会话。",
                CopilotChatStateLoadSource.Backup => "主会话状态不可用，已从可信备份恢复。",
                CopilotChatStateLoadSource.RecoverySnapshot => "主会话状态和即时备份均不可用，已从较早的恢复快照恢复。",
                CopilotChatStateLoadSource.Unrecoverable => "会话状态无法读取，已打开空会话；可恢复的托管附件不会被自动删除。",
                _ => string.Empty,
            };
            var queuedFollowUpNotice = _state.RecoveredQueuedFollowUpCount > 0
                ? $"已将 {_state.RecoveredQueuedFollowUpCount} 条未执行的排队后续恢复到对应会话草稿。"
                : string.Empty;
            var resumedQueuedFollowUpNotice = _state.ResumedQueuedFollowUpCount > 0
                ? $"已恢复 {_state.ResumedQueuedFollowUpCount} 条排队后续；空闲会话将按原顺序继续。"
                : string.Empty;
            var steeringNotice = _state.RecoveredSteeringCount > 0
                ? $"已将 {_state.RecoveredSteeringCount} 条进程退出前尚未确认送达的运行中指令恢复到对应会话草稿。"
                : string.Empty;
            StateRecoveryNoticeText = string.Join(
                Environment.NewLine,
                new[] { loadNotice, queuedFollowUpNotice, resumedQueuedFollowUpNotice, steeringNotice }
                    .Where(text => !string.IsNullOrWhiteSpace(text)));
            StateRecoveryNoticeToolTip = string.IsNullOrWhiteSpace(StateRecoveryNoticeText)
                ? string.Empty
                : $"{StateRecoveryNoticeText}{Environment.NewLine}{Environment.NewLine}状态目录：{stateStore.StateDirectoryPath}";
        }

        private void ReportStatePersistenceError(Exception exception)
        {
            System.Diagnostics.Trace.TraceError($"Copilot state persistence failed: {exception}");
            if (exception is CopilotChatStateFutureVersionException futureVersionException)
            {
                var futureVersionTooltip =
                    $"磁盘上的会话状态 Schema 为 {futureVersionException.SchemaVersion}，当前版本仅支持到 {futureVersionException.SupportedSchemaVersion}。"
                    + $"{Environment.NewLine}{Environment.NewLine}为避免旧版本覆盖新版本历史记录，本进程已经停止写入会话状态。请更新应用并重新打开。";
                UpdateStatePersistenceNotice("检测到更高版本的会话记录；已停止保存以保护历史记录。", futureVersionTooltip);
                return;
            }

            if (exception is CopilotChatStateSizeLimitException sizeLimitException)
            {
                var actualMegabytes = sizeLimitException.ActualBytes / 1024d / 1024d;
                var maximumMegabytes = sizeLimitException.MaximumBytes / 1024 / 1024;
                var sizeTooltip = $"当前会话状态约 {actualMegabytes:F1} MB，保存上限为 {maximumMegabytes} MB。"
                    + $"{Environment.NewLine}{Environment.NewLine}当前会话仍保留在内存中。请先导出需要保留的旧会话，再删除不再需要的会话，最后点击“重试保存”。";
                UpdateStatePersistenceNotice("会话记录过大，暂时无法保存；请先导出并清理旧会话。", sizeTooltip);
                return;
            }

            var safeError = CopilotUserFacingErrorFormatter.Sanitize(exception.Message);
            var stateDirectory = _stateStore is CopilotChatStateStore stateStore
                ? stateStore.StateDirectoryPath
                : string.Empty;
            var tooltip = "当前会话仍保留在内存中；下一次会话变更或显式刷新会再次尝试保存。";
            if (!string.IsNullOrWhiteSpace(safeError))
                tooltip += $"{Environment.NewLine}{Environment.NewLine}错误：{safeError}";
            if (!string.IsNullOrWhiteSpace(stateDirectory))
                tooltip += $"{Environment.NewLine}{Environment.NewLine}状态目录：{stateDirectory}";

            UpdateStatePersistenceNotice("会话保存失败；请暂时不要关闭程序，Copilot 将在下一次变更时重试。", tooltip);
        }

        private void ReportStatePersistenceSuccess() => UpdateStatePersistenceNotice(string.Empty, string.Empty);

        private void UpdateStatePersistenceNotice(string text, string tooltip)
        {
            if (Volatile.Read(ref _disposeState) == 1)
                return;

            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                if (!dispatcher.HasShutdownStarted && !dispatcher.HasShutdownFinished)
                    dispatcher.BeginInvoke(new Action(() => UpdateStatePersistenceNotice(text, tooltip)));
                return;
            }

            StatePersistenceNoticeText = text;
            StatePersistenceNoticeToolTip = tooltip;
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
