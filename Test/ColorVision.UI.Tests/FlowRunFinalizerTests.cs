using ColorVision.Engine;
using ColorVision.Engine.FlowProcessing;
using ColorVision.Engine.FlowProcessing.PostProcess;
using FlowEngineLib.Base;

namespace ColorVision.UI.Tests;

public sealed class FlowRunFinalizerTests
{
    [Fact]
    public async Task RequiredPostProcessFailureProducesFailedFinalResult()
    {
        var batch = new MeasureBatchModel { TId = 17 };
        var persistence = new RecordingPersistence();
        var finalizer = new FlowRunFinalizer(
            new StubPostProcessExecutor(
                Result(
                    "archive",
                    PostProcessFailurePolicy.Required,
                    PostProcessExecutionStatus.ReturnedFalse)),
            persistence);

        FlowRunFinalizedData result = await finalizer.FinalizeAsync(
            Request(StatusTypeEnum.Completed, batch),
            journalScope: null);

        Assert.Equal(FlowFinalOutcome.Failed, result.FinalOutcome);
        Assert.Same(batch, persistence.RequiredFailureBatch);
        Assert.Equal(FlowStatus.Failed, persistence.FallbackStatus);
        Assert.Equal(1, persistence.FallbackCalls);
    }

    [Fact]
    public async Task WarningFailureKeepsSuccessfulBusinessOutcome()
    {
        var persistence = new RecordingPersistence();
        var finalizer = new FlowRunFinalizer(
            new StubPostProcessExecutor(
                Result(
                    "notification",
                    PostProcessFailurePolicy.Warning,
                    PostProcessExecutionStatus.ThrewException)),
            persistence);

        FlowRunFinalizedData result = await finalizer.FinalizeAsync(
            Request(
                StatusTypeEnum.Completed,
                new MeasureBatchModel()),
            journalScope: null);

        Assert.Equal(
            FlowFinalOutcome.SucceededWithWarnings,
            result.FinalOutcome);
        Assert.Null(persistence.RequiredFailureBatch);
        Assert.Equal(FlowStatus.Completed, persistence.FallbackStatus);
    }

    [Fact]
    public async Task PostProcessDispatchExceptionBecomesWarningResult()
    {
        var persistence = new RecordingPersistence();
        var finalizer = new FlowRunFinalizer(
            new ThrowingPostProcessExecutor(),
            persistence);

        FlowRunFinalizedData result = await finalizer.FinalizeAsync(
            Request(
                StatusTypeEnum.Completed,
                new MeasureBatchModel()),
            journalScope: null);

        Assert.Equal(
            FlowFinalOutcome.SucceededWithWarnings,
            result.FinalOutcome);
        PostProcessExecutionResult failure =
            Assert.Single(result.PostProcessResults);
        Assert.Equal(
            PostProcessFailurePolicy.Warning,
            failure.FailurePolicy);
        Assert.Equal(
            PostProcessExecutionStatus.ThrewException,
            failure.Status);
        Assert.Contains("dispatch failed", failure.Message);
    }

    [Fact]
    public async Task PostProcessCompletesBeforeFinalPersistence()
    {
        var calls = new List<string>();
        var persistence = new RecordingPersistence(calls);
        var finalizer = new FlowRunFinalizer(
            new OrderedPostProcessExecutor(calls),
            persistence);

        await finalizer.FinalizeAsync(
            Request(
                StatusTypeEnum.Completed,
                new MeasureBatchModel()),
            journalScope: null);

        Assert.Equal(
            ["post-process", "fallback-run"],
            calls);
    }

    [Theory]
    [InlineData(FlowFinalOutcome.Succeeded, FlowStatus.Completed)]
    [InlineData(
        FlowFinalOutcome.SucceededWithWarnings,
        FlowStatus.Completed)]
    [InlineData(FlowFinalOutcome.Failed, FlowStatus.Failed)]
    [InlineData(FlowFinalOutcome.Canceled, FlowStatus.Canceled)]
    [InlineData(FlowFinalOutcome.TimedOut, FlowStatus.OverTime)]
    public void RecordedStatusUsesFinalBusinessOutcome(
        FlowFinalOutcome outcome,
        FlowStatus expected)
    {
        Assert.Equal(
            expected,
            FlowRunFinalizer.ResolveRecordedStatus(outcome));
    }

