using System.Collections.Concurrent;
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionProgressTests
{
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
}
