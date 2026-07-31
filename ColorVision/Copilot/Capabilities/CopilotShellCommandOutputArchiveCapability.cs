using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace ColorVision.Copilot
{
    internal enum CopilotShellCommandOutputStream
    {
        StandardOutput,
        StandardError,
    }

    internal sealed record CopilotShellCommandOutputArchiveSnapshot(
        string Id,
        string ConversationId,
        DateTimeOffset CreatedAtUtc,
        long ObservedStandardOutputCharacters,
        long ObservedStandardErrorCharacters,
        bool StandardOutputPreviewTruncated,
        bool StandardErrorPreviewTruncated,
        bool StandardOutputArchiveAvailable,
        bool StandardErrorArchiveAvailable,
        int ArchivedStandardOutputCharacters,
        int ArchivedStandardErrorCharacters,
        bool StandardOutputArchiveTruncated,
        bool StandardErrorArchiveTruncated);

    internal sealed record CopilotShellCommandOutputArchiveReadResult(
        CopilotShellCommandOutputArchiveSnapshot? Snapshot,
        CopilotShellCommandOutputStream Stream,
        CopilotRedactedOutputArchivePage? Page,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && Page?.Available == true
            && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed class CopilotShellCommandOutputCapture : IDisposable
    {
        private readonly CopilotTemporaryRedactedOutputArchive? _standardOutput =
            CopilotTemporaryRedactedOutputArchive.TryCreate(
                "ShellOutput",
                "stdout");
        private readonly CopilotTemporaryRedactedOutputArchive? _standardError =
            CopilotTemporaryRedactedOutputArchive.TryCreate(
                "ShellOutput",
                "stderr");
        private long _observedStandardOutputCharacters;
        private long _observedStandardErrorCharacters;
        private int _disposed;

        public bool HasAvailableArchive =>
            _standardOutput?.Available == true
            || _standardError?.Available == true;

        public void AppendStandardOutput(string? value)
        {
            var observed = value ?? string.Empty;
            if (observed.Length == 0 || Volatile.Read(ref _disposed) == 1)
                return;

            AddObserved(
                ref _observedStandardOutputCharacters,
                observed.Length);
            _standardOutput?.Append(observed);
        }

        public void AppendStandardError(string? value)
        {
            var observed = value ?? string.Empty;
            if (observed.Length == 0 || Volatile.Read(ref _disposed) == 1)
                return;

            AddObserved(
                ref _observedStandardErrorCharacters,
                observed.Length);
            _standardError?.Append(observed);
        }

        public void EnsureCaptured(
            string standardOutput,
            string standardError)
        {
            if (Volatile.Read(
                    ref _observedStandardOutputCharacters) == 0)
            {
                AppendStandardOutput(standardOutput);
            }
            if (Volatile.Read(
                    ref _observedStandardErrorCharacters) == 0)
            {
                AppendStandardError(standardError);
            }
        }

        public void Complete()
        {
            _standardOutput?.Complete();
            _standardError?.Complete();
        }

        public CopilotShellCommandOutputArchiveSnapshot CreateSnapshot(
            string id,
            string conversationId,
            DateTimeOffset createdAtUtc,
            CopilotShellProcessResult processResult)
        {
            ArgumentNullException.ThrowIfNull(processResult);
            return new CopilotShellCommandOutputArchiveSnapshot(
                id,
                conversationId,
                createdAtUtc,
                processResult.ObservedStandardOutputCharacters,
                processResult.ObservedStandardErrorCharacters,
                processResult.StandardOutputTruncated,
                processResult.StandardErrorTruncated,
                _standardOutput?.Available == true,
                _standardError?.Available == true,
                _standardOutput?.ArchivedCharacters ?? 0,
                _standardError?.ArchivedCharacters ?? 0,
                _standardOutput?.IsTruncated == true,
                _standardError?.IsTruncated == true);
        }

        public CopilotRedactedOutputArchivePage Read(
            CopilotShellCommandOutputStream stream,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken) =>
            (stream == CopilotShellCommandOutputStream.StandardError
                ? _standardError
                : _standardOutput)?.Read(
                    offsetCharacters,
                    maximumCharacters,
                    cancellationToken)
            ?? new CopilotRedactedOutputArchivePage(
                Available: false,
                Content: string.Empty,
                OffsetCharacters: offsetCharacters,
                ReturnedCharacters: 0,
                NextOffsetCharacters: offsetCharacters,
                ArchivedCharacters: 0,
                EndOfAvailableOutput: true,
                ArchiveTruncated: false,
                ErrorMessage:
                    "The temporary redacted shell output archive is unavailable.");

        private static void AddObserved(ref long target, int increment)
        {
            while (true)
            {
                var current = Volatile.Read(ref target);
                var updated = current > long.MaxValue - increment
                    ? long.MaxValue
                    : current + increment;
                if (Interlocked.CompareExchange(
                        ref target,
                        updated,
                        current) == current)
                {
                    return;
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            _standardOutput?.Dispose();
            _standardError?.Dispose();
        }
    }

    internal sealed class CopilotShellCommandOutputArchiveRegistry : IDisposable
    {
        public const int MaximumRetainedArchives = 24;

        private readonly object _syncRoot = new();
        private readonly List<Entry> _entries = new();
        private bool _disposed;

        public static CopilotShellCommandOutputArchiveRegistry Shared { get; } =
            new();

        public CopilotShellCommandOutputArchiveSnapshot? Retain(
            string? conversationId,
            CopilotShellCommandOutputCapture capture,
            CopilotShellProcessResult processResult)
        {
            ArgumentNullException.ThrowIfNull(capture);
            ArgumentNullException.ThrowIfNull(processResult);
            var normalizedConversationId =
                NormalizeScopeId(conversationId);
            if (normalizedConversationId.Length == 0
                || (!processResult.StandardOutputTruncated
                    && !processResult.StandardErrorTruncated)
                || !capture.HasAvailableArchive)
            {
                capture.Dispose();
                return null;
            }

            var id = "shell:" + Guid.NewGuid().ToString("N");
            var createdAtUtc = DateTimeOffset.UtcNow;
            var snapshot = capture.CreateSnapshot(
                id,
                normalizedConversationId,
                createdAtUtc,
                processResult);
            var removed = new List<Entry>();
            lock (_syncRoot)
            {
                if (_disposed)
                {
                    capture.Dispose();
                    return null;
                }

                _entries.Add(new Entry(snapshot, capture));
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

        public IReadOnlyList<CopilotShellCommandOutputArchiveSnapshot>
            GetSnapshots(string? conversationId)
        {
            var normalizedConversationId =
                NormalizeScopeId(conversationId);
            if (normalizedConversationId.Length == 0)
            {
                return Array.Empty<
                    CopilotShellCommandOutputArchiveSnapshot>();
            }

            lock (_syncRoot)
            {
                return _entries
                    .Where(entry => string.Equals(
                        entry.Snapshot.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .OrderByDescending(entry =>
                        entry.Snapshot.CreatedAtUtc)
                    .Select(entry => entry.Snapshot)
                    .ToArray();
            }
        }

        public CopilotShellCommandOutputArchiveReadResult Read(
            string? conversationId,
            string? archiveId,
            CopilotShellCommandOutputStream stream,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedConversationId =
                NormalizeScopeId(conversationId);
            var normalizedArchiveId = (archiveId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0
                || normalizedArchiveId.Length == 0)
            {
                return Failure(
                    stream,
                    CopilotToolFailureKind.Validation,
                    "conversationId and archiveId are required.");
            }
            if (offsetCharacters < 0)
            {
                return Failure(
                    stream,
                    CopilotToolFailureKind.Validation,
                    "offsetCharacters cannot be negative.");
            }
            if (maximumCharacters is < 1
                or > CopilotOutputArchiveLimits.MaximumReadCharacters)
            {
                return Failure(
                    stream,
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
                    stream,
                    CopilotToolFailureKind.NotFound,
                    "The shell output archive was not found in the current conversation.");
            }

            var page = entry.Capture.Read(
                stream,
                offsetCharacters,
                maximumCharacters,
                cancellationToken);
            return new CopilotShellCommandOutputArchiveReadResult(
                entry.Snapshot,
                stream,
                page,
                page.Available
                    ? CopilotToolFailureKind.None
                    : CopilotToolFailureKind.Transient,
                page.ErrorMessage);
        }

        public int ClearConversation(string? conversationId)
        {
            var normalizedConversationId =
                NormalizeScopeId(conversationId);
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

        internal static bool TryReadArchiveId(
            CopilotAgentToolInput input,
            out string archiveId)
        {
            archiveId = string.Empty;
            if (input?.Arguments.TryGetValue(
                    "archiveId",
                    out var raw) != true
                || raw == null)
            {
                return false;
            }
            if (raw is string text)
            {
                archiveId = text.Trim();
                return archiveId.Length > 0;
            }
            if (raw is JsonElement element
                && element.ValueKind == JsonValueKind.String)
            {
                archiveId = (element.GetString() ?? string.Empty)
                    .Trim();
                return archiveId.Length > 0;
            }
            return false;
        }

        private static string NormalizeScopeId(string? value) =>
            (value ?? string.Empty).Trim();

        private static CopilotShellCommandOutputArchiveReadResult Failure(
            CopilotShellCommandOutputStream stream,
            CopilotToolFailureKind failureKind,
            string errorMessage) =>
            new(
                null,
                stream,
                null,
                failureKind,
                errorMessage);

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

        private sealed record Entry(
            CopilotShellCommandOutputArchiveSnapshot Snapshot,
            CopilotShellCommandOutputCapture Capture) : IDisposable
        {
            public void Dispose() => Capture.Dispose();
        }
    }
}
