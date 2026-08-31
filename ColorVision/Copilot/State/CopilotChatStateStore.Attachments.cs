#pragma warning disable CA1001 // The semaphore lifetime matches the process-wide singleton and short-lived test stores.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatStateStore
    {
        public int CleanupOrphanedAttachments(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            EnsureDirectory();

            var attachmentRoot = Path.GetFullPath(AttachmentDirectoryPath);
            try
            {
                if (ContainsReparsePoint(attachmentRoot, attachmentRoot))
                {
                    Trace.TraceWarning("Copilot orphan attachment cleanup skipped because the attachment root is a reparse point.");
                    return 0;
                }
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not validate the attachment cleanup root: {ex.Message}");
                return 0;
            }

            var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in (state.Conversations ?? new System.Collections.ObjectModel.ObservableCollection<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .SelectMany(conversation => conversation.EnumerateReferencedAttachments())
                .Concat((state.QueuedFollowUpRecoveries
                        ?? new System.Collections.ObjectModel.ObservableCollection<CopilotQueuedFollowUpRecoveryRecord>())
                    .Where(recovery => recovery != null)
                    .SelectMany(recovery => recovery.EnumerateReferencedAttachments())))
            {
                if (string.IsNullOrWhiteSpace(attachment.Value))
                    continue;

                try
                {
                    var fullPath = Path.GetFullPath(attachment.Value);
                    if (IsPathUnderRoot(fullPath, attachmentRoot))
                        referencedPaths.Add(fullPath);
                }
                catch
                {
                }
            }

            if (!TryEnumerateManagedAttachmentFiles(attachmentRoot, out var managedFiles))
                return 0;
            if (IsManagedAttachmentCleanupProtected || LastLoadStatus.RequiresRecoveryProtection)
            {
                if (managedFiles.Any(filePath => !referencedPaths.Contains(Path.GetFullPath(filePath))))
                {
                    Trace.TraceWarning("Copilot orphan attachment cleanup skipped because recovery protection is active.");
                    return 0;
                }

                TryDeleteFile(AttachmentProtectionMarkerPath);
            }

            var deletedCount = 0;
            foreach (var filePath in managedFiles)
            {
                if (referencedPaths.Contains(Path.GetFullPath(filePath)))
                    continue;

                if (TryDeleteManagedAttachmentFile(attachmentRoot, filePath))
                    deletedCount++;
            }

            return deletedCount;
        }

        private bool ProtectManagedAttachments()
        {
            try
            {
                File.WriteAllText(
                    AttachmentProtectionMarkerPath,
                    $"Copilot state recovery protection created at {DateTimeOffset.UtcNow:O}.{Environment.NewLine}"
                    + "Unreferenced managed attachments must not be deleted until their state can be recovered or they are explicitly reattached.",
                    new UTF8Encoding(false));
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not create attachment recovery protection: {ex.Message}");
                return false;
            }
        }

        private static bool TryEnumerateManagedAttachmentFiles(string attachmentRoot, out string[] files)
        {
            try
            {
                files = Directory.GetFiles(attachmentRoot, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = false,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                });
                return true;
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not enumerate managed attachments: {ex.Message}");
                files = [];
                return false;
            }
        }

        public static bool TryDeleteManagedAttachmentFile(string attachmentDirectoryPath, string filePath)
        {
            if (string.IsNullOrWhiteSpace(attachmentDirectoryPath) || string.IsNullOrWhiteSpace(filePath))
                return false;

            try
            {
                var attachmentRoot = Path.GetFullPath(attachmentDirectoryPath);
                var candidatePath = Path.GetFullPath(filePath);
                if (!IsPathUnderRoot(candidatePath, attachmentRoot) || !File.Exists(candidatePath))
                    return false;
                if (ContainsReparsePoint(attachmentRoot, candidatePath))
                    return false;

                File.Delete(candidatePath);
                return true;
            }
            catch
            {
                return false;
            }
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(StateDirectoryPath))
                Directory.CreateDirectory(StateDirectoryPath);

            if (!Directory.Exists(AttachmentDirectoryPath))
                Directory.CreateDirectory(AttachmentDirectoryPath);
        }
    }
}
