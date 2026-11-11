using ColorVision.Scheduler.Data;
using log4net;
using Quartz;
using System.Collections.Concurrent;

namespace ColorVision.Scheduler
{
    public class TaskExecutionListener : IJobListener, ITriggerListener
    {
        private readonly QuartzSchedulerManager _schedulerManager;
        private static readonly ILog _logger = LogManager.GetLogger(typeof(TaskExecutionListener));
        private readonly ConcurrentDictionary<string, ExecutionState> _activeExecutions = new();
        private readonly object _historyWriteSync = new();
        private Task _historyWriteTail = Task.CompletedTask;
        private readonly Action<JobExecutionRecord> _writeHistory;

        public TaskExecutionListener(QuartzSchedulerManager schedulerManager) : this(schedulerManager, WriteHistoryRecord)
        {
        }

        internal TaskExecutionListener(QuartzSchedulerManager schedulerManager, Action<JobExecutionRecord> writeHistory)
        {
            _schedulerManager = schedulerManager;
            _writeHistory = writeHistory;
        }

        public string Name => "TaskExecutionListener";

        public event Action<IJobExecutionContext>? JobExecutedEvent;

        internal void AttachTo(IListenerManager listenerManager)
        {
            listenerManager.AddTriggerListener(this);
            listenerManager.AddJobListener(this);
        }

        internal JobKey[] GetActiveJobKeys() => _activeExecutions.Values.Select(execution => execution.JobKey).Distinct().ToArray();

        async ValueTask<bool> ITriggerListener.VetoJobExecution(ITrigger trigger, IJobExecutionContext context, CancellationToken cancellationToken)
        {
            if (!SchedulerScheduleState.IsScheduledFire(context)
                || !context.JobDetail.JobDataMap.TryGetValue("SchedulerInfo", out object? definition)
                || definition is not SchedulerInfo)
            {
                return false;
            }

            JobKey jobKey = context.JobDetail.Key;
            _activeExecutions[context.FireInstanceId] = new ExecutionState(jobKey, DateTime.Now);
            try
            {
                if (await _schedulerManager.TryCheckpointScheduledFireAsync(context))
                    return false;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to admit scheduled job: {jobKey.Name}({jobKey.Group})", ex);
            }

            _activeExecutions.TryRemove(context.FireInstanceId, out _);
            _schedulerManager.NotifyActiveExecutionsChanged();
            return true;
        }

        public async ValueTask JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            JobKey jobKey = context.JobDetail.Key;
            // Register the fire before awaiting the UI dispatcher. Another
            // execution of the same JobKey can finish while this callback is
            // queued, and its completion must still observe this fire as active.
            bool needsStartingState = _activeExecutions.TryAdd(context.FireInstanceId, new ExecutionState(jobKey, DateTime.Now));
            try
            {
                if (needsStartingState)
                {
                    await _schedulerManager.RecordExecutionAsync(context, taskInfo =>
                    {
                        SchedulerScheduleState.RecordFire(taskInfo, context);
                        if (taskInfo.Status != SchedulerStatus.Paused)
                            taskInfo.Status = SchedulerStatus.Running;
                    });
                }
                _logger.Info($"Job starting: {jobKey.Name}({jobKey.Group}), FireInstanceId: {context.FireInstanceId}");
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to update starting state for job: {jobKey.Name}({jobKey.Group})", ex);
            }

            // Quartz awaits this listener before invoking the job, so record the
            // start after any UI dispatch delay to align with JobRunTime.
            _activeExecutions[context.FireInstanceId] = new ExecutionState(jobKey, DateTime.Now);
            _schedulerManager.NotifyActiveExecutionsChanged();
        }

        public ValueTask JobExecutionVetoed(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            if (_activeExecutions.TryRemove(context.FireInstanceId, out _))
                _schedulerManager.NotifyActiveExecutionsChanged();
            return ValueTask.CompletedTask;
        }

        public async ValueTask JobWasExecuted(
            IJobExecutionContext context,
            JobExecutionException? jobException,
            CancellationToken cancellationToken = default)
        {
            JobKey jobKey = context.JobDetail.Key;
            DateTime endTime = DateTime.Now;
            DateTime startTime = endTime - context.JobRunTime;
            if (_activeExecutions.TryRemove(context.FireInstanceId, out ExecutionState? executionState))
            {
                startTime = executionState.StartTime;
            }

            long executionTimeMs = Math.Max(0, (long)context.JobRunTime.TotalMilliseconds);
            (bool success, string executionMessage) = ReadExecutionResult(context.Result, jobException);
            string executionResult = success
                ? Properties.Resources.Sched_ExecSuccess
                : Properties.Resources.Sched_ExecFail;
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
                _logger.Warn($"Job reported failure: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms, Result: {executionMessage}");
            }
            else
            {
                _logger.Info($"Job completed: {jobKey.Name}({jobKey.Group}), Duration: {executionTimeMs}ms");
            }

            try
            {
                await _schedulerManager.RecordExecutionAsync(context, taskInfo =>
                {
                    taskInfo.RunCount++;
                    if (taskInfo.Status != SchedulerStatus.Paused)
                    {
                        taskInfo.Status = HasActiveExecution(jobKey)
                            ? SchedulerStatus.Running
                            : taskInfo.ScheduleCompleted ? SchedulerStatus.Completed : SchedulerStatus.Ready;
                    }
                    else if (taskInfo.ScheduleCompleted && !HasActiveExecution(jobKey))
                    {
                        taskInfo.Status = SchedulerStatus.Completed;
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

            _schedulerManager.NotifyActiveExecutionsChanged();
            await WriteHistoryAsync(executionRecord);

            RaiseJobExecuted(context);
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
                    _ => _writeHistory(record),
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

        private static (bool Success, string Message) ReadExecutionResult(object? result, JobExecutionException? jobException)
        {
            if (jobException != null)
                return (false, jobException.InnerException?.Message ?? jobException.Message);
            if (result == null)
                return (true, string.Empty);

            bool success;
            try
            {
                // Read the optional result contract without depending on a specific job type.
                var successProperty = result.GetType().GetProperty("Success");
                success = successProperty?.PropertyType != typeof(bool) || (bool)successProperty.GetValue(result)!;
            }
            catch (Exception ex)
            {
                _logger.Error("Failed to read the job result's success state.", ex);
                return (false, ex.GetBaseException().Message);
            }

            try
            {
                return (success, result.ToString() ?? string.Empty);
            }
            catch (Exception ex)
            {
                _logger.Warn("Failed to format the job result.", ex);
                return (success, ex.GetBaseException().Message);
            }
        }

        private sealed record ExecutionState(JobKey JobKey, DateTime StartTime);
    }
}
