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
            return await FlowEngineManager.GetInstance().DisplayFlow.RunFlowAndWaitAsync();
        }

        Task<FlowControlData?> execution = await application.Dispatcher.InvokeAsync(
            () => FlowEngineManager.GetInstance().DisplayFlow.RunFlowAndWaitAsync());
        return await execution;
    }
}
