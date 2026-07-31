using System;

namespace ColorVision.Copilot
{
    public enum CopilotSteeringAdmissionReason
    {
        Accepted,
        InvalidInput,
        PendingUserQuestion,
        NoActiveTask,
        QueueFull,
        RuntimeUnavailable,
    }

    public readonly record struct CopilotSteeringAdmissionResult(
        CopilotSteeringAdmissionReason Reason,
        string MessageId = "")
    {
        public bool IsAccepted =>
            Reason == CopilotSteeringAdmissionReason.Accepted
            && !string.IsNullOrWhiteSpace(MessageId);
    }

    public sealed record CopilotSteeringMessageSnapshot(
        string MessageId,
        string Text);
}
