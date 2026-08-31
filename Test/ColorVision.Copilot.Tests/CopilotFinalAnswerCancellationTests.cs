using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFinalAnswerCancellationTests
{
    [Theory]
    [InlineData(CopilotAgentControlIntent.None, true, CopilotAgentStopReason.BudgetExhausted)]
    [InlineData(CopilotAgentControlIntent.Pause, false, CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentControlIntent.Cancel, false, CopilotAgentStopReason.Cancelled)]
    [InlineData(CopilotAgentControlIntent.Pause, true, CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentControlIntent.Cancel, true, CopilotAgentStopReason.Cancelled)]
    public async Task InterruptedFinalAnswerPreservesRunFactsAndCompletesTurnLifecycle(
        CopilotAgentControlIntent controlIntent,
        bool exhaustTimeBudget,
        CopilotAgentStopReason expectedStopReason)
    {
        using var fixture = new RunFixture(withRunControl: true);
        var runTask = fixture.Start();
        await fixture.Provider.FinalAnswerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
        var observedBudget = fixture.Events.Last(item => item.Type == CopilotAgentEventType.BudgetUpdated).Budget!;
        Assert.Equal(330, observedBudget.ReportedTotalTokens);
        Assert.Equal(2, observedBudget.ProviderCalls);
        Assert.True(observedBudget.ConsumedTokens > 0);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(0, fixture.Provider.FinalAnswerToolCount);
        Assert.Contains(fixture.Events, item =>
            item.Type == CopilotAgentEventType.CheckpointUpdated && item.SessionCheckpoint != null);

        if (controlIntent == CopilotAgentControlIntent.Pause)
            Assert.True(fixture.Request.RunControl!.RequestPause());
        else if (controlIntent == CopilotAgentControlIntent.Cancel)
            Assert.True(fixture.Request.RunControl!.RequestCancel());
        if (exhaustTimeBudget)
            fixture.TimeBudgetCancellation.Cancel();
        if (controlIntent != CopilotAgentControlIntent.None)
            fixture.CallerCancellation.Cancel();

        var result = await runTask.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Equal(expectedStopReason, result.StopReason);
        Assert.Equal(330, result.Usage.EffectiveTotalTokens);
        Assert.Equal(330, result.Budget.ReportedTotalTokens);
        Assert.True(result.Budget.ConsumedTokens >= observedBudget.ConsumedTokens);
        Assert.Equal(3, result.Budget.ProviderCalls);
        Assert.Equal(expectedStopReason == CopilotAgentStopReason.BudgetExhausted, result.Budget.TimeBudgetExhausted);
        Assert.Equal(1, result.Budget.ToolCalls);
        var step = Assert.Single(result.StepRecords);
        Assert.Equal(CopilotToolExecutionState.Completed, step.Execution.State);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(2, fixture.Provider.StreamingCalls);
        Assert.Equal(1, fixture.Provider.FinalAnswerCalls);
        Assert.Contains(result.TaskEventJournal.Events, item =>
            item.Type == CopilotAgentTaskEventType.RunStopped
            && item.State == expectedStopReason.ToString());
        Assert.DoesNotContain(fixture.Events, item =>
            item.Type == CopilotAgentEventType.AnswerDelta && !string.IsNullOrWhiteSpace(item.Text));

        if (controlIntent == CopilotAgentControlIntent.Cancel)
        {
            Assert.Null(result.SessionCheckpoint);
        }
        else
        {
            Assert.NotNull(result.SessionCheckpoint);
            Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
                item.Type == CopilotAgentTaskEventType.ToolCompleted);
            Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item =>
                item.Type == CopilotAgentTaskEventType.RunStopped
                && item.State == expectedStopReason.ToString());
        }

        var turnState = CopilotTurnEventReducer.Reduce(
            CopilotTurnEventState.Create(CopilotAgentMode.Code),
            new CopilotTurnStartedEvent(CopilotAgentMode.Code));
        foreach (var agentEvent in fixture.Events)
            turnState = CopilotTurnEventReducer.Reduce(turnState, new CopilotTurnAgentEvent(agentEvent));
        turnState = CopilotTurnEventReducer.Reduce(
            turnState, new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger)));
        turnState = CopilotTurnEventReducer.Reduce(turnState, new CopilotTurnTokenUsageUpdatedEvent(result.Usage));
        var turnResult = CopilotTurnResult.FromAgent(CopilotAgentMode.Code, result.Usage, result);
        turnState = CopilotTurnEventReducer.Reduce(turnState, new CopilotTurnCompletedEvent(turnResult));
        Assert.Same(turnResult, CopilotTurnEventReducer.RequireCompletion(turnState));
    }

    [Fact]
    public async Task CallerCancellationWithoutRunControlStillPropagatesCancellation()
    {
        using var fixture = new RunFixture(withRunControl: false);
        var runTask = fixture.Start();
        await fixture.Provider.FinalAnswerStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));

        fixture.CallerCancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask.WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.False(fixture.TimeBudgetCancellation.IsCancellationRequested);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(1, fixture.Provider.FinalAnswerCalls);
        Assert.DoesNotContain(fixture.Events, item => item.Type == CopilotAgentEventType.Completed);
    }

    private sealed class RunFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotFinalAnswerCancellationTests-");
        private readonly CancellationTokenSource _linkedCancellation;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;

        public RunFixture(bool withRunControl)
        {
            _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(
                CallerCancellation.Token, TimeBudgetCancellation.Token);
            var capabilityCatalog = new CopilotCapabilityCatalog();
            capabilityCatalog.PublishSource(
                CopilotCapabilitySourceKind.BuiltIn,
                "final-answer-cancellation-tests",
                "Final answer cancellation tests",
                [Tool]);
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([Tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _ => Provider,
                new EmptyExternalToolProvider(),
                capabilityCatalog,
                new CopilotAgentSkillUsageStore(_directory.FullName));
            Request = new CopilotAgentRequest
            {
                ConversationId = "final-answer-cancellation-conversation",
                TaskId = "final-answer-cancellation-task",
                WorkspacePath = _directory.FullName,
                UserText = "Run the bounded workspace validation probe.",
                TaskIntentText = "Run the bounded workspace validation probe.",
                Profile = new CopilotProfileConfig
                {
                    VendorType = CopilotVendorType.Custom,
                    ProviderType = CopilotProviderType.OpenAICompatible,
                    ApiKey = "test-key",
                    BaseUrl = "https://example.test/v1",
                    Model = "test-model",
                    MaxTokens = 4_096,
                },
                Mode = CopilotAgentMode.Code,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                RunControl = withRunControl ? new CopilotAgentRunControl() : null,
                CodexApprovalPolicy = CopilotCodexApprovalPolicy.CreateScalar(CopilotCodexApprovalPolicyMode.Untrusted),
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 2,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
        }

        public CancellationTokenSource CallerCancellation { get; } = new();
        public CancellationTokenSource TimeBudgetCancellation { get; } = new();
        public ConcurrentQueue<CopilotAgentEvent> Events { get; } = new();
        public ValidationProbeTool Tool { get; } = new();
        public FinalAnswerGateChatClient Provider { get; } = new();
        public CopilotAgentRequest Request { get; }

        public Task<CopilotAgentRunResult> Start()
        {
            // Exercise the complete runtime with controllable cancellation sources;
            // the public wrapper only creates these sources using a wall-clock timer.
            var runCore = typeof(CopilotMicrosoftAgentFrameworkRuntime).GetMethod(
                "RunCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Task<CopilotAgentRunResult>)runCore.Invoke(_runtime,
            [
                Request,
                (Action<CopilotAgentEvent>)Events.Enqueue,
                CopilotAgentRunBudget.Resolve(Request),
                Stopwatch.StartNew(),
                TimeBudgetCancellation,
                CallerCancellation.Token,
                _linkedCancellation.Token,
            ])!;
        }

        public void Dispose()
        {
            CallerCancellation.Cancel();
            _linkedCancellation.Dispose();
            TimeBudgetCancellation.Dispose();
            CallerCancellation.Dispose();
            _directory.Delete(recursive: true);
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new CopilotExternalToolLease());
        }
    }

    private sealed class ValidationProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "RunWorkspaceValidation";
        public string Description => "Returns deterministic validation evidence.";
        public int CallCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(
            CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = true,
                Summary = "Validation probe completed.",
                ProcessOperation = "test",
                ProcessExitCode = 0,
            });
        }
    }

    private sealed class FinalAnswerGateChatClient : IChatClient
    {
        private readonly TaskCompletionSource<ChatResponse> _pendingFinalAnswer = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource FinalAnswerStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public int StreamingCalls { get; private set; }
        public int FinalAnswerCalls { get; private set; }
        public int FinalAnswerToolCount { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            FinalAnswerCalls++;
            FinalAnswerToolCount = options?.Tools?.Count ?? 0;
            FinalAnswerStarted.TrySetResult();
            return await _pendingFinalAnswer.Task.WaitAsync(cancellationToken);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            var call = ++StreamingCalls;
            Assert.InRange(call, 1, 2);
            var usage = new UsageContent(new UsageDetails
            {
                InputTokenCount = call * 100,
                OutputTokenCount = call * 10,
                TotalTokenCount = call * 110,
            });
            yield return new ChatResponseUpdate(ChatRole.Assistant, call == 1
                ? [new FunctionCallContent("validation-call", "colorvision_run_workspace_validation", new Dictionary<string, object?>()), usage]
                : [usage])
            {
                FinishReason = call == 1 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
