using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTerminalHistoryTests
{
    [Theory]
    [InlineData(CopilotAgentStopReason.AwaitingUser)]
    [InlineData(CopilotAgentStopReason.ApprovalDenied)]
    [InlineData(CopilotAgentStopReason.BudgetExhausted)]
    [InlineData(CopilotAgentStopReason.TaskPassLimit)]
    [InlineData(CopilotAgentStopReason.Blocked)]
    [InlineData(CopilotAgentStopReason.Paused)]
    [InlineData(CopilotAgentStopReason.Cancelled)]
    [InlineData(CopilotAgentStopReason.IncompleteOutput)]
    [InlineData(CopilotAgentStopReason.ProviderFailure)]
    [InlineData(CopilotAgentStopReason.Interrupted)]
    public void NonCompletedAgentOutcomeIsVisibleToTheNextModelTurn(
        CopilotAgentStopReason stopReason)
    {
        var assistant = new CopilotChatMessage(
            CopilotChatRole.Assistant,
            "Completed one step; remaining work was not verified.")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = stopReason,
        };

        var modelContent = assistant.ModelContent;

        Assert.False(assistant.WasResponseInterrupted);
        Assert.Contains("<agent_turn_incomplete", modelContent, StringComparison.Ordinal);
        Assert.Contains($"stop_reason=\"{stopReason}\"", modelContent, StringComparison.Ordinal);
        Assert.Contains("remaining tasks", modelContent, StringComparison.Ordinal);
        Assert.DoesNotContain(
            CopilotChatMessage.ResponseInterruptionModelMarker,
            modelContent,
            StringComparison.Ordinal);
    }

    [Fact]
    public void CompletedAgentOutcomeDoesNotAddAnIncompleteMarker()
    {
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "All work verified.")
        {
            RequestMode = CopilotAgentMode.Code,
            AgentStopReason = CopilotAgentStopReason.Completed,
        };

        Assert.Equal("All work verified.", assistant.ModelContent);
        Assert.DoesNotContain("agent_turn_incomplete", assistant.ModelContent, StringComparison.Ordinal);
    }

    [Fact]
    public void TruncatedCompletedAgentAnswerUsesTheResponseInterruptionBoundary()
    {
        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, "Visible prefix")
        {
            RequestMode = CopilotAgentMode.Auto,
            AgentStopReason = CopilotAgentStopReason.Completed,
            IsResponseContentTruncated = true,
        };
        conversation.Messages.Add(assistant);

        CopilotHostedTurnCompletion.CompleteTerminalTurn(
            conversation,
            assistant,
            CopilotTokenUsage.Empty);

        Assert.True(assistant.WasResponseInterrupted);
        Assert.Contains(CopilotChatMessage.ResponseInterruptionModelMarker, assistant.ModelContent, StringComparison.Ordinal);
        Assert.DoesNotContain("agent_turn_incomplete", assistant.ModelContent, StringComparison.Ordinal);
    }
}
