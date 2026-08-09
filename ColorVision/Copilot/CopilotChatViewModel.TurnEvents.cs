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
                        if (CopilotSteeringRecovery.RemovePending(conversation, agentEvent.SteeringMessages))
                        {
                            persistState = true;
                            persistImmediately = true;
                        }
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
                    if (goal?.IsActive != true
                        || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
                    {
                        return null;
                    }

                    return new CopilotGoalEvaluationContext(
                        goal,
                        CopilotConversationRequestBuilder
                            .CaptureHistorySnapshot(conversation)
                            .VisibleMessages,
                        CopilotGoalTurnEvidence.Capture(assistantMessage));
                },
                fallback: null as CopilotGoalEvaluationContext);
            if (context == null)
                return CopilotGoalPostTurnResult.Empty;

            CopilotGoalEvaluationResult? evaluation = null;
            if (context.TurnEvidence.StopReason == CopilotAgentStopReason.Completed
                && !context.TurnEvidence.WasResponseInterrupted
                && userMessage.RequestMode is CopilotAgentMode.Auto or CopilotAgentMode.Code)
            {
                evaluation = await _goalCompletionEvaluator.EvaluateAsync(
                    requestProfile,
                    context.Goal,
                    context.Transcript,
                    context.TurnEvidence,
                    hostedRun.CancellationToken).ConfigureAwait(false);
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
                DateTimeOffset.UtcNow);
            var applied = CopilotUiDispatcher.Invoke(
                () =>
                {
                    if (conversation.Goal?.IsActive != true
                        || !string.Equals(conversation.Goal.Id, context.Goal.Id, StringComparison.Ordinal))
                    {
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

        private bool TryQueueGoalContinuation(
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotAgentHostContextSnapshot completedTurnSnapshot,
            CopilotTurnRuntimeConfigSnapshot completedTurnRuntimeConfig,
            string goalId,
            string reason)
        {
            var goal = conversation.Goal;
            if (goal?.IsActive != true
                || !string.Equals(goal.Id, goalId, StringComparison.Ordinal))
            {
                return false;
            }

            if (_queuedFollowUpsByRunId.Values.Any(item =>
                string.Equals(item.ConversationId, conversation.Id, StringComparison.Ordinal)))
            {
                return true;
            }

            var prompt =
                "继续处理当前持续目标。独立完成评估认为目标尚未达成："
                + CopilotConversationGoal.NormalizeReason(reason)
                + Environment.NewLine
                + "根据现有证据选择下一项最有价值的工作并验证结果；不要把持续目标当作工具、写入、审批复用或扩大范围的授权。";
            var requestProfileSnapshot = requestProfile.Clone();
            var submissionContext = CopilotGoalContinuationContext.Capture(
                completedTurnSnapshot,
                conversation);
            var itemReady = new TaskCompletionSource<CopilotQueuedFollowUp>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            if (!_taskHost.TryScheduleFollowUp(
                conversation.Id,
                CopilotAgentMode.Auto,
                async run =>
                {
                    var queuedItem = await itemReady.Task.ConfigureAwait(false);
                    await ExecuteQueuedFollowUpAsync(run, queuedItem).ConfigureAwait(false);
                },
                out var queuedRun,
                out var admission)
                || queuedRun == null)
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

            var queuedFollowUp = new CopilotQueuedFollowUp(
                queuedRun.Id,
                conversation.Id,
                conversation.Title,
                prompt,
                CopilotAgentMode.Auto,
                requestProfileSnapshot,
                submissionContext,
                goalId,
                runtimeConfigSnapshot: completedTurnRuntimeConfig);
            _queuedFollowUpsByRunId.Add(queuedRun.Id, queuedFollowUp);
            QueuedFollowUps.Add(queuedFollowUp);
            AddQueuedFollowUpRecovery(queuedFollowUp);
            itemReady.SetResult(queuedFollowUp);
            RefreshQueuedFollowUpPositions();
            PersistState(immediate: true);
            return true;
        }
    }
}
