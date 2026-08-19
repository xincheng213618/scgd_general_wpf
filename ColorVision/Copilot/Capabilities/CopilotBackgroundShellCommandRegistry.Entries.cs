using ColorVision.Copilot.Mcp;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.Copilot
{
    internal sealed partial class CopilotBackgroundShellCommandRegistry
    {
        private sealed class TerminalObservationScope : IDisposable
        {
            private readonly Entry[] _entries;
            private IDisposable[]? _registrations;

            public TerminalObservationScope(IEnumerable<Entry> entries)
            {
                _entries = (entries ?? Array.Empty<Entry>()).ToArray();
                _registrations = _entries
                    .Select(entry => entry.BeginTerminalObservation())
                    .ToArray();
            }

            public void MarkTerminalResultsReturned(
                IEnumerable<string> backgroundIds)
            {
                var returnedIds = (backgroundIds ?? Array.Empty<string>())
                    .ToHashSet(StringComparer.Ordinal);
                foreach (var entry in _entries.Where(entry =>
                             returnedIds.Contains(entry.Id)))
                {
                    entry.MarkTerminalResultReturned();
                }
            }

            public void Dispose()
            {
                var registrations = Interlocked.Exchange(
                    ref _registrations,
                    null);
                if (registrations == null)
                    return;
                foreach (var registration in registrations)
                    registration.Dispose();
            }
        }

        private sealed class Entry : IDisposable
        {
            private readonly ICopilotBackgroundShellProcess _process;
            private readonly object _terminalObservationSyncRoot = new();
            private CopilotBackgroundShellProcessCompletion? _completion;
            private int _activeTerminalObservationCount;
            private bool _terminalObservationCompletionCaptured;
            private bool _terminalObservationWasPendingAtCompletion;
            private bool _terminalResultWasReturnedAtCompletion;
            private TaskCompletionSource<bool>?
                _terminalObservationSettlement;
            private int _disposed;

            public Entry(
                string id,
                string conversationId,
                string taskId,
                CopilotShellKind shell,
                string workingDirectory,
                string commandPreview,
                string commandSha256,
                DateTimeOffset startedAtUtc,
                ICopilotBackgroundShellProcess process)
            {
                Id = id;
                ConversationId = conversationId;
                TaskId = taskId;
                Shell = shell;
                WorkingDirectory = workingDirectory;
                CommandPreview = commandPreview;
                CommandSha256 = commandSha256;
                StartedAtUtc = startedAtUtc;
                _process = process;
                _ = _process.Completion.ContinueWith(
                    static (_, state) =>
                        ((Entry)state!)
                            .CaptureTerminalObservationOwnership(),
                    this,
                    CancellationToken.None,
                    TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
            }

            public string Id { get; }

            public string ConversationId { get; }

            public string TaskId { get; }

            public CopilotShellKind Shell { get; }

            public string WorkingDirectory { get; }

            public string CommandPreview { get; }

            public string CommandSha256 { get; }

            public DateTimeOffset StartedAtUtc { get; }

            public Task<CopilotBackgroundShellProcessCompletion> Completion =>
                _process.Completion;

            public IDisposable BeginTerminalObservation()
            {
                lock (_terminalObservationSyncRoot)
                {
                    _activeTerminalObservationCount++;
                    if (_process.Completion.IsCompleted)
                        CaptureTerminalObservationOwnershipUnderLock();
                }
                return new TerminalObservationRegistration(this);
            }

            private void CaptureTerminalObservationOwnership()
            {
                lock (_terminalObservationSyncRoot)
                    CaptureTerminalObservationOwnershipUnderLock();
            }

            private void CaptureTerminalObservationOwnershipUnderLock()
            {
                _terminalObservationCompletionCaptured = true;
                if (_activeTerminalObservationCount > 0)
                {
                    _terminalObservationWasPendingAtCompletion = true;
                    _terminalObservationSettlement ??=
                        new TaskCompletionSource<bool>(
                            TaskCreationOptions.RunContinuationsAsynchronously);
                }
                else
                    _terminalObservationSettlement?.TrySetResult(true);
            }

            public void MarkTerminalResultReturned()
            {
                lock (_terminalObservationSyncRoot)
                {
                    if (_terminalObservationCompletionCaptured
                        || _process.Completion.IsCompleted)
                    {
                        _terminalObservationCompletionCaptured = true;
                        _terminalResultWasReturnedAtCompletion = true;
                    }
                }
            }

            public async Task<TerminalObservationOutcome>
                WaitForTerminalObservationOutcomeAsync()
            {
                Task? settlement;
                lock (_terminalObservationSyncRoot)
                {
                    CaptureTerminalObservationOwnershipUnderLock();
                    settlement = _activeTerminalObservationCount > 0
                        ? _terminalObservationSettlement!.Task
                        : null;
                }
                if (settlement != null)
                    await settlement.ConfigureAwait(false);

                lock (_terminalObservationSyncRoot)
                {
                    return new TerminalObservationOutcome(
                        _terminalObservationWasPendingAtCompletion,
                        _terminalResultWasReturnedAtCompletion);
                }
            }

            private void EndTerminalObservation()
            {
                lock (_terminalObservationSyncRoot)
                {
                    _activeTerminalObservationCount--;
                    if (_activeTerminalObservationCount == 0
                        && _terminalObservationCompletionCaptured)
                    {
                        _terminalObservationSettlement?.TrySetResult(true);
                    }
                }
            }

            public void RefreshCompletion()
            {
                if (Volatile.Read(ref _completion) == null
                    && _process.Completion.IsCompletedSuccessfully)
                {
                    Interlocked.CompareExchange(
                        ref _completion,
                        _process.Completion.Result,
                        null);
                }
            }

            public CopilotBackgroundShellCommandSnapshot GetSnapshot()
            {
                RefreshCompletion();
                var completion = Volatile.Read(ref _completion);
                var output = completion == null
                    ? _process.GetOutputSnapshot()
                    : new CopilotBackgroundShellProcessOutput(
                        completion.StandardOutput,
                        completion.StandardError,
                        completion.ObservedStandardOutputCharacters,
                        completion.ObservedStandardErrorCharacters,
                        completion.StandardOutputTruncated,
                        completion.StandardErrorTruncated,
                        completion.StandardOutputArchiveAvailable,
                        completion.StandardErrorArchiveAvailable,
                        completion.ArchivedStandardOutputCharacters,
                        completion.ArchivedStandardErrorCharacters,
                        completion.StandardOutputArchiveTruncated,
                        completion.StandardErrorArchiveTruncated)
                    {
                        ObservationVersion = completion.ObservationVersion,
                    };
                var standardOutput = BoundAndRedactOutput(
                    output.StandardOutput,
                    out var standardOutputTruncated);
                var standardError = BoundAndRedactOutput(
                    output.StandardError,
                    out var standardErrorTruncated);
                return new CopilotBackgroundShellCommandSnapshot(
                    Id,
                    ConversationId,
                    TaskId,
                    Shell,
                    WorkingDirectory,
                    CommandPreview,
                    CommandSha256,
                    StartedAtUtc,
                    completion?.CompletedAtUtc,
                    _process.ProcessId,
                    _process.ProcessTreeContained,
                    completion?.State ?? CopilotBackgroundShellCommandState.Running,
                    completion?.ExitCode,
                    standardOutput,
                    standardError)
                {
                    ObservedStandardOutputCharacters =
                        output.ObservedStandardOutputCharacters,
                    ObservedStandardErrorCharacters =
                        output.ObservedStandardErrorCharacters,
                    StandardOutputTruncated =
                        output.StandardOutputTruncated
                        || standardOutputTruncated,
                    StandardErrorTruncated =
                        output.StandardErrorTruncated
                        || standardErrorTruncated,
                    StandardOutputArchiveAvailable =
                        output.StandardOutputArchiveAvailable,
                    StandardErrorArchiveAvailable =
                        output.StandardErrorArchiveAvailable,
                    ArchivedStandardOutputCharacters =
                        output.ArchivedStandardOutputCharacters,
                    ArchivedStandardErrorCharacters =
                        output.ArchivedStandardErrorCharacters,
                    StandardOutputArchiveTruncated =
                        output.StandardOutputArchiveTruncated,
                    StandardErrorArchiveTruncated =
                        output.StandardErrorArchiveTruncated,
                    ObservationVersion = output.ObservationVersion,
                };
            }

            public CopilotRedactedOutputArchivePage ReadOutputArchive(
                CopilotBackgroundShellOutputStream stream,
                int offsetCharacters,
                int maximumCharacters,
                CancellationToken cancellationToken) =>
                _process.ReadOutputArchive(
                    stream,
                    offsetCharacters,
                    maximumCharacters,
                    cancellationToken);

            public CopilotRedactedOutputArchiveSearchResult SearchOutputArchive(
                CopilotBackgroundShellOutputStream stream,
                string literal,
                int offsetCharacters,
                CancellationToken cancellationToken) =>
                _process.SearchOutputArchive(
                    stream,
                    literal,
                    offsetCharacters,
                    cancellationToken);

            public Task WaitForObservationChangeAsync(
                long observationVersion,
                TimeSpan timeout,
                CancellationToken cancellationToken) =>
                _process.WaitForObservationChangeAsync(
                    observationVersion,
                    timeout,
                    cancellationToken);

            public async Task<CopilotBackgroundShellProcessCompletion> StopAsync(
                CancellationToken cancellationToken)
            {
                var completion = await _process.StopAsync(cancellationToken)
                    .ConfigureAwait(false);
                Volatile.Write(ref _completion, completion);
                return completion;
            }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _disposed, 1) == 0)
                    _process.Dispose();
            }

            private sealed class TerminalObservationRegistration(
                Entry owner) : IDisposable
            {
                private Entry? _owner = owner;

                public void Dispose()
                {
                    Interlocked.Exchange(ref _owner, null)
                        ?.EndTerminalObservation();
                }
            }

            public readonly record struct TerminalObservationOutcome(
                bool WasPendingAtCompletion,
                bool TerminalResultWasReturned);
        }

    }
}
