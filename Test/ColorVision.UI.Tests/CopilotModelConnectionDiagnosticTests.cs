using ColorVision.Copilot;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotModelConnectionDiagnosticTests
{
    private static readonly TimeSpan ShortTimeout = TimeSpan.FromMilliseconds(50);

    [Fact]
    public async Task SuccessfulDiagnosticReportsDisplayableContentAndRetry()
    {
        using var handler = new SequentialHandler(call => call == 1
            ? CreateJsonResponse(HttpStatusCode.ServiceUnavailable, "provider busy")
            : CreateStreamingResponse(CreateCompletedEventStream("OK")));
        using var httpClient = new HttpClient(handler);
        var diagnostic = CreateDiagnostic(httpClient, maximumAttempts: 2);
        var sourceProfile = CreateProfile();
        sourceProfile.UseSystemPromptOverride("Keep this prompt.");
        sourceProfile.Temperature = 0.75;

        var result = await diagnostic.TestAsync(sourceProfile, CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(2, result.DisplayableCharacters);
        Assert.Equal(1, result.RetryCount);
        Assert.Equal(503, result.LatestRetry?.StatusCode);
        Assert.Equal("Keep this prompt.", sourceProfile.EffectiveSystemPrompt);
        Assert.Equal(4_096, sourceProfile.MaxTokens);
        Assert.Equal(0.75, sourceProfile.Temperature);
        Assert.Contains("Received 2 displayable characters.", result.FormatStatus(), StringComparison.Ordinal);
        Assert.Contains("Recovered after 1 retry (last: HTTP 503).", result.FormatStatus(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task AuthenticationFailureIsNotRetriedAndKeepsProviderDetail()
    {
        using var handler = new SequentialHandler(
            _ => CreateJsonResponse(HttpStatusCode.Unauthorized, "invalid api key"));
        using var httpClient = new HttpClient(handler);
        var diagnostic = CreateDiagnostic(httpClient, maximumAttempts: 2);

        var failure = await Assert.ThrowsAsync<CopilotModelConnectionDiagnosticException>(
            () => diagnostic.TestAsync(CreateProfile(), CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(0, failure.RetryCount);
        var status = CopilotSettingsViewModel.FormatModelConnectionDiagnosticFailure(failure);
        Assert.Contains("Connection failed in ", status, StringComparison.Ordinal);
        Assert.Contains("invalid api key", status, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("automatic retry", status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task EndpointFailureReportsExhaustedAutomaticRetry()
    {
        using var handler = new ThrowingHandler("No route to host.");
        using var httpClient = new HttpClient(handler);
        var diagnostic = CreateDiagnostic(httpClient, maximumAttempts: 2);

        var failure = await Assert.ThrowsAsync<CopilotModelConnectionDiagnosticException>(
            () => diagnostic.TestAsync(CreateProfile(), CancellationToken.None));

        Assert.Equal(2, handler.CallCount);
        Assert.Equal(1, failure.RetryCount);
        Assert.Equal("connection failure", failure.LatestRetry?.FailureKind);
        var status = CopilotSettingsViewModel.FormatModelConnectionDiagnosticFailure(failure);
        Assert.Contains("after 1 automatic retry", status, StringComparison.Ordinal);
        Assert.Contains("No route to host.", status, StringComparison.Ordinal);
    }

    [Fact]
    public async Task FirstContentStallReportsTheProviderInactivityPhase()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(new StallingStream()));
        using var httpClient = new HttpClient(handler);
        var diagnostic = CreateDiagnostic(httpClient, maximumAttempts: 1);

        var failure = await Assert.ThrowsAsync<CopilotModelConnectionDiagnosticException>(
            () => diagnostic.TestAsync(CreateProfile(), CancellationToken.None));

        Assert.True(CopilotProviderInactivityException.TryFind(
            failure.InnerException,
            out var inactivity));
        Assert.Equal(CopilotProviderInactivityPhase.FirstResponse, inactivity.Phase);
        Assert.Contains(
            "no displayable content arrived for 50 ms",
            CopilotSettingsViewModel.FormatModelConnectionDiagnosticFailure(failure),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task CallerCancellationIsNotReclassifiedAsConnectionFailure()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(new StallingStream()));
        using var httpClient = new HttpClient(handler);
        var diagnostic = CreateDiagnostic(
            httpClient,
            maximumAttempts: 1,
            firstContentTimeout: TimeSpan.FromSeconds(5));
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        var exception = await Record.ExceptionAsync(
            () => diagnostic.TestAsync(CreateProfile(), cancellation.Token));

        Assert.IsAssignableFrom<OperationCanceledException>(exception);
        Assert.IsNotType<CopilotModelConnectionDiagnosticException>(exception);
    }

    [Fact]
    public async Task EmptyCompletedStreamIsReportedAsCompatibilityFailure()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse("data: [DONE]\n\n"));
        using var httpClient = new HttpClient(handler);
        var diagnostic = CreateDiagnostic(httpClient, maximumAttempts: 1);

        var failure = await Assert.ThrowsAsync<CopilotModelConnectionDiagnosticException>(
            () => diagnostic.TestAsync(CreateProfile(), CancellationToken.None));

        Assert.Contains(
            "no displayable text",
            CopilotSettingsViewModel.FormatModelConnectionDiagnosticFailure(failure),
            StringComparison.OrdinalIgnoreCase);
    }

    private static CopilotModelConnectionDiagnostic CreateDiagnostic(
        HttpClient httpClient,
        int maximumAttempts,
        TimeSpan? firstContentTimeout = null)
    {
        var chatService = new CopilotChatService(
            httpClient,
            maximumAttempts,
            _ => TimeSpan.Zero,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            },
            firstContentTimeout ?? ShortTimeout,
            ShortTimeout);
        return new CopilotModelConnectionDiagnostic(chatService);
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

    private static HttpResponseMessage CreateJsonResponse(
        HttpStatusCode statusCode,
        string message)
    {
        return new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(
                $"{{\"error\":{{\"message\":\"{message}\"}}}}",
                Encoding.UTF8,
                "application/json"),
        };
    }

    private static HttpResponseMessage CreateStreamingResponse(string eventStream) =>
        CreateStreamingResponse(new MemoryStream(Encoding.UTF8.GetBytes(eventStream)));

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

    private sealed class ThrowingHandler(string message) : HttpMessageHandler
    {
        private int _callCount;

        public int CallCount => Volatile.Read(ref _callCount);

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Interlocked.Increment(ref _callCount);
            return Task.FromException<HttpResponseMessage>(
                new HttpRequestException(message));
        }
    }

    private sealed class StallingStream : Stream
    {
        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
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
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return 0;
        }

        public override Task<int> ReadAsync(
            byte[] buffer,
            int offset,
            int count,
            CancellationToken cancellationToken) =>
            ReadAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();

        public override long Seek(long offset, SeekOrigin origin) =>
            throw new NotSupportedException();

        public override void SetLength(long value) =>
            throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) =>
            throw new NotSupportedException();
    }
}
