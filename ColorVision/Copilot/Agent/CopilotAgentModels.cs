using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public enum CopilotAgentMode
    {
        Chat,
        Auto,
        Explain,
        Web,
        Code,
        Diagnose,
    }

    public sealed class CopilotAgentModeOption
    {
        public CopilotAgentMode Mode { get; init; }

        public string Label { get; init; } = string.Empty;

        public string Description { get; init; } = string.Empty;

        public static IReadOnlyList<CopilotAgentModeOption> CreateDefaultOptions()
        {
            return new[]
            {
                new CopilotAgentModeOption
                {
                    Mode = CopilotAgentMode.Chat,
                    Label = "Ask",
                    Description = "普通对话，不主动调用工具。",
                },
                new CopilotAgentModeOption
                {
                    Mode = CopilotAgentMode.Auto,
                    Label = "Agent",
                    Description = "自动分析任务并调用只读工具。",
                },
            };
        }
    }

    public sealed class CopilotAgentToolInput
    {
        public static CopilotAgentToolInput Empty { get; } = new();

        public string Query { get; init; } = string.Empty;

        public string Path { get; init; } = string.Empty;

        public int? StartLine { get; init; }

        public int? EndLine { get; init; }
    }

    public sealed class CopilotAgentRequest
    {
        public string UserText { get; init; } = string.Empty;

        public CopilotProfileConfig Profile { get; init; } = null!;

        public IReadOnlyList<CopilotRequestMessage> History { get; init; } = Array.Empty<CopilotRequestMessage>();

        public IReadOnlyList<CopilotAttachmentItem> Attachments { get; init; } = Array.Empty<CopilotAttachmentItem>();

        public IReadOnlyList<string> SearchRootPaths { get; init; } = Array.Empty<string>();

        public string ActiveDocumentPath { get; init; } = string.Empty;

        public IReadOnlyList<string> ReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> ReadableLocalDirectoryPaths { get; init; } = Array.Empty<string>();

        public CopilotAgentToolInput SelectedToolInput { get; init; } = CopilotAgentToolInput.Empty;

        public string SelectedLocalFilePath => SelectedToolInput?.Path ?? string.Empty;

        public int? SelectedLocalFileStartLine => SelectedToolInput?.StartLine;

        public int? SelectedLocalFileEndLine => SelectedToolInput?.EndLine;

        public string SelectedToolQuery => SelectedToolInput?.Query ?? string.Empty;

        public CopilotAgentMode Mode { get; init; } = CopilotAgentMode.Auto;
    }

    public sealed class CopilotToolResult
    {
        public string ToolName { get; init; } = string.Empty;

        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();
    }

    public enum CopilotAgentEventType
    {
        Status,
        ToolResult,
        ReasoningDelta,
        AnswerDelta,
        Error,
        Completed,
    }

    public sealed class CopilotAgentEvent
    {
        public CopilotAgentEventType Type { get; init; }

        public string Text { get; init; } = string.Empty;

        public CopilotToolResult? ToolResult { get; init; }

        public static CopilotAgentEvent Status(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Status,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent FromToolResult(CopilotToolResult result)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ToolResult,
                Text = result?.Summary ?? string.Empty,
                ToolResult = result,
            };
        }

        public static CopilotAgentEvent ReasoningDelta(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.ReasoningDelta,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent AnswerDelta(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.AnswerDelta,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent Error(string text)
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Error,
                Text = text ?? string.Empty,
            };
        }

        public static CopilotAgentEvent Completed()
        {
            return new CopilotAgentEvent
            {
                Type = CopilotAgentEventType.Completed,
            };
        }
    }

    public sealed class CopilotAgentPreparedPrompt
    {
        public CopilotAgentPreparedPrompt(IReadOnlyList<CopilotRequestMessage> messages, string preparedUserMessageContent)
        {
            Messages = messages ?? Array.Empty<CopilotRequestMessage>();
            PreparedUserMessageContent = preparedUserMessageContent ?? string.Empty;
        }

        public IReadOnlyList<CopilotRequestMessage> Messages { get; }

        public string PreparedUserMessageContent { get; }
    }

    public sealed class CopilotAgentRunResult
    {
        public string PreparedUserMessageContent { get; init; } = string.Empty;

        public CopilotTokenUsage Usage { get; init; } = CopilotTokenUsage.Empty;
    }
}