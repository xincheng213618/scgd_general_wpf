using System;
using System.Collections.Generic;
using System.Threading;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotBackgroundShellCommandRegistry
    {
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
