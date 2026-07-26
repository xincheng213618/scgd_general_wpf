using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotProviderRetryChatClientTests
{
    [Fact]
    public async Task NonStreamingRetryReportsTheScheduledBackoff()
    {
        using var provider = new TransientThenSuccessChatClient(HttpStatusCode.ServiceUnavailable);
        var retries = new List<CopilotProviderRetryInfo>();
        var delays = new List<TimeSpan>();
        using var client = new CopilotProviderRetryChatClient(
            provider,
            retries.Add,
            maximumAttempts: 3,
            _ => TimeSpan.FromMilliseconds(750),
            (delay, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                delays.Add(delay);
                return Task.CompletedTask;
            });

        var response = await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Retry this transient request.")]);

        Assert.Equal("Recovered.", response.Text);
        Assert.Equal(2, provider.CallCount);
        var retry = Assert.Single(retries);
        Assert.Equal(1, retry.FailedAttempt);
        Assert.Equal(2, retry.NextAttempt);
        Assert.Equal(3, retry.MaximumAttempts);
        Assert.Equal(503, retry.StatusCode);
        Assert.Equal(TimeSpan.FromMilliseconds(750), retry.Delay);
        Assert.Equal([TimeSpan.FromMilliseconds(750)], delays);
    }

    [Fact]
    public async Task StreamingFailureAfterFirstUpdateIsNotRetried()
    {
        using var provider = new PartialThenFailedStreamingChatClient();
        var retries = new List<CopilotProviderRetryInfo>();
        using var client = new CopilotProviderRetryChatClient(
            provider,
            retries.Add,
            maximumAttempts: 3,
            _ => TimeSpan.Zero,
            (_, _) => Task.CompletedTask);

        await Assert.ThrowsAsync<HttpRequestException>(
            () => DrainAsync(client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Do not replay partial output.")])));

        Assert.Equal(1, provider.CallCount);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task AgentRuntimeReturnsRateLimitRetryMetrics()
    {
        using var provider = new TransientThenSuccessChatClient(HttpStatusCode.TooManyRequests);
        var runtime = new CopilotMicrosoftAgentFrameworkRuntime(
            new CopilotToolRegistry([]),
            new CopilotAgentContextBuilder(),
            new CopilotToolExecutor(),
            _ => provider,
            EmptyExternalToolProvider.Instance,
            new CopilotCapabilityCatalog());
        var events = new List<CopilotAgentEvent>();

        var result = await runtime.RunAsync(
            new CopilotAgentRequest
            {
                UserText = "Answer this request.",
                Profile = new CopilotProfileConfig
                {
                    ProviderType = CopilotProviderType.OpenAICompatible,
                    BaseUrl = "https://example.com/v1",
                    ApiKey = "test",
                    Model = "test-model",
                    MaxTokens = 4_096,
                },
                Mode = CopilotAgentMode.Code,
            },
            events.Add,
            CancellationToken.None);

        Assert.Equal(CopilotAgentStopReason.Completed, result.StopReason);
        Assert.Equal(2, result.Budget.ProviderCalls);
        Assert.Equal(1, result.Budget.ProviderRetryCount);
        Assert.Equal(1, result.Budget.ProviderRateLimitRetryCount);
        Assert.Equal(250, result.Budget.ProviderRetryDelayMs);
        Assert.Equal(1, result.Budget.ProviderResponseCount);
        Assert.True(
            result.Budget.ProviderCallDurationTotalMs
            >= result.Budget.ProviderFirstResponseLatencyTotalMs);
        Assert.Contains(events, agentEvent =>
            agentEvent.Type == CopilotAgentEventType.RuntimeDiagnostic
            && agentEvent.Text.Contains("Provider request retry 2/3", StringComparison.Ordinal));
    }

    private static async Task DrainAsync(IAsyncEnumerable<ChatResponseUpdate> updates)
    {
        await foreach (var _ in updates)
        {
        }
    }

    private sealed class TransientThenSuccessChatClient(HttpStatusCode statusCode) : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _callCount) == 1)
                throw CreateTransientException(statusCode);

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "Recovered."))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (Interlocked.Increment(ref _callCount) == 1)
                throw CreateTransientException(statusCode);

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
    }

    private sealed class PartialThenFailedStreamingChatClient : IChatClient
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial.");
            await Task.CompletedTask;
            throw CreateTransientException(HttpStatusCode.ServiceUnavailable);
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

    private static HttpRequestException CreateTransientException(HttpStatusCode statusCode)
    {
        return new HttpRequestException(
            $"HTTP {(int)statusCode}",
            inner: null,
            statusCode);
    }
}
