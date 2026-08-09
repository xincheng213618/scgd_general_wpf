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
            if (command.EnvironmentVariables != null)
            {
                startInfo.Environment.Clear();
                foreach (var pair in command.EnvironmentVariables)
                    startInfo.Environment[pair.Key] = pair.Value;
            }
            startInfo.Environment["NO_COLOR"] = "1";
            foreach (var name in startInfo.Environment.Keys
                .Where(CopilotCodexShellEnvironmentPolicy.IsNonInheritableEnvironmentVariable)
                .ToArray())
            {
                startInfo.Environment.Remove(name);
            }

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
        private readonly BoundedOutput _standardOutput;
        private readonly BoundedOutput _standardError;
        private readonly Task<string> _standardOutputTask;
        private readonly Task<string> _standardErrorTask;
        private readonly Task<CopilotBackgroundShellProcessCompletion> _completion;
        private readonly object _observationSignalSyncRoot = new();
        private TaskCompletionSource _observationChanged =
            CreateObservationChangedSource();
        private long _observationVersion;
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
            _standardOutput = new BoundedOutput("stdout");
            _standardError = new BoundedOutput("stderr");
            _standardOutputTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardOutput,
                CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters,
                0,
                "\n...<earlier background output truncated>...\n",
                _outputReadSource.Token,
                value => AppendOutput(_standardOutput, value));
            _standardErrorTask = CopilotProcessExecutionSupport.ReadBoundedAsync(
                process.StandardError,
                CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters,
                0,
                "\n...<earlier background error output truncated>...\n",
                _outputReadSource.Token,
                value => AppendOutput(_standardError, value));
            _completion = MonitorAsync(maximumLifetime);
            _ = _completion.ContinueWith(
                static (_, state) =>
                    ((CopilotBackgroundShellProcess)state!)
                        .SignalObservationChanged(),
                this,
                CancellationToken.None,
                TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
        }

        public int ProcessId { get; }

        public bool ProcessTreeContained { get; }

        public Task<CopilotBackgroundShellProcessCompletion> Completion => _completion;

        public CopilotBackgroundShellProcessOutput GetOutputSnapshot()
        {
            var standardOutput = _standardOutput.Snapshot();
            var standardError = _standardError.Snapshot();
            return new CopilotBackgroundShellProcessOutput(
                standardOutput.Text,
                standardError.Text,
                standardOutput.ObservedCharacters,
                standardError.ObservedCharacters,
                standardOutput.WasTruncated,
                standardError.WasTruncated,
                standardOutput.ArchiveAvailable,
                standardError.ArchiveAvailable,
                standardOutput.ArchivedCharacters,
                standardError.ArchivedCharacters,
                standardOutput.ArchiveTruncated,
                standardError.ArchiveTruncated)
            {
                ObservationVersion =
                    Volatile.Read(ref _observationVersion),
            };
        }

        public CopilotRedactedOutputArchivePage ReadOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            int offsetCharacters,
            int maximumCharacters,
            CancellationToken cancellationToken) =>
            (stream == CopilotBackgroundShellOutputStream.StandardError
                ? _standardError
                : _standardOutput).ReadArchive(
                    offsetCharacters,
                    maximumCharacters,
                    cancellationToken);

        public CopilotRedactedOutputArchiveSearchResult SearchOutputArchive(
            CopilotBackgroundShellOutputStream stream,
            string literal,
            int offsetCharacters,
            CancellationToken cancellationToken) =>
            (stream == CopilotBackgroundShellOutputStream.StandardError
                ? _standardError
                : _standardOutput).SearchArchive(
                    literal,
                    offsetCharacters,
                    cancellationToken);

        public async Task WaitForObservationChangeAsync(
            long observationVersion,
            TimeSpan timeout,
            CancellationToken cancellationToken)
        {
            ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
                timeout,
                TimeSpan.Zero);
            Task notification;
            lock (_observationSignalSyncRoot)
            {
                if (_observationVersion != observationVersion
                    || _completion.IsCompleted)
                {
                    return;
                }
                notification = _observationChanged.Task;
            }

            try
            {
                await notification.WaitAsync(timeout, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
        }

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
                _standardOutput.ReplacePreview(standardOutput);
                _standardError.ReplacePreview(standardError);
                _standardOutput.CompleteArchive();
                _standardError.CompleteArchive();
                var exitCode = TryGetExitCode(_process);
                var reason = Volatile.Read(ref _terminationReason);
                var state = reason switch
                {
                    1 => CopilotBackgroundShellCommandState.Stopped,
                    2 => CopilotBackgroundShellCommandState.Expired,
                    _ when exitCode == 0 => CopilotBackgroundShellCommandState.Completed,
                    _ => CopilotBackgroundShellCommandState.Failed,
                };
                var output = GetOutputSnapshot();
                return new CopilotBackgroundShellProcessCompletion(
                    state,
                    exitCode,
                    DateTimeOffset.UtcNow,
                    output.StandardOutput,
                    output.StandardError)
                {
                    ObservedStandardOutputCharacters =
                        output.ObservedStandardOutputCharacters,
                    ObservedStandardErrorCharacters =
                        output.ObservedStandardErrorCharacters,
                    StandardOutputTruncated =
                        output.StandardOutputTruncated,
                    StandardErrorTruncated =
                        output.StandardErrorTruncated,
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
            catch (Exception ex) when (ex is IOException or Win32Exception or InvalidOperationException or ObjectDisposedException)
            {
                AppendOutput(
                    _standardError,
                    CopilotMcpAuditLogger.RedactText(ex.Message));
                _standardOutput.CompleteArchive();
                _standardError.CompleteArchive();
                var output = GetOutputSnapshot();
                return new CopilotBackgroundShellProcessCompletion(
                    Volatile.Read(ref _terminationReason) == 1
                        ? CopilotBackgroundShellCommandState.Stopped
                        : CopilotBackgroundShellCommandState.Failed,
                    TryGetExitCode(_process),
                    DateTimeOffset.UtcNow,
                    output.StandardOutput,
                    output.StandardError)
                {
                    ObservedStandardOutputCharacters =
                        output.ObservedStandardOutputCharacters,
                    ObservedStandardErrorCharacters =
                        output.ObservedStandardErrorCharacters,
                    StandardOutputTruncated =
                        output.StandardOutputTruncated,
                    StandardErrorTruncated =
                        output.StandardErrorTruncated,
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
        }

        private void AppendOutput(
            BoundedOutput output,
            string? value)
        {
            if (string.IsNullOrEmpty(value))
                return;

            output.Append(value);
            SignalObservationChanged();
        }

        private void SignalObservationChanged()
        {
            TaskCompletionSource notification;
            lock (_observationSignalSyncRoot)
            {
                if (_observationVersion < long.MaxValue)
                    _observationVersion++;
                notification = _observationChanged;
                _observationChanged = CreateObservationChangedSource();
            }
            notification.TrySetResult();
        }

        private static TaskCompletionSource CreateObservationChangedSource() =>
            new(TaskCreationOptions.RunContinuationsAsynchronously);

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

            SignalObservationChanged();
            Interlocked.CompareExchange(ref _terminationReason, 1, 0);
            _processJob?.TryTerminate();
            _processJob?.Dispose();
            _outputReadSource.Cancel();
            _outputReadSource.Dispose();
            _standardOutput.Dispose();
            _standardError.Dispose();
            _process.Dispose();
        }

        private sealed class BoundedOutput : IDisposable
        {
            private readonly object _syncRoot = new();
            private readonly StringBuilder _buffer = new();
            private readonly CopilotTemporaryRedactedOutputArchive? _archive;
            private long _observedCharacters;
            private bool _wasTruncated;

            public BoundedOutput(string streamLabel)
            {
                _archive = CopilotTemporaryRedactedOutputArchive.TryCreate(
                    "BackgroundOutput",
                    streamLabel);
            }

            public void Append(string? value)
            {
                var observed = value ?? string.Empty;
                if (observed.Length == 0)
                    return;
                var redacted = RedactPreview(observed);

                lock (_syncRoot)
                {
                    _observedCharacters = SaturatingAdd(
                        _observedCharacters,
                        observed.Length);
                    if (redacted.Length > 0)
                        AppendPreviewUnderLock(redacted);
                    _archive?.Append(observed);
                }
            }

            public void ReplacePreview(string? value)
            {
                var redacted = RedactPreview(value ?? string.Empty);
                lock (_syncRoot)
                {
                    _buffer.Clear();
                    AppendPreviewUnderLock(redacted);
                }
            }

            public BoundedOutputSnapshot Snapshot()
            {
                lock (_syncRoot)
                {
                    return new BoundedOutputSnapshot(
                        _buffer.ToString(),
                        _observedCharacters,
                        _wasTruncated,
                        _archive?.Available == true,
                        _archive?.ArchivedCharacters ?? 0,
                        _archive?.IsTruncated == true);
                }
            }

            public CopilotRedactedOutputArchivePage ReadArchive(
                int offsetCharacters,
                int maximumCharacters,
                CancellationToken cancellationToken) =>
                _archive?.Read(
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
                        "The temporary redacted output archive is unavailable.");

            public CopilotRedactedOutputArchiveSearchResult SearchArchive(
                string literal,
                int offsetCharacters,
                CancellationToken cancellationToken) =>
                _archive?.Search(
                    literal,
                    offsetCharacters,
                    cancellationToken)
                ?? new CopilotRedactedOutputArchiveSearchResult(
                    Available: false,
                    Matched: false,
                    NextOffsetCharacters: offsetCharacters,
                    ArchivedCharacters: 0,
                    ArchiveTruncated: false,
                    ErrorMessage:
                        "The temporary redacted output archive is unavailable.");

            public void CompleteArchive() => _archive?.Complete();

            private void AppendPreviewUnderLock(string value)
            {
                _buffer.Append(value);
                if (_buffer.Length
                    <= CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters)
                {
                    return;
                }

                _buffer.Remove(
                    0,
                    _buffer.Length
                    - CopilotBackgroundShellCommandRegistry.MaximumOutputCharacters);
                _wasTruncated = true;
            }

            private static string RedactPreview(string value) =>
                CopilotMcpAuditLogger.RedactText(
                    value.Replace("\0", string.Empty, StringComparison.Ordinal));

            private static long SaturatingAdd(long value, int increment) =>
                value > long.MaxValue - increment
                    ? long.MaxValue
                    : value + increment;

            public void Dispose() => _archive?.Dispose();

            public readonly record struct BoundedOutputSnapshot(
                string Text,
                long ObservedCharacters,
                bool WasTruncated,
                bool ArchiveAvailable,
                int ArchivedCharacters,
                bool ArchiveTruncated);
        }
    }
}
