using ColorVision.Copilot;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderPayloadErrorTests
{
    [Theory]
    [InlineData(CopilotProviderType.OpenAICompatible, false)]
    [InlineData(CopilotProviderType.OpenAICompatible, true)]
    [InlineData(CopilotProviderType.AnthropicCompatible, false)]
    [InlineData(CopilotProviderType.AnthropicCompatible, true)]
    public async Task MalformedStreamEventFailsWithoutReplayingOrDiscardingProgress(
        CopilotProviderType providerType,
        bool hasProgress)
    {
        var progress = providerType == CopilotProviderType.OpenAICompatible
            ? "data: {\"choices\":[{\"delta\":{\"content\":\"Partial.\"}}],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":4}}\n\n"
            : "data: {\"type\":\"message_start\",\"message\":{\"usage\":{\"input_tokens\":10,\"output_tokens\":0}}}\n\n"
                + "data: {\"type\":\"content_block_delta\",\"index\":0,\"delta\":{\"type\":\"text_delta\",\"text\":\"Partial.\"}}\n\n"
                + "data: {\"type\":\"message_delta\",\"delta\":{\"stop_reason\":\"end_turn\"},\"usage\":{\"output_tokens\":4}}\n\n";
        var completed = providerType == CopilotProviderType.OpenAICompatible
            ? CreateCompletedOpenAiStream("Later.")
            : CreateCompletedAnthropicStream("Later.");
        using var handler = new SequentialHandler(_ => CreateStreamingResponse(
            (hasProgress ? progress : string.Empty)
            + "data: {\"secret\":\"test-key\",broken}\n\n"
            + completed,
            "req_test-key_malformed"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 3);
        var deltas = new List<CopilotStreamDelta>();
        var usageUpdates = new List<CopilotTokenUsage>();
        var retries = new List<CopilotProviderRetryInfo>();

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(
            () => service.StreamReplyAsync(
                CreateProfile(providerType),
                [new CopilotRequestMessage("user", "Do not silently skip damaged output.")],
                deltas.Add,
                retries.Add,
                usageUpdates.Add,
                CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal(hasProgress ? "Partial." : string.Empty, string.Concat(deltas.Select(delta => delta.Content)));
        if (hasProgress)
            Assert.Equal(14, usageUpdates.Last().TotalTokens);
        else
            Assert.Empty(usageUpdates);
        Assert.Equal("invalid_response_format", failure.ErrorCode);
        Assert.False(failure.IsTransient);
        Assert.Equal("req_redacted_malformed", failure.RequestId);
        Assert.DoesNotContain("test-key", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", failure.Message, StringComparison.Ordinal);
        Assert.Empty(retries);
    }

    [Theory]
    [InlineData(CopilotProviderType.OpenAICompatible)]
    [InlineData(CopilotProviderType.AnthropicCompatible)]
    public async Task MalformedSuccessfulJsonIsReportedAsAProtocolFailure(CopilotProviderType providerType)
    {
        using var handler = new SequentialHandler(_ => CreateJsonResponse(
            "{\"secret\":\"test-key\",broken}", "req_invalid_json"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 3);

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(
            () => service.StreamReplyAsync(
                CreateProfile(providerType),
                [new CopilotRequestMessage("user", "Report damaged JSON accurately.")],
                _ => { },
                CancellationToken.None));

        Assert.Equal(1, handler.CallCount);
        Assert.Equal("invalid_response_format", failure.ErrorCode);
        Assert.False(failure.IsTransient);
        Assert.Equal("req_invalid_json", failure.RequestId);
        Assert.DoesNotContain("no displayable text", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("secret", failure.Message, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(CopilotProviderType.OpenAICompatible)]
    [InlineData(CopilotProviderType.AnthropicCompatible)]
    public async Task ValidUnknownEventsCommentsAndMultilineDataRemainSupported(CopilotProviderType providerType)
    {
        var completed = providerType == CopilotProviderType.OpenAICompatible
            ? CreateCompletedOpenAiStream("Done.")
            : CreateCompletedAnthropicStream("Done.");
        using var handler = new SequentialHandler(_ => CreateStreamingResponse(
            ": keep-alive\n\nevent: ping\ndata: {\"type\":\"ping\"}\n\n"
            + "event: extension\ndata: {\"type\":\"future_event\",\n"
            + "data: \"metadata\":true}\n\n"
            + completed));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 1);
        var deltas = new List<CopilotStreamDelta>();

        await service.StreamReplyAsync(
            CreateProfile(providerType),
            [new CopilotRequestMessage("user", "Allow protocol extensions and keep-alives.")],
            deltas.Add,
            CancellationToken.None);

        Assert.Equal("Done.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task StreamingUsageUpdatesArePublishedBeforeTheResultCompletes()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(
                "data: {\"choices\":[{\"delta\":{\"content\":\"Done.\"}}]}\n\n"
                + "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":4,\"total_tokens\":14,\"prompt_tokens_details\":{\"cached_tokens\":6}}}\n\n"
                + "data: [DONE]\n\n"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 1);
        var usageUpdates = new List<CopilotTokenUsage>();

        var result = await service.StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible),
            [new CopilotRequestMessage("user", "Report usage while streaming.")],
            _ => { },
            onRetry: null,
            usageUpdates.Add,
            CancellationToken.None);

        var usage = Assert.Single(usageUpdates);
        Assert.Equal(new CopilotTokenUsage(10, 4, 14, 6), usage);
        Assert.Equal(usage, result.Usage);
    }

    [Fact]
    public async Task AnthropicOverloadBeforeContentIsRetried()
    {
        using var handler = new SequentialHandler(call => call == 1
            ? CreateStreamingResponse(
                "event: error\n"
                + "data: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"},"
                + "\"request_id\":\"req_anthropic_overload\"}\n\n",
                "req_header_fallback")
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
        Assert.Equal("req_anthropic_overload", retry.RequestId);
        Assert.Contains(
            "request req_anthropic_overload",
            retry.ToDiagnosticText(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task TransientPayloadErrorAfterContentIsNotRetried()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(
                "data: {\"choices\":[{\"delta\":{\"content\":\"Partial.\"}}]}\n\n"
                + "event: error\n"
                + "data: {\"type\":\"error\",\"error\":{\"type\":\"overloaded_error\",\"message\":\"Overloaded\"}}\n\n",
                "req_partial_overload"));
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
        Assert.Equal("req_partial_overload", failure.RequestId);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task OpenAiTopLevelErrorIsReportedAndNotRetried()
    {
        using var handler = new SequentialHandler(
            _ => CreateStreamingResponse(
                "event: error\n"
                + "data: {\"type\":\"error\",\"code\":\"invalid_request_error\","
                + "\"message\":\"Unsupported model for test-key\",\"param\":\"model\"}\n\n",
                "req_openai_invalid"));
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
        Assert.Equal("req_openai_invalid", failure.RequestId);
        Assert.Contains("Unsupported model", failure.Message, StringComparison.Ordinal);
        Assert.Contains("<redacted>", failure.Message, StringComparison.Ordinal);
        Assert.Contains(
            "[request req_openai_invalid]",
            failure.Message,
            StringComparison.Ordinal);
        Assert.DoesNotContain("test-key", failure.Message, StringComparison.Ordinal);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task OpenAiResponseFailedServerErrorBeforeContentIsRetried()
    {
        using var handler = new SequentialHandler(call => call == 1
            ? CreateStreamingResponse(
                "data: {\"type\":\"response.failed\",\"response\":{\"status\":\"failed\","
                + "\"error\":{\"code\":\"server_error\",\"message\":\"Generation failed.\"}}}\n\n",
                "req_response_failed")
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
        var retry = Assert.Single(retries);
        Assert.Equal("server_error", retry.FailureKind);
        Assert.Equal("req_response_failed", retry.RequestId);
    }

    [Fact]
    public async Task SuccessfulJsonErrorPayloadIsNotMisreportedAsEmptyResponse()
    {
        using var handler = new SequentialHandler(
            _ => CreateJsonResponse(
                "{\"error\":{\"type\":\"authentication_error\","
                + "\"message\":\"Credential test-key was rejected.\"},"
                + "\"request_id\":\"req_json_auth\"}",
                "req_header_fallback"));
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
        Assert.Equal("req_json_auth", failure.RequestId);
        Assert.Contains("Credential <redacted> was rejected.", failure.Message, StringComparison.Ordinal);
        Assert.Contains("[request req_json_auth]", failure.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("no displayable text", failure.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task HttpErrorPreservesHeaderRequestId()
    {
        using var handler = new SequentialHandler(
            _ => CreateJsonResponse(
                "{\"error\":{\"type\":\"authentication_error\","
                + "\"message\":\"Credential rejected.\"}}",
                "req_http_401",
                HttpStatusCode.Unauthorized,
                "request-id"));
        using var httpClient = new HttpClient(handler);
        var service = CreateService(httpClient, maximumAttempts: 1);

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.StreamReplyAsync(
                CreateProfile(CopilotProviderType.AnthropicCompatible),
                [new CopilotRequestMessage("user", "Keep the provider request ID.")],
                _ => { },
                CancellationToken.None));

        Assert.Equal("req_http_401", CopilotProviderRequestId.Find(failure));
        Assert.Contains("[request req_http_401]", failure.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestIdNormalizationBoundsUntrustedValues()
    {
        var oversized = new string('a', 200);

        Assert.Equal(
            "req_bad_value_script",
            CopilotProviderRequestId.Normalize(" req_bad value<script> "));
        Assert.Equal(
            "req_redacted_suffix",
            CopilotProviderRequestId.Redact(
                "req_test-key_suffix",
                "test-key"));
        Assert.Equal(128, CopilotProviderRequestId.Normalize(oversized).Length);
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

    private static HttpResponseMessage CreateStreamingResponse(
        string eventStream,
        string? requestId = null)
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StreamContent(
                new MemoryStream(Encoding.UTF8.GetBytes(eventStream))),
        };
        response.Content.Headers.ContentType =
            new System.Net.Http.Headers.MediaTypeHeaderValue("text/event-stream");
        if (!string.IsNullOrWhiteSpace(requestId))
            response.Headers.TryAddWithoutValidation("x-request-id", requestId);
        return response;
    }

    private static HttpResponseMessage CreateJsonResponse(
        string json,
        string? requestId = null,
        HttpStatusCode statusCode = HttpStatusCode.OK,
        string requestIdHeaderName = "x-request-id")
    {
        var response = new HttpResponseMessage(statusCode)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json"),
        };
        if (!string.IsNullOrWhiteSpace(requestId))
        {
            response.Headers.TryAddWithoutValidation(
                requestIdHeaderName,
                requestId);
        }
        return response;
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
