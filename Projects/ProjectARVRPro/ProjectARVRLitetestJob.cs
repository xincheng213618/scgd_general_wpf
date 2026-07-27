#pragma warning disable CA1822,CS0168,CS0219,CS4014,CS8601
using ProjectARVRPro.PluginConfig;
using Quartz;
using System.Windows;
using System.Windows.Threading;

namespace ProjectARVRPro
{
    [DisallowConcurrentExecution]
    public class ProjectARVRLitetestJob : IJob
    {
        public async Task Execute(IJobExecutionContext context)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            Dispatcher? dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.HasShutdownStarted || dispatcher.HasShutdownFinished)
            {
                throw new JobExecutionException("The WPF application dispatcher is unavailable.");
            }

            Task<bool> startTask = await dispatcher.InvokeAsync(
                () =>
                {
                    var window = ProjectWindowInstance.WindowInstance;
                    if (window == null)
                    {
                        throw new JobExecutionException("Open the ProjectARVRPro window before running this scheduled task.");
                    }

                    return window.TryStartNextTemplateAsync(context.CancellationToken);
                },
                DispatcherPriority.Normal,
                context.CancellationToken);
            bool accepted = await startTask;
            if (!accepted)
            {
                throw new JobExecutionException("ARVR flow start was rejected because another flow is active or no enabled process is available.");
            }

            context.Result = "ARVR 流程启动已接受；最终结果由 ProjectARVRPro 流程记录。";
        }
    }
}
