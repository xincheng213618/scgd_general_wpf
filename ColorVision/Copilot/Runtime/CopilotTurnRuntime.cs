using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed class CopilotTurnRuntime : ICopilotTurnRuntime
    {
        private readonly CopilotChatService _chatService;
        private readonly CopilotConversationRequestBuilder _conversationRequestBuilder;
        private readonly CopilotImageUnderstandingService _imageUnderstandingService;
        private readonly CopilotContextRegistry _contextRegistry;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _agentRuntime;
        private readonly CopilotWorkspaceRollbackCoordinator _workspaceRollbackCoordinator;
        private readonly CopilotCodexSessionStartHookLifecycle _sessionStartHookLifecycle;
        private readonly CopilotCodexSessionEndHookLifecycle _sessionEndHookLifecycle;
        private readonly CopilotCodexUserPromptSubmitHookExecutor _userPromptSubmitHookExecutor;
        private readonly CopilotCodexStopHookExecutor _stopHookExecutor;

        public CopilotTurnRuntime(CopilotChatService chatService)
            : this(
                chatService,
                new CopilotCodexSessionStartHookLifecycle(),
                new CopilotCodexSessionEndHookLifecycle(),
                new CopilotCodexUserPromptSubmitHookExecutor(),
                new CopilotCodexStopHookExecutor())
        {
        }

        internal CopilotTurnRuntime(
            CopilotChatService chatService,
            CopilotCodexStopHookExecutor stopHookExecutor)
            : this(
                chatService,
                new CopilotCodexSessionStartHookLifecycle(),
                new CopilotCodexSessionEndHookLifecycle(),
                new CopilotCodexUserPromptSubmitHookExecutor(),
                stopHookExecutor)
        {
        }

        internal CopilotTurnRuntime(
            CopilotChatService chatService,
            CopilotCodexSessionStartHookLifecycle sessionStartHookLifecycle)
            : this(
                chatService,
                sessionStartHookLifecycle,
                new CopilotCodexSessionEndHookLifecycle(),
                new CopilotCodexUserPromptSubmitHookExecutor(),
                new CopilotCodexStopHookExecutor())
        {
        }

        internal CopilotTurnRuntime(
            CopilotChatService chatService,
            CopilotCodexSessionStartHookLifecycle sessionStartHookLifecycle,
            CopilotCodexSessionEndHookLifecycle sessionEndHookLifecycle)
            : this(
                chatService,
                sessionStartHookLifecycle,
                sessionEndHookLifecycle,
                new CopilotCodexUserPromptSubmitHookExecutor(),
                new CopilotCodexStopHookExecutor())
        {
        }

        private CopilotTurnRuntime(
            CopilotChatService chatService,
            CopilotCodexSessionStartHookLifecycle sessionStartHookLifecycle,
            CopilotCodexSessionEndHookLifecycle sessionEndHookLifecycle,
            CopilotCodexUserPromptSubmitHookExecutor userPromptSubmitHookExecutor,
            CopilotCodexStopHookExecutor stopHookExecutor)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _conversationRequestBuilder = new CopilotConversationRequestBuilder();
            _imageUnderstandingService = new CopilotImageUnderstandingService(_chatService);
            _contextRegistry = CopilotContextRegistry.CreateDefault();
            var toolRegistry = CopilotToolRegistry.CreateDefault();
            var toolExecutor = new CopilotToolExecutor();
            _agentRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
                toolRegistry,
                new CopilotAgentContextBuilder(),
                toolExecutor);
            _workspaceRollbackCoordinator = new CopilotWorkspaceRollbackCoordinator(
                toolRegistry,
                toolExecutor);
            _sessionStartHookLifecycle = sessionStartHookLifecycle
                ?? throw new ArgumentNullException(nameof(sessionStartHookLifecycle));
            _sessionEndHookLifecycle = sessionEndHookLifecycle
                ?? throw new ArgumentNullException(nameof(sessionEndHookLifecycle));
            _userPromptSubmitHookExecutor = userPromptSubmitHookExecutor
                ?? throw new ArgumentNullException(nameof(userPromptSubmitHookExecutor));
            _stopHookExecutor = stopHookExecutor
                ?? throw new ArgumentNullException(nameof(stopHookExecutor));
        }

        public IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            return CopilotTurnEventStream.RunAsync(
                request.TaskId,
                request.Mode,
                (eventSink, turnCancellationToken) => RunCoreAsync(
                    request,
                    eventSink,
                    turnCancellationToken),
                cancellationToken);
        }

        private async Task<CopilotTurnResult> RunCoreAsync(
            CopilotTurnRequest request,
            CopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            var bufferedAsyncHookResults = DrainAsyncHookResults(
                request.ConversationId,
                eventSink.OnRuntimeDiagnostic);
            var hookRequest = CreateUserPromptSubmitHookRequest(request);
            var sessionStartOutcome = await _sessionStartHookLifecycle.RunBeforeTurnAsync(
                hookRequest,
                request.HostContext.ConversationHistory.ModelMessages.Count > 0,
                eventSink.OnRuntimeDiagnostic,
                cancellationToken).ConfigureAwait(false);
            if (sessionStartOutcome.ShouldStop)
                throw new CopilotSessionStartHookBlockedException(sessionStartOutcome.StopReason);

            var promptHookOutcome = await _userPromptSubmitHookExecutor.RunAsync(
                hookRequest,
                request.UserText,
                eventSink.OnRuntimeDiagnostic,
                cancellationToken).ConfigureAwait(false);
            if (promptHookOutcome.ShouldStop)
            {
                throw new CopilotUserPromptSubmitHookBlockedException(
                    promptHookOutcome.StopReason);
            }

            var requestStartAsyncHookResults = DrainAsyncHookResults(
                request.ConversationId,
                eventSink.OnRuntimeDiagnostic);
            var asyncHookAdditionalContexts = CopilotCodexAsyncHookResultDelivery
                .GetAdditionalContexts(bufferedAsyncHookResults
                    .Concat(requestStartAsyncHookResults)
                    .ToArray());

            return request.Mode == CopilotAgentMode.Chat
                ? await RunChatAsync(
                    request,
                    sessionStartOutcome.AdditionalContexts,
                    promptHookOutcome.AdditionalContexts,
                    asyncHookAdditionalContexts,
                    eventSink,
                    cancellationToken).ConfigureAwait(false)
                : await RunAgentAsync(
                    request,
                    sessionStartOutcome.AdditionalContexts,
                    promptHookOutcome.AdditionalContexts,
                    asyncHookAdditionalContexts,
                    eventSink,
                    cancellationToken).ConfigureAwait(false);
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(
            string taskId,
            string message) =>
            _agentRuntime.EnqueueSteeringMessage(taskId, message);

        public void QueueSessionStart(
            string conversationId,
            CopilotCodexSessionStartSource source)
        {
            _sessionEndHookLifecycle.Reopen(conversationId);
            _sessionStartHookLifecycle.Queue(conversationId, source);
        }

        public Task<CopilotCodexSessionStartHookOutcome> RunSessionStartHooksAsync(
            CopilotAgentRequest request,
            bool hasPersistedHistory,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken) =>
            _sessionStartHookLifecycle.RunBeforeTurnAsync(
                request,
                hasPersistedHistory,
                onDiagnostic,
                cancellationToken);

        public async Task<CopilotCodexSessionEndHookOutcome> RunSessionEndHooksAsync(
            CopilotAgentRequest request,
            Action<string>? onDiagnostic,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            try
            {
                await CopilotCodexLifecycleHookBackgroundScheduler.Shared
                    .ShutdownSessionAsync(request.ConversationId)
                    .ConfigureAwait(false);
                return await _sessionEndHookLifecycle.EndAsync(
                    request,
                    onDiagnostic,
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                _sessionStartHookLifecycle.End(request.ConversationId);
            }
        }

        public bool TryEnqueueBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandSnapshot snapshot) =>
            _agentRuntime.TryEnqueueBackgroundShellCommandCompletion(snapshot);

        public bool TryEnqueueBackgroundShellCommandOutput(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs) =>
            _agentRuntime.TryEnqueueBackgroundShellCommandOutput(eventArgs);

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) =>
            _agentRuntime.TryAnswerUserQuestion(taskId, requestId, answer);

        private static IReadOnlyList<CopilotCodexAsyncHookResult> DrainAsyncHookResults(
            string conversationId,
            Action<string>? onDiagnostic)
        {
            var results = CopilotCodexLifecycleHookBackgroundScheduler.Shared
                .DrainCompleted(conversationId);
            CopilotCodexAsyncHookResultDelivery.PublishDiagnostics(results, onDiagnostic);
            return results;
        }

        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(
            CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken) =>
            _workspaceRollbackCoordinator.RequestAsync(
                request,
                onEvent,
                cancellationToken);

        private async Task<CopilotTurnResult> RunChatAsync(
            CopilotTurnRequest request,
            IReadOnlyList<string> sessionStartAdditionalContexts,
            IReadOnlyList<string> userPromptSubmitAdditionalContexts,
            IReadOnlyList<string> asyncHookAdditionalContexts,
            CopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            var prompt = request.UserText.Trim();
            var requestContent = request.ExistingRequestContent;
            var attachmentContextCaptured = request.ChatAttachmentContextCaptured;
            var rebuildRequestContext = request.RefreshExternalContext || string.IsNullOrWhiteSpace(requestContent);
            var captureAttachmentContext = rebuildRequestContext
                || request.HostContext.Attachments.Count > 0 && !attachmentContextCaptured;
            var requestContentTask = rebuildRequestContext
                ? _conversationRequestBuilder.BuildUserRequestContentAsync(
                    prompt,
                    request.HostContext.LiveContext,
                    cancellationToken)
                : Task.FromResult(requestContent);
            var imageUnderstandingTask = rebuildRequestContext
                ? _imageUnderstandingService.AnalyzeAsync(
                    request.Profile,
                    prompt,
                    request.HostContext.Attachments,
                    cancellationToken)
                : Task.FromResult(CopilotImageUnderstandingResult.Empty);
            var attachmentContextTask = captureAttachmentContext
                ? CopilotConversationRequestBuilder.BuildAttachmentContextBlockAsync(
                    request.HostContext.Attachments,
                    cancellationToken: cancellationToken)
                : Task.FromResult(string.Empty);

            await Task.WhenAll(requestContentTask, imageUnderstandingTask, attachmentContextTask).ConfigureAwait(false);
            requestContent = await requestContentTask.ConfigureAwait(false);
            var imageUnderstanding = await imageUnderstandingTask.ConfigureAwait(false);
            if (rebuildRequestContext)
                requestContent = InsertImageUnderstandingContext(requestContent, prompt, imageUnderstanding);

            if (captureAttachmentContext)
            {
                var attachmentContext = await attachmentContextTask.ConfigureAwait(false);
                requestContent = InsertRequestContextAfterPrompt(requestContent, prompt, attachmentContext);
                attachmentContextCaptured = request.HostContext.Attachments.Count > 0;
            }

            eventSink.OnRequestPrepared(new CopilotPreparedTurnRequest(requestContent, attachmentContextCaptured));
            eventSink.OnTokenUsageUpdated(imageUnderstanding.Usage);
            var history = await CopilotConversationRequestBuilder.BuildChatHistoryAsync(
                request.HostContext.ConversationHistory,
                requestContent,
                attachments: null,
                limits: request.HistoryLimits,
                includeAttachmentContext: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            var hookDeveloperContext =
                CopilotCodexSessionStartHookExecutor.MergeDeveloperContexts(
                    sessionStartAdditionalContexts,
                    userPromptSubmitAdditionalContexts,
                    asyncHookAdditionalContexts);
            var hookRequest = CreateUserPromptSubmitHookRequest(request);
            var providerUsage = CopilotTokenUsage.Empty;
            var stopHookActive = false;
            var continuationCount = 0;
            var asyncHookContinuationCount = 0;
            var currentHistory = history;
            var streamResult = default(CopilotChatStreamResult);
            while (true)
            {
                var assistantText = new StringBuilder();
                streamResult = await _chatService.StreamReplyAsync(
                    request.Profile,
                    currentHistory,
                    delta =>
                    {
                        if (!string.IsNullOrEmpty(delta.Content))
                            assistantText.Append(delta.Content);
                        eventSink.OnChatDelta(delta);
                    },
                    eventSink.OnProviderRetry,
                    eventSink.OnProviderConnectionRecovery,
                    usage => eventSink.OnTokenUsageUpdated(
                        imageUnderstanding.Usage.Add(providerUsage).Add(usage)),
                    hookDeveloperContext,
                    cancellationToken).ConfigureAwait(false);
                providerUsage = providerUsage.Add(streamResult.Usage);
                streamResult = streamResult with { Usage = providerUsage };
                eventSink.OnTokenUsageUpdated(imageUnderstanding.Usage.Add(providerUsage));
                var completedAsyncHookResults = DrainAsyncHookResults(
                    request.ConversationId,
                    eventSink.OnRuntimeDiagnostic);
                var asyncHookContinuation = CopilotCodexAsyncHookResultDelivery
                    .BuildContinuationMessage(completedAsyncHookResults);
                if (asyncHookContinuation.Length > 0)
                {
                    if (streamResult.IsIncomplete
                        || asyncHookContinuationCount
                            >= CopilotCodexAsyncHookResultDelivery.MaximumConsecutiveContinuations)
                    {
                        CopilotCodexLifecycleHookBackgroundScheduler.Shared.RequeueContexts(
                            request.ConversationId,
                            completedAsyncHookResults);
                        eventSink.OnRuntimeDiagnostic(streamResult.IsIncomplete
                            ? "Completed async hook context was buffered for the next user request because the Chat provider response was incomplete."
                            : $"Async hook continuation limit reached · {CopilotCodexAsyncHookResultDelivery.MaximumConsecutiveContinuations} continuation(s); remaining context was buffered for the next user request.");
                    }
                    else
                    {
                        asyncHookContinuationCount++;
                        eventSink.OnRuntimeDiagnostic(
                            $"Async hook continuation {asyncHookContinuationCount}/{CopilotCodexAsyncHookResultDelivery.MaximumConsecutiveContinuations} · Chat is delivering completed notification-only hook context at the post-sampling boundary.");
                        eventSink.OnChatAnswerReset();
                        currentHistory = CopilotRequestMessageSequence.Normalize(
                            currentHistory
                                .Append(new CopilotRequestMessage("assistant", assistantText.ToString()))
                                .Append(new CopilotRequestMessage("user", asyncHookContinuation)));
                        continue;
                    }
                }
                if (streamResult.IsIncomplete)
                    break;

                var stopOutcome = await _stopHookExecutor.RunAsync(
                    hookRequest,
                    stopHookActive,
                    assistantText.ToString(),
                    eventSink.OnRuntimeDiagnostic,
                    cancellationToken).ConfigureAwait(false);
                if (!stopOutcome.ShouldContinue)
                    break;
                if (continuationCount >= CopilotCodexStopHookExecutor.MaximumConsecutiveContinuations)
                {
                    eventSink.OnRuntimeDiagnostic(
                        $"Stop hook continuation limit reached · {CopilotCodexStopHookExecutor.MaximumConsecutiveContinuations} consecutive continuation(s); the Chat turn is finalizing to avoid an unbounded hook loop.");
                    break;
                }

                continuationCount++;
                stopHookActive = true;
                eventSink.OnRuntimeDiagnostic(
                    $"Stop hook continuation {continuationCount}/{CopilotCodexStopHookExecutor.MaximumConsecutiveContinuations} · Chat is asking the model to revise the completed answer.");
                eventSink.OnChatAnswerReset();
                currentHistory = CopilotRequestMessageSequence.Normalize(
                    currentHistory
                        .Append(new CopilotRequestMessage("assistant", assistantText.ToString()))
                        .Append(new CopilotRequestMessage("user", stopOutcome.ContinuationPrompt)));
            }
            var turnUsage = imageUnderstanding.Usage.Add(providerUsage);
            eventSink.OnTokenUsageUpdated(turnUsage);
            return CopilotTurnResult.FromChat(
                turnUsage,
                requestContent,
                attachmentContextCaptured,
                streamResult);
        }

        private async Task<CopilotTurnResult> RunAgentAsync(
            CopilotTurnRequest request,
            IReadOnlyList<string> sessionStartAdditionalContexts,
            IReadOnlyList<string> userPromptSubmitAdditionalContexts,
            IReadOnlyList<string> asyncHookAdditionalContexts,
            CopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            var effectiveUserText = CopilotPlanHandoff.ResolveEffectiveUserText(
                request.UserText,
                request.ExistingRequestContent);
            var recoveryTaskContext = CopilotAgentRecoveryTaskContext.Resolve(
                effectiveUserText,
                request.Recovery,
                request.SessionCheckpoint);
            var requestPlan = CopilotAgentRequestFactory.Prepare(
                recoveryTaskContext.EffectiveUserText,
                request.Mode,
                request.HostContext);
            var imageUnderstandingTask = _imageUnderstandingService.AnalyzeAsync(
                request.Profile,
                request.UserText,
                request.HostContext.Attachments,
                cancellationToken);
            var contextItemsTask = _contextRegistry.CaptureAsync(
                requestPlan.ContextRequest,
                cancellationToken);

            await Task.WhenAll(imageUnderstandingTask, contextItemsTask).ConfigureAwait(false);
            var imageUnderstanding = await imageUnderstandingTask.ConfigureAwait(false);
            IReadOnlyList<CopilotContextItem> contextItems = await contextItemsTask.ConfigureAwait(false);
            contextItems = MergeCurrentLiveContextSummary(contextItems, request.HostContext.LiveContext);
            contextItems = AppendImageUnderstandingContext(contextItems, imageUnderstanding);
            var agentRequest = CopilotAgentRequestFactory.Create(requestPlan, new CopilotAgentRequestBuildInput
            {
                ConversationId = request.ConversationId,
                TaskId = request.TaskId,
                WorkspacePath = request.HostContext.SolutionDirectoryPath,
                Profile = request.Profile,
                History = CopilotConversationRequestBuilder.BuildVisibleHistory(
                    request.HostContext.ConversationHistory,
                    request.HistoryLimits),
                ContextItems = contextItems,
                SessionStartAdditionalContexts = sessionStartAdditionalContexts,
                UserPromptSubmitAdditionalContexts = userPromptSubmitAdditionalContexts,
                AsyncHookAdditionalContexts = asyncHookAdditionalContexts,
                SessionCheckpoint = request.SessionCheckpoint,
                Recovery = request.Recovery,
                RunControl = request.RunControl,
                AgentDefaults = request.AgentDefaults,
                AccessContext = request.AccessContext,
                ExternalMcpServers = request.ExternalMcpServers,
                TaskIntentText = recoveryTaskContext.TaskIntentText,
                ActiveGoalText = request.ActiveGoalText,
                WorkspaceReviewTarget = request.WorkspaceReviewTarget,
                AgentSkillReference = request.AgentSkillReference,
            });
            var reviewTarget = request.Mode == CopilotAgentMode.Review
                ? request.WorkspaceReviewTarget?.IsStructurallyValid() == true
                    ? request.WorkspaceReviewTarget.CreateSnapshot()
                    : CopilotWorkspaceReviewTargetContext.WorkingTree()
                : null;
            if (reviewTarget != null)
                eventSink.OnReviewEntered(reviewTarget);
            eventSink.OnTokenUsageUpdated(imageUnderstanding.Usage);

            CopilotTurnAnswerLifecycleState? reviewAnswer = reviewTarget != null
                ? CopilotTurnAnswerLifecycleState.Empty
                : null;
            CopilotCodeReviewSnapshot? codeReviewSnapshot = null;
            var workspaceDiff = new CopilotTurnWorkspaceDiffAccumulator(agentRequest.WorkspacePath);
            var turnPlan = new CopilotTurnPlanAccumulator();
            void PublishAgentEvent(CopilotAgentEvent agentEvent)
            {
                if (reviewAnswer.HasValue)
                    reviewAnswer = reviewAnswer.Value.Observe(agentEvent);
                eventSink.OnAgentEvent(agentEvent);
                if (reviewTarget != null
                    && CopilotTurnCodeReviewSnapshotCapture.TryCaptureUpdate(
                        reviewTarget,
                        codeReviewSnapshot,
                        agentEvent,
                        out var updatedCodeReviewSnapshot))
                {
                    codeReviewSnapshot = updatedCodeReviewSnapshot;
                    eventSink.OnCodeReviewSnapshotUpdated(updatedCodeReviewSnapshot);
                }
                if (agentEvent.Type == CopilotAgentEventType.BudgetUpdated
                    && agentEvent.Budget != null)
                {
                    eventSink.OnTokenUsageUpdated(
                        imageUnderstanding.Usage.Add(GetReportedTokenUsage(agentEvent.Budget)));
                }
                if (workspaceDiff.Observe(agentEvent, out var snapshot))
                    eventSink.OnWorkspaceDiffUpdated(snapshot);
                if (turnPlan.Observe(agentEvent, out var planSnapshot))
                    eventSink.OnPlanUpdated(planSnapshot);
            }

            var result = await _agentRuntime.RunAsync(agentRequest, PublishAgentEvent, cancellationToken).ConfigureAwait(false);
            if (turnPlan.Observe(result.TaskLedger, out var finalPlanSnapshot))
                eventSink.OnPlanUpdated(finalPlanSnapshot);
            var turnUsage = imageUnderstanding.Usage.Add(result.Usage);
            eventSink.OnTokenUsageUpdated(turnUsage);
            if (reviewTarget != null)
            {
                var finalReviewAnswer = reviewAnswer!.Value;
                eventSink.OnReviewExited(
                    reviewTarget,
                    finalReviewAnswer.Text,
                    finalReviewAnswer.IsTruncated);
            }
            return CopilotTurnResult.FromAgent(request.Mode, turnUsage, result);
        }

        private static CopilotAgentRequest CreateUserPromptSubmitHookRequest(
            CopilotTurnRequest request)
        {
            var options = request.HostContext.ProjectInstructionDiscoveryOptions;
            var workspacePath = string.IsNullOrWhiteSpace(request.HostContext.SolutionDirectoryPath)
                ? request.HostContext.ProjectConfigWorkingDirectoryPath
                : request.HostContext.SolutionDirectoryPath;
            return new CopilotAgentRequest
            {
                ConversationId = request.ConversationId,
                TaskId = request.TaskId,
                WorkspacePath = workspacePath,
                UserText = request.UserText,
                TaskIntentText = request.UserText,
                Profile = request.Profile,
                Mode = request.Mode,
                SearchRootPaths = request.HostContext.AdditionalReadRootPaths,
                TrustedProjectRootPaths = string.IsNullOrWhiteSpace(
                    request.HostContext.PrimaryTrustedProjectRootPath)
                    ? Array.Empty<string>()
                    : [request.HostContext.PrimaryTrustedProjectRootPath],
                CodexSandboxMode = options.ConfiguredSandboxMode,
                CodexApprovalPolicy = options.ConfiguredApprovalPolicy,
                CodexHooksEnabled = options.ConfiguredHooksEnabled,
                CodexCommandHooks = options.ConfiguredCommandHooks
                    .Select(definition => definition.CreateSnapshot())
                    .ToArray(),
                CodexShellEnvironmentPolicy = options.ConfiguredShellEnvironmentPolicy.CreateSnapshot(),
            };
        }

        internal static CopilotTokenUsage GetReportedTokenUsage(CopilotAgentBudgetSnapshot budget)
        {
            ArgumentNullException.ThrowIfNull(budget);
            return new CopilotTokenUsage(
                Math.Max(0, budget.ReportedInputTokens),
                Math.Max(0, budget.ReportedOutputTokens),
                Math.Max(0, budget.ReportedTotalTokens),
                budget.ReportedCachedInputTokens);
        }

        private static string InsertImageUnderstandingContext(
            string requestContent,
            string prompt,
            CopilotImageUnderstandingResult imageUnderstanding)
        {
            return imageUnderstanding.HasContext
                ? InsertRequestContextAfterPrompt(requestContent, prompt, imageUnderstanding.Context)
                : requestContent;
        }

        private static string InsertRequestContextAfterPrompt(string requestContent, string prompt, string context)
        {
            if (string.IsNullOrWhiteSpace(context))
                return requestContent;

            var normalizedPrompt = (prompt ?? string.Empty).Trim();
            if (normalizedPrompt.Length > 0 && requestContent.StartsWith(normalizedPrompt, StringComparison.Ordinal))
            {
                var remainder = requestContent[normalizedPrompt.Length..].TrimStart();
                return remainder.Length == 0
                    ? normalizedPrompt + Environment.NewLine + Environment.NewLine + context.Trim()
                    : normalizedPrompt + Environment.NewLine + Environment.NewLine + context.Trim()
                        + Environment.NewLine + Environment.NewLine + remainder;
            }

            return string.IsNullOrWhiteSpace(requestContent)
                ? context.Trim()
                : requestContent.TrimEnd() + Environment.NewLine + Environment.NewLine + context.Trim();
        }

        private static IReadOnlyList<CopilotContextItem> AppendImageUnderstandingContext(
            IReadOnlyList<CopilotContextItem> contextItems,
            CopilotImageUnderstandingResult imageUnderstanding)
        {
            if (!imageUnderstanding.HasContext)
                return contextItems;

            return (contextItems ?? Array.Empty<CopilotContextItem>())
                .Append(new CopilotContextItem
                {
                    Id = "attached-image-analysis",
                    Title = "图片像素解析",
                    Summary = imageUnderstanding.IsIncomplete
                        ? "当前模型读取了本轮图片像素，但解析提前结束；仅可把保留文本作为不完整且不可信的视觉观察。"
                        : "已由当前模型读取本轮图片像素；解析文本属于不可信视觉观察。",
                    Content = imageUnderstanding.Context,
                })
                .ToArray();
        }

        private static IReadOnlyList<CopilotContextItem> MergeCurrentLiveContextSummary(
            IReadOnlyList<CopilotContextItem> contextItems,
            CopilotLiveContext? liveContext)
        {
            var liveContextItem = BuildCurrentLiveContextSummaryItem(liveContext);
            if (liveContextItem == null)
                return contextItems;

            var merged = new List<CopilotContextItem>((contextItems?.Count ?? 0) + 1)
            {
                liveContextItem,
            };
            if (contextItems != null)
                merged.AddRange(contextItems);
            return merged;
        }

        private static CopilotContextItem? BuildCurrentLiveContextSummaryItem(CopilotLiveContext? liveContext)
        {
            if (liveContext == null
                || (string.IsNullOrWhiteSpace(liveContext.Title) && string.IsNullOrWhiteSpace(liveContext.Summary)))
            {
                return null;
            }

            return new CopilotContextItem
            {
                Id = string.IsNullOrWhiteSpace(liveContext.SourceId)
                    ? "live-context"
                    : $"{liveContext.SourceId}:summary",
                Title = liveContext.Title,
                Summary = liveContext.Summary,
            };
        }
    }
}
