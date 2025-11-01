
using Quartz;
using Quartz.Listener;

namespace ColorVision.Scheduler
{
    public class TaskExecutionListener : JobListenerSupport
    {
        private readonly QuartzSchedulerManager _schedulerManager;

        public TaskExecutionListener(QuartzSchedulerManager schedulerManager)
        {
            _schedulerManager = schedulerManager;
        }

        public override string Name => "TaskExecutionListener";

        public event Action<IJobExecutionContext>? JobExecutedEvent;

        public override Task JobToBeExecuted(IJobExecutionContext context, CancellationToken cancellationToken = default)
        {
            // 任务即将执行，更新状态为 Running
            var jobKey = context.JobDetail.Key;
            var taskInfo = _schedulerManager.TaskInfos.FirstOrDefault(t => t.JobName == jobKey.Name && t.GroupName == jobKey.Group);
            if (taskInfo != null)
            {
                taskInfo.Status = SchedulerStatus.Running;
            }
            return base.JobToBeExecuted(context, cancellationToken);
        }

        public override Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            base.JobWasExecuted(context, jobException, cancellationToken);
            
            // 更新任务执行次数和状态
            var jobKey = context.JobDetail.Key;
            var taskInfo = _schedulerManager.TaskInfos.FirstOrDefault(t => t.JobName == jobKey.Name && t.GroupName == jobKey.Group);
            if (taskInfo != null)
            {
                taskInfo.RunCount++;
                taskInfo.Status = SchedulerStatus.Ready;
            }
            
            JobExecutedEvent?.Invoke(context);
            return Task.CompletedTask;
        }
    }
}
