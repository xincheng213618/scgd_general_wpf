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

        /// <summary>Read-only maintenance guard: does not refresh entries, consume output, or stop work.</summary>
        internal bool HasActiveCommands
        {
            get
            {
                lock (_syncRoot)
                    return _startReservations > 0 || _entries.Any(entry => !entry.Completion.IsCompletedSuccessfully);
            }
        }

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
            var fullExecutablePath = Path.GetFullPath(executablePath);
            var arguments = CopilotShellCommandService.BuildArguments(
                execution.Shell,
                execution.CommandText);
            if (!CopilotShellCommandService.TryBuildSupportedCommandLine(
                    execution.Shell,
                    fullExecutablePath,
                    arguments,
                    out _))
            {
                return StartFailure(
                    CopilotToolFailureKind.Validation,
                    $"The encoded command line cannot exceed {CopilotShellCommandService.GetMaximumCommandLineCharacters(execution.Shell)} characters for {CopilotShellCommandService.GetShellLabel(execution.Shell)}.");
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
                        fullExecutablePath,
                        arguments,
                        execution.WorkingDirectory,
                        TimeSpan.FromSeconds(lifetimeSeconds))
                    {
                        EnvironmentVariables = request.CodexShellEnvironmentPolicy
                            .CreateEnvironmentVariables(request.ConversationId),
                    },
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
                var terminalObservation = await entry
                    .WaitForTerminalObservationOutcomeAsync()
                    .ConfigureAwait(false);
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
                    terminalObservation.WasPendingAtCompletion,
                    terminalObservation.TerminalResultWasReturned);
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

    }
}
