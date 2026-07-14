using ColorVision.Common.MVVM;
using System;

namespace ColorVision.Copilot
{
    public enum CopilotResponseTimelineEventKind
    {
        Markdown,
        ToolCall,
    }

    public sealed class CopilotResponseTimelineEvent
    {
        public const int CurrentSchemaVersion = 1;

        public int SchemaVersion { get; set; } = CurrentSchemaVersion;

        public CopilotResponseTimelineEventKind Kind { get; set; }

        public int ContentStart { get; set; }

        public int ContentLength { get; set; }

        public string CallId { get; set; } = string.Empty;

        public static CopilotResponseTimelineEvent Markdown(int contentStart, int contentLength)
        {
            return new CopilotResponseTimelineEvent
            {
                Kind = CopilotResponseTimelineEventKind.Markdown,
                ContentStart = contentStart,
                ContentLength = contentLength,
            };
        }

        public static CopilotResponseTimelineEvent ToolCall(string callId)
        {
            return new CopilotResponseTimelineEvent
            {
                Kind = CopilotResponseTimelineEventKind.ToolCall,
                CallId = NormalizeCallId(callId),
            };
        }

        internal bool Normalize(out bool changed)
        {
            changed = SchemaVersion != CurrentSchemaVersion;
            SchemaVersion = CurrentSchemaVersion;

            if (!Enum.IsDefined(Kind))
                return false;

            if (Kind == CopilotResponseTimelineEventKind.Markdown)
            {
                changed |= !string.IsNullOrEmpty(CallId);
                CallId = string.Empty;
                return ContentStart >= 0 && ContentLength > 0;
            }

            var normalizedCallId = NormalizeCallId(CallId);
            changed |= ContentStart != 0 || ContentLength != 0 || !string.Equals(CallId, normalizedCallId, StringComparison.Ordinal);
            ContentStart = 0;
            ContentLength = 0;
            CallId = normalizedCallId;
            return !string.IsNullOrWhiteSpace(CallId);
        }

        internal static string NormalizeCallId(string? callId)
        {
            var value = (callId ?? string.Empty).Replace('\r', ' ').Replace('\n', ' ').Trim();
            return value.Length <= 120 ? value : value[..120];
        }
    }

    public sealed class CopilotResponseTimelineItem : ViewModelBase
    {
        private CopilotResponseTimelineItem(string markdown, CopilotAgentTraceGroup? toolGroup)
        {
            _markdown = markdown;
            _toolGroup = toolGroup;
        }

        public string Markdown
        {
            get => _markdown;
            private set => SetProperty(ref _markdown, value);
        }
        private string _markdown;

        public CopilotAgentTraceGroup? ToolGroup
        {
            get => _toolGroup;
            private set
            {
                SetProperty(ref _toolGroup, value);
                OnPropertyChanged(nameof(IsMarkdown));
                OnPropertyChanged(nameof(IsToolGroup));
            }
        }
        private CopilotAgentTraceGroup? _toolGroup;

        public bool IsMarkdown => ToolGroup == null;

        public bool IsToolGroup => ToolGroup != null;

        internal static CopilotResponseTimelineItem FromMarkdown(string markdown)
        {
            return new CopilotResponseTimelineItem(markdown, null);
        }

        internal static CopilotResponseTimelineItem FromToolGroup(CopilotAgentTraceGroup toolGroup)
        {
            ArgumentNullException.ThrowIfNull(toolGroup);
            return new CopilotResponseTimelineItem(string.Empty, toolGroup);
        }

        internal void UpdateFrom(CopilotResponseTimelineItem source)
        {
            ArgumentNullException.ThrowIfNull(source);
            Markdown = source.Markdown;
            ToolGroup = source.ToolGroup;
        }
    }
}
