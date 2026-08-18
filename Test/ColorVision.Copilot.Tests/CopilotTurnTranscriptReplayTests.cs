using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnTranscriptReplayTests
{
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
    public void TurnRequestPreservesJournalBaselineWithoutARecoveryCheckpoint()
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
    }

    [Fact]
    public async Task CapturedChatRuntimeTranscriptReplaysToTheSameCompletion()
    {
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
            conversationId: "transcript-replay-conversation",
            taskId: "transcript-replay-turn");
        var transcript = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in runtime.RunAsync(request, CancellationToken.None))
            transcript.Add(turnEvent);

        var protocol = new CopilotTurnEventProtocol(request.Mode, request.TaskId);
        foreach (var turnEvent in transcript)
            protocol.Observe(turnEvent);
        var replayed = protocol.RequireCompletion();
        var emitted = Assert.IsType<CopilotTurnCompletedEvent>(transcript[^1]).Result;

        Assert.Same(emitted, replayed);
        Assert.Equal("test prompt", replayed.PreparedUserMessageContent);
        Assert.Equal(
            ["started", "request-prepared", "chat-delta", "completed"],
            transcript.Where(turnEvent => turnEvent is not CopilotTurnRuntimeDiagnosticEvent)
                .Select(GetStableEventKind));
    }

    private static string GetStableEventKind(CopilotTurnEvent turnEvent) => turnEvent switch
    {
        CopilotTurnStartedEvent => "started",
        CopilotTurnRequestPreparedEvent => "request-prepared",
        CopilotTurnChatDeltaEvent => "chat-delta",
        CopilotTurnTokenUsageUpdatedEvent => "token-usage",
        CopilotTurnCompletedEvent => "completed",
        _ => turnEvent.GetType().Name,
    };

    private sealed class StaticChatHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            const string Json = "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"captured answer\"},\"finish_reason\":\"stop\"}]}";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(Json, Encoding.UTF8, "application/json"),
            });
        }
    }
}
