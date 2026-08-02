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
        public CopilotBackgroundShellCommandOutputReadResult ReadOutputArchive(
            string? conversationId,
            string? backgroundId,
            CopilotBackgroundShellOutputStream stream,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedBackgroundId = (backgroundId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0
                || normalizedBackgroundId.Length == 0)
            {
                return OutputReadFailure(
                    stream,
                    CopilotToolFailureKind.Validation,
                    "conversationId and backgroundId are required.");
            }
            if (offsetCharacters < 0)
            {
                return OutputReadFailure(
                    stream,
                    CopilotToolFailureKind.Validation,
                    "offsetCharacters cannot be negative.");
            }
            if (maximumCharacters is < 1 or > MaximumArchiveReadCharacters)
            {
                return OutputReadFailure(
                    stream,
                    CopilotToolFailureKind.Validation,
                    $"maximumCharacters must be an integer from 1 through {MaximumArchiveReadCharacters}.");
            }

            Entry? entry;
            CopilotBackgroundShellCommandSnapshot? snapshot;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                entry = _entries.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Id,
                        normalizedBackgroundId,
                        StringComparison.Ordinal));
                snapshot = entry?.GetSnapshot();
            }
            if (entry == null || snapshot == null)
            {
                return OutputReadFailure(
                    stream,
                    CopilotToolFailureKind.NotFound,
                    "The background command was not found in the current conversation.");
            }

            var page = entry.ReadOutputArchive(
                stream,
                offsetCharacters,
                maximumCharacters,
                cancellationToken);
            if (!page.Available)
            {
                return new CopilotBackgroundShellCommandOutputReadResult(
                    snapshot,
                    stream,
                    page,
                    CopilotToolFailureKind.Transient,
                    page.ErrorMessage);
            }
            return new CopilotBackgroundShellCommandOutputReadResult(
                snapshot,
                stream,
                page,
                CopilotToolFailureKind.None,
                string.Empty);
        }

        public async Task<CopilotBackgroundShellCommandWaitResult> WaitForObservationAsync(
            string? conversationId,
            string? backgroundId,
            string? outputContains,
            int timeoutSeconds,
            Action<CopilotBackgroundShellCommandSnapshot>? onSnapshot,
            CancellationToken cancellationToken)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedBackgroundId = (backgroundId ?? string.Empty).Trim();
            var outputPattern = (outputContains ?? string.Empty)
                .Replace("\0", string.Empty, StringComparison.Ordinal);
            if (normalizedConversationId.Length == 0
                || normalizedBackgroundId.Length == 0)
            {
                return WaitFailure(
                    CopilotToolFailureKind.Validation,
                    "conversationId and backgroundId are required.");
            }
            if (outputPattern.Length > MaximumOutputPatternCharacters)
            {
                return WaitFailure(
                    CopilotToolFailureKind.Validation,
                    $"outputContains cannot exceed {MaximumOutputPatternCharacters} characters.");
            }
            if (timeoutSeconds is < MinimumObservationTimeoutSeconds
                or > MaximumObservationTimeoutSeconds)
            {
                return WaitFailure(
                    CopilotToolFailureKind.Validation,
                    $"timeoutSeconds must be an integer from {MinimumObservationTimeoutSeconds} through {MaximumObservationTimeoutSeconds}.");
            }

            Entry? observedEntry;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                observedEntry = _entries.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Id,
                        normalizedBackgroundId,
                        StringComparison.Ordinal));
            }
            if (observedEntry == null)
            {
                return WaitFailure(
                    CopilotToolFailureKind.NotFound,
                    "The background command was not found in the current conversation.");
            }

            using var terminalObservation =
                new TerminalObservationScope([observedEntry]);
            var standardOutputSearchOffset = 0;
            var standardErrorSearchOffset = 0;
            var stopwatch = Stopwatch.StartNew();
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var snapshot = GetSnapshots(
                        normalizedConversationId,
                        normalizedBackgroundId)
                    .SingleOrDefault();
                if (snapshot == null)
                {
                    return WaitFailure(
                        CopilotToolFailureKind.NotFound,
                        "The background command was not found in the current conversation.",
                        stopwatch.Elapsed);
                }
                TryPublishObservation(onSnapshot, snapshot);
                if (!snapshot.IsActive)
                {
                    return new CopilotBackgroundShellCommandWaitResult(
                        snapshot,
                        CopilotBackgroundShellCommandObservation.Terminal,
                        stopwatch.Elapsed,
                        CopilotToolFailureKind.None,
                        string.Empty);
                }
                var outputMatchSource =
                    CopilotBackgroundShellCommandOutputMatchSource.None;
                if (outputPattern.Length > 0)
                {
                    if (snapshot.StandardOutput.Contains(
                            outputPattern,
                            StringComparison.OrdinalIgnoreCase)
                        || snapshot.StandardError.Contains(
                            outputPattern,
                            StringComparison.OrdinalIgnoreCase))
                    {
                        outputMatchSource =
                            CopilotBackgroundShellCommandOutputMatchSource.Preview;
                    }
                    else if (SearchArchivedOutput(
                            normalizedConversationId,
                            normalizedBackgroundId,
                            CopilotBackgroundShellOutputStream.StandardOutput,
                            outputPattern,
                            ref standardOutputSearchOffset,
                            cancellationToken)
                        || SearchArchivedOutput(
                            normalizedConversationId,
                            normalizedBackgroundId,
                            CopilotBackgroundShellOutputStream.StandardError,
                            outputPattern,
                            ref standardErrorSearchOffset,
                            cancellationToken))
                    {
                        outputMatchSource =
                            CopilotBackgroundShellCommandOutputMatchSource.Archive;
                    }
                }
                if (outputMatchSource
                    != CopilotBackgroundShellCommandOutputMatchSource.None)
                {
                    return new CopilotBackgroundShellCommandWaitResult(
                        snapshot,
                        CopilotBackgroundShellCommandObservation.OutputMatched,
                        stopwatch.Elapsed,
                        CopilotToolFailureKind.None,
                        string.Empty)
                    {
                        OutputMatchSource = outputMatchSource,
                    };
                }
                if (stopwatch.Elapsed >= TimeSpan.FromSeconds(timeoutSeconds))
                {
                    return new CopilotBackgroundShellCommandWaitResult(
                        snapshot,
                        CopilotBackgroundShellCommandObservation.TimedOut,
                        stopwatch.Elapsed,
                        CopilotToolFailureKind.None,
                        string.Empty);
                }

                var remaining = TimeSpan.FromSeconds(timeoutSeconds)
                    - stopwatch.Elapsed;
                await WaitForObservationChangeAsync(
                        normalizedConversationId,
                        normalizedBackgroundId,
                        snapshot.ObservationVersion,
                        remaining,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
        }

        public async Task<CopilotBackgroundShellCommandGroupWaitResult> WaitForTerminalGroupAsync(
            string? conversationId,
            IReadOnlyList<string>? backgroundIds,
            CopilotBackgroundShellCommandGroupWaitMode mode,
            int timeoutSeconds,
            Action<IReadOnlyList<CopilotBackgroundShellCommandSnapshot>>? onSnapshots,
            CancellationToken cancellationToken)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedBackgroundIds = (backgroundIds ?? Array.Empty<string>())
                .Select(backgroundId => (backgroundId ?? string.Empty).Trim())
                .ToArray();
            if (normalizedConversationId.Length == 0)
            {
                return GroupWaitFailure(
                    mode,
                    CopilotToolFailureKind.Validation,
                    "conversationId is required.");
            }
            if (normalizedBackgroundIds.Length is < 1 or > MaximumGroupWaitCommands
                || normalizedBackgroundIds.Any(backgroundId => backgroundId.Length == 0))
            {
                return GroupWaitFailure(
                    mode,
                    CopilotToolFailureKind.Validation,
                    $"backgroundIds must contain 1 through {MaximumGroupWaitCommands} non-empty ids.");
            }
            if (normalizedBackgroundIds.Distinct(StringComparer.Ordinal).Count()
                != normalizedBackgroundIds.Length)
            {
                return GroupWaitFailure(
                    mode,
                    CopilotToolFailureKind.Validation,
                    "backgroundIds must not contain duplicate ids.");
            }
            if (!Enum.IsDefined(mode))
            {
                return GroupWaitFailure(
                    mode,
                    CopilotToolFailureKind.Validation,
                    "mode must be any or all.");
            }
            if (timeoutSeconds is < MinimumObservationTimeoutSeconds
                or > MaximumObservationTimeoutSeconds)
            {
                return GroupWaitFailure(
                    mode,
                    CopilotToolFailureKind.Validation,
                    $"timeoutSeconds must be an integer from {MinimumObservationTimeoutSeconds} through {MaximumObservationTimeoutSeconds}.");
            }

            Entry[] observedEntries;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                var entriesById = _entries
                    .Where(entry => string.Equals(
                        entry.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .ToDictionary(entry => entry.Id, StringComparer.Ordinal);
                if (normalizedBackgroundIds.Any(backgroundId =>
                    !entriesById.ContainsKey(backgroundId)))
                {
                    return GroupWaitFailure(
                        mode,
                        CopilotToolFailureKind.NotFound,
                        "One or more background commands were not found in the current conversation.");
                }
                observedEntries = normalizedBackgroundIds
                    .Select(backgroundId => entriesById[backgroundId])
                    .ToArray();
            }

            using var terminalObservation =
                new TerminalObservationScope(observedEntries);
            var stopwatch = Stopwatch.StartNew();
            var maximumWait = TimeSpan.FromSeconds(timeoutSeconds);
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                CopilotBackgroundShellCommandSnapshot[] snapshots;
                Task<CopilotBackgroundShellProcessCompletion>[] pendingCompletions;
                lock (_syncRoot)
                {
                    RefreshCompletedEntriesUnderLock();
                    var entriesById = _entries
                        .Where(entry => string.Equals(
                            entry.ConversationId,
                            normalizedConversationId,
                            StringComparison.Ordinal))
                        .ToDictionary(entry => entry.Id, StringComparer.Ordinal);
                    if (normalizedBackgroundIds.Any(backgroundId =>
                        !entriesById.ContainsKey(backgroundId)))
                    {
                        return GroupWaitFailure(
                            mode,
                            CopilotToolFailureKind.NotFound,
                            "One or more background commands were not found in the current conversation.",
                            stopwatch.Elapsed);
                    }
                    var entries = normalizedBackgroundIds
                        .Select(backgroundId => entriesById[backgroundId])
                        .ToArray();
                    snapshots = entries
                        .Select(entry => entry.GetSnapshot())
                        .ToArray();
                    pendingCompletions = entries
                        .Where((entry, index) => snapshots[index].IsActive)
                        .Select(entry => entry.Completion)
                        .ToArray();
                }
                TryPublishObservations(onSnapshots, snapshots);

                var terminalCount = snapshots.Count(snapshot => !snapshot.IsActive);
                var terminalConditionMet = mode switch
                {
                    CopilotBackgroundShellCommandGroupWaitMode.Any =>
                        terminalCount > 0,
                    CopilotBackgroundShellCommandGroupWaitMode.All =>
                        terminalCount == snapshots.Length,
                    _ => false,
                };
                if (terminalConditionMet)
                {
                    return new CopilotBackgroundShellCommandGroupWaitResult(
                        snapshots,
                        mode,
                        CopilotBackgroundShellCommandObservation.Terminal,
                        stopwatch.Elapsed,
                        CopilotToolFailureKind.None,
                        string.Empty);
                }
                if (stopwatch.Elapsed >= maximumWait
                    || pendingCompletions.Length == 0)
                {
                    return new CopilotBackgroundShellCommandGroupWaitResult(
                        snapshots,
                        mode,
                        CopilotBackgroundShellCommandObservation.TimedOut,
                        stopwatch.Elapsed,
                        CopilotToolFailureKind.None,
                        string.Empty);
                }

                var remaining = maximumWait - stopwatch.Elapsed;
                if (remaining <= TimeSpan.Zero)
                    continue;
                try
                {
                    var completed = await Task.WhenAny(pendingCompletions)
                        .WaitAsync(remaining, cancellationToken)
                        .ConfigureAwait(false);
                    await completed.WaitAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (TimeoutException)
                {
                }
                catch (Exception ex) when (ex is not OperationCanceledException
                    and not OutOfMemoryException)
                {
                    return GroupWaitFailure(
                        mode,
                        CopilotToolFailureKind.Internal,
                        "A background command completion signal failed: "
                        + CopilotMcpAuditLogger.RedactText(ex.Message),
                        stopwatch.Elapsed);
                }
            }
        }

        private async Task WaitForObservationChangeAsync(
            string conversationId,
            string backgroundId,
            long observationVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            if (timeout <= TimeSpan.Zero)
                return;

            Entry? entry;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                entry = _entries.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.ConversationId,
                        conversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Id,
                        backgroundId,
                        StringComparison.Ordinal));
            }
            if (entry == null)
                return;

            await entry.WaitForObservationChangeAsync(
                    observationVersion,
                    timeout,
                    cancellationToken)
                .ConfigureAwait(false);
        }

        private bool SearchArchivedOutput(
            string conversationId,
            string backgroundId,
            CopilotBackgroundShellOutputStream stream,
            string literal,
            ref int offsetCharacters,
            CancellationToken cancellationToken)
        {
            Entry? entry;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                entry = _entries.SingleOrDefault(candidate =>
                    string.Equals(
                        candidate.ConversationId,
                        conversationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        candidate.Id,
                        backgroundId,
                        StringComparison.Ordinal));
            }
            if (entry == null)
                return false;

            var search = entry.SearchOutputArchive(
                stream,
                literal,
                offsetCharacters,
                cancellationToken);
            if (!search.Available)
                return false;

            offsetCharacters = search.NextOffsetCharacters;
            return search.Matched;
        }
    }
}
