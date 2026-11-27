using ColorVision.Scheduler.Data;
using SqlSugar;
using System.IO;

namespace ColorVision.UI.Tests;

public sealed class SchedulerHistoryQueryTests : IDisposable
{
    private readonly List<SqlSugarClient> _clients = [];
    private readonly string _temporaryDirectory = Path.Combine(
        Path.GetTempPath(),
        nameof(SchedulerHistoryQueryTests),
        Guid.NewGuid().ToString("N"));

    [Fact]
    public void QueryExecutionHistory_AppliesResultFilterBeforePagingAndStatistics()
    {
        using var db = CreateDatabase();
        DateTime now = new(2026, 7, 26, 12, 0, 0);
        var records = Enumerable.Range(0, 120)
            .Select(index => new JobExecutionRecord
            {
                JobName = "Camera",
                GroupName = "Acquisition",
                StartTime = now.AddMinutes(-index),
                EndTime = now.AddMinutes(-index).AddMilliseconds(index < 110 ? 100 : 200),
                ExecutionTimeMs = index < 110 ? 100 : 200,
                Success = index < 110,
                Result = index < 110 ? "Success" : "Failed",
            })
            .ToList();
        db.Insertable(records).ExecuteCommand();

        var result = SchedulerDbManager.QueryExecutionHistory(
            db,
            new JobExecutionHistoryRequest(
                ResultFilter: JobExecutionResultFilter.Failed,
                PageIndex: 1,
                PageSize: 5));

        Assert.True(result.QuerySucceeded, result.ErrorMessage);
        Assert.Equal(10, result.TotalCount);
        Assert.Equal(2, result.PageCount);
        Assert.Equal(0, result.SuccessCount);
        Assert.Equal(10, result.FailureCount);
        Assert.Equal(200L, result.AverageExecutionTimeMs);
        Assert.Equal(5, result.Records.Count);
        Assert.All(result.Records, record => Assert.False(record.Success));
        Assert.False(result.HasPreviousPage);
        Assert.True(result.HasNextPage);
    }

    [Fact]
    public void QueryExecutionHistory_ClampsPastLastPageAndClearsEmptyStatistics()
    {
        using var db = CreateDatabase();
        DateTime now = new(2026, 7, 26, 12, 0, 0);
        db.Insertable(Enumerable.Range(0, 3).Select(index => new JobExecutionRecord
        {
            JobName = "Flow",
            GroupName = "Production",
            StartTime = now.AddMinutes(-index),
            EndTime = now.AddMinutes(-index).AddMilliseconds(50),
            ExecutionTimeMs = 50,
            Success = true,
            Result = "Success",
        }).ToList()).ExecuteCommand();

        var lastPage = SchedulerDbManager.QueryExecutionHistory(
            db,
            new JobExecutionHistoryRequest(PageIndex: 99, PageSize: 2));

        Assert.True(lastPage.QuerySucceeded, lastPage.ErrorMessage);
        Assert.Equal(2, lastPage.PageIndex);
        Assert.Equal(2, lastPage.PageCount);
        Assert.Equal(3, lastPage.TotalCount);
        Assert.Single(lastPage.Records);
        Assert.True(lastPage.HasPreviousPage);
        Assert.False(lastPage.HasNextPage);

        var empty = SchedulerDbManager.QueryExecutionHistory(
            db,
            new JobExecutionHistoryRequest(
                JobName: "Missing",
                GroupName: "Missing",
                PageIndex: 4,
                PageSize: 2));

        Assert.True(empty.QuerySucceeded, empty.ErrorMessage);
        Assert.Equal(1, empty.PageIndex);
        Assert.Equal(0, empty.PageCount);
        Assert.Equal(0, empty.TotalCount);
        Assert.Equal(0, empty.SuccessCount);
        Assert.Equal(0, empty.FailureCount);
        Assert.Equal(0L, empty.AverageExecutionTimeMs);
        Assert.Empty(empty.Records);
        Assert.False(empty.HasPreviousPage);
        Assert.False(empty.HasNextPage);
    }

    [Fact]
    public void QueryExecutionHistory_CombinesScopedCountsAndLongDurationsBeforePaging()
    {
        using var db = CreateDatabase();
        DateTime now = new(2026, 7, 26, 12, 0, 0);
        db.Insertable(new[]
        {
            new JobExecutionRecord { JobName = "Camera", GroupName = "Acquisition", StartTime = now, EndTime = now, ExecutionTimeMs = 3_000_000_000L, Success = false },
            new JobExecutionRecord { JobName = "Camera", GroupName = "Acquisition", StartTime = now, EndTime = now, ExecutionTimeMs = 5_000_000_003L, Success = true },
            new JobExecutionRecord { JobName = "Camera", GroupName = "Other", StartTime = now, EndTime = now, ExecutionTimeMs = 25L, Success = true },
        }).ExecuteCommand();

        var result = SchedulerDbManager.QueryExecutionHistory(
            db, new JobExecutionHistoryRequest(JobName: "Camera", GroupName: "Acquisition", PageIndex: int.MaxValue, PageSize: 1));

        Assert.True(result.QuerySucceeded, result.ErrorMessage);
        Assert.Equal(2, result.TotalCount);
        Assert.Equal(1, result.SuccessCount);
        Assert.Equal(1, result.FailureCount);
        Assert.Equal(4_000_000_001L, result.AverageExecutionTimeMs);
        Assert.Equal(2, result.PageIndex);
        Assert.False(Assert.Single(result.Records).Success);
    }

