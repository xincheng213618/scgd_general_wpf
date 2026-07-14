using ColorVision.Copilot;
using ColorVision.Copilot.Mcp;
using System.Net;
using System.Net.Http;

namespace ColorVision.UI.Tests;

[Collection(CopilotSharedStateTestGroup.Name)]
public sealed class CopilotToolExecutorTests : IDisposable
{
    public CopilotToolExecutorTests()
    {
        CopilotToolExecutionAuditLogger.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ClearForTests();
    }

    [Fact]
    public async Task ExecuteAsync_EmitsLifecycleAndWritesRedactedAuditEntry()
    {
        var tool = new RecordingTool("Inspect", TimeSpan.FromSeconds(1));
        var executor = new CopilotToolExecutor();
        var events = new List<CopilotAgentEvent>();

        var outcome = await executor.ExecuteAsync(CreateInvocation(tool, new CopilotAgentToolInput
        {
            Query = "api_key=secret-value",
        }), events.Add, CancellationToken.None);

        Assert.True(outcome.Result.Success);
        Assert.Equal(CopilotToolExecutionState.Completed, outcome.Execution.State);
        Assert.False(string.IsNullOrWhiteSpace(outcome.Execution.CallId));
        Assert.Equal(outcome.Execution.CallId, outcome.StepRecord.Execution.CallId);
        Assert.Collection(events,
            item => Assert.Equal(CopilotAgentEventType.ToolStarted, item.Type),
            item => Assert.Equal(CopilotAgentEventType.ToolResult, item.Type));
        Assert.Equal(CopilotToolAccess.ReadOnly, events[0].ToolExecution?.Access);
        Assert.Contains("<redacted>", events[0].ToolExecution?.ArgumentSummary, StringComparison.Ordinal);

        var audit = Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries());
        Assert.Equal(outcome.Execution.CallId, audit.CallId);
        Assert.Equal("built-in", audit.RuntimeName);
        Assert.Equal(CopilotToolExecutionState.Completed, audit.State);
        Assert.Contains("<redacted>", audit.ArgumentSummary, StringComparison.Ordinal);
        Assert.DoesNotContain("secret-value", audit.ArgumentSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ExecuteAsync_UsesCapabilityDescriptorForPolicyAndAudit()
    {
        var tool = new CapabilityOnlyTool(new CopilotToolCapabilityDescriptor
        {
            Access = CopilotToolAccess.ReadOnly,
            RiskLevel = CopilotToolRiskLevel.Medium,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.Unknown,
            ConcurrencyMode = CopilotToolConcurrencyMode.SharedRead,
            ExecutionTimeout = TimeSpan.FromSeconds(2),
            AuditArgumentMode = CopilotToolAuditArgumentMode.NamesOnly,
        });
        var executor = new CopilotToolExecutor();
        var input = new CopilotAgentToolInput
        {
            Query = "sensitive-query-value",
            Arguments = new Dictionary<string, object?> { ["api_token"] = "secret-value" },
        };

        var outcome = await executor.ExecuteAsync(CreateInvocation(tool, input), _ => { }, CancellationToken.None);

        Assert.Equal(CopilotToolRiskLevel.Medium, outcome.Execution.RiskLevel);
        Assert.Equal(CopilotToolIdempotency.Unknown, outcome.Execution.Idempotency);
        Assert.Equal(CopilotToolConcurrencyMode.Exclusive, outcome.Execution.ConcurrencyMode);
        Assert.Equal(2_000, outcome.Execution.TimeoutMs);
        Assert.Equal("fields=api_token,query", outcome.Execution.ArgumentSummary);
        Assert.DoesNotContain("secret-value", outcome.Execution.ArgumentSummary, StringComparison.Ordinal);
        Assert.Equal(outcome.Execution.ArgumentSummary, Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries()).ArgumentSummary);
    }

