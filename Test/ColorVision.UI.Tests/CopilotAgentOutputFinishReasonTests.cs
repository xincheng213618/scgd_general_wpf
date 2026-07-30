using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentOutputFinishReasonTests
{
    [Fact]
    public async Task ContentFilteredAgentAnswerRemainsIncompleteWithoutAutomaticRetry()
    {
        using var provider = new ScriptedFinishReasonChatClient(
            ChatFinishReason.ContentFilter,
            "Allowed primary partial.",
            repairFinishReason: null,
            repairText: string.Empty);
        var events = new List<CopilotAgentEvent>();

        var result = await CreateRuntime(provider).RunAsync(
            CreateRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(1, provider.StreamingCallCount);
        Assert.Equal(0, provider.NonStreamingCallCount);
        Assert.Equal(CopilotAgentStopReason.IncompleteOutput, result.StopReason);
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Equal("provider_content_filtered", Assert.Single(result.Blockers).Code);
        var answer = ReconstructAnswer(events);
        Assert.Contains("Allowed primary partial.", answer, StringComparison.Ordinal);
        Assert.Contains("内容策略提前停止", answer, StringComparison.Ordinal);
        Assert.Contains(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains("without an automatic retry", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ContentFilteredRepairCannotUpgradeLengthLimitedAnswerToCompleted()
    {
        using var provider = new ScriptedFinishReasonChatClient(
            ChatFinishReason.Length,
            "Primary partial.",
            ChatFinishReason.ContentFilter,
            "Filtered replacement.");
        var events = new List<CopilotAgentEvent>();

        var result = await CreateRuntime(provider).RunAsync(
            CreateRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(1, provider.StreamingCallCount);
        Assert.Equal(1, provider.NonStreamingCallCount);
        Assert.Equal(CopilotAgentStopReason.IncompleteOutput, result.StopReason);
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Equal("provider_content_filtered", Assert.Single(result.Blockers).Code);
        var answer = ReconstructAnswer(events);
        Assert.Contains("Primary partial.", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Filtered replacement.", answer, StringComparison.Ordinal);
        Assert.Contains("内容策略提前停止", answer, StringComparison.Ordinal);
    }

    [Fact]
    public async Task NaturalRepairReplacesLengthLimitedPrimaryAnswer()
    {
        using var provider = new ScriptedFinishReasonChatClient(
            ChatFinishReason.Length,
            "Primary partial.",
            ChatFinishReason.Stop,
            "Recovered complete answer.");
        var events = new List<CopilotAgentEvent>();

        var result = await CreateRuntime(provider).RunAsync(
            CreateRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Empty(result.Blockers);
        Assert.Equal("Recovered complete answer.", ReconstructAnswer(events));
    }

    [Fact]
    public async Task ToolRequestAtTerminalBoundaryUsesBoundedFinalization()
    {
        using var provider = new ScriptedFinishReasonChatClient(
            ChatFinishReason.ToolCalls,
            "Primary text before the unresolved tool request.",
            ChatFinishReason.Stop,
            "Recovered without another tool call.");
        var events = new List<CopilotAgentEvent>();

        var result = await CreateRuntime(provider).RunAsync(
            CreateRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(1, provider.StreamingCallCount);
        Assert.Equal(1, provider.NonStreamingCallCount);
        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Empty(result.Blockers);
        Assert.Equal("Recovered without another tool call.", ReconstructAnswer(events));
        Assert.Contains(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains("explicit non-success finish reason", StringComparison.Ordinal));
    }

    [Fact]
    public async Task UnknownRepairFinishReasonCannotUpgradePartialAnswer()
    {
        using var provider = new ScriptedFinishReasonChatClient(
            ChatFinishReason.Length,
            "Primary partial.",
            new ChatFinishReason("provider_paused"),
            "Unconfirmed replacement.");
        var events = new List<CopilotAgentEvent>();

        var result = await CreateRuntime(provider).RunAsync(
            CreateRequest(),
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.IncompleteOutput, result.StopReason);
        Assert.NotNull(result.SessionCheckpoint);
        Assert.Equal("provider_output_finish_reason", Assert.Single(result.Blockers).Code);
        var answer = ReconstructAnswer(events);
        Assert.Contains("Primary partial.", answer, StringComparison.Ordinal);
        Assert.DoesNotContain("Unconfirmed replacement.", answer, StringComparison.Ordinal);
        Assert.Contains("未确认完成的提供商状态", answer, StringComparison.Ordinal);
    }

    private static CopilotMicrosoftAgentFrameworkRuntime CreateRuntime(IChatClient provider)
    {
        return new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            CopilotCapabilityCatalog.Shared);
    }

    private static CopilotAgentRequest CreateRequest()
    {
        return new CopilotAgentRequest
        {
            ConversationId = "conversation",
            TaskId = "task",
            UserText = "Return a concise final answer.",
            Profile = new CopilotProfileConfig
            {
                VendorType = CopilotVendorType.Custom,
                ProviderType = CopilotProviderType.OpenAICompatible,
                ApiKey = "test-key",
                BaseUrl = "https://example.test/v1",
                Model = "test-model",
                MaxTokens = 4_096,
            },
            Mode = CopilotAgentMode.Code,
        };
    }

    private static string ReconstructAnswer(IEnumerable<CopilotAgentEvent> events)
    {
        var answer = new StringBuilder();
        foreach (var agentEvent in events)
        {
            if (agentEvent.Type == CopilotAgentEventType.AnswerReset)
                answer.Clear();
            else if (agentEvent.Type == CopilotAgentEventType.AnswerDelta)
                answer.Append(agentEvent.Text);
        }
        return answer.ToString();
    }

    private sealed class ScriptedFinishReasonChatClient(
        ChatFinishReason streamingFinishReason,
        string streamingText,
        ChatFinishReason? repairFinishReason,
        string repairText) : IChatClient
    {
        private int _nonStreamingCallCount;
        private int _streamingCallCount;

        public int NonStreamingCallCount => Volatile.Read(ref _nonStreamingCallCount);

        public int StreamingCallCount => Volatile.Read(ref _streamingCallCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _nonStreamingCallCount);
            if (!repairFinishReason.HasValue)
                throw new InvalidOperationException("The content-filtered primary response must not be retried automatically.");

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, repairText))
            {
                FinishReason = repairFinishReason,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _streamingCallCount);
            yield return new ChatResponseUpdate(ChatRole.Assistant, streamingText)
            {
                FinishReason = streamingFinishReason,
            };
            await Task.CompletedTask;
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
