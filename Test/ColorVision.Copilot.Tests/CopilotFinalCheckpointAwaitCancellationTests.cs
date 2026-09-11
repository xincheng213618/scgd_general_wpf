using ColorVision.Copilot;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFinalCheckpointAwaitCancellationTests
{
    private const string InterruptedCheckpointDiagnostic =
        "Agent control or total-time cancellation interrupted final checkpoint capture; the latest incremental checkpoint was retained.";

    [Theory]
    [InlineData(CopilotAgentControlIntent.Pause, false, CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentControlIntent.Cancel, false, CopilotAgentStopReason.Cancelled)]
    [InlineData(CopilotAgentControlIntent.None, true, CopilotAgentStopReason.BudgetExhausted)]
    [InlineData(CopilotAgentControlIntent.Pause, true, CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentControlIntent.Cancel, true, CopilotAgentStopReason.Cancelled)]
    public async Task CancellationDuringFinalSerializationRetainsFactsAndSealsTheFallback(
        CopilotAgentControlIntent intent,
        bool exhaustTimeBudget,
        CopilotAgentStopReason expectedStopReason)
    {
        using var fixture = new RunFixture();
        var run = fixture.Start();
        var incremental = await fixture.WaitForFinalSerializationAsync(run);

        fixture.Interrupt(intent, exhaustTimeBudget);
        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Gate.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(expectedStopReason, result.StopReason);
        Assert.Equal(expectedStopReason == CopilotAgentStopReason.BudgetExhausted, result.Budget.TimeBudgetExhausted);
        AssertRunFacts(fixture, result);
        Assert.Contains(fixture.Events, item => item.Text == InterruptedCheckpointDiagnostic);
        Assert.Equal(1, fixture.Gate.FinalSerializationCalls);
        Assert.Single(fixture.Events, item => item.Type == CopilotAgentEventType.Completed);
        AssertTerminalJournal(result, expectedStopReason);
        if (intent == CopilotAgentControlIntent.Cancel)
            Assert.Null(result.SessionCheckpoint);
        else
            AssertFallbackSession(incremental, Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint));
    }

    [Fact]
    public async Task CallerOnlyCancellationDuringFinalSerializationStillPropagates()
    {
        using var fixture = new RunFixture(withRunControl: false);
        var run = fixture.Start();
        await fixture.WaitForFinalSerializationAsync(run);

        fixture.Interrupt(CopilotAgentControlIntent.Cancel, exhaustTimeBudget: false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => run.WaitAsync(TimeSpan.FromSeconds(10)));
        await fixture.Gate.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(2, fixture.Provider.StreamingCalls);
        Assert.Equal(1, fixture.Gate.FinalSerializationCalls);
        Assert.DoesNotContain(fixture.Events, item => item.Type == CopilotAgentEventType.Completed);
        Assert.DoesNotContain(fixture.Events, item => item.Text == InterruptedCheckpointDiagnostic);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Pause, false)]
    [InlineData(CopilotAgentControlIntent.None, true)]
    public async Task InterruptedSerializationCannotMakeAnUncertainToolSessionResumable(
        CopilotAgentControlIntent intent,
        bool exhaustTimeBudget)
    {
        using var fixture = new RunFixture(uncertainToolOutcome: true);
        var run = fixture.Start();
        var incremental = await fixture.WaitForFinalSerializationAsync(run);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome,
            incremental.EvaluateFor(fixture.Request.Profile, fixture.Capabilities).Kind);

        fixture.Interrupt(intent, exhaustTimeBudget);
        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));
        await fixture.Gate.CancellationObserved.Task.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(intent == CopilotAgentControlIntent.Pause
            ? CopilotAgentStopReason.Paused : CopilotAgentStopReason.BudgetExhausted, result.StopReason);
        AssertRunFacts(fixture, result);
        Assert.Contains(fixture.Events, item => item.Text == InterruptedCheckpointDiagnostic);
        AssertTerminalJournal(result, result.StopReason);
        var retained = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
        AssertFallbackSession(incremental, retained);
        Assert.Equal(CopilotAgentSessionResumeRestriction.UncertainToolOutcome, retained.SessionResumeRestriction);
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome,
            retained.EvaluateFor(fixture.Request.Profile, fixture.Capabilities).Kind);

        // The latest visible journal may later be normalized, but that cannot
        // resolve the uncertain outcome belonging to this opaque Framework session.
        var settledJournal = new CopilotAgentTaskEventJournalBuilder();
        settledJournal.RecordRunStarted();
        settledJournal.RecordStop(CopilotAgentStopReason.Completed);
        var normalized = Assert.IsType<CopilotAgentSessionCheckpoint>(
            retained.CopyWithTaskEventJournal(settledJournal.Snapshot()));
        Assert.Equal(CopilotAgentSessionResumeRestriction.UncertainToolOutcome, normalized.SessionResumeRestriction);
    }

    [Fact]
    public async Task ReleasingTheWaitDelegatesToTheRealHarnessAndCompletesNormally()
    {
        using var fixture = new RunFixture();
        var run = fixture.Start();
        await fixture.WaitForFinalSerializationAsync(run);

        fixture.Gate.Release.TrySetResult();
        var result = await run.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        AssertRunFacts(fixture, result);
        AssertTerminalJournal(result, CopilotAgentStopReason.Completed);
        Assert.NotNull(result.SessionCheckpoint);
        Assert.True(result.SessionCheckpoint.IsStructurallyValid());
        Assert.False(fixture.Gate.CancellationObserved.Task.IsCompleted);
        Assert.Equal(1, fixture.Gate.FinalSerializationCalls);
        Assert.DoesNotContain(fixture.Events, item => item.Text == InterruptedCheckpointDiagnostic);
    }

    private static void AssertRunFacts(RunFixture fixture, CopilotAgentRunResult result)
    {
        Assert.Equal(330, result.Usage.EffectiveTotalTokens);
        Assert.Equal(330, result.Budget.ReportedTotalTokens);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(1, result.Budget.ToolCalls);
        Assert.Equal(1, fixture.Tool.CallCount);
        Assert.Equal(2, fixture.Provider.StreamingCalls);
        var step = Assert.Single(result.StepRecords);
        Assert.Equal(fixture.Tool.UncertainOutcome ? CopilotToolExecutionState.Failed : CopilotToolExecutionState.Completed,
            step.Execution.State);
        if (fixture.Tool.UncertainOutcome)
            Assert.Equal(CopilotToolFailureCode.OutcomeUnknown, step.Observation.FailureCode);
        Assert.Contains(fixture.Events, item => item.Type == CopilotAgentEventType.AnswerDelta
            && item.Text.Contains("Validation finished", StringComparison.Ordinal));
        var published = fixture.Events.Last(item => item.Type == CopilotAgentEventType.BudgetUpdated).Budget!;
        Assert.True(result.Budget.ConsumedTokens >= published.ConsumedTokens);
    }

    private static void AssertTerminalJournal(CopilotAgentRunResult result, CopilotAgentStopReason expected)
    {
        var terminal = Assert.Single(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);
        Assert.Equal(expected.ToString(), terminal.State);
        Assert.Contains(result.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.ToolCompleted);
        if (result.SessionCheckpoint is { } checkpoint)
        {
            var checkpointTerminal = Assert.Single(checkpoint.TaskEventJournal.Events, item => item.Type == CopilotAgentTaskEventType.RunStopped);
            Assert.Equal(terminal.Id, checkpointTerminal.Id);
            Assert.Equal(terminal.State, checkpointTerminal.State);
        }
    }

    private static void AssertFallbackSession(CopilotAgentSessionCheckpoint incremental, CopilotAgentSessionCheckpoint retained)
    {
        Assert.Equal(incremental.SerializedSessionJson, retained.SerializedSessionJson);
        Assert.Equal(incremental.ProfileKey, retained.ProfileKey);
        Assert.Equal(incremental.EnvironmentFingerprint, retained.EnvironmentFingerprint);
        Assert.Equal(incremental.HookSurfaceFingerprint, retained.HookSurfaceFingerprint);
        Assert.Equal(incremental.ProjectInstructionSurfaceFingerprint, retained.ProjectInstructionSurfaceFingerprint);
        Assert.Equal(incremental.AvailableToolNames, retained.AvailableToolNames);
    }

    private sealed class RunFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("CopilotFinalCheckpointAwaitTests-");
        private readonly CancellationTokenSource _callerCancellation = new();
        private readonly CancellationTokenSource _timeBudgetCancellation = new();
        private readonly CancellationTokenSource _linkedCancellation;
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;

        public RunFixture(bool withRunControl = true, bool uncertainToolOutcome = false)
        {
            Tool.UncertainOutcome = uncertainToolOutcome;
            _linkedCancellation = CancellationTokenSource.CreateLinkedTokenSource(_callerCancellation.Token, _timeBudgetCancellation.Token);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "final-checkpoint-await-tests", "Final checkpoint await tests", [Tool]);
            Capabilities = catalog.GetSnapshot();
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([Tool]), new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
                _ => Provider, new EmptyExternalToolProvider(), catalog, new CopilotAgentSkillUsageStore(_directory.FullName),
                new CopilotAutomaticApprovalReviewer(), decorateHarnessAgent: agent => new SerializationGateAgent(agent, Gate));
            Request = new CopilotAgentRequest
            {
                ConversationId = "final-checkpoint-await-conversation",
                TaskId = "final-checkpoint-await-task",
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

        public ConcurrentQueue<CopilotAgentEvent> Events { get; } = new();
        public SerializationGate Gate { get; } = new();
        public ValidationProbeTool Tool { get; } = new();
        public CompletedAnswerChatClient Provider { get; } = new();
        public CopilotAgentRequest Request { get; }
        public CopilotCapabilityCatalogSnapshot Capabilities { get; }

        private void ObserveEvent(CopilotAgentEvent item)
        {
            Events.Enqueue(item);
            if (item.Type == CopilotAgentEventType.RuntimeDiagnostic && item.Text.StartsWith("Agent task ledger · ", StringComparison.Ordinal))
                Gate.Arm();
        }

        public async Task<CopilotAgentSessionCheckpoint> WaitForFinalSerializationAsync(Task<CopilotAgentRunResult> run)
        {
            await Gate.Entered.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.False(run.IsCompleted);
            Assert.Equal(1, Gate.FinalSerializationCalls);
            Assert.Equal(1, Tool.CallCount);
            Assert.Equal(2, Provider.StreamingCalls);
            return Assert.IsType<CopilotAgentSessionCheckpoint>(Events.Last(item => item.Type == CopilotAgentEventType.CheckpointUpdated).SessionCheckpoint);
        }

        public void Interrupt(CopilotAgentControlIntent intent, bool exhaustTimeBudget)
        {
            if (intent == CopilotAgentControlIntent.Pause)
                Request.RunControl?.RequestPause();
            else if (intent == CopilotAgentControlIntent.Cancel)
                Request.RunControl?.RequestCancel();
            if (exhaustTimeBudget)
                _timeBudgetCancellation.Cancel();
            if (intent != CopilotAgentControlIntent.None)
                _callerCancellation.Cancel();
        }

        public Task<CopilotAgentRunResult> Start()
        {
            // The public wrapper only creates these cancellation sources. Invoke
            // the actual complete loop with a deterministic total-time source.
            var method = typeof(CopilotMicrosoftAgentFrameworkRuntime).GetMethod("RunCoreAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
            return (Task<CopilotAgentRunResult>)method.Invoke(_runtime,
            [Request, (Action<CopilotAgentEvent>)ObserveEvent, CopilotAgentRunBudget.Resolve(Request),
                Stopwatch.StartNew(), _timeBudgetCancellation, _callerCancellation.Token, _linkedCancellation.Token])!;
        }

        public void Dispose()
        {
            _callerCancellation.Cancel();
            Gate.Release.TrySetResult();
            _linkedCancellation.Dispose();
            _timeBudgetCancellation.Dispose();
            _callerCancellation.Dispose();
            _directory.Delete(recursive: true);
        }
    }

    private sealed class SerializationGate
    {
        private int _armed;
        private int _finalSerializationCalls;
        public bool IsArmed => Volatile.Read(ref _armed) != 0;
        public int FinalSerializationCalls => Volatile.Read(ref _finalSerializationCalls);
        public TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource CancellationObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public void Arm() => Volatile.Write(ref _armed, 1);

        public async Task WaitAsync(CancellationToken cancellationToken)
        {
            Assert.True(cancellationToken.CanBeCanceled);
            Assert.False(cancellationToken.IsCancellationRequested);
            Interlocked.Increment(ref _finalSerializationCalls);
            var wait = Release.Task.WaitAsync(cancellationToken);
            Assert.False(wait.IsCompleted);
            Entered.TrySetResult();
            try
            {
                await wait;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                CancellationObserved.TrySetResult();
                throw;
            }
        }
    }

    private sealed class SerializationGateAgent(AIAgent innerAgent, SerializationGate gate) : DelegatingAIAgent(innerAgent)
    {
        protected override async ValueTask<JsonElement> SerializeSessionCoreAsync(
            AgentSession session, JsonSerializerOptions? jsonSerializerOptions = null, CancellationToken cancellationToken = default)
        {
            // All services, streaming, sessions and unarmed serialization are the
            // real Harness. Only the final serialization is held at an async gate.
            if (gate.IsArmed)
                await gate.WaitAsync(cancellationToken);
            return await base.SerializeSessionCoreAsync(session, jsonSerializerOptions, cancellationToken);
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
        public bool UncertainOutcome { get; set; }
        public int CallCount { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;

        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            CallCount++;
            return Task.FromResult(new CopilotToolResult
            {
                ToolName = Name,
                Success = !UncertainOutcome,
                Summary = UncertainOutcome ? "The validation probe ended without a confirmed outcome." : "Validation probe completed.",
                FailureKind = UncertainOutcome ? CopilotToolFailureKind.OutcomeUnknown : CopilotToolFailureKind.None,
                FailureCode = UncertainOutcome ? CopilotToolFailureCode.OutcomeUnknown : string.Empty,
                ProcessOperation = UncertainOutcome ? string.Empty : "test",
                ProcessExitCode = UncertainOutcome ? null : 0,
            });
        }
    }

    private sealed class CompletedAnswerChatClient : IChatClient
    {
        public int StreamingCalls { get; private set; }
        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException("The completed answer must not require another provider call.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            var call = ++StreamingCalls;
            Assert.InRange(call, 1, 2);
            var usage = new UsageContent(new UsageDetails { InputTokenCount = call * 100, OutputTokenCount = call * 10, TotalTokenCount = call * 110 });
            yield return new ChatResponseUpdate(ChatRole.Assistant, call == 1
                ? [new FunctionCallContent("validation-call", "colorvision_run_workspace_validation", new Dictionary<string, object?>()), usage]
                : [new TextContent("Validation finished: the bounded validation probe returned its outcome; no further actions were performed."), usage])
            {
                FinishReason = call == 1 ? ChatFinishReason.ToolCalls : ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
