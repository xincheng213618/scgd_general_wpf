using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal enum CopilotBackgroundShellOutputMonitorState
    {
        Running,
        Completed,
        Stopped,
        Expired,
        ArchiveUnavailable,
        ArchiveTruncated,
        Overloaded,
    }

    internal sealed record CopilotBackgroundShellOutputMonitorSnapshot(
        string Id,
        string ConversationId,
        string BackgroundId,
        CopilotBackgroundShellOutputStream Stream,
        string Description,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset ExpiresAtUtc,
        CopilotBackgroundShellOutputMonitorState State,
        int PublishedEvents,
        int SuppressedEvents)
    {
        public bool IsActive =>
            State == CopilotBackgroundShellOutputMonitorState.Running;
    }

    internal sealed record CopilotBackgroundShellOutputMonitorStartResult(
        CopilotBackgroundShellOutputMonitorSnapshot? Snapshot,
        bool AlreadyRunning,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed record CopilotBackgroundShellOutputMonitorStopResult(
        CopilotBackgroundShellOutputMonitorSnapshot? Snapshot,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed class CopilotBackgroundShellOutputMonitorEventArgs :
        EventArgs
    {
        public CopilotBackgroundShellOutputMonitorEventArgs(
            CopilotBackgroundShellOutputMonitorSnapshot monitor,
            string content,
            int suppressedEvents)
        {
            Monitor = monitor
                ?? throw new ArgumentNullException(nameof(monitor));
            Content = content ?? string.Empty;
            SuppressedEvents = Math.Max(0, suppressedEvents);
        }

        public CopilotBackgroundShellOutputMonitorSnapshot Monitor { get; }

        public string Content { get; }

        public int SuppressedEvents { get; }
    }

    internal sealed partial class CopilotBackgroundShellCommandRegistry
    {
        public const int DefaultOutputMonitorLifetimeSeconds = 600;
        public const int MinimumOutputMonitorLifetimeSeconds = 10;
        public const int MaximumOutputMonitorLifetimeSeconds = 3_600;
        public const int MaximumOutputMonitorDescriptionCharacters = 120;
        public const int MaximumActiveOutputMonitorsPerConversation = 4;
        public const int MaximumActiveOutputMonitors = 8;
        public const int MaximumRetainedOutputMonitors = 24;

        private static readonly TimeSpan OutputMonitorDebounce =
            TimeSpan.FromMilliseconds(200);
        private readonly List<OutputMonitorEntry> _outputMonitors = new();

        public event EventHandler<CopilotBackgroundShellOutputMonitorEventArgs>?
            OutputMonitorEvent;

        public CopilotBackgroundShellOutputMonitorStartResult StartOutputMonitor(
            string? conversationId,
            string? backgroundId,
            CopilotBackgroundShellOutputStream stream,
            string? description,
            int lifetimeSeconds)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedBackgroundId = (backgroundId ?? string.Empty).Trim();
            var normalizedDescription = NormalizeMonitorDescription(description);
            if (normalizedConversationId.Length == 0
                || normalizedBackgroundId.Length == 0)
            {
                return OutputMonitorStartFailure(
                    CopilotToolFailureKind.Validation,
                    "conversationId and backgroundId are required.");
            }
            if (!Enum.IsDefined(stream))
            {
                return OutputMonitorStartFailure(
                    CopilotToolFailureKind.Validation,
                    "stream must be stdout or stderr.");
            }
            if (normalizedDescription.Length == 0)
            {
                return OutputMonitorStartFailure(
                    CopilotToolFailureKind.Validation,
                    "description is required.");
            }
            if (lifetimeSeconds is < MinimumOutputMonitorLifetimeSeconds
                or > MaximumOutputMonitorLifetimeSeconds)
            {
                return OutputMonitorStartFailure(
                    CopilotToolFailureKind.Validation,
                    $"lifetimeSeconds must be an integer from {MinimumOutputMonitorLifetimeSeconds} through {MaximumOutputMonitorLifetimeSeconds}.");
            }

            OutputMonitorEntry monitor;
            lock (_syncRoot)
            {
                if (_isShuttingDown)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.Transient,
                        "The application is shutting down and cannot start an output monitor.");
                }

                RefreshCompletedEntriesUnderLock();
                var entry = _entries.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Id,
                        normalizedBackgroundId,
                        StringComparison.Ordinal));
                var command = entry?.GetSnapshot();
                if (entry == null || command == null)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.NotFound,
                        "The background command was not found in the current conversation.");
                }
                if (!command.IsActive)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.Validation,
                        "Only a running background command can be monitored.");
                }

                var archiveAvailable =
                    stream == CopilotBackgroundShellOutputStream.StandardError
                        ? command.StandardErrorArchiveAvailable
                        : command.StandardOutputArchiveAvailable;
                var archiveTruncated =
                    stream == CopilotBackgroundShellOutputStream.StandardError
                        ? command.StandardErrorArchiveTruncated
                        : command.StandardOutputArchiveTruncated;
                if (!archiveAvailable)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.Transient,
                        "The selected temporary redacted output archive is unavailable.");
                }
                if (archiveTruncated)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.Validation,
                        "The selected output archive is already truncated and cannot provide a safe complete live offset.");
                }

                var existing = _outputMonitors.FirstOrDefault(candidate =>
                    candidate.IsActive
                    && string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.BackgroundId,
                        normalizedBackgroundId,
                        StringComparison.Ordinal)
                    && candidate.Stream == stream);
                if (existing != null)
                {
                    return new CopilotBackgroundShellOutputMonitorStartResult(
                        existing.GetSnapshot(),
                        AlreadyRunning: true,
                        CopilotToolFailureKind.None,
                        string.Empty);
                }

                var activeMonitors = _outputMonitors.Count(candidate =>
                    candidate.IsActive);
                var activeConversationMonitors = _outputMonitors.Count(candidate =>
                    candidate.IsActive
                    && string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal));
                if (activeMonitors >= MaximumActiveOutputMonitors)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.Transient,
                        $"At most {MaximumActiveOutputMonitors} output monitors can run in this application session.");
                }
                if (activeConversationMonitors
                    >= MaximumActiveOutputMonitorsPerConversation)
                {
                    return OutputMonitorStartFailure(
                        CopilotToolFailureKind.Transient,
                        $"At most {MaximumActiveOutputMonitorsPerConversation} output monitors can run for one conversation.");
                }

                var initialOffset =
                    stream == CopilotBackgroundShellOutputStream.StandardError
                        ? command.ArchivedStandardErrorCharacters
                        : command.ArchivedStandardOutputCharacters;
                var startedAtUtc = DateTimeOffset.UtcNow;
                monitor = new OutputMonitorEntry(
                    "monitor:" + Guid.NewGuid().ToString("N"),
                    normalizedConversationId,
                    normalizedBackgroundId,
                    stream,
                    normalizedDescription,
                    startedAtUtc,
                    startedAtUtc.AddSeconds(lifetimeSeconds),
                    initialOffset,
                    entry);
                _outputMonitors.Add(monitor);
                TrimRetainedOutputMonitorsUnderLock();
            }

            _ = ObserveOutputMonitorAsync(monitor);
            return new CopilotBackgroundShellOutputMonitorStartResult(
                monitor.GetSnapshot(),
                AlreadyRunning: false,
                CopilotToolFailureKind.None,
                string.Empty);
        }

        public CopilotBackgroundShellOutputMonitorStopResult StopOutputMonitor(
            string? conversationId,
            string? monitorId)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedMonitorId = (monitorId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0
                || normalizedMonitorId.Length == 0)
            {
                return OutputMonitorStopFailure(
                    CopilotToolFailureKind.Validation,
                    "conversationId and monitorId are required.");
            }

            lock (_syncRoot)
            {
                var monitor = _outputMonitors.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Id,
                        normalizedMonitorId,
                        StringComparison.Ordinal));
                if (monitor == null)
                {
                    return OutputMonitorStopFailure(
                        CopilotToolFailureKind.NotFound,
                        "The output monitor was not found in the current conversation.");
                }

                monitor.TryComplete(
                    CopilotBackgroundShellOutputMonitorState.Stopped);
                return new CopilotBackgroundShellOutputMonitorStopResult(
                    monitor.GetSnapshot(),
                    CopilotToolFailureKind.None,
                    string.Empty);
            }
        }

        internal IReadOnlyList<CopilotBackgroundShellOutputMonitorSnapshot>
            GetOutputMonitorSnapshots(string? conversationId)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            if (normalizedConversationId.Length == 0)
            {
                return Array.Empty<
                    CopilotBackgroundShellOutputMonitorSnapshot>();
            }

            lock (_syncRoot)
            {
                return _outputMonitors
                    .Where(candidate => string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .OrderByDescending(candidate => candidate.StartedAtUtc)
                    .Select(candidate => candidate.GetSnapshot())
                    .ToArray();
            }
        }

        private async Task ObserveOutputMonitorAsync(OutputMonitorEntry monitor)
        {
            try
            {
                while (monitor.IsActive)
                {
                    var now = DateTimeOffset.UtcNow;
                    if (now >= monitor.ExpiresAtUtc)
                    {
                        PublishMonitorBatches(
                            monitor,
                            monitor.FlushPartialLine(),
                            now);
                        monitor.TryComplete(
                            CopilotBackgroundShellOutputMonitorState.Expired);
                        return;
                    }

                    var command = monitor.CommandEntry.GetSnapshot();
                    var archiveAvailable =
                        monitor.Stream
                            == CopilotBackgroundShellOutputStream.StandardError
                            ? command.StandardErrorArchiveAvailable
                            : command.StandardOutputArchiveAvailable;
                    var archiveTruncated =
                        monitor.Stream
                            == CopilotBackgroundShellOutputStream.StandardError
                            ? command.StandardErrorArchiveTruncated
                            : command.StandardOutputArchiveTruncated;
                    if (!archiveAvailable)
                    {
                        monitor.TryComplete(
                            CopilotBackgroundShellOutputMonitorState
                                .ArchiveUnavailable);
                        return;
                    }
                    if (archiveTruncated)
                    {
                        monitor.TryComplete(
                            CopilotBackgroundShellOutputMonitorState
                                .ArchiveTruncated);
                        return;
                    }

                    var archivedCharacters =
                        monitor.Stream
                            == CopilotBackgroundShellOutputStream.StandardError
                            ? command.ArchivedStandardErrorCharacters
                            : command.ArchivedStandardOutputCharacters;
                    if (archivedCharacters > monitor.OffsetCharacters)
                    {
                        if (command.IsActive)
                        {
                            await Task.Delay(
                                    OutputMonitorDebounce,
                                    monitor.CancellationToken)
                                .ConfigureAwait(false);
                            command = monitor.CommandEntry.GetSnapshot();
                        }

                        var batches = ReadMonitorBatches(
                            monitor,
                            flushPartialLine: !command.IsActive);
                        PublishMonitorBatches(
                            monitor,
                            batches,
                            DateTimeOffset.UtcNow);
                        if (!monitor.IsActive)
                            return;
                    }
                    else if (!command.IsActive)
                    {
                        PublishMonitorBatches(
                            monitor,
                            monitor.FlushPartialLine(),
                            DateTimeOffset.UtcNow);
                    }

                    if (!command.IsActive)
                    {
                        monitor.TryComplete(
                            CopilotBackgroundShellOutputMonitorState.Completed);
                        return;
                    }

                    var remaining = monitor.ExpiresAtUtc
                        - DateTimeOffset.UtcNow;
                    if (remaining <= TimeSpan.Zero)
                        continue;
                    await monitor.CommandEntry.WaitForObservationChangeAsync(
                            command.ObservationVersion,
                            remaining,
                            monitor.CancellationToken)
                        .ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
                when (monitor.CancellationToken.IsCancellationRequested)
            {
            }
            catch (Exception ex)
                when (ex is not OutOfMemoryException)
            {
                monitor.TryComplete(
                    CopilotBackgroundShellOutputMonitorState.ArchiveUnavailable);
                Trace.TraceError(
                    "Copilot background output monitor failed: "
                    + CopilotMcpAuditLogger.RedactText(ex.Message));
            }
        }

        private static List<string> ReadMonitorBatches(
            OutputMonitorEntry monitor,
            bool flushPartialLine)
        {
            var batches = new List<string>();
            while (monitor.IsActive)
            {
                var page = monitor.CommandEntry.ReadOutputArchive(
                    monitor.Stream,
                    monitor.OffsetCharacters,
                    MaximumArchiveReadCharacters,
                    monitor.CancellationToken);
                if (!page.Available)
                {
                    monitor.TryComplete(
                        CopilotBackgroundShellOutputMonitorState
                            .ArchiveUnavailable);
                    return batches;
                }
                if (page.ArchiveTruncated)
                {
                    monitor.TryComplete(
                        CopilotBackgroundShellOutputMonitorState
                            .ArchiveTruncated);
                    return batches;
                }
                if (page.ReturnedCharacters > 0)
                {
                    monitor.AdvanceOffset(page.NextOffsetCharacters);
                    batches.AddRange(
                        monitor.AppendContent(
                            page.Content,
                            flushPartialLine: false));
                }
                if (page.EndOfAvailableOutput
                    || page.ReturnedCharacters == 0)
                {
                    break;
                }
            }

            if (flushPartialLine && monitor.IsActive)
                batches.AddRange(monitor.FlushPartialLine());
            return batches;
        }

        private void PublishMonitorBatches(
            OutputMonitorEntry monitor,
            IReadOnlyList<string> batches,
            DateTimeOffset now)
        {
            foreach (var batch in batches)
            {
                if (!monitor.TryCreateEvent(
                        batch,
                        now,
                        out var eventArgs))
                {
                    if (!monitor.IsActive)
                        return;
                    continue;
                }
                PublishOutputMonitorEvent(eventArgs);
            }
        }

        private void PublishOutputMonitorEvent(
            CopilotBackgroundShellOutputMonitorEventArgs eventArgs)
        {
            var handlers = OutputMonitorEvent;
            if (handlers == null)
                return;

            foreach (
                EventHandler<CopilotBackgroundShellOutputMonitorEventArgs>
                    handler in handlers.GetInvocationList())
            {
                try
                {
                    handler(this, eventArgs);
                }
                catch (Exception ex)
                {
                    Trace.TraceError(
                        "Copilot background output monitor handler failed: "
                        + CopilotMcpAuditLogger.RedactText(ex.Message));
                }
            }
        }

        private void StopAllOutputMonitorsUnderLock(
            CopilotBackgroundShellOutputMonitorState state)
        {
            foreach (var monitor in _outputMonitors)
                monitor.TryComplete(state);
        }

        private void RemoveOutputMonitorsForCommandUnderLock(
            string conversationId,
            string backgroundId)
        {
            var removed = _outputMonitors
                .Where(monitor =>
                    string.Equals(
                        monitor.ConversationId,
                        conversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        monitor.BackgroundId,
                        backgroundId,
                        StringComparison.Ordinal))
                .ToArray();
            foreach (var monitor in removed)
            {
                _outputMonitors.Remove(monitor);
                monitor.Dispose();
            }
        }

        private void TrimRetainedOutputMonitorsUnderLock()
        {
            var removable = _outputMonitors
                .Where(monitor => !monitor.IsActive)
                .OrderBy(monitor => monitor.StartedAtUtc)
                .ToList();
            while (_outputMonitors.Count > MaximumRetainedOutputMonitors
                && removable.Count > 0)
            {
                var monitor = removable[0];
                removable.RemoveAt(0);
                _outputMonitors.Remove(monitor);
                monitor.Dispose();
            }
        }

        private static string NormalizeMonitorDescription(string? value)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace("\0", string.Empty, StringComparison.Ordinal);
            var collapsed = string.Join(
                " ",
                redacted.Split(
                    (char[]?)null,
                    StringSplitOptions.RemoveEmptyEntries));
            return collapsed.Length <= MaximumOutputMonitorDescriptionCharacters
                ? collapsed
                : collapsed[..MaximumOutputMonitorDescriptionCharacters];
        }

        private static CopilotBackgroundShellOutputMonitorStartResult
            OutputMonitorStartFailure(
                CopilotToolFailureKind failureKind,
                string errorMessage) =>
            new(
                null,
                AlreadyRunning: false,
                failureKind,
                errorMessage);

        private static CopilotBackgroundShellOutputMonitorStopResult
            OutputMonitorStopFailure(
                CopilotToolFailureKind failureKind,
                string errorMessage) =>
            new(null, failureKind, errorMessage);

        private sealed class OutputMonitorEntry : IDisposable
        {
            private readonly object _syncRoot = new();
            private readonly CancellationTokenSource _cancellationSource =
                new();
            private readonly CancellationToken _cancellationToken;
            private readonly CopilotBackgroundShellOutputLineAssembler
                _lineAssembler = new();
            private readonly CopilotBackgroundShellOutputMonitorRateLimiter
                _rateLimiter;
            private CopilotBackgroundShellOutputMonitorState _state =
                CopilotBackgroundShellOutputMonitorState.Running;
            private int _offsetCharacters;
            private int _publishedEvents;
            private int _disposed;

            public OutputMonitorEntry(
                string id,
                string conversationId,
                string backgroundId,
                CopilotBackgroundShellOutputStream stream,
                string description,
                DateTimeOffset startedAtUtc,
                DateTimeOffset expiresAtUtc,
                int offsetCharacters,
                Entry commandEntry)
            {
                Id = id;
                ConversationId = conversationId;
                BackgroundId = backgroundId;
                Stream = stream;
                Description = description;
                StartedAtUtc = startedAtUtc;
                ExpiresAtUtc = expiresAtUtc;
                _offsetCharacters = offsetCharacters;
                CommandEntry = commandEntry;
                _cancellationToken = _cancellationSource.Token;
                _rateLimiter =
                    new CopilotBackgroundShellOutputMonitorRateLimiter(
                        startedAtUtc);
            }

            public string Id { get; }

            public string ConversationId { get; }

            public string BackgroundId { get; }

            public CopilotBackgroundShellOutputStream Stream { get; }

            public string Description { get; }

            public DateTimeOffset StartedAtUtc { get; }

            public DateTimeOffset ExpiresAtUtc { get; }

            public Entry CommandEntry { get; }

            public CancellationToken CancellationToken =>
                _cancellationToken;

            public bool IsActive
            {
                get
                {
                    lock (_syncRoot)
                    {
                        return _state
                            == CopilotBackgroundShellOutputMonitorState.Running;
                    }
                }
            }

            public int OffsetCharacters
            {
                get
                {
                    lock (_syncRoot)
                        return _offsetCharacters;
                }
            }

            public void AdvanceOffset(int offsetCharacters)
            {
                lock (_syncRoot)
                {
                    _offsetCharacters = Math.Max(
                        _offsetCharacters,
                        offsetCharacters);
                }
            }

            public IReadOnlyList<string> AppendContent(
                string content,
                bool flushPartialLine)
            {
                lock (_syncRoot)
                {
                    return _lineAssembler.Append(
                        content,
                        flushPartialLine);
                }
            }

            public IReadOnlyList<string> FlushPartialLine()
            {
                lock (_syncRoot)
                    return _lineAssembler.Flush();
            }

            public bool TryCreateEvent(
                string content,
                DateTimeOffset now,
                out CopilotBackgroundShellOutputMonitorEventArgs eventArgs)
            {
                lock (_syncRoot)
                {
                    eventArgs = null!;
                    if (_state
                        != CopilotBackgroundShellOutputMonitorState.Running
                        || string.IsNullOrEmpty(content))
                    {
                        return false;
                    }

                    if (!_rateLimiter.TryAcquire(
                            now,
                            out var suppressedEvents,
                            out var overloaded))
                    {
                        if (overloaded)
                        {
                            _state =
                                CopilotBackgroundShellOutputMonitorState
                                    .Overloaded;
                            _cancellationSource.Cancel();
                        }
                        return false;
                    }

                    _publishedEvents++;
                    eventArgs =
                        new CopilotBackgroundShellOutputMonitorEventArgs(
                            CreateSnapshotUnderLock(),
                            content,
                            suppressedEvents);
                    return true;
                }
            }

            public bool TryComplete(
                CopilotBackgroundShellOutputMonitorState state)
            {
                lock (_syncRoot)
                {
                    if (_state
                        != CopilotBackgroundShellOutputMonitorState.Running)
                    {
                        return false;
                    }
                    _state = state;
                    _cancellationSource.Cancel();
                    return true;
                }
            }

            public CopilotBackgroundShellOutputMonitorSnapshot GetSnapshot()
            {
                lock (_syncRoot)
                    return CreateSnapshotUnderLock();
            }

            private CopilotBackgroundShellOutputMonitorSnapshot
                CreateSnapshotUnderLock() =>
                new(
                    Id,
                    ConversationId,
                    BackgroundId,
                    Stream,
                    Description,
                    StartedAtUtc,
                    ExpiresAtUtc,
                    _state,
                    _publishedEvents,
                    _rateLimiter.TotalSuppressedEvents);

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 1)
                    return;
                TryComplete(
                    CopilotBackgroundShellOutputMonitorState.Stopped);
                _cancellationSource.Dispose();
            }
        }
    }

    internal sealed class CopilotBackgroundShellOutputMonitorRateLimiter
    {
        internal const int Capacity = 10;
        internal static readonly TimeSpan RefillInterval =
            TimeSpan.FromSeconds(2);
        internal static readonly TimeSpan MaximumContinuousSuppression =
            TimeSpan.FromSeconds(30);

        private double _availableTokens = Capacity;
        private DateTimeOffset _lastRefillUtc;
        private DateTimeOffset? _suppressionStartedAtUtc;
        private int _pendingSuppressedEvents;

        public CopilotBackgroundShellOutputMonitorRateLimiter(
            DateTimeOffset startedAtUtc)
        {
            _lastRefillUtc = startedAtUtc;
        }

        public int TotalSuppressedEvents { get; private set; }

        public bool TryAcquire(
            DateTimeOffset now,
            out int suppressedEvents,
            out bool overloaded)
        {
            Refill(now);
            suppressedEvents = 0;
            overloaded = false;
            if (_availableTokens >= Capacity)
                _suppressionStartedAtUtc = null;
            if (_suppressionStartedAtUtc.HasValue
                && now - _suppressionStartedAtUtc.Value
                    >= MaximumContinuousSuppression)
            {
                overloaded = true;
                return false;
            }
            if (_availableTokens >= 1)
            {
                _availableTokens -= 1;
                suppressedEvents = _pendingSuppressedEvents;
                _pendingSuppressedEvents = 0;
                return true;
            }

            TotalSuppressedEvents++;
            _pendingSuppressedEvents++;
            _suppressionStartedAtUtc ??= now;
            return false;
        }

        private void Refill(DateTimeOffset now)
        {
            if (now <= _lastRefillUtc)
                return;
            var elapsed = now - _lastRefillUtc;
            _availableTokens = Math.Min(
                Capacity,
                _availableTokens
                + elapsed.TotalMilliseconds
                    / RefillInterval.TotalMilliseconds);
            _lastRefillUtc = now;
        }
    }

    internal sealed class CopilotBackgroundShellOutputLineAssembler
    {
        internal const int MaximumLineCharacters = 500;
        internal const int MaximumBatchCharacters = 3_000;
        private const string TruncationSuffix = "...<line truncated>";

        private readonly StringBuilder _pendingLine = new();
        private bool _pendingLineTruncated;

        public IReadOnlyList<string> Append(
            string? content,
            bool flushPartialLine)
        {
            var lines = new List<string>();
            foreach (var character in content ?? string.Empty)
            {
                if (character == '\n')
                {
                    CompleteLine(lines);
                    continue;
                }
                if (character == '\r')
                    continue;
                if (_pendingLine.Length < MaximumLineCharacters)
                    _pendingLine.Append(character);
                else
                    _pendingLineTruncated = true;
            }
            if (flushPartialLine)
                CompleteLine(lines);
            return CreateBatches(lines);
        }

        public IReadOnlyList<string> Flush() =>
            Append(string.Empty, flushPartialLine: true);

        private void CompleteLine(List<string> lines)
        {
            if (_pendingLine.Length == 0 && !_pendingLineTruncated)
                return;

            var line = _pendingLine.ToString();
            if (_pendingLineTruncated)
            {
                var retainedCharacters =
                    MaximumLineCharacters - TruncationSuffix.Length;
                line = line[..Math.Min(line.Length, retainedCharacters)]
                    + TruncationSuffix;
            }
            if (line.Length > 0)
                lines.Add(line);
            _pendingLine.Clear();
            _pendingLineTruncated = false;
        }

        private static IReadOnlyList<string> CreateBatches(
            List<string> lines)
        {
            if (lines.Count == 0)
                return Array.Empty<string>();

            var batches = new List<string>();
            var batch = new StringBuilder();
            foreach (var line in lines)
            {
                var separatorCharacters = batch.Length == 0 ? 0 : 1;
                if (batch.Length > 0
                    && batch.Length + separatorCharacters + line.Length
                        > MaximumBatchCharacters)
                {
                    batches.Add(batch.ToString());
                    batch.Clear();
                }
                if (batch.Length > 0)
                    batch.Append('\n');
                batch.Append(line);
            }
            if (batch.Length > 0)
                batches.Add(batch.ToString());
            return batches;
        }
    }
}
