using System;
using System.Collections.Generic;
using System.IO;
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
            var selectedPath = toolInput?.Path;
            if (!string.IsNullOrWhiteSpace(selectedPath)
                && !IsExplicitlyAllowed(selectedPath, allowedDirectories))
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
                cancellationToken);
            return Task.FromResult(result.ToToolResult(Name));
        }

        private static bool IsExplicitlyAllowed(string path, IEnumerable<string> allowedPaths)
        {
            if (!Path.IsPathFullyQualified(path))
                return false;

            try
            {
                var fullPath = Path.GetFullPath(path);
                return allowedPaths.Any(allowedPath => string.Equals(Path.GetFullPath(allowedPath), fullPath, StringComparison.OrdinalIgnoreCase));
            }
            catch
            {
                return false;
            }
        }
    }
}
