using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotToolExecutionCancellationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task ToolTimeoutDoesNotWaitForSynchronousPrefixOrBlockingCancellationCallback()
    {
        using var tool = new BlockingCancellationTool();
        var executor = new CopilotToolExecutor();
        var executionTask = executor.ExecuteAsync(
            new CopilotToolInvocation
            {
                CallId = "blocking-tool-call",
                Round = 1,
                Attempt = 1,
                MaxAttempts = 1,
                RuntimeName = "test",
                Tool = tool,
                AgentRequest = new CopilotAgentRequest
                {
                    Mode = CopilotAgentMode.Auto,
                    UserText = "exercise the timeout boundary",
                },
            },
            _ => { },
            CancellationToken.None);

        try
        {
            Assert.True(tool.Started.Wait(TestTimeout));
            var outcome = await executionTask.WaitAsync(TestTimeout);

            Assert.Equal(CopilotToolExecutionState.TimedOut, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.Transient, outcome.Result.FailureKind);
            Assert.True(tool.CancellationCallbackStarted.Wait(TestTimeout));
            Assert.False(tool.ReleaseCancellationCallback.IsSet);
            Assert.False(tool.ReleaseInvocation.IsSet);
        }
        finally
        {
            tool.ReleaseCancellationCallback.Set();
            tool.ReleaseInvocation.Set();
            try
            {
                await executionTask.WaitAsync(TestTimeout);
            }
            catch
            {
            }
        }
    }

    [Fact]
    public async Task CallerCancellationDoesNotRunToolCallbackOnRequestingThread()
    {
        using var tool = new BlockingCancellationTool(TimeSpan.FromSeconds(10));
        using var cancellation = new CancellationTokenSource();
        var executor = new CopilotToolExecutor();
        var executionTask = executor.ExecuteAsync(
            CreateInvocation(tool, "caller-cancelled-tool-call"),
            _ => { },
            cancellation.Token);

        try
        {
            Assert.True(tool.Started.Wait(TestTimeout));
            var cancelTask = Task.Factory.StartNew(
                cancellation.Cancel,
                CancellationToken.None,
                TaskCreationOptions.LongRunning,
                TaskScheduler.Default);

            await cancelTask.WaitAsync(TestTimeout);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => executionTask.WaitAsync(TestTimeout));
            Assert.True(tool.CancellationCallbackStarted.Wait(TestTimeout));
            Assert.False(tool.ReleaseCancellationCallback.IsSet);
        }
        finally
        {
            tool.ReleaseCancellationCallback.Set();
            tool.ReleaseInvocation.Set();
            try
            {
                await executionTask.WaitAsync(TestTimeout);
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
                UserText = "exercise the cancellation boundary",
            },
        };
    }

    private sealed class BlockingCancellationTool : ICopilotTool, IDisposable
    {
        private readonly TimeSpan _timeout;

        public BlockingCancellationTool()
            : this(TimeSpan.FromMilliseconds(50))
        {
        }

        public BlockingCancellationTool(TimeSpan timeout)
        {
            _timeout = timeout;
        }

        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim CancellationCallbackStarted { get; } = new();

        public ManualResetEventSlim ReleaseCancellationCallback { get; } = new();

        public ManualResetEventSlim ReleaseInvocation { get; } = new();

        public string Name => "BlockingCancellationTool";

        public string Description => "Blocks its synchronous prefix and cancellation callback for timeout testing.";

        public CopilotToolCapabilityDescriptor Capability =>
            CopilotToolCapabilityDescriptor.ReadOnly(_timeout);

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            using var registration = cancellationToken.Register(() =>
            {
                CancellationCallbackStarted.Set();
                ReleaseCancellationCallback.Wait(CancellationToken.None);
            });
            Started.Set();
            ReleaseInvocation.Wait(CancellationToken.None);
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "completed",
            });
        }

        public void Dispose()
        {
            ReleaseCancellationCallback.Set();
            ReleaseInvocation.Set();
            Started.Dispose();
            CancellationCallbackStarted.Dispose();
            ReleaseCancellationCallback.Dispose();
            ReleaseInvocation.Dispose();
        }
    }
}
