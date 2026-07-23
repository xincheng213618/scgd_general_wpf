using ColorVision.UI;

namespace ColorVision.Scheduler
{
    public class TaskViewerInitializer : InitializerBase
    {

        public override string Name => nameof(TaskViewerInitializer);

        public override int Order => 1;

        public override async Task InitializeAsync()
        {
            QuartzSchedulerManager.GetInstance();
        }
    }
}
