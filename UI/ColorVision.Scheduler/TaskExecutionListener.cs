
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
                lock (_executionTimers)
                {
                    if (_executionTimers.TryGetValue(timerKey, out var stopwatch))
                    {
                        stopwatch.Stop();
                        executionTimeMs = stopwatch.ElapsedMilliseconds;
                        _executionTimers.Remove(timerKey);
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
                
                // 更新平均执行时间
                if (taskInfo.RunCount > 0)
                {
                    var totalSuccessCount = taskInfo.SuccessCount + (jobException == null ? 1 : 0);
                    if (totalSuccessCount > 0)
                    {
                        taskInfo.AverageExecutionTimeMs = 
                            (taskInfo.AverageExecutionTimeMs * (totalSuccessCount - 1) + executionTimeMs) / totalSuccessCount;
                    }
                }

                if (jobException != null)
                {
                    taskInfo.FailureCount++;
                    _logger.Error($"Job execution failed: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms", jobException);
                }
                else
                {
                    taskInfo.SuccessCount++;
                    _logger.Info($"Job completed: {jobKey.Name}({jobKey.Group}), RunCount: {taskInfo.RunCount}, Duration: {executionTimeMs}ms");
                }
            }
            
            JobExecutedEvent?.Invoke(context);
            return Task.CompletedTask;
        }
    }
}
