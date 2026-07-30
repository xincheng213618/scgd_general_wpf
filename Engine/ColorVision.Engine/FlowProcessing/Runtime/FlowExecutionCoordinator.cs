using ColorVision.Engine.FlowProcessing.Artifacts;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.Templates.Flow.Routing;
using FlowEngineLib;
using System;
using System.Collections.Generic;
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
    /// Loads and validates the published executable, then runs its compiled
    /// STN and effective policy through the same isolated headless service.
    /// Artifact persistence remains owned by the artifact application layer.
    /// </summary>
    public Task<FlowHeadlessExecutionResult>
        RunPublishedArtifactHeadlessAsync(
            string flowKey,
            string startNodeName,
            string serialNumber,
            IEnumerable<MQTTServiceInfo>? services = null,
            TimeSpan? readinessTimeout = null,
            TimeSpan? executionTimeout = null,
            CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(flowKey);
        using FlowArtifactApplicationService artifacts =
            FlowArtifactServiceProvider.Create(
                ensureSchema: false);
        FlowPublishedExecutable executable =
            artifacts.GetPublishedExecutable(flowKey);
        var request = new FlowHeadlessExecutionRequest(
            executable.CompiledStn,
            startNodeName,
            serialNumber,
            services,
            FlowExecutionPolicyRuntimeAdapter.ToRuntimeErrorRoutes(
                executable.ExecutionPolicy),
            FlowExecutionPolicyRuntimeAdapter.ToRuntimeRetryPolicies(
                executable.ExecutionPolicy),
            readinessTimeout,
            executionTimeout);
        return RunHeadlessAsync(request, cancellationToken);
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
