using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadLocalFileTool : ICopilotAgentDrivenTool
    {
        public string Name => "ReadLocalFile";

        public string Description => "Read local text files allowed for the current round, with optional path and line-range focus, and report a safe line-and-column continuation cursor when content is truncated.";

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
            var selectedPath = toolInput?.Path;
            if (!string.IsNullOrWhiteSpace(selectedPath)
                && !IsExplicitlyAllowed(selectedPath, allowedFiles))
            {
                if (!CopilotWorkspaceSearchSupport.TryResolveExistingFileWithinRoots(
                    selectedPath,
                    request.SearchRootPaths,
                    out var resolvedPath,
                    out var pathError))
                {
                    return new CopilotCapabilityResult
                    {
                        Success = false,
                        Summary = "The requested local file could not be resolved within the current workspace.",
                        ErrorMessage = pathError,
                    }.ToToolResult(Name);
                }

                selectedPath = resolvedPath;
                allowedFiles.Add(resolvedPath);
            }

            var result = await CopilotReadLocalFileCapability.ReadAsync(
                allowedFiles.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                selectedPath,
                request.PreferBatchReadLocalFiles,
                toolInput?.StartLine,
                toolInput?.StartColumn,
                toolInput?.EndLine,
                cancellationToken);
            return result.ToToolResult(Name);
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
