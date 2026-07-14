using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadLocalFileTool : ICopilotAgentDrivenTool
    {
        public string Name => "ReadLocalFile";

        public string Description => "Read local text files allowed for the current round, with optional path and line-range focus.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.FileRead();

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && (request.ReadableLocalFilePaths.Count > 0 || request.SearchRootPaths.Count > 0);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var allowedFiles = new List<string>(request.ReadableLocalFilePaths);
            if (!string.IsNullOrWhiteSpace(toolInput?.Path)
                && CopilotWorkspaceSearchSupport.IsPathWithinRoots(toolInput.Path, request.SearchRootPaths))
            {
                allowedFiles.Add(toolInput.Path);
            }

            var result = await CopilotReadLocalFileCapability.ReadAsync(
                allowedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                toolInput?.Path,
                request.PreferBatchReadLocalFiles,
                toolInput?.StartLine,
                toolInput?.EndLine,
                cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
