using ColorVision.Scheduler.Data;
using log4net;
using Quartz;
using Quartz.Listener;
using System.Collections.Concurrent;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.Scheduler
{
    public class TaskExecutionListener : JobListenerSupport
    {
        private readonly QuartzSchedulerManager _schedulerManager;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(TaskExecutionListener));
        private readonly ConcurrentDictionary<string, ExecutionState> _activeExecutions = new();
        private readonly object _historyWriteSync = new();
        private Task _historyWriteTail = Task.CompletedTask;

        public TaskExecutionListener(QuartzSchedulerManager schedulerManager)
        {
            _schedulerManager = schedulerManager;
        }

        public override string Name => "TaskExecutionListener";

        public event Action<IJobExecutionContext>? JobExecutedEvent;

        public override async Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            await base.JobToBeExecuted(context, cancellationToken);

            JobKey jobKey = context.JobDetail.Key;
            // Register the fire before awaiting the UI dispatcher. Another
            // execution of the same JobKey can finish while this callback is
            // queued, and its completion must still observe this fire as active.
            _activeExecutions[context.FireInstanceId] = new ExecutionState(jobKey, DateTime.Now);
            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    SchedulerInfo? taskInfo = ResolveTaskInfo(context);
                    if (taskInfo != null && taskInfo.Status != SchedulerStatus.Paused)
                    {
                        taskInfo.Status = SchedulerStatus.Running;
                    }
                });
                _logger.Info($"Job starting: {jobKey.Name}({jobKey.Group}), FireInstanceId: {context.FireInstanceId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to update starting state for job: {jobKey.Name}({jobKey.Group})", ex);
            }

            // Quartz awaits this listener before invoking the job, so record the
            // start after any UI dispatch delay to align with JobRunTime.
            _activeExecutions[context.FireInstanceId] = new ExecutionState(jobKey, DateTime.Now);
        }

        public override async Task JobWasExecuted(
            IJobExecutionContext context,
            JobExecutionException? jobException,
            CancellationToken cancellationToken = default)
        {
            await base.JobWasExecuted(context, jobException, cancellationToken);

            JobKey jobKey = context.JobDetail.Key;
            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime - context.JobRunTime;
            if (_activeExecutions.TryRemove(context.FireInstanceId, out ExecutionState? executionState))
            {
                startTime = executionState.StartTime;
            }

            long executionTimeMs = Math.Max(0, (long)context.JobRunTime.TotalMilliseconds);
            bool success = jobException == null && !IsJobResultFailure(context.Result);
            string executionResult = success
                ? Properties.Resources.Sched_ExecSuccess
                : Properties.Resources.Sched_ExecFail;
            string executionMessage = jobException != null
                ? jobException.InnerException?.Message ?? jobException.Message
                : context.Result?.ToString() ?? string.Empty;
            var executionRecord = new JobExecutionRecord
            {
                JobName = jobKey.Name,
                GroupName = jobKey.Group,
                StartTime = startTime,
                EndTime = endTime,
                ExecutionTimeMs = executionTimeMs,
                Success = success,
                Result = executionResult,
                Message = executionMessage
            };

            if (jobException != null)
            {
                _logger.Error($"Job execution failed: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms", jobException);
            }
            else if (!success)
            {
                _logger.Warn($"Job reported failure: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms, Result: {context.Result}");
            }
            else
            {
                _logger.Info($"Job completed: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms");
            }

            try
            {
                await InvokeOnUiThreadAsync(() =>
                {
                    SchedulerInfo? taskInfo = ResolveTaskInfo(context);
                    if (taskInfo == null)
                    {
                        return;
                    }

                    taskInfo.RunCount++;
                    if (taskInfo.Status != SchedulerStatus.Paused)
                    {
                        taskInfo.Status = HasActiveExecution(jobKey)
                            ? SchedulerStatus.Running
                            : SchedulerStatus.Ready;
                    }

                    UpdateExecutionStatistics(taskInfo, executionTimeMs);
                    if (success)
                        taskInfo.SuccessCount++;
                    else
                        taskInfo.FailureCount++;
                    taskInfo.LastExecutionResult = executionResult;
                    taskInfo.LastExecutionMessage = executionMessage;
                });
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to update completed state for job: {jobKey.Name}({jobKey.Group})", ex);
            }

            await WriteHistoryAsync(executionRecord);

            RaiseJobExecuted(context);
        }

        private SchedulerInfo? ResolveTaskInfo(IJobExecutionContext context)
        {
            JobKey jobKey = context.JobDetail.Key;
            SchedulerInfo? currentTaskInfo = _schedulerManager.TaskInfos.FirstOrDefault(
                task => task.JobName == jobKey.Name && task.GroupName == jobKey.Group);
            if (currentTaskInfo != null)
            {
                return currentTaskInfo;
            }

            JobDataMap jobDataMap = context.JobDetail.JobDataMap;
            if (jobDataMap.TryGetValue("SchedulerInfo", out object? value) &&
                value is SchedulerInfo taskInfo)
            {
                return taskInfo;
            }

            return null;
        }

        private bool HasActiveExecution(JobKey jobKey)
        {
            return _activeExecutions.Values.Any(execution => execution.JobKey.Equals(jobKey));
        }

        private static void UpdateExecutionStatistics(SchedulerInfo taskInfo, long executionTimeMs)
        {
            taskInfo.LastExecutionTimeMs = executionTimeMs;

            if (taskInfo.RunCount == 1)
            {
                taskInfo.MinExecutionTimeMs = executionTimeMs;
                taskInfo.MaxExecutionTimeMs = executionTimeMs;
            }
            else
            {
                if (executionTimeMs < taskInfo.MinExecutionTimeMs || taskInfo.MinExecutionTimeMs == 0)
                {
                    taskInfo.MinExecutionTimeMs = executionTimeMs;
                }
                if (executionTimeMs > taskInfo.MaxExecutionTimeMs)
                {
                    taskInfo.MaxExecutionTimeMs = executionTimeMs;
                }
            }

            taskInfo.AverageExecutionTimeMs =
                (taskInfo.AverageExecutionTimeMs * (taskInfo.RunCount - 1) + executionTimeMs) /
                taskInfo.RunCount;
        }

        private Task WriteHistoryAsync(JobExecutionRecord record)
        {
            lock (_historyWriteSync)
            {
                _historyWriteTail = _historyWriteTail.ContinueWith(
                    _ => WriteHistoryRecord(record),
                    CancellationToken.None,
                    TaskContinuationOptions.DenyChildAttach,
                    TaskScheduler.Default);
                return _historyWriteTail;
            }
        }

        private static void WriteHistoryRecord(JobExecutionRecord record)
        {
            try
            {
                SchedulerDbManager.GetInstance().InsertRecord(record);
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to write execution history for job: {record.JobName}({record.GroupName})", ex);
            }
        }

        private static Task InvokeOnUiThreadAsync(Action action)
        {
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess())
            {
                action();
                return Task.CompletedTask;
            }

            if (dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                return Task.CompletedTask;
            }

            return dispatcher.InvokeAsync(action, DispatcherPriority.DataBind).Task;
        }

        private void RaiseJobExecuted(IJobExecutionContext context)
        {
            Delegate[] handlers = JobExecutedEvent?.GetInvocationList() ?? Array.Empty<Delegate>();
            foreach (Delegate handler in handlers)
            {
                if (handler is not Action<IJobExecutionContext> typedHandler)
                {
                    continue;
                }

                try
                {
                    typedHandler(context);
                }
                catch (Exception ex)
                {
                    _logger.Error("A JobExecutedEvent subscriber failed.", ex);
                }
            }
        }

        /// <summary>
        /// 判断 Job 返回的结果是否表示失败。
        /// 约定：如果 context.Result 是一个含 Success 属性的对象且 Success == false，则视为失败。
        /// </summary>
        private static bool IsJobResultFailure(object? result)
        {
            if (result == null)
            {
                return false;
            }

            // 通过反射检查是否有 bool Success 属性（避免 Scheduler 层直接引用 Engine 层类型）
            var successProp = result.GetType().GetProperty("Success");
            if (successProp != null && successProp.PropertyType == typeof(bool))
            {
                return !(bool)successProp.GetValue(result)!;
            }
            return false;
        }

        private sealed record ExecutionState(JobKey JobKey, DateTime StartTime);
    }
}
