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
            bool terminalObservationWasPendingAtCompletion = false,
            bool terminalResultWasReturned = false)
        {
            Snapshot = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
            TerminalObservationWasPendingAtCompletion =
                terminalObservationWasPendingAtCompletion;
            TerminalResultWasReturned = terminalResultWasReturned;
        }

        public CopilotBackgroundShellCommandSnapshot Snapshot { get; }

        public bool TerminalObservationWasPendingAtCompletion { get; }

        public bool TerminalResultWasReturned { get; }
    }

}
