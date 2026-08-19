using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchFilesTool : ICopilotAgentDrivenTool
    {
        public string Name => CopilotSharedCapabilityCatalog.SearchFiles.AgentToolName;

        public string Description => CopilotSharedCapabilityCatalog.SearchFiles.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.SearchFiles.AgentCapability;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.SearchFiles.AgentInputSchema;

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
