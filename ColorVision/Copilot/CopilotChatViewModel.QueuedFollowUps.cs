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
            var composerCapture = _composerSession.Capture();
            var prompt = composerCapture.Text.Trim();
            var isLocalCommand = IsQueuedLocalCommandInput(prompt);
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
            var preflightAdmission = _followUpQueue.EvaluateAdmission(
                conversation.Id,
                activeRun.Mode);
            if (!preflightAdmission.IsAllowed)
            {
                ReportRequestAdmissionFailure(preflightAdmission);
                return false;
            }
            var agentSkillReference = composerCapture.AgentSkillReference;
            var capturedAttachments = conversation.Attachments.ToArray();
            var initialSubmissionContext = CaptureHostedTurnSnapshot(
                conversation,
                attachmentOverride: capturedAttachments);
            CopilotAgentHostContextSnapshot submissionContext;
            if (isLocalCommand)
            {
                submissionContext = initialSubmissionContext;
            }
            else if (!TryResolveProjectTrustForSubmission(
                         initialSubmissionContext,
                         () => CaptureHostedTurnSnapshot(
                             conversation,
                             attachmentOverride: capturedAttachments),
                         out submissionContext))
            {
                return false;
            }
            var requestProfile = CreateConversationRequestProfile(
                profile,
                conversation,
                activeRun.Mode,
                submissionContext.ProjectInstructionDiscoveryOptions);
            if (!TryValidateComposerCharacterLimit(prompt)
                || !TryValidateComposerAttachments(submissionContext.Attachments)
                || !isLocalCommand
                    && !TryValidatePromptBudget(
                        prompt,
                        activeRun.Mode,
                        requestProfile,
                        submissionContext.ProjectInstructionDiscoveryOptions)
                || !isLocalCommand
                    && !TryPrepareExplicitSkillMcpDependencies(
                        prompt,
                        agentSkillReference,
                        submissionContext.ProjectInstructionDiscoveryOptions,
                        conversation.Id))
            {
                return false;
            }
            var runtimeConfigSnapshot = CaptureTurnRuntimeConfigSnapshot();

            var queueRequest = new CopilotQueuedFollowUpRequest(
                conversation.Id,
                conversation.Title,
                prompt,
                activeRun.Mode,
                requestProfile,
                submissionContext,
                agentSkillReference,
                runtimeConfigSnapshot,
                ResolveQueuedFollowUpReviewTarget(conversation, activeRun.Mode),
                IsLocalCommand: isLocalCommand);
            if (!_followUpQueue.TrySchedule(
                queueRequest,
                runNext,
                ExecuteQueuedFollowUpAsync,
                out _,
                out var admission))
            {
                ReportRequestAdmissionFailure(admission);
                return false;
            }

            DismissLocalCommandResult();
            if (_composerSession.CommitScheduled(composerCapture.Token))
            {
                SynchronizeSelectedConversationComposerDraft();
                ConsumeCapturedComposerAttachments(conversation, capturedAttachments);
                NotifyComposerTextChanged(synchronizeDraft: false);
                OnComposerRequestModeChanged();
            }
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

        private static bool IsQueuedLocalCommandInput(string prompt)
        {
            var normalized = (prompt ?? string.Empty).TrimStart();
            return normalized.Length > 0 && normalized[0] == '/';
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
            if (queuedFollowUp.IsLocalCommand)
            {
                await ExecuteQueuedLocalCommandAsync(hostedRun, queuedFollowUp).ConfigureAwait(false);
                return;
            }

            var preparedTurn = CopilotUiDispatcher.Invoke(
                () => PrepareQueuedFollowUpTurn(queuedFollowUp),
                fallback: null as CopilotPreparedHostedTurn);
            if (preparedTurn == null)
            {
                if (queuedFollowUp.IsGoalBound)
                    return;
                throw new InvalidOperationException("The queued Copilot follow-up could not be prepared on the UI thread.");
            }

            try
            {
                await _statePersistenceCoordinator.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                CopilotUiDispatcher.Invoke(() =>
                    RollbackUnpersistedQueuedFollowUp(queuedFollowUp, preparedTurn));
                throw;
            }

            var recoveryCommitted = CopilotUiDispatcher.Invoke(
                () =>
                {
                    _followUpQueue.RemoveRecovery(queuedFollowUp.RunId);
                    PersistState(immediate: true);
                    return true;
                },
                fallback: false);
            if (!recoveryCommitted)
                throw new OperationCanceledException("The Copilot UI shut down before the queued follow-up could be committed.");

            await ExecuteHostedPreparedTurnAsync(hostedRun, preparedTurn).ConfigureAwait(false);
        }

        private async Task ExecuteQueuedLocalCommandAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotQueuedFollowUp queuedFollowUp)
        {
            var recoveryPrepared = CopilotUiDispatcher.Invoke(
                () =>
                {
                    RemoveQueuedFollowUp(queuedFollowUp.RunId, removeRecoveryRecord: false);
                    _followUpQueue.MarkRecoveryDispatching(queuedFollowUp.RunId);
                    PersistState(immediate: true);
                    return true;
                },
                fallback: false);
            if (!recoveryPrepared)
                throw new OperationCanceledException("The Copilot UI shut down before the queued command could be prepared.");

            try
            {
                await _statePersistenceCoordinator.FlushAsync().ConfigureAwait(false);
            }
            catch
            {
                CopilotUiDispatcher.Invoke(() =>
                {
                    _followUpQueue.RestoreRecoveryToDraft(queuedFollowUp.RunId);
                    PersistState(immediate: true);
                });
                throw;
            }

            QueuedLocalCommandExecutionContext? executionContext;
            try
            {
                executionContext = CopilotUiDispatcher.Invoke(
                    () => BeginQueuedLocalCommandExecution(hostedRun, queuedFollowUp),
                    fallback: null as QueuedLocalCommandExecutionContext);
                if (executionContext == null)
                    throw new OperationCanceledException("The Copilot UI shut down before the queued command could be executed.");
            }
            catch
            {
                CopilotUiDispatcher.Invoke(() =>
                {
                    _followUpQueue.RestoreRecoveryToDraft(queuedFollowUp.RunId);
                    PersistState(immediate: true);
                });
                throw;
            }

            try
            {
                await executionContext.DrainOperationsAsync().ConfigureAwait(false);
            }
            finally
            {
                CopilotUiDispatcher.Invoke(() => CompleteQueuedLocalCommandExecution(executionContext));
            }
        }

        private QueuedLocalCommandExecutionContext BeginQueuedLocalCommandExecution(
            CopilotHostedAgentRun hostedRun,
            CopilotQueuedFollowUp queuedFollowUp)
        {
            var conversation = Conversations.FirstOrDefault(candidate => string.Equals(
                candidate.Id,
                queuedFollowUp.ConversationId,
                StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The conversation for the queued Copilot command no longer exists.");
            var previouslySelectedConversation = SelectedConversation;
            QueuedLocalCommandExecutionContext? context = null;
            IsBusy = false;

            try
            {
                if (!ReferenceEquals(conversation, SelectedConversation))
                {
                    SelectConversation(
                        conversation,
                        persist: true,
                        preferredProfileId: queuedFollowUp.Profile.Id);
                }
                if (!ReferenceEquals(conversation, SelectedConversation))
                    throw new InvalidOperationException("The queued Copilot command conversation could not be selected.");

                var composerCapture = _composerSession.Capture();
                var composerBeforeCommand = CopilotComposerStash.Capture(
                    composerCapture.Text,
                    composerCapture.Text.Length,
                    composerCapture.RequestMode,
                    conversation.Attachments,
                    composerCapture.WorkspaceReviewTarget,
                    composerCapture.AgentSkillReference);
                var createdContext = new QueuedLocalCommandExecutionContext(
                    hostedRun,
                    queuedFollowUp,
                    conversation,
                    previouslySelectedConversation,
                    composerBeforeCommand);
                context = createdContext;
                _queuedLocalCommandExecution = createdContext;

                try
                {
                    var handled = TryExecuteLocalCommand(queuedFollowUp.Prompt, clearComposer: false)
                        || TryReportCommandInputRecovery(queuedFollowUp.Prompt);
                    if (!handled)
                    {
                        LocalCommandResultTitle = "排队命令无法执行";
                        LocalCommandResultText = "该斜杠命令在轮到执行时无法识别；它没有作为普通提示词发送给 Agent。请检查命令名称后重试。";
                    }
                }
                catch (Exception exception)
                {
                    LocalCommandResultTitle = "排队命令执行失败";
                    LocalCommandResultText = CopilotUserFacingErrorFormatter.Sanitize(exception.Message);
                }
                return createdContext;
            }
            catch
            {
                if (ReferenceEquals(_queuedLocalCommandExecution, context))
                    _queuedLocalCommandExecution = null;
                throw;
            }
            finally
            {
                IsBusy = _taskHost.IsActive;
            }
        }

        private void CompleteQueuedLocalCommandExecution(QueuedLocalCommandExecutionContext context)
        {
            if (!ReferenceEquals(_queuedLocalCommandExecution, context))
                return;

            try
            {
                RestoreUnusedQueuedCommandAttachments(context);
                if (context.CommandComposerWasCommitted)
                    RestoreComposerAfterQueuedCommandSuccessor(context);

                var commandResultTitle = LocalCommandResultTitle;
                var commandResultText = LocalCommandResultText;
                if (context.PreviouslySelectedConversation != null
                    && !ReferenceEquals(context.PreviouslySelectedConversation, context.Conversation)
                    && ReferenceEquals(SelectedConversation, context.Conversation)
                    && Conversations.Contains(context.PreviouslySelectedConversation))
                {
                    SelectConversation(
                        context.PreviouslySelectedConversation,
                        persist: true,
                        preferredProfileId: context.PreviouslySelectedConversation.ProfileId);
                    if (!string.IsNullOrWhiteSpace(commandResultText))
                    {
                        LocalCommandResultTitle = commandResultTitle;
                        LocalCommandResultText = commandResultText;
                    }
                }
            }
            finally
            {
                _queuedLocalCommandExecution = null;
                _followUpQueue.RemoveRecovery(context.QueuedFollowUp.RunId);
                PersistState(immediate: true);
                IsBusy = _taskHost.IsActive;
            }
        }

        private void RestoreUnusedQueuedCommandAttachments(QueuedLocalCommandExecutionContext context)
        {
            if (context.QueuedAttachmentsConsumedBySuccessor
                || !Conversations.Contains(context.Conversation))
                return;

            var restoredCount = CopilotComposerAttachmentService.RestoreDistinctSnapshots(
                context.Conversation.Attachments,
                context.QueuedFollowUp.SubmissionContext.Attachments.Select(attachment => attachment.CreateSnapshot()));
            if (restoredCount == 0)
                return;

            UpdateAttachmentsState(context.Conversation);
        }

        private void RestoreComposerAfterQueuedCommandSuccessor(QueuedLocalCommandExecutionContext context)
        {
            if (context.ComposerRestoreToken is not { } restoreToken)
                return;
            if (!Conversations.Contains(context.Conversation))
                return;
            if (ReferenceEquals(SelectedConversation, context.Conversation)
                && _composerSession.Capture().Token != restoreToken)
            {
                return;
            }

            var stash = context.ComposerBeforeCommand;
            context.Conversation.DraftText = stash.Text;
            context.Conversation.DraftRequestMode = stash.RequestMode;
            context.Conversation.DraftWorkspaceReviewTarget = stash.WorkspaceReviewTarget?.CreateSnapshot();
            context.Conversation.DraftAgentSkillReference = stash.AgentSkillReference?.CreateSnapshot();
            if (!ReferenceEquals(SelectedConversation, context.Conversation))
                return;

            _composerSession.Load(context.Conversation);
            SynchronizeSelectedConversationComposerDraft();
            NotifyComposerTextChanged(synchronizeDraft: false);
            OnComposerRequestModeChanged();
        }

        private CopilotPreparedHostedTurn? PrepareQueuedFollowUpTurn(CopilotQueuedFollowUp queuedFollowUp)
        {
            RemoveQueuedFollowUp(queuedFollowUp.RunId, removeRecoveryRecord: false);
            var conversation = Conversations.FirstOrDefault(candidate =>
                string.Equals(candidate.Id, queuedFollowUp.ConversationId, StringComparison.Ordinal))
                ?? throw new InvalidOperationException("The conversation for the queued Copilot follow-up no longer exists.");
            if (queuedFollowUp.IsGoalBound
                && (conversation.Goal?.IsActive != true
                    || !string.Equals(conversation.Goal.Id, queuedFollowUp.GoalId, StringComparison.Ordinal)))
            {
                _followUpQueue.RemoveRecovery(queuedFollowUp.RunId);
                PersistState(immediate: true);
                return null;
            }
            var turnSnapshot = queuedFollowUp.CreateExecutionContext(
                CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation));
            var userMessage = new CopilotChatMessage(CopilotChatRole.User, queuedFollowUp.Prompt)
            {
                Id = queuedFollowUp.RunId,
                RequestMode = queuedFollowUp.Mode,
                WorkspaceReviewTarget = queuedFollowUp.CreateWorkspaceReviewTargetSnapshot(),
                AgentSkillReference = queuedFollowUp.AgentSkillReference?.CreateSnapshot(),
                Attachments = new ObservableCollection<CopilotAttachmentItem>(turnSnapshot.Attachments),
                AttachmentSnapshotCaptured = true,
            };
            var assistantMessage = CreatePendingAssistantMessage(queuedFollowUp.Profile, queuedFollowUp.Mode);

            conversation.ProfileId = queuedFollowUp.Profile.Id;
            conversation.ProfileDisplayName = queuedFollowUp.Profile.DisplayLabel;
            conversation.Messages.Add(userMessage);
            conversation.Messages.Add(assistantMessage);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState(immediate: true);
            return new CopilotPreparedHostedTurn(
                conversation,
                queuedFollowUp.Profile,
                userMessage,
                assistantMessage,
                turnSnapshot,
                queuedFollowUp.RuntimeConfigSnapshot,
                refreshExternalContext: true,
                isAutomaticGoalContinuation: queuedFollowUp.IsAutomaticGoalContinuation);
        }

        private void RollbackUnpersistedQueuedFollowUp(
            CopilotQueuedFollowUp queuedFollowUp,
            CopilotPreparedHostedTurn preparedTurn)
        {
            preparedTurn.Conversation.Messages.Remove(preparedTurn.AssistantMessage);
            preparedTurn.Conversation.Messages.Remove(preparedTurn.UserMessage);
            _followUpQueue.RestoreRecoveryToDraft(queuedFollowUp.RunId);
            UpdateConversationMetadata(preparedTurn.Conversation, touch: true);

            if (ReferenceEquals(preparedTurn.Conversation, SelectedConversation))
            {
                _composerSession.Load(preparedTurn.Conversation);
                SynchronizeSelectedConversationComposerDraft();
                NotifyComposerTextChanged(synchronizeDraft: false);
                UpdateAttachmentsState(preparedTurn.Conversation);
                OnComposerRequestModeChanged();
            }
            RefreshCompactHistoryConversations();
            if (HasConversationSearchQuery)
                RefreshFilteredConversations();
            PersistState(immediate: true);
        }

        private bool CanSendQueuedFollowUpNow(CopilotQueuedFollowUp? queuedFollowUp)
        {
            return queuedFollowUp != null
                && (ActiveHostedRun == null || ActiveHostedRun.CanRequestCancel)
                && _followUpQueue.GetQueuePosition(queuedFollowUp.RunId) > 0;
        }

        private void SendQueuedFollowUpNow(CopilotQueuedFollowUp? queuedFollowUp)
        {
            TrySendQueuedFollowUpNow(queuedFollowUp);
        }

        private bool TrySendQueuedFollowUpNow(CopilotQueuedFollowUp? queuedFollowUp)
        {
            var activeRun = ActiveHostedRun;
            if (!CanSendQueuedFollowUpNow(queuedFollowUp) || queuedFollowUp == null)
            {
                return false;
            }

            if (_queuedLocalCommandExecution != null)
            {
                if (!_followUpQueue.TryPromote(queuedFollowUp.RunId))
                    return false;
                PersistState(immediate: true);
                return true;
            }
            if (activeRun == null)
                return _followUpQueue.TryStart(queuedFollowUp.RunId);
            if (!_followUpQueue.TryPromote(queuedFollowUp.RunId))
                return false;

            PersistState(immediate: true);
            _taskHost.RequestCancel(activeRun.Id);
            return true;
        }

        private bool CanEditQueuedFollowUp(CopilotQueuedFollowUp? queuedFollowUp)
        {
            if (queuedFollowUp == null
                || queuedFollowUp.IsGoalBound
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

            var composerState = queuedFollowUp.CreateComposerState();
            var previousMode = ResolveComposerRequestMode();
            var previousReviewTarget = _composerSession.WorkspaceReviewTarget;
            foreach (var attachment in composerState.CreateAttachmentSnapshots())
                conversation.Attachments.Add(attachment);
            SetPendingRequestModeOverride(composerState.RequestMode);
            SetPendingWorkspaceReviewTarget(composerState.WorkspaceReviewTarget);
            InputText = composerState.Text;
            SetPendingAgentSkillReference(composerState.AgentSkillReference);
            UpdateAttachmentsState(conversation);
            if (!_followUpQueue.RequestCancel(queuedFollowUp.RunId))
            {
                conversation.Attachments.Clear();
                SetPendingRequestModeOverride(previousMode);
                SetPendingWorkspaceReviewTarget(previousReviewTarget);
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
            if (queuedFollowUp == null || !_followUpQueue.TryMove(queuedFollowUp.RunId, offset))
                return false;
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
            if (queuedFollowUp == null || !_followUpQueue.RequestCancel(queuedFollowUp.RunId))
                return false;

            if (!queuedFollowUp.IsGoalBound)
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
            var result = _followUpQueue.Remove(runId, removeRecoveryRecord);
            if (result.RecoveryChanged)
                PersistState(immediate: true);
        }

        private void RestoreDurableQueuedFollowUps()
        {
            var records = _followUpQueue.GetResumableRecoveries();
            if (records.Count == 0)
                return;

            var hostWasIdle = _followUpQueue.ScheduledRuns.Count == 0;
            var firstAutoDispatchRunId = string.Empty;
            var restoredCount = 0;
            var restoredDraftCount = 0;
            foreach (var record in records)
            {
                if (!record.TryGetNormalized(
                        out var runId,
                        out var conversationId,
                        out var composerState)
                    || !record.CanResumeAfterRestart(composerState))
                {
                    continue;
                }

                var conversation = Conversations.FirstOrDefault(candidate =>
                    string.Equals(candidate.Id, conversationId, StringComparison.Ordinal));
                var profile = _config.FindProfile(record.ProfileId);
                if (conversation == null || profile == null)
                {
                    if (_followUpQueue.RestoreRecoveryToDraft(runId))
                        restoredDraftCount++;
                    continue;
                }

                if (record.IsGoalBound
                    && (conversation.Goal?.IsActive != true
                        || !string.Equals(conversation.Goal.Id, record.GoalId, StringComparison.Ordinal)))
                {
                    _followUpQueue.RemoveRecovery(runId);
                    continue;
                }

                var attachments = composerState.CreateAttachmentSnapshots();
                var submissionContext = CaptureHostedTurnSnapshot(
                    conversation,
                    attachmentOverride: attachments);
                if (!record.IsLocalCommand
                    && CopilotCodexProjectTrustPersistence.RequiresDecision(
                        submissionContext.PrimaryTrustedProjectRootPath,
                        submissionContext.ProjectInstructionDiscoveryOptions))
                {
                    if (_followUpQueue.RestoreRecoveryToDraft(runId))
                        restoredDraftCount++;
                    continue;
                }

                var requestProfile = CreateConversationRequestProfile(
                    profile,
                    conversation,
                    composerState.RequestMode,
                    submissionContext.ProjectInstructionDiscoveryOptions);
                var queuedFollowUp = new CopilotQueuedFollowUp(
                    runId,
                    conversationId,
                    conversation.Title,
                    composerState.Text,
                    composerState.RequestMode,
                    requestProfile,
                    submissionContext,
                    goalId: record.GoalId,
                    agentSkillReference: composerState.AgentSkillReference,
                    runtimeConfigSnapshot: CaptureTurnRuntimeConfigSnapshot(),
                    workspaceReviewTarget: composerState.WorkspaceReviewTarget,
                    queuedAtUtc: record.QueuedAtUtc,
                    automaticGoalContinuation: record.IsAutomaticGoalContinuation,
                    isLocalCommand: record.IsLocalCommand);
                if (!_followUpQueue.TryRestore(
                    queuedFollowUp,
                    ExecuteQueuedFollowUpAsync))
                {
                    if (_followUpQueue.RestoreRecoveryToDraft(runId))
                        restoredDraftCount++;
                    continue;
                }

                if (firstAutoDispatchRunId.Length == 0
                    && CopilotQueuedFollowUpRecovery.CanAutoDispatch(conversation))
                {
                    firstAutoDispatchRunId = runId;
                }
                restoredCount++;
            }

            _followUpQueue.RecordStartupRecovery(restoredCount, restoredDraftCount);
            if (restoredDraftCount > 0)
                SynchronizeSelectedDraftAfterQueuedRecovery();
            _followUpQueue.RefreshPositions();
            PersistState(immediate: true);
            if (hostWasIdle
                && firstAutoDispatchRunId.Length > 0
                && _taskHost.ActiveRun == null)
            {
                _followUpQueue.TryStart(firstAutoDispatchRunId);
            }
        }

        private void SynchronizeSelectedDraftAfterQueuedRecovery()
        {
            var conversation = SelectedConversation;
            if (conversation == null)
                return;

            _composerSession.Load(conversation);
            SynchronizeSelectedConversationComposerDraft();
            NotifyComposerTextChanged(synchronizeDraft: false);
            UpdateAttachmentsState(conversation);
            OnComposerRequestModeChanged();
        }

        internal static CopilotWorkspaceReviewTargetContext? ResolveQueuedFollowUpReviewTarget(
            CopilotConversationRecord conversation,
            CopilotAgentMode mode)
        {
            ArgumentNullException.ThrowIfNull(conversation);
            if (mode != CopilotAgentMode.Review)
                return null;

            return conversation.Messages
                .LastOrDefault(message => message?.IsUser == true)
                ?.WorkspaceReviewTarget
                ?.CreateSnapshot();
        }

        private void FollowUpQueue_Changed(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(HasQueuedFollowUps));
            OnPropertyChanged(nameof(QueuedFollowUpCountLabel));
            OnPropertyChanged(nameof(CanQueueCurrentRunFollowUp));
            CommandManager.InvalidateRequerySuggested();
        }

        private sealed class QueuedLocalCommandExecutionContext
        {
            private readonly object _operationGate = new();
            private readonly List<Task> _operations = new();
            private int _drainedOperationCount;

            public QueuedLocalCommandExecutionContext(
                CopilotHostedAgentRun hostedRun,
                CopilotQueuedFollowUp queuedFollowUp,
                CopilotConversationRecord conversation,
                CopilotConversationRecord? previouslySelectedConversation,
                CopilotComposerStash composerBeforeCommand)
            {
                HostedRun = hostedRun;
                QueuedFollowUp = queuedFollowUp;
                Conversation = conversation;
                PreviouslySelectedConversation = previouslySelectedConversation;
                ComposerBeforeCommand = composerBeforeCommand;
            }

            public CopilotHostedAgentRun HostedRun { get; }

            public CopilotQueuedFollowUp QueuedFollowUp { get; }

            public CopilotConversationRecord Conversation { get; }

            public CopilotConversationRecord? PreviouslySelectedConversation { get; }

            public CopilotComposerStash ComposerBeforeCommand { get; }

            public bool QueuedAttachmentsConsumedBySuccessor { get; set; }

            public CopilotComposerCaptureToken? ComposerRestoreToken { get; set; }

            public bool CommandComposerWasCommitted => ComposerRestoreToken.HasValue;

            public void TrackOperation(Task operation)
            {
                ArgumentNullException.ThrowIfNull(operation);
                lock (_operationGate)
                    _operations.Add(operation);
            }

            public async Task DrainOperationsAsync()
            {
                while (true)
                {
                    Task[] pending;
                    lock (_operationGate)
                    {
                        if (_drainedOperationCount >= _operations.Count)
                            return;
                        pending = _operations.Skip(_drainedOperationCount).ToArray();
                        _drainedOperationCount = _operations.Count;
                    }
                    await Task.WhenAll(pending).ConfigureAwait(false);
                }
            }
        }

    }
}
