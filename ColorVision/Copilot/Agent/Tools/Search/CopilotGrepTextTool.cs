using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGrepTextTool : ICopilotAgentDrivenTool
    {
        public string Name => "GrepText";

        public string Description => "Search text files in the current solution for keyword or identifier matches.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("Keyword, identifier, or exact text pattern to find.", required: true);

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request?.SearchRootPaths?.Count > 0 && request.Mode != CopilotAgentMode.Chat;
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = CopilotGrepTextCapability.Search(
                request.SearchRootPaths,
                toolInput?.Query,
                request.UserText,
                cancellationToken);
            return Task.FromResult(result.ToCapabilityResult().ToToolResult(Name));
        }
    }
}
