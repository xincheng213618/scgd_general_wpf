using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit.Abstractions;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotInitialCheckpointPromptRecoveryTests(ITestOutputHelper output)
{
    [Theory]
    [InlineData(false, "initial")]
    [InlineData(true, "initial")]
    [InlineData(false, "provider_failure")]
    [InlineData(true, "provider_failure")]
    [InlineData(false, "pause_before_provider")]
    [InlineData(true, "pause_before_provider")]
    public async Task CheckpointRecoveryPreservesPromptConstraintsExactlyOnce(bool resumeExistingSession, string checkpointMoment)
    {
        using var fixture = new RuntimeFixture();
        CopilotAgentSessionCheckpoint? previousCheckpoint = null;
        var visibleHistory = new List<CopilotRequestMessage>();
        if (resumeExistingSession)
        {
            const string seedRequest = "Record the seed notes before the next request.";
            var seed = await fixture.RunAsync(null, [], seedRequest);
            previousCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(seed.Result.SessionCheckpoint);
            visibleHistory.Add(new("user", seedRequest));
            visibleHistory.Add(new("assistant", RecordingChatClient.Answer));
        }

        const string historyConstraint = "The earlier constraint is checkpoint-history-unique-731.";
        const string currentConstraint = "The current constraint is checkpoint-current-unique-824.";
        visibleHistory.Add(new("user", historyConstraint));
        visibleHistory.Add(new("assistant", "The earlier constraint was acknowledged."));
        var interrupted = await fixture.RunAsync(previousCheckpoint, visibleHistory, currentConstraint,
            checkpointMoment == "initial" ? "complete" : checkpointMoment);
        Assert.Equal(resumeExistingSession, interrupted.SessionResumed);

        // The initial case simulates process exit immediately after the first checkpoint;
        // completing that synthetic run only releases its resources. The other cases use
        // the actual final checkpoint returned after provider rejection or user pause.
        var persistedJson = checkpointMoment == "initial"
            ? interrupted.InitialCheckpointJson
            : JsonConvert.SerializeObject(Assert.IsType<CopilotAgentSessionCheckpoint>(interrupted.Result.SessionCheckpoint));
        var restored = Assert.IsType<CopilotAgentSessionCheckpoint>(
            JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(persistedJson));
        if (checkpointMoment == "initial")
        {
            Assert.DoesNotContain(historyConstraint, restored.SerializedSessionJson, StringComparison.Ordinal);
            Assert.DoesNotContain(currentConstraint, restored.SerializedSessionJson, StringComparison.Ordinal);
        }
        output.WriteLine("Checkpoint memory contains history: {0}; current user: {1}",
            restored.ConversationMemory.Any(message => message.Content.Contains(historyConstraint, StringComparison.Ordinal)),
            restored.ConversationMemory.Any(message => message.Content.Contains(currentConstraint, StringComparison.Ordinal)));
        output.WriteLine("Serialized session contains history: {0}; current user: {1}",
            restored.SerializedSessionJson.Contains(historyConstraint, StringComparison.Ordinal),
            restored.SerializedSessionJson.Contains(currentConstraint, StringComparison.Ordinal));

        visibleHistory.Add(new("user", currentConstraint));
        var recovered = await fixture.RunAsync(restored, visibleHistory, "Continue using the supplied constraints.");

        Assert.True(recovered.SessionResumed);
        Assert.Single(recovered.ProviderUserMessages, text => text.Contains(historyConstraint, StringComparison.Ordinal));
        Assert.Single(recovered.ProviderUserMessages, text => text.Contains(currentConstraint, StringComparison.Ordinal));
    }

    [Fact]
    public async Task ReplannedInitialCheckpointRetainsCheckpointOnlySteeringAcrossAnotherRestart()
    {
        using var fixture = new RuntimeFixture();
        const string originalRequest = "Record the original notes.";
        const string hiddenSteering = "Preserve checkpoint-only-steering-unique-925 in the summary.";
        var seed = await fixture.RunAsync(null, [], originalRequest, steering: hiddenSteering);
        Assert.Single(seed.ProviderUserMessages, text => text.Contains(hiddenSteering, StringComparison.Ordinal));
        var oldCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(seed.Result.SessionCheckpoint);
        Assert.Contains(oldCheckpoint.ConversationMemory, message => message.IsSteering && message.Content == hiddenSteering);
        var visibleHistory = new List<CopilotRequestMessage>
        {
            new("user", originalRequest),
            new("assistant", RecordingChatClient.Answer),
        };

        fixture.UseDifferentModel();
        const string replanRequest = "Continue the summary with the selected model.";
        var replanned = await fixture.RunAsync(oldCheckpoint, visibleHistory, replanRequest);
        Assert.False(replanned.SessionResumed);
        Assert.Single(replanned.ProviderUserMessages, text => text.Contains(hiddenSteering, StringComparison.Ordinal));
        var restored = Assert.IsType<CopilotAgentSessionCheckpoint>(
            JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(replanned.InitialCheckpointJson));
        Assert.DoesNotContain(hiddenSteering, restored.SerializedSessionJson, StringComparison.Ordinal);

        visibleHistory.Add(new("user", replanRequest));
        var recovered = await fixture.RunAsync(restored, visibleHistory, "Continue after reopening the conversation.");

        Assert.True(recovered.SessionResumed);
        Assert.Single(recovered.ProviderUserMessages, text => text.Contains(hiddenSteering, StringComparison.Ordinal));
    }

    [Fact]
    public async Task SteeringDeliveredByTheSecondProviderRequestIsNotAppendedAgainAfterResume()
    {
        using var fixture = new RuntimeFixture();
        const string originalRequest = "Record the notes before the late constraint arrives.";
        const string lateSteering = "Preserve second-provider-steering-unique-436 in the summary.";
        var seed = await fixture.RunAsync(null, [], originalRequest,
            steering: lateSteering, steerDuringFirstRequest: true);

        Assert.Equal(2, seed.ProviderRequests.Length);
        Assert.DoesNotContain(seed.ProviderRequests[0], text => text.Contains(lateSteering, StringComparison.Ordinal));
        Assert.Single(seed.ProviderRequests[1], text => text.Contains(lateSteering, StringComparison.Ordinal));
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(seed.Result.SessionCheckpoint);
        var restored = Assert.IsType<CopilotAgentSessionCheckpoint>(
            JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(JsonConvert.SerializeObject(checkpoint)));
        var visibleHistory = checkpoint.ConversationMemory.Where(message => !message.IsSteering).ToArray();
        Assert.DoesNotContain(visibleHistory, message => message.Content.Contains(lateSteering, StringComparison.Ordinal));

        var recovered = await fixture.RunAsync(restored, visibleHistory, "Continue after reopening the conversation.");

        Assert.True(recovered.SessionResumed);
        Assert.Single(recovered.ProviderRequests);
        Assert.Single(recovered.ProviderUserMessages, text => text.Contains(lateSteering, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PendingSteeringRecoveryPreservesOtherMessagesAndDelegatedInput(bool delegated)
    {
        using var fixture = new RuntimeFixture();
        const string originalRequest = "Summarize the original notes.";
        const string pendingSteering = "Apply pending-steering-unique-683 only to the interrupted request.";
        const string backgroundNotice = "background-notice-unique-257: the observation is ready.";
        const string developerHook = "developer-hook-unique-491: preserve the context boundary.";
        var original = await fixture.RunAsync(null, [], originalRequest,
            steering: pendingSteering, steerDuringFirstRequest: true, delegated: delegated);
        Assert.DoesNotContain(original.ProviderRequests[0], text => text.Contains(pendingSteering, StringComparison.Ordinal));
        Assert.NotNull(original.PendingSteeringCheckpointJson);

        // This earlier snapshot was captured after the first successful request, while
        // its pending injected message had not yet reached the second provider request.
        var restored = Assert.IsType<CopilotAgentSessionCheckpoint>(
            JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(original.PendingSteeringCheckpointJson));
        Assert.True(restored.IsStructurallyValid());
        Assert.Contains(pendingSteering, restored.SerializedSessionJson, StringComparison.Ordinal);
        var session = JObject.Parse(restored.SerializedSessionJson);
        var pendingQueue = Assert.IsType<JArray>(session["stateBag"]?["MessageInjectingChatClient.PendingInjectedMessages"]);
        var pendingMessage = Assert.IsType<JObject>(Assert.Single(pendingQueue));
        var pendingMessageId = Assert.IsType<string>(pendingMessage.Value<string>("messageId"));
        var steeringSubjectId = CopilotAgentTaskEventIds.ForSteering(pendingSteering);
        pendingQueue.AddFirst(EncodeMessage(ChatRole.User, backgroundNotice, "background-notice-257"));
        // A matching identifier prefix alone must never classify developer context as user steering.
        pendingQueue.Add(EncodeMessage(new ChatRole("developer"), developerHook, "colorvision-steering-developer-hook-491"));
        using var reader = new JsonTextReader(new StringReader(original.PendingSteeringCheckpointJson))
        {
            // Event IDs include the original DateTimeOffset representation; preserve it
            // while changing only the pending message queue in the captured checkpoint.
            DateParseHandling = DateParseHandling.None,
        };
        var persisted = JObject.Load(reader);
        persisted[nameof(CopilotAgentSessionCheckpoint.SerializedSessionJson)] = session.ToString(Formatting.None);
        restored = Assert.IsType<CopilotAgentSessionCheckpoint>(
            JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(persisted.ToString(Formatting.None)));
        Assert.True(restored.IsStructurallyValid());
        if (!delegated)
        {
            const string newerDraft = "A newer unsent draft must survive recovery.";
            var conversation = CopilotConversationRecord.CreateEmpty("checkpoint-test-profile", "Checkpoint test");
            conversation.DraftText = newerDraft;
            Assert.True(conversation.SetAgentSessionCheckpoint(restored));
            Assert.True(CopilotSteeringRecovery.TrackPending(conversation,
                restored.TaskEventJournal.Events.Last(item => item.Type == CopilotAgentTaskEventType.RunStarted).RunId,
                new CopilotSteeringMessageSnapshot(pendingMessageId, pendingSteering), DateTimeOffset.UtcNow));
            var state = new CopilotChatState { ActiveConversationId = conversation.Id, Conversations = [conversation] };
            fixture.StateStore.Save(state);
            var reopened = fixture.StateStore.Load();
            var reopenedConversation = Assert.Single(reopened.Conversations);
            var reopenedCheckpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(reopenedConversation.AgentSessionCheckpoint);
            Assert.True(reopenedCheckpoint.IsStructurallyValid());
            Assert.Equal(pendingMessageId, Assert.Single(reopenedConversation.PendingSteeringRecoveries).MessageId);
            Assert.True(CopilotSteeringRecovery.RestorePendingToDrafts(reopened));
            Assert.Empty(reopenedConversation.PendingSteeringRecoveries);
            Assert.StartsWith(newerDraft, reopenedConversation.DraftText, StringComparison.Ordinal);
            Assert.Contains(pendingSteering, reopenedConversation.DraftText, StringComparison.Ordinal);
            Assert.Equal(reopenedConversation.DraftText.IndexOf(pendingSteering, StringComparison.Ordinal),
                reopenedConversation.DraftText.LastIndexOf(pendingSteering, StringComparison.Ordinal));
            Assert.True(CopilotAgentSessionCheckpoint.AreEquivalent(restored, reopenedConversation.AgentSessionCheckpoint));
            fixture.StateStore.Save(reopened);
            var reopenedAgain = fixture.StateStore.Load();
            var settledConversation = Assert.Single(reopenedAgain.Conversations);
            Assert.False(CopilotSteeringRecovery.RestorePendingToDrafts(reopenedAgain));
            Assert.Equal(reopenedConversation.DraftText, settledConversation.DraftText);
            Assert.True(CopilotAgentSessionCheckpoint.AreEquivalent(restored, settledConversation.AgentSessionCheckpoint));
            restored = Assert.IsType<CopilotAgentSessionCheckpoint>(settledConversation.AgentSessionCheckpoint);
        }
        var visibleHistory = restored.ConversationMemory.Where(message => !message.IsSteering).ToArray();
        Assert.DoesNotContain(visibleHistory, message => message.Content.Contains(pendingSteering, StringComparison.Ordinal));
        if (delegated)
        {
            var paused = await fixture.RunAsync(restored, visibleHistory,
                "Pause the delegated summary before its next provider request.",
                ending: "pause_before_provider", delegated: true);
            Assert.True(paused.SessionResumed);
            Assert.Empty(paused.ProviderInputs);
            Assert.Equal(0, paused.DeliveredSteeringCount);
            Assert.Equal(1, paused.UndeliveredSteeringCount);
            var undelivered = Assert.Single(paused.RecoveredSteeringMessages);
            Assert.Equal(pendingMessageId, undelivered.MessageId);
            Assert.Equal(pendingSteering, undelivered.Text);
            restored = ReloadCheckpoint(paused.Result);
            Assert.Single(restored.TaskEventJournal.Events,
                item => item.Type == CopilotAgentTaskEventType.SteeringQueued && item.SubjectId == steeringSubjectId);
            Assert.DoesNotContain(restored.TaskEventJournal.Events,
                item => item.Type == CopilotAgentTaskEventType.SteeringDelivered && item.SubjectId == steeringSubjectId);
            Assert.DoesNotContain(restored.ConversationMemory,
                message => message.IsSteering && message.Content == pendingSteering);
            visibleHistory = restored.ConversationMemory.Where(message => !message.IsSteering).ToArray();
        }

        var recovered = await fixture.RunAsync(restored, visibleHistory,
            "Summarize a new set of notes for this new request.", delegated: delegated);

        Assert.True(recovered.SessionResumed);
        var input = Assert.Single(recovered.ProviderInputs);
        Assert.Single(input, message => message.Role == ChatRole.User && message.Text.Contains(originalRequest, StringComparison.Ordinal));
        Assert.Single(input, message => message.Role == ChatRole.Assistant && message.Text == RecordingChatClient.Answer);
        var background = Assert.Single(input, message => message.Text == backgroundNotice);
        Assert.Equal(ChatRole.User, background.Role);
        Assert.Equal("background-notice-257", background.MessageId);
        var hook = Assert.Single(input, message => message.Text == developerHook);
        Assert.Equal(new ChatRole("developer"), hook.Role);
        Assert.Equal("colorvision-steering-developer-hook-491", hook.MessageId);
        Assert.True(Array.IndexOf(input, background) < Array.IndexOf(input, hook));
        if (delegated)
        {
            Assert.Equal(1, recovered.DeliveredSteeringCount);
            Assert.Equal(0, recovered.UndeliveredSteeringCount);
            var steering = Assert.Single(input, message => message.Text == pendingSteering);
            Assert.Equal(pendingMessageId, steering.MessageId);
            Assert.True(Array.IndexOf(input, background) < Array.IndexOf(input, steering));
            Assert.True(Array.IndexOf(input, steering) < Array.IndexOf(input, hook));

            var deliveredCheckpoint = ReloadCheckpoint(recovered.Result);
            var continued = await fixture.RunAsync(deliveredCheckpoint,
                deliveredCheckpoint.ConversationMemory.Where(message => !message.IsSteering).ToArray(),
                "Continue the delegated summary after another restart.", delegated: true);
            Assert.True(continued.SessionResumed);
            Assert.Equal(0, continued.DeliveredSteeringCount);
            Assert.Equal(0, continued.UndeliveredSteeringCount);
            var continuedInput = Assert.Single(continued.ProviderInputs);
            Assert.Single(continuedInput, message => message.Text == pendingSteering);

            var continuedCheckpoint = ReloadCheckpoint(continued.Result);
            // Exercise the Runtime's semantic-memory rebuild boundary directly. The
            // subagent coordinator separately rejects incompatible model changes.
            fixture.UseDifferentModel();
            var replanned = await fixture.RunAsync(continuedCheckpoint,
                continuedCheckpoint.ConversationMemory.Where(message => !message.IsSteering).ToArray(),
                "Continue the delegated summary using the selected model.", delegated: true);
            Assert.False(replanned.SessionResumed);
            var replannedInput = Assert.Single(replanned.ProviderInputs);
            // Rebuilding can combine adjacent user turns; count the full instruction
            // across all user text so duplication within a combined message also fails.
            var replannedSteeringCopies = replannedInput.Where(message => message.Role == ChatRole.User)
                .Sum(message => message.Text.Split(pendingSteering, StringSplitOptions.None).Length - 1);
            output.WriteLine("Recovered steering: delivered events={0}; semantic memory={1}; session memory={2}; resumed provider copies={3}; replanned provider copies={4}",
                deliveredCheckpoint.TaskEventJournal.Events.Count(item => item.Type == CopilotAgentTaskEventType.SteeringDelivered && item.SubjectId == steeringSubjectId),
                deliveredCheckpoint.ConversationMemory.Count(message => message.IsSteering && message.Content == pendingSteering),
                deliveredCheckpoint.SessionConversationMemory?.Count(message => message.IsSteering && message.Content == pendingSteering),
                continuedInput.Count(message => message.Text == pendingSteering),
                replannedSteeringCopies);

            Assert.Single(deliveredCheckpoint.TaskEventJournal.Events,
                item => item.Type == CopilotAgentTaskEventType.SteeringDelivered && item.SubjectId == steeringSubjectId);
            Assert.Single(deliveredCheckpoint.TaskEventJournal.Events,
                item => item.Type == CopilotAgentTaskEventType.SteeringQueued && item.SubjectId == steeringSubjectId);
            Assert.Single(deliveredCheckpoint.ConversationMemory,
                message => message.IsSteering && message.Content == pendingSteering);
            Assert.NotNull(deliveredCheckpoint.SessionConversationMemory);
            Assert.Single(deliveredCheckpoint.SessionConversationMemory,
                message => message.IsSteering && message.Content == pendingSteering);
            Assert.Single(continuedCheckpoint.TaskEventJournal.Events,
                item => item.Type == CopilotAgentTaskEventType.SteeringDelivered && item.SubjectId == steeringSubjectId);
            Assert.Single(continuedCheckpoint.TaskEventJournal.Events,
                item => item.Type == CopilotAgentTaskEventType.SteeringQueued && item.SubjectId == steeringSubjectId);
            Assert.Equal(1, replannedSteeringCopies);
        }
        else
        {
            Assert.DoesNotContain(input, message => message.Text.Contains(pendingSteering, StringComparison.Ordinal));
        }

        static JObject EncodeMessage(ChatRole role, string text, string id) => JObject.Parse(
            System.Text.Json.JsonSerializer.Serialize(new ChatMessage(role, text) { MessageId = id }, AIJsonUtilities.DefaultOptions));

        static CopilotAgentSessionCheckpoint ReloadCheckpoint(CopilotAgentRunResult result)
        {
            var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
            return Assert.IsType<CopilotAgentSessionCheckpoint>(
                JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(JsonConvert.SerializeObject(checkpoint)));
        }
    }

    private sealed record ProviderInput(ChatRole Role, string Text, string? MessageId);

    private sealed record RunSnapshot(
        CopilotAgentRunResult Result,
        string InitialCheckpointJson,
        string? PendingSteeringCheckpointJson,
        string[][] ProviderRequests,
        ProviderInput[][] ProviderInputs,
        bool SessionResumed,
        int DeliveredSteeringCount,
        int UndeliveredSteeringCount,
        CopilotSteeringMessageSnapshot[] RecoveredSteeringMessages)
    {
        public string[] ProviderUserMessages => ProviderRequests.LastOrDefault() ?? [];
    }

    private sealed class RuntimeFixture : IDisposable
    {
        private readonly string _directory = Directory.CreateTempSubdirectory("CopilotInitialCheckpointTests-").FullName;
        private readonly string _conversationId = Guid.NewGuid().ToString("N");
        private readonly CopilotCapabilityCatalog _catalog = new();
        private readonly UnusedProbeTool _tool = new();
        private readonly CopilotProfileConfig _profile = new()
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };

        public RuntimeFixture()
        {
            _catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn,
                "initial-checkpoint-tests", "Initial checkpoint tests", [_tool]);
            StateStore = new CopilotChatStateStore(_directory);
        }

        public CopilotChatStateStore StateStore { get; }

        public void UseDifferentModel() => _profile.Model = "replacement-test-model";

        public async Task<RunSnapshot> RunAsync(
            CopilotAgentSessionCheckpoint? checkpoint,
            IReadOnlyList<CopilotRequestMessage> history,
            string userText,
            string ending = "complete",
            string? steering = null,
            bool steerDuringFirstRequest = false,
            bool delegated = false)
        {
            var provider = new RecordingChatClient(ending == "provider_failure");
            using var cancellation = new CancellationTokenSource();
            var runControl = new CopilotAgentRunControl();
            var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([_tool]), new CopilotAgentContextBuilder(), new CopilotToolExecutor(),
                _ => provider, new EmptyExternalToolProvider(), _catalog, new CopilotAgentSkillUsageStore(_directory));
            string? initialCheckpointJson = null;
            string? pendingSteeringCheckpointJson = null;
            var steeringMetrics = new CopilotSubagentSteeringMetrics();
            var recoveredSteeringMessages = new List<CopilotSteeringMessageSnapshot>();
            var request = new CopilotAgentRequest
            {
                ConversationId = _conversationId,
                TaskId = CopilotAgentTaskEventIds.CreateRunId(),
                WorkspacePath = _directory,
                Profile = _profile,
                UserText = userText,
                TaskIntentText = "Summarize the supplied test notes.",
                History = history,
                SessionCheckpoint = checkpoint,
                RunControl = runControl,
                Mode = CopilotAgentMode.Auto,
                HarnessFeatures = CopilotAgentHarnessFeatures.None,
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 1,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(30),
                },
            };
            if (delegated)
            {
                request.RuntimeExecutionScope = CopilotExecutionScope.ForAgentRequest(
                    request, CopilotAgentTaskEventIds.CreateRunId()).DeriveChild(request.TaskId);
            }
            if (steerDuringFirstRequest)
            {
                Assert.NotNull(steering);
                provider.FirstRequestStarted = () =>
                    Assert.True(runtime.EnqueueSteeringMessage(request.TaskId, steering).IsAccepted);
            }
            var result = await runtime.RunAsync(request, item =>
            {
                steeringMetrics.Observe(item);
                if (item.Type == CopilotAgentEventType.SteeringRecovery)
                    recoveredSteeringMessages.AddRange(item.SteeringMessages);
                if (initialCheckpointJson == null
                    && item is { Type: CopilotAgentEventType.CheckpointUpdated, SessionCheckpoint: not null })
                {
                    Assert.Empty(provider.Requests);
                    initialCheckpointJson = JsonConvert.SerializeObject(item.SessionCheckpoint);
                }
                if (pendingSteeringCheckpointJson == null && steerDuringFirstRequest && steering != null
                    && provider.Requests.Count == 1
                    && item is { Type: CopilotAgentEventType.CheckpointUpdated, SessionCheckpoint: not null }
                    && item.SessionCheckpoint.SerializedSessionJson.Contains(steering, StringComparison.Ordinal))
                {
                    Assert.DoesNotContain(provider.Requests[0], text => text.Contains(steering, StringComparison.Ordinal));
                    pendingSteeringCheckpointJson = JsonConvert.SerializeObject(item.SessionCheckpoint);
                }
                if (ending == "pause_before_provider" && item.Type == CopilotAgentEventType.CheckpointReady)
                {
                    Assert.True(runControl.RequestPause());
                    cancellation.Cancel();
                }
                if (steering != null && !steerDuringFirstRequest && item.Type == CopilotAgentEventType.CheckpointReady)
                    Assert.True(runtime.EnqueueSteeringMessage(request.TaskId, steering).IsAccepted);
            }, cancellation.Token).WaitAsync(TimeSpan.FromSeconds(10));

            var expectedStopReason = ending switch
            {
                "provider_failure" => CopilotAgentStopReason.ProviderFailure,
                "pause_before_provider" => CopilotAgentStopReason.Paused,
                _ => CopilotAgentStopReason.Completed,
            };
            Assert.Equal(expectedStopReason, result.StopReason);
            Assert.NotNull(initialCheckpointJson);
            if (ending == "pause_before_provider")
                Assert.Empty(provider.Requests);
            else if (steerDuringFirstRequest)
                Assert.Equal(2, provider.Requests.Count);
            else
                Assert.Single(provider.Requests);
            return new RunSnapshot(result, initialCheckpointJson, pendingSteeringCheckpointJson,
                provider.Requests.ToArray(), provider.Inputs.ToArray(),
                result.TaskEventJournal.Events.Any(item => item.Type == CopilotAgentTaskEventType.SessionResumed
                    && item.RunId == request.TaskId), steeringMetrics.DeliveredCount,
                steeringMetrics.UndeliveredCount, recoveredSteeringMessages.ToArray());
        }

        public void Dispose()
        {
            var resolved = Path.GetFullPath(_directory);
            Assert.Equal(Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())),
                Path.GetDirectoryName(resolved), ignoreCase: true);
            Assert.StartsWith("CopilotInitialCheckpointTests-", Path.GetFileName(resolved), StringComparison.Ordinal);
            Directory.Delete(resolved, recursive: true);
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

    private sealed class UnusedProbeTool : ICopilotAgentDrivenTool
    {
        public string Name => "ReadCheckpointProbe";
        public string Description => "A synthetic read-only capability that this test must not invoke.";
        public bool CanHandle(CopilotAgentRequest request) => true;
        public bool IsAvailable(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput input,
            CancellationToken cancellationToken) => throw new InvalidOperationException("No tool execution is expected.");
    }

    private sealed class RecordingChatClient(bool rejectRequest) : IChatClient
    {
        public const string Answer = "The supplied notes are recorded.";
        public List<string[]> Requests { get; } = [];
        public List<ProviderInput[]> Inputs { get; } = [];
        public Action? FirstRequestStarted { get; set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default) => throw new InvalidOperationException("No final-answer repair is expected.");

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var input = messages.SelectMany(message => message.Contents.OfType<TextContent>()
                .Select(content => new ProviderInput(message.Role, content.Text, message.MessageId))).ToArray();
            Inputs.Add(input);
            Requests.Add(input.Where(message => message.Role == ChatRole.User).Select(message => message.Text).ToArray());
            if (Requests.Count == 1)
                FirstRequestStarted?.Invoke();
            await Task.CompletedTask;
            if (rejectRequest)
            {
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial summary before the provider disconnects.");
                throw new IOException("Synthetic provider disconnect after visible response progress.");
            }
            yield return new ChatResponseUpdate(ChatRole.Assistant, Answer) { FinishReason = ChatFinishReason.Stop };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
