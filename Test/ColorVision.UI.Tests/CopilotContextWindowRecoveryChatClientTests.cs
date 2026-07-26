using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotContextWindowRecoveryChatClientTests
{
    [Fact]
    public async Task SeparateModelTurnsCanEachRecoverOnce()
    {
        using var provider = new ContextLimitChatClient(call => call % 2 == 1);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = new CopilotContextWindowRecoveryChatClient(
            provider,
            inputBudgetTokens: 8_000,
            recoveries.Add);
        var messages = CreateLargeConversation();

        await client.GetResponseAsync(messages);
        await client.GetResponseAsync(messages);

        Assert.Equal(4, provider.CallCount);
        Assert.Equal(messages.Length, provider.MessageCounts[0]);
        Assert.True(provider.MessageCounts[1] < messages.Length);
        Assert.Equal(messages.Length, provider.MessageCounts[2]);
        Assert.True(provider.MessageCounts[3] < messages.Length);
        Assert.Equal(2, recoveries.Count);
        Assert.All(recoveries, recovery =>
        {
            Assert.True(recovery.EstimatedInputTokensBefore > recovery.EstimatedInputTokensAfter);
            Assert.True(recovery.CompactedMessageCount < recovery.OriginalMessageCount);
            Assert.Equal("HTTP 400 context limit", recovery.FailureKind);
            Assert.Contains("estimated input", recovery.ToDiagnosticText(), StringComparison.Ordinal);
        });
    }

    [Fact]
    public async Task SeparateStreamingTurnsCanEachRecoverOnce()
    {
        using var provider = new ContextLimitChatClient(call => call % 2 == 1);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = new CopilotContextWindowRecoveryChatClient(
            provider,
            inputBudgetTokens: 8_000,
            recoveries.Add);
        var messages = CreateLargeConversation();

        await DrainAsync(client.GetStreamingResponseAsync(messages));
        await DrainAsync(client.GetStreamingResponseAsync(messages));

        Assert.Equal(4, provider.CallCount);
        Assert.Equal(2, recoveries.Count);
        Assert.True(provider.MessageCounts[1] < provider.MessageCounts[0]);
        Assert.True(provider.MessageCounts[3] < provider.MessageCounts[2]);
    }

    [Fact]
    public async Task AModelTurnDoesNotRetryMoreThanOnce()
    {
        using var provider = new ContextLimitChatClient(_ => true);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = new CopilotContextWindowRecoveryChatClient(
            provider,
            inputBudgetTokens: 8_000,
            recoveries.Add);
        var messages = CreateLargeConversation();

        var first = await Assert.ThrowsAsync<CopilotAgentContextWindowRecoveryExhaustedException>(
            () => client.GetResponseAsync(messages));
        var second = await Assert.ThrowsAsync<CopilotAgentContextWindowRecoveryExhaustedException>(
            () => client.GetResponseAsync(messages));

        Assert.Equal(4, provider.CallCount);
        Assert.Equal(2, recoveries.Count);
        Assert.True(first.EstimatedInputTokensBefore > first.EstimatedInputTokensAfter);
        Assert.True(second.EstimatedInputTokensBefore > second.EstimatedInputTokensAfter);
        Assert.Equal(first.OriginalMessageCount, second.OriginalMessageCount);
        Assert.Equal(first.CompactedMessageCount, second.CompactedMessageCount);
    }

    private static ChatMessage[] CreateLargeConversation()
    {
        var messages = new List<ChatMessage>();
        for (var index = 0; index < 6; index++)
        {
            messages.Add(new ChatMessage(
                ChatRole.User,
                $"Request {index}: {new string('u', 1_200)}"));
            messages.Add(new ChatMessage(
                ChatRole.Assistant,
                $"Answer {index}: {new string('a', 1_200)}"));
        }
        return messages.ToArray();
    }

    private static async Task DrainAsync(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        await foreach (var _ in updates)
        {
        }
    }

    private sealed class ContextLimitChatClient(Func<int, bool> shouldReject) : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public List<int> MessageCounts { get; } = [];

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Record(messages);
            if (shouldReject(call))
                throw CreateContextLimitException();

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "Recovered.")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var call = Record(messages);
            if (shouldReject(call))
                throw CreateContextLimitException();

            yield return new ChatResponseUpdate(ChatRole.Assistant, "Recovered.")
            {
                FinishReason = ChatFinishReason.Stop,
            };
            await Task.CompletedTask;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }

        private int Record(IEnumerable<ChatMessage> messages)
        {
            MessageCounts.Add(messages.Count());
            return Interlocked.Increment(ref _callCount);
        }

        private static HttpRequestException CreateContextLimitException()
        {
            return new HttpRequestException(
                "context_length_exceeded: prompt is too long",
                inner: null,
                HttpStatusCode.BadRequest);
        }
    }
}
