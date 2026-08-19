using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadLocalFileTool : ICopilotAgentDrivenTool
    {
        private readonly int _maximumReadCharacters;

        public CopilotReadLocalFileTool()
            : this(CopilotLocalFileToolSupport.MaxReadCharacters)
        {
        }

        internal CopilotReadLocalFileTool(int maximumReadCharacters)
        {
            if (maximumReadCharacters is < CopilotLocalFileToolSupport.MinimumReadCharacters
                or > CopilotLocalFileToolSupport.MaxReadCharacters)
            {
                throw new ArgumentOutOfRangeException(nameof(maximumReadCharacters));
            }

            _maximumReadCharacters = maximumReadCharacters;
        }

        public string Name => CopilotSharedCapabilityCatalog.ReadAllowedFile.AgentToolName;

        public string Description => CopilotSharedCapabilityCatalog.ReadAllowedFile.AgentDescription;

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotSharedCapabilityCatalog.ReadAllowedFile.AgentCapability;

        public CopilotToolInputSchema InputSchema => CopilotSharedCapabilityCatalog.ReadAllowedFile.AgentInputSchema;

        internal int MaximumReadCharacters => _maximumReadCharacters;

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request != null
                && request.Mode != CopilotAgentMode.Chat
                && (request.ReadableLocalFilePaths.Count > 0 || request.SearchRootPaths.Count > 0)
                && CopilotToolIntentPolicy.NeedsLocalEvidence(request);
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
                && !CopilotWorkspaceSearchSupport.IsExplicitlyAllowedPath(selectedPath, allowedFiles))
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

            var distinctAllowedFiles = allowedFiles
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            var useTaskFocusedBatch = request.PreferBatchReadLocalFiles
                && string.IsNullOrWhiteSpace(selectedPath)
                && toolInput?.StartLine == null
                && toolInput?.StartColumn == null
                && toolInput?.EndLine == null
                && distinctAllowedFiles.Length > 1;
            var result = useTaskFocusedBatch
                ? await CopilotReadLocalFileCapability.ReadTaskFocusedBatchAsync(
                    distinctAllowedFiles,
                    request.UserText,
                    _maximumReadCharacters,
                    cancellationToken)
                : await CopilotReadLocalFileCapability.ReadAsync(
                    distinctAllowedFiles,
                    selectedPath,
                    request.PreferBatchReadLocalFiles,
                    toolInput?.StartLine,
                    toolInput?.StartColumn,
                    toolInput?.EndLine,
                    _maximumReadCharacters,
                    cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}
