using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotListDirectoryTool : ICopilotAgentDrivenTool
    {
        public string Name => CopilotSharedCapabilityCatalog.ListAllowedDirectory.AgentToolName;

        public string Description => CopilotSharedCapabilityCatalog.ListAllowedDirectory.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.ListAllowedDirectory.AgentCapability;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.ListAllowedDirectory.AgentInputSchema;

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && (request.ReadableLocalDirectoryPaths.Count > 0 || request.SearchRootPaths.Count > 0)
                && CopilotToolIntentPolicy.NeedsWorkspaceDiscovery(request);
        }

        public bool CanHandle(CopilotAgentRequest request) => IsAvailable(request);

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var allowedDirectories = new List<string>(request.ReadableLocalDirectoryPaths);
            var selectedPath = toolInput?.Path;
            if (!string.IsNullOrWhiteSpace(selectedPath)
                && !CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(selectedPath, allowedDirectories))
            {
                if (!CopilotWorkspaceSearchSupport.TryResolveExistingDirectoryWithinRoots(
                    selectedPath,
                    request.SearchRootPaths,
                    out var resolvedPath,
                    out var pathError))
                {
                    return Task.FromResult(new CopilotCapabilityResult
                    {
                        Success = false,
                        Summary = "The requested local directory could not be resolved within the current workspace.",
                        ErrorMessage = pathError,
                    }.ToToolResult(Name));
                }

                selectedPath = resolvedPath;
                allowedDirectories.Add(resolvedPath);
            }
            else if (string.IsNullOrWhiteSpace(selectedPath))
            {
                allowedDirectories.AddRange(request.SearchRootPaths);
            }

            var result = CopilotListDirectoryCapability.List(
                allowedDirectories.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                selectedPath,
                toolInput?.Cursor,
                cancellationToken);
            return Task.FromResult(result.ToToolResult(Name));
        }
    }
}
