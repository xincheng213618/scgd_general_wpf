using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnEventProtocolTests
{
    [Fact]
    public void AcceptsPreparedChatProgressAndMatchingCompletion()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat);
        var result = CreateChatResult();

        protocol.Observe(new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", true)));
        protocol.Observe(new CopilotTurnChatDeltaEvent(
            new CopilotStreamDelta(string.Empty, "partial")));
        protocol.Observe(new CopilotTurnProviderRetryEvent(
            new CopilotProviderRetryInfo(1, 2, 3, TimeSpan.Zero, "timeout", null)));
        protocol.Observe(new CopilotTurnCompletedEvent(result));

        Assert.Same(result, protocol.RequireCompletion());
    }

    [Fact]
    public void RejectsChatProgressBeforeRequestPreparation()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "partial"))));

        Assert.Contains("before its request was prepared", exception.Message);
    }

    [Fact]
    public void RejectsDuplicateChatRequestPreparation()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat);
        var prepared = new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", false));
        protocol.Observe(prepared);

        var exception = Assert.Throws<InvalidOperationException>(() => protocol.Observe(prepared));

        Assert.Contains("more than once", exception.Message);
    }

    [Fact]
    public void RejectsAgentEventDuringChatTurn()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnAgentEvent(
                CopilotAgentEvent.AnswerDelta("partial"))));

        Assert.Contains("chat turn cannot emit", exception.Message);
    }

    [Fact]
    public void RejectsChatEventDuringAgentTurn()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", false))));

        Assert.Contains("Auto turn cannot emit", exception.Message);
    }

    [Fact]
    public void AcceptsAgentProgressAndMatchingCompletion()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Auto);
        var result = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Auto,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());

        protocol.Observe(new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("partial")));
        protocol.Observe(new CopilotTurnCompletedEvent(result));

        Assert.Same(result, protocol.RequireCompletion());
    }

    [Fact]
    public void RejectsCompletionForDifferentMode()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnCompletedEvent(CreateChatResult())));

        Assert.Contains("but Auto was requested", exception.Message);
    }

    [Fact]
    public void RejectsEventAfterCompletion()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Chat);
        protocol.Observe(new CopilotTurnRequestPreparedEvent(
            new CopilotPreparedTurnRequest("prepared", false)));
        protocol.Observe(new CopilotTurnCompletedEvent(CreateChatResult()));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            protocol.Observe(new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "late"))));

        Assert.Contains("after completion", exception.Message);
    }

    [Fact]
    public void RequiresCompletionBeforeReturningResult()
    {
        var protocol = new CopilotTurnEventProtocol(CopilotAgentMode.Auto);

        var exception = Assert.Throws<InvalidOperationException>(protocol.RequireCompletion);

        Assert.Contains("without a completion event", exception.Message);
    }

    private static CopilotTurnResult CreateChatResult()
    {
        return CopilotTurnResult.FromChat(
            CopilotTokenUsage.Empty,
            "prepared",
            chatAttachmentContextCaptured: false,
            new CopilotChatStreamResult(
                CopilotTokenUsage.Empty,
                CopilotChatFinishKind.Complete,
                "stop"));
    }
}
