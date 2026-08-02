using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using FlowEngineLib.Base;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.UI.Tests;

public class PostProcessFinalOutcomeTests
{
    [Theory]
    [InlineData(StatusTypeEnum.Completed, FlowFinalOutcome.Succeeded)]
    [InlineData(StatusTypeEnum.Failed, FlowFinalOutcome.Failed)]
    [InlineData(StatusTypeEnum.Canceled, FlowFinalOutcome.Canceled)]
    [InlineData(StatusTypeEnum.OverTime, FlowFinalOutcome.TimedOut)]
    public void NoPostProcessMapsEngineStatus(
        StatusTypeEnum engineStatus,
        FlowFinalOutcome expectedOutcome)
    {
        var engineResult = new FlowControlData { Status = engineStatus };

        FlowFinalOutcome outcome = FlowFinalOutcomeResolver.Resolve(
            engineResult,
            Array.Empty<PostProcessExecutionResult>());

        Assert.Equal(expectedOutcome, outcome);
    }

    [Fact]
    public void WarningFailureProducesSucceededWithWarnings()
    {
        IReadOnlyList<PostProcessExecutionResult> results = Run(
            Meta("warning", PostProcessFailurePolicy.Warning, new StubPostProcessor(() => false)));

        FlowFinalOutcome outcome = FlowFinalOutcomeResolver.Resolve(
            new FlowControlData { Status = StatusTypeEnum.Completed },
            results);

        Assert.Equal(FlowFinalOutcome.SucceededWithWarnings, outcome);
        PostProcessExecutionResult result = Assert.Single(results);
        Assert.Equal(PostProcessExecutionStatus.ReturnedFalse, result.Status);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void RequiredFailureProducesFailed()
    {
        IReadOnlyList<PostProcessExecutionResult> results = Run(
            Meta("required", PostProcessFailurePolicy.Required, new StubPostProcessor(() => false)));

        FlowFinalOutcome outcome = FlowFinalOutcomeResolver.Resolve(
            new FlowControlData { Status = StatusTypeEnum.Completed },
            results);

        Assert.Equal(FlowFinalOutcome.Failed, outcome);
    }

    [Theory]
    [InlineData(StatusTypeEnum.Failed, FlowFinalOutcome.Failed)]
    [InlineData(StatusTypeEnum.Canceled, FlowFinalOutcome.Canceled)]
    [InlineData(StatusTypeEnum.OverTime, FlowFinalOutcome.TimedOut)]
    public void RequiredPostProcessFailureDoesNotHideEngineTerminalOutcome(
        StatusTypeEnum engineStatus,
        FlowFinalOutcome expectedOutcome)
    {
        IReadOnlyList<PostProcessExecutionResult> results = Run(
            Meta(
                "required",
                PostProcessFailurePolicy.Required,
                new StubPostProcessor(() => false)));

        FlowFinalOutcome outcome = FlowFinalOutcomeResolver.Resolve(
            new FlowControlData { Status = engineStatus },
            results);

        Assert.Equal(expectedOutcome, outcome);
    }

    [Fact]
    public void RunnerRecordsFalseAndExceptionAndContinuesInOrder()
    {
        int finalStepRuns = 0;
        IReadOnlyList<PostProcessExecutionResult> results = Run(
            Meta("first", PostProcessFailurePolicy.Warning, new StubPostProcessor(() => false)),
            Meta(
                "second",
                PostProcessFailurePolicy.Required,
                new StubPostProcessor(() => throw new InvalidOperationException("boom"))),
            Meta(
                "third",
                PostProcessFailurePolicy.Warning,
                new StubPostProcessor(() =>
                {
                    finalStepRuns++;
                    return true;
                })));

        Assert.Collection(
            results,
            result =>
            {
                Assert.Equal("first", result.Name);
                Assert.Equal(PostProcessExecutionStatus.ReturnedFalse, result.Status);
            },
            result =>
            {
                Assert.Equal("second", result.Name);
                Assert.Equal(PostProcessExecutionStatus.ThrewException, result.Status);
                Assert.Equal("boom", result.Message);
            },
            result =>
            {
                Assert.Equal("third", result.Name);
                Assert.Equal(PostProcessExecutionStatus.Succeeded, result.Status);
            });
        Assert.Equal(1, finalStepRuns);
    }

    [Fact]
    public void LegacyPersistenceWithoutFailurePolicyDefaultsToWarning()
    {
        const string json =
            """
            {
              "Name": "legacy",
              "TemplateName": "flow",
              "ProcessTypeFullName": "Legacy.Processor",
              "ConfigJson": "{}",
              "Tag": ""
            }
            """;

        PostProcessPersist? persisted = JsonConvert.DeserializeObject<PostProcessPersist>(json);

        Assert.NotNull(persisted);
        Assert.Equal(PostProcessFailurePolicy.Warning, persisted.FailurePolicy);
    }

    [Fact]
    public void PersistenceRoundTripsRequiredFailurePolicy()
    {
        var source = new PostProcessPersist
        {
            Name = "required",
            TemplateName = "flow",
            ProcessTypeFullName = typeof(StubPostProcessor).FullName!,
            ConfigJson = "{}",
            Tag = "test",
            FailurePolicy = PostProcessFailurePolicy.Required
        };

        string json = JsonConvert.SerializeObject(source);
        PostProcessPersist? persisted = JsonConvert.DeserializeObject<PostProcessPersist>(json);

        Assert.NotNull(persisted);
        Assert.Equal(PostProcessFailurePolicy.Required, persisted.FailurePolicy);
    }

    private static IReadOnlyList<PostProcessExecutionResult> Run(params PostProcessMeta[] metas)
    {
        return PostProcessExecutionRunner.Execute(
            metas,
            new PostProcessContext(new PostProcessConfig())
            {
                Batch = new MeasureBatchModel(),
                FlowName = "flow"
            });
    }

    private static PostProcessMeta Meta(
        string name,
        PostProcessFailurePolicy failurePolicy,
        IPostProcessor processor)
    {
        return new PostProcessMeta
        {
            Name = name,
            TemplateName = "flow",
            FailurePolicy = failurePolicy,
            PostProcessor = processor
        };
    }

    private sealed class StubPostProcessor : IPostProcessor
    {
        private readonly Func<bool> _process;

        public StubPostProcessor(Func<bool> process)
        {
            _process = process;
        }

        public bool Process(PostProcessContext ctx)
        {
            return _process();
        }
    }
}
