using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    internal static class CopilotComposerAttachmentService
    {
        public const int MaximumAttachmentCount = 32;

        private static readonly HashSet<string> UnsafeAttachmentExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".application", ".bat", ".cmd", ".com", ".cpl", ".exe", ".gadget", ".hta", ".inf", ".ins", ".isp",
            ".jar", ".js", ".jse", ".lnk", ".msi", ".msp", ".pif", ".ps1", ".py", ".pyw", ".reg", ".scr",
            ".sct", ".sh", ".shb", ".shs", ".url", ".vb", ".vbe", ".vbs", ".ws", ".wsc", ".wsf", ".wsh",
        };

        public static string[] NormalizeFilePaths(IEnumerable<string>? filePaths)
        {
            if (filePaths == null)
                return [];

            return filePaths
                .Where(filePath => !string.IsNullOrWhiteSpace(filePath))
                .Select(TryNormalizeFilePath)
                .Where(filePath => filePath != null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }

        public static string[] FilterExistingFilePaths(
            IEnumerable<string> normalizedPaths,
            CancellationToken cancellationToken)
        {
            var existingPaths = new List<string>();
            foreach (var filePath in normalizedPaths)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (File.Exists(filePath))
                    existingPaths.Add(filePath);
            }

            return existingPaths.ToArray();
        }

        public static bool IsUnsafeFilePath(string filePath) =>
            UnsafeAttachmentExtensions.Contains(Path.GetExtension(filePath));

        public static CopilotAttachmentCapacityResult EvaluateCapacity(
            CopilotConversationRecord conversation,
            CopilotAttachmentType attachmentType)
        {
            ArgumentNullException.ThrowIfNull(conversation);

            if (attachmentType == CopilotAttachmentType.Image
                && conversation.Attachments.Count(attachment => attachment.Type == CopilotAttachmentType.Image)
                    >= CopilotImagePayloadLoader.MaximumImages)
            {
                return CopilotAttachmentCapacityResult.ImageLimit;
            }

            return conversation.Attachments.Count >= MaximumAttachmentCount
                ? CopilotAttachmentCapacityResult.AttachmentLimit
                : CopilotAttachmentCapacityResult.Available;
        }

        public static CopilotAttachmentValidationResult Validate(
            IEnumerable<CopilotAttachmentItem> attachments)
        {
            ArgumentNullException.ThrowIfNull(attachments);
            var attachmentSnapshot = attachments.Where(attachment => attachment != null).ToArray();
            var imageCount = attachmentSnapshot.Count(attachment => attachment.Type == CopilotAttachmentType.Image);
            var failure = attachmentSnapshot.Length > MaximumAttachmentCount
                ? CopilotAttachmentValidationFailure.AttachmentLimit
                : imageCount > CopilotImagePayloadLoader.MaximumImages
                    ? CopilotAttachmentValidationFailure.ImageLimit
                    : CopilotAttachmentValidationFailure.None;
            return new CopilotAttachmentValidationResult(
                failure,
                attachmentSnapshot.Length,
                imageCount);
        }

        public static int RemoveCapturedByReference(
            IList<CopilotAttachmentItem> currentAttachments,
            IReadOnlyList<CopilotAttachmentItem> capturedAttachments)
        {
            ArgumentNullException.ThrowIfNull(currentAttachments);
            ArgumentNullException.ThrowIfNull(capturedAttachments);

            var removedCount = 0;
            for (var index = currentAttachments.Count - 1; index >= 0; index--)
            {
                var attachment = currentAttachments[index];
                if (!capturedAttachments.Any(captured => ReferenceEquals(captured, attachment)))
                    continue;

                currentAttachments.RemoveAt(index);
                removedCount++;
            }
            return removedCount;
        }

        private static string? TryNormalizeFilePath(string filePath)
        {
            try
            {
                return Path.GetFullPath(filePath.Trim());
            }
            catch (Exception ex) when (ex is ArgumentException or IOException or NotSupportedException or System.Security.SecurityException)
            {
                return null;
            }
        }
    }

    internal enum CopilotAttachmentCapacityResult
    {
        Available,
        ImageLimit,
        AttachmentLimit,
    }

    internal enum CopilotAttachmentValidationFailure
    {
        None,
        AttachmentLimit,
        ImageLimit,
    }

    internal readonly record struct CopilotAttachmentValidationResult(
        CopilotAttachmentValidationFailure Failure,
        int AttachmentCount,
        int ImageCount)
    {
        public bool IsValid => Failure == CopilotAttachmentValidationFailure.None;
    }
}
