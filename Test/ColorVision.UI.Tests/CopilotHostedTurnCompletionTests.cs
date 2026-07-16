using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotHostedTurnCompletionTests
{
    [Fact]
    public void CompleteSuccessfullyFinalizesMessageAndStoresUsage()
    {
        var conversation = CreateConversation();
        var message = CreateRunningMessage();
        CopilotAssistantMessagePresenter.AppendExecutionTrace(message, "Runtime diagnostic.");

        CopilotHostedTurnCompletion.CompleteSuccessfully(conversation, message, new CopilotTokenUsage(10, 4, 14));

        Assert.Equal(new CopilotTokenUsage(10, 4, 14), conversation.LastUsage);
        Assert.False(message.IsThinkingInProgress);
        Assert.Equal("No final answer was received; only execution trace or reasoning content is available.", message.Content);
    }

    [Theory]
    [InlineData(CopilotAgentControlIntent.Cancel, CopilotAgentStopReason.Cancelled, false, "cancelled")]
    [InlineData(CopilotAgentControlIntent.Pause, CopilotAgentStopReason.Paused, true, "暂停")]
    [InlineData(CopilotAgentControlIntent.None, CopilotAgentStopReason.None, true, "cancelled")]
    public void CompleteCancellationAppliesControlSpecificCheckpointAndStopReason(
        CopilotAgentControlIntent controlIntent,
        CopilotAgentStopReason expectedStopReason,
        bool preservesCheckpoint,
        string expectedFallback)
    {
        var checkpoint = new CopilotAgentSessionCheckpoint();
        var conversation = CreateConversation();
        conversation.AgentSessionCheckpoint = checkpoint;
        conversation.SetLastUsage(new CopilotTokenUsage(5, 2, 7));
        var message = CreateRunningMessage();
        message.BeginResponseTimeline();

        CopilotHostedTurnCompletion.CompleteCancellation(conversation, message, controlIntent);

        Assert.Equal(expectedStopReason, message.AgentStopReason);
        if (preservesCheckpoint)
            Assert.Same(checkpoint, conversation.AgentSessionCheckpoint);
        else
            Assert.Null(conversation.AgentSessionCheckpoint);
        Assert.Contains(expectedFallback, message.Content, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(CopilotTokenUsage.Empty, conversation.LastUsage);
        Assert.False(message.IsThinkingInProgress);
        Assert.Equal(message.Content, Assert.Single(message.VisibleResponseTimelineItems).Markdown);
    }

    [Fact]
    public void CompleteFailurePreservesPartialAnswerAndClearsUsage()
    {
        var conversation = CreateConversation();
        conversation.SetLastUsage(new CopilotTokenUsage(5, 2, 7));
        var message = CreateRunningMessage();
        message.Content = "Partial answer already received.";

        CopilotHostedTurnCompletion.CompleteFailure(conversation, message, "Provider disconnected.");

        Assert.Equal("Partial answer already received.", message.Content);
        Assert.Equal(CopilotTokenUsage.Empty, conversation.LastUsage);
        Assert.False(message.IsThinkingInProgress);
    }

    [Fact]
    public void CompleteFailureAddsFallbackToResponseTimeline()
    {
        var conversation = CreateConversation();
        var message = CreateRunningMessage();
        message.BeginResponseTimeline();

        CopilotHostedTurnCompletion.CompleteFailure(conversation, message, "  Provider disconnected.  ");

        Assert.Equal("Request failed: Provider disconnected.", message.Content);
        Assert.Equal(message.Content, Assert.Single(message.VisibleResponseTimelineItems).Markdown);
    }

    [Fact]
    public void CompleteFailureSanitizesPersistedProviderError()
    {
        const string apiKey = "unlabelled-provider-secret";
        var conversation = CreateConversation();
        var message = CreateRunningMessage();
        message.BeginResponseTimeline();
        var providerError = $"api_key=labelled-secret\r\nEchoed credential: {apiKey} " + new string('x', 1_000);

        CopilotHostedTurnCompletion.CompleteFailure(conversation, message, providerError, apiKey);

        Assert.Contains("api_key=<redacted>", message.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("labelled-secret", message.Content, StringComparison.Ordinal);
        Assert.DoesNotContain(apiKey, message.Content, StringComparison.Ordinal);
        Assert.DoesNotContain('\r', message.Content);
        Assert.DoesNotContain('\n', message.Content);
        Assert.True(message.Content.Length <= "Request failed: ".Length + CopilotUserFacingErrorFormatter.MaximumMessageLength);
        Assert.Equal(message.Content, Assert.Single(message.VisibleResponseTimelineItems).Markdown);
    }

    [Fact]
    public void CompleteQueuedCancellationMarksCancelledWithoutChangingConversationUsage()
    {
        var message = CreateRunningMessage();

        CopilotHostedTurnCompletion.CompleteQueuedCancellation(message);

        Assert.Equal(CopilotAgentStopReason.Cancelled, message.AgentStopReason);
        Assert.False(message.IsThinkingInProgress);
        Assert.Contains("排队", message.Content, StringComparison.Ordinal);
    }

    private static CopilotConversationRecord CreateConversation() =>
        CopilotConversationRecord.CreateEmpty("profile", "Test Model");

    private static CopilotChatMessage CreateRunningMessage()
    {
        var message = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            IsExecutionInProgress = true,
            IsReasoningInProgress = true,
        };
        message.MarkThinkingStarted();
        return message;
    }
}
