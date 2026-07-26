using ColorVision.Copilot;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotProviderPayloadErrorTests
{
    [Fact]
    public async Task AnthropicOverloadBeforeContentIsRetried()
    {
        using var handler = new SequentialHandler(call => call == 1
            ? CreateStreamingResponse(
                "event: error\n"
                + "data: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n")
            : CreateStreamingResponse(CreateCompletedAnthropicStream("Recovered.")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2);
        var deltas = new List<CopilotStreamDelta>();
        var retries = new List<CopilotProviderRetryInfo>();

        await service.StreamReplyAsync(
            CreateProfile(CopilotProviderType.AnthropicCompatible),
            [new CopilotRequestMessage("user", "Retry an overload before output.")],
            deltas.Add,
            retries.Add,
            CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("Recovered.", string.Concat(deltas.Select(delta => delta.Content)));
        var retry = Assert.Single(retries);
        Assert.Equal("overloaded_error", retry.FailureKind);
        Assert.Null(retry.StatusCode);
    }

    [Fact]
    public async Task TransientPayloadErrorAfterContentIsNotRetried()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(
                "data: {\"choices\":[{\"delta\":{\"content\":\"Partial.\"}}]}\n\n"
                + "event: error\n"
                + "data: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2);
        var deltas = new List<CopilotStreamDelta>();
        var retries = new List<CopilotProviderRetryInfo>();

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(
            () => service.StreamReplyAsync(
                CreateProfile(CopilotProviderType.OpenAICompatible),
                [new CopilotRequestMessage("user", "Do not replay partial output.")],
                deltas.Add,
                retries.Add,
                CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("Partial.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal("overloaded_error", failure.ErrorCode);
        Assert.True(failure.IsTransient);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task OpenAiTopLevelErrorIsReportedAndNotRetried()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(
                "event: error\n"
                + "data: {\"type\":\"error\",\"code\":\"invalid_request_error\","
                + "\"message\":\"Unsupported model for test-key\",\"param\":\"model\"}\n\n"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2);
        var retries = new List<CopilotProviderRetryInfo>();

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(
            () => service.StreamReplyAsync(
                CreateProfile(CopilotProviderType.OpenAICompatible),
                [new CopilotRequestMessage("user", "Expose the structured provider error.")],
                _ => { },
                retries.Add,
                CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("invalid_request_error", failure.ErrorCode);
        Assert.False(failure.IsTransient);
        Assert.Contains("Unsupported model", failure.Message, StringComparison.Ordinal);
        Assert.Contains("<redacted>", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", failure.Message, StringComparison.Ordinal);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task OpenAiResponseFailedServerErrorBeforeContentIsRetried()
    {
        using var handler = new SequentialHandler(call => call == 1
            ? CreateStreamingResponse(
                "data: {\"type\":\"response.failed\",\"response\":{\"status\":\"failed\","
                + "\"error\":{\"code\":\"server_error\",\"message\":\"Generation failed.\"}}}\n\n")
            : CreateStreamingResponse(CreateCompletedOpenAiStream("Recovered.")));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2);
        var deltas = new List<CopilotStreamDelta>();
        var retries = new List<CopilotProviderRetryInfo>();

        await service.StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible),
            [new CopilotRequestMessage("user", "Retry a failed response before output.")],
            deltas.Add,
            retries.Add,
            CancellationToken.None);

        Assert.Equal(2, handler.CallCount);
        Assert.Equal("Recovered.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal("server_error", Assert.Single(retries).FailureKind);
    }

    [Fact]
    public async Task SuccessfulJsonErrorPayloadIsNotMisreportedAsEmptyResponse()
    {
        using var handler = new SequentialHandler(
            _ => CreateJsonResponse(
                "{\"error\":{\"type\":\"authentication_error\","
                + "\"message\":\"Credential test-key was rejected.\"}}"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 2);
        var retries = new List<CopilotProviderRetryInfo>();

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(
            () => service.StreamReplyAsync(
                CreateProfile(CopilotProviderType.OpenAICompatible),
                [new CopilotRequestMessage("user", "Report the provider payload error.")],
                _ => { },
                retries.Add,
                CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("authentication_error", failure.ErrorCode);
        Assert.Contains("Credential <redacted> was rejected.", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no displayable text", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(retries);
    }

    private static CopilotChatService CreateService(
        HttpClient httpClient,
        int maximumAttempts)
    {
        return new CopilotChatService(
            httpClient,
            maximumAttempts,
            _ => TimeSpan.Zero,
            static (_, cancellationToken) =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Task.CompletedTask;
            });
    }

    private static CopilotProfileConfig CreateProfile(CopilotProviderType providerType)
    {
        return new CopilotProfileConfig
        {
            VendorType = CopilotVendorType.Custom,
            ProviderType = providerType,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
            MaxTokens = 4_096,
        };
    }

    private static HttpResponseMessage CreateStreamingResponse(string eventStream)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(eventStream))),
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        return response;
    }

    private static HttpResponseMessage CreateJsonResponse(string json)
    {
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
    }

    private static string CreateCompletedOpenAiStream(string content)
    {
        return $"data: {{\"choices\":[{{\"delta\":{{\"content\":\"{content}\"}},\"finish_reason\":\"stop\"}}]}}\n\n"
            + "data: [DONE]\n\n";
    }

    private static string CreateCompletedAnthropicStream(string content)
    {
        return "event: content_block_delta\n"
            + $"data: {{\"type\":\"content_block_delta\",\"index\":0,\"delta\":{{\"type\":\"text_delta\",\"text\":\"{content}\"}}}}\n\n"
            + "event: message_delta\n"
            + "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"}}\n\n"
            + "event: message_stop\n"
            + "data: {\"type\":\"message_stop\"}\n\n";
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
}
