using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadLocalFileTool : ICopilotTool
    {
        private const int MaxFilesPerRequest = 3;

        public string Name => "ReadLocalFile";

        public string Description => "读取用户在当前消息中明确提到的本地文本文件。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.ReadableLocalFilePaths?.Count > 0
                && request.Mode != CopilotAgentMode.Chat;
        }

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var paths = request.ReadableLocalFilePaths
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Take(MaxFilesPerRequest)
                .ToArray();

            if (paths.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "当前消息没有可读取的显式本地文件路径。",
                    ErrorMessage = "未检测到当前消息中的本地文件路径。",
                };
            }

            var builder = new StringBuilder();
            var successCount = 0;
            var errors = new List<string>();

            foreach (var path in paths)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(path, cancellationToken);
                builder.AppendLine($"[文件] {result.FullPath}");

                if (result.Success)
                {
                    if (result.WasTruncated)
                        builder.AppendLine("说明：文件内容较长，已截断后发送给模型。");

                    builder.AppendLine(result.Content);
                    successCount++;
                }
                else
                {
                    builder.AppendLine(result.ErrorMessage);
                    errors.Add($"{result.FullPath}: {result.ErrorMessage}");
                }

                builder.AppendLine();
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"已读取 {successCount}/{paths.Length} 个显式本地文件。"
                    : $"未能读取显式本地文件，共 {paths.Length} 个路径。",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("；", errors),
            };
        }
    }
}