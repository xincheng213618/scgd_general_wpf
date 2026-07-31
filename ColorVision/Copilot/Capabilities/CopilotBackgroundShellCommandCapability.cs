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
    }

    internal sealed record CopilotBackgroundShellProcessCompletion(
        CopilotBackgroundShellCommandState State,
        int? ExitCode,
        DateTimeOffset CompletedAtUtc,
        string StandardOutput,
        string StandardError);

    internal interface ICopilotBackgroundShellProcess : IDisposable
    {
        int ProcessId { get; }

        bool ProcessTreeContained { get; }

        Task<CopilotBackgroundShellProcessCompletion> Completion { get; }

        (string StandardOutput, string StandardError) GetOutputSnapshot();

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

    internal sealed record CopilotBackgroundShellCommandWaitResult(
        CopilotBackgroundShellCommandSnapshot? Snapshot,
        CopilotBackgroundShellCommandObservation Observation,
        TimeSpan Elapsed,
        CopilotToolFailureKind FailureKind,
        string ErrorMessage)
    {
        public bool Success => Snapshot != null
            && FailureKind == CopilotToolFailureKind.None;
    }

    internal sealed class CopilotBackgroundShellCommandCompletedEventArgs : EventArgs
    {
        public CopilotBackgroundShellCommandCompletedEventArgs(
            CopilotBackgroundShellCommandSnapshot snapshot)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public CopilotBackgroundShellCommandSnapshot Snapshot { get; }
    }

    internal sealed class CopilotBackgroundShellCommandRegistry
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
        private static readonly TimeSpan ObservationPollInterval =
            TimeSpan.FromMilliseconds(100);

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
                if (outputPattern.Length > 0
                    && (snapshot.StandardOutput.Contains(
                            outputPattern,
                            StringComparison.OrdinalIgnoreCase)
                        || snapshot.StandardError.Contains(
                            outputPattern,
                            StringComparison.OrdinalIgnoreCase)))
                {
                    return new CopilotBackgroundShellCommandWaitResult(
                        snapshot,
                        CopilotBackgroundShellCommandObservation.OutputMatched,
                        stopwatch.Elapsed,
                        CopilotToolFailureKind.None,
                        string.Empty);
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
                await Task.Delay(
                        remaining <= TimeSpan.Zero
                            ? TimeSpan.Zero
                            : remaining < ObservationPollInterval
                                ? remaining
                                : ObservationPollInterval,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
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
                    _entries.Remove(entry);
            }
            foreach (var entry in removed)
                entry.Dispose();
            return removed.Length;
        }

        public async Task ShutdownAsync()
        {
            Entry[] entries;
            lock (_syncRoot)
            {
                if (_isShuttingDown)
                    return;
                _isShuttingDown = true;
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
                    snapshot);
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

        private static string BoundAndRedactOutput(string? value)
        {
            const string truncationMarker =
                "...<earlier background output truncated>...\n";
            var redacted = CopilotMcpAuditLogger.RedactText(value ?? string.Empty)
                .Replace("\0", string.Empty, StringComparison.Ordinal);
            if (redacted.Length <= MaximumOutputCharacters)
                return redacted;

            var retainedCharacters =
                MaximumOutputCharacters - truncationMarker.Length;
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

        private sealed class Entry : IDisposable
        {
            private readonly ICopilotBackgroundShellProcess _process;
            private CopilotBackgroundShellProcessCompletion? _completion;
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
                    : (completion.StandardOutput, completion.StandardError);
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
                    BoundAndRedactOutput(output.Item1),
                    BoundAndRedactOutput(output.Item2));
            }

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
        }
    }

    internal sealed class CopilotBackgroundShellProcessLauncher : ICopilotBackgroundShellProcessLauncher
    {
        public Task<ICopilotBackgroundShellProcess> StartAsync(
            CopilotShellProcessCommand command,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(command);
            cancellationToken.ThrowIfCancellationRequested();
            var streamEncoding = CopilotShellProcessRunner.GetStreamEncoding(command.Shell);
            var startInfo = new ProcessStartInfo
            {
                FileName = command.ExecutablePath,
                WorkingDirectory = command.WorkingDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = streamEncoding,
                StandardErrorEncoding = streamEncoding,
            };
            foreach (var argument in command.Arguments)
                startInfo.ArgumentList.Add(argument);
            startInfo.Environment["NO_COLOR"] = "1";

            var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };
            try
            {
                if (!process.Start())
                    throw new InvalidOperationException("The background shell process did not start.");
                var processJob = CopilotWindowsProcessJob.TryAssign(process);
                process.StandardInput.Close();
                return Task.FromResult<ICopilotBackgroundShellProcess>(
                    new CopilotBackgroundShellProcess(process, processJob, command.Timeout));
            }
            catch
            {
                process.Dispose();
                throw;
            }
        }
    }

    internal sealed class CopilotBackgroundShellProcess : ICopilotBackgroundShellProcess
    {
        private readonly Process _process;
        private readonly CopilotWindowsProcessJob? _processJob;
        private readonly CancellationTokenSource _outputReadSource = new();
        private readonly BoundedOutput _standardOutput = new();
        private readonly BoundedOutput _standardError = new();
        private readonly Task<string> _standardOutputTask;
        private readonly Task<string> _standardErrorTask;
        private readonly Task<CopilotBackgroundShellProcessCompletion> _completion;
        private int _terminationReason;
        private int _disposed;

        public CopilotBackgroundShellProcess(
            Process process,
            CopilotWindowsProcessJob? processJob,
            TimeSpan maximumLifetime)
        {
            _process = process ?? throw new ArgumentNullException(nameof(process));
            _processJob = processJob;
            ProcessId = process.Id;
            ProcessTreeContained = processJob != null;
            _standardOutputTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardOutput,
                CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters,
                0,
                "\n...<earlier background output truncated>...\n",
                _outputReadSource.Token,
                _standardOutput.Append);
            _standardErrorTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardError,
                CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters,
                0,
                "\n...<earlier background error output truncated>...\n",
                _outputReadSource.Token,
                _standardError.Append);
            _completion = MonitorAsync(maximumLifetime);
        }

        public int ProcessId { get; }

        public bool ProcessTreeContained { get; }

        public Task<CopilotBackgroundShellProcessCompletion> Completion => _completion;

        public (string StandardOutput, string StandardError) GetOutputSnapshot() =>
            (_standardOutput.Snapshot(), _standardError.Snapshot());

        public async Task<CopilotBackgroundShellProcessCompletion> StopAsync(
            CancellationToken cancellationToken)
        {
            Interlocked.CompareExchange(ref _terminationReason, 1, 0);
            await CopilotProcessExecutionSupport.TerminateProcessTreeAsync(_process, _processJob)
                .ConfigureAwait(false);
            return await _completion.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        private async Task<CopilotBackgroundShellProcessCompletion> MonitorAsync(
            TimeSpan maximumLifetime)
        {
            try
            {
                var processExit = _process.WaitForExitAsync();
                var lifetime = Task.Delay(maximumLifetime);
                if (await Task.WhenAny(processExit, lifetime).ConfigureAwait(false) == lifetime)
                {
                    Interlocked.CompareExchange(ref _terminationReason, 2, 0);
                    await CopilotProcessExecutionSupport.TerminateProcessTreeAsync(_process, _processJob)
                        .ConfigureAwait(false);
                }
                else
                {
                    await processExit.ConfigureAwait(false);
                    await CopilotProcessExecutionSupport.TerminateProcessTreeAsync(_process, _processJob)
                        .ConfigureAwait(false);
                }

                var (standardOutput, standardError) =
                    await CopilotProcessExecutionSupport.DrainOutputAsync(
                        _standardOutputTask,
                        _standardErrorTask,
                        _outputReadSource,
                        _process.StandardOutput,
                        _process.StandardError).ConfigureAwait(false);
                _standardOutput.Replace(standardOutput);
                _standardError.Replace(standardError);
                var exitCode = TryGetExitCode(_process);
                var reason = Volatile.Read(ref _terminationReason);
                var state = reason switch
                {
                    1 => CopilotBackgroundShellCommandState.Stopped,
                    2 => CopilotBackgroundShellCommandState.Expired,
                    _ when exitCode == 0 => CopilotBackgroundShellCommandState.Completed,
                    _ => CopilotBackgroundShellCommandState.Failed,
                };
                return new CopilotBackgroundShellProcessCompletion(
                    state,
                    exitCode,
                    DateTimeOffset.UtcNow,
                    _standardOutput.Snapshot(),
                    _standardError.Snapshot());
            }
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or ObjectDisposedException)
            {
                _standardError.Append(CopilotMcpAuditLogger.RedactText(ex.Message));
                return new CopilotBackgroundShellProcessCompletion(
                    Volatile.Read(ref _terminationReason) == 1
                        ? CopilotBackgroundShellCommandState.Stopped
                        : CopilotBackgroundShellCommandState.Failed,
                    TryGetExitCode(_process),
                    DateTimeOffset.UtcNow,
                    _standardOutput.Snapshot(),
                    _standardError.Snapshot());
            }
        }

        private static int? TryGetExitCode(Process process)
        {
            try
            {
                return process.HasExited ? process.ExitCode : null;
            }
            catch (Exception ex) when (ex is InvalidOperationException or ObjectDisposedException or Win32Exception)
            {
                return null;
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 1)
                return;

            Interlocked.CompareExchange(ref _terminationReason, 1, 0);
            _processJob?.TryTerminate();
            _processJob?.Dispose();
            _outputReadSource.Cancel();
            _outputReadSource.Dispose();
            _process.Dispose();
        }

        private sealed class BoundedOutput
        {
            private readonly object _syncRoot = new();
            private readonly StringBuilder _buffer = new();

            public void Append(string? value)
            {
                var redacted = CopilotMcpAuditLogger.RedactText(
                    (value ?? string.Empty).Replace("\0", string.Empty, StringComparison.Ordinal));
                if (redacted.Length == 0)
                    return;

                lock (_syncRoot)
                {
                    _buffer.Append(redacted);
                    if (_buffer.Length > CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters)
                    {
                        _buffer.Remove(
                            0,
                            _buffer.Length - CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters);
                    }
                }
            }

            public void Replace(string? value)
            {
                lock (_syncRoot)
                {
                    _buffer.Clear();
                    Append(value);
                }
            }

            public string Snapshot()
            {
                lock (_syncRoot)
                    return _buffer.ToString();
            }
        }
    }
}
