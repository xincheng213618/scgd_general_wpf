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
                _logger.Info($"Scheduler database initialized: {DbPath}");
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
                int totalCount = 0;
                int pageCount = 0;
                var records = BuildHistoryQuery(db, request)
                    .OrderBy($"{nameof(JobExecutionRecord.StartTime)} DESC, {nameof(JobExecutionRecord.Id)} DESC")
                    .ToPageList(pageIndex, pageSize, ref totalCount, ref pageCount);
                pageCount = totalCount == 0 ? 0 : Math.Max(1, pageCount);

                if (pageCount == 0)
                {
                    pageIndex = 1;
                }
                else if (pageIndex > pageCount)
                {
                    pageIndex = pageCount;
                    records = BuildHistoryQuery(db, request)
                        .OrderBy($"{nameof(JobExecutionRecord.StartTime)} DESC, {nameof(JobExecutionRecord.Id)} DESC")
                        .ToPageList(pageIndex, pageSize);
                }

                int successCount = request.ResultFilter switch
                {
                    JobExecutionResultFilter.Succeeded => totalCount,
                    JobExecutionResultFilter.Failed => 0,
                    _ => BuildHistoryQuery(db, request).Count(record => record.Success),
                };
                int failureCount = totalCount - successCount;
                long totalExecutionTimeMs = totalCount == 0
                    ? 0
                    : BuildHistoryQuery(db, request).Sum(record => record.ExecutionTimeMs);

                var result = new JobExecutionHistoryPage
                {
                    QuerySucceeded = true,
                    Records = records,
                    PageIndex = pageIndex,
                    PageSize = pageSize,
                    TotalCount = totalCount,
                    PageCount = pageCount,
                    SuccessCount = successCount,
                    FailureCount = failureCount,
                    AverageExecutionTimeMs = totalCount == 0 ? 0 : totalExecutionTimeMs / totalCount,
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
                var records = db.Queryable<JobExecutionRecord>()
                    .Where(r => r.JobName == jobName && r.GroupName == groupName)
                    .ToList();

                if (records.Count == 0)
                    return (0, 0, 0, 0, 0, 0, null, null);

                int runCount = records.Count;
                int successCount = records.Count(r => r.Success);
                int failureCount = runCount - successCount;
                long avgMs = (long)records.Average(r => r.ExecutionTimeMs);
                long minMs = records.Min(r => r.ExecutionTimeMs);
                long maxMs = records.Max(r => r.ExecutionTimeMs);

                var last = records.OrderByDescending(r => r.StartTime).First();

                return (runCount, successCount, failureCount, avgMs, minMs, maxMs, last.Result, last.Message);
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to get task stats", ex);
                return (0, 0, 0, 0, 0, 0, null, null);
            }
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
