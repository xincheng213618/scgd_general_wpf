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
                || CopilotDocsToolSupport.HasDocumentationIntent(request.UserText);
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var query = CopilotDocsToolSupport.ResolveQuery(request, toolInput);
            if (string.IsNullOrWhiteSpace(query))
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "缺少文档检索问题或关键字。",
                    ErrorMessage = "请提供更具体的软件问题、页面名称或功能关键字。",
                };
            }

            try
            {
                var searchResult = await CopilotDocsToolSupport.SearchAsync(query, cancellationToken);
                if (searchResult.Hits.Count == 0)
                {
                    return new CopilotToolResult
                    {
                        ToolName = Name,
                        Success = false,
                        Summary = "在线文档中没有检索到相关片段。",
                        ErrorMessage = "请把问题说得更具体，例如功能名、页面名、菜单名、设备名或模块名。",
                    };
                }

                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = true,
                    Summary = $"已从在线文档命中 {searchResult.DistinctPageCount} 个页面，返回 {searchResult.Hits.Count} 个相关片段。",
                    Content = CopilotDocsToolSupport.BuildContextBlock(searchResult),
                };
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "无法读取在线文档索引。",
                    ErrorMessage = ex.Message,
                };
            }
        }
    }
}