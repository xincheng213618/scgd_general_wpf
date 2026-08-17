using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchFilesTool : ICopilotAgentDrivenTool
    {
        public string Name => CopilotSharedCapabilityCatalog.SearchFiles.AgentToolName;

        public string Description => "Find one stable bounded page of candidate files by file name or path fragment, optionally limited to one workspace directory, with a continuation cursor when more matches remain. A completed empty search is successful evidence, not a tool failure; inspect scan_complete before concluding that a file is absent.";

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.SearchFiles.SharedInputSchema!;

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request?.SearchRootPaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat
                && CopilotToolIntentPolicy.NeedsWorkspaceDiscovery(request);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!CopilotWorkspaceSearchSupport.TryResolveDirectoryScope(
                toolInput?.Path, request.SearchRootPaths, out var searchRoots, out var scopeError))
            {
                return Task.FromResult(new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "The requested file-search directory could not be resolved within the current workspace.",
                    ErrorMessage = scopeError,
                }.ToToolResult(Name));
            }

            var result = CopilotSearchFilesCapability.SearchWithinScope(
                searchRoots,
                request.SearchRootPaths,
                toolInput?.Query,
                request.UserText,
                allowPlainSearchTerms: false,
                toolInput?.Cursor,
                cancellationToken);
            return Task.FromResult(result.ToCapabilityResult().ToToolResult(Name));
        }
    }
}
