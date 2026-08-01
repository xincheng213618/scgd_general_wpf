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
        private void ExecutePrimaryAction()
        {
            if (IsPromptHistorySearchOpen)
            {
                TryCompletePromptHistorySearch();
                return;
            }
            if (_isCompactingConversation)
            {
                _compactConversationCts?.RequestCancellation();
                return;
            }
            if (_fileAttachmentCts != null)
            {
                _fileAttachmentCts.RequestCancellation();
                return;
            }
            if (_webPageAttachmentCts != null)
            {
                _webPageAttachmentCts.RequestCancellation();
                return;
            }
            if (IsViewingQueuedRun || IsViewingActiveRun)
            {
                if (IsViewingActiveRun && ActiveHostedRunInteraction.PrimaryAction == CopilotHostedRunPrimaryAction.None)
                    return;
                StopCurrentReply();
                return;
            }

            RunUiOperation(SendAsync, "发送请求");
        }

        private void ExecuteSendOrSteer()
        {
            if (IsPromptHistorySearchOpen)
            {
                TryCompletePromptHistorySearch();
                return;
            }
            if (IsViewingActiveRun)
            {
                if (IsAnsweringUserQuestion)
                {
                    TryAnswerCurrentUserQuestion(InputText);
                    return;
                }

                if (TryHandleComposerLocalCommandDuringRun(InputText, out var recognized)
                    || recognized)
                {
                    return;
                }

                if (DefaultFollowUpBehavior == CopilotFollowUpBehavior.Queue)
                    TryQueueCurrentRunFollowUp();
                else
                    TrySteerCurrentRun();
                return;
            }
            if (IsViewingQueuedRun)
                return;

            RunUiOperation(SendAsync, "发送请求");
        }

        private bool CanAnswerUserQuestionOption(CopilotUserQuestionOption? option)
        {
            var question = ActiveUserQuestion;
            return option != null
                && IsAnsweringUserQuestion
                && question != null
                && string.Equals(option.RequestId, question.RequestId, StringComparison.Ordinal)
                && string.Equals(option.TaskId, question.TaskId, StringComparison.Ordinal)
                && question.Options.Any(candidate =>
                    string.Equals(candidate.Label, option.Label, StringComparison.Ordinal));
        }

        private void AnswerUserQuestionOption(CopilotUserQuestionOption? option)
        {
            if (CanAnswerUserQuestionOption(option))
                TryAnswerCurrentUserQuestion(option!.Label);
        }

        private bool TryAnswerCurrentUserQuestion(string? answer)
        {
            var run = ActiveHostedRun;
            var message = ActiveUserQuestionMessage;
            var question = message?.UserQuestion;
            if (run == null
                || message == null
                || question?.IsPending != true
                || !IsAnsweringUserQuestion
                || !CopilotUserQuestionSnapshot.TryNormalizeAnswer(answer, out var normalized)
                || !_turnRuntime.TryAnswerUserQuestion(run.Id, question.RequestId, normalized))
            {
                return false;
            }

            message.UserQuestion = question.Resolve(CopilotUserQuestionResolution.Answered, normalized);
            InputText = string.Empty;
            NotifyUserQuestionStateChanged();
            return true;
        }

        private bool TrySteerCurrentRun()
        {
            var steeringMessage = (InputText ?? string.Empty).Trim();
            var activeRun = ActiveHostedRun;
            if (!CanSteerCurrentRun || activeRun == null || string.IsNullOrWhiteSpace(steeringMessage))
                return false;
            if (TryHandleComposerLocalCommandDuringRun(steeringMessage, out var recognizedLocalCommand))
                return true;
            if (recognizedLocalCommand)
                return false;
            if (SelectedProfile == null
                || !TryValidateComposerCharacterLimit(steeringMessage)
                || !TryValidatePromptBudget(steeringMessage, activeRun.Mode, SelectedProfile))
            {
                return false;
            }
            var admission = _turnRuntime.EnqueueSteeringMessage(
                activeRun.Id,
                steeringMessage);
            if (!admission.IsAccepted)
            {
                ReportSteeringAdmissionFailure(admission);
                return false;
            }

            var activeConversation = Conversations.FirstOrDefault(conversation => string.Equals(conversation.Id, activeRun.ConversationId, StringComparison.Ordinal));
            var steeringSnapshot = new CopilotSteeringMessageSnapshot(
                admission.MessageId,
                steeringMessage);
            if (activeConversation == null
                || !CopilotSteeringRecovery.TrackPending(
                    activeConversation,
                    activeRun.Id,
                    steeringSnapshot,
                    DateTimeOffset.UtcNow))
            {
                LocalCommandResultTitle = "运行中指令已发送，输入已保留";
                LocalCommandResultText = "运行时已接受这条指令，但无法建立可靠的恢复记录；输入未清空，请确认 Agent 响应后再处理。";
                PersistState(immediate: true);
                return true;
            }
            var activeAssistant = activeConversation?.Messages.LastOrDefault(message => !message.IsUser && message.IsThinkingInProgress);
            if (activeAssistant != null)
                CopilotAssistantMessagePresenter.AppendExecutionTrace(activeAssistant, "User steering queued · " + CopilotAgentTraceEntry.Sanitize(steeringMessage));

            DismissLocalCommandResult();
            InputText = string.Empty;
            PersistState(immediate: true);
            return true;
        }

        internal static string GetSteeringAdmissionFailureText(
            CopilotSteeringAdmissionResult admission) => admission.Reason switch
        {
            CopilotSteeringAdmissionReason.InvalidInput =>
                "这条运行中指令为空、过长，或缺少有效的任务标识。输入已保留，请修改后重试。",
            CopilotSteeringAdmissionReason.PendingUserQuestion =>
                "Agent 正在等待问题回答。输入已保留，请先回答问题，再发送运行中指令。",
            CopilotSteeringAdmissionReason.NoActiveTask =>
                "当前 Agent 已结束或已切换任务。输入已保留，请直接发送，或重新排到下一轮。",
            CopilotSteeringAdmissionReason.QueueFull =>
                "运行中指令缓冲区已满。输入已保留，请等待 Agent 消费已有指令后重试，或改为排到下一轮。",
            CopilotSteeringAdmissionReason.RuntimeUnavailable =>
                "运行中指令未能送达。输入已保留，请重试，或改为排到下一轮。",
            _ => "运行中指令未发送。输入已保留，请重试。",
        };

        private void ReportSteeringAdmissionFailure(
            CopilotSteeringAdmissionResult admission)
        {
            LocalCommandResultTitle = "运行中指令未发送";
            LocalCommandResultText = GetSteeringAdmissionFailureText(admission);
        }

        public bool TrySubmitAlternateCurrentRunFollowUp()
        {
            return CopilotFollowUpPreference.Alternate(DefaultFollowUpBehavior) switch
            {
                CopilotFollowUpBehavior.Steer => TrySteerCurrentRun(),
                _ => TryQueueCurrentRunFollowUp(),
            };
        }

        public bool TryQueueCurrentRunFollowUp()
        {
            return TryQueueCurrentRunFollowUp(runNext: false, cancelActiveRun: false);
        }

        public bool TrySendCurrentRunFollowUpNow()
        {
            return TryQueueCurrentRunFollowUp(runNext: true, cancelActiveRun: true);
        }

        private bool TryQueueCurrentRunFollowUp(bool runNext, bool cancelActiveRun)
        {
            var prompt = (InputText ?? string.Empty).Trim();
            var activeRun = ActiveHostedRun;
            var conversation = SelectedConversation;
            var profile = SelectedProfile;
            if (!CanSteerCurrentRun
                || activeRun == null
                || conversation == null
                || profile == null
                || string.IsNullOrWhiteSpace(prompt))
            {
                return false;
            }
            if (TryHandleComposerLocalCommandDuringRun(prompt, out var recognizedLocalCommand))
                return true;
            if (recognizedLocalCommand)
                return false;
            var preflightAdmission = _taskHost.EvaluateFollowUpAdmission(
                conversation.Id,
                activeRun.Mode);
            if (!preflightAdmission.IsAllowed)
            {
                ReportRequestAdmissionFailure(preflightAdmission);
                return false;
            }
            if (!TryValidateComposerCharacterLimit(prompt)
                || !TryValidatePromptBudget(prompt, activeRun.Mode, profile))
            {
                return false;
            }

            var requestProfile = CreateConversationRequestProfile(profile, conversation);
            var submissionContext = CaptureHostedTurnSnapshot(conversation, attachmentOverride: conversation.Attachments);
            if (!TryValidateComposerAttachments(submissionContext.Attachments))
                return false;

            var itemReady = new TaskCompletionSource<CopilotQueuedFollowUp>(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task ExecuteFollowUpAsync(CopilotHostedAgentRun run)
            {
                var queuedItem = await itemReady.Task.ConfigureAwait(false);
                await ExecuteQueuedFollowUpAsync(run, queuedItem).ConfigureAwait(false);
            }

            CopilotHostedAgentRun? queuedRun;
            CopilotRequestAdmissionResult admission;
            var scheduled = runNext
                ? _taskHost.TryScheduleFollowUpNext(
                    conversation.Id,
                    activeRun.Mode,
                    ExecuteFollowUpAsync,
                    out queuedRun,
                    out admission)
                : _taskHost.TryScheduleFollowUp(
                    conversation.Id,
                    activeRun.Mode,
                    ExecuteFollowUpAsync,
                    out queuedRun,
                    out admission);
            if (!scheduled || queuedRun == null)
            {
                ReportRequestAdmissionFailure(admission);
                return false;
            }

            var queuedFollowUp = new CopilotQueuedFollowUp(
                queuedRun.Id,
                conversation.Id,
                conversation.Title,
                prompt,
                activeRun.Mode,
                requestProfile,
                submissionContext);
            _queuedFollowUpsByRunId.Add(queuedRun.Id, queuedFollowUp);
            QueuedFollowUps.Add(queuedFollowUp);
            AddQueuedFollowUpRecovery(queuedFollowUp);
            itemReady.SetResult(queuedFollowUp);
            RefreshQueuedFollowUpPositions();
            if (runNext)
                SynchronizeQueuedFollowUpRecoveryOrder();

            DismissLocalCommandResult();
            ConsumeComposerAttachments(conversation);
            InputText = string.Empty;
            ClearPendingRequestModeOverride();
            if (cancelActiveRun)
            {
                var activeConversation = Conversations.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, activeRun.ConversationId, StringComparison.Ordinal));
                var activeAssistant = activeConversation?.Messages.LastOrDefault(message =>
                    !message.IsUser && message.IsThinkingInProgress);
                if (activeAssistant != null)
                {
                    CopilotAssistantMessagePresenter.AppendExecutionTrace(
                        activeAssistant,
                        "Immediate user follow-up queued as the next turn.");
                }
            }
            PersistState(immediate: true);
            if (cancelActiveRun)
                _taskHost.RequestCancel(activeRun.Id);
            return true;
        }

        private bool TryHandleComposerLocalCommandDuringRun(
            string prompt,
            out bool recognized)
        {
            var invocation = CopilotLocalCommandCatalog.Parse(prompt);
            if (invocation == null)
            {
                recognized = TryReportCommandInputRecovery(prompt);
                return recognized;
            }

            recognized = true;
            if (CopilotLocalCommandAvailabilityPolicy.CanExecute(
                invocation.Command,
                ResolveLocalCommandComposerContext()))
            {
                return TryExecuteLocalCommand(prompt);
            }

            ReportUnavailableLocalCommandDuringRun(invocation.Command);
            return false;
        }

        private void ReportUnavailableLocalCommandDuringRun(CopilotLocalCommand command)
        {
            LocalCommandResultTitle = command.Name + " · 当前任务运行中";
            LocalCommandResultText = "本地命令不会作为普通 Agent 提示词注入或排队；请等待当前任务结束后再执行该命令。";
        }

        private async Task ExecuteQueuedFollowUpAsync(CopilotHostedAgentRun hostedRun, CopilotQueuedFollowUp queuedFollowUp)
        {
            var preparedTurn = CopilotUiDispatcher.Invoke(
                () => PrepareQueuedFollowUpTurn(queuedFollowUp),
                fallback: null as CopilotPreparedQueuedFollowUpTurn);
            if (preparedTurn == null)
            {
                if (queuedFollowUp.IsAutomaticGoalContinuation)
                    return;
                throw new InvalidOperationException("The queued Copilot follow-up could not be prepared on the UI thread.");
            }

            await ExecuteHostedPreparedTurnAsync(
                hostedRun,
                preparedTurn.Conversation,
                queuedFollowUp.Profile,
                preparedTurn.UserMessage,
                preparedTurn.AssistantMessage,
                preparedTurn.TurnSnapshot,
                refreshExternalContext: true).ConfigureAwait(false);
        }

        private CopilotPreparedQueuedFollowUpTurn? PrepareQueuedFollowUpTurn(CopilotQueuedFollowUp queuedFollowUp)
        {
            RemoveQueuedFollowUp(queuedFollowUp.RunId, removeRecoveryRecord: false);
            var conversation = Conversations.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, queuedFollowUp.ConversationId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The conversation for the queued Copilot follow-up no longer exists.");
            if (queuedFollowUp.IsAutomaticGoalContinuation
                && (conversation.Goal?.IsActive != true
                    || !string.Equals(conversation.Goal.Id, queuedFollowUp.GoalId, StringComparison.Ordinal)))
            {
                RemoveQueuedFollowUpRecovery(queuedFollowUp.RunId);
                PersistState(immediate: true);
                return null;
            }
            var submittedContext = queuedFollowUp.SubmissionContext;
            var turnSnapshot = new CopilotAgentHostContextSnapshot(
                submittedContext.ActiveDocumentPath,
                submittedContext.SolutionDirectoryPath,
                submittedContext.Attachments,
                submittedContext.LiveContext,
                CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation),
                submittedContext.AdditionalReadRootPaths);
            var userMessage = new CopilotChatMessage(CopilotChatRole.User, queuedFollowUp.Prompt)
            {
                RequestMode = queuedFollowUp.Mode,
                Attachments = new ObservableCollection<CopilotAttachmentItem>(turnSnapshot.Attachments),
                AttachmentSnapshotCaptured = true,
            };
            var assistantMessage = CreatePendingAssistantMessage(queuedFollowUp.Profile, queuedFollowUp.Mode);

            conversation.ProfileId = queuedFollowUp.Profile.Id;
            conversation.ProfileDisplayName = queuedFollowUp.Profile.DisplayLabel;
            conversation.Messages.Add(userMessage);
            conversation.Messages.Add(assistantMessage);
            RemoveQueuedFollowUpRecovery(queuedFollowUp.RunId);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState(immediate: true);
            return new CopilotPreparedQueuedFollowUpTurn(conversation, userMessage, assistantMessage, turnSnapshot);
        }

        private bool CanSendQueuedFollowUpNow(CopilotQueuedFollowUp? queuedFollowUp)
        {
            return queuedFollowUp != null
                && ActiveHostedRun?.CanRequestCancel == true
                && _taskHost.GetQueuePosition(queuedFollowUp.RunId) > 0;
        }

        private void SendQueuedFollowUpNow(CopilotQueuedFollowUp? queuedFollowUp)
        {
            TrySendQueuedFollowUpNow(queuedFollowUp);
        }

        private bool TrySendQueuedFollowUpNow(CopilotQueuedFollowUp? queuedFollowUp)
        {
            var activeRun = ActiveHostedRun;
            if (!CanSendQueuedFollowUpNow(queuedFollowUp)
                || queuedFollowUp == null
                || activeRun == null
                || !_taskHost.PromoteQueuedRun(queuedFollowUp.RunId))
            {
                return false;
            }

            RefreshQueuedFollowUpPositions();
            SynchronizeQueuedFollowUpRecoveryOrder();
            PersistState(immediate: true);
            _taskHost.RequestCancel(activeRun.Id);
            return true;
        }

        private bool CanEditQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp)
        {
            if (queuedFollowUp == null
                || queuedFollowUp.IsAutomaticGoalContinuation
                || IsEditingMessage
                || !IsInputEmpty)
            {
                return false;
            }

            var conversation = Conversations.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, queuedFollowUp.ConversationId, StringComparison.Ordinal));
            return conversation != null
                && !conversation.HasDraft
                && conversation.Attachments.Count == 0
                && (ReferenceEquals(conversation, SelectedConversation) || CanSwitchConversation);
        }

        private void EditQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp)
        {
            TryEditQueuedFollowUp(queuedFollowUp);
        }

        private bool TryEditQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp)
        {
            if (!CanEditQueuedFollowUp(queuedFollowUp) || queuedFollowUp == null)
                return false;

            var previousConversation = SelectedConversation;
            var conversation = Conversations.First(candidate =>
                string.Equals(candidate.Id, queuedFollowUp.ConversationId, StringComparison.Ordinal));
            if (!ReferenceEquals(conversation, SelectedConversation))
                SelectConversation(conversation, persist: true, preferredProfileId: conversation.ProfileId);
            if (!ReferenceEquals(conversation, SelectedConversation))
                return false;

            var composerState = CopilotComposerStash.Capture(
                queuedFollowUp.Prompt,
                queuedFollowUp.Prompt.Length,
                queuedFollowUp.Mode,
                queuedFollowUp.SubmissionContext.Attachments);
            var previousMode = ResolveComposerRequestMode();
            foreach (var attachment in composerState.CreateAttachmentSnapshots())
                conversation.Attachments.Add(attachment);
            SetPendingRequestModeOverride(composerState.RequestMode);
            InputText = composerState.Text;
            UpdateAttachmentsState(conversation);
            if (!_taskHost.RequestCancel(queuedFollowUp.RunId))
            {
                conversation.Attachments.Clear();
                SetPendingRequestModeOverride(previousMode);
                InputText = string.Empty;
                UpdateAttachmentsState(conversation);
                if (previousConversation != null
                    && !ReferenceEquals(previousConversation, conversation)
                    && CanSwitchConversation)
                {
                    SelectConversation(
                        previousConversation,
                        persist: true,
                        preferredProfileId: previousConversation.ProfileId);
                }
                return false;
            }
            return true;
        }

        private void MoveQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp, int offset)
        {
            TryMoveQueuedFollowUp(queuedFollowUp, offset);
        }

        private bool TryMoveQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp, int offset)
        {
            if (queuedFollowUp == null || !_taskHost.MoveQueuedRun(queuedFollowUp.RunId, offset))
                return false;
            RefreshQueuedFollowUpPositions();
            SynchronizeQueuedFollowUpRecoveryOrder();
            PersistState(immediate: true);
            return true;
        }

        private void DeleteQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp)
        {
            TryDeleteQueuedFollowUp(queuedFollowUp, out _);
        }

        private bool TryDeleteQueuedFollowUp(
            CopilotQueuedFollowUp? queuedFollowUp,
            out bool pausedGoal)
        {
            pausedGoal = false;
            if (queuedFollowUp == null || !_taskHost.RequestCancel(queuedFollowUp.RunId))
                return false;

            if (!queuedFollowUp.IsAutomaticGoalContinuation)
                return true;

            var conversation = Conversations.FirstOrDefault(item =>
                string.Equals(item.Id, queuedFollowUp.ConversationId, StringComparison.Ordinal));
            if (conversation?.Goal?.IsActive == true
                && string.Equals(conversation.Goal.Id, queuedFollowUp.GoalId, StringComparison.Ordinal))
            {
                conversation.Goal = conversation.Goal.WithState(
                    CopilotConversationGoalState.Paused,
                    DateTimeOffset.UtcNow,
                    "用户取消了已排队的自动续作，持续目标已暂停。");
                pausedGoal = true;
                UpdateConversationMetadata(conversation, touch: true);
                PersistState(immediate: true);
            }
            return true;
        }

        private void RemoveQueuedFollowUp(string runId, bool removeRecoveryRecord = true)
        {
            var changed = false;
            if (_queuedFollowUpsByRunId.Remove(runId, out var queuedFollowUp))
            {
                QueuedFollowUps.Remove(queuedFollowUp);
                OnQueuedFollowUpsChanged();
                changed = true;
            }
            if (removeRecoveryRecord)
                changed |= RemoveQueuedFollowUpRecovery(runId);
            if (changed && removeRecoveryRecord)
                PersistState(immediate: true);
        }

        private void AddQueuedFollowUpRecovery(CopilotQueuedFollowUp queuedFollowUp)
        {
            _state.QueuedFollowUpRecoveries ??= new ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>();
            _state.QueuedFollowUpRecoveries.Add(new CopilotQueuedFollowUpRecoveryRecord
            {
                RunId = queuedFollowUp.RunId,
                ConversationId = queuedFollowUp.ConversationId,
                Prompt = queuedFollowUp.Prompt,
                ComposerState = CopilotComposerStash.Capture(
                    queuedFollowUp.Prompt,
                    queuedFollowUp.Prompt.Length,
                    queuedFollowUp.Mode,
                    queuedFollowUp.SubmissionContext.Attachments),
            });
        }

        private bool RemoveQueuedFollowUpRecovery(string runId)
        {
            if (_state.QueuedFollowUpRecoveries == null)
                return false;

            var changed = false;
            for (var index = _state.QueuedFollowUpRecoveries.Count - 1; index >= 0; index--)
            {
                if (!string.Equals(_state.QueuedFollowUpRecoveries[index]?.RunId, runId, StringComparison.Ordinal))
                    continue;

                _state.QueuedFollowUpRecoveries.RemoveAt(index);
                changed = true;
            }
            return changed;
        }

        private void SynchronizeQueuedFollowUpRecoveryOrder()
        {
            if (_state.QueuedFollowUpRecoveries == null || _state.QueuedFollowUpRecoveries.Count < 2)
                return;

            var positions = _taskHost.ScheduledRuns
                .Select((run, index) => new { run.Id, Position = index })
                .ToDictionary(item => item.Id, item => item.Position, StringComparer.Ordinal);
            var ordered = _state.QueuedFollowUpRecoveries
                .Select((record, index) => new { Record = record, OriginalPosition = index })
                .OrderBy(item => positions.TryGetValue(item.Record.RunId, out var position) ? position : int.MaxValue)
                .ThenBy(item => item.OriginalPosition)
                .Select(item => item.Record)
                .ToArray();
            if (ordered.SequenceEqual(_state.QueuedFollowUpRecoveries))
                return;

            _state.QueuedFollowUpRecoveries.Clear();
            foreach (var record in ordered)
                _state.QueuedFollowUpRecoveries.Add(record);
        }

        private void RefreshQueuedFollowUpPositions()
        {
            var queuedRuns = _taskHost.QueuedRuns;
            var positions = queuedRuns
                .Select((run, index) => new { run.Id, Position = index + 1 })
                .ToDictionary(item => item.Id, item => item.Position, StringComparer.Ordinal);
            var ordered = QueuedFollowUps
                .Where(item => positions.ContainsKey(item.RunId))
                .OrderBy(item => positions[item.RunId])
                .ToArray();

            for (var targetIndex = 0; targetIndex < ordered.Length; targetIndex++)
            {
                var currentIndex = QueuedFollowUps.IndexOf(ordered[targetIndex]);
                if (currentIndex != targetIndex)
                    QueuedFollowUps.Move(currentIndex, targetIndex);
            }
            foreach (var item in ordered)
                item.UpdateQueuePosition(positions[item.RunId], queuedRuns.Count);
            OnQueuedFollowUpsChanged();
        }

        private void OnQueuedFollowUpsChanged()
        {
            OnPropertyChanged(nameof(HasQueuedFollowUps));
            OnPropertyChanged(nameof(QueuedFollowUpCountLabel));
            OnPropertyChanged(nameof(CanQueueCurrentRunFollowUp));
            CommandManager.InvalidateRequerySuggested();
        }

    }
}
