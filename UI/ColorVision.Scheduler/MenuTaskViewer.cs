using ColorVision.UI;

namespace ColorVision.Scheduler
{
    public class TaskViewerInitializer : InitializerBase
    {

        public override string Name => nameof(TaskViewerInitializer);

        // Scheduler jobs may depend on MQTT, device services, RC, CUDA and flow
        // runtime initializers. Start only after those lower-order dependencies.
        public override int Order => 1000;

        public override async Task InitializeAsync()
        {
            await QuartzSchedulerManager.GetInstance().InitializationTask;
        }
    }
}
