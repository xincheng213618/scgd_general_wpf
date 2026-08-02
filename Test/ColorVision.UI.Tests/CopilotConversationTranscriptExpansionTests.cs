using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotConversationTranscriptExpansionTests
{
    [Fact]
    public void TranscriptCommandAdvertisesStrictArgumentsAndAgentRunAvailability()
    {
        var invocation = CopilotLocalCommandCatalog.Parse("/transcript expand");

        Assert.NotNull(invocation);
        Assert.Equal(CopilotLocalCommandKind.Transcript, invocation.Command.Kind);
        Assert.True(invocation.Command.AvailableWhileAgentRuns);
        Assert.Equal(["expand", "collapse"], invocation.Command.Arguments!.Select(item => item.Value));
    }

    [Fact]
    public void EmptyArgumentExpandsCollapsedTracesThenCollapsesWhenAllAreExpanded()
    {
        var conversation = CreateConversationWithTraces();

        var expanded = CopilotConversationTranscriptExpansion.Execute(conversation, string.Empty);
        var collapsed = CopilotConversationTranscriptExpansion.Execute(conversation, string.Empty);

        Assert.Equal(2, expanded.EligibleMessageCount);
        Assert.Equal(2, expanded.ChangedMessageCount);
        Assert.True(expanded.IsExpanded);
        Assert.Equal(2, collapsed.ChangedMessageCount);
        Assert.False(collapsed.IsExpanded);
        Assert.All(
            conversation.Messages.Where(message => message.HasThinkingTrace),
            message => Assert.False(message.IsThinkingExpanded));
    }

    [Fact]
    public void ExplicitActionOnlyChangesMessagesThatAlreadyExposeTraceContent()
    {
        var conversation = CreateConversationWithTraces();
        var ordinaryAnswer = conversation.Messages.Single(message => message.Content == "ordinary");
        ordinaryAnswer.IsThinkingExpanded = true;

        var result = CopilotConversationTranscriptExpansion.Execute(conversation, "expand");

        Assert.Equal(2, result.EligibleMessageCount);
        Assert.Equal(2, result.ChangedMessageCount);
        Assert.True(result.IsExpanded);
        Assert.True(ordinaryAnswer.IsThinkingExpanded);
        Assert.Contains("不读取隐藏请求或附件正文", result.Report);
    }

    [Fact]
    public void InvalidArgumentDoesNotMutateExpansionState()
    {
        var conversation = CreateConversationWithTraces();

        var result = CopilotConversationTranscriptExpansion.Execute(conversation, "all");

        Assert.Null(result.IsExpanded);
        Assert.Equal(0, result.ChangedMessageCount);
        Assert.Contains(CopilotConversationTranscriptExpansion.Usage, result.Report);
        Assert.All(conversation.Messages, message => Assert.False(message.IsThinkingExpanded));
    }

    private static CopilotConversationRecord CreateConversationWithTraces()
    {
        var conversation = new CopilotConversationRecord();
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.User, "request"));
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "reasoned")
        {
            ReasoningContent = "visible reasoning",
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "executed")
        {
            ExecutionContent = "visible execution summary",
        });
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, "ordinary"));
        return conversation;
    }
}
