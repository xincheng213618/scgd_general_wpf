using System.Net;
using System.Net.Http;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderRateLimitTrackerTests
{
    private static readonly DateTimeOffset CapturedAtUtc =
        new(2026, 7, 31, 8, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData("1m2.5s", 62_500)]
    [InlineData("500ms", 500)]
    [InlineData("2", 2_000)]
    public void ResetParserSupportsProviderRelativeDurations(
        string value,
        int expectedMilliseconds)
    {
        Assert.True(CopilotProviderRateLimitTimeParser.TryResolveResetDeadline(
            value,
            CapturedAtUtc,
            out var deadlineUtc));
        Assert.Equal(CapturedAtUtc.AddMilliseconds(expectedMilliseconds), deadlineUtc);
    }

    [Fact]
    public void RetryAfterParserSupportsHttpDate()
    {
        Assert.True(CopilotProviderRateLimitTimeParser.TryResolveRetryAfterDeadline(
            "Fri, 31 Jul 2026 08:00:30 GMT",
            CapturedAtUtc,
            out var deadlineUtc));
        Assert.Equal(CapturedAtUtc.AddSeconds(30), deadlineUtc);
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
}
