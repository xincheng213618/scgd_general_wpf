using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotAgentService
    {
        private readonly CopilotChatService _chatService;
        private readonly CopilotToolRegistry _toolRegistry;
        private readonly CopilotAgentContextBuilder _contextBuilder;

        public CopilotAgentService(
            CopilotChatService chatService,
            CopilotToolRegistry toolRegistry,
            CopilotAgentContextBuilder contextBuilder)
        {
            _chatService = chatService ?? throw new ArgumentNullException(nameof(chatService));
            _toolRegistry = toolRegistry ?? throw new ArgumentNullException(nameof(toolRegistry));
            _contextBuilder = contextBuilder ?? throw new ArgumentNullException(nameof(contextBuilder));
        }

        public async Task<CopilotAgentRunResult> RunAsync(
            CopilotAgentRequest request,
            Action<CopilotAgentEvent> onEvent,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            ArgumentNullException.ThrowIfNull(onEvent);

            onEvent(CopilotAgentEvent.Status("正在分析任务..."));

            var tools = _toolRegistry.FindTools(request);
            var toolResults = new List<CopilotToolResult>(tools.Count);

            if (tools.Count == 0)
                onEvent(CopilotAgentEvent.Status("当前任务无需额外工具，直接生成回答。"));

            foreach (var tool in tools)
            {
                cancellationToken.ThrowIfCancellationRequested();
                onEvent(CopilotAgentEvent.Status($"正在执行工具：{tool.Name}"));

                CopilotToolResult result;
                try
                {
                    result = await tool.ExecuteAsync(request, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    result = new CopilotToolResult
                    {
                        ToolName = tool.Name,
                        Success = false,
                        Summary = $"{tool.Name} 执行失败。",
                        ErrorMessage = ex.Message,
                    };
                }

                toolResults.Add(result);
                onEvent(CopilotAgentEvent.FromToolResult(result));
            }

            var preparedPrompt = _contextBuilder.BuildMessages(request, toolResults);
            onEvent(CopilotAgentEvent.Status("正在生成回答..."));

            await _chatService.StreamReplyAsync(
                request.Profile,
                preparedPrompt.Messages,
                delta =>
                {
                    if (delta.HasReasoning)
                        onEvent(CopilotAgentEvent.ReasoningDelta(delta.ReasoningContent));

                    if (delta.HasContent)
                        onEvent(CopilotAgentEvent.AnswerDelta(delta.Content));
                },
                cancellationToken);

            onEvent(CopilotAgentEvent.Completed());
            return new CopilotAgentRunResult
            {
                PreparedUserMessageContent = preparedPrompt.PreparedUserMessageContent,
            };
        }
    }
}