    [Fact]
    public void ToolRegistry_RejectsUnsafeCapabilityDescriptor()
    {
        var tool = new CapabilityOnlyTool(new CopilotToolCapabilityDescriptor
        {
            Access = CopilotToolAccess.Write,
            RiskLevel = CopilotToolRiskLevel.High,
            ApprovalMode = CopilotToolApprovalMode.Never,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            ConcurrencyMode = CopilotToolConcurrencyMode.Exclusive,
        });

        var error = Assert.Throws<ArgumentException>(() => new CopilotToolRegistry([tool]));

        Assert.Contains("without approval", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_ConvertsTimeoutToFailedObservation()
    {
        var tool = new RecordingTool("SlowTool", TimeSpan.FromMilliseconds(20), waitForCancellation: true);
        var executor = new CopilotToolExecutor();

        var outcome = await executor.ExecuteAsync(CreateInvocation(tool, attempt: 1, maxAttempts: 2), _ => { }, CancellationToken.None);

        Assert.Equal(CopilotToolExecutionState.TimedOut, outcome.Execution.State);
        Assert.False(outcome.Result.Success);
        Assert.Contains("timeout", outcome.Result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(20, outcome.Execution.TimeoutMs);
        Assert.Equal(CopilotToolFailureKind.Transient, outcome.Execution.FailureKind);
        Assert.True(outcome.Execution.RetryEligible);
        var audit = Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries());
        Assert.Equal(CopilotToolExecutionState.TimedOut, audit.State);
        Assert.Equal(1, audit.Attempt);
        Assert.Equal(2, audit.MaxAttempts);
        Assert.True(audit.RetryEligible);
    }

    [Fact]
    public async Task ExecuteAsync_CancellationIsRecordedAndNeverRetryEligible()
    {
        var tool = new RecordingTool("CancelledTool", TimeSpan.FromSeconds(2), waitForCancellation: true);
        var executor = new CopilotToolExecutor();
        var events = new List<CopilotAgentEvent>();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(20));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => executor.ExecuteAsync(
            CreateInvocation(tool, attempt: 1, maxAttempts: 2),
            events.Add,
            cancellation.Token));

        var resultEvent = Assert.Single(events, item => item.Type == CopilotAgentEventType.ToolResult);
        Assert.Equal(CopilotToolExecutionState.Cancelled, resultEvent.ToolExecution?.State);
        Assert.Equal(CopilotToolFailureKind.Cancelled, resultEvent.ToolExecution?.FailureKind);
        Assert.False(resultEvent.ToolExecution?.RetryEligible);
        var audit = Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries());
        Assert.Equal(CopilotToolExecutionState.Cancelled, audit.State);
        Assert.False(audit.RetryEligible);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound, CopilotToolFailureKind.NotFound, false)]
    [InlineData(HttpStatusCode.Unauthorized, CopilotToolFailureKind.Authorization, false)]
    [InlineData(HttpStatusCode.TooManyRequests, CopilotToolFailureKind.Transient, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, CopilotToolFailureKind.Transient, true)]
    public async Task ExecuteAsync_ClassifiesHttpFailuresBeforeGrantingRetry(
        HttpStatusCode statusCode,
        CopilotToolFailureKind expectedFailureKind,
        bool expectedRetryEligible)
    {
        var tool = new HttpFailureTool(statusCode);
        var executor = new CopilotToolExecutor();

        var outcome = await executor.ExecuteAsync(
            CreateInvocation(tool, attempt: 1, maxAttempts: 2),
            _ => { },
            CancellationToken.None);

        Assert.Equal(expectedFailureKind, outcome.Execution.FailureKind);
        Assert.Equal(expectedRetryEligible, outcome.Execution.RetryEligible);
    }

    [Fact]
    public async Task ExecuteAsync_PreHookCanDenyWithoutRunningTool()
    {
        var tool = new RecordingTool("WriteTool", TimeSpan.FromSeconds(1));
        var hook = new DenyingHook();
        var executor = new CopilotToolExecutor(new[] { hook });
        var events = new List<CopilotAgentEvent>();

        var outcome = await executor.ExecuteAsync(CreateInvocation(tool), events.Add, CancellationToken.None);

        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Contains("policy denied", outcome.Result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
        Assert.True(hook.AfterCalled);
        Assert.Single(events);
        Assert.Equal(CopilotAgentEventType.ToolResult, events[0].Type);
    }

    [Fact]
    public async Task ExecuteAsync_WritePolicyDeniesToolWhenRequestNoLongerAuthorizesIt()
    {
        var tool = new RecordingTool(
            "WriteTool",
            TimeSpan.FromSeconds(1),
            access: CopilotToolAccess.Write,
            canHandle: false);
        var executor = new CopilotToolExecutor();

        var outcome = await executor.ExecuteAsync(CreateInvocation(tool), _ => { }, CancellationToken.None);

        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Contains("no longer authorizes", outcome.Result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_MapsStructuredApprovalAndLinksActionToCall()
    {
        var tool = new ApprovalTool();
        var executor = new CopilotToolExecutor();
        var statuses = new List<ConfirmableActionStatus>();
        void OnStatusChanged(object? _, ConfirmableActionChangedEventArgs args) => statuses.Add(args.Action.Status);
        CopilotMcpConfirmationStore.Instance.ActionStatusChanged += OnStatusChanged;
        try
        {
            var outcome = await executor.ExecuteAsync(CreateInvocation(tool), _ => { }, CancellationToken.None);
            var action = Assert.Single(CopilotMcpConfirmationStore.Instance.GetPendingActions());

            Assert.Equal(CopilotToolExecutionState.AwaitingApproval, outcome.Execution.State);
            Assert.Equal(action.ActionId, outcome.Execution.ApprovalActionId);
            Assert.Equal(outcome.Execution.CallId, action.AgentCallId);
            Assert.Equal(CopilotToolRiskLevel.High, outcome.Execution.RiskLevel);
            Assert.Equal(CopilotToolApprovalMode.Always, outcome.Execution.ApprovalMode);
            Assert.Equal(CopilotToolIdempotency.NonIdempotent, outcome.Execution.Idempotency);

            var executed = await CopilotMcpConfirmationStore.Instance.ApproveAndExecuteAsync(action.ActionId, CancellationToken.None);

            Assert.True(executed.Success);
            Assert.Contains(ConfirmableActionStatus.Approved, statuses);
            Assert.Contains(ConfirmableActionStatus.Executing, statuses);
            Assert.Contains(ConfirmableActionStatus.Executed, statuses);
        }
        finally
        {
            CopilotMcpConfirmationStore.Instance.ActionStatusChanged -= OnStatusChanged;
        }
    }

    [Fact]
    public async Task ExecuteAsync_DeniesHighRiskWriteToolWithoutApprovalPolicy()
    {
        var tool = new RecordingTool(
            "UnsafeWrite",
            TimeSpan.FromSeconds(1),
            access: CopilotToolAccess.Write,
            riskLevel: CopilotToolRiskLevel.High,
            approvalMode: CopilotToolApprovalMode.Never);
        var executor = new CopilotToolExecutor();

        var outcome = await executor.ExecuteAsync(CreateInvocation(tool), _ => { }, CancellationToken.None);

        Assert.Equal(0, tool.ExecutionCount);
        Assert.Equal(CopilotToolExecutionState.Denied, outcome.Execution.State);
        Assert.Contains("approval policy", outcome.Result.ErrorMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAsync_AllowsDifferentReadResourcesToRunConcurrentlyWithinBudget()
    {
        var tracker = new ConcurrencyTracker(expectedConcurrent: 4);
        var executor = new CopilotToolExecutor();
        var resourceKeys = CreateResourceKeysWithEarlyStripeCollision();
        var tasks = resourceKeys.Select((resourceKey, index) => executor.ExecuteAsync(
            CreateInvocation(new TrackedTool($"Read{index + 1}", tracker), new CopilotAgentToolInput { Query = resourceKey }),
            _ => { },
            CancellationToken.None)).ToArray();

        await tracker.ExpectedConcurrencyReached.Task.WaitAsync(TimeSpan.FromSeconds(2));
        tracker.Release();
        var outcomes = await Task.WhenAll(tasks);

        Assert.Equal(4, tracker.MaximumActive);
        Assert.All(outcomes, outcome => Assert.Equal(CopilotToolConcurrencyMode.SharedRead, outcome.Execution.ConcurrencyMode));
    }

    [Fact]
    public async Task ExecuteAsync_SerializesReadsWithSameResourceKey()
    {
        var first = new BlockingTool("FirstRead", "shared-resource");
        var second = new BlockingTool("SecondRead", "shared-resource");
        var executor = new CopilotToolExecutor();
        var firstTask = executor.ExecuteAsync(CreateInvocation(first), _ => { }, CancellationToken.None);
        await first.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));

        var secondTask = executor.ExecuteAsync(CreateInvocation(second), _ => { }, CancellationToken.None);
        await Task.Delay(50);
        Assert.False(second.Started.Task.IsCompleted);

        first.Release();
        await second.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        second.Release();
        var outcomes = await Task.WhenAll(firstTask, secondTask);

        Assert.Equal(outcomes[0].Execution.ConcurrencyKey, outcomes[1].Execution.ConcurrencyKey);
        Assert.StartsWith("resource:", outcomes[0].Execution.ConcurrencyKey, StringComparison.Ordinal);
        Assert.True(outcomes[1].Execution.QueueDurationMs > 0);
    }

    [Fact]
    public async Task ExecuteAsync_WriteCreatesGlobalBarrierForExistingAndNewReads()
    {
        var firstRead = new BlockingTool("FirstRead", "read-one");
        var write = new BlockingTool("Write", "write", CopilotToolAccess.Write, CopilotToolConcurrencyMode.SharedRead);
        var secondRead = new BlockingTool("SecondRead", "read-two");
        var executor = new CopilotToolExecutor();

        var firstReadTask = executor.ExecuteAsync(CreateInvocation(firstRead), _ => { }, CancellationToken.None);
        await firstRead.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var writeTask = executor.ExecuteAsync(CreateInvocation(write), _ => { }, CancellationToken.None);
        var secondReadTask = executor.ExecuteAsync(CreateInvocation(secondRead), _ => { }, CancellationToken.None);
        await Task.Delay(50);
        Assert.False(write.Started.Task.IsCompleted);
        Assert.False(secondRead.Started.Task.IsCompleted);

        firstRead.Release();
        await write.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.False(secondRead.Started.Task.IsCompleted);
        write.Release();
        await secondRead.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        secondRead.Release();

        var outcomes = await Task.WhenAll(firstReadTask, writeTask, secondReadTask);
        Assert.Equal(CopilotToolConcurrencyMode.Exclusive, outcomes[1].Execution.ConcurrencyMode);
        Assert.Equal(CopilotToolConcurrencyMode.SharedRead, outcomes[2].Execution.ConcurrencyMode);
    }

    [Fact]
    public async Task ExecuteAsync_CancelsQueuedReadWithoutInvokingTool()
    {
        var write = new BlockingTool("Write", "write", CopilotToolAccess.Write);
        var queuedRead = new BlockingTool("QueuedRead", "read");
        var executor = new CopilotToolExecutor();
        var writeTask = executor.ExecuteAsync(CreateInvocation(write), _ => { }, CancellationToken.None);
        await write.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        using var cancellation = new CancellationTokenSource();

        var readTask = executor.ExecuteAsync(CreateInvocation(queuedRead), _ => { }, cancellation.Token);
        await Task.Delay(50);
        cancellation.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => readTask);

        Assert.False(queuedRead.Started.Task.IsCompleted);
        var cancelledAudit = Assert.Single(CopilotToolExecutionAuditLogger.GetRecentEntries(), entry => entry.ToolName == "QueuedRead");
        Assert.Equal(CopilotToolExecutionState.Cancelled, cancelledAudit.State);
        Assert.True(cancelledAudit.QueueDurationMs > 0);

        write.Release();
        await writeTask;
    }

    public void Dispose()
    {
        CopilotToolExecutionAuditLogger.ClearForTests();
        CopilotMcpConfirmationStore.Instance.ClearForTests();
    }

    private static CopilotToolInvocation CreateInvocation(
        ICopilotTool tool,
        CopilotAgentToolInput? input = null,
        int attempt = 1,
        int maxAttempts = 1)
    {
        input ??= CopilotAgentToolInput.Empty;
        return new CopilotToolInvocation
        {
            Round = 2,
            Attempt = attempt,
            MaxAttempts = maxAttempts,
            RuntimeName = "built-in",
            Tool = tool,
            AgentRequest = new CopilotAgentRequest
            {
                Profile = new CopilotProfileConfig(),
                Mode = CopilotAgentMode.Auto,
                UserText = "explicit test request",
            },
            ToolInput = input,
            ToolCall = new CopilotToolCall
            {
                ToolName = tool.Name,
                ToolInput = input,
                Reason = "test",
            },
        };
    }

    private static string[] CreateResourceKeysWithEarlyStripeCollision()
    {
        const int stripeCount = 64;
        var firstByStripe = new Dictionary<int, string>();
        string? firstCollision = null;
        string? secondCollision = null;
        var collisionStripe = -1;
        for (var index = 0; index < 10_000 && firstCollision == null; index++)
        {
            var key = $"resource-{index}";
            var stripe = (StringComparer.OrdinalIgnoreCase.GetHashCode(key) & int.MaxValue) % stripeCount;
            if (firstByStripe.TryGetValue(stripe, out firstCollision))
            {
                secondCollision = key;
                collisionStripe = stripe;
            }
            else
            {
                firstByStripe[stripe] = key;
            }
        }

        Assert.NotNull(firstCollision);
        Assert.NotNull(secondCollision);
        var results = new List<string> { firstCollision!, secondCollision! };
        var usedStripes = new HashSet<int> { collisionStripe };
        for (var index = 10_000; results.Count < 6; index++)
        {
            var key = $"resource-{index}";
            var stripe = (StringComparer.OrdinalIgnoreCase.GetHashCode(key) & int.MaxValue) % stripeCount;
            if (usedStripes.Add(stripe))
                results.Add(key);
        }
        return results.ToArray();
    }

    private sealed class RecordingTool : ICopilotTool
    {
        private readonly bool _waitForCancellation;
        private readonly bool _canHandle;

        public RecordingTool(
            string name,
            TimeSpan executionTimeout,
            bool waitForCancellation = false,
            CopilotToolAccess access = CopilotToolAccess.ReadOnly,
            bool canHandle = true,
            CopilotToolRiskLevel? riskLevel = null,
            CopilotToolApprovalMode? approvalMode = null)
        {
            Name = name;
            ExecutionTimeout = executionTimeout;
            _waitForCancellation = waitForCancellation;
            Access = access;
            RiskLevel = riskLevel ?? (access == CopilotToolAccess.ReadOnly ? CopilotToolRiskLevel.Low : CopilotToolRiskLevel.Medium);
            ApprovalMode = approvalMode ?? (access == CopilotToolAccess.ReadOnly ? CopilotToolApprovalMode.Never : CopilotToolApprovalMode.Conditional);
            _canHandle = canHandle;
        }

        public string Name { get; }

        public string Description => "Deterministic execution-pipeline test tool.";

        public CopilotToolAccess Access { get; }

        public CopilotToolRiskLevel RiskLevel { get; }

        public CopilotToolApprovalMode ApprovalMode { get; }

        public TimeSpan ExecutionTimeout { get; }

        public int ExecutionCount { get; private set; }

        public bool CanHandle(CopilotAgentRequest request) => _canHandle;

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            ExecutionCount++;
            if (_waitForCancellation)
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);

            return new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Completed.",
            };
        }
    }

    private sealed class CapabilityOnlyTool(CopilotToolCapabilityDescriptor capability) : ICopilotTool
    {
        public string Name => "DescriptorTool";

        public string Description => "Exercises the canonical capability descriptor.";

        public CopilotToolCapabilityDescriptor Capability { get; } = capability;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Completed.",
            });
        }
    }

    private sealed class TrackedTool(string name, ConcurrencyTracker tracker) : ICopilotTool
    {
        public string Name { get; } = name;

        public string Description => "Tracks bounded read concurrency.";

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => toolInput.Query;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            await tracker.EnterAsync(cancellationToken);
            return new CopilotToolResult { ToolName = Name, Success = true, Summary = "Completed." };
        }
    }

    private sealed class BlockingTool(
        string name,
        string concurrencyKey,
        CopilotToolAccess access = CopilotToolAccess.ReadOnly,
        CopilotToolConcurrencyMode? declaredConcurrencyMode = null) : ICopilotTool
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string Name { get; } = name;

        public string Description => "Blocks until released by the test.";

        public CopilotToolAccess Access { get; } = access;

        public CopilotToolApprovalMode ApprovalMode => Access == CopilotToolAccess.Write ? CopilotToolApprovalMode.Conditional : CopilotToolApprovalMode.Never;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public CopilotToolConcurrencyMode ConcurrencyMode { get; } = declaredConcurrencyMode
            ?? (access == CopilotToolAccess.Write ? CopilotToolConcurrencyMode.Exclusive : CopilotToolConcurrencyMode.SharedRead);

        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public string GetConcurrencyKey(CopilotAgentRequest request, CopilotAgentToolInput toolInput) => concurrencyKey;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public async Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            Started.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            return new CopilotToolResult { ToolName = Name, Success = true, Summary = "Completed." };
        }

        public void Release() => _release.TrySetResult();
    }

    private sealed class ConcurrencyTracker(int expectedConcurrent)
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _active;
        private int _maximumActive;

        public TaskCompletionSource ExpectedConcurrencyReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int MaximumActive => Volatile.Read(ref _maximumActive);

        public async Task EnterAsync(CancellationToken cancellationToken)
        {
            var active = Interlocked.Increment(ref _active);
            UpdateMaximum(active);
            if (active >= expectedConcurrent)
                ExpectedConcurrencyReached.TrySetResult();
            try
            {
                await _release.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                Interlocked.Decrement(ref _active);
            }
        }

        public void Release() => _release.TrySetResult();

        private void UpdateMaximum(int value)
        {
            var current = Volatile.Read(ref _maximumActive);
            while (value > current)
            {
                var previous = Interlocked.CompareExchange(ref _maximumActive, value, current);
                if (previous == current)
                    return;
                current = previous;
            }
        }
    }

    private sealed class ApprovalTool : ICopilotTool
    {
        public string Name => "CreateProtectedResource";

        public string Description => "Creates a protected resource after approval.";

        public CopilotToolAccess Access => CopilotToolAccess.Write;

        public CopilotToolRiskLevel RiskLevel => CopilotToolRiskLevel.High;

        public CopilotToolApprovalMode ApprovalMode => CopilotToolApprovalMode.Always;

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.NonIdempotent;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            var action = CopilotMcpConfirmationStore.Instance.Create(
                "Create protected resource",
                "Create one deterministic test resource.",
                "confirmation-required",
                Name,
                "resource=test",
                _ => Task.FromResult(CopilotMcpToolCallResult.Ok("created")),
                executeOnApproval: true);
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Waiting for approval.",
                Approval = new CopilotToolApprovalInfo
                {
                    ActionId = action.ActionId,
                    Title = action.Title,
                    RiskLevel = action.RiskLevel,
                    ExpiresAtUtc = action.ExpiresAt,
                    ExecuteOnApproval = true,
                },
            });
        }
    }

    private sealed class HttpFailureTool(HttpStatusCode statusCode) : ICopilotTool
    {
        public string Name => "HttpFailure";

        public string Description => "Throws one deterministic HTTP failure.";

        public CopilotToolIdempotency Idempotency => CopilotToolIdempotency.Idempotent;

        public bool CanHandle(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            throw new HttpRequestException("Deterministic HTTP failure.", null, statusCode);
        }
    }

    private sealed class DenyingHook : ICopilotToolExecutionHook
    {
        public bool AfterCalled { get; private set; }

        public Task<CopilotToolExecutionHookDecision> BeforeExecuteAsync(CopilotToolExecutionHookContext context, CancellationToken cancellationToken)
        {
            return Task.FromResult(CopilotToolExecutionHookDecision.Deny("Policy denied this call."));
        }

        public Task AfterExecuteAsync(CopilotToolExecutionOutcome outcome, CancellationToken cancellationToken)
        {
            AfterCalled = true;
            return Task.CompletedTask;
        }
    }
}
