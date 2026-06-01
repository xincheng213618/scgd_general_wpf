using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotSearchDocsTool : ICopilotTool
    {
        public string Name => "SearchDocs";

        public string Description => "查询 ColorVision 在线文档索引，按章节、页面和页面内标题返回最相关的文档片段。适合回答软件使用、菜单、设备、插件、开发指南和架构说明问题。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            return request.Mode == CopilotAgentMode.Web
                || CopilotDocsCapability.HasDocumentationIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = CopilotDocsCapability.ResolveQuery(request.UserText, toolInput?.Query);
            var result = await CopilotDocsCapability.SearchAsync(query, cancellationToken);
            return result.ToToolResult(Name);
        }
    }
}