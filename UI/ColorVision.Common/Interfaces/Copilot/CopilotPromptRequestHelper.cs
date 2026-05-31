using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.UI
{
    public readonly record struct CopilotPromptDispatchResult(bool IsAvailable, bool WasSent, string StatusMessage);

    public sealed class CopilotPromptRequestOptions
    {
        public string Prompt { get; init; } = string.Empty;

        public CopilotPromptMode Mode { get; init; } = CopilotPromptMode.Agent;

        public bool StartNewConversation { get; init; } = true;

        public bool SendNow { get; init; } = true;

        public bool AttachContextSnapshot { get; init; } = true;

        public string ContextAttachmentTitle { get; init; } = string.Empty;

        public string ContextAttachmentSourceId { get; init; } = string.Empty;

        public IReadOnlyList<CopilotContextItem> ContextItems { get; init; } = Array.Empty<CopilotContextItem>();
    }

    public static class CopilotPromptRequestHelper
    {
        public const string EmptyPromptMessage = "没有可发送给 Copilot 的内容。";
        public const string UnavailableMessage = "主界面的 Copilot 面板尚未就绪或尚未配置。";
        public const string SendFailedMessage = "无法把当前上下文发送到 Copilot。";
        public const string SentMessage = "已发送到 Copilot。";

        public static CopilotPromptRequest CreateRequest(CopilotPromptRequestOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            return new CopilotPromptRequest
            {
                Prompt = options.Prompt ?? string.Empty,
                Mode = options.Mode,
                StartNewConversation = options.StartNewConversation,
                SendNow = options.SendNow,
                AttachContextSnapshot = options.AttachContextSnapshot,
                ContextAttachmentTitle = options.ContextAttachmentTitle ?? string.Empty,
                ContextAttachmentSourceId = options.ContextAttachmentSourceId ?? string.Empty,
                ContextItems = options.ContextItems?.Where(item => item != null).ToArray()
                    ?? Array.Empty<CopilotContextItem>(),
            };
        }

        public static CopilotPromptDispatchResult Dispatch(CopilotPromptRequestOptions options, ICopilotService? service = null)
        {
            return Dispatch(CreateRequest(options), service);
        }

        public static CopilotPromptDispatchResult Dispatch(CopilotPromptRequest request, ICopilotService? service = null)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (string.IsNullOrWhiteSpace(request.Prompt))
                return new CopilotPromptDispatchResult(false, false, EmptyPromptMessage);

            var copilotService = service ?? CopilotServiceRegistry.Current;
            if (copilotService == null || !copilotService.IsAvailable)
                return new CopilotPromptDispatchResult(false, false, UnavailableMessage);

            var sent = copilotService.Ask(request);
            return new CopilotPromptDispatchResult(true, sent, sent ? SentMessage : SendFailedMessage);
        }
    }
}