#pragma warning disable CA1822
using log4net;
using SqlSugar;
using System.IO;
using ColorVision.UI;

namespace ColorVision.Scheduler.Data
{
    public class SchedulerDbManager
    {
        private static readonly ILog _logger = LogManager.GetLogger(typeof(SchedulerDbManager));
        private static SchedulerDbManager? _instance;
        private static readonly object _locker = new();

        public static SchedulerDbManager GetInstance()
        {
            lock (_locker) { return _instance ??= new SchedulerDbManager(); }
        }

        private static readonly string DbDirectory = Environments.DirStateScheduler;

        public static string DbPath { get; } = Path.Combine(DbDirectory, "SchedulerHistory.db");

        private SchedulerDbManager()
        {
            Initialize();
        }

        private void Initialize()
        {
            try
            {
                if (!Directory.Exists(DbDirectory))
                    Directory.CreateDirectory(DbDirectory);

                using var db = CreateClient();
                db.CodeFirst.InitTables<JobExecutionRecord>();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to initialize scheduler database", ex);
            }
        }

        public static SqlSugarClient CreateClient()
        {
            return new SqlSugarClient(new ConnectionConfig
            {
                ConnectionString = $"Data Source={DbPath}",
                DbType = DbType.Sqlite,
                IsAutoCloseConnection = true
            });
        }

        /// <summary>
        /// 查询执行历史、分页信息和统计摘要。所有结果使用同一组任务和结果筛选条件。
        /// </summary>
        public JobExecutionHistoryPage QueryExecutionHistory(JobExecutionHistoryRequest request)
        {
            ArgumentNullException.ThrowIfNull(request);

            try
            {
                using var db = CreateClient();
                return QueryExecutionHistory(db, request);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to create scheduler history database client", ex);
                return new JobExecutionHistoryPage
                {
                    QuerySucceeded = false,
                    ErrorMessage = ex.Message,
                    PageIndex = Math.Max(1, request.PageIndex),
                    PageSize = Math.Clamp(request.PageSize, 1, 1000),
                };
            }
        }

        /// <summary>
        /// 使用指定数据库连接查询执行历史。该入口也用于隔离数据库的自动化测试。
        /// </summary>
        public static JobExecutionHistoryPage QueryExecutionHistory(SqlSugarClient db, JobExecutionHistoryRequest request)
        {
            ArgumentNullException.ThrowIfNull(db);
            ArgumentNullException.ThrowIfNull(request);

            int pageIndex = Math.Max(1, request.PageIndex);
            int pageSize = Math.Clamp(request.PageSize, 1, 1000);
            bool transactionStarted = false;

            try
            {
                db.Ado.BeginTran();
                transactionStarted = true;
                var summary = BuildHistoryQuery(db, request)
                    .Select(record => new ExecutionHistoryAggregate
                    {
                        TotalCount = SqlFunc.AggregateCount(record.Id),
                        SuccessCount = SqlFunc.IsNull(SqlFunc.AggregateSum(record.Success ? 1 : 0), 0),
                        TotalExecutionTimeMs = SqlFunc.IsNull(SqlFunc.AggregateSum(record.ExecutionTimeMs), 0L),
                    })
                    .First();
                int totalCount = summary.TotalCount;
                int pageCount = totalCount == 0 ? 0 : (totalCount - 1) / pageSize + 1;
                pageIndex = pageCount == 0 ? 1 : Math.Min(pageIndex, pageCount);
                IReadOnlyList<JobExecutionRecord> records = totalCount == 0
                    ? Array.Empty<JobExecutionRecord>()
                    : BuildHistoryQuery(db, request)
                        .OrderBy($"{nameof(JobExecutionRecord.StartTime)} DESC, {nameof(JobExecutionRecord.Id)} DESC")
                        .ToPageList(pageIndex, pageSize);

                var result = new JobExecutionHistoryPage
                {
                    QuerySucceeded = true,
                    Records = records,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    PageCount = pageCount,
                    SuccessCount = summary.SuccessCount,
                    FailureCount = totalCount - summary.SuccessCount,
                    AverageExecutionTimeMs = totalCount == 0 ? 0 : summary.TotalExecutionTimeMs / totalCount,
                };
                db.Ado.CommitTran();
                transactionStarted = false;
                return result;
            }
            catch (Exception ex)
            {
                if (transactionStarted)
                {
                    try
                    {
                        db.Ado.RollbackTran();
                    }
                    catch (Exception rollbackException)
                    {
                        _logger.Warn("Failed to roll back scheduler history read transaction", rollbackException);
                    }
                }
                _logger.Error("Failed to query execution history", ex);
                return new JobExecutionHistoryPage
                {
                    QuerySucceeded = false,
                    ErrorMessage = ex.Message,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                };
            }
        }

        private sealed class ExecutionHistoryAggregate
        {
            public int TotalCount { get; set; }
            public int SuccessCount { get; set; }
            public long TotalExecutionTimeMs { get; set; }
        }

