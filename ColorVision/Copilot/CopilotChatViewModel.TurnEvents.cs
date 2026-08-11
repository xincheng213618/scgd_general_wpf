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
        private void WorkspaceManager_ContentIdSelected(object? sender, string contentId)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => WorkspaceManager_ContentIdSelected(sender, contentId)));
                return;
            }

            var activeDocumentPath = NormalizeExistingFilePath(contentId);
            if (string.Equals(_activeDocumentPath, activeDocumentPath, StringComparison.OrdinalIgnoreCase))
                return;

            _activeDocumentPath = activeDocumentPath;
            OnActiveDocumentStateChanged();
        }

        private static string TryGetActiveDocumentPath()
        {
            try
            {
                return NormalizeExistingFilePath(WorkspaceManager.SelectedContentId);
            }
            catch
            {
                return string.Empty;
            }
        }

        private static string NormalizeExistingFilePath(string? filePath)
        {
            if (string.IsNullOrWhiteSpace(filePath))
                return string.Empty;

            try
            {
                var fullPath = Path.GetFullPath(filePath.Trim());
                return File.Exists(fullPath) ? fullPath : string.Empty;
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
            {
                return string.Empty;
            }
        }

        private void OnActiveDocumentStateChanged()
        {
            OnPropertyChanged(nameof(HasActiveDocument));
            OnPropertyChanged(nameof(IsActiveDocumentAttached));
            OnPropertyChanged(nameof(CanAttachActiveDocument));
            OnPropertyChanged(nameof(ActiveDocumentAttachmentMenuText));
            RefreshLocalCommandSuggestions();
            if (CopilotComposerReferenceCatalog.TryParseMention(InputText, out _))
                RefreshComposerReferenceSuggestions();
            _ = CaptureHostedTurnSnapshot(Array.Empty<CopilotAttachmentItem>());
            RefreshComposerTokenEstimate();
            CommandManager.InvalidateRequerySuggested();
        }

        private void CopilotLiveContextRegistry_CurrentChanged(object? sender, EventArgs e)
        {
            if (Application.Current != null && !Application.Current.Dispatcher.CheckAccess())
            {
                Application.Current.Dispatcher.BeginInvoke(new Action(() => CopilotLiveContextRegistry_CurrentChanged(sender, e)));
                return;
            }

            _currentLiveContext = CopilotLiveContextRegistry.Current;
            OnCurrentLiveContextStateChanged();
        }



        private void OnCurrentLiveContextStateChanged()
        {
            OnPropertyChanged(nameof(HasCurrentLiveContext));
            OnPropertyChanged(nameof(HasAvailableCurrentLiveContext));
            OnPropertyChanged(nameof(HasComposerAttachmentItems));
            OnPropertyChanged(nameof(CanAttachCurrentLiveContext));
            OnPropertyChanged(nameof(IsCurrentLiveContextAttached));
            OnPropertyChanged(nameof(CurrentLiveContextAttachmentLabel));
            RefreshComposerTokenEstimate();
            CommandManager.InvalidateRequerySuggested();
        }

        private CopilotAgentHostContextSnapshot CaptureHostedTurnSnapshot(
            CopilotConversationRecord conversation,
            CopilotChatMessage? stopBeforeMessage = null,
            IEnumerable<CopilotAttachmentItem>? attachmentOverride = null)
        {
            var attachments = attachmentOverride ?? (stopBeforeMessage?.AttachmentSnapshotCaptured == true
                ? stopBeforeMessage.Attachments
                : conversation.Attachments);
            return CaptureHostedTurnSnapshot(
                attachments,
                CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation, stopBeforeMessage),
                conversation.AdditionalReadRootPaths);
        }

        private CopilotAgentHostContextSnapshot CaptureHostedTurnSnapshot(
            IEnumerable<CopilotAttachmentItem> attachments,
            CopilotConversationHistorySnapshot? conversationHistory = null,
            IEnumerable<string>? additionalReadRootPaths = null)
        {
            var snapshot = new CopilotAgentHostContextSnapshot(
                _activeDocumentPath,
                SolutionManager.GetInstance().CurrentSolutionExplorer?.DirectoryInfo?.FullName ?? string.Empty,
                attachments,
                _currentLiveContext,
                conversationHistory,
                additionalReadRootPaths,
                CopilotAgentProjectInstructions.ResolveGlobalInstructionRootPath());
            var previousMentionsV2Enabled = _currentCodexConfigOptions.ConfiguredMentionsV2Enabled;
            _currentCodexConfigOptions = snapshot.ProjectInstructionDiscoveryOptions;
            if (previousMentionsV2Enabled != _currentCodexConfigOptions.ConfiguredMentionsV2Enabled)
            {
                OnPropertyChanged(nameof(ComposerReferenceHeader));
                OnPropertyChanged(nameof(ComposerReferenceMenuHeader));
                OnPropertyChanged(nameof(ComposerReferenceMenuToolTip));
                if (CopilotComposerReferenceCatalog.TryParseMention(InputText, out _))
                    RefreshComposerReferenceSuggestions();
            }
            return snapshot;
        }

        private void ApplyChatDeltas(CopilotChatMessage assistantMessage, IReadOnlyList<CopilotStreamDelta> deltas)
        {
            foreach (var delta in deltas)
                CopilotAssistantMessagePresenter.ApplyStreamDelta(assistantMessage, delta);
            PersistState();
        }

        private void ApplyProviderRetryOnUiThread(CopilotChatMessage assistantMessage, CopilotProviderRetryInfo retry)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ApplyProviderRetry(assistantMessage, retry));
                return;
            }

            ApplyProviderRetry(assistantMessage, retry);
        }

        private void ApplyProviderConnectionRecoveryOnUiThread(
            CopilotChatMessage assistantMessage,
            CopilotProviderConnectionRecoveryInfo recovery)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher != null && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(() => ApplyProviderConnectionRecovery(assistantMessage, recovery));
                return;
            }

            ApplyProviderConnectionRecovery(assistantMessage, recovery);
        }

        private static void ApplyPreparedTurnRequestOnUiThread(
            CopilotChatMessage userMessage,
            CopilotPreparedTurnRequest preparedRequest)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                userMessage.RequestContent = preparedRequest.Content;
                userMessage.ChatAttachmentContextCaptured = preparedRequest.ChatAttachmentContextCaptured;
            });
        }

        private void ApplyReviewEnteredOnUiThread(
            CopilotChatMessage assistantMessage,
            CopilotWorkspaceReviewTargetContext target)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                var result = CopilotAssistantMessagePresenter.ApplyReviewEntered(assistantMessage, target);
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
            });
        }

        private void ApplyReviewExitedOnUiThread(
            CopilotChatMessage assistantMessage,
            CopilotWorkspaceReviewTargetContext target,
            string reviewText,
            bool reviewTextTruncated)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                var result = CopilotAssistantMessagePresenter.ApplyReviewExited(
                    assistantMessage,
                    target,
                    reviewText,
                    reviewTextTruncated);
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
            });
        }

        private void ApplyWorkspaceDiffUpdatedOnUiThread(
            CopilotChatMessage assistantMessage,
            CopilotTurnWorkspaceDiffSnapshot snapshot)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                var result = CopilotAssistantMessagePresenter.ApplyWorkspaceDiffUpdated(assistantMessage, snapshot);
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
            });
        }

        private void ApplyCodeReviewSnapshotUpdatedOnUiThread(
            CopilotChatMessage assistantMessage,
            CopilotCodeReviewSnapshot snapshot)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                var result = CopilotAssistantMessagePresenter.ApplyCodeReviewSnapshotUpdated(
                    assistantMessage,
                    snapshot);
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
            });
        }

        private static void ApplyTokenUsageUpdatedOnUiThread(
            CopilotChatMessage assistantMessage,
            CopilotTokenUsage usage)
        {
            CopilotUiDispatcher.Invoke(() => assistantMessage.SetReportedUsage(usage));
        }

        private void ApplyProviderRetry(CopilotChatMessage assistantMessage, CopilotProviderRetryInfo retry)
        {
            var result = CopilotAssistantMessagePresenter.ApplyAgentEvent(
                assistantMessage,
                CopilotAgentEvent.RuntimeDiagnostic(retry.ToDiagnosticText()));
            if (result.PersistenceMode != CopilotAgentEventPersistenceMode.None)
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
        }

        private void ApplyProviderConnectionRecovery(
            CopilotChatMessage assistantMessage,
            CopilotProviderConnectionRecoveryInfo recovery)
        {
            var result = CopilotAssistantMessagePresenter.ApplyAgentEvent(
                assistantMessage,
                CopilotAgentEvent.FromProviderConnectionRecovery(recovery));
            if (result.PersistenceMode != CopilotAgentEventPersistenceMode.None)
                PersistState(immediate: result.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate);
        }

        private void ApplyAgentEvents(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            IReadOnlyList<CopilotAgentEvent> agentEvents)
        {
            var persistState = false;
            var persistImmediately = false;
            var refreshAgentTasks = false;
            var refreshUserQuestionState = false;
            try
            {
                foreach (var agentEvent in agentEvents)
                {
                    if (agentEvent.Type == CopilotAgentEventType.SteeringDelivered)
                    {
                        hostedRun.RecordDeliveredSteeringAwaitingCheckpoint(
                            agentEvent.SteeringMessages,
                            conversation.AgentSessionCheckpoint);
                        continue;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.SteeringRecovery
                        && RestoreUndeliveredSteering(conversation, agentEvent.SteeringMessages))
                    {
                        persistState = true;
                        persistImmediately = true;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.CheckpointReady)
                    {
                        _taskHost.MarkCheckpointReady(hostedRun.Id);
                        continue;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.CheckpointUpdated)
                    {
                        if (hostedRun.State == CopilotHostedRunState.CancelRequested
                            || agentEvent.SessionCheckpoint?.IsStructurallyValid() != true
                            || agentEvent.TaskLedger == null)
                        {
                            continue;
                        }

                        conversation.AgentSessionCheckpoint = agentEvent.SessionCheckpoint;
                        conversation.UpdateLatestAgentTaskEventJournal(agentEvent.SessionCheckpoint.TaskEventJournal);
                        var deliveredBatch = hostedRun.GetDeliveredSteeringAwaitingCheckpoint();
                        if (deliveredBatch.Messages.Count > 0
                            && CopilotSteeringRecovery.AreNewMessagesIncludedInCheckpoint(
                                deliveredBatch.BaselineCheckpoint,
                                agentEvent.SessionCheckpoint,
                                deliveredBatch.Messages))
                        {
                            // Commit the recovery records in the same persisted state
                            // update as the checkpoint so a process exit cannot lose or
                            // replay an instruction.
                            var committedBatch = hostedRun.TakeDeliveredSteeringAwaitingCheckpoint();
                            CopilotSteeringRecovery.RemovePending(
                                conversation,
                                committedBatch.Messages);
                        }
                        persistState = true;
                        persistImmediately = true;
                        continue;
                    }

                    if (agentEvent.Type == CopilotAgentEventType.PlanUpdated
                        && hostedRun.State == CopilotHostedRunState.CancelRequested)
                    {
                        continue;
                    }

                    var presentationResult = CopilotAssistantMessagePresenter.ApplyAgentEvent(assistantMessage, agentEvent);
                    refreshAgentTasks |= agentEvent.Type == CopilotAgentEventType.PlanUpdated
                        && ReferenceEquals(conversation, SelectedConversation);
                    refreshUserQuestionState |= agentEvent.Type is CopilotAgentEventType.UserQuestionRequested
                        or CopilotAgentEventType.UserQuestionResolved
                        or CopilotAgentEventType.Error
                        or CopilotAgentEventType.Completed;
                    if (agentEvent.Type == CopilotAgentEventType.ToolResult
                        && agentEvent.ToolResult?.Success == true
                        && agentEvent.ToolExecution != null
                        && string.Equals(agentEvent.ToolExecution.ToolName, "RollbackWorkspacePatchEnvelope", StringComparison.Ordinal))
                    {
                        var rollbackTrace = assistantMessage.AgentTraceEntries.FirstOrDefault(trace =>
                            string.Equals(trace.CallId, agentEvent.ToolExecution.CallId, StringComparison.Ordinal));
                        if (rollbackTrace?.IsCompletedWorkspaceRollback == true)
                            persistState |= conversation.MarkWorkspaceChangeSetRolledBack(rollbackTrace.WorkspaceChangeSetId);
                    }
                    if (!presentationResult.IsHandled || presentationResult.PersistenceMode == CopilotAgentEventPersistenceMode.None)
                        continue;

                    persistState = true;
                    persistImmediately |= presentationResult.PersistenceMode == CopilotAgentEventPersistenceMode.Immediate;
                    if (agentEvent.Type == CopilotAgentEventType.ToolResult
                        && agentEvent.ToolResult?.DelegatedRunUsage != null)
                    {
                        CaptureSubagentCompletionNotice(
                            conversation,
                            agentEvent.ToolResult);
                    }
                }
            }
            finally
            {
                if (persistState)
                    PersistState(immediate: persistImmediately);
                if (refreshAgentTasks)
                    RefreshAgentTasks();
                if (refreshUserQuestionState)
                    NotifyUserQuestionStateChanged();
            }
        }

        private bool RestoreUndeliveredSteering(
            CopilotConversationRecord conversation,
            IReadOnlyList<CopilotSteeringMessageSnapshot> messages)
        {
            var isSelectedConversation = ReferenceEquals(conversation, SelectedConversation);
            var restored = CopilotSteeringRecovery.RestoreMessagesToDraft(conversation, messages);
            var changed = restored;
            changed |= CopilotSteeringRecovery.RemovePending(conversation, messages);
            if (!changed)
                return false;

            if (restored && isSelectedConversation)
                InputText = conversation.DraftText;
            if (restored)
            {
                RefreshCompactHistoryConversations();
                if (HasConversationSearchQuery)
                    RefreshFilteredConversations();
            }
            return true;
        }

        private void ResolveDeliveredSteeringAtTerminal(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotAgentSessionCheckpoint? checkpoint,
            bool discard)
        {
            var deliveredBatch = hostedRun.TakeDeliveredSteeringAwaitingCheckpoint();
            if (deliveredBatch.Messages.Count == 0)
                return;
            if (discard
                || CopilotSteeringRecovery.AreNewMessagesIncludedInCheckpoint(
                    deliveredBatch.BaselineCheckpoint,
                    checkpoint,
                    deliveredBatch.Messages))
            {
                CopilotSteeringRecovery.RemovePending(conversation, deliveredBatch.Messages);
                return;
            }

            if (!CopilotSteeringRecovery.RestorePendingMessagesToDraft(
                conversation,
                deliveredBatch.Messages))
                return;

            if (ReferenceEquals(conversation, SelectedConversation))
                InputText = conversation.DraftText;
            RefreshCompactHistoryConversations();
            if (HasConversationSearchQuery)
                RefreshFilteredConversations();
        }

        private async Task<CopilotGoalPostTurnResult> ProcessGoalAfterTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            string boundGoalId,
            CopilotTokenUsage turnUsage)
        {
            if (!hostedRun.IsAgent || string.IsNullOrWhiteSpace(boundGoalId))
                return CopilotGoalPostTurnResult.Empty;

            var context = CopilotUiDispatcher.Invoke(
                () =>
                {
                    var goal = conversation.Goal;
                    if (goal == null
                        || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
                    {
                        return null;
                    }
                    if (!goal.IsActive)
                    {
                        TryAccountPausedBoundGoalTurn(
                            conversation,
                            assistantMessage,
                            boundGoalId,
                            turnUsage,
                            hostedRun.ElapsedSeconds);
                        return null;
                    }

                    var taskEventJournal = conversation.LatestAgentTaskEventJournal;
                    if (taskEventJournal?.IsStructurallyValid() != true)
                        taskEventJournal = conversation.AgentSessionCheckpoint?.TaskEventJournal;
                    return new CopilotGoalEvaluationContext(
                        goal,
                        CopilotConversationRequestBuilder
                            .CaptureHistorySnapshot(conversation)
                            .VisibleMessages,
                        CopilotGoalTurnEvidence.Capture(
                            assistantMessage,
                            taskEventJournal));
                },
                fallback: null as CopilotGoalEvaluationContext);
            if (context == null)
                return CopilotGoalPostTurnResult.Empty;

            CopilotGoalEvaluationResult? evaluation = null;
            if (context.TurnEvidence.StopReason == CopilotAgentStopReason.Completed
                && !context.TurnEvidence.WasResponseInterrupted
                && userMessage.RequestMode is CopilotAgentMode.Auto or CopilotAgentMode.Code)
            {
                try
                {
                    evaluation = await _goalCompletionEvaluator.EvaluateAsync(
                        requestProfile,
                        context.Goal,
                        context.Transcript,
                        context.TurnEvidence,
                        hostedRun.CancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (hostedRun.CancellationToken.IsCancellationRequested)
                {
                    hostedRun.SuppressAutomaticFollowUpDispatch();
                    CopilotUiDispatcher.Invoke(
                        () => TryAccountBoundGoalAfterEvaluationCancellation(
                            conversation,
                            assistantMessage,
                            boundGoalId,
                            turnUsage,
                            hostedRun.ElapsedSeconds),
                        fallback: false);
                    return CopilotGoalPostTurnResult.Empty;
                }
            }

            var evaluationUsage = evaluation?.Usage ?? CopilotTokenUsage.Empty;
            var decision = CopilotGoalContinuationPolicy.Evaluate(
                context.Goal,
                userMessage.RequestMode,
                context.TurnEvidence.StopReason,
                context.TurnEvidence.WasResponseInterrupted,
                turnUsage.Add(evaluationUsage),
                hostedRun.ElapsedSeconds,
                evaluation,
                context.TurnEvidence,
                DateTimeOffset.UtcNow);
            var applied = CopilotUiDispatcher.Invoke(
                () =>
                {
                    var currentGoal = conversation.Goal;
                    if (currentGoal == null
                        || !string.Equals(currentGoal.Id, context.Goal.Id, StringComparison.Ordinal))
                    {
                        return false;
                    }
                    if (hostedRun.CancellationToken.IsCancellationRequested)
                    {
                        hostedRun.SuppressAutomaticFollowUpDispatch();
                        TryAccountBoundGoalAfterEvaluationCancellation(
                            conversation,
                            assistantMessage,
                            boundGoalId,
                            turnUsage.Add(evaluationUsage),
                            hostedRun.ElapsedSeconds);
                        return false;
                    }
                    if (!currentGoal.IsActive)
                    {
                        TryAccountPausedBoundGoalTurn(
                            conversation,
                            assistantMessage,
                            boundGoalId,
                            turnUsage.Add(evaluationUsage),
                            hostedRun.ElapsedSeconds);
                        return false;
                    }

                    conversation.Goal = decision.Goal;
                    CopilotAssistantMessagePresenter.AppendExecutionTrace(
                        assistantMessage,
                        "Goal "
                        + decision.Action.ToString().ToLowerInvariant()
                        + " · "
                        + CopilotAgentTraceEntry.Sanitize(decision.Reason));
                    return true;
                },
                fallback: false);
            if (!applied)
                return new CopilotGoalPostTurnResult(evaluationUsage, string.Empty, string.Empty, false);

            return new CopilotGoalPostTurnResult(
                evaluationUsage,
                decision.Goal.Id,
                decision.Reason,
                decision.Action == CopilotGoalTurnAction.QueueContinuation);
        }

        private static bool TryAccountBoundGoalAfterEvaluationCancellation(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string boundGoalId,
            CopilotTokenUsage usage,
            long elapsedSeconds)
        {
            var goal = conversation.Goal;
            if (goal?.State == CopilotConversationGoalState.Paused)
            {
                return TryAccountPausedBoundGoalTurn(
                    conversation,
                    assistantMessage,
                    boundGoalId,
                    usage,
                    elapsedSeconds);
            }
            if (goal?.IsActive != true
                || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
            {
                return false;
            }

            const string reason =
                "主 Agent 轮次已经完成，但持续目标的独立完成判定被取消；本轮结果与用量已保留，目标已暂停，也不会自动续作。";
            conversation.Goal = goal.WithTurnOutcome(
                CopilotConversationGoalState.Paused,
                usage,
                elapsedSeconds,
                evaluated: false,
                continued: false,
                reason,
                DateTimeOffset.UtcNow);
            CopilotAssistantMessagePresenter.AppendExecutionTrace(
                assistantMessage,
                "Goal paused · completion evaluation cancelled after the bound turn completed; usage retained.");
            return true;
        }

        private static bool TryAccountPausedBoundGoalTurn(
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string boundGoalId,
            CopilotTokenUsage usage,
            long elapsedSeconds)
        {
            var goal = conversation.Goal;
            if (goal?.State != CopilotConversationGoalState.Paused
                || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
            {
                return false;
            }

            const string reason =
                "用户在本轮收尾前暂停了持续目标；本轮用量已记入历史，完成判定未应用，也不会自动续作。";
            conversation.Goal = goal.WithTurnOutcome(
                CopilotConversationGoalState.Paused,
                usage,
                elapsedSeconds,
                evaluated: false,
                continued: false,
                reason,
                DateTimeOffset.UtcNow);
            CopilotAssistantMessagePresenter.AppendExecutionTrace(
                assistantMessage,
                "Goal paused · completed bound turn accounted without evaluation or continuation.");
            return true;
        }

        private bool TryQueueGoalContinuation(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            CopilotProfileConfig requestProfile,
            CopilotAgentHostContextSnapshot completedTurnSnapshot,
            string goalId,
            string reason)
        {
            if (hostedRun.CancellationToken.IsCancellationRequested)
            {
                if (TryPauseBoundGoalBeforeContinuationQueueing(
                    hostedRun,
                    conversation,
                    assistantMessage,
                    goalId))
                {
                    _followUpQueue.CancelAutomaticGoalContinuations(conversation.Id);
                    PersistState(immediate: true);
                }
                return false;
            }

            var goal = conversation.Goal;
            if (goal?.IsActive != true
                || !string.Equals(goal.Id, goalId, StringComparison.Ordinal))
            {
                return false;
            }

            if (_followUpQueue.HasContinuationForGoal(conversation.Id, goalId))
            {
                return true;
            }

            var prompt = CopilotGoalContinuationPrompt.Build(goal, reason);
            var currentProfile = _config.FindProfile(conversation.ProfileId)
                ?? _config.FindProfile(requestProfile.Id)
                ?? _config.GetPreferredDefaultProfile();
            if (currentProfile?.IsConfigured != true)
            {
                const string pauseReason =
                    "当前配置没有可用于下一轮持续目标任务的模型，目标已暂停。";
                conversation.Goal = goal.WithState(
                    CopilotConversationGoalState.Paused,
                    DateTimeOffset.UtcNow,
                    pauseReason);
                PersistState(immediate: true);
                return false;
            }

            var requestProfileSnapshot = CreateConversationRequestProfile(
                currentProfile,
                conversation,
                CopilotAgentMode.Auto,
                completedTurnSnapshot.ProjectInstructionDiscoveryOptions);
            var runtimeConfigSnapshot = CaptureTurnRuntimeConfigSnapshot();
            var submissionContext = CopilotGoalContinuationContext.Capture(
                completedTurnSnapshot,
                conversation);
            var queueRequest = new CopilotQueuedFollowUpRequest(
                conversation.Id,
                conversation.Title,
                prompt,
                CopilotAgentMode.Auto,
                requestProfileSnapshot,
                submissionContext,
                AgentSkillReference: null,
                RuntimeConfigSnapshot: runtimeConfigSnapshot,
                WorkspaceReviewTarget: null,
                GoalId: goalId);
            if (!_followUpQueue.TrySchedule(
                queueRequest,
                runNext: false,
                ExecuteQueuedFollowUpAsync,
                out _,
                out var admission))
            {
                var pauseReason = "无法排入下一轮持续目标任务："
                    + GetRequestAdmissionText(admission)
                    + "。目标已暂停，避免静默丢失续作。";
                conversation.Goal = goal.WithState(
                    CopilotConversationGoalState.Paused,
                    DateTimeOffset.UtcNow,
                    pauseReason);
                PersistState(immediate: true);
                return false;
            }

            PersistState(immediate: true);
            return true;
        }

        private static bool TryPauseBoundGoalBeforeContinuationQueueing(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string goalId)
        {
            var goal = conversation.Goal;
            if (goal?.IsActive != true
                || !string.Equals(goal.Id, goalId, StringComparison.Ordinal))
            {
                return false;
            }

            var reason = hostedRun.RunControl?.Intent switch
            {
                CopilotAgentControlIntent.Pause =>
                    "用户在完成判定后暂停了当前 Agent；本轮结果已保留，自动续作未排队，持续目标已暂停。",
                CopilotAgentControlIntent.Cancel =>
                    "用户在完成判定后取消了当前 Agent；本轮结果已保留，自动续作未排队，持续目标已暂停。",
                _ =>
                    "完成判定后的 Agent 收尾被中止；本轮结果已保留，自动续作未排队，持续目标已安全暂停。",
            };
            conversation.Goal = goal.WithState(
                CopilotConversationGoalState.Paused,
                DateTimeOffset.UtcNow,
                reason);
            CopilotAssistantMessagePresenter.AppendExecutionTrace(
                assistantMessage,
                "Goal paused · continuation queueing cancelled after the completed turn.");
            return true;
        }
    }
}
