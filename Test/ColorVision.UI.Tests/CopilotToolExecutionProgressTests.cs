using System.Collections.Concurrent;
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionProgressTests
{
    [Fact]
    public void ProgressContextBoundsUpdatesAndIgnoresReportsAfterCompletion()
    {
        var progress = new CopilotToolProgressContext();
        progress.Report(new CopilotToolProgressUpdate
        {
            Message = "phase\r\n" + new string('x', 300),
            Completed = 20,
            Total = 10,
            Unit = new string('u', 40),
        });

        var accepted = Assert.IsType<CopilotToolProgressUpdate>(progress.LatestSnapshot);
        Assert.DoesNotContain('\r', accepted.Message);
        Assert.DoesNotContain('\n', accepted.Message);
        Assert.True(accepted.Message.Length <= 243);
        Assert.Equal(10, accepted.Completed);
        Assert.Equal(10, accepted.Total);
        Assert.Equal(24, accepted.Unit.Length);

        progress.Complete();
        progress.Report("late update", completed: 1, total: 1);

        Assert.Same(accepted, progress.LatestSnapshot);
    }

    [Fact]
    public async Task ReportedToolProgressFlowsThroughHeartbeatEvents()
    {
        var tool = new ReportingTool();
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromMilliseconds(20));
        var reported = new TaskCompletionSource<CopilotAgentEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var executionTask = executor.ExecuteAsync(
            CreateInvocation(tool, "reported-progress-call"),
            agentEvent =>
            {
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.Progress != null)
                {
                    reported.TrySetResult(agentEvent);
                }
            },
            CancellationToken.None);

        try
        {
            var progressEvent = await reported.Task.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.Equal(CopilotToolExecutionState.Running, progressEvent.ToolExecution?.State);
            Assert.Equal("Converting approved images", progressEvent.Progress!.Message);
            Assert.Equal(3, progressEvent.Progress.Completed);
            Assert.Equal(10, progressEvent.Progress.Total);
            Assert.Equal("files", progressEvent.Progress.Unit);
            Assert.Contains("3/10 files", progressEvent.Text, StringComparison.Ordinal);
        }
        finally
        {
            tool.Release.TrySetResult();
            await executionTask.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task WaitingForSharedResourcePublishesPendingProgressBeforeStart()
    {
        using var firstTool = new ResourceTool(block: true);
        using var secondTool = new ResourceTool(block: false);
        var executor = new CopilotToolExecutor(
            hooks: null,
            utcNow: null,
            hookPhaseTimeout: null,
            progressInterval: TimeSpan.FromMilliseconds(20));
        var firstTask = executor.ExecuteAsync(
            CreateInvocation(firstTool, "first-call"),
            _ => { },
            CancellationToken.None);
        Assert.True(firstTool.Started.Wait(TimeSpan.FromSeconds(1)));

        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var pendingProgress = new TaskCompletionSource<CopilotAgentEvent>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var secondTask = executor.ExecuteAsync(
            CreateInvocation(secondTool, "second-call"),
            agentEvent =>
            {
                events.Enqueue(agentEvent);
                if (agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && agentEvent.ToolExecution?.State == CopilotToolExecutionState.Pending)
                {
                    pendingProgress.TrySetResult(agentEvent);
                }
            },
            CancellationToken.None);

        try
        {
            var queuedEvent = await pendingProgress.Task.WaitAsync(TimeSpan.FromSeconds(1));
            Assert.Contains("waiting for an execution slot", queuedEvent.Text, StringComparison.Ordinal);
            Assert.True(queuedEvent.ToolExecution!.QueueDurationMs > 0);
            Assert.False(secondTool.Started.IsSet);

            firstTool.Release.Set();
            await firstTask.WaitAsync(TimeSpan.FromSeconds(2));
            var secondOutcome = await secondTask.WaitAsync(TimeSpan.FromSeconds(2));

            Assert.True(secondTool.Started.IsSet);
            Assert.Equal(CopilotToolExecutionState.Completed, secondOutcome.Execution.State);
            Assert.True(secondOutcome.Execution.QueueDurationMs > 0);
            Assert.True(events
                .Select((agentEvent, index) => (agentEvent, index))
                .Where(item => item.agentEvent.Type == CopilotAgentEventType.ToolProgress
                    && item.agentEvent.ToolExecution?.State == CopilotToolExecutionState.Pending)
                .Select(item => item.index)
                .First()
                < events
                    .Select((agentEvent, index) => (agentEvent, index))
                    .Where(item => item.agentEvent.Type == CopilotAgentEventType.ToolStarted)
                    .Select(item => item.index)
                    .First());
        }
        finally
        {
            firstTool.Release.Set();
            try
            {
                await Task.WhenAll(firstTask, secondTask).WaitAsync(TimeSpan.FromSeconds(2));
            }
            catch
            {
            }
        }
    }

    private static CopilotToolInvocation CreateInvocation(ICopilotTool tool, string callId)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "test",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "exercise tool queue progress",
            },
        };
    }

    private sealed class ResourceTool(bool block) : ICopilotTool, IDisposable
    {
        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim Release { get; } = new();

        public string Name => "ResourceTool";

        public string Description => "Uses one shared resource for queue-progress testing.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(TimeSpan.FromSeconds(5));

        public bool CanHandle(CopilotAgentRequest request) => true;

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) =>
            "resource:shared-test";

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            Started.Set();
            if (block)
                Release.Wait(cancellationToken);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            });
        }

        public void Dispose()
        {
            Release.Set();
            Started.Dispose();
            Release.Dispose();
        }
    }

    private sealed class ReportingTool : ICopilotProgressReportingTool
    {
        public TaskCompletionSource Release { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name => "ReportingTool";

        public string Description => "Reports structured progress for executor testing.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ReadOnly(TimeSpan.FromSeconds(5));

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            throw new InvalidOperationException("The progress-aware execution path was not used.");
        }

        public async Task<CopilotToolResult> ExecuteWithProgressAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CopilotToolProgressContext progress,
            CancellationToken cancellationToken)
        {
            progress.Report("Converting approved images", completed: 3, total: 10, unit: "files");
            await Release.Task.WaitAsync(cancellationToken);
            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            };
        }
    }
}
