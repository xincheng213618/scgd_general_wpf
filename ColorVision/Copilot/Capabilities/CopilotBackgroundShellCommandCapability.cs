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
    internal enum CopilotBackgroundShellCommandState
    {
        Running,
        Completed,
        Failed,
        Stopped,
        Expired,
    }

    internal enum CopilotBackgroundShellOutputStream
    {
        StandardOutput,
        StandardError,
    }

    internal sealed record CopilotBackgroundShellCommandSnapshot(
        string Id,
        string ConversationId,
        string TaskId,
        CopilotShellKind Shell,
        string WorkingDirectory,
        string CommandPreview,
        string CommandSha256,
        DateTimeOffset StartedAtUtc,
        DateTimeOffset? CompletedAtUtc,
        int ProcessId,
        bool ProcessTreeContained,
        CopilotBackgroundShellCommandState State,
        int? ExitCode,
        string StandardOutput,
        string StandardError)
    {
        public bool IsActive => State == CopilotBackgroundShellCommandState.Running;

        public long ObservedStandardOutputCharacters { get; init; } =
            StandardOutput.Length;

        public long ObservedStandardErrorCharacters { get; init; } =
            StandardError.Length;

        public bool StandardOutputTruncated { get; init; }

        public bool StandardErrorTruncated { get; init; }

        public bool StandardOutputArchiveAvailable { get; init; }

        public bool StandardErrorArchiveAvailable { get; init; }

        public int ArchivedStandardOutputCharacters { get; init; }

        public int ArchivedStandardErrorCharacters { get; init; }

        public bool StandardOutputArchiveTruncated { get; init; }

        public bool StandardErrorArchiveTruncated { get; init; }

        public long ObservationVersion { get; init; }
    }

    internal sealed record CopilotBackgroundShellProcessCompletion(
        CopilotBackgroundShellCommandState State,
        int? ExitCode,
        DateTimeOffset CompletedAtUtc,
        string StandardOutput,
        string StandardError)
    {
        public long ObservedStandardOutputCharacters { get; init; } =
            StandardOutput.Length;

        public long ObservedStandardErrorCharacters { get; init; } =
            StandardError.Length;

        public bool StandardOutputTruncated { get; init; }

        public bool StandardErrorTruncated { get; init; }

        public bool StandardOutputArchiveAvailable { get; init; }

        public bool StandardErrorArchiveAvailable { get; init; }

        public int ArchivedStandardOutputCharacters { get; init; }

        public int ArchivedStandardErrorCharacters { get; init; }

        public bool StandardOutputArchiveTruncated { get; init; }

        public bool StandardErrorArchiveTruncated { get; init; }

        public long ObservationVersion { get; init; }
    }

    internal readonly record struct CopilotBackgroundShellProcessOutput(
        string StandardOutput,
        string StandardError,
        long ObservedStandardOutputCharacters,
        long ObservedStandardErrorCharacters,
        bool StandardOutputTruncated,
        bool StandardErrorTruncated,
        bool StandardOutputArchiveAvailable,
        bool StandardErrorArchiveAvailable,
        int ArchivedStandardOutputCharacters,
        int ArchivedStandardErrorCharacters,
        bool StandardOutputArchiveTruncated,
        bool StandardErrorArchiveTruncated)
    {
        public long ObservationVersion { get; init; }
    }

    internal interface ICopilotBackgroundShellProcess : IDisposable
    {
        int ProcessId { get; }

        bool ProcessTreeContained { get; }

        Task<CopilotBackgroundShellProcessCompletion> Completion { get; }

        CopilotBackgroundShellProcessOutput GetOutputSnapshot();

        CopilotRedactedOutputArchivePage ReadOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken);

        CopilotRedactedOutputArchiveSearchResult SearchOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            string literal,
            int offsetCharacters,
            CancellationToken cancellationToken);

        Task WaitForObservationChangeAsync(
            long observationVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken);

        Task<CopilotBackgroundShellProcessCompletion> StopAsync(CancellationToken cancellationToken);
    }

    internal interface ICopilotBackgroundShellProcessLauncher
    {
        Task<ICopilotBackgroundShellProcess> StartAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken);
    }

    internal sealed record CopilotBackgroundShellCommandStartResult(
        CopilotBackgroundShellCommandSnapshot? Snapshot,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed record CopilotBackgroundShellCommandStopResult(
        CopilotBackgroundShellCommandSnapshot? Snapshot,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null && FailureKind == CopilotToolFailureKind.None;
    }

    internal enum CopilotBackgroundShellCommandObservation
    {
        OutputMatched,
        Terminal,
        TimedOut,
    }

    internal enum CopilotBackgroundShellCommandOutputMatchSource
    {
        None,
        Preview,
        Archive,
    }

    internal enum CopilotBackgroundShellCommandGroupWaitMode
    {
        Any,
        All,
    }

    internal sealed record CopilotBackgroundShellCommandWaitResult(
        CopilotBackgroundShellCommandSnapshot? Snapshot,
        CopilotBackgroundShellCommandObservation Observation,
        TimeSpan Elapsed,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && FailureKind == CopilotToolFailureKind.None;

        public CopilotBackgroundShellCommandOutputMatchSource OutputMatchSource
        {
            get;
            init;
        }
    }

    internal sealed record CopilotBackgroundShellCommandGroupWaitResult(
        IReadOnlyList<CopilotBackgroundShellCommandSnapshot> Snapshots,
        CopilotBackgroundShellCommandGroupWaitMode Mode,
        CopilotBackgroundShellCommandObservation Observation,
        TimeSpan Elapsed,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshots.Count > 0
            && FailureKind == CopilotToolFailureKind.None;

        public int TerminalCount => Snapshots.Count(snapshot => !snapshot.IsActive);
    }

    internal sealed record CopilotBackgroundShellCommandOutputReadResult(
        CopilotBackgroundShellCommandSnapshot? Snapshot,
        CopilotBackgroundShellOutputStream Stream,
        CopilotRedactedOutputArchivePage? Page,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && Page?.Available == true
            && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed class CopilotBackgroundShellCommandCompletedEventArgs : EventArgs
    {
        public CopilotBackgroundShellCommandCompletedEventArgs(
            CopilotBackgroundShellCommandSnapshot snapshot,
            bool terminalObservationWasPendingAtCompletion = false)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TerminalObservationWasPendingAtCompletion =
                terminalObservationWasPendingAtCompletion;
        }

        public CopilotBackgroundShellCommandSnapshot Snapshot { get; }

        public bool TerminalObservationWasPendingAtCompletion { get; }
    }

    internal sealed partial class CopilotBackgroundShellCommandRegistry
    {
        public const int DefaultLifetimeSeconds = 3_600;
        public const int MinimumLifetimeSeconds = 60;
        public const int MaximumLifetimeSeconds = 86_400;
        public const int MaximumActivePerConversation = 4;
        public const int MaximumActiveCommands = 8;
        public const int MaximumRetainedCommands = 24;
        public const int MaximumOutputCharacters = 16_384;
        public const int MaximumCommandPreviewCharacters = 180;
        public const int DefaultObservationTimeoutSeconds = 10;
        public const int MinimumObservationTimeoutSeconds = 1;
        public const int MaximumObservationTimeoutSeconds = 30;
        public const int MaximumOutputPatternCharacters = 256;
        public const int MaximumGroupWaitCommands = MaximumActivePerConversation;
        public const int MaximumArchivedOutputCharacters =
            CopilotOutputArchiveLimits.MaximumArchivedCharacters;
        public const int DefaultArchiveReadCharacters =
            CopilotOutputArchiveLimits.DefaultReadCharacters;
        public const int MaximumArchiveReadCharacters =
            CopilotOutputArchiveLimits.MaximumReadCharacters;

        private readonly object _syncRoot = new();
        private readonly List<Entry> _entries = new();
        private readonly Dictionary<string, int> _startReservationsByConversation =
            new(StringComparer.Ordinal);
        private readonly ICopilotBackgroundShellProcessLauncher _launcher;
        private int _startReservations;
        private bool _isShuttingDown;

        public CopilotBackgroundShellCommandRegistry()
            : this(new CopilotBackgroundShellProcessLauncher())
        {
        }

        internal CopilotBackgroundShellCommandRegistry(ICopilotBackgroundShellProcessLauncher launcher)
        {
            _launcher = launcher ?? throw new ArgumentNullException(nameof(launcher));
        }

        public static CopilotBackgroundShellCommandRegistry Shared { get; } = new();

        public event EventHandler<CopilotBackgroundShellCommandCompletedEventArgs>? CommandCompleted;

        public async Task<CopilotBackgroundShellCommandStartResult> StartAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput input,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(request);
            input ??= CopilotAgentToolInput.Empty;
            var conversationId = NormalizeScopeId(request.ConversationId);
            if (conversationId.Length == 0)
            {
                return StartFailure(
                    CopilotToolFailureKind.Validation,
                    "A background command requires a persisted conversation identity.");
            }
            if (!CopilotShellCommandService.TryResolveExecution(
                    request,
                    input,
                    out var execution,
                    out var validationFailure))
            {
                return StartFailure(
                    validationFailure?.FailureKind ?? CopilotToolFailureKind.Validation,
                    validationFailure?.ErrorMessage ?? "The background shell execution context is invalid.");
            }
            if (!TryReadOptionalInt(
                    input,
                    "lifetimeSeconds",
                    DefaultLifetimeSeconds,
                    out var lifetimeSeconds)
                || lifetimeSeconds is < MinimumLifetimeSeconds or > MaximumLifetimeSeconds)
            {
                return StartFailure(
                    CopilotToolFailureKind.Validation,
                    $"lifetimeSeconds must be an integer from {MinimumLifetimeSeconds} through {MaximumLifetimeSeconds}.");
            }

            var executablePath = CopilotShellCommandService.FindTrustedShellExecutable(execution.Shell);
            if (string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
            {
                return StartFailure(
                    CopilotToolFailureKind.NotFound,
                    $"{CopilotShellCommandService.GetShellLabel(execution.Shell)} could not be located in a trusted system path.");
            }

            lock (_syncRoot)
            {
                if (_isShuttingDown)
                {
                    return StartFailure(
                        CopilotToolFailureKind.Transient,
                        "The application is shutting down and cannot start another background command.");
                }

                RefreshCompletedEntriesUnderLock();
                var activeEntries = _entries.Count(entry => entry.GetSnapshot().IsActive);
                var activeConversationEntries = _entries.Count(entry =>
                    string.Equals(entry.ConversationId, conversationId, StringComparison.Ordinal)
                    && entry.GetSnapshot().IsActive);
                _startReservationsByConversation.TryGetValue(
                    conversationId,
                    out var conversationStartReservations);
                if (activeEntries + _startReservations >= MaximumActiveCommands)
                {
                    return StartFailure(
                        CopilotToolFailureKind.Transient,
                        $"At most {MaximumActiveCommands} background commands can run in this application session.");
                }
                if (activeConversationEntries + conversationStartReservations
                    >= MaximumActivePerConversation)
                {
                    return StartFailure(
                        CopilotToolFailureKind.Transient,
                        $"At most {MaximumActivePerConversation} background commands can run for one conversation.");
                }
                _startReservations++;
                _startReservationsByConversation[conversationId] =
                    conversationStartReservations + 1;
            }

            ICopilotBackgroundShellProcess? process = null;
            try
            {
                process = await _launcher.StartAsync(
                    new CopilotShellProcessCommand(
                        execution.Shell,
                        Path.GetFullPath(executablePath),
                        CopilotShellCommandService.BuildArguments(execution.Shell, execution.CommandText),
                        execution.WorkingDirectory,
                        TimeSpan.FromSeconds(lifetimeSeconds)),
                    cancellationToken).ConfigureAwait(false);
                var entry = new Entry(
                    "bg:" + Guid.NewGuid().ToString("N"),
                    conversationId,
                    NormalizeScopeId(request.TaskId),
                    execution.Shell,
                    execution.WorkingDirectory,
                    BuildCommandPreview(execution.CommandText),
                    BuildCommandDigest(execution.CommandText),
                    DateTimeOffset.UtcNow,
                    process);

                CopilotBackgroundShellCommandStartResult? startResult = null;
                lock (_syncRoot)
                {
                    ReleaseStartReservationUnderLock(conversationId);
                    if (_isShuttingDown)
                    {
                        process = null;
                    }
                    else
                    {
                        _entries.Add(entry);
                        TrimRetainedEntriesUnderLock();
                        process = null;
                        startResult = new CopilotBackgroundShellCommandStartResult(
                            entry.GetSnapshot(),
                            CopilotToolFailureKind.None,
                            string.Empty);
                    }
                }
                if (startResult != null)
                {
                    _ = ObserveCompletionAsync(entry);
                    return startResult;
                }

                await entry.StopAsync(CancellationToken.None).ConfigureAwait(false);
                entry.Dispose();
                return StartFailure(
                    CopilotToolFailureKind.Transient,
                    "The application began shutting down while the background command was starting.");
            }
            catch (OperationCanceledException)
            {
                lock (_syncRoot)
                    ReleaseStartReservationUnderLock(conversationId);
                process?.Dispose();
                throw;
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception or InvalidOperationException)
            {
                lock (_syncRoot)
                    ReleaseStartReservationUnderLock(conversationId);
                process?.Dispose();
                return StartFailure(
                    CopilotToolFailureKind.Internal,
                    "The background shell process could not be started: "
                    + CopilotMcpAuditLogger.RedactText(ex.Message));
            }
        }

        public IReadOnlyList<CopilotBackgroundShellCommandSnapshot> GetSnapshots(
            string? conversationId,
            string? backgroundId = null)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedBackgroundId = (backgroundId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0)
                return Array.Empty<CopilotBackgroundShellCommandSnapshot>();

            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                return _entries
                    .Where(entry => string.Equals(
                        entry.ConversationId,
                        normalizedConversationId,
                        StringComparison.Ordinal))
                    .Where(entry => normalizedBackgroundId.Length == 0
                        || string.Equals(entry.Id, normalizedBackgroundId, StringComparison.Ordinal))
                    .OrderByDescending(entry => entry.StartedAtUtc)
                    .Select(entry => entry.GetSnapshot())
                    .ToArray();
            }
        }

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

        public async Task<CopilotBackgroundShellCommandStopResult> StopAsync(
            string? conversationId,
            string? backgroundId,
            CancellationToken cancellationToken)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            var normalizedBackgroundId = (backgroundId ?? string.Empty).Trim();
            if (normalizedConversationId.Length == 0 || normalizedBackgroundId.Length == 0)
            {
                return StopFailure(
                    CopilotToolFailureKind.Validation,
                    "conversationId and backgroundId are required.");
            }

            Entry? entry;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                entry = _entries.FirstOrDefault(candidate =>
                    string.Equals(candidate.ConversationId, normalizedConversationId, StringComparison.Ordinal)
                    && string.Equals(candidate.Id, normalizedBackgroundId, StringComparison.Ordinal));
            }
            if (entry == null)
            {
                return StopFailure(
                    CopilotToolFailureKind.NotFound,
                    "The background command was not found in the current conversation.");
            }

            var before = entry.GetSnapshot();
            if (!before.IsActive)
            {
                return new CopilotBackgroundShellCommandStopResult(
                    before,
                    CopilotToolFailureKind.None,
                    string.Empty);
            }

            try
            {
                await entry.StopAsync(cancellationToken).ConfigureAwait(false);
                return new CopilotBackgroundShellCommandStopResult(
                    entry.GetSnapshot(),
                    CopilotToolFailureKind.None,
                    string.Empty);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or ObjectDisposedException)
            {
                return StopFailure(
                    CopilotToolFailureKind.Internal,
                    "The background process tree could not be stopped: "
                    + CopilotMcpAuditLogger.RedactText(ex.Message));
            }
        }

        public int ClearCompleted(string? conversationId)
        {
            var normalizedConversationId = NormalizeScopeId(conversationId);
            if (normalizedConversationId.Length == 0)
                return 0;

            Entry[] removed;
            lock (_syncRoot)
            {
                RefreshCompletedEntriesUnderLock();
                removed = _entries
                    .Where(entry => string.Equals(
                            entry.ConversationId,
                            normalizedConversationId,
                            StringComparison.Ordinal)
                        && !entry.GetSnapshot().IsActive)
                    .ToArray();
                foreach (var entry in removed)
                {
                    _entries.Remove(entry);
                    RemoveOutputMonitorsForCommandUnderLock(
                        entry.ConversationId,
                        entry.Id);
                }
            }
            foreach (var entry in removed)
                entry.Dispose();
            return removed.Length;
        }

        public async Task ShutdownAsync()
        {
            Entry[] entries;
            OutputMonitorEntry[] outputMonitors;
            lock (_syncRoot)
            {
                if (_isShuttingDown)
                    return;
                _isShuttingDown = true;
                StopAllOutputMonitorsUnderLock(
                    CopilotBackgroundShellOutputMonitorState.Stopped);
                outputMonitors = _outputMonitors.ToArray();
                _outputMonitors.Clear();
                entries = _entries.ToArray();
                _entries.Clear();
            }

            try
            {
                await Task.WhenAll(entries.Select(entry =>
                        entry.StopAsync(CancellationToken.None)))
                    .ConfigureAwait(false);
            }
            finally
            {
                foreach (var entry in entries)
                    entry.Dispose();
                foreach (var monitor in outputMonitors)
                    monitor.Dispose();
            }
        }

        internal static bool TryReadBackgroundId(
            CopilotAgentToolInput input,
            out string backgroundId)
        {
            backgroundId = string.Empty;
            if (input?.Arguments.TryGetValue("backgroundId", out var raw) != true || raw == null)
                return false;
            if (raw is string text)
            {
                backgroundId = text.Trim();
                return backgroundId.Length > 0;
            }
            if (raw is JsonElement element && element.ValueKind == JsonValueKind.String)
            {
                backgroundId = (element.GetString() ?? string.Empty).Trim();
                return backgroundId.Length > 0;
            }
            return false;
        }

        private void RefreshCompletedEntriesUnderLock()
        {
            foreach (var entry in _entries)
                entry.RefreshCompletion();
        }

        private async Task ObserveCompletionAsync(Entry entry)
        {
            try
            {
                await entry.Completion.ConfigureAwait(false);
                CopilotBackgroundShellCommandSnapshot? snapshot;
                lock (_syncRoot)
                {
                    if (_isShuttingDown || !_entries.Contains(entry))
                        return;
                    snapshot = entry.GetSnapshot();
                }

                var handlers = CommandCompleted;
                if (handlers == null)
                    return;
                var eventArgs = new CopilotBackgroundShellCommandCompletedEventArgs(
                    snapshot,
                    entry.TerminalObservationWasPendingAtCompletion);
                foreach (EventHandler<CopilotBackgroundShellCommandCompletedEventArgs> handler
                    in handlers.GetInvocationList())
                {
                    try
                    {
                        handler(this, eventArgs);
                    }
                    catch (Exception ex)
                    {
                        Trace.TraceError(
                            "Copilot background command completion handler failed: "
                            + CopilotMcpAuditLogger.RedactText(ex.Message));
                    }
                }
            }
            catch (Exception ex)
            {
                Trace.TraceError(
                    "Copilot background command completion observation failed: "
                    + CopilotMcpAuditLogger.RedactText(ex.Message));
            }
        }

        private void ReleaseStartReservationUnderLock(string conversationId)
        {
            _startReservations = Math.Max(0, _startReservations - 1);
            if (!_startReservationsByConversation.TryGetValue(
                    conversationId,
                    out var conversationReservations))
            {
                return;
            }

            if (conversationReservations <= 1)
                _startReservationsByConversation.Remove(conversationId);
            else
                _startReservationsByConversation[conversationId] =
                    conversationReservations - 1;
        }

        private void TrimRetainedEntriesUnderLock()
        {
            RefreshCompletedEntriesUnderLock();
            var removable = _entries
                .Where(entry => !entry.GetSnapshot().IsActive)
                .OrderBy(entry => entry.StartedAtUtc)
                .ToList();
            while (_entries.Count > MaximumRetainedCommands && removable.Count > 0)
            {
                var entry = removable[0];
                removable.RemoveAt(0);
                _entries.Remove(entry);
                RemoveOutputMonitorsForCommandUnderLock(
                    entry.ConversationId,
                    entry.Id);
                entry.Dispose();
            }
        }

        private static bool TryReadOptionalInt(
            CopilotAgentToolInput input,
            string name,
            int defaultValue,
            out int value)
        {
            if (!input.Arguments.TryGetValue(name, out var raw) || raw == null)
            {
                value = defaultValue;
                return true;
            }
            if (raw is int intValue)
            {
                value = intValue;
                return true;
            }
            if (raw is long longValue && longValue is >= int.MinValue and <= int.MaxValue)
            {
                value = (int)longValue;
                return true;
            }
            if (raw is JsonElement element
                && element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out value))
            {
                return true;
            }
            value = 0;
            return false;
        }

        private static string NormalizeScopeId(string? value)
        {
            var normalized = (value ?? string.Empty).Trim();
            return normalized.Length <= 160 ? normalized : normalized[..160];
        }

        private static string BuildCommandPreview(string command)
        {
            var redacted = CopilotMcpAuditLogger.RedactText(command ?? string.Empty);
            var collapsed = string.Join(
                " ",
                redacted.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
            return collapsed.Length <= MaximumCommandPreviewCharacters
                ? collapsed
                : collapsed[..(MaximumCommandPreviewCharacters - 3)] + "...";
        }

        private static string BuildCommandDigest(string command)
        {
            return Convert.ToHexString(
                SHA256.HashData(Encoding.UTF8.GetBytes(command ?? string.Empty)))
                .ToLowerInvariant();
        }

        private static string BoundAndRedactOutput(
            string? value,
            out bool truncated)
        {
            const string truncationMarker =
                "...<earlier background output truncated>...\n";
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace("\0", string.Empty, StringComparison.Ordinal);
            if (redacted.Length <= MaximumOutputCharacters)
            {
                truncated = false;
                return redacted;
            }

            var retainedCharacters =
                MaximumOutputCharacters - truncationMarker.Length;
            truncated = true;
            return truncationMarker + redacted[^retainedCharacters..];
        }

        private static CopilotBackgroundShellCommandStartResult StartFailure(
            CopilotToolFailureKind kind,
            string message) =>
            new(null, kind, message);

        private static CopilotBackgroundShellCommandStopResult StopFailure(
            CopilotToolFailureKind kind,
            string message) =>
            new(null, kind, message);

        private static CopilotBackgroundShellCommandWaitResult WaitFailure(
            CopilotToolFailureKind kind,
            string message,
            TimeSpan? elapsed = null) =>
            new(
                null,
                CopilotBackgroundShellCommandObservation.TimedOut,
                elapsed ?? TimeSpan.Zero,
                kind,
                message);

        private static CopilotBackgroundShellCommandOutputReadResult OutputReadFailure(
            CopilotBackgroundShellOutputStream stream,
            CopilotToolFailureKind kind,
            string message) =>
            new(
                null,
                stream,
                null,
                kind,
                message);

        private static CopilotBackgroundShellCommandGroupWaitResult GroupWaitFailure(
            CopilotBackgroundShellCommandGroupWaitMode mode,
            CopilotToolFailureKind kind,
            string message,
            TimeSpan? elapsed = null) =>
            new(
                Array.Empty<CopilotBackgroundShellCommandSnapshot>(),
                mode,
                CopilotBackgroundShellCommandObservation.TimedOut,
                elapsed ?? TimeSpan.Zero,
                kind,
                message);

        private static void TryPublishObservation(
            Action<CopilotBackgroundShellCommandSnapshot>? onSnapshot,
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            if (onSnapshot == null)
                return;
            try
            {
                onSnapshot(snapshot);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }
        }

        private static void TryPublishObservations(
            Action<IReadOnlyList<CopilotBackgroundShellCommandSnapshot>>? onSnapshots,
            IReadOnlyList<CopilotBackgroundShellCommandSnapshot> snapshots)
        {
            if (onSnapshots == null)
                return;
            try
            {
                onSnapshots(snapshots);
            }
            catch (Exception ex) when (ex is not OutOfMemoryException)
            {
            }
        }

        private sealed class TerminalObservationScope : IDisposable
        {
            private IDisposable[]? _registrations;

            public TerminalObservationScope(IEnumerable<Entry> entries)
            {
                _registrations = (entries ?? Array.Empty<Entry>())
                    .Select(entry => entry.BeginTerminalObservation())
                    .ToArray();
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
            private CopilotBackgroundShellProcessCompletion? _completion;
            private int _activeTerminalObservationCount;
            private int _terminalObservationWasPendingAtCompletion;
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

            public bool TerminalObservationWasPendingAtCompletion =>
                Volatile.Read(
                    ref _terminalObservationWasPendingAtCompletion) == 1;

            public IDisposable BeginTerminalObservation()
            {
                Interlocked.Increment(
                    ref _activeTerminalObservationCount);
                if (_process.Completion.IsCompleted)
                    CaptureTerminalObservationOwnership();
                return new TerminalObservationRegistration(this);
            }

            private void CaptureTerminalObservationOwnership()
            {
                if (Volatile.Read(ref _activeTerminalObservationCount) > 0)
                {
                    Interlocked.Exchange(
                        ref _terminalObservationWasPendingAtCompletion,
                        1);
                }
            }

            private void EndTerminalObservation()
            {
                Interlocked.Decrement(
                    ref _activeTerminalObservationCount);
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
        }
    }
}
