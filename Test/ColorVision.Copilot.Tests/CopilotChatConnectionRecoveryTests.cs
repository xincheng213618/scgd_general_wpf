using ColorVision.Copilot;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotChatConnectionRecoveryTests
{
    [Fact]
    public async Task ConnectionFailuresDoNotConsumeOrdinaryRetryBudget()
    {
        using var handler = new SequentialHandler(call => call switch
        {
            <= 2 => throw new HttpRequestException("provider connection unavailable"),
            3 => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                Content = new StringContent("temporarily unavailable"),
            },
            _ => CreateCompletedResponse("Recovered."),
        });
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var service = CreateService(httpClient, maximumAttempts: 2, delays);
        var deltas = new List<CopilotStreamDelta>();
        var retries = new List<CopilotProviderRetryInfo>();
        var recoveries = new List<CopilotProviderConnectionRecoveryInfo>();

        var result = await service.StreamReplyAsync(
            CreateProfile(),
            [new CopilotRequestMessage("user", "Wait for the provider connection.")],
            deltas.Add,
            retries.Add,
            recoveries.Add,
            onUsageChanged: null,
            CancellationToken.None);

        Assert.Equal(4, handler.CallCount);
        Assert.Equal("Recovered.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal("stop", result.FinishReason);
        Assert.Collection(
            recoveries,
            recovery =>
            {
                Assert.Equal(1, recovery.RecoveryAttempt);
                Assert.Equal(TimeSpan.FromSeconds(5), recovery.Delay);
            },
            recovery =>
            {
                Assert.Equal(2, recovery.RecoveryAttempt);
                Assert.Equal(TimeSpan.FromSeconds(10), recovery.Delay);
            });
        var retry = Assert.Single(retries);
        Assert.Equal(1, retry.FailedAttempt);
        Assert.Equal(2, retry.NextAttempt);
        Assert.Equal(HttpStatusCode.ServiceUnavailable, (HttpStatusCode)retry.StatusCode!.Value);
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(250)],
            delays);
    }

    [Fact]
    public async Task ConnectionFailureAfterContentIsNotRecoveredOrReplayed()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(new ThrowAfterPayloadStream(
                "data: {\"choices\":[{\"delta\":{\"content\":\"Partial.\"}}]}\n\n")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2, []);
        var deltas = new List<CopilotStreamDelta>();
        var recoveries = new List<CopilotProviderConnectionRecoveryInfo>();

        await Assert.ThrowsAsync<HttpRequestException>(() => service.StreamReplyAsync(
            CreateProfile(),
            [new CopilotRequestMessage("user", "Do not replay partial content.")],
            deltas.Add,
            onRetry: null,
            recoveries.Add,
            onUsageChanged: null,
            CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("Partial.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Empty(recoveries);
    }

    [Fact]
    public async Task RequestsWithoutRecoveryObserverKeepBoundedConnectionRetries()
    {
        using var handler = new SequentialHandler(
            _ => throw new HttpRequestException("provider connection unavailable"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2, []);
        var retries = new List<CopilotProviderRetryInfo>();

        await Assert.ThrowsAsync<HttpRequestException>(() => service.StreamReplyAsync(
            CreateProfile(),
            [new CopilotRequestMessage("user", "Run a bounded connection diagnostic.")],
            _ => { },
            retries.Add,
            CancellationToken.None));

        Assert.Equal(2, handler.CallCount);
        var retry = Assert.Single(retries);
        Assert.Equal(1, retry.FailedAttempt);
        Assert.Equal("connection failure", retry.FailureKind);
    }

    private static CopilotChatService CreateService(
        HttpClient httpClient,
        int maximumAttempts,
        List<TimeSpan> delays) =>
        new(
            httpClient,
            maximumAttempts,
            _ => TimeSpan.FromMilliseconds(250),
            (delay, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                delays.Add(delay);
                return Task.CompletedTask;
            });

    private static CopilotProfileConfig CreateProfile() => new()
    {
        VendorType = CopilotVendorType.Custom,
        ProviderType = CopilotProviderType.OpenAICompatible,
        ApiKey = "test-key",
        BaseUrl = "https://example.test/v1",
        Model = "test-model",
        MaxTokens = 4_096,
    };

    private static HttpResponseMessage CreateCompletedResponse(string content) =>
        CreateStreamingResponse(new MemoryStream(Encoding.UTF8.GetBytes(
            $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{content}\"}},\"finish_reason\":\"stop\"}}]}}\n\n"
            + "data: [DONE]\n\n")));

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

    private sealed class SequentialHandler(
        Func<int, HttpResponseMessage> responseFactory) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return Task.FromResult(responseFactory(Interlocked.Increment(ref _callCount)));
        }
    }

    private sealed class ThrowAfterPayloadStream(string payload) : Stream
    {
        private readonly byte[] _payload = Encoding.UTF8.GetBytes(payload);
        private int _offset;

        public override bool CanRead => true;
        public override bool CanSeek => false;
        public override bool CanWrite => false;
        public override long Length => _payload.Length;
        public override long Position
        {
            get => _offset;
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count)
        {
            if (_offset >= _payload.Length)
                throw new HttpRequestException("connection dropped after content");

            var bytesToCopy = Math.Min(count, _payload.Length - _offset);
            Array.Copy(_payload, _offset, buffer, offset, bytesToCopy);
            _offset += bytesToCopy;
            return bytesToCopy;
        }

        public override ValueTask<int> ReadAsync(
            Memory<byte> buffer,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_offset >= _payload.Length)
            {
                return ValueTask.FromException<int>(
                    new HttpRequestException("connection dropped after content"));
            }

            var bytesToCopy = Math.Min(buffer.Length, _payload.Length - _offset);
            _payload.AsMemory(_offset, bytesToCopy).CopyTo(buffer);
            _offset += bytesToCopy;
            return ValueTask.FromResult(bytesToCopy);
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
