using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadAttachedFileTool : ICopilotTool
    {
        private const int MaxFileContentChars = 20000;

        public string Name => "ReadAttachedFile";

        public string Description => "读取当前会话已附加的文本文件内容。";

        public bool CanHandle(CopilotAgentRequest request)
        {
            return request?.Attachments?.Any(item => item.Type == CopilotAttachmentType.File) == true
                && request.Mode != CopilotAgentMode.Chat;
        }

        public async Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);

            var attachments = request.Attachments
                .Where(item => item.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value))
                .GroupBy(item => item.Value, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .ToArray();

            if (attachments.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "当前会话没有可读取的文件附件。",
                    ErrorMessage = "未找到文件类型的会话附件。",
                };
            }

            var builder = new StringBuilder();
            var successCount = 0;
            var errors = new List<string>();

            foreach (var attachment in attachments)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    if (!File.Exists(attachment.Value))
                    {
                        var missingMessage = $"文件不存在：{attachment.Value}";
                        builder.AppendLine($"[文件] {attachment.Value}");
                        builder.AppendLine(missingMessage);
                        builder.AppendLine();
                        errors.Add(missingMessage);
                        continue;
                    }

                    await using var stream = new FileStream(attachment.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8, true);
                    var content = await reader.ReadToEndAsync(cancellationToken);

                    if (content.Length > MaxFileContentChars)
                        content = content[..MaxFileContentChars] + Environment.NewLine + $"...<内容已截断，仅保留前 {MaxFileContentChars} 字符。>";

                    builder.AppendLine($"[文件] {attachment.Value}");
                    builder.AppendLine(content.TrimEnd());
                    builder.AppendLine();
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    builder.AppendLine($"[文件] {attachment.Value}");
                    builder.AppendLine($"读取失败：{ex.Message}");
                    builder.AppendLine();
                    errors.Add($"{attachment.Value}: {ex.Message}");
                }
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"已读取 {successCount}/{attachments.Length} 个附件文件。"
                    : $"未能读取附件文件，共 {attachments.Length} 个。",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("；", errors),
            };
        }
    }
}