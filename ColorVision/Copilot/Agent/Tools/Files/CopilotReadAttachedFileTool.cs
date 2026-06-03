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

        public string Description => "Read text file attachments from the current session.";

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
                    if (!File.Exists(attachment.Value))
                    {
                        var missingMessage = $"File does not exist: {attachment.Value}";
                        builder.AppendLine($"[File] {attachment.Value}");
                        builder.AppendLine(missingMessage);
                        builder.AppendLine();
                        errors.Add(missingMessage);
                        continue;
                    }

                    await using var stream = new FileStream(attachment.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                    using var reader = new StreamReader(stream, Encoding.UTF8, true);
                    var content = await reader.ReadToEndAsync(cancellationToken);

                    if (content.Length > MaxFileContentChars)
                        content = content[..MaxFileContentChars] + Environment.NewLine + $"...<content truncated; kept the first {MaxFileContentChars} characters.>";

                    builder.AppendLine($"[File] {attachment.Value}");
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
