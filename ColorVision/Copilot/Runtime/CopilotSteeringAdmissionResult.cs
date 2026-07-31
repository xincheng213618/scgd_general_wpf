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
        CopilotSteeringAdmissionReason Reason)
    {
        public bool IsAccepted =>
            Reason == CopilotSteeringAdmissionReason.Accepted;
    }
}
