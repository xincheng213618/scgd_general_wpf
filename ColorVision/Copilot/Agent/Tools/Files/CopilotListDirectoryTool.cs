using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotListDirectoryTool : ICopilotTool
    {
        public string Name => "ListDirectory";

        public string Description => "List the contents of local directories allowed for the current round, including files and subdirectories.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Path("Allowed local directory path to list.");

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.ReadableLocalDirectoryPaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = CopilotListDirectoryCapability.List(
                request.ReadableLocalDirectoryPaths,
                toolInput?.Path,
                cancellationToken);
            return Task.FromResult(result.ToToolResult(Name));
        }
    }
}
