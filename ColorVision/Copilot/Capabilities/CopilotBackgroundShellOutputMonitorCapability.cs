using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
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

}
