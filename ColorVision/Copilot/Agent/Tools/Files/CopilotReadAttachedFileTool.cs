using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed class CopilotReadAttachedFileTool : ICopilotTool
    {
        public string Name => "ReadAttachedFile";

        public string Description => "Read text file attachments from the current session.";

        public CopilotToolInputSchema InputSchema => CopilotToolInputSchema.Empty;

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
                    Summary = "The current session has no readable file attachments.",
                    ErrorMessage = "No file attachments were found.",
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
                    var result = await CopilotLocalFileToolSupport.ReadTextFileAsync(attachment.Value, cancellationToken);
                    builder.AppendLine($"[File] {result.FullPath}");

                    if (!result.Success)
                    {
                        builder.AppendLine(result.ErrorMessage);
                        builder.AppendLine();
                        errors.Add($"{result.FullPath}: {result.ErrorMessage}");
                        continue;
                    }

                    if (result.WasTruncated)
                        builder.AppendLine("Note: The file content was long and was truncated before sending to the model.");

                    builder.AppendLine(result.Content);
                    builder.AppendLine();
                    successCount++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    builder.AppendLine($"[File] {attachment.Value}");
                    builder.AppendLine($"Read failed: {ex.Message}");
                    builder.AppendLine();
                    errors.Add($"{attachment.Value}: {ex.Message}");
                }
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = successCount > 0,
                Summary = successCount > 0
                    ? $"Read {successCount}/{attachments.Length} attached files."
                    : $"Failed to read any attached files from {attachments.Length} attachments.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = errors.Count == 0 ? string.Empty : string.Join("; ", errors),
            };
        }
    }
}
