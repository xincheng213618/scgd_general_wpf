using ColorVision.Copilot;
using System.Net;
using System.Net.Http;
using System.Text;

namespace ColorVision.UI.Tests;

public sealed class CopilotProviderRateLimitTrackerTests
{
    [Fact]
    public void StatusPresentationIsHiddenWithoutCapturedHeaders()
    {
        var presentation = CopilotProviderRateLimitStatusPresenter.Create(
            CopilotProviderRateLimitSnapshot.Empty);

        Assert.False(presentation.IsVisible);
        Assert.Empty(presentation.Label);
        Assert.Empty(presentation.ToolTip);
        Assert.False(presentation.IsUnderPressure);
    }

    [Fact]
    public void StatusPresentationShowsLatestRequestBucketAndAccountBoundary()
    {
        var presentation = CopilotProviderRateLimitStatusPresenter.Create(
            new CopilotProviderRateLimitSnapshot
            {
                CapturedAtUtc = new DateTimeOffset(2026, 7, 31, 8, 0, 0, TimeSpan.Zero),
                RequestLimit = 20,
                RequestRemaining = 19,
                TokenLimit = 10_000,
                TokenRemaining = 9_000,
            });

        Assert.True(presentation.IsVisible);
        Assert.Equal("请求 19/20", presentation.Label);
        Assert.False(presentation.IsUnderPressure);
        Assert.Contains("供应商限额：请求：剩余 19/20", presentation.ToolTip, StringComparison.Ordinal);
        Assert.Contains("可能已经过期", presentation.ToolTip, StringComparison.Ordinal);
        Assert.Contains("不代表账户套餐余额或可用金额", presentation.ToolTip, StringComparison.Ordinal);
        Assert.Contains("/usage session", presentation.ToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void StatusPresentationPrioritizesPressuredTokenBucket()
    {
        var presentation = CopilotProviderRateLimitStatusPresenter.Create(
            new CopilotProviderRateLimitSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                RequestLimit = 20,
                RequestRemaining = 19,
                TokenLimit = 10_000,
                TokenRemaining = 900,
            });

        Assert.Equal("Token 900/10K", presentation.Label);
        Assert.True(presentation.IsUnderPressure);
    }

    [Fact]
    public void StatusPresentationMarksRetryAfterAsThrottled()
    {
        var presentation = CopilotProviderRateLimitStatusPresenter.Create(
            new CopilotProviderRateLimitSnapshot
            {
                CapturedAtUtc = DateTimeOffset.UtcNow,
                RequestLimit = 20,
                RequestRemaining = 19,
                RetryAfter = "4",
            });

        Assert.Equal("限流重试", presentation.Label);
        Assert.True(presentation.IsUnderPressure);
        Assert.Contains("Retry-After 4", presentation.ToolTip, StringComparison.Ordinal);
    }

    [Fact]
    public void CapturesOpenAiAndProjectRateLimitHeadersPerProfile()
    {
        var profileId = Guid.NewGuid().ToString("N");
        using var response = new HttpResponseMessage(HttpStatusCode.OK);
        response.Headers.TryAddWithoutValidation("x-ratelimit-limit-requests", "20");
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-requests", "19");
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-requests", "2s");
        response.Headers.TryAddWithoutValidation("x-ratelimit-limit-tokens", "1000");
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-tokens", "900");
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-tokens", "1s");
        response.Headers.TryAddWithoutValidation("x-ratelimit-limit-project-tokens", "2000");
        response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-project-tokens", "1800");
        response.Headers.TryAddWithoutValidation("x-ratelimit-reset-project-tokens", "500ms");
        response.Headers.TryAddWithoutValidation("x-request-id", "req_rate_limit");

        try
        {
            CopilotProviderRateLimitTracker.Capture(profileId, response);
            var snapshot = CopilotProviderRateLimitTracker.GetSnapshot(profileId);

            Assert.NotEqual(default, snapshot.CapturedAtUtc);
            Assert.Equal(20, snapshot.RequestLimit);
            Assert.Equal(19, snapshot.RequestRemaining);
            Assert.Equal("2s", snapshot.RequestReset);
            Assert.Equal(1000, snapshot.TokenLimit);
            Assert.Equal(900, snapshot.TokenRemaining);
            Assert.Equal("1s", snapshot.TokenReset);
            Assert.Equal(2000, snapshot.ProjectTokenLimit);
            Assert.Equal(1800, snapshot.ProjectTokenRemaining);
            Assert.Equal("500ms", snapshot.ProjectTokenReset);
            Assert.Equal("req_rate_limit", snapshot.RequestId);
            Assert.Same(
                CopilotProviderRateLimitSnapshot.Empty,
                CopilotProviderRateLimitTracker.GetSnapshot(Guid.NewGuid().ToString("N")));
        }
        finally
        {
            CopilotProviderRateLimitTracker.Clear(profileId);
        }
    }

    [Fact]
    public void CapturesAnthropicAliasesAndKeepsSnapshotAfterHeaderlessResponse()
    {
        var profileId = Guid.NewGuid().ToString("N");
        using var response = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        response.Headers.TryAddWithoutValidation("x-ratelimit-limit-requests", "invalid");
        response.Headers.TryAddWithoutValidation("anthropic-ratelimit-requests-limit", "10");
        response.Headers.TryAddWithoutValidation("anthropic-ratelimit-requests-remaining", "0");
        response.Headers.TryAddWithoutValidation("anthropic-ratelimit-requests-reset", "2026-07-31T06:00:00Z");
        response.Headers.TryAddWithoutValidation("anthropic-ratelimit-tokens-limit", "500");
        response.Headers.TryAddWithoutValidation("anthropic-ratelimit-tokens-remaining", "0");
        response.Headers.TryAddWithoutValidation("anthropic-ratelimit-tokens-reset", "3s");
        response.Headers.TryAddWithoutValidation("Retry-After", "4");

        try
        {
            CopilotProviderRateLimitTracker.Capture(profileId, response);
            var captured = CopilotProviderRateLimitTracker.GetSnapshot(profileId);
            using var headerlessResponse = new HttpResponseMessage(HttpStatusCode.OK);
            CopilotProviderRateLimitTracker.Capture(profileId, headerlessResponse);

            Assert.Same(captured, CopilotProviderRateLimitTracker.GetSnapshot(profileId));
            Assert.Equal(10, captured.RequestLimit);
            Assert.Equal(0, captured.RequestRemaining);
            Assert.Equal("2026-07-31T06:00:00Z", captured.RequestReset);
            Assert.Equal(500, captured.TokenLimit);
            Assert.Equal(0, captured.TokenRemaining);
            Assert.Equal("3s", captured.TokenReset);
            Assert.Equal("4", captured.RetryAfter);
        }
        finally
        {
            CopilotProviderRateLimitTracker.Clear(profileId);
        }
    }

    [Fact]
    public async Task TrackingHandlerCapturesAgentSdkResponsesWithoutOwningInnerHandler()
    {
        var profileId = Guid.NewGuid().ToString("N");
        using var innerHandler = new RateLimitResponseHandler();
        using var trackingHandler = new CopilotProviderRateLimitTrackingHandler(profileId, innerHandler);
        using var client = new HttpClient(trackingHandler, disposeHandler: false);

        try
        {
            using var response = await client.GetAsync("https://example.test/rate-limit");
            var snapshot = CopilotProviderRateLimitTracker.GetSnapshot(profileId);

            Assert.Equal(7, snapshot.RequestRemaining);
            trackingHandler.Dispose();
            Assert.False(innerHandler.IsDisposed);
        }
        finally
        {
            CopilotProviderRateLimitTracker.Clear(profileId);
        }
    }

    [Fact]
    public async Task DirectChatServiceCapturesLatestProviderHeaders()
    {
        var profile = new CopilotProfileConfig
        {
            ProviderType = CopilotProviderType.OpenAICompatible,
            VendorType = CopilotVendorType.Custom,
            ApiKey = "test-key",
            BaseUrl = "https://example.test/v1",
            Model = "test-model",
        };
        using var handler = new RateLimitResponseHandler(includeReply: true);
        using var client = new HttpClient(handler);
        var service = new CopilotChatService(client);

        try
        {
            var reply = await service.CompleteReplyAsync(
                profile,
                [new CopilotRequestMessage("user", "hello")],
                CancellationToken.None);
            var snapshot = CopilotProviderRateLimitTracker.GetSnapshot(profile.Id);

            Assert.Equal("OK", reply.Content);
            Assert.Equal(7, snapshot.RequestRemaining);
        }
        finally
        {
            CopilotProviderRateLimitTracker.Clear(profile.Id);
        }
    }

    private sealed class RateLimitResponseHandler(bool includeReply = false) : HttpMessageHandler
    {
        public bool IsDisposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(
                    includeReply
                        ? "{\"choices\":[{\"message\":{\"role\":\"assistant\",\"content\":\"OK\"},\"finish_reason\":\"stop\"}]}"
                        : string.Empty,
                    Encoding.UTF8,
                    "application/json"),
            };
            response.Headers.TryAddWithoutValidation("x-ratelimit-limit-requests", "10");
            response.Headers.TryAddWithoutValidation("x-ratelimit-remaining-requests", "7");
            return Task.FromResult(response);
        }

        protected override void Dispose(bool disposing)
        {
            IsDisposed = true;
            base.Dispose(disposing);
        }
    }
}
