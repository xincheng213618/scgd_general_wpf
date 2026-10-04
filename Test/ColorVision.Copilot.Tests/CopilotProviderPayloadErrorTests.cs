using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderPayloadErrorTests
{
    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task InactivityTimeoutPreservesSettledOfficialUsage(bool streaming, bool hasPriorContent)
    {
        using var callerCancellation = new CancellationTokenSource();
        var provider = new SettledCancellationChatClient(hasPriorContent);
        using var client = new CopilotTokenBudgetChatClient(
            new CopilotProviderInactivityChatClient(provider,
                firstResponseTimeout: hasPriorContent ? TimeSpan.FromSeconds(30) : TimeSpan.FromMilliseconds(25),
                streamingUpdateTimeout: TimeSpan.FromMilliseconds(25)),
            new CopilotAgentTokenBudget
            {
                ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
                MaxOutputTokens = 1_024,
                RequestTokenBudget = 4_096,
            });
        var updates = new List<ChatResponseUpdate>();
        async Task InvokeAsync()
        {
            if (streaming)
            {
                await foreach (var update in client.GetStreamingResponseAsync(
                    [new ChatMessage(ChatRole.User, "Keep the settled bill when the provider stalls.")],
                    cancellationToken: callerCancellation.Token))
                {
                    updates.Add(update);
                }
            }
            else
            {
                await client.GetResponseAsync(
                    [new ChatMessage(ChatRole.User, "Keep the settled bill when the provider stalls.")],
                    cancellationToken: callerCancellation.Token);
            }
        }

        var failure = await Assert.ThrowsAsync<CopilotProviderInactivityException>(
            () => InvokeAsync().WaitAsync(TimeSpan.FromSeconds(5)));

        Assert.Equal(hasPriorContent ? CopilotProviderInactivityPhase.StreamingUpdate : CopilotProviderInactivityPhase.FirstResponse,
            failure.Phase);
        Assert.False(callerCancellation.IsCancellationRequested);
        var settledFailure = Assert.IsAssignableFrom<OperationCanceledException>(provider.SettledFailure);
        Assert.True(settledFailure.CancellationToken.IsCancellationRequested);
        Assert.NotEqual(callerCancellation.Token, settledFailure.CancellationToken);
        var reportedUsage = new CopilotTokenUsage(12, 8, 20, 3);
        Assert.Equal(reportedUsage, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(settledFailure));
        Assert.Equal(reportedUsage, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(hasPriorContent ? "Partial." : string.Empty, string.Concat(updates.Select(update => update.Text)));
        Assert.Equal(1, client.Snapshot.ProviderCalls);
        Assert.Equal(12, client.Snapshot.ReportedInputTokens);
        Assert.Equal(8, client.Snapshot.ReportedOutputTokens);
        Assert.Equal(20, client.Snapshot.ReportedTotalTokens);
        Assert.Equal(20, client.Snapshot.ConsumedTokens);
        Assert.Equal(3, client.Snapshot.ReportedCachedInputTokens);
        Assert.False(client.Snapshot.UsedEstimatedUsage);
    }

    [Theory]
    [InlineData(false, "missing", "incomplete")]
    [InlineData(true, "missing", "incomplete")]
    [InlineData(false, "empty", "incomplete")]
    [InlineData(true, "empty", "incomplete")]
    [InlineData(false, "stop", "incomplete")]
    [InlineData(true, "stop", "incomplete")]
    [InlineData(false, "max_output_tokens", "max_output_tokens")]
    [InlineData(true, "max_output_tokens", "max_output_tokens")]
    public async Task IncompleteResponsesRemainIncompleteWhenTheirReasonIsUnavailable(bool streaming, string reason, string expectedFinishReason)
    {
        var response = new Dictionary<string, object?>
        {
            ["status"] = "incomplete",
            ["output_text"] = "Partial summary.",
            ["usage"] = new { input_tokens = 100, output_tokens = 10, total_tokens = 110 },
        };
        if (reason != "missing")
            response["incomplete_details"] = new { reason = reason == "empty" ? "" : reason };
        var body = JsonSerializer.Serialize(response);
        using var handler = new SequentialHandler(_ => streaming
            ? CreateStreamingResponse("data: {\"type\":\"response.output_text.delta\",\"delta\":\"Partial summary.\"}\n\n"
                + "data: " + JsonSerializer.Serialize(new { type = "response.incomplete", response }) + "\n\n")
            : CreateJsonResponse(body));
        using var httpClient = new HttpClient(handler);

        var result = await CreateService(httpClient, maximumAttempts: 1).CompleteReplyDetailedAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible),
            [new CopilotRequestMessage("user", "Summarize earlier conversation.")], CancellationToken.None);

        Assert.Equal("Partial summary.", result.Content);
        Assert.Equal(expectedFinishReason, result.StreamResult.FinishReason);
        Assert.True(result.IsIncomplete);
        Assert.Equal(110, result.Usage.EffectiveTotalTokens);
        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task SpecificErrorCodeRedactsCredentialsBeforeTruncation(bool isHttp)
    {
        var profile = CreateProfile(CopilotProviderType.OpenAICompatible);
        profile.ApiKey = new string('z', 100);
        var body = JsonSerializer.Serialize(new { error = new { code = profile.ApiKey, message = "Controlled failure." } });
        using var handler = new SequentialHandler(_ => isHttp
            ? CreateJsonResponse(body, statusCode: HttpStatusCode.BadRequest)
            : CreateStreamingResponse("data: " + body + "\n\n"));
        using var httpClient = new HttpClient(handler);

        var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => CreateService(httpClient, 1).StreamReplyAsync(
            profile, [new CopilotRequestMessage("user", "Read the result.")], _ => { }, CancellationToken.None));

        Assert.Contains("redacted", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(new string('z', 64), error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task CancellationDuringServerBackoffDoesNotSendAnotherRequest()
    {
        using var handler = new SequentialHandler(_ =>
        {
            var response = CreateJsonResponse("{\"error\":{\"code\":\"slow_down\",\"message\":\"Retry later.\"}}", statusCode: HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", "120");
            return response;
        });
        using var httpClient = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var service = new CopilotChatService(httpClient, 3, _ => TimeSpan.Zero, (delay, token) =>
        {
            Assert.Equal(TimeSpan.FromMinutes(2), delay);
            cancellation.Cancel();
            return Task.FromCanceled(token);
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Read the result.")],
            _ => { }, cancellation.Token));

        Assert.Equal(1, handler.CallCount);
    }

    [Theory]
    [InlineData("insufficient_quota", "rate_limit_error")]
    [InlineData("credit_balance_exhausted", "rate_limit_error")]
    [InlineData("organization_spend_limit_exceeded", "insufficient_quota")]
    [InlineData("project_spend_limit_exceeded", "rate_limit_error")]
    [InlineData("organization_usage_limit_exceeded", "insufficient_quota")]
    [InlineData("vendor_code", "insufficient_quota")]
    [InlineData("slow_down", "insufficient_quota")]
    public async Task BillingErrorsStopInHttpJsonAndStreamResponses(string code, string type)
    {
        var body = JsonSerializer.Serialize(new { error = new { code, type, message = "Account action required." } });
        foreach (var transport in new[] { "http", "json", "sse", "responses" })
        {
            using var handler = new SequentialHandler(_ => transport switch
            {
                "http" => CreateJsonResponse(body, statusCode: HttpStatusCode.TooManyRequests),
                "json" => CreateJsonResponse(body),
                "responses" => CreateStreamingResponse("data: " + JsonSerializer.Serialize(new
                {
                    type = "response.failed", response = new { error = new { code, type, message = "Account action required." } },
                }) + "\n\n"),
                _ => CreateStreamingResponse("data: " + body + "\n\n"),
            });
            using var httpClient = new HttpClient(handler);
            var retries = new List<CopilotProviderRetryInfo>();
            var service = CreateService(httpClient, maximumAttempts: 3);

            var error = await Assert.ThrowsAnyAsync<InvalidOperationException>(() => service.StreamReplyAsync(
                CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Read the result.")],
                _ => { }, retries.Add, CancellationToken.None));

            Assert.Contains(code, error.Message, StringComparison.Ordinal);
            Assert.Equal(1, handler.CallCount);
            Assert.Empty(retries);
        }
    }

    [Theory]
    [InlineData(429, "slow_down", "rate_limit_error")]
    [InlineData(503, "server_is_overloaded", "service_unavailable_error")]
    public async Task TransientErrorsKeepSpecificCodeAcrossHttpAndPayloads(int status, string code, string type)
    {
        foreach (var isHttp in new[] { true, false })
        {
            var body = JsonSerializer.Serialize(new { error = new { code, type, message = "Retry later." } });
            using var handler = new SequentialHandler(call => call > 1
                ? CreateStreamingResponse(CreateCompletedOpenAiStream("Recovered."))
                : isHttp ? CreateJsonResponse(body, statusCode: (HttpStatusCode)status) : CreateStreamingResponse("data: " + body + "\n\n"));
            using var httpClient = new HttpClient(handler);
            var retries = new List<CopilotProviderRetryInfo>();
            var deltas = new List<CopilotStreamDelta>();
            await CreateService(httpClient, 3).StreamReplyAsync(
                CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Read the result.")],
                deltas.Add, retries.Add, CancellationToken.None);

            Assert.Equal(2, handler.CallCount);
            Assert.Equal("Recovered.", string.Concat(deltas.Select(delta => delta.Content)));
            var retry = Assert.Single(retries);
            Assert.Equal(isHttp ? $"HTTP {status} ({code})" : code, retry.FailureKind);
            Assert.Equal(isHttp ? status : (int?)null, retry.StatusCode);
        }
    }

    [Theory]
    [InlineData("0", 1, true)]
    [InlineData("7", 7, true)]
    [InlineData("120", 120, true)]
    [InlineData("121", 121, false)]
    [InlineData("999999999999999999999", 0, false)]
    [InlineData("invalid", 1, true)]
    public async Task ChatHonorsRetryAfterWithoutShorteningLongDelays(string header, int expectedSeconds, bool shouldRetry)
    {
        using var handler = new SequentialHandler(call =>
        {
            if (call > 1)
                return CreateStreamingResponse(CreateCompletedOpenAiStream("Recovered."));
            var response = CreateJsonResponse("{\"error\":{\"code\":\"slow_down\",\"message\":\"Retry later.\"}}", statusCode: HttpStatusCode.TooManyRequests);
            response.Headers.TryAddWithoutValidation("Retry-After", header);
            return response;
        });
        using var httpClient = new HttpClient(handler);
        var delays = new List<TimeSpan>();
        var retries = new List<CopilotProviderRetryInfo>();
        var service = new CopilotChatService(httpClient, 3, _ => TimeSpan.FromSeconds(1),
            (delay, _) => { delays.Add(delay); return Task.CompletedTask; });
        Task<CopilotChatStreamResult> RunAsync() => service.StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Read the result.")],
            _ => { }, retries.Add, CancellationToken.None);

        if (shouldRetry)
        {
            await RunAsync();
            Assert.Equal(TimeSpan.FromSeconds(expectedSeconds), Assert.Single(delays));
            Assert.Single(retries);
        }
        else
        {
            var error = await Assert.ThrowsAsync<InvalidOperationException>(RunAsync);
            Assert.Contains("slow_down", error.Message, StringComparison.Ordinal);
            Assert.Empty(delays);
            Assert.Empty(retries);
        }
        Assert.Equal(shouldRetry ? 2 : 1, handler.CallCount);
    }

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

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RetriedPayloadUsageIsIncludedInSuccessfulResultAndUpdates(bool streaming)
    {
        using var handler = new SequentialHandler(call => call < 3
            ? CreateFailedOpenAiResponse(streaming, "server_error", new CopilotTokenUsage(6 * call, 4 * call, 10 * call, call))
            : streaming
                ? CreateStreamingResponse("data: {\"choices\":[{\"delta\":{\"content\":\"Recovered.\"},\"finish_reason\":\"stop\"}],"
                    + "\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15,\"prompt_tokens_details\":{\"cached_tokens\":2}}}\n\n"
                    + "data: [DONE]\n\n")
                : CreateJsonResponse("{\"choices\":[{\"message\":{\"content\":\"Recovered.\"},\"finish_reason\":\"stop\"}],"
                    + "\"usage\":{\"prompt_tokens\":10,\"completion_tokens\":5,\"total_tokens\":15,\"prompt_tokens_details\":{\"cached_tokens\":2}}}"));
        using var httpClient = new HttpClient(handler);
        var retries = new List<CopilotProviderRetryInfo>();
        var usageUpdates = new List<CopilotTokenUsage>();
        var deltas = new List<CopilotStreamDelta>();

        var result = await CreateService(httpClient, maximumAttempts: 3).StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Keep the bill of failed attempts.")],
            deltas.Add, retries.Add, usageUpdates.Add, CancellationToken.None);

        Assert.Equal(new CopilotTokenUsage(28, 17, 45, 5), result.Usage);
        Assert.Equal(new[]
        {
            new CopilotTokenUsage(6, 4, 10, 1),
            new CopilotTokenUsage(18, 12, 30, 3),
            result.Usage,
        }, usageUpdates);
        Assert.Equal("Recovered.", string.Concat(deltas.Select(delta => delta.Content)));
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(2, retries.Count);
    }

    [Theory]
    [InlineData(false, "server_error")]
    [InlineData(true, "server_error")]
    [InlineData(false, "insufficient_quota")]
    [InlineData(true, "insufficient_quota")]
    public async Task FinalPayloadFailurePreservesAllAttemptUsage(bool streaming, string finalCode)
    {
        var lastUsage = new CopilotTokenUsage(10, 5, 15, 2);
        using var handler = new SequentialHandler(call => CreateFailedOpenAiResponse(
            streaming, call < 3 ? "server_error" : finalCode,
            call < 3 ? new CopilotTokenUsage(6 * call, 4 * call, 10 * call, call) : lastUsage,
            "req_test-key_failed"));
        using var httpClient = new HttpClient(handler);
        var retries = new List<CopilotProviderRetryInfo>();
        var usageUpdates = new List<CopilotTokenUsage>();
        var deltas = new List<CopilotStreamDelta>();

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(() => CreateService(httpClient, 3).StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Keep settled usage on failure.")],
            deltas.Add, retries.Add, usageUpdates.Add, CancellationToken.None));

        var expectedUsage = new CopilotTokenUsage(28, 17, 45, 5);
        Assert.Equal(lastUsage, failure.ReportedUsage);
        Assert.Equal(expectedUsage, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(expectedUsage, usageUpdates.Last());
        Assert.Equal(finalCode, failure.ErrorCode);
        Assert.Equal(finalCode == "server_error", failure.IsTransient);
        Assert.Equal("req_redacted_failed", failure.RequestId);
        Assert.DoesNotContain("test-key", failure.Message, StringComparison.Ordinal);
        Assert.Equal(3, handler.CallCount);
        Assert.Equal(2, retries.Count);
        Assert.Empty(deltas);
    }

    [Fact]
    public async Task CancellationDuringBilledPayloadBackoffPreservesUsageWithoutSendingAnotherRequest()
    {
        var reportedUsage = new CopilotTokenUsage(12, 8, 20, 3);
        using var handler = new SequentialHandler(_ => CreateFailedOpenAiResponse(true, "server_error", reportedUsage));
        using var httpClient = new HttpClient(handler);
        using var cancellation = new CancellationTokenSource();
        var service = new CopilotChatService(httpClient, 3, _ => TimeSpan.Zero, (_, token) =>
        {
            cancellation.Cancel();
            return Task.FromCanceled(token);
        });
        var usageUpdates = new List<CopilotTokenUsage>();

        var failure = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Cancel during retry.")],
            _ => Assert.Fail("A failed response must not emit content."), onRetry: null, usageUpdates.Add, cancellation.Token));

        Assert.Equal(cancellation.Token, failure.CancellationToken);
        Assert.Equal(reportedUsage, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(reportedUsage, usageUpdates.Last());
        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task PayloadUsageMergesWithPriorUsageSnapshotWithoutEnablingReplay()
    {
        var reportedUsage = new CopilotTokenUsage(12, 8, 20, 3);
        using var failedResponse = CreateFailedOpenAiResponse(true, "server_error", reportedUsage);
        var failureEvent = await failedResponse.Content.ReadAsStringAsync();
        using var handler = new SequentialHandler(_ => CreateStreamingResponse(
            "data: {\"choices\":[],\"usage\":{\"prompt_tokens\":12,\"completion_tokens\":4,\"total_tokens\":16,\"prompt_tokens_details\":{\"cached_tokens\":3}}}\n\n"
            + failureEvent));
        using var httpClient = new HttpClient(handler);
        var usageUpdates = new List<CopilotTokenUsage>();
        var retries = new List<CopilotProviderRetryInfo>();

        var failure = await Assert.ThrowsAsync<CopilotProviderPayloadException>(() => CreateService(httpClient, 3).StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Do not replay a usage-only response.")],
            _ => Assert.Fail("This response has no content."), retries.Add, usageUpdates.Add, CancellationToken.None));

        Assert.Equal(reportedUsage, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(reportedUsage, usageUpdates.Last());
        Assert.Equal(1, handler.CallCount);
        Assert.Empty(retries);
    }

    [Fact]
    public async Task FinalHttpFailureWithoutReportedUsageKeepsOnlyPreviouslyBilledAttempts()
    {
        var reportedUsage = new CopilotTokenUsage(12, 8, 20, 3);
        using var handler = new SequentialHandler(call => call == 1
            ? CreateFailedOpenAiResponse(true, "server_error", reportedUsage)
            : CreateJsonResponse("{\"error\":{\"code\":\"authentication_error\",\"message\":\"Credential rejected.\"}}", statusCode: HttpStatusCode.Unauthorized));
        using var httpClient = new HttpClient(handler);
        var retries = new List<CopilotProviderRetryInfo>();
        var usageUpdates = new List<CopilotTokenUsage>();

        var failure = await Assert.ThrowsAsync<InvalidOperationException>(() => CreateService(httpClient, 3).StreamReplyAsync(
            CreateProfile(CopilotProviderType.OpenAICompatible), [new CopilotRequestMessage("user", "Do not estimate an HTTP error bill.")],
            _ => Assert.Fail("A failed response must not emit content."), retries.Add, usageUpdates.Add, CancellationToken.None));

        Assert.Equal(reportedUsage, CopilotTokenBudgetChatClient.ExtractPayloadFailureUsage(failure));
        Assert.Equal(reportedUsage, usageUpdates.Last());
        Assert.Equal(2, handler.CallCount);
        Assert.Single(retries);
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

    private static HttpResponseMessage CreateFailedOpenAiResponse(
        bool streaming,
        string code,
        CopilotTokenUsage usage,
        string requestId = "req_billed_failure")
    {
        var response = new
        {
            status = "failed",
            error = new { code, message = "Generation failed for test-key." },
            usage = new
            {
                input_tokens = usage.InputTokens,
                output_tokens = usage.OutputTokens,
                total_tokens = usage.TotalTokens,
                input_tokens_details = new { cached_tokens = usage.CachedInputTokens },
            },
        };
        return streaming
            ? CreateStreamingResponse("data: " + JsonSerializer.Serialize(new { type = "response.failed", response }) + "\n\n", requestId)
            : CreateJsonResponse(JsonSerializer.Serialize(response), requestId);
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

    private sealed class SettledCancellationChatClient(bool hasPriorContent) : IChatClient
    {
        public OperationCanceledException? SettledFailure { get; private set; }

        public async Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            throw await WaitForSettledCancellationAsync(cancellationToken);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            if (hasPriorContent)
                yield return new ChatResponseUpdate(ChatRole.Assistant, "Partial.");
            throw await WaitForSettledCancellationAsync(cancellationToken);
        }

        private async Task<OperationCanceledException> WaitForSettledCancellationAsync(CancellationToken cancellationToken)
        {
            try
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            }
            catch (OperationCanceledException exception)
            {
                SettledFailure = exception;
                CopilotTokenBudgetChatClient.PreserveSettledFailureUsage(exception, new CopilotTokenUsage(12, 8, 20, 3));
                return exception;
            }
            throw new InvalidOperationException("The provider's linked cancellation was not observed.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
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
