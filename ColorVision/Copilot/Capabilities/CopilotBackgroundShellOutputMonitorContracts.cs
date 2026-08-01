using ColorVision.Copilot.Mcp;
using System;

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
}
