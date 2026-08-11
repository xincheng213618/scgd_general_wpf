using ColorVision.Copilot;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotPreparedHostedTurnTests
{
    [Fact]
    public void ExposesPreparedFactsAndAcceptsMatchingHostedRun()
    {
        var fixture = CreateFixture(CopilotAgentMode.Code);
        var preparedTurn = fixture.PreparedTurn;
        var hostedRun = new CopilotHostedAgentRun(
            fixture.Conversation.Id,
            CopilotAgentMode.Code);

        try
        {
            preparedTurn.ValidateHostedRun(hostedRun);

            Assert.Same(fixture.Conversation, preparedTurn.Conversation);
            Assert.Same(fixture.Profile, preparedTurn.RequestProfile);
            Assert.Same(fixture.UserMessage, preparedTurn.UserMessage);
            Assert.Same(fixture.AssistantMessage, preparedTurn.AssistantMessage);
            Assert.Same(fixture.HostContext, preparedTurn.HostContext);
            Assert.Same(fixture.RuntimeConfig, preparedTurn.RuntimeConfig);
            Assert.Equal(fixture.Conversation.Id, preparedTurn.ConversationId);
            Assert.Equal(CopilotAgentMode.Code, preparedTurn.Mode);
            Assert.False(preparedTurn.RefreshExternalContext);
            Assert.True(preparedTurn.IsAutomaticGoalContinuation);
        }
        finally
        {
            Complete(hostedRun);
        }
    }

    [Fact]
    public void RejectsHostedRunFromAnotherConversation()
    {
        var preparedTurn = CreateFixture(CopilotAgentMode.Auto).PreparedTurn;
        var hostedRun = new CopilotHostedAgentRun("another-conversation", CopilotAgentMode.Auto);

        try
        {
            Assert.Throws<InvalidOperationException>(() => preparedTurn.ValidateHostedRun(hostedRun));
        }
        finally
        {
            Complete(hostedRun);
        }
    }

    [Fact]
    public void RejectsHostedRunWithAnotherMode()
    {
        var fixture = CreateFixture(CopilotAgentMode.Plan);
        var hostedRun = new CopilotHostedAgentRun(fixture.Conversation.Id, CopilotAgentMode.Code);

        try
        {
            Assert.Throws<InvalidOperationException>(() => fixture.PreparedTurn.ValidateHostedRun(hostedRun));
        }
        finally
        {
            Complete(hostedRun);
        }
    }

    [Fact]
    public void RejectsMessagesWithReversedRoles()
    {
        var fixture = CreateFixture(CopilotAgentMode.Auto);

        Assert.Throws<ArgumentException>(() => new CopilotPreparedHostedTurn(
            fixture.Conversation,
            fixture.Profile,
            fixture.AssistantMessage,
            fixture.AssistantMessage,
            fixture.HostContext,
            fixture.RuntimeConfig,
            refreshExternalContext: true,
            isAutomaticGoalContinuation: false));
        Assert.Throws<ArgumentException>(() => new CopilotPreparedHostedTurn(
            fixture.Conversation,
            fixture.Profile,
            fixture.UserMessage,
            fixture.UserMessage,
            fixture.HostContext,
            fixture.RuntimeConfig,
            refreshExternalContext: true,
            isAutomaticGoalContinuation: false));
    }

    private static PreparedTurnFixture CreateFixture(CopilotAgentMode mode)
    {
        var profile = new CopilotProfileConfig
        {
            Id = "prepared-turn-profile",
            Name = "Prepared turn",
        };
        var conversation = CopilotConversationRecord.CreateEmpty(profile.Id, profile.DisplayLabel);
        conversation.Id = "prepared-turn-conversation";
        var userMessage = new CopilotChatMessage(CopilotChatRole.User, "Do the work.")
        {
            RequestMode = mode,
        };
        var assistantMessage = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
        var hostContext = new CopilotAgentHostContextSnapshot(
            string.Empty,
            string.Empty,
            Array.Empty<CopilotAttachmentItem>());
        var runtimeConfig = new CopilotTurnRuntimeConfigSnapshot(
            new CopilotAgentDefaultsConfig(),
            Array.Empty<CopilotMcpClientServerConfig>());
        var preparedTurn = new CopilotPreparedHostedTurn(
            conversation,
            profile,
            userMessage,
            assistantMessage,
            hostContext,
            runtimeConfig,
            refreshExternalContext: false,
            isAutomaticGoalContinuation: true);
        return new PreparedTurnFixture(
            conversation,
            profile,
            userMessage,
            assistantMessage,
            hostContext,
            runtimeConfig,
            preparedTurn);
    }

    private static void Complete(CopilotHostedAgentRun hostedRun)
    {
        hostedRun.TryStart();
        hostedRun.Complete(error: null);
    }

    private sealed record PreparedTurnFixture(
        CopilotConversationRecord Conversation,
        CopilotProfileConfig Profile,
        CopilotChatMessage UserMessage,
        CopilotChatMessage AssistantMessage,
        CopilotAgentHostContextSnapshot HostContext,
        CopilotTurnRuntimeConfigSnapshot RuntimeConfig,
        CopilotPreparedHostedTurn PreparedTurn);
}
