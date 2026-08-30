using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

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

    [Fact]
    public async Task NonIdempotentWriteTimeoutReportsUnknownOutcomeWithoutPermittingRetry()
    {
        using var tool = new BlockingCancellationTool(
            TimeSpan.FromMilliseconds(50),
            isWrite: true);
        var executor = new CopilotToolExecutor();
        var executionTask = executor.ExecuteAsync(
            CreateInvocation(tool, "write-timeout-call", frameworkApprovalGranted: true),
            _ => { },
            CancellationToken.None);

        try
        {
            Assert.True(tool.Started.Wait(TestTimeout));
            var outcome = await executionTask.WaitAsync(TestTimeout);

            Assert.Equal(CopilotToolExecutionState.TimedOut, outcome.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, outcome.Result.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, outcome.Result.FailureCode);
            Assert.False(outcome.Execution.RetryEligible);
            using var payload = JsonDocument.Parse(CopilotFrameworkToolResultFormatter.Format(outcome));
            Assert.Equal("outcome_unknown", payload.RootElement.GetProperty("failure_kind").GetString());
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, payload.RootElement.GetProperty("failure_code").GetString());
            Assert.False(payload.RootElement.GetProperty("retry_allowed").GetBoolean());
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
    public async Task CancellingStartedWritePublishesInterruptedUnknownOutcome()
    {
        using var tool = new BlockingCancellationTool(TimeSpan.FromSeconds(10), isWrite: true);
        using var cancellation = new CancellationTokenSource();
        var events = new ConcurrentQueue<CopilotAgentEvent>();
        var executor = new CopilotToolExecutor();
        var executionTask = executor.ExecuteAsync(
            CreateInvocation(tool, "write-cancelled-call", frameworkApprovalGranted: true),
            events.Enqueue,
            cancellation.Token);

        try
        {
            Assert.True(tool.Started.Wait(TestTimeout));
            cancellation.Cancel();
            var cancellationException = await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(
                () => executionTask.WaitAsync(TestTimeout));

            var terminal = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
            Assert.Same(cancellationException.Outcome.Execution, terminal.ToolExecution);
            Assert.Same(cancellationException.Outcome.Result, terminal.ToolResult);
            Assert.Equal(CopilotToolExecutionState.Interrupted, terminal.ToolExecution?.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, terminal.ToolResult?.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, terminal.ToolResult?.FailureCode);
            Assert.False(terminal.ToolExecution!.RetryEligible);
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
    public async Task FrameworkBridgeRetainsStartedWriteCancellationAsAStepRecord()
    {
        using var tool = new BlockingCancellationTool(
            TimeSpan.FromSeconds(10),
            isWrite: true,
            requiresApproval: false);
        using var cancellation = new CancellationTokenSource();
        var request = new CopilotAgentRequest
        {
            ConversationId = "write-cancellation-conversation",
            TaskId = "write-cancellation-task",
            Mode = CopilotAgentMode.Code,
            UserText = "Apply the requested write.",
            TaskIntentText = "Apply the requested write.",
        };
        var bridge = new CopilotMicrosoftAgentFrameworkRuntime.HarnessToolBridge(
            request,
            CopilotExecutionScope.ForAgentRun(request),
            [tool],
            maxToolCalls: 1,
            new CopilotToolExecutor(),
            new CopilotFrameworkApprovalCoordinator(),
            _ => { },
            capabilityRevisionProvider: () => 1);
        var function = Assert.IsAssignableFrom<AIFunction>(Assert.Single(bridge.CreateFunctions()));
        var executionTask = function.InvokeAsync(
            new AIFunctionArguments(),
            cancellation.Token).AsTask();

        try
        {
            Assert.True(tool.Started.Wait(TestTimeout));
            cancellation.Cancel();
            await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(
                () => executionTask.WaitAsync(TestTimeout));

            var step = Assert.Single(bridge.StepRecords);
            Assert.Equal(CopilotToolExecutionState.Interrupted, step.Execution.State);
            Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, step.Observation.FailureKind);
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, step.Observation.FailureCode);
            Assert.Contains(CopilotToolFailureCode.OutcomeUnknown, step.ModelToolResult, StringComparison.Ordinal);
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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SettledWriteCancellationDoesNotProveThatNoSideEffectsOccurred(bool performPartialWrite)
    {
        var tool = new SelfCancellingWriteTool(performPartialWrite);
        var executor = new CopilotToolExecutor();

        var exception = await Assert.ThrowsAsync<CopilotToolExecutionCancellationException>(() =>
            executor.ExecuteAsync(
                CreateInvocation(tool, "settled-write-cancellation", frameworkApprovalGranted: true),
                _ => { },
                CancellationToken.None));

        Assert.Equal(performPartialWrite ? 1 : 0, tool.CompletedWrites);
        Assert.Equal(CopilotToolExecutionState.Interrupted, exception.Outcome.Execution.State);
        Assert.Equal(CopilotToolFailureKind.OutcomeUnknown, exception.Outcome.Result.FailureKind);
        Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, exception.Outcome.Result.FailureCode);
        Assert.False(exception.Outcome.Execution.RetryEligible);
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        string callId,
        bool frameworkApprovalGranted = false)
    {
        return new CopilotToolInvocation
        {
            CallId = callId,
            Round = 1,
            Attempt = 1,
            MaxAttempts = 1,
            RuntimeName = "test",
            Tool = tool,
            FrameworkApprovalGranted = frameworkApprovalGranted,
            AgentRequest = new CopilotAgentRequest
            {
                Mode = CopilotAgentMode.Auto,
                UserText = "exercise the cancellation boundary",
            },
        };
    }

    private sealed class BlockingCancellationTool : ICopilotFrameworkApprovedTool, IDisposable
    {
        private readonly TimeSpan _timeout;
        private readonly bool _isWrite;
        private readonly bool _requiresApproval;

        public BlockingCancellationTool()
            : this(TimeSpan.FromMilliseconds(50), isWrite: false)
        {
        }

        public BlockingCancellationTool(
            TimeSpan timeout,
            bool isWrite = false,
            bool requiresApproval = true)
        {
            _timeout = timeout;
            _isWrite = isWrite;
            _requiresApproval = requiresApproval;
        }

        public ManualResetEventSlim Started { get; } = new();

        public ManualResetEventSlim CancellationCallbackStarted { get; } = new();

        public ManualResetEventSlim ReleaseCancellationCallback { get; } = new();

        public ManualResetEventSlim ReleaseInvocation { get; } = new();

        public string Name => "BlockingCancellationTool";

        public string Description => "Blocks its synchronous prefix and cancellation callback for timeout testing.";

        public CopilotToolCapabilityDescriptor Capability =>
            _isWrite
                ? _requiresApproval
                    ? CopilotToolCapabilityDescriptor.ProtectedWrite(
                        CopilotToolIdempotency.NonIdempotent,
                        _timeout)
                    : new CopilotToolCapabilityDescriptor
                    {
                        Access = CopilotToolAccess.Write,
                        RiskLevel = CopilotToolRiskLevel.Medium,
                        ApprovalMode = CopilotToolApprovalMode.Never,
                        Idempotency = CopilotToolIdempotency.NonIdempotent,
                        ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
                        ExecutionTimeout = _timeout,
                        EvidenceMode = CopilotToolEvidenceMode.None,
                    }
                : CopilotToolCapabilityDescriptor.ReadOnly(_timeout);

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

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => ExecuteAsync(request, toolInput, cancellationToken);

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

    private sealed class SelfCancellingWriteTool(bool performPartialWrite) : ICopilotFrameworkApprovedTool
    {
        public int CompletedWrites { get; private set; }

        public string Name => "SelfCancellingWriteTool";

        public string Description => "Cancels itself after the executor records that it started.";

        public CopilotToolCapabilityDescriptor Capability { get; } =
            CopilotToolCapabilityDescriptor.ProtectedWrite(
                CopilotToolIdempotency.NonIdempotent,
                TimeSpan.FromSeconds(5));

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken) => ExecuteApprovedAsync(request, toolInput, cancellationToken);

        public Task<CopilotToolResult> ExecuteApprovedAsync(
            CopilotAgentRequest request,
            CopilotAgentToolInput toolInput,
            CancellationToken cancellationToken)
        {
            if (performPartialWrite)
                CompletedWrites++;
            return Task.FromCanceled<CopilotToolResult>(new CancellationToken(canceled: true));
        }
    }
}
