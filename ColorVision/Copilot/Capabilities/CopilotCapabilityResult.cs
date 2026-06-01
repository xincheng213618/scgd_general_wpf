using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed class CopilotCapabilityResult
    {
        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public CopilotToolResult ToToolResult(string toolName)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = Success,
                Summary = Summary,
                Content = Content,
                ErrorMessage = ErrorMessage,
                SuggestedReadableLocalFilePaths = SuggestedReadableLocalFilePaths,
            };
        }
    }
}