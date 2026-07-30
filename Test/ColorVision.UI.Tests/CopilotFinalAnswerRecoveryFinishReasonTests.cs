using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotFinalAnswerRecoveryFinishReasonTests
{
    [Fact]
    public async Task LengthLimitedRecoveryRetainsPartialAnswerAndCheckpoint()
    {
        await AssertIncompleteRecoveryAsync(
            ChatFinishReason.Length,
            "provider_output_length",
            "最终回答再次达到模型输出上限",
            "最终回答达到输出上限，已保留部分内容");
    }

    [Fact]
    public async Task ContentFilteredRecoveryRetainsAllowedAnswerAndCheckpoint()
    {
        await AssertIncompleteRecoveryAsync(
            ChatFinishReason.ContentFilter,
            "provider_content_filtered",
            "最终回答被提供商内容策略提前停止",
            "最终回答被内容策略提前停止");
    }

    [Fact]
    public async Task NaturallyCompletedRecoveryRetiresCheckpoint()
    {
        var fixture = CreateFixture(ChatFinishReason.Stop);

        var result = await fixture.Runtime.RunAsync(
            fixture.Request,
            fixture.Events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Null(result.SessionCheckpoint);
        Assert.Empty(result.Blockers);
        Assert.Equal("Recovered final answer.", JoinAnswer(fixture.Events));
        Assert.Contains(fixture.Events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains("old executable session checkpoint was retired", StringComparison.Ordinal));
    }

    [Fact]
    public void ProviderFinishReasonClassificationSeparatesPartialOutcomes()
    {
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsLengthLimitedOutput(ChatFinishReason.Length));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsLengthLimitedOutput(ChatFinishReason.ContentFilter));
        Assert.True(CopilotMicrosoftAgentFrameworkRuntime.IsContentFilteredOutput(ChatFinishReason.ContentFilter));
        Assert.False(CopilotMicrosoftAgentFrameworkRuntime.IsContentFilteredOutput(ChatFinishReason.Stop));
    }

    private static async Task AssertIncompleteRecoveryAsync(
        ChatFinishReason finishReason,
        string expectedBlockerCode,
        string expectedNotice,
        string expectedDetail)
    {
        var fixture = CreateFixture(finishReason);

        var result = await fixture.Runtime.RunAsync(
            fixture.Request,
            fixture.Events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.IncompleteOutput, result.StopReason);
        var checkpoint = Assert.IsType<CopilotAgentSessionCheckpoint>(result.SessionCheckpoint);
        Assert.True(checkpoint.IsStructurallyValid());
        var blocker = Assert.Single(result.Blockers);
        Assert.Equal(CopilotAgentBlockerKind.ProviderOutput, blocker.Kind);
        Assert.Equal(expectedBlockerCode, blocker.Code);
        Assert.Contains("Recovered final answer.", JoinAnswer(fixture.Events), StringComparison.Ordinal);
        Assert.Contains(expectedNotice, JoinAnswer(fixture.Events), StringComparison.Ordinal);

        var conversation = CopilotConversationRecord.CreateEmpty("profile", "Profile");
        conversation.AgentSessionCheckpoint = checkpoint;
        conversation.Messages.Add(new CopilotChatMessage(CopilotChatRole.Assistant, JoinAnswer(fixture.Events))
        {
            AgentStopReason = result.StopReason,
            AgentBlockers = result.Blockers,
            AgentTaskLedger = result.TaskLedger,
        });
        var task = Assert.Single(CopilotAgentTaskIndex.Build([conversation]));
        Assert.Equal(expectedDetail, task.DetailLabel);
        Assert.True(task.CanResume);
        Assert.Equal("重试最终回答", task.RecoveryActionLabel);
    }

    private static RecoveryFixture CreateFixture(ChatFinishReason finishReason)
    {
        var profile = new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
        var catalog = CopilotCapabilityCatalog.Shared;
        var journal = new CopilotAgentTaskEventJournalBuilder();
        journal.RecordRunStarted();
        journal.RecordStop(CopilotAgentStopReason.Completed);
        var checkpoint = CopilotAgentSessionCheckpoint.Create(
            profile,
            "{}",
            catalog.GetSnapshot(),
            taskEventJournal: journal.Snapshot(),
            conversationMemory:
            [
                new CopilotRequestMessage("user", "Complete the work."),
                new CopilotRequestMessage("assistant", "Previous final answer was truncated."),
            ]);
        Assert.NotNull(checkpoint);

        var provider = new FinishReasonChatClient(finishReason);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            catalog);
        var request = new CopilotAgentRequest
        {
            ConversationId = "conversation",
            TaskId = "task",
            UserText = CopilotAgentRecoveryPolicy.FinalizeUserMessage,
            Profile = profile,
            Mode = CopilotAgentMode.Auto,
            SessionCheckpoint = checkpoint,
            Recovery = new CopilotAgentRecoveryRequest
            {
                Mode = CopilotAgentRecoveryMode.Finalize,
                PreviousStopReason = CopilotAgentStopReason.Completed,
                PreviousResponseWasInterrupted = true,
            },
        };
        return new RecoveryFixture(runtime, request, []);
    }

    private static string JoinAnswer(IEnumerable<CopilotAgentEvent> events)
    {
        return string.Concat(events
            .Where(agentEvent => agentEvent.Type == CopilotAgentEventType.AnswerDelta)
            .Select(agentEvent => agentEvent.Text));
    }

    private sealed record RecoveryFixture(
        CopilotMicrosoftAgentFrameworkRuntime Runtime,
        CopilotAgentRequest Request,
        List<CopilotAgentEvent> Events);

    private sealed class FinishReasonChatClient(ChatFinishReason finishReason) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "Recovered final answer."))
            {
                FinishReason = finishReason,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class EmptyExternalToolProvider : ICopilotExternalToolProvider
    {
        public static EmptyExternalToolProvider Instance { get; } = new();

        public Task<CopilotExternalToolLease> DiscoverAsync(
            CopilotAgentRequest request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new CopilotExternalToolLease());
    }
}
