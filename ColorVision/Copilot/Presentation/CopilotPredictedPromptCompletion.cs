using System;

namespace ColorVision.Copilot
{
    internal static class CopilotPredictedPromptCompletion
    {
        public static bool TryResolve(
            string? suggestion,
            string? input,
            out string remainingText)
        {
            remainingText = string.Empty;
            var normalizedSuggestion = (suggestion ?? string.Empty).Trim();
            var currentInput = input ?? string.Empty;
            if (normalizedSuggestion.Length == 0
                || currentInput.Length >= normalizedSuggestion.Length
                || !normalizedSuggestion.StartsWith(currentInput, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            remainingText = normalizedSuggestion[currentInput.Length..];
            return remainingText.Length > 0;
        }

        public static bool ShouldClear(
            string? suggestion,
            string? input,
            bool requestPending)
        {
            var currentInput = input ?? string.Empty;
            return currentInput.Length > 0
                && (requestPending
                    || string.Equals(
                        currentInput,
                        suggestion,
                        StringComparison.OrdinalIgnoreCase));
        }
    }
}
