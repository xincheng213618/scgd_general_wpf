using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed record CopilotToolOutputArchiveSnapshot(
        string Id,
        string ConversationId,
        string ToolName,
        string CallId,
        DateTimeOffset CreatedAtUtc,
        long ObservedCharacters,
        int ArchivedCharacters,
        bool ArchiveTruncated);

    internal sealed record CopilotToolOutputArchiveReadResult(
        CopilotToolOutputArchiveSnapshot? Snapshot,
        CopilotRedactedOutputArchivePage? Page,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && Page?.Available == true
            && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed class CopilotToolOutputArchiveRegistry : IDisposable
    {
        public const int MaximumRetainedArchives = 24;

        private readonly object _syncRoot = new();
        private readonly List<Entry> _entries = [];
        private bool _disposed;

        public static CopilotToolOutputArchiveRegistry Shared { get; } = new();

        public CopilotToolOutputArchiveSnapshot? Retain(
            string? conversationId,
            string? toolName,
            string? callId,
            string? content)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedToolName = NormalizeLabel(toolName, 120);
            var normalizedCallId = NormalizeLabel(callId, 128);
            var observed = content ?? string.Empty;
            if (normalizedConversationId.Length == 0
                || normalizedToolName.Length == 0
                || normalizedCallId.Length == 0
                || observed.Length == 0)
            {
                return null;
            }

            var archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
                "ToolOutput",
                "content");
            if (archive == null)
                return null;

            archive.Append(observed);
            archive.Complete();
            if (!archive.Available || archive.ArchivedCharacters == 0)
            {
                archive.Dispose();
                return null;
            }

            var snapshot = new CopilotToolOutputArchiveSnapshot(
                "tool:" + Guid.NewGuid().ToString("N"),
                normalizedConversationId,
                normalizedToolName,
                normalizedCallId,
                DateTimeOffset.UtcNow,
                archive.ObservedCharacters,
                archive.ArchivedCharacters,
                archive.IsTruncated);
            var removed = new List<Entry>();
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    archive.Dispose();
                    return null;
                }

                _entries.Add(new Entry(snapshot, archive));
                while (_entries.Count > MaximumRetainedArchives)
                {
                    removed.Add(_entries[0]);
                    _entries.RemoveAt(0);
                }
            }

            foreach (var entry in removed)
                entry.Dispose();
            return snapshot;
        }

        public IReadOnlyList<CopilotToolOutputArchiveSnapshot> GetSnapshots(
            string? conversationId)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            if (normalizedConversationId.Length == 0)
                return Array.Empty<CopilotToolOutputArchiveSnapshot>();

            lock (_syncRoot)
            {
                return _entries
                    .Where(entry => string.Equals(
                        entry.Snapshot.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .OrderByDescending(entry => entry.Snapshot.CreatedAtUtc)
                    .Select(entry => entry.Snapshot)
                    .ToArray();
            }
        }

        public CopilotToolOutputArchiveReadResult Read(
            string? conversationId,
            string? archiveId,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedArchiveId = NormalizeScopeId(archiveId);
            if (normalizedConversationId.Length == 0
                || normalizedArchiveId.Length == 0)
            {
                return Failure(
                    CopilotToolFailureKind.Validation,
                    "conversationId and archiveId are required.");
            }
            if (offsetCharacters < 0)
            {
                return Failure(
                    CopilotToolFailureKind.Validation,
                    "offsetCharacters cannot be negative.");
            }
            if (maximumCharacters is < 1
                or > CopilotOutputArchiveLimits.MaximumReadCharacters)
            {
                return Failure(
                    CopilotToolFailureKind.Validation,
                    $"maximumCharacters must be an integer from 1 through {CopilotOutputArchiveLimits.MaximumReadCharacters}.");
            }

            Entry? entry;
            lock (_syncRoot)
            {
                entry = _entries.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.Snapshot.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Snapshot.Id,
                        normalizedArchiveId,
                        StringComparison.Ordinal));
            }
            if (entry == null)
            {
                return Failure(
                    CopilotToolFailureKind.NotFound,
                    "The tool output archive was not found in the current conversation.");
            }

            var page = entry.Archive.Read(
                offsetCharacters,
                maximumCharacters,
                cancellationToken);
            return new CopilotToolOutputArchiveReadResult(
                entry.Snapshot,
                page,
                page.Available
                    ? CopilotToolFailureKind.None
                    : CopilotToolFailureKind.Transient,
                page.ErrorMessage);
        }

        public bool Remove(string? archiveId)
        {
            var normalizedArchiveId = NormalizeScopeId(archiveId);
            if (normalizedArchiveId.Length == 0)
                return false;

            Entry? removed;
            lock (_syncRoot)
            {
                removed = _entries.SingleOrDefault(entry => string.Equals(
                    entry.Snapshot.Id,
                    normalizedArchiveId,
                    StringComparison.Ordinal));
                if (removed != null)
                    _entries.Remove(removed);
            }
            removed?.Dispose();
            return removed != null;
        }

        public int ClearConversation(string? conversationId)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            if (normalizedConversationId.Length == 0)
                return 0;

            Entry[] removed;
            lock (_syncRoot)
            {
                removed = _entries
                    .Where(entry => string.Equals(
                        entry.Snapshot.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .ToArray();
                _entries.RemoveAll(entry => string.Equals(
                    entry.Snapshot.ConversationId,
                    normalizedConversationId,
                    StringComparison.Ordinal));
            }

            foreach (var entry in removed)
                entry.Dispose();
            return removed.Length;
        }

        public void Dispose()
        {
            Entry[] entries;
            lock (_syncRoot)
            {
                if (_disposed)
                    return;

                _disposed = true;
                entries = _entries.ToArray();
                _entries.Clear();
            }

            foreach (var entry in entries)
                entry.Dispose();
        }

        private static string NormalizeScopeId(string? value) =>
            (value ?? string.Empty).Trim();

        private static string NormalizeLabel(string? value, int maximumCharacters) =>
            new((value ?? string.Empty)
                .Trim()
                .Where(character => !char.IsControl(character))
                .Take(maximumCharacters)
                .ToArray());

        private static CopilotToolOutputArchiveReadResult Failure(
            CopilotToolFailureKind failureKind,
            string errorMessage) =>
            new(null, null, failureKind, errorMessage);

        private sealed record Entry(
            CopilotToolOutputArchiveSnapshot Snapshot,
            CopilotTemporaryRedactedOutputArchive Archive) : IDisposable
        {
            public void Dispose() => Archive.Dispose();
        }
    }
}
