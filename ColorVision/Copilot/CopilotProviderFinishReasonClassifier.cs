using System;

namespace ColorVision.Copilot
{
    internal static class CopilotProviderFinishReasonClassifier
    {
        public static CopilotChatFinishKind Classify(string? finishReason)
        {
            if (string.IsNullOrWhiteSpace(finishReason))
                return CopilotChatFinishKind.Unspecified;

            var comparable = finishReason
                .Trim()
                .Replace('-', '_')
                .Replace(' ', '_')
                .ToLowerInvariant();
            if (comparable is "stop" or "end_turn" or "stop_sequence" or "complete" or "completed" or "success")
                return CopilotChatFinishKind.Complete;
            if (comparable is "length" or "max_tokens" or "max_output_tokens" or "model_length"
                || comparable.Contains("length_limit", StringComparison.Ordinal)
                || comparable.Contains("token_limit", StringComparison.Ordinal))
            {
                return CopilotChatFinishKind.LengthLimit;
            }
            if (comparable is "content_filter" or "safety" or "blocked" or "refusal"
                || comparable.Contains("filter", StringComparison.Ordinal))
            {
                return CopilotChatFinishKind.ContentFiltered;
            }
            if (comparable is "tool_calls" or "tool_use" or "function_call" or "pause_turn"
                || comparable.Contains("tool_call", StringComparison.Ordinal))
            {
                return CopilotChatFinishKind.ToolRequested;
            }
            return CopilotChatFinishKind.Other;
        }
    }
}
