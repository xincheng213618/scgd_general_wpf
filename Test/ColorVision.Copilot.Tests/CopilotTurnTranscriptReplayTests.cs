using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnTranscriptReplayTests
{
    [Fact]
    public void AgentRequestFreezesSubmittedModelContextCollections()
    {
        var history = new List<CopilotRequestMessage>
        {
            new("user", "original history"),
        };
        var contextItems = new List<CopilotContextItem>
        {
            new() { Id = "context", Content = "original context" },
        };
        var projectInstructions = new List<CopilotProjectInstructionDocument>
        {
            new() { Path = @"C:\project\AGENTS.md", Content = "original instructions" },
        };
        var request = new CopilotAgentRequest
        {
            History = history,
            ContextItems = contextItems,
            ProjectInstructions = projectInstructions,
        };

        history.Clear();
        contextItems.Clear();
        projectInstructions.Clear();

        Assert.Equal("original history", Assert.Single(request.History).Content);
        Assert.Equal("original context", Assert.Single(request.ContextItems).Content);
        Assert.Equal("original instructions", Assert.Single(request.ProjectInstructions).Content);
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CopilotRequestMessage>)request.History).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CopilotContextItem>)request.ContextItems).Clear());
        Assert.Throws<NotSupportedException>(() =>
            ((IList<CopilotProjectInstructionDocument>)request.ProjectInstructions).Clear());
    }

    [Fact]
    public void TurnRequestFreezesTheSubmittedProfile()
    {
        var submittedProfile = new CopilotProfileConfig
        {
            Id = "submitted-profile",
            Model = "submitted-model",
            BaseUrl = "https://submitted.example/v1",
        };
        var request = new CopilotTurnRequest(
            submittedProfile,
            CopilotAgentMode.Auto,
            userText: "inspect",
            existingRequestContent: string.Empty,
            chatAttachmentContextCaptured: false,
            refreshExternalContext: true,
            new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: null),
            CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
            sessionCheckpoint: null,
            recovery: null,
            runControl: null,
            new CopilotAgentDefaultsConfig(),
            externalMcpServers: null,
            conversationId: "profile-snapshot-conversation",
            taskId: "profile-snapshot-turn");

        submittedProfile.Model = "mutated-model";
        submittedProfile.BaseUrl = "https://mutated.example/v1";

        Assert.NotSame(submittedProfile, request.Profile);
        Assert.Equal("submitted-model", request.Profile.Model);
        Assert.Equal("https://submitted.example/v1", request.Profile.BaseUrl);
    }

    [Fact]
    public void TurnRequestReusesDetachedJournalBaselineWithoutARecoveryCheckpoint()
    {
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Cancelled);
        var baseline = journal.Snapshot();

        var request = new CopilotTurnRequest(
            new CopilotProfileConfig(),
            CopilotAgentMode.Auto,
            userText: "continue",
            existingRequestContent: string.Empty,
            chatAttachmentContextCaptured: false,
            refreshExternalContext: true,
            new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: null),
            CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
            sessionCheckpoint: null,
            recovery: null,
            runControl: null,
            new CopilotAgentDefaultsConfig(),
            externalMcpServers: null,
            conversationId: "journal-baseline-conversation",
            taskId: "journal-baseline-turn",
            taskEventJournalBaseline: baseline);

        Assert.Null(request.SessionCheckpoint);
        Assert.Same(baseline, request.TaskEventJournalBaseline);
        Assert.True(CopilotAgentTaskEventJournal.AreEquivalent(
            baseline,
            request.TaskEventJournalBaseline));
    }

    [Fact]
    public void TurnRequestFreezesRecoveryCheckpointAndSharesItsJournalBaseline()
    {
        var sourceToolNames = new[] { "ReadWorkspace" };
        var checkpoint = new CopilotAgentSessionCheckpoint
        {
            ProfileKey = "persisted-profile",
            SerializedSessionJson = "{}",
            ToolSurfaceVersion = CopilotAgentSessionCheckpoint.CurrentToolSurfaceVersion,
            AvailableToolNames = sourceToolNames,
            TaskEventJournal = new CopilotAgentTaskEventJournalSnapshot(),
        };

        var request = new CopilotTurnRequest(
            new CopilotProfileConfig(),
            CopilotAgentMode.Auto,
            userText: "continue",
            existingRequestContent: string.Empty,
            chatAttachmentContextCaptured: false,
            refreshExternalContext: true,
            new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: null),
            CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
            sessionCheckpoint: checkpoint,
            recovery: null,
            runControl: null,
            new CopilotAgentDefaultsConfig(),
            externalMcpServers: null,
            conversationId: "checkpoint-snapshot-conversation",
            taskId: "checkpoint-snapshot-turn");

        Assert.NotNull(request.SessionCheckpoint);
        Assert.NotSame(checkpoint, request.SessionCheckpoint);
        Assert.Same(
            request.SessionCheckpoint.TaskEventJournal,
            request.TaskEventJournalBaseline);
        sourceToolNames[0] = "RewriteWorkspace";
        Assert.Equal("ReadWorkspace", Assert.Single(request.SessionCheckpoint.AvailableToolNames));
        var persistedToolNames = Assert.IsAssignableFrom<IList<string>>(
            request.SessionCheckpoint.AvailableToolNames);
        Assert.Throws<NotSupportedException>(() => persistedToolNames[0] = "RewriteWorkspace");
    }

    [Theory]
    [InlineData("baseline")]
    [InlineData("cached-page")]
    [InlineData("refresh-page")]
    public async Task CapturedChatRuntimeTranscriptReplaysToTheSameCompletion(string scenario)
    {
        const string PageUrl = "https://192.0.2.1/captured-page";
        const string CachedPageBody = "Original cached webpage evidence.";
        const string CachedRequestContent = "test prompt\nSaved webpage observation: " + CachedPageBody;
        var hasPage = scenario != "baseline";
        var attachment = hasPage ? CopilotAttachmentItem.CreateWebPage(PageUrl, "Captured page", CachedPageBody) : null;
        if (hasPage)
        {
            // The literal documentation address is rejected before DNS or HTTP connection.
            Assert.False(CopilotWebPageToolSupport.IsPotentiallyPublicWebPageUri(new Uri(PageUrl)));
        }
        using var handler = new StaticChatHandler();
        using var httpClient = new HttpClient(handler);
        var runtime = new CopilotTurnRuntime(new CopilotChatService(httpClient));
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
        profile.UseSystemPromptOverride("Answer the test request.");
        var request = new CopilotTurnRequest(
            profile,
            CopilotAgentMode.Chat,
            "test prompt",
            existingRequestContent: hasPage ? CachedRequestContent : string.Empty,
            chatAttachmentContextCaptured: hasPage,
            refreshExternalContext: scenario != "cached-page",
            new CopilotAgentHostContextSnapshot(
                activeDocumentPath: null,
                solutionDirectoryPath: null,
                attachments: hasPage ? [attachment!] : null,
                liveContext: null,
                conversationHistory: null,
                additionalReadRootPaths: null,
                globalInstructionRootPath: null),
            CopilotConversationHistoryWindow.ResolveLimits(32_000, 4_096),
            sessionCheckpoint: null,
            recovery: null,
            runControl: null,
            new CopilotAgentDefaultsConfig(),
            externalMcpServers: null,
            conversationId: "transcript-replay-conversation",
            taskId: "transcript-replay-turn");
        var transcript = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in runtime.RunAsync(request, CancellationToken.None))
        {
            transcript.Add(turnEvent);
            if (turnEvent is CopilotTurnStatePersistenceBarrierEvent barrier)
                Assert.True(barrier.TryCommit());
        }

        var protocol = new CopilotTurnEventProtocol(request.Mode, request.TaskId);
        foreach (var turnEvent in transcript)
            protocol.Observe(turnEvent);
        var replayed = protocol.RequireCompletion();
        var emitted = Assert.IsType<CopilotTurnCompletedEvent>(transcript[^1]).Result;

        Assert.Same(emitted, replayed);
        var prepared = Assert.Single(transcript.OfType<CopilotTurnRequestPreparedEvent>()).Request;
        Assert.Equal(prepared.Content, replayed.PreparedUserMessageContent);
        Assert.Equal(hasPage, prepared.ChatAttachmentContextCaptured);
        Assert.Equal(hasPage, replayed.ChatAttachmentContextCaptured);
        var outbound = JObject.Parse(Assert.Single(handler.Payloads));
        var outboundMessages = Assert.IsType<JArray>(outbound["messages"]);
        Assert.Equal(prepared.Content, outboundMessages.Last!["content"]!.Value<string>());
        Assert.Equal(
            ["started", "state-persistence-barrier", "request-prepared", "chat-delta", "completed"],
            transcript.Where(turnEvent => turnEvent is not CopilotTurnRuntimeDiagnosticEvent)
                .Select(GetStableEventKind));
        if (scenario == "refresh-page")
        {
            Assert.Contains("[Web Page Fetch Failed] " + PageUrl, prepared.Content, StringComparison.Ordinal);
            Assert.DoesNotContain(CachedPageBody, prepared.Content, StringComparison.Ordinal);
        }
        else
        {
            Assert.Equal(hasPage ? CachedRequestContent : "test prompt", prepared.Content);
        }
    }

    [Fact]
    public async Task WebAttachmentRefreshUsesFetchedEvidenceWithoutChangingStoredSnapshots()
    {
        const string PageUrl = "https://public.test/captured-page";
        const string CachedBody = "Original cached webpage evidence.";
        const string UpdatedBody = "Current webpage evidence from the requested refresh.";
        var page = CopilotAttachmentItem.CreateWebPage(PageUrl, "Saved page", CachedBody);
        var context = CopilotAttachmentItem.CreateContext("Static measurement evidence.", title: "Saved context", source: "measurement");
        var loaderCalls = 0;
        var builder = new CopilotConversationRequestBuilder((url, token) =>
        {
            token.ThrowIfCancellationRequested();
            Assert.Equal(PageUrl, url);
            loaderCalls++;
            return Task.FromResult(new CopilotFetchedWebPageContent(url, "Current page", string.Empty, UpdatedBody));
        });

        var cached = await builder.BuildRequestAttachmentContextBlockAsync([page, context], false, CancellationToken.None);
        Assert.Equal(0, loaderCalls);
        Assert.Contains(CachedBody, cached, StringComparison.Ordinal);
        var refreshed = await builder.BuildRequestAttachmentContextBlockAsync([page, context], true, CancellationToken.None);
        Assert.Equal(1, loaderCalls);
        Assert.Contains(UpdatedBody, refreshed, StringComparison.Ordinal);
        Assert.Contains("Current page", refreshed, StringComparison.Ordinal);
        Assert.DoesNotContain(CachedBody, refreshed, StringComparison.Ordinal);
        Assert.Contains(context.Value, refreshed, StringComparison.Ordinal);
        Assert.Equal((PageUrl, "Saved page", CachedBody), (page.Source, page.Title, page.Value));
        Assert.Equal("Static measurement evidence.", context.Value);
    }

    private static string GetStableEventKind(CopilotTurnEvent turnEvent) => turnEvent switch
    {
        CopilotTurnStartedEvent => "started",
        CopilotTurnStatePersistenceBarrierEvent => "state-persistence-barrier",
        CopilotTurnRequestPreparedEvent => "request-prepared",
        CopilotTurnChatDeltaEvent => "chat-delta",
        CopilotTurnTokenUsageUpdatedEvent => "token-usage",
        CopilotTurnCompletedEvent => "completed",
        _ => turnEvent.GetType().Name,
    };

    private sealed class StaticChatHandler : HttpMessageHandler
    {
        public List<string> Payloads { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            Payloads.Add(await request.Content!.ReadAsStringAsync(cancellationToken));
            const string Json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"captured answer\"},\"finish_reason\":\"stop\"}]}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json"),
            };
        }
    }
}
