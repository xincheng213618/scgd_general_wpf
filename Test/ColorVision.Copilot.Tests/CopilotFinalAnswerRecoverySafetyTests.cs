using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.IO;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotFinalAnswerRecoverySafetyTests
{
    [Theory]
    [InlineData(false, "", "stop")]
    [InlineData(false, "The operation's outcome is still unknown.", "length")]
    [InlineData(true, "", "stop")]
    [InlineData(true, "The operation's outcome is still unknown.", "length")]
    public async Task RepeatedIncompleteFinalizationDoesNotMakeTheOldExecutableCheckpointResumable(bool unresolvedProviderCall, string text, string finishReason)
    {
        using var fixture = new RecoveryFixture(text, finishReason, unresolvedProviderCall);

        var result = await fixture.RunAsync();

        fixture.AssertNoToolsWereUsed(result);
        Assert.Equal(CopilotAgentStopReason.IncompleteOutput, result.StopReason);
        var retained = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
        result = await fixture.RunAsync(retained, result.StopReason);
        fixture.AssertNoToolsWereUsed(result, expectedProviderCalls: 2);
        Assert.Equal(CopilotAgentStopReason.IncompleteOutput, result.StopReason);
        retained = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
        Assert.Equal(fixture.Checkpoint.SerializedSessionJson, retained.SerializedSessionJson);
        fixture.AssertOriginalUnsafeEvidenceRemains(retained.TaskEventJournal);
        Assert.Equal(3, retained.TaskEventJournal.Events.Count(item => item.Type == CopilotAgentTaskEventType.RunStarted));

        // The no-tools run did not replace or settle the executable session that
        // contains the unresolved call. Updating its journal cannot make it resumable,
        // including after saving and reopening the conversation.
        var reopened = Assert.IsType<CopilotAgentSessionCheckpoint>(JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(JsonConvert.SerializeObject(retained)));
        var compatibility = reopened.EvaluateFor(fixture.Profile, fixture.Capabilities);
        Assert.False(compatibility.CanResume);
        Assert.True(compatibility.RequiresReplan);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task SuccessfulFinalizationRetiresTheUnsafeExecutableCheckpointWithoutCallingTools(bool unresolvedProviderCall)
    {
        using var fixture = new RecoveryFixture("The operation was interrupted; its outcome is not verified.", "stop", unresolvedProviderCall);

        var result = await fixture.RunAsync();

        fixture.AssertNoToolsWereUsed(result);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Null(result.SessionCheckpoint);
        fixture.AssertOriginalUnsafeEvidenceRemains(result.TaskEventJournal);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CopyingOnlyTheJournalPreservesTheSessionRestrictionAcrossSnapshotsAndStorage(bool unresolvedProviderCall)
    {
        using var fixture = new RecoveryFixture("", "stop", unresolvedProviderCall);
        var nextJournal = CreateSettledJournal(fixture.Checkpoint.TaskEventJournal);
        var expectedRestriction = unresolvedProviderCall
            ? CopilotAgentSessionResumeRestriction.UnresolvedProviderToolCall
            : CopilotAgentSessionResumeRestriction.UncertainToolOutcome;
        CopilotAgentSessionCheckpoint?[] copies =
        [
            fixture.Checkpoint.CopyWithTaskEventJournal(nextJournal),
            fixture.Checkpoint.CopyWithTaskEventJournalForNormalization(nextJournal),
            fixture.Checkpoint.CopyWithOutcome(nextJournal, [new CopilotRequestMessage("assistant", "Updated summary only.")]),
        ];

        foreach (var candidate in copies)
        {
            var copy = Assert.IsType<CopilotAgentSessionCheckpoint>(candidate);
            Assert.Equal(expectedRestriction, copy.SessionResumeRestriction);
            Assert.Equal(fixture.Checkpoint.SerializedSessionJson, copy.SerializedSessionJson);
            var reopened = Assert.IsType<CopilotAgentSessionCheckpoint>(JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(JsonConvert.SerializeObject(copy)));
            Assert.True(CopilotAgentSessionCheckpoint.TryCreateSnapshot(reopened, out var snapshot));
            Assert.NotSame(reopened, snapshot);
            Assert.Equal(expectedRestriction, snapshot.SessionResumeRestriction);
            Assert.True(CopilotAgentSessionCheckpoint.AreEquivalent(copy, snapshot));

            // The restriction belongs to the session, even when bounded journal
            // retention no longer contains the original uncertain run.
            var trimmed = Assert.IsType<CopilotAgentSessionCheckpoint>(snapshot.CopyWithTaskEventJournal(CreateSettledJournal()));
            Assert.Equal(expectedRestriction, trimmed.SessionResumeRestriction);
            Assert.False(trimmed.EvaluateFor(fixture.Profile, fixture.Capabilities).CanResume);

            var withoutRestriction = ChangePersistedRestriction(copy, 0);
            Assert.True(withoutRestriction.IsStructurallyValid());
            Assert.False(CopilotAgentSessionCheckpoint.AreEquivalent(copy, withoutRestriction));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CreatingAFreshExecutableSessionDoesNotInheritHistoricalRestrictions(bool unresolvedProviderCall)
    {
        using var fixture = new RecoveryFixture("", "stop", unresolvedProviderCall);
        var settledJournal = CreateSettledJournal(fixture.Checkpoint.TaskEventJournal);
        var retainedOldSession = Assert.IsType<CopilotAgentSessionCheckpoint>(fixture.Checkpoint.CopyWithTaskEventJournal(settledJournal));
        Assert.False(retainedOldSession.EvaluateFor(fixture.Profile, fixture.Capabilities).CanResume);

        var newSession = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
            fixture.Profile,
            "{\"source\":\"new-session-after-replan\"}",
            fixture.Capabilities,
            taskEventJournal: retainedOldSession.TaskEventJournal));

        Assert.Equal(CopilotAgentSessionResumeRestriction.None, newSession.SessionResumeRestriction);
        Assert.True(newSession.EvaluateFor(fixture.Profile, fixture.Capabilities).CanResume);
        fixture.AssertOriginalUnsafeEvidenceRemains(newSession.TaskEventJournal);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void UndefinedPersistedSessionRestrictionsAreRejected(int restriction)
    {
        using var fixture = new RecoveryFixture("", "stop", unresolvedProviderCall: false);
        var invalid = ChangePersistedRestriction(fixture.Checkpoint, restriction);

        Assert.False(invalid.IsStructurallyValid());
        Assert.False(CopilotAgentSessionCheckpoint.TryCreateSnapshot(invalid, out _));
        Assert.Null(invalid.CopyWithTaskEventJournal(CreateSettledJournal()));
        Assert.Equal(CopilotAgentCheckpointCompatibilityKind.Invalid, invalid.EvaluateFor(fixture.Profile, fixture.Capabilities).Kind);
    }

    private static CopilotAgentTaskEventJournalSnapshot CreateSettledJournal(CopilotAgentTaskEventJournalSnapshot? previous = null)
    {
        var journal = new CopilotAgentTaskEventJournalBuilder(previous);
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Completed);
        return journal.Snapshot();
    }

    private static CopilotAgentSessionCheckpoint ChangePersistedRestriction(CopilotAgentSessionCheckpoint checkpoint, int restriction)
    {
        using var reader = new JsonTextReader(new StringReader(JsonConvert.SerializeObject(checkpoint)))
        {
            // Event IDs include the original DateTimeOffset representation.
            // A JObject date conversion must not replace its offset with local time.
            DateParseHandling = DateParseHandling.None,
        };
        var persisted = JObject.Load(reader);
        persisted[nameof(CopilotAgentSessionCheckpoint.SessionResumeRestriction)] = restriction;
        return Assert.IsType<CopilotAgentSessionCheckpoint>(JsonConvert.DeserializeObject<CopilotAgentSessionCheckpoint>(persisted.ToString()));
    }

    private sealed class RecoveryFixture : IDisposable
    {
        private readonly DirectoryInfo _directory = Directory.CreateTempSubdirectory("copilot-finalize-safety-");
        private readonly FinalAnswerClient _client;
        private readonly DiscoveryProbe _externalProvider = new();
        private readonly WriteProbe _tool = new();
        private readonly CopilotMicrosoftAgentFrameworkRuntime _runtime;
        private readonly CopilotAgentRecoveryRequest _recovery;
        private readonly bool _unresolvedProviderCall;
        private readonly List<CopilotAgentEvent> _events = [];

        public RecoveryFixture(string text, string finishReason, bool unresolvedProviderCall)
        {
            _unresolvedProviderCall = unresolvedProviderCall;
            _client = new FinalAnswerClient(text, finishReason);
            var catalog = new CopilotCapabilityCatalog();
            catalog.PublishSource(CopilotCapabilitySourceKind.BuiltIn, "finalize-safety", "Finalize safety", [_tool]);
            Capabilities = catalog.GetSnapshot();
            var journal = new CopilotAgentTaskEventJournalBuilder();
            journal.RecordRunStarted();
            if (unresolvedProviderCall)
            {
                journal.RecordProviderToolHistory(CopilotProviderToolHistoryDelta.Capture(
                    requestMessages: null,
                    responseMessages:
                    [
                        new ChatMessage(ChatRole.Assistant,
                            [new FunctionCallContent("unresolved-provider-call", _tool.Name, new Dictionary<string, object?>())]),
                    ]));
            }
            else
            {
                journal.Observe(CopilotAgentEvent.ToolStarted(CreateExecution(CopilotToolExecutionState.Running)));
                journal.Observe(CopilotAgentEvent.FromToolResult(new CopilotToolResult
                {
                    ToolName = _tool.Name,
                    Success = false,
                    Summary = "The write ended without a confirmed result.",
                    FailureKind = CopilotToolFailureKind.OutcomeUnknown,
                    FailureCode = CopilotToolFailureCode.OutcomeUnknown,
                }, CreateExecution(CopilotToolExecutionState.Interrupted)));
            }
            journal.RecordStop(CopilotAgentStopReason.Interrupted);
            Checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(CopilotAgentSessionCheckpoint.Create(
                Profile,
                "{\"source\":\"executable-session-before-unknown-write\"}",
                Capabilities,
                taskEventJournal: journal.Snapshot()));
            Assert.Equal(unresolvedProviderCall
                    ? CopilotAgentCheckpointCompatibilityKind.UnresolvedProviderToolCall
                    : CopilotAgentCheckpointCompatibilityKind.UncertainToolOutcome,
                Checkpoint.EvaluateFor(Profile, Capabilities).Kind);

            // Exercise the same policy decision that offers the message-card action.
            var decision = CopilotAgentRecoveryPolicy.Evaluate(new CopilotChatMessage(CopilotChatRole.Assistant, "Interrupted")
            {
                AgentStopReason = CopilotAgentStopReason.Interrupted,
            }, Checkpoint, Profile, Capabilities);
            _recovery = Assert.IsType<CopilotAgentRecoveryRequest>(decision.Request);
            Assert.Equal(CopilotAgentRecoveryMode.Finalize, _recovery.Mode);
            _runtime = new CopilotMicrosoftAgentFrameworkRuntime(
                new CopilotToolRegistry([_tool]),
                new CopilotAgentContextBuilder(),
                new CopilotToolExecutor(),
                _ => _client,
                _externalProvider,
                catalog,
                new CopilotAgentSkillUsageStore(_directory.FullName));
        }

        public CopilotProfileConfig Profile { get; } = new()
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            VendorType = CopilotVendorType.Custom,
            ApiKey = "finalize-test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 1_024,
        };

        public CopilotCapabilityCatalogSnapshot Capabilities { get; }
        public CopilotAgentSessionCheckpoint Checkpoint { get; }

        public async Task<CopilotAgentRunResult> RunAsync(CopilotAgentSessionCheckpoint? checkpoint = null, CopilotAgentStopReason previousStopReason = CopilotAgentStopReason.Interrupted)
        {
            using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            return await _runtime.RunAsync(new CopilotAgentRequest
            {
                Profile = Profile,
                ConversationId = "finalize-safety-conversation",
                TaskId = "finalize-safety-task",
                WorkspacePath = _directory.FullName,
                UserText = CopilotAgentRecoveryPolicy.FinalizeUserMessage,
                Mode = CopilotAgentMode.Auto,
                SessionCheckpoint = checkpoint ?? Checkpoint,
                Recovery = checkpoint == null ? _recovery : new CopilotAgentRecoveryRequest
                {
                    Mode = CopilotAgentRecoveryMode.Finalize,
                    PreviousStopReason = previousStopReason,
                },
                RunBudgetOverride = new CopilotAgentRunBudgetOverride
                {
                    RequestTokenBudget = 32_768,
                    MaxToolCalls = 1,
                    MaxAgentPasses = 1,
                    TotalDuration = TimeSpan.FromSeconds(10),
                },
            }, _events.Add, cancellation.Token);
        }

        public void AssertNoToolsWereUsed(CopilotAgentRunResult result, int expectedProviderCalls = 1)
        {
            Assert.Equal(expectedProviderCalls, _client.Calls);
            Assert.Equal(0, _client.StreamingCalls);
            Assert.NotNull(_client.Options);
            Assert.Empty(_client.Options.Tools!);
            Assert.Equal(0, _externalProvider.Calls);
            Assert.Equal(0, _tool.Calls);
            Assert.Empty(result.StepRecords);
            Assert.Equal(0, result.Budget.ToolCalls);
            Assert.DoesNotContain(_events, item => item.Type is CopilotAgentEventType.ToolStarted or CopilotAgentEventType.ToolResult);
        }

        public void AssertOriginalUnsafeEvidenceRemains(CopilotAgentTaskEventJournalSnapshot journal)
        {
            Assert.Contains(journal.Events, item => _unresolvedProviderCall
                ? item.Type == CopilotAgentTaskEventType.ProviderToolCallPersisted
                : item.Type == CopilotAgentTaskEventType.ToolCompleted && item.FailureCode == CopilotToolFailureCode.OutcomeUnknown);
        }

        private CopilotToolExecutionInfo CreateExecution(CopilotToolExecutionState state) => new()
        {
            CallId = "unknown-write-before-finalize",
            Round = 1,
            RuntimeName = "FinalizeSafetyTest",
            ToolName = _tool.Name,
            Access = CopilotToolAccess.Write,
            Idempotency = CopilotToolIdempotency.NonIdempotent,
            State = state,
            StartedAtUtc = DateTimeOffset.UtcNow,
        };

        public void Dispose()
        {
            _client.Dispose();
            var resolved = Path.GetFullPath(_directory.FullName);
            if (!string.Equals(Path.GetDirectoryName(resolved), Path.TrimEndingDirectorySeparator(Path.GetFullPath(Path.GetTempPath())), StringComparison.OrdinalIgnoreCase)
                || !Path.GetFileName(resolved).StartsWith("copilot-finalize-safety-", StringComparison.Ordinal))
                throw new InvalidOperationException("The recovery fixture directory is outside the expected temporary root.");
            Directory.Delete(resolved, recursive: true);
        }
    }

    private sealed class FinalAnswerClient(string text, string finishReason) : IChatClient
    {
        public int Calls { get; private set; }
        public int StreamingCalls { get; private set; }
        public ChatOptions? Options { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Calls++;
            Options = options;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, text))
            {
                FinishReason = new ChatFinishReason(finishReason),
            });
        }

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            StreamingCalls++;
            throw new InvalidOperationException("Finalization must bypass the streaming Harness.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class DiscoveryProbe : ICopilotExternalToolProvider
    {
        public int Calls { get; private set; }

        public Task<CopilotExternalToolLease> DiscoverAsync(CopilotAgentRequest request, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Finalization must not discover external tools.");
        }
    }

    private sealed class WriteProbe : ICopilotFrameworkApprovedTool
    {
        public string Name => "UnknownWriteProbe";
        public string Description => "Represents a write whose previous outcome is unknown.";
        public CopilotToolCapabilityDescriptor Capability => CopilotToolCapabilityDescriptor.ProtectedWrite(CopilotToolIdempotency.NonIdempotent);
        public int Calls { get; private set; }
        public bool CanHandle(CopilotAgentRequest request) => true;
        public Task<CopilotToolResult> ExecuteAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken) =>
            ExecuteApprovedAsync(request, toolInput, cancellationToken);

        public Task<CopilotToolResult> ExecuteApprovedAsync(CopilotAgentRequest request, CopilotAgentToolInput toolInput, CancellationToken cancellationToken)
        {
            Calls++;
            throw new InvalidOperationException("Finalization must not replay a write.");
        }
    }
}