    [Fact]
    public void RequiredFailureSummaryIncludesAllRequiredFailuresOnly()
    {
        string summary =
            DefaultFlowRunFinalizationPersistence
                .CreateRequiredPostProcessFailureSummary(
                new[]
                {
                    Result(
                        "required-a",
                        PostProcessFailurePolicy.Required,
                        PostProcessExecutionStatus.ReturnedFalse),
                    Result(
                        "warning",
                        PostProcessFailurePolicy.Warning,
                        PostProcessExecutionStatus.ReturnedFalse),
                    Result(
                        "required-b",
                        PostProcessFailurePolicy.Required,
                        PostProcessExecutionStatus.ThrewException),
                });

        Assert.Contains("required-a", summary);
        Assert.Contains("required-b", summary);
        Assert.DoesNotContain("warning", summary);
    }

    private static FlowRunFinalizationRequest Request(
        StatusTypeEnum status,
        MeasureBatchModel? batch)
    {
        return new FlowRunFinalizationRequest(
            new FlowControlData
            {
                Status = status,
                SerialNumber = "SN-1",
                Params = "engine-result",
            },
            batch,
            "flow-a",
            123);
    }

    private static PostProcessExecutionResult Result(
        string name,
        PostProcessFailurePolicy policy,
        PostProcessExecutionStatus status)
    {
        DateTime now = DateTime.UtcNow;
        return new PostProcessExecutionResult(
            name,
            $"Tests.{name}",
            policy,
            status,
            $"{name}-message",
            now,
            now);
    }

    private sealed class StubPostProcessExecutor :
        IFlowPostProcessExecutor
    {
        private readonly IReadOnlyList<PostProcessExecutionResult> results;

        public StubPostProcessExecutor(
            params PostProcessExecutionResult[] results)
        {
            this.results = results;
        }

        public Task<IReadOnlyList<PostProcessExecutionResult>>
            ExecuteAsync(
                MeasureBatchModel batch,
                string flowName)
        {
            return Task.FromResult(results);
        }
    }

    private sealed class ThrowingPostProcessExecutor :
        IFlowPostProcessExecutor
    {
        public Task<IReadOnlyList<PostProcessExecutionResult>>
            ExecuteAsync(
                MeasureBatchModel batch,
                string flowName)
        {
            throw new InvalidOperationException("dispatch failed");
        }
    }

    private sealed class OrderedPostProcessExecutor :
        IFlowPostProcessExecutor
    {
        private readonly List<string> calls;

        public OrderedPostProcessExecutor(List<string> calls)
        {
            this.calls = calls;
        }

        public Task<IReadOnlyList<PostProcessExecutionResult>>
            ExecuteAsync(
                MeasureBatchModel batch,
                string flowName)
        {
            calls.Add("post-process");
            return Task.FromResult<IReadOnlyList<PostProcessExecutionResult>>(
                Array.Empty<PostProcessExecutionResult>());
        }
    }

    private sealed class RecordingPersistence :
        IFlowRunFinalizationPersistence
    {
        private readonly List<string>? calls;

        public RecordingPersistence(List<string>? calls = null)
        {
            this.calls = calls;
        }

        public MeasureBatchModel? RequiredFailureBatch { get; private set; }

        public FlowStatus? FallbackStatus { get; private set; }

        public int FallbackCalls { get; private set; }

        public void ApplyRequiredPostProcessFailure(
            MeasureBatchModel batch,
            IReadOnlyList<PostProcessExecutionResult>
                postProcessResults)
        {
            RequiredFailureBatch = batch;
        }

        public void RecordFallbackRun(
            MeasureBatchModel batch,
            string flowName,
            FlowControlData engineResult,
            FlowStatus status,
            long elapsedMilliseconds)
        {
            calls?.Add("fallback-run");
            FallbackStatus = status;
            FallbackCalls++;
        }
    }
}