        private static ISugarQueryable<JobExecutionRecord> BuildHistoryQuery(
            SqlSugarClient db,
            JobExecutionHistoryRequest request)
        {
            var query = db.Queryable<JobExecutionRecord>();

            if (!string.IsNullOrWhiteSpace(request.JobName))
                query = query.Where(record => record.JobName == request.JobName);
            if (!string.IsNullOrWhiteSpace(request.GroupName))
                query = query.Where(record => record.GroupName == request.GroupName);

            return request.ResultFilter switch
            {
                JobExecutionResultFilter.Succeeded => query.Where(record => record.Success),
                JobExecutionResultFilter.Failed => query.Where(record => !record.Success),
                _ => query,
            };
        }

        /// <summary>
        /// 插入一条执行记录
        /// </summary>
        public void InsertRecord(JobExecutionRecord record)
        {
            try
            {
                using var db = CreateClient();
                db.Insertable(record).ExecuteCommand();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to insert execution record", ex);
            }
        }

        /// <summary>
        /// 查询指定任务的执行历史（按时间倒序）
        /// </summary>
        public List<JobExecutionRecord> QueryRecords(string jobName, string groupName, int pageIndex = 1, int pageSize = 100)
        {
            try
            {
                using var db = CreateClient();
                return db.Queryable<JobExecutionRecord>()
                    .Where(r => r.JobName == jobName && r.GroupName == groupName)
                    .OrderByDescending(r => r.StartTime)
                    .ToPageList(pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to query execution records", ex);
                return new List<JobExecutionRecord>();
            }
        }

        /// <summary>
        /// 查询所有执行记录（按时间倒序）
        /// </summary>
        public List<JobExecutionRecord> QueryAllRecords(int pageIndex = 1, int pageSize = 200)
        {
            try
            {
                using var db = CreateClient();
                return db.Queryable<JobExecutionRecord>()
                    .OrderByDescending(r => r.StartTime)
                    .ToPageList(pageIndex, pageSize);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to query all execution records", ex);
                return new List<JobExecutionRecord>();
            }
        }

        /// <summary>
        /// 获取指定任务的聚合统计信息（用于重启后恢复）
        /// </summary>
        public (int RunCount, int SuccessCount, int FailureCount, long AvgMs, long MinMs, long MaxMs, string? LastResult, string? LastMessage)
            GetTaskStats(string jobName, string groupName)
        {
            try
            {
                using var db = CreateClient();
                return ReadTaskStats(db, jobName, groupName);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get task stats", ex);
                return (0, 0, 0, 0, 0, 0, null, null);
            }
        }

        internal static (int RunCount, int SuccessCount, int FailureCount, long AvgMs, long MinMs, long MaxMs, string? LastResult, string? LastMessage)
            ReadTaskStats(SqlSugarClient db, string jobName, string groupName)
        {
            // One statement keeps the counters and the latest execution in the same SQLite snapshot.
            var stats = db.Ado.SqlQuery<TaskStatsRow>("""
                SELECT summary.*, latest.Result AS LastResult, latest.Message AS LastMessage
                FROM (
                    SELECT COUNT(*) AS RunCount,
                           COALESCE(SUM(CASE WHEN Success THEN 1 ELSE 0 END), 0) AS SuccessCount,
                           COALESCE(SUM(ExecutionTimeMs), 0) AS TotalMs,
                           COALESCE(MIN(ExecutionTimeMs), 0) AS MinMs,
                           COALESCE(MAX(ExecutionTimeMs), 0) AS MaxMs
                    FROM job_execution_record
                    WHERE JobName = @jobName AND GroupName = @groupName
                ) AS summary
                LEFT JOIN job_execution_record AS latest ON latest.Id = (
                    SELECT Id FROM job_execution_record
                    WHERE JobName = @jobName AND GroupName = @groupName
                    ORDER BY StartTime DESC, Id ASC
                    LIMIT 1
                )
                """, new SugarParameter("@jobName", jobName), new SugarParameter("@groupName", groupName)).Single();

            long average = stats.RunCount == 0 ? 0 : (long)((double)stats.TotalMs / stats.RunCount);
            return (stats.RunCount, stats.SuccessCount, stats.RunCount - stats.SuccessCount,
                average, stats.MinMs, stats.MaxMs, stats.LastResult, stats.LastMessage);
        }

        private sealed class TaskStatsRow
        {
            public int RunCount { get; set; }
            public int SuccessCount { get; set; }
            public long TotalMs { get; set; }
            public long MinMs { get; set; }
            public long MaxMs { get; set; }
            public string? LastResult { get; set; }
            public string? LastMessage { get; set; }
        }

        /// <summary>
        /// 清理指定天数之前的历史记录
        /// </summary>
        public int CleanupOldRecords(int keepDays = 90)
        {
            try
            {
                var cutoff = DateTime.Now.AddDays(-keepDays);
                using var db = CreateClient();
                return db.Deleteable<JobExecutionRecord>()
                    .Where(r => r.StartTime < cutoff)
                    .ExecuteCommand();
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to cleanup old records", ex);
                return 0;
            }
        }
    }
}
