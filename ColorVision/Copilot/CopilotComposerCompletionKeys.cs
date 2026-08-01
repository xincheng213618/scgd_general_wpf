namespace ColorVision.Copilot
{
    internal static class CopilotComposerCompletionKeys
    {
        public static bool CanAcceptRightArrow(
            int caretIndex,
            int selectionLength,
            int textLength)
        {
            return caretIndex >= 0
                && selectionLength == 0
                && textLength >= 0
                && caretIndex == textLength;
        }
    }
}
