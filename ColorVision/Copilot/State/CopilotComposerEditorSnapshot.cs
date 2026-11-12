using System;

namespace ColorVision.Copilot
{
    internal sealed record CopilotComposerEditorCapture(
        CopilotComposerCaptureSnapshot Composer,
        string EditingConversationId,
        string EditingUserMessageId,
        object? MessageEditLifetime)
    {
        public string ConversationId => Composer.ConversationId;

        public string Text => Composer.Text;
    }

    internal readonly record struct CopilotComposerEditorSnapshot(string Text, int CaretIndex)
    {
        internal static CopilotComposerEditorSnapshot Capture(string? text, int caretIndex)
        {
            var normalizedText = text ?? string.Empty;
            return new CopilotComposerEditorSnapshot(
                normalizedText,
                Math.Clamp(caretIndex, 0, normalizedText.Length));
        }
    }
}
