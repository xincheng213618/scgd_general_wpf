#pragma warning disable CA1001 // The semaphore lifetime matches the process-wide singleton and short-lived test stores.
using ColorVision.UI;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public sealed partial class CopilotChatStateStore : IIncrementalCopilotChatStateStore
    {
        private enum StateFileReadStatus
        {
            Missing,
            Valid,
            FutureVersion,
            Invalid,
        }

        private const long MaximumStateFileBytes = 64L * 1024 * 1024;
        private const int MaximumRecoverySnapshots = 12;
        private static readonly TimeSpan RecoverySnapshotInterval = TimeSpan.FromMinutes(30);
        private static readonly Lazy<CopilotChatStateStore> _instance = new(() => new CopilotChatStateStore());
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            Formatting = Formatting.None,
            NullValueHandling = NullValueHandling.Ignore,
        };
        private readonly SemaphoreSlim _fileGate = new(1, 1);

        public static CopilotChatStateStore Instance => _instance.Value;

        public string RootDirectoryPath { get; }

        public string StateDirectoryPath { get; }

        public string StateFilePath { get; }

        public string BackupStateFilePath { get; }

        public string TemporaryStateFilePath { get; }

        public string RecoveryStateDirectoryPath { get; }

        public string AttachmentProtectionMarkerPath { get; }

        public string AttachmentDirectoryPath { get; }

        public CopilotChatStateLoadStatus LastLoadStatus { get; private set; } = new(CopilotChatStateLoadSource.NotAttempted);

        public bool IsStatePersistenceBlocked => LastLoadStatus.IsFutureVersion;

        public bool IsManagedAttachmentCleanupProtected => File.Exists(AttachmentProtectionMarkerPath);

        private CopilotChatStateStore()
            : this(Path.Combine(Environments.DirLocalAppData, "Copilot"))
        {
        }

        public CopilotChatStateStore(string rootDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(rootDirectoryPath))
                throw new ArgumentException("A root directory is required.", nameof(rootDirectoryPath));

            RootDirectoryPath = Path.GetFullPath(rootDirectoryPath);
            StateDirectoryPath = Path.Combine(RootDirectoryPath, "State");
            StateFilePath = Path.Combine(StateDirectoryPath, "chat-state.json");
            BackupStateFilePath = StateFilePath + ".bak";
            TemporaryStateFilePath = StateFilePath + ".tmp";
            RecoveryStateDirectoryPath = Path.Combine(StateDirectoryPath, "Recovery");
            AttachmentProtectionMarkerPath = Path.Combine(StateDirectoryPath, "attachments-recovery.protected");
            AttachmentDirectoryPath = Path.Combine(StateDirectoryPath, "Attachments");
        }

        public CopilotChatState Load()
        {
            _fileGate.Wait();
            try
            {
                EnsureDirectory();
                LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.NotAttempted);
                var recoveryStateFiles = EnumerateRecoveryStateFiles();
                var hadStateCandidate = File.Exists(StateFilePath)
                    || File.Exists(BackupStateFilePath)
                    || File.Exists(TemporaryStateFilePath)
                    || recoveryStateFiles.Length > 0;

                var temporaryStatus = ReadStateFile(TemporaryStateFilePath, out var temporaryState, out var temporarySchemaVersion);
                if (temporaryStatus == StateFileReadStatus.FutureVersion)
                    return BlockForFutureVersion(temporarySchemaVersion);
                if (temporaryStatus == StateFileReadStatus.Valid)
                {
                    var primaryStatus = ReadStateFile(StateFilePath, out _, out var primarySchemaVersion);
                    if (primaryStatus == StateFileReadStatus.FutureVersion)
                        return BlockForFutureVersion(primarySchemaVersion);
                    if (primaryStatus == StateFileReadStatus.Valid
                        && File.GetLastWriteTimeUtc(TemporaryStateFilePath) <= File.GetLastWriteTimeUtc(StateFilePath))
                    {
                        TryDeleteFile(TemporaryStateFilePath);
                    }
                    else
                    {
                        try
                        {
                            ReplaceStateFile(TemporaryStateFilePath);
                        }
                        catch (CopilotChatStateFutureVersionException ex)
                        {
                            return BlockForFutureVersion(ex.SchemaVersion);
                        }
                        catch
                        {
                            // The validated snapshot is still safe to use for this process even if disk promotion fails.
                        }
                    }

                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Temporary);
                    return temporaryState;
                }
                if (temporaryStatus == StateFileReadStatus.Invalid)
                    TryDeleteFile(TemporaryStateFilePath);

                var primaryReadStatus = ReadStateFile(StateFilePath, out var state, out var stateSchemaVersion);
                if (primaryReadStatus == StateFileReadStatus.FutureVersion)
                    return BlockForFutureVersion(stateSchemaVersion);
                if (primaryReadStatus == StateFileReadStatus.Valid)
                {
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Primary);
                    return state;
                }

                var backupReadStatus = ReadStateFile(BackupStateFilePath, out state, out stateSchemaVersion);
                if (backupReadStatus == StateFileReadStatus.FutureVersion)
                    return BlockForFutureVersion(stateSchemaVersion);
                if (backupReadStatus == StateFileReadStatus.Valid)
                {
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Backup);
                    TryRestorePrimaryState(state);
                    return state;
                }

                foreach (var recoveryStateFile in recoveryStateFiles)
                {
                    var recoveryReadStatus = ReadStateFile(recoveryStateFile, out state, out stateSchemaVersion);
                    if (recoveryReadStatus == StateFileReadStatus.FutureVersion)
                        return BlockForFutureVersion(stateSchemaVersion);
                    if (recoveryReadStatus != StateFileReadStatus.Valid)
                        continue;

                    PreserveUnreadableStateCandidate(BackupStateFilePath, "backup");
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.RecoverySnapshot);
                    TryRestorePrimaryState(state);
                    return state;
                }

                if (hadStateCandidate || EnumerateManagedAttachmentFiles(AttachmentDirectoryPath).Length > 0)
                {
                    PreserveUnreadableStateCandidate(BackupStateFilePath, "backup");
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Unrecoverable);
                    ProtectManagedAttachments();
                }
                else
                {
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Fresh);
                }
                return new CopilotChatState();
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public void Save(CopilotChatState state)
        {
            var serializedState = Serialize(state);
            _fileGate.Wait();
            try
            {
                ThrowIfStatePersistenceBlocked();
                WriteSerializedState(serializedState);
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public string Serialize(CopilotChatState state)
        {
            return Serialize(CaptureSnapshot(state));
        }

        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state)
        {
            var capture = BeginSnapshot(state);
            while (capture.CaptureNextChunk())
            {
            }

            return capture.Complete();
        }

        public CopilotChatStateSnapshotCapture BeginSnapshot(CopilotChatState state)
        {
            return new CopilotChatStateSnapshotCapture(state, SerializerSettings);
        }

        public string Serialize(CopilotChatStateSnapshot snapshot)
        {
            ArgumentNullException.ThrowIfNull(snapshot);
            var serializedState = snapshot.Document.ToString(Formatting.None);
            ValidateSerializedStateSize(serializedState);
            return serializedState;
        }

        public async Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(serializedState);
            ValidateSerializedStateSize(serializedState);

            await _fileGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                ThrowIfStatePersistenceBlocked();
                EnsureDirectory();

                try
                {
                    await File.WriteAllTextAsync(TemporaryStateFilePath, serializedState, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), cancellationToken).ConfigureAwait(false);
                    cancellationToken.ThrowIfCancellationRequested();
                    ValidateStateFile(TemporaryStateFilePath);
                    ReplaceStateFile(TemporaryStateFilePath);
                }
                finally
                {
                    TryDeleteFile(TemporaryStateFilePath);
                }
            }
            finally
            {
                _fileGate.Release();
            }
        }

        private void WriteSerializedState(string serializedState)
        {
            EnsureDirectory();

            try
            {
                File.WriteAllText(TemporaryStateFilePath, serializedState, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
                ValidateStateFile(TemporaryStateFilePath);
                ReplaceStateFile(TemporaryStateFilePath);
            }
            finally
            {
                TryDeleteFile(TemporaryStateFilePath);
            }
        }

        private static bool IsPathUnderRoot(string path, string root)
        {
            var relativePath = Path.GetRelativePath(root, path);
            return !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath);
        }

        private static bool ContainsReparsePoint(string root, string target)
        {
            var current = root;
            if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                return true;

            foreach (var segment in Path.GetRelativePath(root, target)
                .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries))
            {
                current = Path.Combine(current, segment);
                if ((File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                    return true;
            }

            return false;
        }
    }
}
