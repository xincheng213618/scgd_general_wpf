#pragma warning disable CA1001 // The semaphore lifetime matches the process-wide singleton and short-lived test stores.
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    public enum CopilotChatStateLoadSource
    {
        NotAttempted,
        Fresh,
        Primary,
        Temporary,
        Backup,
        Unrecoverable,
    }

    public readonly record struct CopilotChatStateLoadStatus(CopilotChatStateLoadSource Source)
    {
        public bool IsRecovery => Source is CopilotChatStateLoadSource.Temporary or CopilotChatStateLoadSource.Backup;

        public bool IsUnrecoverable => Source == CopilotChatStateLoadSource.Unrecoverable;
    }

    public interface ICopilotChatStateStore
    {
        string AttachmentDirectoryPath { get; }

        CopilotChatState Load();

        void Save(CopilotChatState state);

        string Serialize(CopilotChatState state);

        Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default);

        int CleanupOrphanedAttachments(CopilotChatState state);
    }

    public sealed class CopilotChatStateStore : ICopilotChatStateStore
    {
        private const long MaximumStateFileBytes = 64L * 1024 * 1024;
        private static readonly Lazy<CopilotChatStateStore> _instance = new(() => new CopilotChatStateStore());
        private static readonly JsonSerializerSettings SerializerSettings = new()
        {
            Formatting = Formatting.Indented,
            NullValueHandling = NullValueHandling.Ignore,
        };
        private readonly SemaphoreSlim _fileGate = new(1, 1);

        public static CopilotChatStateStore Instance => _instance.Value;

        public string RootDirectoryPath { get; }

        public string StateDirectoryPath { get; }

        public string StateFilePath { get; }

        public string BackupStateFilePath { get; }

        public string TemporaryStateFilePath { get; }

        public string AttachmentProtectionMarkerPath { get; }

        public string AttachmentDirectoryPath { get; }

        public CopilotChatStateLoadStatus LastLoadStatus { get; private set; } = new(CopilotChatStateLoadSource.NotAttempted);

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
            AttachmentProtectionMarkerPath = Path.Combine(StateDirectoryPath, "attachments-recovery.protected");
            AttachmentDirectoryPath = Path.Combine(StateDirectoryPath, "Attachments");
        }

        public CopilotChatState Load()
        {
            _fileGate.Wait();
            try
            {
                EnsureDirectory();
                var hadStateCandidate = File.Exists(StateFilePath)
                    || File.Exists(BackupStateFilePath)
                    || File.Exists(TemporaryStateFilePath);

                if (TryRecoverTemporaryState(out var temporaryState))
                {
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Temporary);
                    return temporaryState;
                }

                if (TryLoad(StateFilePath, out var state))
                {
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Primary);
                    return state;
                }

                if (TryLoad(BackupStateFilePath, out state))
                {
                    LastLoadStatus = new CopilotChatStateLoadStatus(CopilotChatStateLoadSource.Backup);
                    TryRestorePrimaryState(state);
                    return state;
                }

                if (hadStateCandidate || EnumerateManagedAttachmentFiles(AttachmentDirectoryPath).Length > 0)
                {
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
                WriteSerializedState(serializedState);
            }
            finally
            {
                _fileGate.Release();
            }
        }

        public string Serialize(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
            var serializedState = JsonConvert.SerializeObject(state, SerializerSettings);
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

        private bool TryRecoverTemporaryState(out CopilotChatState state)
        {
            if (!TryLoad(TemporaryStateFilePath, out state))
            {
                TryDeleteFile(TemporaryStateFilePath);
                return false;
            }

            var currentIsValid = TryLoad(StateFilePath, out _);
            if (currentIsValid
                && File.GetLastWriteTimeUtc(TemporaryStateFilePath) <= File.GetLastWriteTimeUtc(StateFilePath))
            {
                TryDeleteFile(TemporaryStateFilePath);
                return false;
            }

            try
            {
                ReplaceStateFile(TemporaryStateFilePath);
            }
            catch
            {
                // The validated snapshot is still safe to use for this process even if disk promotion fails.
            }
            return true;
        }

        private void ReplaceStateFile(string tempFilePath)
        {
            if (TryLoad(StateFilePath, out _))
            {
                File.Replace(tempFilePath, StateFilePath, BackupStateFilePath, ignoreMetadataErrors: true);
                return;
            }

            File.Move(tempFilePath, StateFilePath, overwrite: true);
        }

        private static void ValidateStateFile(string filePath)
        {
            if (!TryLoad(filePath, out _))
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

        public int CleanupOrphanedAttachments(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            EnsureDirectory();

            var attachmentRoot = Path.GetFullPath(AttachmentDirectoryPath);
            var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in (state.Conversations ?? new System.Collections.ObjectModel.ObservableCollection<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .SelectMany(conversation => conversation.Attachments))
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
            if (IsManagedAttachmentCleanupProtected || LastLoadStatus.IsUnrecoverable)
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

        private static bool TryLoad(string filePath, out CopilotChatState state)
        {
            state = new CopilotChatState();
            if (!File.Exists(filePath))
                return false;

            try
            {
                using var stream = new FileStream(
                    filePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 8192,
                    FileOptions.SequentialScan);
                if (stream.Length > MaximumStateFileBytes)
                    return false;

                using var textReader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 8192,
                    leaveOpen: false);
                using var jsonReader = new JsonTextReader(textReader) { CloseInput = false };
                if (JToken.Load(jsonReader) is not JObject document
                    || jsonReader.Read()
                    || !HasTrustedDocumentShape(document))
                {
                    return false;
                }

                var deserializedState = document.ToObject<CopilotChatState>(JsonSerializer.Create(SerializerSettings));
                if (deserializedState == null)
                    return false;

                state = deserializedState;
                state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static void ValidateSerializedStateSize(string serializedState)
        {
            if (serializedState.Length <= MaximumStateFileBytes
                && Encoding.UTF8.GetByteCount(serializedState) <= MaximumStateFileBytes)
            {
                return;
            }

            throw new InvalidDataException(
                $"Copilot state snapshot exceeded the size limit ({MaximumStateFileBytes / 1024 / 1024} MB).");
        }

        private static bool HasTrustedDocumentShape(JObject document)
        {
            var schemaToken = document.GetValue(nameof(CopilotChatState.SchemaVersion), StringComparison.OrdinalIgnoreCase);
            if (schemaToken != null)
            {
                if (schemaToken.Type != JTokenType.Integer)
                    return false;

                var schemaVersion = schemaToken.Value<int>();
                if (schemaVersion < 1 || schemaVersion > CopilotChatState.CurrentSchemaVersion)
                    return false;
            }

            if (!IsStringOrNull(document.GetValue(nameof(CopilotChatState.ActiveConversationId), StringComparison.OrdinalIgnoreCase))
                || !IsStringOrNull(document.GetValue(nameof(CopilotChatState.ActiveProfileId), StringComparison.OrdinalIgnoreCase))
                || document.GetValue(nameof(CopilotChatState.Conversations), StringComparison.OrdinalIgnoreCase) is not JArray conversations)
            {
                return false;
            }

            foreach (var conversationToken in conversations)
            {
                if (conversationToken is not JObject conversation
                    || conversation.GetValue(nameof(CopilotConversationRecord.Messages), StringComparison.OrdinalIgnoreCase) is not JArray messages
                    || conversation.GetValue(nameof(CopilotConversationRecord.Attachments), StringComparison.OrdinalIgnoreCase) is not JArray attachments
                    || messages.Any(item => item is not JObject)
                    || attachments.Any(item => item is not JObject))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsStringOrNull(JToken? token) => token?.Type is JTokenType.String or JTokenType.Null;

        private static bool IsPathUnderRoot(string path, string root)
        {
            var relativePath = Path.GetRelativePath(root, path);
            return !relativePath.Equals("..", StringComparison.Ordinal)
                && !relativePath.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal)
                && !Path.IsPathRooted(relativePath);
        }
    }
}
