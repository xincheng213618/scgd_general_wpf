using ColorVision.Engine.FlowProcessing.PostProcess;
using System;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Engine.FlowProcessing;

public sealed class FlowExecutionCoordinator
{
    private static readonly Lazy<FlowExecutionCoordinator> LazyInstance = new(() => new FlowExecutionCoordinator());

    public static FlowExecutionCoordinator Instance => LazyInstance.Value;

    private FlowExecutionCoordinator()
    {
    }

    public async Task<FlowControlData?> RunSelectedFlowAsync()
    {
        Application application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");

        if (application.Dispatcher.CheckAccess())
        {
            return await FlowEngineManager.GetInstance().RunFlowAsync();
        }

        Task<FlowControlData?> execution = await application.Dispatcher.InvokeAsync(
            () => FlowEngineManager.GetInstance().RunFlowAsync());
        return await execution;
    }

    public async Task<FlowRunFinalizedData?> RunSelectedFlowAndWaitForFinalizationAsync()
    {
        Application application = Application.Current
            ?? throw new InvalidOperationException("The WPF application is not available.");

        if (application.Dispatcher.CheckAccess())
        {
            return await FlowEngineManager
                .GetInstance()
                .RunFlowAndWaitForFinalizationAsync();
        }

        Task<FlowRunFinalizedData?> execution = await application.Dispatcher.InvokeAsync(
            () => FlowEngineManager
                .GetInstance()
                .RunFlowAndWaitForFinalizationAsync());
        return await execution;
    }
}
