using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchFilesTool : ICopilotAgentDrivenTool
    {
        public string Name => "SearchFiles";

        public string Description => "Find one stable bounded page of candidate files by file name or path fragment, optionally limited to one workspace directory, with a continuation cursor when more matches remain.";

        public CopilotToolInputSchema InputSchema { get; } = new CopilotToolInputSchema(new[]
        {
            new CopilotToolParameter { Name = "query", Description = "Literal file name or workspace-relative path fragment to locate; not a natural-language instruction or glob.", Type = CopilotToolParameterType.Text, Required = true },
            new CopilotToolParameter { Name = "path", Description = "Optional workspace-relative or absolute directory to search within.", Type = CopilotToolParameterType.Text },
            new CopilotToolParameter { Name = "cursor", Description = "Optional opaque next_cursor returned by the preceding SearchFiles page for the same query and path. Never invent or modify it.", Type = CopilotToolParameterType.Text },
        });

        public bool IsAvailable(CopilotAgentRequest request)
        {
            return request?.SearchRootPaths?.Count > 0 && request.Mode != CopilotAgentMode.Chat;
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
