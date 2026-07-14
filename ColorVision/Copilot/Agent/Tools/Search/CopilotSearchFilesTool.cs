using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchFilesTool : ICopilotTool
    {
        public string Name => "SearchFiles";

        public string Description => "Find candidate files in the current solution by file name or path fragment.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Query("File name or path fragment to locate.", required: true);

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.SearchRootPaths?.Count > 0
                && CopilotToolIntentPolicy.NeedsLocalEvidence(request);
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = CopilotSearchFilesCapability.Search(
                request.SearchRootPaths,
                toolInput?.Query,
                request.UserText,
                allowPlainSearchTerms: false,
                cancellationToken);
            return Task.FromResult(result.ToCapabilityResult().ToToolResult(Name));
        }
    }
}
