using ColorVision.Solution;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColorVision.Copilot.Tests;

[Collection(CopilotChatViewModelProfileIsolationFixture.Name)]
public sealed class CopilotHostedTurnUsageTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);
    private static readonly CopilotTokenUsage PreviousUsage = new(900, 90, 990, 400);
    private static readonly CopilotTokenUsage CurrentUsage = new(120, 30, 150, 80);

    [Theory]
    [InlineData("cancel_review")]
    [InlineData("cancel_billed_review")]
    [InlineData("failed_primary")]
    public async Task GoalEvaluationInterruptionRetainsAllReportedUsage(string scenario)
    {
        CopilotAgentTaskHost? host = null;
        void CancelReview()
        {
            var active = Assert.IsType<CopilotHostedAgentRun>(host!.ActiveRun);
            Assert.True(host.RequestCancel(active.Id));
        }
        var cancelled = scenario != "failed_primary";
        using var handler = new GoalEvaluationCancellationHandler(scenario, CancelReview);
        using var client = new HttpClient(handler);
        var service = scenario == "cancel_billed_review"
            ? new CopilotChatService(client, 3, _ => TimeSpan.Zero, (_, token) =>
            {
                CancelReview();
                return Task.Delay(Timeout.InfiniteTimeSpan, token);
            })
            : new CopilotChatService(client);
        await using var fixture = new UsageFixture(
            reportUsage: true, previousUsageReported: false, chatService: service, completeAgentRun: true);
        host = fixture.Host;
        var conversation = fixture.Conversation;
        conversation.SetCustomTitle("Goal evaluation cancellation accounting");
        var now = DateTimeOffset.UtcNow;
        conversation.Goal = CopilotConversationGoal.Create("Verify the completed fixture work.", now)
            .WithTokenBudget(160, now);
        var user = new CopilotChatMessage(CopilotChatRole.User, "Complete the fixture work.")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        assistant.MarkThinkingStarted();
        conversation.Messages.Add(user);
        conversation.Messages.Add(assistant);
        var profile = fixture.ViewModel.SelectedProfile!.Clone();
        profile.BaseUrl = "https://unit.test/v1/responses";
        var prepared = new CopilotPreparedHostedTurn(
            conversation, profile, user, assistant,
            new CopilotAgentHostContextSnapshot(string.Empty, string.Empty, []),
            new CopilotTurnRuntimeConfigSnapshot(new CopilotAgentDefaultsConfig(), []),
            refreshExternalContext: false, isAutomaticGoalContinuation: false);
        var execute = typeof(CopilotChatViewModel)
            .GetMethod("ExecuteHostedPreparedTurnAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .CreateDelegate<Func<CopilotHostedAgentRun, CopilotPreparedHostedTurn, Task>>(fixture.ViewModel);
        fixture.Runtime.Release();

        var run = host.Start(conversation.Id, CopilotAgentMode.Auto, run => execute(run, prepared));
        try
        {
            await run.Completion.WaitAsync(TestTimeout);
        }
        catch (OperationCanceledException) when (cancelled && run.CancellationToken.IsCancellationRequested)
        {
        }

        Assert.False(assistant.WasResponseInterrupted, assistant.ResponseInterruptionDetail);
        Assert.Equal(CopilotAgentStopReason.Completed, assistant.AgentStopReason);
        Assert.Equal(cancelled, run.CancellationToken.IsCancellationRequested);
        Assert.Equal(cancelled ? 2 : 1, handler.RequestCount);
        Assert.Equal(cancelled ? CopilotAgentControlIntent.Cancel : CopilotAgentControlIntent.None, run.RunControl!.Intent);
        if (cancelled)
            Assert.False(run.AllowsAutomaticFollowUpDispatch);
        var expectedEvaluationUsage = scenario == "failed_primary"
            ? GoalEvaluationCancellationHandler.FailureUsage
            : GoalEvaluationCancellationHandler.PrimaryUsage.Add(scenario == "cancel_billed_review"
                ? GoalEvaluationCancellationHandler.FailureUsage : CopilotTokenUsage.Empty);
        var expectedUsage = CurrentUsage.Add(expectedEvaluationUsage);
        Assert.Equal(expectedUsage, assistant.ReportedUsage);
        Assert.Equal(expectedUsage, conversation.LastUsage);
        Assert.Equal(expectedUsage, CopilotConversationUsageDiagnostics.Capture(conversation).TotalUsage);
        var goal = Assert.IsType<CopilotConversationGoal>(conversation.Goal);
        Assert.Equal(cancelled ? CopilotConversationGoalState.Paused : CopilotConversationGoalState.BudgetLimited, goal.State);
        Assert.Equal(1, goal.TurnCount);
        Assert.Equal(cancelled ? 0 : 1, goal.EvaluationCount);
        Assert.Equal(expectedUsage.EffectiveTotalTokens, goal.TokensUsed);
        Assert.True(goal.IsTokenBudgetExhausted);
        Assert.Empty(fixture.Host.QueuedRuns);

        fixture.DiskStore.Save(fixture.State);
        var restored = Assert.Single(fixture.DiskStore.Load().Conversations);
        Assert.Equal(expectedUsage, restored.LastUsage);
        Assert.Equal(expectedUsage, restored.Messages.Single(message => message.Id == assistant.Id).ReportedUsage);
        Assert.Equal(goal.TokensUsed, restored.Goal!.TokensUsed);
        Assert.True(restored.Goal.IsTokenBudgetExhausted);
    }

    [Theory]
    [InlineData(false, true, true)]
    [InlineData(true, true, true)]
    [InlineData(false, false, true)]
    [InlineData(true, false, true)]
    [InlineData(false, true, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(true, false, false)]
    public async Task HostedChatInterruptionSettlesTheUsageReceivedThroughTurnEvents(
        bool cancelled, bool reportUsage, bool previousUsageReported)
    {
        await using var fixture = new UsageFixture(reportUsage, previousUsageReported);
        fixture.ViewModel.QueueExternalPrompt("Reply to this test request", startNewConversation: false, sendNow: true, mode: CopilotAgentMode.Chat);
        await fixture.Runtime.UsageProcessed.WaitAsync(TestTimeout);
        var assistant = fixture.Conversation.Messages.Last(message => !message.IsUser);
        var expectedUsage = reportUsage ? CurrentUsage : CopilotTokenUsage.Empty;
        var expectedPreviousUsage = previousUsageReported ? PreviousUsage : CopilotTokenUsage.Empty;
        Assert.Equal(expectedUsage, assistant.ReportedUsage);
        Assert.Equal(expectedPreviousUsage, fixture.Conversation.LastUsage);
        var run = Assert.IsType<CopilotHostedAgentRun>(fixture.Host.ActiveRun);

        if (cancelled)
            Assert.True(fixture.Host.RequestCancel(run.Id));
        else
            fixture.Runtime.Release();
        try
        {
            await run.Completion.WaitAsync(TestTimeout);
        }
        catch (OperationCanceledException) when (cancelled)
        {
        }

        Assert.True(assistant.WasResponseInterrupted);
        Assert.Contains("Partial provider answer", assistant.Content, StringComparison.Ordinal);
        Assert.Equal(expectedUsage, assistant.ReportedUsage);
        Assert.Equal(expectedUsage, fixture.Conversation.LastUsage);
        Assert.Equal(expectedPreviousUsage, fixture.PreviousAssistant.ReportedUsage);

        var state = fixture.State;
        for (var roundTrip = 0; roundTrip < 2; roundTrip++)
        {
            fixture.DiskStore.Save(state);
            state = fixture.DiskStore.Load();
            var restored = Assert.Single(state.Conversations);
            restored.EnsureValid();
            Assert.Equal(fixture.Conversation.Messages.Select(message => message.Id), restored.Messages.Select(message => message.Id));
            var previous = Assert.Single(restored.Messages, message => message.Id == fixture.PreviousAssistant.Id);
            var interrupted = Assert.Single(restored.Messages, message => message.Id == assistant.Id);
            Assert.False(previous.WasResponseInterrupted);
            Assert.True(interrupted.WasResponseInterrupted);
            Assert.Equal(expectedPreviousUsage, previous.ReportedUsage);
            Assert.Equal(expectedUsage, interrupted.ReportedUsage);
            Assert.Equal(expectedUsage, restored.LastUsage);
            var diagnostics = CopilotConversationUsageDiagnostics.Capture(restored);
            var expectedTrackedResponses = (previousUsageReported ? 1 : 0) + (reportUsage ? 1 : 0);
            Assert.Equal(expectedPreviousUsage.Add(expectedUsage), diagnostics.TotalUsage);
            Assert.Equal(reportUsage ? expectedUsage : expectedPreviousUsage, diagnostics.LastUsage);
            Assert.Equal(expectedTrackedResponses, diagnostics.TrackedResponses);
            Assert.Equal(2 - expectedTrackedResponses, diagnostics.UnreportedResponses);
            Assert.Equal(1, diagnostics.InterruptedResponses);
            Assert.Equal(0, diagnostics.ActiveResponses);
        }
    }

    [Fact]
    public void LegacyLastUsageStillBackfillsTheLatestCompletedAssistant()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("legacy-profile", "Legacy profile");
        var previous = new CopilotChatMessage(CopilotChatRole.Assistant, "Earlier unreported answer");
        var latest = new CopilotChatMessage(CopilotChatRole.Assistant, "Latest completed answer");
        conversation.Messages =
        [
            new CopilotChatMessage(CopilotChatRole.User, "Earlier request"), previous,
            new CopilotChatMessage(CopilotChatRole.User, "Latest request"), latest,
        ];
        conversation.SetLastUsage(CurrentUsage);

        Assert.True(conversation.EnsureValid());
        Assert.Equal(CopilotTokenUsage.Empty, previous.ReportedUsage);
        Assert.Equal(CurrentUsage, latest.ReportedUsage);
        Assert.Equal(CurrentUsage, CopilotConversationUsageDiagnostics.Capture(conversation).TotalUsage);
        Assert.False(conversation.EnsureValid());
    }

    private sealed class UsageFixture : IAsyncDisposable
    {
        private static readonly FieldInfo SolutionInstanceField = typeof(SolutionManager)
            .GetField("_instance", BindingFlags.Static | BindingFlags.NonPublic)!;
        private readonly object? _previousSolutionInstance = SolutionInstanceField.GetValue(null);
        private readonly object _testSolutionInstance = RuntimeHelpers.GetUninitializedObject(typeof(SolutionManager));
        private readonly string _root = Path.Combine(Path.GetTempPath(), "CopilotHostedUsage-" + Guid.NewGuid().ToString("N"));

        public CopilotConversationRecord Conversation { get; }
        public CopilotChatMessage PreviousAssistant { get; }
        public CopilotChatState State { get; }
        public CopilotChatStateStore DiskStore { get; }
        public CopilotChatViewModel ViewModel { get; }
        public CopilotAgentTaskHost Host { get; } = new();
        public UsageThenFailureRuntime Runtime { get; }

        public UsageFixture(bool reportUsage, bool previousUsageReported,
            CopilotChatService? chatService = null, bool completeAgentRun = false)
        {
            SolutionInstanceField.SetValue(null, _testSolutionInstance);
            Directory.CreateDirectory(_root);
            DiskStore = new CopilotChatStateStore(_root);
            var profile = new CopilotProfileConfig
            {
                Id = "hosted-usage-profile",
                Name = "Hosted usage profile",
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "hosted-usage-test-key",
                BaseUrl = "https://unit.test/v1",
                Model = "test-model",
            };
            Conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
            Conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Earlier request"));
            PreviousAssistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Earlier answer");
            if (previousUsageReported)
                PreviousAssistant.SetReportedUsage(PreviousUsage);
            Conversation.Messages.Add(PreviousAssistant);
            if (previousUsageReported)
                Conversation.SetLastUsage(PreviousUsage);
            State = new CopilotChatState
            {
                ActiveConversationId = Conversation.Id,
                ActiveProfileId = profile.Id,
                Conversations = [Conversation],
            };
            var config = new CopilotConfig
            {
                SchemaVersion = CopilotConfig.CurrentSchemaVersion,
                McpBearerToken = "hosted-usage-test-token",
                Profiles = [profile],
            };
            Runtime = new UsageThenFailureRuntime(reportUsage, completeAgentRun);
            ViewModel = new CopilotChatViewModel(chatService ?? new CopilotChatService(), new MemoryStateStore(State, _root), config, Runtime, Host);
        }

        public async ValueTask DisposeAsync()
        {
            try
            {
                Runtime.Release();
                if (Host.ActiveRun is { } active)
                {
                    try
                    {
                        await active.Completion.WaitAsync(TestTimeout);
                    }
                    catch (OperationCanceledException)
                    {
                    }
                }
            }
            finally
            {
                Host.Shutdown();
                ViewModel.Dispose();
                if (ReferenceEquals(SolutionInstanceField.GetValue(null), _testSolutionInstance))
                    SolutionInstanceField.SetValue(null, _previousSolutionInstance);
                var fullRoot = Path.GetFullPath(_root);
                var tempRoot = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
                if (fullRoot.StartsWith(tempRoot, StringComparison.OrdinalIgnoreCase)
                    && Path.GetFileName(fullRoot).StartsWith("CopilotHostedUsage-", StringComparison.Ordinal))
                {
                    Directory.Delete(fullRoot, recursive: true);
                }
            }
        }
    }

    private sealed class MemoryStateStore(CopilotChatState state, string root) : ICopilotChatStateStore
    {
        public string AttachmentDirectoryPath => root;
        public CopilotChatState Load() => state;
        public void Save(CopilotChatState value) { }
        public CopilotChatStateSnapshot CaptureSnapshot(CopilotChatState value) => new(new JObject());
        public string Serialize(CopilotChatStateSnapshot snapshot) => "{}";
        public string Serialize(CopilotChatState value) => "{}";
        public Task SaveSerializedAsync(string serializedState, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public int CleanupOrphanedAttachments(CopilotChatState value) => 0;
    }

    private sealed class GoalEvaluationCancellationHandler(string scenario, Action cancelReview) : HttpMessageHandler
    {
        public static readonly CopilotTokenUsage PrimaryUsage = new(10, 5, 15, 4);
        public static readonly CopilotTokenUsage FailureUsage = new(12, 8, 20, 3);
        public int RequestCount { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            RequestCount++;
            if (RequestCount == 2 && scenario == "cancel_review")
            {
                cancelReview();
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            if (scenario == "failed_primary" || RequestCount == 2 && scenario == "cancel_billed_review")
            {
                Assert.Equal(scenario == "failed_primary" ? 1 : 2, RequestCount);
                var code = scenario == "failed_primary" ? "insufficient_quota" : "server_error";
                var failure = new JObject
                {
                    ["id"] = "resp_goal_failure", ["object"] = "response", ["status"] = "failed", ["output"] = new JArray(),
                    ["error"] = new JObject { ["code"] = code, ["type"] = code, ["message"] = "Controlled evaluation failure." },
                    ["usage"] = new JObject
                    {
                        ["input_tokens"] = FailureUsage.InputTokens, ["output_tokens"] = FailureUsage.OutputTokens,
                        ["total_tokens"] = FailureUsage.TotalTokens,
                        ["input_tokens_details"] = new JObject { ["cached_tokens"] = FailureUsage.CachedInputTokens },
                    },
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(failure.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"),
                };
            }
            Assert.Equal(1, RequestCount);
            const string verdict = """
                {"verdict":"achieved","progress_score":100,"checkpoint":"Fixture work completed","verified":"All required evidence is present","remaining":"","next_step":"","reason":"All material conditions are verified."}
                """;
            var response = new JObject
            {
                ["id"] = "resp_goal_primary", ["object"] = "response", ["status"] = "completed",
                ["output"] = new JArray(new JObject
                {
                    ["type"] = "message", ["role"] = "assistant",
                    ["content"] = new JArray(new JObject { ["type"] = "output_text", ["text"] = verdict }),
                }),
                ["usage"] = new JObject
                {
                    ["input_tokens"] = PrimaryUsage.InputTokens, ["output_tokens"] = PrimaryUsage.OutputTokens,
                    ["total_tokens"] = PrimaryUsage.TotalTokens,
                    ["input_tokens_details"] = new JObject { ["cached_tokens"] = PrimaryUsage.CachedInputTokens },
                },
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(response.ToString(Newtonsoft.Json.Formatting.None), Encoding.UTF8, "application/json"),
            };
        }
    }

    private sealed class UsageThenFailureRuntime(bool reportUsage, bool completeAgentRun) : ICopilotTurnRuntime
    {
        private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource _usageProcessed = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task UsageProcessed => _usageProcessed.Task;
        public void Release() => _release.TrySetResult();

        public async IAsyncEnumerable<CopilotTurnEvent> RunAsync(
            CopilotTurnRequest request, [EnumeratorCancellation] CancellationToken cancellationToken)
        {
            yield return new CopilotTurnStartedEvent(request.TaskId, request.Mode);
            if (completeAgentRun)
                yield return new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("Fixture work completed."));
            else
            {
                yield return new CopilotTurnRequestPreparedEvent(new CopilotPreparedTurnRequest(request.UserText, false));
                yield return new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(string.Empty, "Partial provider answer"));
            }
            var events = new List<CopilotTurnEvent>();
            var sink = new CopilotTurnEventSink(events.Add);
            sink.OnTokenUsageUpdated(reportUsage ? CurrentUsage : CopilotTokenUsage.Empty);
            foreach (var turnEvent in events)
                yield return turnEvent;
            _usageProcessed.TrySetResult();
            await _release.Task.WaitAsync(cancellationToken);
            if (completeAgentRun)
            {
                var result = new CopilotAgentRunResult
                {
                    Usage = CurrentUsage, StopReason = CopilotAgentStopReason.Completed,
                };
                yield return new CopilotTurnPlanUpdatedEvent(CopilotTurnPlanSnapshot.FromTaskLedger(result.TaskLedger));
                yield return new CopilotTurnAgentEvent(CopilotAgentEvent.Completed());
                yield return CopilotTurnCompletedEvent.Completed(request.TaskId, CopilotTurnResult.FromAgent(
                    request.Mode, CurrentUsage, result));
                yield break;
            }
            throw new InvalidOperationException("Expected provider failure after partial answer.");
        }

        public CopilotSteeringAdmissionResult EnqueueSteeringMessage(string taskId, string message) => new(CopilotSteeringAdmissionReason.RuntimeUnavailable);
        public bool TryEnqueueBackgroundShellCommandCompletion(CopilotBackgroundShellCommandSnapshot snapshot) => false;
        public bool TryEnqueueBackgroundShellCommandOutput(CopilotBackgroundShellOutputMonitorEventArgs eventArgs) => false;
        public bool TryAnswerUserQuestion(string taskId, string requestId, string answer) => false;
        public Task<CopilotWorkspaceRollbackActionResult> RequestWorkspaceRollbackAsync(CopilotWorkspaceRollbackActionRequest request,
            Action<CopilotAgentEvent> onEvent, CancellationToken cancellationToken) => Task.FromException<CopilotWorkspaceRollbackActionResult>(new NotSupportedException());
    }
}
