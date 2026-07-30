using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotInterruptedHistoryTests
{
    [Fact]
    public void PartialAssistantResponseGetsAModelVisibleInterruptionBoundary()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Apply the requested change."));
        var assistant = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "I updated the first file, and next I will verify");
        assistant.MarkResponseInterrupted("Provider failure at https://secret.example.test.");
        conversation.Messages.Add(assistant);

        var snapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation);

        Assert.Collection(
            snapshot.ModelMessages,
            message => Assert.Equal("Apply the requested change.", message.Content),
            message =>
            {
                Assert.StartsWith("I updated the first file", message.Content, StringComparison.Ordinal);
                Assert.Contains(CopilotChatMessage.ResponseInterruptionModelMarker, message.Content, StringComparison.Ordinal);
                Assert.Contains("not as a completed answer", message.Content, StringComparison.Ordinal);
                Assert.Contains("Re-check current evidence before continuing", message.Content, StringComparison.Ordinal);
                Assert.DoesNotContain("secret.example.test", message.Content, StringComparison.Ordinal);
            });
        Assert.Collection(
            snapshot.VisibleMessages,
            message => Assert.Equal("Apply the requested change.", message.Content),
            message => Assert.Equal("I updated the first file, and next I will verify", message.Content));
    }

    [Fact]
    public void InterruptedDisplayOnlyRecoveryStillClosesTheModelTurn()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect the failure."));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            IsResponsePending = true,
        };
        conversation.Messages.Add(assistant);

        Assert.True(CopilotInterruptedResponseRecovery.Normalize(conversation, assistant));
        var snapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation);

        Assert.True(assistant.IsContentDisplayOnly);
        Assert.Collection(
            snapshot.ModelMessages,
            message => Assert.Equal("Inspect the failure.", message.Content),
            message => Assert.Equal(CopilotChatMessage.ResponseInterruptionModelMarker, message.Content));
        Assert.Single(snapshot.VisibleMessages);
        Assert.Equal("Inspect the failure.", snapshot.VisibleMessages[0].Content);
        Assert.DoesNotContain(assistant.Content, snapshot.ModelMessages[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void CompletedAssistantAndUserMessagesRemainUnchanged()
    {
        var user = new CopilotChatMessage(CopilotChatRole.User, "Continue.");
        user.MarkResponseInterrupted("invalid user-side flag");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Completed answer.");

        Assert.Equal("Continue.", user.ModelContent);
        Assert.Equal("Completed answer.", assistant.ModelContent);
        Assert.DoesNotContain("assistant_response_interrupted", assistant.ModelContent, StringComparison.Ordinal);
    }

    [Fact]
    public void StartingANewAttemptClearsTheInterruptionBoundary()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Partial answer.");
        assistant.MarkResponseInterrupted("Stopped.");
        Assert.Contains(CopilotChatMessage.ResponseInterruptionModelMarker, assistant.ModelContent, StringComparison.Ordinal);

        assistant.MarkThinkingStarted();

        Assert.False(assistant.WasResponseInterrupted);
        Assert.Equal("Partial answer.", assistant.ModelContent);
    }
}
