using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Linq;
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

        public CopilotTurnRuntime(CopilotChatService chatService)
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

        private Task<CopilotTurnResult> RunCoreAsync(
            CopilotTurnRequest request,
            CopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            return request.Mode == CopilotAgentMode.Chat
                ? RunChatAsync(request, eventSink, cancellationToken)
                : RunAgentAsync(request, eventSink, cancellationToken);
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(
            string taskId,
            string message) =>
            _agentRuntime.EnqueueSteeringMessage(taskId, message);

        public bool TryEnqueueBackgroundShellCommandCompletion(
            CopilotBackgroundShellCommandSnapshot snapshot) =>
            _agentRuntime.TryEnqueueBackgroundShellCommandCompletion(snapshot);

        public bool TryEnqueueBackgroundShellCommandOutput(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs) =>
            _agentRuntime.TryEnqueueBackgroundShellCommandOutput(eventArgs);

        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) =>
            _agentRuntime.TryAnswerUserQuestion(taskId, requestId, answer);

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
            var streamResult = await _chatService.StreamReplyAsync(
                request.Profile,
                history,
                eventSink.OnChatDelta,
                eventSink.OnProviderRetry,
                eventSink.OnProviderConnectionRecovery,
                usage => eventSink.OnTokenUsageUpdated(imageUnderstanding.Usage.Add(usage)),
                cancellationToken).ConfigureAwait(false);
            var turnUsage = imageUnderstanding.Usage.Add(streamResult.Usage);
            eventSink.OnTokenUsageUpdated(turnUsage);
            return CopilotTurnResult.FromChat(
                turnUsage,
                requestContent,
                attachmentContextCaptured,
                streamResult);
        }

        private async Task<CopilotTurnResult> RunAgentAsync(
            CopilotTurnRequest request,
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
            var workspaceDiff = new CopilotTurnWorkspaceDiffAccumulator(agentRequest.WorkspacePath);
            var turnPlan = new CopilotTurnPlanAccumulator();
            void PublishAgentEvent(CopilotAgentEvent agentEvent)
            {
                if (reviewAnswer.HasValue)
                    reviewAnswer = reviewAnswer.Value.Observe(agentEvent);
                eventSink.OnAgentEvent(agentEvent);
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
