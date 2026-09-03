using ColorVision.Copilot;
using System;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotCodexReasoningVisibilityTests
{
    [Fact]
    public void HiddenReasoningIsFilteredOnlyAfterTheTurnProtocolBoundary()
    {
        var mixedChatDelta = new CopilotTurnChatDeltaEvent(
            new CopilotStreamDelta("private reasoning", "public answer"));
        var reasoningOnlyChatDelta = new CopilotTurnChatDeltaEvent(
            new CopilotStreamDelta("private reasoning", string.Empty));
        var agentReasoning = new CopilotTurnAgentEvent(
            CopilotAgentEvent.ReasoningDelta("private reasoning"));
        var agentAnswer = new CopilotTurnAgentEvent(
            CopilotAgentEvent.AnswerDelta("public answer"));

        var filteredChat = Assert.IsType<CopilotTurnChatDeltaEvent>(
            CopilotReasoningVisibility.FilterForPresentation(mixedChatDelta, hideAgentReasoning: true));
        Assert.Empty(filteredChat.Delta.ReasoningContent);
        Assert.Equal("public answer", filteredChat.Delta.Content);
        Assert.Null(CopilotReasoningVisibility.FilterForPresentation(
            reasoningOnlyChatDelta,
            hideAgentReasoning: true));
        Assert.Null(CopilotReasoningVisibility.FilterForPresentation(
            agentReasoning,
            hideAgentReasoning: true));
        Assert.Same(
            agentAnswer,
            CopilotReasoningVisibility.FilterForPresentation(agentAnswer, hideAgentReasoning: true));
        Assert.Same(
            agentReasoning,
            CopilotReasoningVisibility.FilterForPresentation(agentReasoning, hideAgentReasoning: false));
    }
}
