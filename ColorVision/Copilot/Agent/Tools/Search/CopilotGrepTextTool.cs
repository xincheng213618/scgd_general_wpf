using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGrepTextTool : ICopilotAgentDrivenTool
    {
        public string Name => CopilotSharedCapabilityCatalog.GrepText.AgentToolName;

        public string Description => "Search one stable bounded page of workspace text matches, optionally limited to one workspace file or directory, with an opaque continuation cursor when more matches remain. A completed empty search is successful evidence, not a tool failure; inspect scan_complete before concluding that text is absent.";

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.GrepText.SharedInputSchema!;

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request?.SearchRootPaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat
                && CopilotToolIntentPolicy.NeedsLocalEvidence(request);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            if (!CopilotWorkspaceSearchSupport.TryResolveFileOrDirectoryScope(
                toolInput?.Path, request.SearchRootPaths, out var searchRoots, out var scopeError))
            {
                return Task.FromResult(new CopilotCapabilityResult
                {
                    Success = false,
                    Summary = "The requested text-search scope could not be resolved within the current workspace.",
                    ErrorMessage = scopeError,
                }.ToToolResult(Name));
            }

            var result = CopilotGrepTextCapability.SearchWithinScope(
                searchRoots,
                request.SearchRootPaths,
                toolInput?.Query,
                request.UserText,
                toolInput?.Cursor,
                cancellationToken);
            return Task.FromResult(result.ToCapabilityResult().ToToolResult(Name));
        }
    }
}
