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

        public CopilotTurnRuntime(CopilotChatService chatService)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _conversationRequestBuilder = new CopilotConversationRequestBuilder();
            _imageUnderstandingService = new CopilotImageUnderstandingService(_chatService);
            _contextRegistry = CopilotContextRegistry.CreateDefault();
            _agentRuntime = new CopilotMicrosoftAgentFrameworkRuntime(
                CopilotToolRegistry.CreateDefault(),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor());
        }

        public Task<CopilotTurnResult> RunAsync(
            CopilotTurnRequest request,
            ICopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(eventSink);
            return request.Mode == CopilotAgentMode.Chat
                ? RunChatAsync(request, eventSink, cancellationToken)
                : RunAgentAsync(request, eventSink, cancellationToken);
        }

        public bool TryEnqueueSteeringMessage(string message) => _agentRuntime.TryEnqueueSteeringMessage(message);

        private async Task<CopilotTurnResult> RunChatAsync(
            CopilotTurnRequest request,
            ICopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            var prompt = request.UserText.Trim();
            var requestContent = request.ExistingRequestContent;
            var attachmentContextCaptured = request.ChatAttachmentContextCaptured;
            var imageUnderstanding = CopilotImageUnderstandingResult.Empty;
            var rebuildRequestContext = request.RefreshExternalContext || string.IsNullOrWhiteSpace(requestContent);
            if (rebuildRequestContext)
            {
                requestContent = await _conversationRequestBuilder.BuildUserRequestContentAsync(
                    prompt,
                    request.HostContext.LiveContext,
                    cancellationToken).ConfigureAwait(false);
                imageUnderstanding = await _imageUnderstandingService.AnalyzeAsync(
                    request.Profile,
                    prompt,
                    request.HostContext.Attachments,
                    cancellationToken).ConfigureAwait(false);
                requestContent = InsertImageUnderstandingContext(requestContent, prompt, imageUnderstanding);
            }

            if (rebuildRequestContext || (request.HostContext.Attachments.Count > 0 && !attachmentContextCaptured))
            {
                var attachmentContext = await CopilotConversationRequestBuilder.BuildAttachmentContextBlockAsync(
                    request.HostContext.Attachments,
                    cancellationToken: cancellationToken).ConfigureAwait(false);
                requestContent = InsertRequestContextAfterPrompt(requestContent, prompt, attachmentContext);
                attachmentContextCaptured = request.HostContext.Attachments.Count > 0;
            }

            eventSink.OnRequestPrepared(new CopilotPreparedTurnRequest(requestContent, attachmentContextCaptured));
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
                cancellationToken).ConfigureAwait(false);
            return CopilotTurnResult.FromChat(
                imageUnderstanding.Usage.Add(streamResult.Usage),
                requestContent,
                attachmentContextCaptured,
                streamResult);
        }

        private async Task<CopilotTurnResult> RunAgentAsync(
            CopilotTurnRequest request,
            ICopilotTurnEventSink eventSink,
            CancellationToken cancellationToken)
        {
            var imageUnderstanding = await _imageUnderstandingService.AnalyzeAsync(
                request.Profile,
                request.UserText,
                request.HostContext.Attachments,
                cancellationToken).ConfigureAwait(false);
            var requestPlan = CopilotAgentRequestFactory.Prepare(request.UserText, request.Mode, request.HostContext);
            IReadOnlyList<CopilotContextItem> contextItems = await _contextRegistry.CaptureAsync(
                requestPlan.ContextRequest,
                cancellationToken).ConfigureAwait(false);
            contextItems = MergeCurrentLiveContextSummary(contextItems, request.HostContext.LiveContext);
            contextItems = AppendImageUnderstandingContext(contextItems, imageUnderstanding);
            var agentRequest = CopilotAgentRequestFactory.Create(requestPlan, new CopilotAgentRequestBuildInput
            {
                Profile = request.Profile,
                History = CopilotConversationRequestBuilder.BuildVisibleHistory(
                    request.HostContext.ConversationHistory,
                    request.HistoryLimits),
                ContextItems = contextItems,
                SessionCheckpoint = request.SessionCheckpoint,
                Recovery = request.Recovery,
                RunControl = request.RunControl,
                AgentDefaults = request.AgentDefaults,
                ExternalMcpServers = request.ExternalMcpServers,
            });
            var result = await _agentRuntime.RunAsync(agentRequest, eventSink.OnAgentEvent, cancellationToken).ConfigureAwait(false);
            return CopilotTurnResult.FromAgent(request.Mode, imageUnderstanding.Usage.Add(result.Usage), result);
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
