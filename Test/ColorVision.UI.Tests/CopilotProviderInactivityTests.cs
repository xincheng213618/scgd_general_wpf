using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotProviderInactivityTests
{
    private static readonly TimeSpan TestInactivityTimeout =
        TimeSpan.FromMilliseconds(75);

    [Fact]
    public async Task AgentRetriesFirstContentTimeoutAfterMetadataOnlyUpdate()
    {
        using var provider = new MetadataThenStallChatClient();
        var retries = new List<CopilotProviderRetryInfo>();
        using var client = CreateRetryingAgentClient(provider, retries);
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Retry only before real content.")]))
        {
            updates.Add(update);
        }

        Assert.Equal(2, provider.CallCount);
        Assert.Equal("Recovered.", string.Concat(updates.Select(update => update.Text)));
        Assert.DoesNotContain(
            updates.SelectMany(update => update.Contents),
            content => content is UsageContent);
        var retry = Assert.Single(retries);
        Assert.Equal("first-content timeout", retry.FailureKind);
    }

    [Fact]
    public async Task AgentDoesNotRetryStreamingTimeoutAfterContent()
    {
        using var provider = new PartialThenStallChatClient();
        var retries = new List<CopilotProviderRetryInfo>();
        using var client = CreateRetryingAgentClient(provider, retries);
        var updates = new List<ChatResponseUpdate>();

        var exception = await Assert.ThrowsAsync<CopilotProviderInactivityException>(
            () => DrainAsync(
                client.GetStreamingResponseAsync(
                    [new ChatMessage(ChatRole.User, "Do not replay partial content.")]),
                updates));

        Assert.Equal(CopilotProviderInactivityPhase.StreamingUpdate, exception.Phase);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal("Partial.", string.Concat(updates.Select(update => update.Text)));
        Assert.Empty(retries);
    }

    [Fact]
    public async Task AgentPreservesCallerCancellation()
    {
        using var provider = new MetadataThenStallChatClient();
        using var client = new CopilotProviderInactivityChatClient(
            new CopilotCancellationGuardChatClient(provider),
            firstResponseTimeout: TimeSpan.FromSeconds(5),
            streamingUpdateTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(
            TimeSpan.FromMilliseconds(50));

        var exception = await Record.ExceptionAsync(
            () => DrainAsync(
                client.GetStreamingResponseAsync(
                    [new ChatMessage(ChatRole.User, "Cancel this request.")],
                    cancellationToken: cancellation.Token)));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.IsNotType<CopilotProviderInactivityException>(exception);
        Assert.Equal(1, provider.CallCount);
    }

    [Fact]
    public async Task DirectChatRetriesFirstContentTimeout()
    {
        using var handler = new SequentialStreamingHandler(call => call == 1
            ? CreateStreamingResponse(new PrefixThenStallStream([]))
            : CreateStreamingResponse(new MemoryStream(
                Encoding.UTF8.GetBytes(CreateCompletedEventStream("Recovered.")))));
        using var httpClient = new HttpClient(handler);
        var retries = new List<CopilotProviderRetryInfo>();
        var deltas = new List<CopilotStreamDelta>();
        var service = CreateDirectChatService(httpClient);

        await service.StreamReplyAsync(
            CreateProfile(),
            [new CopilotRequestMessage("user", "Retry this stalled request.")],
            deltas.Add,
            retries.Add,
            CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("Recovered.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal(
            "first-content timeout",
            Assert.Single(retries).FailureKind);
    }

    [Fact]
    public async Task DirectChatDoesNotRetryStreamingTimeoutAfterContent()
    {
        var partialEvent = Encoding.UTF8.GetBytes(
            "data: {\"choices\":[{\"delta\":{\"content\":\"Partial.\"}}]}\n\n");
        using var handler = new SequentialStreamingHandler(
            _ => CreateStreamingResponse(
                new PrefixThenStallStream(partialEvent)));
        using var httpClient = new HttpClient(handler);
        var retries = new List<CopilotProviderRetryInfo>();
        var deltas = new List<CopilotStreamDelta>();
        var service = CreateDirectChatService(httpClient);

        var exception = await Assert.ThrowsAsync<CopilotProviderInactivityException>(
            () => service.StreamReplyAsync(
                CreateProfile(),
                [new CopilotRequestMessage("user", "Do not replay partial content.")],
                deltas.Add,
                retries.Add,
                CancellationToken.None));

        Assert.Equal(CopilotProviderInactivityPhase.StreamingUpdate, exception.Phase);
        Assert.Equal(1, handler.CallCount);
        Assert.Equal("Partial.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Empty(retries);
    }

    [Fact]
    public async Task DirectChatDoesNotRetryNonTransientStatusWhenErrorBodyStalls()
    {
        using var handler = new SequentialStreamingHandler(
            _ => new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StreamContent(new PrefixThenStallStream([])),
            });
        using var httpClient = new HttpClient(handler);
        var retries = new List<CopilotProviderRetryInfo>();
        var service = CreateDirectChatService(httpClient);

        var exception = await Assert.ThrowsAsync<CopilotProviderInactivityException>(
            () => service.StreamReplyAsync(
                CreateProfile(),
                [new CopilotRequestMessage("user", "Do not retry authentication failures.")],
                _ => { },
                retries.Add,
                CancellationToken.None));

        Assert.Equal(CopilotProviderInactivityPhase.FirstResponse, exception.Phase);
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(retries);
    }

    private static CopilotProviderRetryChatClient CreateRetryingAgentClient(
        IChatClient provider,
        ICollection<CopilotProviderRetryInfo> retries)
    {
        return new CopilotProviderRetryChatClient(
            new CopilotProviderInactivityChatClient(
                new CopilotCancellationGuardChatClient(provider),
                TestInactivityTimeout,
                TestInactivityTimeout),
            retries.Add,
            maximumAttempts: 2,
            _ => TimeSpan.Zero,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
    }

    private static CopilotChatService CreateDirectChatService(HttpClient httpClient)
    {
        return new CopilotChatService(
            httpClient,
            maximumAttempts: 2,
            retryDelayFactory: _ => TimeSpan.Zero,
            delayAsync: static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            firstResponseTimeout: TestInactivityTimeout,
            streamingUpdateTimeout: TestInactivityTimeout);
    }

    private static CopilotProfileConfig CreateProfile()
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = CopilotProviderType.OpenAICompatible,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }

    private static HttpResponseMessage CreateStreamingResponse(Stream stream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(stream),
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        return response;
    }

    private static string CreateCompletedEventStream(string content)
    {
        return $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{content}\"}},\"finish_reason\":\"stop\"}}]}}\n\n"
            + "data: [DONE]\n\n";
    }

    private static async Task DrainAsync(
        IAsyncEnumerable<ChatResponseUpdate> source,
        List<ChatResponseUpdate>? destination = null)
    {
        await foreach (var update in source)
            destination?.Add(update);
    }

    private sealed class MetadataThenStallChatClient : IChatClient
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
            var call = Interlocked.Increment(ref _callCount);
            if (call == 1)
            {
                yield return new ChatResponseUpdate(
                    ChatRole.Assistant,
                    [new UsageContent(new UsageDetails { TotalTokenCount = 1 })]);
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                yield break;
            }

            yield return new ChatResponseUpdate(ChatRole.Assistant, "Recovered.")
            {
                FinishReason = ChatFinishReason.Stop,
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class PartialThenStallChatClient : IChatClient
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
            Interlocked.Increment(ref _callCount);
            yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial.");
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }

    private sealed class SequentialStreamingHandler(
        Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(
                responseFactory(Interlocked.Increment(ref _callCount)));
        }
    }

    private sealed class PrefixThenStallStream(byte[] prefix) : Stream
    {
        private int _offset;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override void Flush()
        {
        }

        public override int Read(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();

        public override async ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            if (_offset < prefix.Length)
            {
                var count = Math.Min(buffer.Length, prefix.Length - _offset);
                prefix.AsMemory(_offset, count).CopyTo(buffer);
                _offset += count;
                return count;
            }

            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(
                    buffer.AsMemory(offset, count),
                    cancellationToken)
                .AsTask();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
