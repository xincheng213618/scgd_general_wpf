using System;
using System.Collections.Generic;

namespace ColorVision.Copilot
{
    public sealed class CopilotCapabilityResult
    {
        public bool Success { get; init; }

        public string Summary { get; init; } = string.Empty;

        public string PartialResultMessage { get; init; } = string.Empty;

        public string Content { get; init; } = string.Empty;

        public string ErrorMessage { get; init; } = string.Empty;

        public CopilotToolFailureKind FailureKind { get; init; }

        public IReadOnlyList<string> SuggestedReadableLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> AttemptedLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<string> SuccessfullyReadLocalFilePaths { get; init; } = Array.Empty<string>();

        public IReadOnlyList<CopilotLocalFileReadScope> LocalFileReadScopes { get; init; } = Array.Empty<CopilotLocalFileReadScope>();

        internal IReadOnlyList<string> LocalObservationScopePaths { get; init; } = Array.Empty<string>();

        public CopilotToolResult ToToolResult(string toolName)
        {
            return new CopilotToolResult
            {
                ToolName = toolName,
                Success = Success,
                Summary = Summary,
                PartialResultMessage = PartialResultMessage,
                Content = Content,
                ErrorMessage = ErrorMessage,
                FailureKind = !Success && FailureKind == CopilotToolFailureKind.None
                    ? CopilotToolFailureKind.Unspecified
                    : FailureKind,
                SuggestedReadableLocalFilePaths = SuggestedReadableLocalFilePaths,
                AttemptedLocalFilePaths = AttemptedLocalFilePaths,
                SuccessfullyReadLocalFilePaths = SuccessfullyReadLocalFilePaths,
                LocalFileReadScopes = LocalFileReadScopes,
                LocalObservationScopePaths = LocalObservationScopePaths,
            };
        }
    }
}
