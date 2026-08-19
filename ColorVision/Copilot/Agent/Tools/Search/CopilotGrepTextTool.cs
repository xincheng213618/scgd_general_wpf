using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotGrepTextTool : ICopilotAgentDrivenTool
    {
        public string Name => CopilotSharedCapabilityCatalog.GrepText.AgentToolName;

        public string Description => CopilotSharedCapabilityCatalog.GrepText.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.GrepText.AgentCapability;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.GrepText.AgentInputSchema;

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
