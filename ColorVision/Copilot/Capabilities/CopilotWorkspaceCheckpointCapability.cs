using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotWorkspacePatchStore
    {
        private const int WorkspaceCheckpointSchemaVersion = 1;
        private readonly CopilotWorkspaceChangeSetCheckpointStore? _checkpointStore;
        private bool _checkpointsLoaded;

        internal CopilotWorkspacePatchStore()
        {
        }

        internal CopilotWorkspacePatchStore(CopilotWorkspaceChangeSetCheckpointStore checkpointStore)
        {
            _checkpointStore = checkpointStore ?? throw new ArgumentNullException(nameof(checkpointStore));
        }

        private void EnsureCheckpointRecordsLoaded()
        {
            if (_checkpointStore == null || _checkpointsLoaded)
                return;

            lock (_syncRoot)
            {
                if (_checkpointsLoaded)
                    return;

                var now = DateTimeOffset.UtcNow;
                IReadOnlyList<CopilotWorkspaceChangeSetCheckpointBlob> blobs;
                try
                {
                    blobs = _checkpointStore.Load();
                }
                catch (Exception exception) when (exception is IOException
                    or UnauthorizedAccessException
                    or CryptographicException)
                {
                    _checkpointsLoaded = true;
                    return;
                }

                foreach (var blob in blobs)
                {
                    try
                    {
                        var payload = JsonSerializer.Deserialize<WorkspaceChangeSetCheckpoint>(blob.Payload);
                        if (payload == null
                            || !string.Equals(payload.ChangeSetId, blob.ChangeSetId, StringComparison.Ordinal)
                            || !TryRestoreCheckpoint(payload, now))
                        {
                            _checkpointStore.Delete(blob.ChangeSetId);
                        }
                    }
                    catch (Exception exception) when (exception is JsonException or NotSupportedException)
                    {
                        _checkpointStore.Delete(blob.ChangeSetId);
                    }
                }

                RemoveExpiredEntries(now);
                _checkpointsLoaded = true;
            }
        }

        private bool TryRestoreCheckpoint(WorkspaceChangeSetCheckpoint checkpoint, DateTimeOffset now)
        {
            if (checkpoint.SchemaVersion != WorkspaceCheckpointSchemaVersion
                || !TryGetChangeSetSuffix(checkpoint.ChangeSetId, out _)
                || checkpoint.CreatedAtUtc > checkpoint.ExpiresAtUtc
                || checkpoint.CreatedAtUtc > now.AddMinutes(5)
                || checkpoint.ExpiresAtUtc <= now
                || string.IsNullOrWhiteSpace(checkpoint.ConversationId)
                || !TryNormalizeWorkspaceIdentity(checkpoint.WorkspacePath, out var workspacePath)
                || checkpoint.Records is not { Length: >= 1 and <= MaxChangeSetFiles }
                || _changeSets.Count >= MaxChangeSets
                || _records.Count + checkpoint.Records.Length > MaxEntries
                || _changeSets.ContainsKey(checkpoint.ChangeSetId))
            {
                return false;
            }

            var restoredRecords = new List<WorkspacePatchRecord>(checkpoint.Records.Length);
            var previewIds = new HashSet<string>(StringComparer.Ordinal);
            var fullPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var record in checkpoint.Records)
            {
                if (!TryRestoreCheckpointRecord(
                        checkpoint.ChangeSetId,
                        checkpoint.ExpiresAtUtc,
                        record,
                        previewIds,
                        fullPaths,
                        out var restoredRecord))
                {
                    return false;
                }
                restoredRecords.Add(restoredRecord);
            }
            if (restoredRecords.Any(record => _records.ContainsKey(record.PreviewId)))
                return false;

            foreach (var record in restoredRecords)
                _records.Add(record.PreviewId, record);
            _changeSets.Add(checkpoint.ChangeSetId, new WorkspaceChangeSetRecord
            {
                ChangeSetId = checkpoint.ChangeSetId,
                PreviewIds = restoredRecords.Select(record => record.PreviewId).ToArray(),
                ConversationId = checkpoint.ConversationId.Trim(),
                WorkspacePath = workspacePath,
                CreatedAtUtc = checkpoint.CreatedAtUtc,
                ExpiresAtUtc = checkpoint.ExpiresAtUtc,
                State = WorkspaceChangeSetState.Applied,
            });
            return true;
        }

        private static bool TryRestoreCheckpointRecord(
            string changeSetId,
            DateTimeOffset changeSetExpiry,
            WorkspacePatchCheckpoint record,
            HashSet<string> previewIds,
            HashSet<string> fullPaths,
            out WorkspacePatchRecord restoredRecord)
        {
            restoredRecord = null!;
            if (record == null
                || !Enum.IsDefined(record.Operation)
                || !TryNormalizeAbsolutePath(record.FullPath, out var fullPath)
                || !previewIds.Add(record.PreviewId)
                || !fullPaths.Add(fullPath)
                || record.CreatedAtUtc > record.ExpiresAtUtc
                || record.ExpiresAtUtc < changeSetExpiry
                || record.OriginalBytes == null
                || record.PatchedBytes == null
                || record.OriginalBytes.Length > MaxFileBytes
                || record.PatchedBytes.Length > MaxFileBytes
                || record.Replacements == null
                || record.CreatedDirectories == null
                || !AreCreatedDirectoriesSafe(fullPath, record.CreatedDirectories))
            {
                return false;
            }

            var expectedPreviewPrefix = record.Operation switch
            {
                WorkspacePatchOperation.Replace => "workspace-patch:",
                WorkspacePatchOperation.Create => "workspace-create:",
                WorkspacePatchOperation.Delete => "workspace-delete:",
                _ => string.Empty,
            };
            if (!TryGetIdentifierSuffix(record.PreviewId, expectedPreviewPrefix, out _))
                return false;

            var replacements = new WorkspaceTextReplacement[record.Replacements.Length];
            for (var index = 0; index < replacements.Length; index++)
            {
                var replacement = record.Replacements[index];
                if (replacement == null
                    || replacement.OldText == null
                    || replacement.NewText == null
                    || replacement.OldText.Length == 0
                    || replacement.OldText.Length > MaxReplacementCharacters
                    || replacement.NewText.Length > MaxReplacementCharacters)
                {
                    return false;
                }
                replacements[index] = new WorkspaceTextReplacement(replacement.OldText, replacement.NewText);
            }
            if (replacements.Sum(replacement => (long)replacement.OldText.Length + replacement.NewText.Length) > MaxTotalReplacementCharacters)
                return false;

            var hashesAreValid = record.Operation switch
            {
                WorkspacePatchOperation.Replace =>
                    replacements.Length is >= 1 and <= MaxReplacementsPerFile
                    && string.Equals(record.BeforeSha256, Hash(record.OriginalBytes), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(record.AfterSha256, Hash(record.PatchedBytes), StringComparison.OrdinalIgnoreCase),
                WorkspacePatchOperation.Create =>
                    replacements.Length == 0
                    && record.OriginalBytes.Length == 0
                    && string.Equals(record.BeforeSha256, "missing", StringComparison.Ordinal)
                    && string.Equals(record.AfterSha256, Hash(record.PatchedBytes), StringComparison.OrdinalIgnoreCase),
                WorkspacePatchOperation.Delete =>
                    replacements.Length == 0
                    && record.PatchedBytes.Length == 0
                    && string.Equals(record.BeforeSha256, Hash(record.OriginalBytes), StringComparison.OrdinalIgnoreCase)
                    && string.Equals(record.AfterSha256, "missing", StringComparison.Ordinal),
                _ => false,
            };
            if (!hashesAreValid)
                return false;

            restoredRecord = new WorkspacePatchRecord
            {
                PreviewId = record.PreviewId,
                Operation = record.Operation,
                FullPath = fullPath,
                OriginalBytes = record.OriginalBytes,
                PatchedBytes = record.PatchedBytes,
                BeforeSha256 = record.BeforeSha256,
                AfterSha256 = record.AfterSha256,
                OldText = record.OldText ?? string.Empty,
                NewText = record.NewText ?? string.Empty,
                Replacements = replacements,
                CreatedAtUtc = record.CreatedAtUtc,
                ExpiresAtUtc = record.ExpiresAtUtc,
                CreatedDirectories = record.CreatedDirectories,
                ChangeSetId = changeSetId,
                State = WorkspacePatchState.Applied,
            };
            return true;
        }

        private void PersistCheckpoint(
            WorkspaceChangeSetRecord changeSet,
            IEnumerable<WorkspacePatchRecord> records)
        {
            if (_checkpointStore == null)
                return;

            try
            {
                var checkpoint = new WorkspaceChangeSetCheckpoint
                {
                    SchemaVersion = WorkspaceCheckpointSchemaVersion,
                    ChangeSetId = changeSet.ChangeSetId,
                    ConversationId = changeSet.ConversationId,
                    WorkspacePath = changeSet.WorkspacePath,
                    CreatedAtUtc = changeSet.CreatedAtUtc,
                    ExpiresAtUtc = changeSet.ExpiresAtUtc,
                    Records = records.Select(record => new WorkspacePatchCheckpoint
                    {
                        PreviewId = record.PreviewId,
                        Operation = record.Operation,
                        FullPath = record.FullPath,
                        OriginalBytes = record.OriginalBytes,
                        PatchedBytes = record.PatchedBytes,
                        BeforeSha256 = record.BeforeSha256,
                        AfterSha256 = record.AfterSha256,
                        OldText = record.OldText,
                        NewText = record.NewText,
                        Replacements = record.Replacements.Select(replacement => new WorkspaceTextReplacementCheckpoint
                        {
                            OldText = replacement.OldText,
                            NewText = replacement.NewText,
                        }).ToArray(),
                        CreatedAtUtc = record.CreatedAtUtc,
                        ExpiresAtUtc = record.ExpiresAtUtc,
                        CreatedDirectories = record.CreatedDirectories,
                    }).ToArray(),
                };
                _checkpointStore.Save(
                    changeSet.ChangeSetId,
                    JsonSerializer.SerializeToUtf8Bytes(checkpoint));
            }
            catch (Exception exception) when (exception is IOException
                or UnauthorizedAccessException
                or CryptographicException
                or JsonException
                or ArgumentException)
            {
                // Checkpoint persistence is a safety net and must not turn a completed workspace write into a failure.
            }
        }

        private void DeleteCheckpoint(string changeSetId)
        {
            try
            {
                _checkpointStore?.Delete(changeSetId);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ArgumentException)
            {
            }
        }

        private static bool MatchesCheckpointBinding(
            CopilotAgentRequest request,
            WorkspaceChangeSetRecord changeSet)
        {
            return string.Equals(
                    request.ConversationId?.Trim(),
                    changeSet.ConversationId,
                    StringComparison.Ordinal)
                && TryNormalizeWorkspaceIdentity(request.WorkspacePath, out var workspacePath)
                && string.Equals(workspacePath, changeSet.WorkspacePath, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TryNormalizeWorkspaceIdentity(string? path, out string normalizedPath)
        {
            normalizedPath = string.Empty;
            if (!TryNormalizeAbsolutePath(path, out var fullPath)
                || !Directory.Exists(fullPath))
            {
                return false;
            }

            normalizedPath = Path.TrimEndingDirectorySeparator(fullPath);
            return true;
        }

        private static bool TryNormalizeAbsolutePath(string? path, out string fullPath)
        {
            fullPath = string.Empty;
            if (string.IsNullOrWhiteSpace(path))
                return false;
            try
            {
                fullPath = Path.GetFullPath(path);
                return Path.IsPathFullyQualified(fullPath);
            }
            catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
            {
                return false;
            }
        }

        private static bool AreCreatedDirectoriesSafe(string fullPath, string[] createdDirectories)
        {
            if (createdDirectories.Length > 64)
                return false;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var directory in createdDirectories)
            {
                if (!TryNormalizeAbsolutePath(directory, out var fullDirectory)
                    || !seen.Add(fullDirectory)
                    || !fullPath.StartsWith(
                        Path.TrimEndingDirectorySeparator(fullDirectory) + Path.DirectorySeparatorChar,
                        StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }
            return true;
        }

        private static bool TryGetChangeSetSuffix(string changeSetId, out string suffix) =>
            TryGetIdentifierSuffix(changeSetId, "workspace-change-set:", out suffix);

        private static bool TryGetIdentifierSuffix(string value, string prefix, out string suffix)
        {
            suffix = string.Empty;
            if (string.IsNullOrWhiteSpace(value)
                || string.IsNullOrEmpty(prefix)
                || !value.StartsWith(prefix, StringComparison.Ordinal)
                || value.Length != prefix.Length + 32)
            {
                return false;
            }

            suffix = value[prefix.Length..];
            return Guid.TryParseExact(suffix, "N", out _);
        }

        private sealed class WorkspaceChangeSetCheckpoint
        {
            public int SchemaVersion { get; init; }
            public string ChangeSetId { get; init; } = string.Empty;
            public string ConversationId { get; init; } = string.Empty;
            public string WorkspacePath { get; init; } = string.Empty;
            public DateTimeOffset CreatedAtUtc { get; init; }
            public DateTimeOffset ExpiresAtUtc { get; init; }
            public WorkspacePatchCheckpoint[] Records { get; init; } = Array.Empty<WorkspacePatchCheckpoint>();
        }

        private sealed class WorkspacePatchCheckpoint
        {
            public string PreviewId { get; init; } = string.Empty;
            public WorkspacePatchOperation Operation { get; init; }
            public string FullPath { get; init; } = string.Empty;
            public byte[] OriginalBytes { get; init; } = Array.Empty<byte>();
            public byte[] PatchedBytes { get; init; } = Array.Empty<byte>();
            public string BeforeSha256 { get; init; } = string.Empty;
            public string AfterSha256 { get; init; } = string.Empty;
            public string? OldText { get; init; }
            public string? NewText { get; init; }
            public WorkspaceTextReplacementCheckpoint[] Replacements { get; init; } = Array.Empty<WorkspaceTextReplacementCheckpoint>();
            public DateTimeOffset CreatedAtUtc { get; init; }
            public DateTimeOffset ExpiresAtUtc { get; init; }
            public string[] CreatedDirectories { get; init; } = Array.Empty<string>();
        }

        private sealed class WorkspaceTextReplacementCheckpoint
        {
            public string OldText { get; init; } = string.Empty;
            public string NewText { get; init; } = string.Empty;
        }
    }

    internal sealed class CopilotWorkspaceChangeSetCheckpointStore
    {
        private const int MaximumPayloadBytes = 32 * 1024 * 1024;
        private const int MaximumProtectedBytes = MaximumPayloadBytes + 64 * 1024;
        private const string FileExtension = ".checkpoint";
        private static readonly byte[] OptionalEntropy =
            Encoding.UTF8.GetBytes("ColorVision.Copilot.WorkspaceChangeSet.v1");
        private readonly object _syncRoot = new();

        public CopilotWorkspaceChangeSetCheckpointStore(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath))
                throw new ArgumentException("A checkpoint directory is required.", nameof(directoryPath));
            DirectoryPath = Path.GetFullPath(directoryPath);
        }

        public string DirectoryPath { get; }

        public static CopilotWorkspaceChangeSetCheckpointStore CreateDefault() =>
            new(Path.Combine(Environments.DirLocalAppData, "Copilot", "WorkspaceCheckpoints"));

        public IReadOnlyList<CopilotWorkspaceChangeSetCheckpointBlob> Load()
        {
            lock (_syncRoot)
            {
                Directory.CreateDirectory(DirectoryPath);
                var checkpoints = new List<CopilotWorkspaceChangeSetCheckpointBlob>();
                foreach (var filePath in Directory.EnumerateFiles(DirectoryPath, "*" + FileExtension, SearchOption.TopDirectoryOnly))
                {
                    var suffix = Path.GetFileNameWithoutExtension(filePath);
                    var changeSetId = "workspace-change-set:" + suffix;
                    if (!Guid.TryParseExact(suffix, "N", out _))
                    {
                        TryDelete(filePath);
                        continue;
                    }

                    try
                    {
                        var fileInfo = new FileInfo(filePath);
                        if (fileInfo.Length is <= 0 or > MaximumProtectedBytes)
                        {
                            TryDelete(filePath);
                            continue;
                        }

                        var protectedBytes = File.ReadAllBytes(filePath);
                        var payload = ProtectedData.Unprotect(
                            protectedBytes,
                            OptionalEntropy,
                            DataProtectionScope.CurrentUser);
                        if (payload.Length is <= 0 or > MaximumPayloadBytes)
                        {
                            TryDelete(filePath);
                            continue;
                        }
                        checkpoints.Add(new CopilotWorkspaceChangeSetCheckpointBlob(changeSetId, payload));
                    }
                    catch (Exception exception) when (exception is IOException
                        or UnauthorizedAccessException
                        or CryptographicException)
                    {
                        TryDelete(filePath);
                    }
                }
                return checkpoints;
            }
        }

        public void Save(string changeSetId, byte[] payload)
        {
            ArgumentNullException.ThrowIfNull(payload);
            if (!TryGetCheckpointPath(changeSetId, out var checkpointPath)
                || payload.Length is <= 0 or > MaximumPayloadBytes)
            {
                throw new ArgumentException("The workspace checkpoint payload is invalid.", nameof(payload));
            }

            lock (_syncRoot)
            {
                Directory.CreateDirectory(DirectoryPath);
                var protectedBytes = ProtectedData.Protect(
                    payload,
                    OptionalEntropy,
                    DataProtectionScope.CurrentUser);
                if (protectedBytes.Length > MaximumProtectedBytes)
                    throw new ArgumentException("The protected workspace checkpoint exceeds the size limit.", nameof(payload));

                var temporaryPath = Path.Combine(
                    DirectoryPath,
                    $".{Path.GetFileName(checkpointPath)}.{Guid.NewGuid():N}.tmp");
                try
                {
                    using (var stream = new FileStream(
                        temporaryPath,
                        FileMode.CreateNew,
                        FileAccess.Write,
                        FileShare.None,
                        4096,
                        FileOptions.WriteThrough))
                    {
                        stream.Write(protectedBytes);
                        stream.Flush(flushToDisk: true);
                    }
                    File.Move(temporaryPath, checkpointPath, overwrite: true);
                }
                finally
                {
                    TryDelete(temporaryPath);
                }
            }
        }

        public void Delete(string changeSetId)
        {
            if (!TryGetCheckpointPath(changeSetId, out var checkpointPath))
                return;
            lock (_syncRoot)
                TryDelete(checkpointPath);
        }

        private bool TryGetCheckpointPath(string changeSetId, out string checkpointPath)
        {
            checkpointPath = string.Empty;
            const string prefix = "workspace-change-set:";
            if (string.IsNullOrWhiteSpace(changeSetId)
                || !changeSetId.StartsWith(prefix, StringComparison.Ordinal)
                || changeSetId.Length != prefix.Length + 32)
            {
                return false;
            }

            var suffix = changeSetId[prefix.Length..];
            if (!Guid.TryParseExact(suffix, "N", out _))
                return false;
            checkpointPath = Path.Combine(DirectoryPath, suffix + FileExtension);
            return true;
        }

        private static void TryDelete(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                    File.Delete(filePath);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
            }
        }
    }

    internal sealed record CopilotWorkspaceChangeSetCheckpointBlob(
        string ChangeSetId,
        byte[] Payload);
}
