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
        private Task SendAsync() => SendAsync(null, null, null);

        private async Task SendAsync(
            string? directPrompt,
            CopilotAgentMode? directMode,
            string? directRequestContent = null)
        {
            if (directPrompt == null && IsPromptHistorySearchOpen)
            {
                TryCompletePromptHistorySearch();
                return;
            }

            var isDirectSubmission = directPrompt != null;
            var prompt = (directPrompt ?? InputText ?? string.Empty).Trim();
            var modelPrompt = (directRequestContent ?? prompt).Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                return;
            if (!TryValidateComposerCharacterLimit(modelPrompt))
                return;
            if (!isDirectSubmission && !IsEditingMessage)
            {
                if (TryExecuteLocalCommand(prompt)
                    || TryReportCommandInputRecovery(prompt))
                {
                    return;
                }
            }

            var requestMode = directMode ?? ResolveComposerRequestMode();
            if (!CanScheduleComposerRequest(requestMode))
                return;

            var selectedProfile = SelectedProfile;
            if (selectedProfile == null || !selectedProfile.IsConfigured)
            {
                OpenSettings();
                return;
            }

            var requestProfile = CreateCurrentConversationRequestProfile(selectedProfile, SelectedConversation);
            if (!TryValidatePromptBudget(modelPrompt, requestMode, requestProfile))
                return;
            var requestAttachments = isDirectSubmission
                ? Array.Empty<CopilotAttachmentItem>()
                : Attachments.ToArray();
            if (!TryValidateComposerAttachments(requestAttachments))
                return;

            var conversation = EnsureConversation();
            var automaticCompaction = await TryAutoCompactConversationAsync(
                conversation,
                requestProfile,
                modelPrompt);
            if (automaticCompaction == CopilotAutomaticCompactionOutcome.Failed)
                return;

            conversation.ProfileId = requestProfile.Id;
            conversation.ProfileDisplayName = requestProfile.DisplayLabel;
            var replacedUserIndex = -1;
            CopilotChatMessage replacedUserMessage = null!;
            CopilotChatMessage? replacedAssistantMessage = null;
            var isReplacingTurn = !isDirectSubmission && TryResolvePendingMessageEdit(
                conversation,
                out replacedUserIndex,
                out replacedUserMessage,
                out replacedAssistantMessage);
            if (!isDirectSubmission && IsEditingMessage && !isReplacingTurn)
            {
                CancelMessageEdit();
                return;
            }

            var turnSnapshot = isReplacingTurn
                ? CaptureHostedTurnSnapshot(conversation, replacedUserMessage, conversation.Attachments)
                : CaptureHostedTurnSnapshot(conversation, attachmentOverride: requestAttachments);
            requestProfile = CreateConversationRequestProfile(
                selectedProfile,
                conversation,
                turnSnapshot.ProjectInstructionDiscoveryOptions);
            var recoveryRequest = isDirectSubmission ? null : ConsumePendingAgentRecoveryRequest();
            if (!isDirectSubmission)
                requestMode = ConsumeRequestModeOverride();
            var workspaceReviewTarget = isDirectSubmission
                ? null
                : ConsumePendingWorkspaceReviewTarget(requestMode);
            var agentSkillReference = isDirectSubmission
                ? null
                : ResolvePendingAgentSkillReference(prompt);
            if (workspaceReviewTarget == null
                && isReplacingTurn
                && requestMode == CopilotAgentMode.Review
                && replacedUserMessage.WorkspaceReviewTarget?.IsStructurallyValid() == true)
            {
                workspaceReviewTarget = replacedUserMessage.WorkspaceReviewTarget.CreateSnapshot();
            }

            var userMessage = new CopilotChatMessage(CopilotChatRole.User, prompt)
            {
                RequestMode = requestMode,
                WorkspaceReviewTarget = workspaceReviewTarget,
                AgentSkillReference = agentSkillReference,
                RequestContent = directRequestContent ?? string.Empty,
                RecoveryRequest = recoveryRequest,
                Attachments = new ObservableCollection<CopilotAttachmentItem>(turnSnapshot.Attachments),
                AttachmentSnapshotCaptured = true,
            };
            var assistantMessage = CreatePendingAssistantMessage(requestProfile, requestMode);
            var previousCheckpoint = conversation.AgentSessionCheckpoint;

            if (isReplacingTurn)
            {
                if (replacedAssistantMessage != null)
                    conversation.Messages.Remove(replacedAssistantMessage);
                conversation.Messages.Remove(replacedUserMessage);
                conversation.Messages.Insert(replacedUserIndex, userMessage);
                conversation.Messages.Insert(replacedUserIndex + 1, assistantMessage);
                conversation.AgentSessionCheckpoint = null;
            }
            else
            {
                conversation.Messages.Add(userMessage);
                conversation.Messages.Add(assistantMessage);
            }
            UpdateConversationMetadata(conversation, touch: true);
            PersistState();

            if (!_taskHost.TrySchedule(
                conversation.Id,
                userMessage.RequestMode,
                run => ExecuteHostedTurnAsync(run, conversation, requestProfile, userMessage, assistantMessage, turnSnapshot),
                out var hostedRun,
                out var admission)
                || hostedRun == null)
            {
                conversation.Messages.Remove(assistantMessage);
                conversation.Messages.Remove(userMessage);
                if (isReplacingTurn)
                {
                    conversation.Messages.Insert(replacedUserIndex, replacedUserMessage);
                    if (replacedAssistantMessage != null)
                        conversation.Messages.Insert(replacedUserIndex + 1, replacedAssistantMessage);
                    conversation.AgentSessionCheckpoint = previousCheckpoint;
                }
                if (!isDirectSubmission)
                {
                    _pendingAgentRecoveryRequest = recoveryRequest;
                    SetPendingRequestModeOverride(requestMode);
                    SetPendingWorkspaceReviewTarget(workspaceReviewTarget);
                }
                UpdateConversationMetadata(conversation, touch: true);
                PersistState();
                ReportRequestAdmissionFailure(admission);
                if (!isDirectSubmission)
                    OnComposerRequestModeChanged();
                return;
            }

            if (automaticCompaction != CopilotAutomaticCompactionOutcome.Applied)
                DismissLocalCommandResult();
            if (!isDirectSubmission && isReplacingTurn)
            {
                _composerDraftBeforeMessageEdit = null;
                SetMessageEditState(string.Empty, string.Empty);
            }
            if (!isDirectSubmission)
            {
                ConsumeComposerAttachments(conversation);
                InputText = string.Empty;
            }
            await AwaitHostedRunCompletionAsync(hostedRun);
            if (!hostedRun.HasStarted)
                FinalizeCancelledQueuedRun(conversation, assistantMessage);
        }

        private static async Task AwaitHostedRunCompletionAsync(CopilotHostedAgentRun hostedRun)
        {
            try
            {
                await hostedRun.Completion;
            }
            catch (OperationCanceledException) when (hostedRun.CancellationToken.IsCancellationRequested)
            {
            }
        }

        private void FinalizeCancelledQueuedRun(CopilotConversationRecord conversation, CopilotChatMessage assistantMessage)
        {
            if (conversation.RevokeFullAccessGrant())
                OnComposerAccessModeChanged();
            CopilotHostedTurnCompletion.CompleteBeforeStartCancellation(assistantMessage);
            UpdateConversationMetadata(conversation, touch: true);
            PersistState(immediate: true);
            RefreshAgentTasks();
        }

        private Task ExecuteHostedTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot) =>
            ExecuteHostedPreparedTurnAsync(
                hostedRun,
                conversation,
                requestProfile,
                userMessage,
                assistantMessage,
                turnSnapshot,
                refreshExternalContext: true,
                isAutomaticGoalContinuation: false);

        private async Task ExecuteHostedPreparedTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext,
            bool isAutomaticGoalContinuation)
        {
            CopilotUiDispatcher.Invoke(() =>
            {
                CopilotConversationService.MarkTurnStarted(
                    Conversations,
                    conversation,
                    DateTime.Now);

                if (conversation.TryBeginGoalTurn(hostedRun.IsAgent, isAutomaticGoalContinuation))
                {
                    CopilotAssistantMessagePresenter.AppendExecutionTrace(
                        assistantMessage,
                        "Goal continuation deferral consumed · explicit Agent turn owns lifecycle.");
                }
                PersistState(immediate: true);
            });
            var boundGoalId = CopilotUiDispatcher.Invoke(
                () => conversation.Goal?.IsActive == true ? conversation.Goal.Id : string.Empty,
                fallback: string.Empty);
            var goalOutcomeRecorded = false;
            try
            {
                var usage = await RunConversationTurnAsync(
                    hostedRun,
                    conversation,
                    requestProfile,
                    userMessage,
                    assistantMessage,
                    turnSnapshot,
                    refreshExternalContext);
                CopilotHostedTurnCompletion.PrepareTerminalEvidence(assistantMessage);
                var goalResult = await ProcessGoalAfterTurnAsync(
                    hostedRun,
                    conversation,
                    requestProfile,
                    userMessage,
                    assistantMessage,
                    boundGoalId,
                    usage);
                goalOutcomeRecorded = true;
                usage = usage.Add(goalResult.EvaluationUsage);
                CopilotHostedTurnCompletion.CompleteTerminalTurn(conversation, assistantMessage, usage);
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
                if (goalResult.ShouldQueueContinuation)
                {
                    CopilotUiDispatcher.Invoke(() =>
                        TryQueueGoalContinuation(
                            conversation,
                            requestProfile,
                            goalResult.GoalId,
                            goalResult.Reason));
                }
                _ = _conversationTitleCoordinator.QueueAsync(conversation, requestProfile);
            }
            catch (OperationCanceledException) when (hostedRun.CancellationToken.IsCancellationRequested)
            {
                var controlIntent = hostedRun.RunControl?.Intent ?? CopilotAgentControlIntent.None;
                CopilotHostedTurnCompletion.CompleteCancellation(conversation, assistantMessage, controlIntent);
                if (!goalOutcomeRecorded)
                {
                    PauseBoundGoalAfterHostedTurnFailure(
                        hostedRun,
                        conversation,
                        assistantMessage,
                        boundGoalId,
                        controlIntent == CopilotAgentControlIntent.Pause
                            ? "用户暂停了当前 Agent 轮次，持续目标已同步暂停。"
                            : "用户取消了当前 Agent 轮次，持续目标已暂停。");
                }
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
            }
            catch (Exception ex)
            {
                CopilotHostedTurnCompletion.CompleteFailure(conversation, assistantMessage, ex.Message, requestProfile.ApiKey);
                if (!goalOutcomeRecorded)
                {
                    PauseBoundGoalAfterHostedTurnFailure(
                        hostedRun,
                        conversation,
                        assistantMessage,
                        boundGoalId,
                        "Agent 轮次异常结束，持续目标已暂停；请检查本轮错误后使用 /goal resume 重试。");
                }
                UpdateConversationMetadata(conversation, touch: true);
                await PersistStateAndFlushAsync();
            }
            finally
            {
                CopilotUiDispatcher.Invoke(() =>
                {
                    if (conversation.RevokeFullAccessGrant(hostedRun.Id)
                        && ReferenceEquals(SelectedConversation, conversation))
                    {
                        OnComposerAccessModeChanged();
                        SetPendingActionFeedback("本任务的临时自动复核授权已结束，后续受保护操作恢复按需确认。");
                    }
                });
                RefreshAgentTasks();
            }
        }

        private static void PauseBoundGoalAfterHostedTurnFailure(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotChatMessage assistantMessage,
            string boundGoalId,
            string reason)
        {
            if (string.IsNullOrWhiteSpace(boundGoalId))
                return;

            CopilotUiDispatcher.Invoke(() =>
            {
                var goal = conversation.Goal;
                if (goal?.IsActive != true
                    || !string.Equals(goal.Id, boundGoalId, StringComparison.Ordinal))
                {
                    return;
                }

                conversation.Goal = goal.WithTurnOutcome(
                    CopilotConversationGoalState.Paused,
                    CopilotTokenUsage.Empty,
                    hostedRun.ElapsedSeconds,
                    evaluated: false,
                    continued: false,
                    reason,
                    DateTimeOffset.UtcNow);
                CopilotAssistantMessagePresenter.AppendExecutionTrace(
                    assistantMessage,
                    "Goal pause · " + CopilotAgentTraceEntry.Sanitize(reason));
            });
        }

        private async Task<CopilotTokenUsage> RunConversationTurnAsync(
            CopilotHostedAgentRun hostedRun,
            CopilotConversationRecord conversation,
            CopilotProfileConfig requestProfile,
            CopilotChatMessage userMessage,
            CopilotChatMessage assistantMessage,
            CopilotAgentHostContextSnapshot turnSnapshot,
            bool refreshExternalContext)
        {
            var cancellationToken = hostedRun.CancellationToken;
            if (hostedRun.IsAgent)
            {
                CopilotUiDispatcher.Invoke(() =>
                {
                    var previousMode = conversation.AccessMode;
                    var previousTaskId = conversation.FullAccessTaskId;
                    conversation.BindFullAccessGrantToTask(hostedRun.Id, turnSnapshot.SolutionDirectoryPath);
                    if (ReferenceEquals(SelectedConversation, conversation)
                        && (previousMode != conversation.AccessMode
                            || !string.Equals(previousTaskId, conversation.FullAccessTaskId, StringComparison.Ordinal)))
                    {
                        OnComposerAccessModeChanged();
                    }
                });
            }
            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                conversation.AgentSessionCheckpoint = null;
                PersistState();
            }

            var dispatcher = Application.Current?.Dispatcher;
            var streamContext = dispatcher == null
                ? SynchronizationContext.Current
                : new DispatcherSynchronizationContext(dispatcher);
            CopilotStreamDeltaBuffer? deltaBuffer = null;
            CopilotAgentEventBuffer? eventBuffer = null;
            if (userMessage.RequestMode == CopilotAgentMode.Chat)
            {
                deltaBuffer = new CopilotStreamDeltaBuffer(
                    streamContext,
                    deltas => ApplyChatDeltas(assistantMessage, deltas),
                    isOnTargetThread: dispatcher == null ? null : dispatcher.CheckAccess);
            }
            else
            {
                eventBuffer = new CopilotAgentEventBuffer(
                    streamContext,
                    events => ApplyAgentEvents(hostedRun, conversation, assistantMessage, events),
                    isOnTargetThread: dispatcher == null ? null : dispatcher.CheckAccess);
            }

            var sessionCheckpoint = conversation.AgentSessionCheckpoint;
            var accessContext = conversation.AccessContext;
            var turnRequest = new CopilotTurnRequest(
                requestProfile,
                userMessage.RequestMode,
                userMessage.Content,
                userMessage.RequestContent,
                userMessage.ChatAttachmentContextCaptured,
                refreshExternalContext,
                turnSnapshot,
                ResolveConversationHistoryLimits(requestProfile),
                sessionCheckpoint,
                userMessage.RecoveryRequest,
                hostedRun.RunControl,
                _config.AgentDefaults,
                _config.ExternalMcpServers,
                conversation.Id,
                hostedRun.Id,
                accessContext,
                conversation.Goal?.IsActive == true ? conversation.Goal.Objective : string.Empty,
                userMessage.WorkspaceReviewTarget,
                userMessage.AgentSkillReference);
            var eventProtocol = new CopilotTurnEventProtocol(userMessage.RequestMode, hostedRun.Id);
            try
            {
                try
                {
                    await foreach (var turnEvent in _turnRuntime.RunAsync(turnRequest, cancellationToken))
                    {
                        eventProtocol.Observe(turnEvent);

                        switch (turnEvent)
                        {
                            case CopilotTurnStartedEvent:
                                break;
                            case CopilotTurnErrorEvent:
                                break;
                            case CopilotTurnRequestPreparedEvent prepared:
                                ApplyPreparedTurnRequestOnUiThread(userMessage, prepared.Request);
                                break;
                            case CopilotTurnChatDeltaEvent chatDelta:
                                deltaBuffer?.Enqueue(chatDelta.Delta);
                                break;
                            case CopilotTurnProviderRetryEvent providerRetry:
                                hostedRun.RecordProviderRetry(providerRetry.Retry);
                                ApplyProviderRetryOnUiThread(assistantMessage, providerRetry.Retry);
                                break;
                            case CopilotTurnReviewEnteredEvent reviewEntered:
                                ApplyReviewEnteredOnUiThread(assistantMessage, reviewEntered.Target);
                                break;
                            case CopilotTurnAgentEvent agent:
                                if (agent.Event.ProviderRetry != null)
                                    hostedRun.RecordProviderRetry(agent.Event.ProviderRetry);
                                eventBuffer?.Enqueue(agent.Event);
                                break;
                            case CopilotTurnWorkspaceDiffUpdatedEvent workspaceDiff:
                                ApplyWorkspaceDiffUpdatedOnUiThread(assistantMessage, workspaceDiff.Snapshot);
                                break;
                            case CopilotTurnPlanUpdatedEvent plan:
                                eventBuffer?.Enqueue(CopilotAgentEvent.PlanUpdated(plan.Snapshot));
                                break;
                            case CopilotTurnTokenUsageUpdatedEvent tokenUsage:
                                ApplyTokenUsageUpdatedOnUiThread(assistantMessage, tokenUsage.Usage);
                                break;
                            case CopilotTurnReviewExitedEvent reviewExited:
                                if (eventBuffer != null)
                                {
                                    await eventBuffer.CompleteAsync();
                                    eventBuffer = null;
                                }
                                ApplyReviewExitedOnUiThread(
                                    assistantMessage,
                                    reviewExited.Target,
                                    reviewExited.ReviewText,
                                    reviewExited.ReviewTextTruncated);
                                break;
                            case CopilotTurnCompletedEvent:
                                break;
                        }
                    }
                }
                finally
                {
                    if (deltaBuffer != null)
                        await deltaBuffer.CompleteAsync();
                    if (eventBuffer != null)
                        await eventBuffer.CompleteAsync();
                }
            }
            catch (OperationCanceledException) when (
                userMessage.RequestMode != CopilotAgentMode.Chat
                && hostedRun.RunControl?.Intent == CopilotAgentControlIntent.Pause
                && sessionCheckpoint != null)
            {
                conversation.AgentSessionCheckpoint ??= sessionCheckpoint;
                PersistState(immediate: true);
                throw;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception)
            {
                if (userMessage.RequestMode != CopilotAgentMode.Chat
                    && sessionCheckpoint != null
                    && conversation.AgentSessionCheckpoint == null)
                {
                    conversation.AgentSessionCheckpoint = sessionCheckpoint;
                    PersistState(immediate: true);
                }
                throw;
            }

            var result = eventProtocol.RequireCompletion();
            if (result.Mode == CopilotAgentMode.Chat)
            {
                userMessage.RequestContent = result.PreparedUserMessageContent;
                userMessage.ChatAttachmentContextCaptured = result.ChatAttachmentContextCaptured;
                var streamResult = result.ChatStreamResult
                    ?? throw new InvalidOperationException("Chat turn completed without stream result metadata.");
                if (streamResult.IsIncomplete)
                {
                    CopilotUiDispatcher.Invoke(() =>
                        assistantMessage.MarkResponseInterrupted(BuildChatInterruptionDetail(streamResult)));
                }
                else
                {
                    CopilotUiDispatcher.Invoke(() =>
                    {
                        if (assistantMessage.IsResponseContentTruncated)
                        {
                            assistantMessage.MarkResponseInterrupted(
                                "回答达到应用显示上限；已保留前面的内容，可缩小问题范围后重新生成。");
                        }
                    });
                }

                if (eventProtocol.TerminalStatus == CopilotTurnStatus.Interrupted)
                    throw new OperationCanceledException(cancellationToken);
                return result.Usage;
            }

            var agentResult = result.AgentRunResult
                ?? throw new InvalidOperationException("Agent turn completed without an agent result.");
            hostedRun.SetAgentStopReason(agentResult.StopReason);
            if (!CopilotPlanHandoff.IsApprovedExecutionRequest(userMessage.RequestContent))
                userMessage.RequestContent = agentResult.PreparedUserMessageContent;
            assistantMessage.AgentTaskLedger = agentResult.TaskLedger;
            assistantMessage.AgentStopReason = agentResult.StopReason;
            assistantMessage.AgentRunBudget = agentResult.Budget;
            assistantMessage.AgentBlockers = agentResult.Blockers;
            conversation.UpdateLatestAgentTaskEventJournal(agentResult.TaskEventJournal);
            conversation.AgentSessionCheckpoint = agentResult.SessionCheckpoint;
            if (string.IsNullOrWhiteSpace(assistantMessage.Content))
            {
                CopilotAssistantMessagePresenter.SetFallbackContent(assistantMessage, agentResult.StopReason switch
                {
                    CopilotAgentStopReason.Paused => "Agent 任务已暂停；当前任务状态已经保存，可以稍后继续。",
                    CopilotAgentStopReason.Cancelled => "Agent 任务已取消；本轮新 checkpoint 已丢弃。",
                    _ => assistantMessage.Content,
                });
            }
            PersistState(immediate: true);
            if (eventProtocol.TerminalStatus == CopilotTurnStatus.Interrupted)
                throw new OperationCanceledException(cancellationToken);
            return result.Usage;
        }

        private static string BuildChatInterruptionDetail(CopilotChatStreamResult streamResult)
        {
            return streamResult.FinishKind switch
            {
                CopilotChatFinishKind.LengthLimit => "模型因输出长度上限提前结束；已保留现有内容，可发送“继续”补全或重新生成。",
                CopilotChatFinishKind.ContentFiltered => "提供商的内容安全策略提前停止了回答；已保留允许返回的内容。",
                CopilotChatFinishKind.ToolRequested => "模型改为请求工具，但普通 Chat 不执行工具；请改用 Agent 模式继续。",
                CopilotChatFinishKind.Other => string.IsNullOrWhiteSpace(streamResult.FinishReason)
                    ? "提供商提前结束了回答；已保留现有内容，但回答可能不完整。"
                    : $"提供商以未识别的原因提前结束了回答（{streamResult.FinishReason}）；已保留现有内容。",
                _ => string.Empty,
            };
        }
    }
}
