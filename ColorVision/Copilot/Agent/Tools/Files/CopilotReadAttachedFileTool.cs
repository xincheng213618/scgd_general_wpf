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
        private const int MaxAttachmentsPerBatch = 3;
        private const int MaxReportedOmittedAttachments = 8;

        public string Name => "ReadAttachedFile";

        public string Description => "Read up to three text file attachments from the current session, or one selected attachment with an optional line-and-column continuation range.";

        public CopilotToolInputSchema InputSchema { get; } = CopilotToolInputSchema.FileRead();

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

            var attachmentPaths = request.Attachments
                .Where(item => item.Type == CopilotAttachmentType.File && !string.IsNullOrWhiteSpace(item.Value))
                .Select(item => NormalizePath(item.Value))
                .Where(path => !string.IsNullOrWhiteSpace(path))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            if (attachmentPaths.Length == 0)
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The current session has no readable file attachments.",
                    ErrorMessage = "No file attachments were found.",
                };
            }

            if (!TryResolveSelectedPath(toolInput?.Path, attachmentPaths, out var selectedPath, out var selectionError))
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "The selected file is not an attachment in the current session.",
                    ErrorMessage = selectionError,
                    FailureKind = CopilotToolFailureKind.Validation,
                };
            }

            if (string.IsNullOrWhiteSpace(selectedPath)
                && (toolInput?.StartLine.HasValue == true || toolInput?.StartColumn.HasValue == true || toolInput?.EndLine.HasValue == true))
            {
                return new CopilotToolResult
                {
                    ToolName = Name,
                    Success = false,
                    Summary = "An attachment path is required for a line or column range.",
                    ErrorMessage = "Select one attached file with path before supplying startLine, startColumn, or endLine.",
                    FailureKind = CopilotToolFailureKind.Validation,
                };
            }

            var attemptedPaths = string.IsNullOrWhiteSpace(selectedPath)
                ? attachmentPaths.Take(MaxAttachmentsPerBatch).ToArray()
                : [selectedPath];
            var omittedPaths = string.IsNullOrWhiteSpace(selectedPath)
                ? attachmentPaths.Skip(attemptedPaths.Length).ToArray()
                : Array.Empty<string>();
            var reportedOmittedPaths = omittedPaths.Take(MaxReportedOmittedAttachments).ToArray();
            var result = await CopilotReadLocalFileCapability.ReadAsync(
                attachmentPaths,
                selectedPath,
                preferBatchReadAll: false,
                toolInput?.StartLine,
                toolInput?.StartColumn,
                toolInput?.EndLine,
                cancellationToken);

            var builder = new StringBuilder();
            builder.AppendLine("[Attachment Read Scope]");
            builder.AppendLine($"selection_mode: {(string.IsNullOrWhiteSpace(selectedPath) ? "batch" : "selected")}");
            builder.AppendLine($"attachment_files_total: {attachmentPaths.Length}");
            builder.AppendLine($"attachment_files_attempted: {attemptedPaths.Length}");
            builder.AppendLine($"attachment_files_omitted: {omittedPaths.Length}");
            builder.AppendLine($"attachment_set_complete: {(omittedPaths.Length == 0).ToString().ToLowerInvariant()}");
            builder.AppendLine($"omitted_attachment_paths_listed: {reportedOmittedPaths.Length}");
            builder.AppendLine($"omitted_attachment_list_complete: {(reportedOmittedPaths.Length == omittedPaths.Length).ToString().ToLowerInvariant()}");
            foreach (var omittedPath in reportedOmittedPaths)
                builder.AppendLine($"omitted_attachment_path: {omittedPath}");
            if (!string.IsNullOrWhiteSpace(result.Content))
            {
                builder.AppendLine();
                builder.Append(result.Content);
            }

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = result.Success,
                Summary = omittedPaths.Length == 0
                    ? result.Summary
                    : $"{result.Summary} {omittedPaths.Length} additional attachment(s) were not read in this batch.",
                Content = builder.ToString().TrimEnd(),
                ErrorMessage = result.ErrorMessage,
                FailureKind = result.FailureKind,
                SuggestedReadableLocalFilePaths = result.SuggestedReadableLocalFilePaths,
                AttemptedLocalFilePaths = result.AttemptedLocalFilePaths,
                SuccessfullyReadLocalFilePaths = result.SuccessfullyReadLocalFilePaths,
            };
        }

        private static bool TryResolveSelectedPath(
            string? requestedPath,
            IReadOnlyList<string> attachmentPaths,
            out string selectedPath,
            out string error)
        {
            selectedPath = string.Empty;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(requestedPath))
                return true;

            var normalized = NormalizePath(requestedPath);
            var exactMatch = attachmentPaths.FirstOrDefault(path => string.Equals(path, normalized, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(exactMatch))
            {
                selectedPath = exactMatch;
                return true;
            }

            var fileName = Path.GetFileName(requestedPath.Trim());
            var fileNameMatches = attachmentPaths
                .Where(path => string.Equals(Path.GetFileName(path), fileName, StringComparison.OrdinalIgnoreCase))
                .Take(2)
                .ToArray();
            if (fileNameMatches.Length == 1)
            {
                selectedPath = fileNameMatches[0];
                return true;
            }

            error = fileNameMatches.Length > 1
                ? $"More than one current attachment is named {fileName}; use the exact attached path."
                : $"The selected path is not a current file attachment: {requestedPath.Trim()}";
            return false;
        }

        private static string NormalizePath(string? path)
        {
            if (string.IsNullOrWhiteSpace(path))
                return string.Empty;

            try
            {
                return Path.GetFullPath(path);
            }
            catch
            {
                return path.Trim();
            }
        }
    }
}
