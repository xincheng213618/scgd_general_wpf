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

    [Fact]
    public void HostedAgentCancellationPreservesPartialTextAndClosesTheModelTurn()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Apply both changes."));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Applied the first change.")
        {
            RequestMode = CopilotAgentMode.Auto,
        };
        conversation.Messages.Add(assistant);

        CopilotHostedTurnCompletion.CompleteCancellation(
            conversation,
            assistant,
            CopilotAgentControlIntent.Cancel);
        var snapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation);

        Assert.True(assistant.WasResponseInterrupted);
        Assert.Equal(CopilotAgentStopReason.Cancelled, assistant.AgentStopReason);
        Assert.Contains("Applied the first change.", snapshot.ModelMessages[1].Content, StringComparison.Ordinal);
        Assert.Contains(CopilotChatMessage.ResponseInterruptionModelMarker, snapshot.ModelMessages[1].Content, StringComparison.Ordinal);
    }

    [Fact]
    public void HostedFailureWithoutTextStillClosesTheModelTurn()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Inspect the workspace."));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        conversation.Messages.Add(assistant);

        CopilotHostedTurnCompletion.CompleteFailure(
            conversation,
            assistant,
            "Connection failed at https://secret.example.test.",
            "https://secret.example.test.");
        var snapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation);

        Assert.True(assistant.WasResponseInterrupted);
        Assert.True(assistant.IsContentDisplayOnly);
        Assert.Equal(CopilotChatMessage.ResponseInterruptionModelMarker, snapshot.ModelMessages[1].Content);
        Assert.DoesNotContain("secret.example.test", snapshot.ModelMessages[1].Content, StringComparison.Ordinal);
        Assert.Single(snapshot.VisibleMessages);
    }

    [Fact]
    public void QueuedCancellationClosesTheUnstartedModelTurn()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "Run this after the current task."));
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty)
        {
            RequestMode = CopilotAgentMode.Code,
        };
        conversation.Messages.Add(assistant);

        CopilotHostedTurnCompletion.CompleteBeforeStartCancellation(assistant);
        var snapshot = CopilotConversationRequestBuilder.CaptureHistorySnapshot(conversation);

        Assert.True(assistant.WasResponseInterrupted);
        Assert.Equal(CopilotAgentStopReason.Cancelled, assistant.AgentStopReason);
        Assert.Equal(CopilotChatMessage.ResponseInterruptionModelMarker, snapshot.ModelMessages[1].Content);
        Assert.Single(snapshot.VisibleMessages);
    }
}
