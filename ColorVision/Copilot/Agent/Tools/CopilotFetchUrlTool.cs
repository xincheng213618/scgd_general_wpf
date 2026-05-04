using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotFetchUrlTool : ICopilotTool
    {
        private const int MaxUrlsPerRequest = 3;

        public string Name => "FetchUrl";

        public string Description => "抓取用户输入中出现的网页标题、描述和正文。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            if (request == null || request.Mode == CopilotAgentMode.Chat)
                return false;

            return CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText).Count > 0;
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var urls = CopilotWebPageToolSupport.ExtractHttpUrls(request.UserText)
                .Take(MaxUrlsPerRequest)
                .ToArray();

            if (urls.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "未检测到可抓取的网页地址。",
                    ErrorMessage = "用户输入中没有可处理的 http/https URL。",
                };
            }

            var builder = new StringBuilder();
            var successCount = 0;
            var errors = new List<string>();

            foreach (var url in urls)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    var page = await CopilotWebPageToolSupport.LoadWebPageContentAsync(url, cancellationToken);
                    builder.AppendLine(CopilotWebPageToolSupport.BuildFetchedWebPageContextBlock(page));
                    builder.AppendLine();
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    builder.AppendLine(CopilotWebPageToolSupport.BuildFailedWebPageContextBlock(url, ex.Message));
                    builder.AppendLine();
                    errors.Add($"{url}: {ex.Message}");
                }
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"已抓取 {successCount}/{urls.Length} 个网页。"
                    : $"未能抓取网页，共 {urls.Length} 个地址。",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("；", errors),
            };
        }
    }
}