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

        public string Name => "ReadLocalFile";

        public string Description => "Read bounded local text allowed for the current round, prefix every returned source line with its authoritative one-based L<number>: coordinate, and report a safe line-and-column continuation cursor when content is truncated. When multiple exact files are preselected, omit path and line range to batch-read one task-focused evidence window from every file in one call. Otherwise, for known files or symbols, use GrepText on each exact file first and request focused line ranges; an unbounded read intentionally returns only the first bounded segment.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.FileRead();

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
