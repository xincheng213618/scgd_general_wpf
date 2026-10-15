using System;

namespace ColorVision.Copilot
{
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
