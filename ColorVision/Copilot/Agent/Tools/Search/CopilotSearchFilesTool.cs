using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchFilesTool : ICopilotTool
    {
        public string Name => "SearchFiles";

        public string Description => "按文件名或路径片段在当前解决方案范围内查找候选文件。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null
                || request.Mode == CopilotAgentMode.Chat
                || request.SearchRootPaths.Count == 0)
            {
                return false;
            }

            return true;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = CopilotSearchFilesCapability.Search(
                request.SearchRootPaths,
                toolInput?.Query,
                request.UserText,
                allowPlainSearchTerms: false,
                cancellationToken);
            return Task.FromResult(result.ToCapabilityResult().ToToolResult(Name));
        }
    }
}