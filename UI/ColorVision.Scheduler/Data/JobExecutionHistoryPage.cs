namespace ColorVision.Scheduler.Data
{
    public enum JobExecutionResultFilter
    {
        All,
        Succeeded,
        Failed,
    }

    public sealed record JobExecutionHistoryRequest(
        string? JobName = null,
        string? GroupName = null,
        JobExecutionResultFilter ResultFilter = JobExecutionResultFilter.All,
        int PageIndex = 1,
        int PageSize = 100);

    public sealed class JobExecutionHistoryPage
    {
        public bool QuerySucceeded { get; init; }

        public string? ErrorMessage { get; init; }

        public IReadOnlyList<JobExecutionRecord> Records { get; init; } = Array.Empty<JobExecutionRecord>();

        public int PageIndex { get; init; } = 1;

        public int PageSize { get; init; }

        public int TotalCount { get; init; }

        public int PageCount { get; init; }

        public int SuccessCount { get; init; }

        public int FailureCount { get; init; }

        public long AverageExecutionTimeMs { get; init; }

        public bool HasPreviousPage => QuerySucceeded && PageIndex > 1;

        public bool HasNextPage => QuerySucceeded && PageIndex < PageCount;
    }
}
