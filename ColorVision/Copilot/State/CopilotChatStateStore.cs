#pragma warning disable CA1001 // The semaphore lifetime matches the process-wide singleton and short-lived test stores.
using ColorVision.UI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
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
        RecoverySnapshot,
        FutureVersion,
        Unrecoverable,
    }

    public readonly record struct CopilotChatStateLoadStatus(CopilotChatStateLoadSource Source, int? SchemaVersion = null)
    {
        public bool IsRecovery => Source is CopilotChatStateLoadSource.Temporary
            or CopilotChatStateLoadSource.Backup
            or CopilotChatStateLoadSource.RecoverySnapshot;

        public bool IsUnrecoverable => Source == CopilotChatStateLoadSource.Unrecoverable;

        public bool IsFutureVersion => Source == CopilotChatStateLoadSource.FutureVersion;

        public bool RequiresRecoveryProtection => IsUnrecoverable || IsFutureVersion;
    }

    public sealed class CopilotChatStateSizeLimitException : IOException
    {
        public long ActualBytes { get; }

        public long MaximumBytes { get; }

        public CopilotChatStateSizeLimitException(long actualBytes, long maximumBytes)
            : base($"Copilot state snapshot exceeded the size limit ({actualBytes / 1024d / 1024d:F1} MB of {maximumBytes / 1024 / 1024} MB).")
        {
            ActualBytes = actualBytes;
            MaximumBytes = maximumBytes;
        }
    }

    public sealed class CopilotChatStateFutureVersionException : IOException
    {
        public int SchemaVersion { get; }

        public int SupportedSchemaVersion { get; }

        public CopilotChatStateFutureVersionException(int schemaVersion, int supportedSchemaVersion)
            : base($"Copilot state schema {schemaVersion} was created by a newer application version; this version supports schema {supportedSchemaVersion}.")
        {
            SchemaVersion = schemaVersion;
            SupportedSchemaVersion = supportedSchemaVersion;
        }
    }

    public sealed class CopilotChatStateSnapshot
    {
        internal JObject Document { get; }

        internal CopilotChatStateSnapshot(JObject document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
        }
    }

    public sealed class CopilotChatStateSnapshotCapture
    {
        private readonly JsonSerializer _serializer;
        private readonly JObject _document;
        private readonly JArray _conversationDocuments = new();
        private readonly JArray? _queuedFollowUpRecoveryDocuments;
        private readonly CopilotConversationRecord[] _conversations;
        private readonly CopilotQueuedFollowUpRecoveryRecord[] _queuedFollowUpRecoveries;
        private int _conversationIndex;
        private int _queuedFollowUpRecoveryIndex;

        internal CopilotChatStateSnapshotCapture(CopilotChatState state, JsonSerializerSettings serializerSettings)
        {
            ArgumentNullException.ThrowIfNull(state);
            ArgumentNullException.ThrowIfNull(serializerSettings);

            state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
            _serializer = JsonSerializer.Create(serializerSettings);
            _conversations = state.Conversations?.ToArray() ?? [];
            _queuedFollowUpRecoveries = state.QueuedFollowUpRecoveries?.ToArray() ?? [];
            _queuedFollowUpRecoveryDocuments = _queuedFollowUpRecoveries.Length > 0 ? new JArray() : null;
            _document = new JObject
            {
                [nameof(CopilotChatState.SchemaVersion)] = state.SchemaVersion,
                [nameof(CopilotChatState.Conversations)] = _conversationDocuments,
            };
            AddStringProperty(_document, nameof(CopilotChatState.ActiveConversationId), state.ActiveConversationId);
            AddStringProperty(_document, nameof(CopilotChatState.ActiveProfileId), state.ActiveProfileId);
            if (_queuedFollowUpRecoveryDocuments != null)
                _document[nameof(CopilotChatState.QueuedFollowUpRecoveries)] = _queuedFollowUpRecoveryDocuments;
        }

        public bool IsComplete =>
            _conversationIndex >= _conversations.Length
            && _queuedFollowUpRecoveryIndex >= _queuedFollowUpRecoveries.Length;

        public bool CaptureNextChunk()
        {
            if (_conversationIndex < _conversations.Length)
            {
                AddObject(_conversationDocuments, _conversations[_conversationIndex++]);
                return true;
            }

            if (_queuedFollowUpRecoveryIndex < _queuedFollowUpRecoveries.Length)
            {
                AddObject(_queuedFollowUpRecoveryDocuments!, _queuedFollowUpRecoveries[_queuedFollowUpRecoveryIndex++]);
                return true;
            }

            return false;
        }

        public CopilotChatStateSnapshot Complete()
        {
            if (!IsComplete)
                throw new InvalidOperationException("Copilot state snapshot capture is incomplete.");

            return new CopilotChatStateSnapshot(_document);
        }

        private void AddObject(JArray target, object? value)
        {
            if (value == null)
            {
                target.Add(JValue.CreateNull());
                return;
            }

            var builder = new StringBuilder();
            using var stringWriter = new StringWriter(builder, CultureInfo.InvariantCulture);
            using var jsonWriter = new JsonTextWriter(stringWriter);
            _serializer.Serialize(jsonWriter, value);
            jsonWriter.Flush();
            target.Add(new JRaw(builder.ToString()));
        }

        private static void AddStringProperty(JObject document, string propertyName, string? value)
        {
            if (value != null)
                document[propertyName] = value;
        }
    }

    public interface ICopilotChatStateStore
    {
        string AttachmentDirectoryPath { get; }

        CopilotChatState Load();

        void Save(CopilotChatState state);

        CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState state);

        string Serialize(CopilotChatStateSnapshot snapshot);

        string Serialize(CopilotChatState state);

        Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default);

        int CleanupOrphanedAttachments(CopilotChatState state);
    }

    public interface IIncrementalCopilotChatStateStore : ICopilotChatStateStore
    {
        CopilotChatStateSnapshotCapture BeginSnapshot(CopilotChatState state);
    }

    public sealed class CopilotChatStateStore : IIncrementalCopilotChatStateStore
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

        public int CleanupOrphanedAttachments(CopilotChatState state)
        {
            ArgumentNullException.ThrowIfNull(state);
            EnsureDirectory();

            var attachmentRoot = Path.GetFullPath(AttachmentDirectoryPath);
            var referencedPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var attachment in (state.Conversations ?? new System.Collections.ObjectModel.ObservableCollection<CopilotConversationRecord>())
                .Where(conversation => conversation != null)
                .SelectMany(conversation => conversation.EnumerateReferencedAttachments()))
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

        private static StateFileReadStatus ReadStateFile(
            string filePath,
            out CopilotChatState state,
            out int schemaVersion)
        {
            state = new CopilotChatState();
            schemaVersion = 0;
            if (!File.Exists(filePath))
                return StateFileReadStatus.Missing;

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
                    return StateFileReadStatus.Invalid;

                using var textReader = new StreamReader(
                    stream,
                    Encoding.UTF8,
                    detectEncodingFromByteOrderMarks: true,
                    bufferSize: 8192,
                    leaveOpen: false);
                using var jsonReader = new JsonTextReader(textReader) { CloseInput = false };
                if (JToken.Load(jsonReader) is not JObject document || jsonReader.Read())
                    return StateFileReadStatus.Invalid;

                var schemaToken = document.GetValue(nameof(CopilotChatState.SchemaVersion), StringComparison.OrdinalIgnoreCase);
                if (schemaToken != null)
                {
                    if (schemaToken.Type != JTokenType.Integer)
                        return StateFileReadStatus.Invalid;

                    schemaVersion = schemaToken.Value<int>();
                    if (schemaVersion > CopilotChatState.CurrentSchemaVersion)
                        return StateFileReadStatus.FutureVersion;
                    if (schemaVersion < 1)
                        return StateFileReadStatus.Invalid;
                }

                if (!HasTrustedDocumentShape(document))
                    return StateFileReadStatus.Invalid;

                var deserializedState = document.ToObject<CopilotChatState>(JsonSerializer.Create(SerializerSettings));
                if (deserializedState == null)
                    return StateFileReadStatus.Invalid;

                state = deserializedState;
                state.SchemaVersion = CopilotChatState.CurrentSchemaVersion;
                return StateFileReadStatus.Valid;
            }
            catch
            {
                return StateFileReadStatus.Invalid;
            }
        }

        private static void ValidateSerializedStateSize(string serializedState)
        {
            var actualBytes = Encoding.UTF8.GetByteCount(serializedState);
            if (actualBytes <= MaximumStateFileBytes)
                return;

            throw new CopilotChatStateSizeLimitException(actualBytes, MaximumStateFileBytes);
        }

        private static bool HasTrustedDocumentShape(JObject document)
        {
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
                    || !IsOptionalString(conversation.GetValue(nameof(CopilotConversationRecord.DraftText), StringComparison.OrdinalIgnoreCase))
                    || !IsOptionalObject(conversation.GetValue(nameof(CopilotConversationRecord.BranchOrigin), StringComparison.OrdinalIgnoreCase))
                    || messages.Any(item => item is not JObject)
                    || attachments.Any(item => item is not JObject))
                {
                    return false;
                }
            }

            var recoveryToken = document.GetValue(nameof(CopilotChatState.QueuedFollowUpRecoveries), StringComparison.OrdinalIgnoreCase);
            if (recoveryToken != null && recoveryToken.Type != JTokenType.Null)
            {
                if (recoveryToken is not JArray recoveries)
                    return false;

                foreach (var recoveryTokenItem in recoveries)
                {
                    if (recoveryTokenItem is not JObject recovery
                        || !IsOptionalString(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.RunId), StringComparison.OrdinalIgnoreCase))
                        || !IsOptionalString(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.ConversationId), StringComparison.OrdinalIgnoreCase))
                        || !IsOptionalString(recovery.GetValue(nameof(CopilotQueuedFollowUpRecoveryRecord.Prompt), StringComparison.OrdinalIgnoreCase)))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static bool IsStringOrNull(JToken? token) => token?.Type is JTokenType.String or JTokenType.Null;

        private static bool IsOptionalString(JToken? token) => token == null || IsStringOrNull(token);

        private static bool IsOptionalObject(JToken? token) => token == null || token.Type == JTokenType.Null || token is JObject;

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