    [Fact]
    public void QueryExecutionHistory_ReturnsExplicitFailureForDatabaseErrors()
    {
        string databasePath = Path.Combine(_temporaryDirectory, "missing-parent", "history.db");
        using var db = CreateClient(databasePath);

        var result = SchedulerDbManager.QueryExecutionHistory(
            db,
            new JobExecutionHistoryRequest());

        Assert.False(result.QuerySucceeded);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Empty(result.Records);
    }

    [Fact]
    public void QueryExecutionHistory_UsesIdAsStableTieBreakerForEqualStartTimes()
    {
        using var db = CreateDatabase();
        DateTime startTime = new(2026, 7, 26, 12, 0, 0);
        db.Insertable(Enumerable.Range(1, 3).Select(index => new JobExecutionRecord
        {
            JobName = $"Job-{index}",
            GroupName = "StableOrder",
            StartTime = startTime,
            EndTime = startTime.AddMilliseconds(index),
            ExecutionTimeMs = index,
            Success = true,
        }).ToList()).ExecuteCommand();

        JobExecutionHistoryPage firstPage = SchedulerDbManager.QueryExecutionHistory(
            db,
            new JobExecutionHistoryRequest(PageIndex: 1, PageSize: 2));
        JobExecutionHistoryPage secondPage = SchedulerDbManager.QueryExecutionHistory(
            db,
            new JobExecutionHistoryRequest(PageIndex: 2, PageSize: 2));

        Assert.Equal([3, 2], firstPage.Records.Select(record => record.Id));
        Assert.Equal([1], secondPage.Records.Select(record => record.Id));
    }

    [Fact]
    public void ReadTaskStats_AggregatesOnlyTheRequestedIdentityAndPreservesEarlierIdOnTimeTies()
    {
        using var db = CreateDatabase();
        DateTime now = new(2026, 7, 26, 12, 0, 0);
        db.Insertable(new[]
        {
            new JobExecutionRecord { JobName = "Camera", GroupName = "A", StartTime = now, EndTime = now, ExecutionTimeMs = 5_000_000_003L, Success = false, Message = "First at latest time" },
            new JobExecutionRecord { JobName = "Camera", GroupName = "A", StartTime = now.AddMinutes(-1), EndTime = now, ExecutionTimeMs = 100, Success = true, Result = "Older" },
            new JobExecutionRecord { JobName = "Camera", GroupName = "A", StartTime = now, EndTime = now, ExecutionTimeMs = 3_000_000_000L, Success = true, Result = "Second at latest time" },
            new JobExecutionRecord { JobName = "Camera", GroupName = "B", StartTime = now.AddMinutes(1), EndTime = now, ExecutionTimeMs = 5, Success = true },
        }).ExecuteCommand();

        var stats = SchedulerDbManager.ReadTaskStats(db, "Camera", "A");
        Assert.Equal((3, 2, 1, 2_666_666_701L, 100L, 5_000_000_003L, (string?)null, "First at latest time"), stats);

        var empty = SchedulerDbManager.ReadTaskStats(db, "Camera", "Missing");
        Assert.Equal((0, 0, 0, 0L, 0L, 0L, (string?)null, (string?)null), empty);
    }

    private SqlSugarClient CreateDatabase()
    {
        Directory.CreateDirectory(_temporaryDirectory);
        string databasePath = Path.Combine(_temporaryDirectory, $"{Guid.NewGuid():N}.db");
        var db = CreateClient(databasePath);
        db.CodeFirst.InitTables<JobExecutionRecord>();
        return db;
    }

    private SqlSugarClient CreateClient(string databasePath)
    {
        var db = new SqlSugarClient(new ConnectionConfig
        {
            ConnectionString = $"Data Source={databasePath};Pooling=False",
            DbType = DbType.Sqlite,
            IsAutoCloseConnection = true,
        });
        _clients.Add(db);
        return db;
    }

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Close();
            client.Dispose();
        }

        if (Directory.Exists(_temporaryDirectory))
            Directory.Delete(_temporaryDirectory, recursive: true);
    }
}
