using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatStateStore
    {
        private void TryRestorePrimaryState(CopilotChatState recoveredState)
        {
            try
            {
                WriteSerializedState(Serialize(recoveredState));
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot recovered state from backup but could not restore the primary state file: {ex.Message}");
            }
        }

        private CopilotChatState BlockForFutureVersion(int schemaVersion)
        {
            LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.FutureVersion, schemaVersion);
            ProtectManagedAttachments();
            return new CopilotChatState();
        }

        private void ThrowIfStatePersistenceBlocked()
        {
            if (LastLoadStatus.IsFutureVersion)
                throw new CopilotChatStateFutureVersionException(
                    LastLoadStatus.SchemaVersion ?? CopilotChatState.CurrentSchemaVersion + 1,
                    CopilotChatState.CurrentSchemaVersion);
        }

        private void ReplaceStateFile(string tempFilePath)
        {
            var currentStatus = ReadStateFile(StateFilePath, out _, out var currentSchemaVersion);
            if (currentStatus == StateFileReadStatus.FutureVersion)
            {
                BlockForFutureVersion(currentSchemaVersion);
                throw new CopilotChatStateFutureVersionException(currentSchemaVersion, CopilotChatState.CurrentSchemaVersion);
            }

            if (currentStatus == StateFileReadStatus.Valid)
            {
                CreateRecoverySnapshotIfNeeded();
                File.Replace(tempFilePath, StateFilePath, BackupStateFilePath, ignoreMetadataErrors: true);
                return;
            }

            if (currentStatus == StateFileReadStatus.Invalid)
                PreserveUnreadableStateCandidate(StateFilePath, "primary");
            File.Move(tempFilePath, StateFilePath, overwrite: true);
        }

        private void CreateRecoverySnapshotIfNeeded()
        {
            try
            {
                Directory.CreateDirectory(RecoveryStateDirectoryPath);
                var recoveryFiles = EnumerateRecoveryStateFiles();
                if (recoveryFiles.Length > 0
                    && DateTime.UtcNow - File.GetLastWriteTimeUtc(recoveryFiles[0]) < RecoverySnapshotInterval)
                {
                    return;
                }

                var snapshotPath = CreateUniqueRecoveryFilePath(
                    $"chat-state-backup-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}",
                    ".json");
                File.Copy(StateFilePath, snapshotPath, overwrite: false);
                File.SetLastWriteTimeUtc(snapshotPath, DateTime.UtcNow);
                TrimRecoveryFiles("chat-state-backup-*.json", MaximumRecoverySnapshots);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not create a recovery state snapshot: {ex.Message}");
            }
        }

        private string[] EnumerateRecoveryStateFiles()
        {
            try
            {
                if (!Directory.Exists(RecoveryStateDirectoryPath))
                    return [];

                return Directory.GetFiles(RecoveryStateDirectoryPath, "chat-state-backup-*.json", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(File.GetLastWriteTimeUtc)
                    .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not enumerate recovery state snapshots: {ex.Message}");
                return [];
            }
        }

        private void PreserveUnreadableStateCandidate(string filePath, string label)
        {
            if (ReadStateFile(filePath, out _, out _) != StateFileReadStatus.Invalid)
                return;

            try
            {
                Directory.CreateDirectory(RecoveryStateDirectoryPath);
                var snapshotPath = CreateUniqueRecoveryFilePath(
                    $"chat-state-unreadable-{label}-{DateTime.UtcNow:yyyyMMdd-HHmmss-fffffff}",
                    ".json");
                File.Copy(filePath, snapshotPath, overwrite: false);
                TrimRecoveryFiles("chat-state-unreadable-*.json", 4);
            }
            catch (Exception ex)
            {
                Trace.TraceWarning($"Copilot could not preserve an unreadable {label} state file: {ex.Message}");
            }
        }

        private string CreateUniqueRecoveryFilePath(string fileNameWithoutExtension, string extension)
        {
            var candidate = Path.Combine(RecoveryStateDirectoryPath, fileNameWithoutExtension + extension);
            for (var suffix = 1; File.Exists(candidate); suffix++)
                candidate = Path.Combine(RecoveryStateDirectoryPath, $"{fileNameWithoutExtension}-{suffix}{extension}");
            return candidate;
        }

        private void TrimRecoveryFiles(string searchPattern, int maximumFiles)
        {
            var files = Directory.GetFiles(RecoveryStateDirectoryPath, searchPattern, SearchOption.TopDirectoryOnly)
                .OrderByDescending(File.GetLastWriteTimeUtc)
                .ThenByDescending(path => path, StringComparer.OrdinalIgnoreCase)
                .Skip(maximumFiles)
                .ToArray();
            foreach (var file in files)
                TryDeleteFile(file);
        }

        private static void ValidateStateFile(string filePath)
        {
            if (ReadStateFile(filePath, out _, out _) != StateFileReadStatus.Valid)
                throw new InvalidDataException("Copilot state serialization did not produce a valid state document.");
        }

        private static void TryDeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch
            {
            }
        }
    }
}
