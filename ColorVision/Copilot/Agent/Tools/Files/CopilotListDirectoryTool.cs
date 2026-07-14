using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotListDirectoryTool : ICopilotAgentDrivenTool
    {
        public string Name => "ListDirectory";

        public string Description => "List the contents of local directories allowed for the current round, including files and subdirectories.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.Path("Allowed local directory path to list.");

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && (request.ReadableLocalDirectoryPaths.Count > 0 || request.SearchRootPaths.Count > 0);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var allowedDirectories = new List<string>(request.ReadableLocalDirectoryPaths);
            if (!string.IsNullOrWhiteSpace(toolInput?.Path)
                && CopilotWorkspaceSearchSupport.IsPathWithinRoots(toolInput.Path, request.SearchRootPaths))
            {
                allowedDirectories.Add(toolInput.Path);
            }
            else if (string.IsNullOrWhiteSpace(toolInput?.Path))
            {
                allowedDirectories.AddRange(request.SearchRootPaths);
            }

            var result = CopilotListDirectoryCapability.List(
                allowedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                toolInput?.Path,
                cancellationToken);
            return Task.FromResult(result.ToToolResult(Name));
        }
    }
}
