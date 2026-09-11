using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotLateFinalizationCancellationTests
{
    [Theory]
    [InlineData("Agent budget used ", CopilotAgentControlIntent.Pause, false, CopilotAgentStopReason.Paused)]
    [InlineData("Agent budget used ", CopilotAgentControlIntent.Cancel, false, CopilotAgentStopReason.Cancelled)]
    [InlineData("Agent budget used ", CopilotAgentControlIntent.None, true, CopilotAgentStopReason.BudgetExhausted)]
    [InlineData("Agent budget used ", CopilotAgentControlIntent.Pause, true, CopilotAgentStopReason.Paused)]
    [InlineData("Agent budget used ", CopilotAgentControlIntent.Cancel, true, CopilotAgentStopReason.Cancelled)]
    [InlineData("Agent task ledger", CopilotAgentControlIntent.Pause, false, CopilotAgentStopReason.Paused)]
    [InlineData("Agent task ledger", CopilotAgentControlIntent.Cancel, false, CopilotAgentStopReason.Cancelled)]
    [InlineData("Agent task ledger", CopilotAgentControlIntent.None, true, CopilotAgentStopReason.BudgetExhausted)]
    public async Task LateInterruptionPreservesCompletedRunFacts(
        string triggerPrefix,
        CopilotAgentControlIntent controlIntent,
        bool exhaustTimeBudget,
        CopilotAgentStopReason expectedStopReason)
    {
        using var fixture = new RunFixture(triggerPrefix, controlIntent, exhaustTimeBudget);
        var result = await fixture.Start().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(fixture.CancellationTriggered);
        Assert.Equal(expectedStopReason, result.StopReason);
        Assert.Equal(330, result.Usage.EffectiveTotalTokens);
        Assert.Equal(330, result.Budget.ReportedTotalTokens);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(1, result.Budget.ToolCalls);
        Assert.Equal(expectedStopReason == CopilotAgentStopReason.BudgetExhausted, result.Budget.TimeBudgetExhausted);
        var publishedBudget = fixture.Events.Last(item => item.Type == CopilotAgentEventType.BudgetUpdated).Budget!;
        Assert.True(result.Budget.ConsumedTokens >= publishedBudget.ConsumedTokens);
        Assert.Equal(CopilotToolExecutionState.Completed, Assert.Single(result.StepRecords).Execution.State);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(2, fixture.Provider.StreamingCalls);
        Assert.Contains(fixture.Events, item => item.Type == CopilotAgentEventType.AnswerDelta && item.Text.Contains("Validation completed", StringComparison.Ordinal));
        Assert.Contains(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped && item.State == expectedStopReason.ToString());
        if (controlIntent == CopilotAgentControlIntent.Cancel)
            Assert.Null(result.SessionCheckpoint);
        else
        {
            Assert.NotNull(result.SessionCheckpoint);
            Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
            Assert.Contains(result.SessionCheckpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped && item.State == expectedStopReason.ToString());
        }

        var state = CopilotTurnEventReducer.Reduce(CopilotTurnEventState.Create(CopilotAgentMode.Code), new CopilotTurnStartedEvent(CopilotAgentMode.Code));
        foreach (var agentEvent in fixture.Events)
            state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnAgentEvent(agentEvent));
        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger)));
        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnTokenUsageUpdatedEvent(result.Usage));
        var turnResult = CopilotTurnResult.FromAgent(CopilotAgentMode.Code, result.Usage, result);
        state = CopilotTurnEventReducer.Reduce(state, new CopilotTurnCompletedEvent(turnResult));
        Assert.Same(turnResult, CopilotTurnEventReducer.RequireCompletion(state));
    }

    [Theory]
    [InlineData("Agent budget used ")]
    [InlineData("Agent task ledger")]
    public async Task LateCallerCancellationWithoutRunControlStillPropagates(string triggerPrefix)
    {
        using var fixture = new RunFixture(triggerPrefix, CopilotAgentControlIntent.Cancel, false, withRunControl: false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Start().WaitAsync(TimeSpan.FromSeconds(10)));
        Assert.True(fixture.CancellationTriggered);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.DoesNotContain(fixture.Events, item => item.Type == CopilotAgentEventType.Completed);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Pause, CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentControlIntent.Cancel, CopilotAgentStopReason.Cancelled)]
    public async Task LateControlKeepsFailedToolFactsWithoutPublishingAnObsoleteBlocker(
        CopilotAgentControlIntent controlIntent,
        CopilotAgentStopReason expectedStopReason)
    {
        using var fixture = new RunFixture("Agent task ledger", controlIntent, false);
        fixture.Tool.ShouldFail = true;
        var result = await fixture.Start().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(fixture.CancellationTriggered);
        Assert.Equal(expectedStopReason, result.StopReason);
        Assert.Equal(CopilotToolExecutionState.Failed, Assert.Single(result.StepRecords).Execution.State);
        Assert.Equal(330, result.Usage.EffectiveTotalTokens);
        Assert.Empty(result.Blockers);
        Assert.DoesNotContain(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.BlockerDetected);
        Assert.Contains(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted && item.State == CopilotToolExecutionState.Failed.ToString());
        if (result.SessionCheckpoint != null)
            Assert.DoesNotContain(result.SessionCheckpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.BlockerDetected);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Pause, false)]
    [InlineData(CopilotAgentControlIntent.Cancel, false)]
    [InlineData(CopilotAgentControlIntent.None, true)]
    public async Task CancellationAfterTheTerminalDecisionDoesNotRewriteTheCommittedOutcome(
        CopilotAgentControlIntent controlIntent,
        bool exhaustTimeBudget)
    {
        using var fixture = new RunFixture("Agent stop reason ", controlIntent, exhaustTimeBudget);
        var result = await fixture.Start().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.True(fixture.CancellationTriggered);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(330, result.Usage.EffectiveTotalTokens);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.False(result.Budget.TimeBudgetExhausted);
        Assert.NotNull(result.SessionCheckpoint);
        var terminal = Assert.Single(result.TaskEventJournal.Events.Where(item => item.Type == CopilotAgentTaskEventType.RunStopped));
        Assert.Equal(CopilotAgentStopReason.Completed.ToString(), terminal.State);
    }
    private sealed class RunFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotLateFinalizationCancellationTests-");
        private readonly CancellationTokenSource _linkedCancellation;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;

        public RunFixture(string triggerPrefix, CopilotAgentControlIntent controlIntent, bool exhaustTimeBudget, bool withRunControl = true)
        {
            TriggerPrefix = triggerPrefix;
            ControlIntent = controlIntent;
            ExhaustTimeBudget = exhaustTimeBudget;
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
        public CompletedAnswerChatClient Provider { get; } = new();
        public CopilotAgentRequest Request { get; }

        private string TriggerPrefix { get; }
        private CopilotAgentControlIntent ControlIntent { get; }
        private bool ExhaustTimeBudget { get; }
        public bool CancellationTriggered { get; private set; }

        private void ObserveEvent(CopilotAgentEvent agentEvent)
        {
            Events.Enqueue(agentEvent);
            if (CancellationTriggered || agentEvent.Type != CopilotAgentEventType.RuntimeDiagnostic
                || !agentEvent.Text.StartsWith(TriggerPrefix, StringComparison.Ordinal))
                return;

            // The two observable boundaries run after provider completion: before
            // ledger capture, and after ledger capture before the final checkpoint.
            CancellationTriggered = true;
            if (ControlIntent == CopilotAgentControlIntent.Pause)
                Request.RunControl?.RequestPause();
            else if (ControlIntent == CopilotAgentControlIntent.Cancel)
                Request.RunControl?.RequestCancel();
            if (ExhaustTimeBudget)
                TimeBudgetCancellation.Cancel();
            if (ControlIntent != CopilotAgentControlIntent.None)
                CallerCancellation.Cancel();
        }
        public Task<CopilotAgentRunResult> Start()
        {
            // Exercise the complete runtime with controllable cancellation sources;
            // the public wrapper only creates these sources using a wall-clock timer.
            var runCore = typeof(CopilotMicrosoftAgentFrameworkRuntime).GetMethod(
                "RunCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Task<CopilotAgentRunResult>)runCore.Invoke(_runtime,
            [
                Request,
                (Action<CopilotAgentEvent>)ObserveEvent,
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
        public bool ShouldFail { get; set; }
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
                Success = !ShouldFail,
                Summary = ShouldFail ? "Validation probe failed." : "Validation probe completed.",
                ProcessOperation = "test",
                ProcessExitCode = ShouldFail ? 1 : 0,
            });
        }
    }

    private sealed class CompletedAnswerChatClient : IChatClient
    {
        public int StreamingCalls { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The completed answer must not need another provider call.");

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
                : [new TextContent("Validation completed successfully: the bounded validation probe returned exit code 0."), usage])
            {
                FinishReason = call == 1 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
