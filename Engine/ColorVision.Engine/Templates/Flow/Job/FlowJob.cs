#pragma warning disable CS8602,CS8603,CS8601
using Quartz;
using System.Threading.Tasks;
using System.Windows;


namespace ColorVision.Engine.Templates.Flow.Job
{
    public class FlowJob : IJob
    {
        public  Task Execute(IJobExecutionContext context)
        {
            Application.Current.Dispatcher.Invoke(() => FlowEngineManager.GetInstance().DisplayFlow.RunFlow());
            return Task.CompletedTask;
        }
    }
}
