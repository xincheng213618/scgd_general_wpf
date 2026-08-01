using System;

namespace ColorVision.Copilot
{
    internal readonly record struct CopilotComposerEditorSnapshot(string Text, int CaretIndex)
    {
        internal static CopilotComposerEditorSnapshot Capture(string? text, int caretIndex)
        {
            var normalizedText = CopilotComposerTextLimits.Bound(text);
            return new CopilotComposerEditorSnapshot(
                normalizedText,
                Math.Clamp(caretIndex, 0, normalizedText.Length));
        }
    }

    internal static class CopilotComposerTextLimits
    {
        internal static string Bound(string? value)
        {
            var normalized = value ?? string.Empty;
            if (normalized.Length <= CopilotConversationHistoryWindow.MaximumContentCharacterLimit)
                return normalized;

            var retainedLength = CopilotConversationHistoryWindow.MaximumContentCharacterLimit;
            if (char.IsHighSurrogate(normalized[retainedLength - 1])
                && char.IsLowSurrogate(normalized[retainedLength]))
            {
                retainedLength--;
            }
            return normalized[..retainedLength];
        }
    }
}
