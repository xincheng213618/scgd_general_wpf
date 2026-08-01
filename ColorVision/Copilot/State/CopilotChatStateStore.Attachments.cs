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

            var managedFiles = EnumerateManagedAttachmentFiles(attachmentRoot);
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

                try
                {
                    File.Delete(filePath);
                    deletedCount++;
                }
                catch
                {
                }
            }

            return deletedCount;
        }

        private void ProtectManagedAttachments()
        {
            try
            {
                File.WriteAllText(
                    AttachmentProtectionMarkerPath,
                    $"Copilot state recovery protection created at {DateTimeOffset.UtcNow:O}.{Environment.NewLine}"
                    + "Unreferenced managed attachments must not be deleted until their state can be recovered or they are explicitly reattached.",
                    new UTF8Encoding(false));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not create attachment recovery protection: {ex.Message}");
            }
        }

        private static string[] EnumerateManagedAttachmentFiles(string attachmentRoot)
        {
            try
            {
                return Directory.GetFiles(attachmentRoot, "*", new EnumerationOptions
                {
                    RecurseSubdirectories = true,
                    IgnoreInaccessible = true,
                    AttributesToSkip = FileAttributes.ReparsePoint,
                });
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not enumerate managed attachments: {ex.Message}");
                return [];
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
