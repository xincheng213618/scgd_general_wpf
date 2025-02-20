
using Quartz;
using Quartz.Listener;

namespace ColorVision.Scheduler
{


    public class TaskExecutionListener : JobListenerSupport
    {
        public override string Name => "TaskExecutionListener";

        public event Action<IJobExecutionContext> JobExecutedEvent;

        public override Task JobWasExecuted(IJobExecutionContext context, JobExecutionException? jobException, CancellationToken cancellationToken = default)
        {
            base.JobWasExecuted(context, jobException, cancellationToken);
            JobExecutedEvent?.Invoke(context);
            return Task.CompletedTask;
        }
    }
}
