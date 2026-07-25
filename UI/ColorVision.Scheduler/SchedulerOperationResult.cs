namespace ColorVision.Scheduler
{
    public enum SchedulerOperationError
    {
        None,
        Validation,
        SchedulerUnavailable,
        Conflict,
        NotFound,
        PersistenceFailure,
        QuartzFailure,
    }

    public sealed record SchedulerOperationResult(
        bool Success,
        SchedulerOperationError Error,
        string Message,
        bool Changed = false,
        DateTimeOffset? FirstFireTimeUtc = null)
    {
        public static SchedulerOperationResult Completed(DateTimeOffset? firstFireTimeUtc = null, bool changed = true)
        {
            return new SchedulerOperationResult(true, SchedulerOperationError.None, string.Empty, changed, firstFireTimeUtc);
        }

        public static SchedulerOperationResult Failed(SchedulerOperationError error, string message)
        {
            return new SchedulerOperationResult(false, error, message);
        }
    }
}
