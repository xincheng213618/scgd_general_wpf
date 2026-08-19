using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotTurnEventState(
        string TurnId,
        CopilotAgentMode Mode,
        bool Started,
        CopilotPreparedTurnRequest? PreparedChatRequest,
        bool ReviewEntered,
        CopilotWorkspaceReviewTargetContext? ReviewTarget,
        CopilotCodeReviewSnapshot? PendingCodeReviewSnapshot,
        CopilotCodeReviewSnapshot? CodeReviewSnapshot,
        string ReviewText,
        bool ReviewTextTruncated,
        bool ReviewExited,
        bool WorkspaceDiffExpected,
        CopilotTurnWorkspaceDiffSnapshot? WorkspaceDiff,
        CopilotTurnPlanSnapshot? Plan,
        CopilotTurnAnswerLifecycleState AnswerLifecycle,
        CopilotTokenUsage? TokenUsage,
        CopilotTurnProviderRetryLifecycleState ProviderRetryLifecycle,
        CopilotTurnProviderConnectionRecoveryLifecycleState ProviderConnectionRecoveryLifecycle,
        CopilotTurnBudgetLifecycleState BudgetLifecycle,
        CopilotTurnCheckpointLifecycleState CheckpointLifecycle,
        CopilotTurnHookLifecycleState HookLifecycle,
        CopilotTurnToolLifecycleState ToolLifecycle,
        CopilotTurnUserQuestionLifecycleState UserQuestionLifecycle,
        CopilotTurnSteeringLifecycleState SteeringLifecycle,
        CopilotTurnApprovalLifecycleState ApprovalLifecycle,
        bool AgentCompleted,
        CopilotTurnStatus? TerminalStatus,
        CopilotTurnError? TerminalError,
        CopilotTurnResult? Completion)
    {
        public bool ChatRequestPrepared => PreparedChatRequest.HasValue;

        public bool CodeReviewSnapshotExpected => PendingCodeReviewSnapshot != null;

        public static CopilotTurnEventState Create(
            CopilotAgentMode mode,
            string turnId = CopilotTurnStartedEvent.DefaultTurnId)
        {
            if (!Enum.IsDefined(mode))
                throw new ArgumentOutOfRangeException(nameof(mode));

            return new CopilotTurnEventState(
                CopilotTurnStartedEvent.NormalizeTurnId(turnId),
                mode,
                false,
                null,
                false,
                null,
                null,
                null,
                string.Empty,
                false,
                false,
                false,
                null,
                null,
                CopilotTurnAnswerLifecycleState.Empty,
                null,
                CopilotTurnProviderRetryLifecycleState.Empty,
                CopilotTurnProviderConnectionRecoveryLifecycleState.Empty,
                CopilotTurnBudgetLifecycleState.Empty,
                CopilotTurnCheckpointLifecycleState.Empty,
                CopilotTurnHookLifecycleState.Empty,
                CopilotTurnToolLifecycleState.Empty,
                CopilotTurnUserQuestionLifecycleState.Empty,
                CopilotTurnSteeringLifecycleState.Empty,
                CopilotTurnApprovalLifecycleState.Empty,
                false,
                null,
                null,
                null);
        }
    }

    internal static class CopilotTurnEventReducer
    {
        public static CopilotTurnEventState Reduce(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            ArgumentNullException.ThrowIfNull(turnEvent);
            if (state.TerminalStatus != null)
                throw new InvalidOperationException("Copilot turn emitted an event after completion.");
            if (turnEvent is not CopilotTurnStartedEvent && !state.Started)
                throw new InvalidOperationException("Copilot turn emitted an event before its started event.");
            if (state.TerminalError != null && turnEvent is not CopilotTurnCompletedEvent)
                throw new InvalidOperationException("Copilot turn emitted an event after its error event.");

            return turnEvent switch
            {
                CopilotTurnStartedEvent started => ReduceStarted(state, started),
                CopilotTurnErrorEvent error => ReduceError(state, error),
                CopilotTurnRuntimeDiagnosticEvent => state,
                CopilotTurnStatePersistenceBarrierEvent barrier =>
                    ReduceStatePersistenceBarrier(state, barrier),
                CopilotTurnRequestPreparedEvent prepared => ReduceRequestPrepared(state, prepared),
                CopilotTurnChatDeltaEvent => ReduceChatProgress(state, turnEvent),
                CopilotTurnChatAnswerResetEvent => ReduceChatProgress(state, turnEvent),
                CopilotTurnProviderRetryEvent providerRetry => ReduceProviderRetry(state, providerRetry),
                CopilotTurnProviderConnectionRecoveryEvent connectionRecovery =>
                    ReduceProviderConnectionRecovery(state, connectionRecovery),
                CopilotTurnReviewEnteredEvent reviewEntered => ReduceReviewEntered(state, reviewEntered),
                CopilotTurnReviewExitedEvent reviewExited => ReduceReviewExited(state, reviewExited),
                CopilotTurnAgentEvent agent => ReduceAgentEvent(state, agent),
                CopilotTurnCodeReviewSnapshotUpdatedEvent codeReview => ReduceCodeReviewSnapshotUpdated(state, codeReview),
                CopilotTurnWorkspaceDiffUpdatedEvent workspaceDiff => ReduceWorkspaceDiffUpdated(state, workspaceDiff),
                CopilotTurnPlanUpdatedEvent plan => ReducePlanUpdated(state, plan),
                CopilotTurnTokenUsageUpdatedEvent tokenUsage => ReduceTokenUsageUpdated(state, tokenUsage),
                CopilotTurnCompletedEvent completed => ReduceCompletion(state, completed),
                _ => throw new InvalidOperationException(
                    $"Unsupported Copilot turn event: {turnEvent.GetType().Name}."),
            };
        }

        public static CopilotTurnResult RequireCompletion(CopilotTurnEventState state)
        {
            if (state.TerminalStatus == null)
                throw new InvalidOperationException("Copilot turn ended without a completion event.");
            return state.Completion
                ?? throw new InvalidOperationException(
                    $"Copilot turn ended as {state.TerminalStatus} without a structured result.");
        }

        private static CopilotTurnEventState ReduceStarted(
            CopilotTurnEventState state,
            CopilotTurnStartedEvent started)
        {
            if (state.Started)
                throw new InvalidOperationException("Copilot turn emitted its started event more than once.");
            RequireMatchingTurn(state, started.TurnId, started.Mode);
            if (started.Status != CopilotTurnStatus.InProgress)
                throw new InvalidOperationException("Copilot turn started with a non-running status.");

            return state with { Started = true };
        }

        private static CopilotTurnEventState ReduceError(
            CopilotTurnEventState state,
            CopilotTurnErrorEvent error)
        {
            RequireMatchingTurn(state, error.TurnId, error.Mode);
            if (error.Error?.IsStructurallyValid() != true)
                throw new InvalidOperationException("Copilot turn emitted invalid error metadata.");

            return state with { TerminalError = error.Error };
        }

        private static CopilotTurnEventState ReduceRequestPrepared(
            CopilotTurnEventState state,
            CopilotTurnRequestPreparedEvent prepared)
        {
            RequireChatMode(state, prepared);
            if (state.ChatRequestPrepared)
                throw new InvalidOperationException("Copilot chat turn prepared its request more than once.");
            if (prepared.Request.Content == null)
                throw new InvalidOperationException("Copilot chat turn prepared an invalid request snapshot.");

            return state with { PreparedChatRequest = prepared.Request };
        }

        private static CopilotTurnEventState ReduceChatProgress(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            RequirePreparedChatRequest(state, turnEvent);
            return state;
        }

        private static CopilotTurnEventState ReduceProviderRetry(
            CopilotTurnEventState state,
            CopilotTurnProviderRetryEvent providerRetry)
        {
            RequirePreparedChatRequest(state, providerRetry);
            if (providerRetry.Retry == null)
                throw new InvalidOperationException("Copilot provider retry event has no retry metadata.");

            return state with
            {
                ProviderRetryLifecycle = state.ProviderRetryLifecycle.Observe(providerRetry.Retry),
            };
        }

        private static CopilotTurnEventState ReduceProviderConnectionRecovery(
            CopilotTurnEventState state,
            CopilotTurnProviderConnectionRecoveryEvent connectionRecovery)
        {
            RequirePreparedChatRequest(state, connectionRecovery);
            if (connectionRecovery.Recovery == null)
            {
                throw new InvalidOperationException(
                    "Copilot provider connection-recovery event has no recovery metadata.");
            }

            return state with
            {
                ProviderConnectionRecoveryLifecycle =
                    state.ProviderConnectionRecoveryLifecycle.Observe(connectionRecovery.Recovery),
            };
        }

        private static CopilotTurnEventState ReduceAgentEvent(
            CopilotTurnEventState state,
            CopilotTurnAgentEvent agent)
        {
            RequireAgentMode(state, agent);
            if (state.Mode == CopilotAgentMode.Review && !state.ReviewEntered)
                throw new InvalidOperationException("Copilot Review emitted an Agent event before entering review mode.");
            if (state.ReviewExited)
                throw new InvalidOperationException("Copilot Review emitted an Agent event after exiting review mode.");
            if (state.AgentCompleted)
                throw new InvalidOperationException("Copilot Agent emitted an event after its completed item.");
            if (state.WorkspaceDiffExpected)
                throw new InvalidOperationException("Copilot Agent emitted another event before its workspace diff update.");
            if (state.CodeReviewSnapshotExpected)
                throw new InvalidOperationException("Copilot Review emitted another event before its code review snapshot update.");
            if (agent.Event == null)
                throw new InvalidOperationException("Copilot Agent event has no payload.");

            CopilotAgentEventProtocol.Validate(agent.Event);
            var hookLifecycle = state.HookLifecycle.Observe(agent.Event);
            var answerLifecycle = state.AnswerLifecycle.Observe(agent.Event);
            var budgetLifecycle = state.BudgetLifecycle.Observe(agent.Event);
            var checkpointLifecycle = state.CheckpointLifecycle.Observe(agent.Event);
            var userQuestionLifecycle = state.UserQuestionLifecycle.Observe(
                agent.Event,
                state.TurnId);
            var steeringLifecycle = state.SteeringLifecycle.Observe(agent.Event);
            var approvalLifecycle = state.ApprovalLifecycle.Observe(agent.Event);
            var toolLifecycle = state.ToolLifecycle.Observe(agent.Event);
            var workspaceDiffExpected = agent.Event.Type == CopilotAgentEventType.ToolResult
                && agent.Event.ToolResult?.Success == true
                && agent.Event.ToolResult.WorkspaceMutation != null;
            CopilotCodeReviewSnapshot? pendingCodeReviewSnapshot = null;
            if (state.Mode == CopilotAgentMode.Review)
            {
                CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(
                    state.ReviewTarget!,
                    state.CodeReviewSnapshot,
                    agent.Event,
                    out pendingCodeReviewSnapshot);
            }
            return state with
            {
                AgentCompleted = agent.Event.Type == CopilotAgentEventType.Completed,
                WorkspaceDiffExpected = workspaceDiffExpected,
                PendingCodeReviewSnapshot = pendingCodeReviewSnapshot,
                AnswerLifecycle = answerLifecycle,
                BudgetLifecycle = budgetLifecycle,
                CheckpointLifecycle = checkpointLifecycle,
                HookLifecycle = hookLifecycle,
                ToolLifecycle = toolLifecycle,
                UserQuestionLifecycle = userQuestionLifecycle,
                SteeringLifecycle = steeringLifecycle,
                ApprovalLifecycle = approvalLifecycle,
            };
        }

        private static CopilotTurnEventState ReduceStatePersistenceBarrier(
            CopilotTurnEventState state,
            CopilotTurnStatePersistenceBarrierEvent barrier)
        {
            if (state.AgentCompleted)
            {
                throw new InvalidOperationException(
                    "Copilot turn requested state persistence after its Agent completed item.");
            }

            return state;
        }

        private static CopilotTurnEventState ReduceCodeReviewSnapshotUpdated(
            CopilotTurnEventState state,
            CopilotTurnCodeReviewSnapshotUpdatedEvent codeReview)
        {
            RequireReviewMode(state, codeReview);
            if (!state.ReviewEntered)
                throw new InvalidOperationException("Copilot Review emitted a code review snapshot before entering review mode.");
            if (state.ReviewExited)
                throw new InvalidOperationException("Copilot Review emitted a code review snapshot after exiting review mode.");
            if (!state.CodeReviewSnapshotExpected)
                throw new InvalidOperationException("Copilot Review emitted a code review snapshot without a matching Git diff result or findings submission.");
            if (codeReview.Snapshot?.IsStructurallyValid() != true
                || !CopilotTurnCodeReviewSnapshotCapture.MatchesTarget(
                    state.ReviewTarget!,
                    codeReview.Snapshot)
                || codeReview.Snapshot != state.PendingCodeReviewSnapshot)
            {
                throw new InvalidOperationException("Copilot Review emitted an invalid or mismatched code review snapshot.");
            }

            return state with
            {
                PendingCodeReviewSnapshot = null,
                CodeReviewSnapshot = codeReview.Snapshot.CreateSnapshot(),
            };
        }

        private static CopilotTurnEventState ReduceWorkspaceDiffUpdated(
            CopilotTurnEventState state,
            CopilotTurnWorkspaceDiffUpdatedEvent workspaceDiff)
        {
            RequireAgentMode(state, workspaceDiff);
            if (!state.WorkspaceDiffExpected)
                throw new InvalidOperationException("Copilot Agent emitted a workspace diff without a matching mutation result.");
            if (state.ReviewExited)
                throw new InvalidOperationException("Copilot Review emitted a workspace diff after exiting review mode.");
            if (workspaceDiff.Snapshot?.IsStructurallyValid() != true)
                throw new InvalidOperationException("Copilot Agent emitted an invalid workspace diff snapshot.");

            return state with
            {
                WorkspaceDiffExpected = false,
                WorkspaceDiff = workspaceDiff.Snapshot,
            };
        }

        private static CopilotTurnEventState ReducePlanUpdated(
            CopilotTurnEventState state,
            CopilotTurnPlanUpdatedEvent plan)
        {
            RequireAgentMode(state, plan);
            if (state.ReviewExited)
                throw new InvalidOperationException("Copilot Review emitted a plan update after exiting review mode.");
            if (plan.Snapshot?.IsStructurallyValid() != true)
                throw new InvalidOperationException("Copilot Agent emitted an invalid plan snapshot.");
            if (CopilotTurnPlanSnapshot.AreEquivalent(state.Plan, plan.Snapshot))
                throw new InvalidOperationException("Copilot Agent emitted a duplicate plan snapshot.");

            return state with { Plan = plan.Snapshot };
        }

        private static CopilotTurnEventState ReduceTokenUsageUpdated(
            CopilotTurnEventState state,
            CopilotTurnTokenUsageUpdatedEvent tokenUsage)
        {
            var current = tokenUsage.Usage;
            if (!current.HasAny
                || current.InputTokens < 0
                || current.OutputTokens < 0
                || current.TotalTokens < current.InputTokens + (long)current.OutputTokens
                || current.CachedInputTokens is < 0
                || current.CachedInputTokens > current.InputTokens)
            {
                throw new InvalidOperationException("Copilot turn emitted an invalid token usage snapshot.");
            }

            if (state.TokenUsage is CopilotTokenUsage previous)
            {
                if (current == previous)
                    throw new InvalidOperationException("Copilot turn emitted a duplicate token usage snapshot.");
                if (current.InputTokens < previous.InputTokens
                    || current.OutputTokens < previous.OutputTokens
                    || current.EffectiveTotalTokens < previous.EffectiveTotalTokens
                    || current.EffectiveCachedInputTokens < previous.EffectiveCachedInputTokens)
                {
                    throw new InvalidOperationException("Copilot turn token usage moved backwards.");
                }
            }

            return state with { TokenUsage = current };
        }

        private static CopilotTurnEventState ReduceReviewEntered(
            CopilotTurnEventState state,
            CopilotTurnReviewEnteredEvent reviewEntered)
        {
            RequireReviewMode(state, reviewEntered);
            if (state.ReviewEntered)
                throw new InvalidOperationException("Copilot Review entered review mode more than once.");
            if (reviewEntered.Target?.IsStructurallyValid() != true)
                throw new InvalidOperationException("Copilot Review entered review mode without a valid target.");

            return state with
            {
                ReviewEntered = true,
                ReviewTarget = reviewEntered.Target.CreateSnapshot(),
            };
        }

        private static CopilotTurnEventState ReduceReviewExited(
            CopilotTurnEventState state,
            CopilotTurnReviewExitedEvent reviewExited)
        {
            RequireReviewMode(state, reviewExited);
            if (!state.ReviewEntered)
                throw new InvalidOperationException("Copilot Review exited review mode before entering it.");
            if (state.ReviewExited)
                throw new InvalidOperationException("Copilot Review exited review mode more than once.");
            if (!state.AgentCompleted)
                throw new InvalidOperationException("Copilot Review exited review mode before its completed item was emitted.");
            if (reviewExited.Target?.IsStructurallyValid() != true)
                throw new InvalidOperationException("Copilot Review exited review mode without a valid target.");
            if (!MatchesReviewTarget(state.ReviewTarget, reviewExited.Target))
                throw new InvalidOperationException("Copilot Review exited review mode with a different target than it entered.");
            state.AnswerLifecycle.ValidateSnapshot(
                reviewExited.ReviewText,
                reviewExited.ReviewTextTruncated);

            return state with
            {
                ReviewText = reviewExited.ReviewText,
                ReviewTextTruncated = reviewExited.ReviewTextTruncated,
                ReviewExited = true,
            };
        }

        private static CopilotTurnEventState ReduceCompletion(
            CopilotTurnEventState state,
            CopilotTurnCompletedEvent completed)
        {
            RequireMatchingTurn(state, completed.TurnId, completed.Mode);
            if (completed.Status == CopilotTurnStatus.InProgress)
                throw new InvalidOperationException("Copilot completion event has a non-terminal status.");

            if (completed.Status == CopilotTurnStatus.Interrupted)
            {
                if (completed.Error != null || state.TerminalError != null)
                    throw new InvalidOperationException("Copilot interrupted turn carried unexpected result or error metadata.");
                if (completed.Result == null)
                    return state with { TerminalStatus = completed.Status };

                ValidateStructuredResult(state, completed.Result);
                return state with
                {
                    TerminalStatus = completed.Status,
                    Completion = completed.Result,
                };
            }
            if (completed.Status == CopilotTurnStatus.Failed)
            {
                if (completed.Result != null
                    || completed.Error?.IsStructurallyValid() != true
                    || state.TerminalError == null
                    || !Equals(state.TerminalError, completed.Error))
                {
                    throw new InvalidOperationException("Copilot failed turn carried invalid terminal metadata.");
                }
                return state with
                {
                    TerminalStatus = completed.Status,
                };
            }
            if (completed.Status != CopilotTurnStatus.Completed)
                throw new InvalidOperationException("Copilot completion event has an unsupported terminal status.");
            if (completed.Result == null || completed.Error != null || state.TerminalError != null)
                throw new InvalidOperationException("Copilot completed turn has invalid result or error metadata.");

            ValidateStructuredResult(state, completed.Result);
            return state with
            {
                TerminalStatus = completed.Status,
                Completion = completed.Result,
            };
        }

        private static void ValidateStructuredResult(
            CopilotTurnEventState state,
            CopilotTurnResult result)
        {
            if (result.Mode != state.Mode)
            {
                throw new InvalidOperationException(
                    $"Copilot turn completed as {result.Mode}, but {state.Mode} was requested.");
            }
            if (state.TokenUsage is CopilotTokenUsage reportedUsage
                && reportedUsage != CopilotTurnTokenUsageUpdatedEvent.Normalize(result.Usage))
            {
                throw new InvalidOperationException(
                    "Copilot turn completed with token usage that did not match its latest update.");
            }
            if (state.Mode == CopilotAgentMode.Chat)
            {
                if (state.PreparedChatRequest is not CopilotPreparedTurnRequest preparedRequest)
                    throw new InvalidOperationException("Copilot chat turn completed before its request was prepared.");
                if (!string.Equals(
                        preparedRequest.Content,
                        result.PreparedUserMessageContent,
                        StringComparison.Ordinal)
                    || preparedRequest.ChatAttachmentContextCaptured != result.ChatAttachmentContextCaptured)
                {
                    throw new InvalidOperationException(
                        "Copilot chat turn completed with a request snapshot that did not match its prepared request.");
                }
            }
            if (state.WorkspaceDiffExpected)
                throw new InvalidOperationException("Copilot Agent turn completed before its workspace diff update.");
            if (state.CodeReviewSnapshotExpected)
                throw new InvalidOperationException("Copilot Review turn completed before its code review snapshot update.");
            if (state.Mode != CopilotAgentMode.Chat && !state.AgentCompleted)
                throw new InvalidOperationException("Copilot Agent turn completed before its completed item was emitted.");
            if (state.Mode != CopilotAgentMode.Chat)
            {
                var agentRunResult = result.AgentRunResult
                    ?? throw new InvalidOperationException("Copilot Agent turn completed without a structured Agent result.");
                state.BudgetLifecycle.ValidateCompletion(agentRunResult);
                state.CheckpointLifecycle.ValidateCompletion(agentRunResult);
                var finalPlan = CopilotTurnPlanSnapshot.FromTaskLedger(
                    agentRunResult.TaskLedger
                    ?? throw new InvalidOperationException("Copilot Agent turn completed without a final task ledger."));
                if (!CopilotTurnPlanSnapshot.AreEquivalent(state.Plan, finalPlan))
                    throw new InvalidOperationException("Copilot Agent turn completed before its final plan update.");
            }
            if (state.Mode == CopilotAgentMode.Review && !state.ReviewExited)
                throw new InvalidOperationException("Copilot Review turn completed before exiting review mode.");
            if (state.Mode == CopilotAgentMode.Review
                && result.AgentRunResult?.StopReason == CopilotAgentStopReason.Completed
                && string.IsNullOrWhiteSpace(state.ReviewText))
            {
                throw new InvalidOperationException("Copilot Review completed without final review text.");
            }
            if (state.Mode == CopilotAgentMode.Review
                && result.AgentRunResult?.StopReason == CopilotAgentStopReason.Completed
                && state.CodeReviewSnapshot != null
                && !state.CodeReviewSnapshot.HasFindingsSubmission())
            {
                throw new InvalidOperationException(
                    "Copilot Review completed before submitting structured findings for its latest Git diff.");
            }
        }

        private static void RequireMatchingTurn(
            CopilotTurnEventState state,
            string turnId,
            CopilotAgentMode mode)
        {
            if (!string.Equals(state.TurnId, turnId, StringComparison.Ordinal))
                throw new InvalidOperationException("Copilot turn lifecycle event referenced a different turn ID.");
            if (state.Mode != mode)
            {
                throw new InvalidOperationException(
                    $"Copilot turn lifecycle event used {mode}, but {state.Mode} was requested.");
            }
        }

        private static void RequirePreparedChatRequest(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            RequireChatMode(state, turnEvent);
            if (!state.ChatRequestPrepared)
            {
                throw new InvalidOperationException(
                    $"Copilot chat turn emitted {turnEvent.GetType().Name} before its request was prepared.");
            }
        }

        private static void RequireChatMode(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            if (state.Mode != CopilotAgentMode.Chat)
            {
                throw new InvalidOperationException(
                    $"Copilot {state.Mode} turn cannot emit {turnEvent.GetType().Name}.");
            }
        }

        private static void RequireAgentMode(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            if (state.Mode == CopilotAgentMode.Chat)
            {
                throw new InvalidOperationException(
                    $"Copilot chat turn cannot emit {turnEvent.GetType().Name}.");
            }
        }

        private static void RequireReviewMode(
            CopilotTurnEventState state,
            CopilotTurnEvent turnEvent)
        {
            if (state.Mode != CopilotAgentMode.Review)
            {
                throw new InvalidOperationException(
                    $"Copilot {state.Mode} turn cannot emit {turnEvent.GetType().Name}.");
            }
        }

        private static bool MatchesReviewTarget(
            CopilotWorkspaceReviewTargetContext? expected,
            CopilotWorkspaceReviewTargetContext actual)
        {
            if (expected == null || expected.Target != actual.Target)
                return false;

            return string.Equals(
                expected.Revision,
                actual.Revision,
                expected.Target == CopilotWorkspaceReviewTarget.Commit
                    ? StringComparison.OrdinalIgnoreCase
                    : StringComparison.Ordinal);
        }
    }
}
