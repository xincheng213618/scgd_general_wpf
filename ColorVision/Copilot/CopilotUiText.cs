namespace ColorVision.Copilot
{
    public static class CopilotUiText
    {
        public const string UserHeader = "You";
        public const string ExecutionInProgressHeader = "Running";
        public const string ExecutionHeader = "Execution";
        public const string ReasoningInProgressHeader = "Reasoning";
        public const string ReasoningHeader = "Reasoning Details";
        public const string NewConversationTitle = "New Chat";
        public const string EmptyConversationPreview = "Click + to start a new chat, or type a question";
        public const string PinnedLabel = "Pinned";
        public const string PinMenuText = "Pin";
        public const string UnpinMenuText = "Unpin";
        public const string SingleAttachmentMounted = "1 attachment mounted";
        public const string FileBadge = "File";
        public const string ImageBadge = "Image";
        public const string WebPageBadge = "Web";
        public const string ContextBadge = "Context";
        public const string ImagePreviewUnavailable = "Image preview unavailable";
        public const string CopilotPanelTitle = "Chat Assistant";

        public static string FormatAttachmentMountedCount(int count)
        {
            return count == 1
                ? SingleAttachmentMounted
                : $"{count} attachments mounted";
        }
    }
}
