using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGrepTextTool : ICopilotTool
    {
        public string Name => "GrepText";

        public string Description => "Search text files in the current solution for keyword or identifier matches.";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat || request.SearchRootPaths.Count == 0)
                return false;

            return true;
        }

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
