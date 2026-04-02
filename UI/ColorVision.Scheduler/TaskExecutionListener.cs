
using ColorVision.Scheduler.Data;
using log4net;
using log4net.Repository.Hierarchy;
using Quartz;
using Quartz.Listener;
using System.Diagnostics;

namespace ColorVision.Scheduler
{
    public class TaskExecutionListener : JobListenerSupport
    {
        private readonly QuartzSchedulerManager _schedulerManager;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(TaskExecutionListener));
        private readonly Dictionary<string, Stopwatch> _executionTimers = new();
        private readonly Dictionary<string, DateTime> _executionStartTimes = new();

        public TaskExecutionListener(QuartzSchedulerManager schedulerManager)
        {
            _schedulerManager = schedulerManager;
        }

        public override string Name => "TaskExecutionListener";

        public event Action<IJobExecutionContext>? JobExecutedEvent;

        public override Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            // 任务即将执行，更新状态为 Running 并启动计时器
            var jobKey = context.JobDetail.Key;
            var taskInfo = _schedulerManager.TaskInfos.FirstOrDefault(t => t.JobName == jobKey.Name && t.GroupName == jobKey.Group);
            if (taskInfo != null)
            {
                taskInfo.Status = SchedulerStatus.Running;
                _logger.Info($"Job starting: {jobKey.Name}({jobKey.Group})");
                
                // 启动计时器
                var timerKey = $"{jobKey.Name}_{jobKey.Group}";
                var stopwatch = Stopwatch.StartNew();
                lock (_executionTimers)
                {
                    _executionTimers[timerKey] = stopwatch;
                    _executionStartTimes[timerKey] = DateTime.Now;
                }
            }
            return base.JobToBeExecuted(context, cancellationToken);
        }

        public override Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            base.JobWasExecuted(context, jobException, cancellationToken);

            var jobKey = context.JobDetail.Key;

            // 优化：尝试直接从 JobDataMap 获取 SchedulerInfo
            SchedulerInfo? taskInfo = context.JobDetail.JobDataMap["SchedulerInfo"] as SchedulerInfo;

            if (taskInfo != null)
            {
                taskInfo.RunCount++;
                taskInfo.Status = SchedulerStatus.Ready;
                
                // 停止计时器并更新统计信息
                var timerKey = $"{jobKey.Name}_{jobKey.Group}";
                long executionTimeMs = 0;
                DateTime startTime = DateTime.Now;
                lock (_executionTimers)
                {
                    if (_executionTimers.TryGetValue(timerKey, out var stopwatch))
                    {
                        stopwatch.Stop();
                        executionTimeMs = stopwatch.ElapsedMilliseconds;
                        _executionTimers.Remove(timerKey);
                    }
                    if (_executionStartTimes.TryGetValue(timerKey, out var st))
                    {
                        startTime = st;
                        _executionStartTimes.Remove(timerKey);
                    }
                }

                // 更新执行时间统计
                taskInfo.LastExecutionTimeMs = executionTimeMs;
                
                // 更新最大/最小执行时间
                if (taskInfo.RunCount == 1)
                {
                    taskInfo.MinExecutionTimeMs = executionTimeMs;
                    taskInfo.MaxExecutionTimeMs = executionTimeMs;
                }
                else
                {
                    if (executionTimeMs < taskInfo.MinExecutionTimeMs || taskInfo.MinExecutionTimeMs == 0)
                        taskInfo.MinExecutionTimeMs = executionTimeMs;
                    if (executionTimeMs > taskInfo.MaxExecutionTimeMs)
                        taskInfo.MaxExecutionTimeMs = executionTimeMs;
                }
                
                // 更新平均执行时间（基于所有执行次数）
                if (taskInfo.RunCount > 0)
                {
                    taskInfo.AverageExecutionTimeMs = 
                        (taskInfo.AverageExecutionTimeMs * (taskInfo.RunCount - 1) + executionTimeMs) / taskInfo.RunCount;
                }

                if (jobException != null)
                {
                    taskInfo.FailureCount++;
                    taskInfo.LastExecutionResult = "失败";
                    taskInfo.LastExecutionMessage = jobException.InnerException?.Message ?? jobException.Message;
                    _logger.Error($"Job execution failed: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms", jobException);
                }
                else if (IsJobResultFailure(context.Result))
                {
                    taskInfo.FailureCount++;
                    taskInfo.LastExecutionResult = "失败";
                    taskInfo.LastExecutionMessage = context.Result?.ToString() ?? string.Empty;
                    _logger.Warn($"Job reported failure: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms, Result: {context.Result}");
                }
                else
                {
                    taskInfo.SuccessCount++;
                    taskInfo.LastExecutionResult = "成功";
                    taskInfo.LastExecutionMessage = context.Result?.ToString() ?? string.Empty;
                    _logger.Info($"Job completed: {jobKey.Name}({jobKey.Group}), RunCount: {taskInfo.RunCount}, Duration: {executionTimeMs}ms");
                }

                // 保存执行记录到 SQLite
                Task.Run(() =>
                {
                    var record = new JobExecutionRecord
                    {
                        JobName = jobKey.Name,
                        GroupName = jobKey.Group,
                        StartTime = startTime,
                        EndTime = DateTime.Now,
                        ExecutionTimeMs = executionTimeMs,
                        Success = taskInfo.LastExecutionResult == "成功",
                        Result = taskInfo.LastExecutionResult,
                        Message = taskInfo.LastExecutionMessage
                    };
                    SchedulerDbManager.GetInstance().InsertRecord(record);
                });
            }
            
            JobExecutedEvent?.Invoke(context);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 判断 Job 返回的结果是否表示失败。
        /// 约定：如果 context.Result 是一个含 Success 属性的对象且 Success == false，则视为失败。
        /// </summary>
        private static bool IsJobResultFailure(object? result)
        {
            if (result == null) return false;

            // 通过反射检查是否有 bool Success 属性（避免 Scheduler 层直接引用 Engine 层类型）
            var successProp = result.GetType().GetProperty("Success");
            if (successProp != null && successProp.PropertyType == typeof(bool))
            {
                return !(bool)successProp.GetValue(result)!;
            }
            return false;
        }
    }
}
