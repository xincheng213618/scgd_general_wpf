using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotListDirectoryTool : ICopilotTool
    {
        public string Name => "ListDirectory";

        public string Description => "列出当前轮允许访问的本地文件夹内容，返回子文件和子目录。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.ReadableLocalDirectoryPaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat;
        }

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = CopilotListDirectoryCapability.List(
                request.ReadableLocalDirectoryPaths,
                toolInput?.Path,
                cancellationToken);
            return Task.FromResult(result.ToToolResult(Name));
        }
    }
}