using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using ColorVision.Engine.FlowProcessing.Scheduling;
using ColorVision.Engine.Templates;
using ColorVision.Engine.Templates.Flow;
using FlowEngineLib.Base;
using System;
using System.Reflection;
using System.Threading.Tasks;

namespace ColorVision.UI.Tests;

public class FlowFinalizedExecutionApiTests
{
    [Fact]
    public void CompatibilityAndFinalizedApisExposeIndependentReturnTypes()
    {
        Assert.Equal(
            typeof(EventHandler<FlowControlData>),
            typeof(ViewFlow)
                .GetEvent(nameof(ViewFlow.EngineExecutionCompleted))!
                .EventHandlerType);
        EventInfo legacyEvent = typeof(ViewFlow)
            .GetEvent(nameof(ViewFlow.FlowExecutionCompleted))!;
        Assert.Equal(
            typeof(EventHandler<FlowControlData>),
            legacyEvent.EventHandlerType);
        Assert.NotNull(
            legacyEvent.GetCustomAttribute<ObsoleteAttribute>());
        Assert.Equal(
            typeof(EventHandler<FlowRunFinalizedData>),
            typeof(ViewFlow)
                .GetEvent(nameof(ViewFlow.RunFinalized))!
                .EventHandlerType);

        MethodInfo legacyManagerMethod = typeof(FlowEngineManager).GetMethod(
            nameof(FlowEngineManager.RunFlowAsync),
            [typeof(TemplateModel<FlowParam>)])!;
        Assert.Equal(typeof(Task<FlowControlData>), legacyManagerMethod.ReturnType);

        MethodInfo finalizedManagerMethod = typeof(FlowEngineManager).GetMethod(
            nameof(FlowEngineManager.RunFlowAndWaitForFinalizationAsync),
            [typeof(TemplateModel<FlowParam>)])!;
        Assert.Equal(
            typeof(Task<FlowRunFinalizedData>),
            finalizedManagerMethod.ReturnType);

        MethodInfo legacyCoordinatorMethod = typeof(FlowExecutionCoordinator).GetMethod(
            nameof(FlowExecutionCoordinator.RunSelectedFlowAsync))!;
        Assert.Equal(typeof(Task<FlowControlData>), legacyCoordinatorMethod.ReturnType);

        MethodInfo finalizedCoordinatorMethod = typeof(FlowExecutionCoordinator).GetMethod(
            nameof(FlowExecutionCoordinator.RunSelectedFlowAndWaitForFinalizationAsync))!;
        Assert.Equal(
            typeof(Task<FlowRunFinalizedData>),
            finalizedCoordinatorMethod.ReturnType);

        MethodInfo finalizedViewMethod = typeof(ViewFlow).GetMethod(
            "RunFlowAndWaitForFinalizationAsync",
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        Assert.Equal(typeof(Task<FlowRunFinalizedData>), finalizedViewMethod.ReturnType);

        Type sessionType = typeof(ViewFlow).Assembly.GetType(
            "ColorVision.Engine.FlowProcessing.FlowExecutionSession",
            throwOnError: true)!;
        Assert.Equal(
            typeof(EventHandler<FlowControlData>),
            sessionType
                .GetEvent("EngineExecutionCompleted", BindingFlags.Instance | BindingFlags.Public)!
                .EventHandlerType);
        Assert.Equal(
            typeof(EventHandler<FlowRunFinalizedData>),
            sessionType
                .GetEvent("RunFinalized", BindingFlags.Instance | BindingFlags.Public)!
                .EventHandlerType);
        MethodInfo finalizedSessionMethod = sessionType.GetMethod(
            "RunFlowAndWaitForFinalizationAsync",
            BindingFlags.Instance | BindingFlags.Public)!;
        Assert.Equal(
            typeof(Task<FlowRunFinalizedData>),
            finalizedSessionMethod.ReturnType);
    }

    [Theory]
    [InlineData(FlowFinalOutcome.Succeeded, true)]
    [InlineData(FlowFinalOutcome.SucceededWithWarnings, true)]
    [InlineData(FlowFinalOutcome.Failed, false)]
    [InlineData(FlowFinalOutcome.Canceled, false)]
    [InlineData(FlowFinalOutcome.TimedOut, false)]
    public void FlowJobUsesFinalOutcomeForSuccess(
        FlowFinalOutcome outcome,
        bool expectedSuccess)
    {
        FlowJobResult result = FlowJob.CreateJobResult(CreateFinalizedData(outcome));

        Assert.Equal(expectedSuccess, result.Success);
        Assert.Equal(outcome.ToString(), result.Status);
    }

    [Fact]
    public void RequiredPostProcessFailureIsVisibleInFlowJobResult()
    {
        DateTime now = DateTime.UtcNow;
        var requiredFailure = new PostProcessExecutionResult(
            "archive",
            "Test.ArchiveProcessor",
            PostProcessFailurePolicy.Required,
            PostProcessExecutionStatus.ReturnedFalse,
            "处理器返回 false。",
            now,
            now);
        var finalizedData = new FlowRunFinalizedData(
            CreateEngineResult(),
            FlowFinalOutcome.Failed,
            [requiredFailure],
            now);

        FlowJobResult result = FlowJob.CreateJobResult(finalizedData);

        Assert.False(result.Success);
        Assert.Contains("archive", result.Message, StringComparison.Ordinal);
        Assert.Contains("处理器返回 false。", result.Message, StringComparison.Ordinal);
    }

    private static FlowRunFinalizedData CreateFinalizedData(FlowFinalOutcome outcome)
    {
        return new FlowRunFinalizedData(
            CreateEngineResult(),
            outcome,
            Array.Empty<PostProcessExecutionResult>(),
            DateTime.UtcNow);
    }

    private static FlowControlData CreateEngineResult()
    {
        return new FlowControlData
        {
            Status = StatusTypeEnum.Completed,
            EventName = StatusTypeEnum.Completed.ToString(),
            Params = "engine result",
            TotalTime = 123
        };
    }
}
