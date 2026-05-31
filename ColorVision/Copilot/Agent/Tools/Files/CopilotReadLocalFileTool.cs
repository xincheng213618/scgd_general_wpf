using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadLocalFileTool : ICopilotTool
    {
        public string Name => "ReadLocalFile";

        public string Description => "读取当前轮允许访问的本地文本文件，支持按路径和行范围精读。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.ReadableLocalFilePaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat;
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var result = await CopilotReadLocalFileCapability.ReadAsync(
                request.ReadableLocalFilePaths,
                toolInput?.Path,
                request.PreferBatchReadLocalFiles,
                toolInput?.StartLine,
                toolInput?.EndLine,
                cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}