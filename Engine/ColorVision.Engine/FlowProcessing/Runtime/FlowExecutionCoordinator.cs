using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

    /// <summary>
    /// Executes an immutable STN snapshot without touching the singleton
    /// editor, selected template, UI batch or pre/post-process pipeline.
    /// </summary>
    public Task<FlowHeadlessExecutionResult> RunHeadlessAsync(
        FlowHeadlessExecutionRequest request,
        CancellationToken cancellationToken = default)
    {
        return FlowHeadlessExecutionService.Shared.RunAsync(
            request,
            cancellationToken);
    }

    internal Task<FlowHeadlessExecutionResult> RunHeadlessAsync(
        FlowHeadlessExecutionRequest request,
        FlowHeadlessExecutionObserver observer,
        CancellationToken cancellationToken)
    {
        return FlowHeadlessExecutionService.Shared.RunAsync(
            request,
            observer,
            cancellationToken);
    }

    /// <summary>
    /// Loads the currently saved STN and execution policy, then runs that
    /// snapshot through the isolated headless service.
    /// </summary>
    public Task<FlowHeadlessExecutionResult>
        RunSavedFlowHeadlessAsync(
            string flowKey,
            string startNodeName,
            string serialNumber,
            IEnumerable<MQTTServiceInfo>? services = null,
            TimeSpan? readinessTimeout = null,
            TimeSpan? executionTimeout = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flowKey);
        string? savedStnBase64 = ReadSavedStnBase64(flowKey);
        if (string.IsNullOrWhiteSpace(savedStnBase64))
        {
            throw new InvalidOperationException(
                $"流程 {flowKey} 没有可执行的已保存 STN。");
        }
        byte[] savedStn;
        try
        {
            savedStn = Convert.FromBase64String(savedStnBase64);
        }
        catch (FormatException ex)
        {
            throw new InvalidOperationException(
                $"流程 {flowKey} 的 STN 数据无效。",
                ex);
        }
        var request = new FlowHeadlessExecutionRequest(
            savedStn,
            startNodeName,
            serialNumber,
            services,
            readinessTimeout,
            executionTimeout);
        return RunHeadlessAsync(request, cancellationToken);
    }

    private static string? ReadSavedStnBase64(string flowKey)
    {
        Application? application = Application.Current;
        if (application != null
            && !application.Dispatcher.CheckAccess())
        {
            return application.Dispatcher.Invoke(
                () => FindSavedStnBase64(flowKey));
        }
        return FindSavedStnBase64(flowKey);
    }

    private static string? FindSavedStnBase64(string flowKey)
    {
        FlowParam? flowParam = TemplateFlow.Params
            .Select(item => item.Value)
            .FirstOrDefault(item => string.Equals(
                item.FlowKey,
                flowKey,
                StringComparison.Ordinal));
        return flowParam?.DataBase64;
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
