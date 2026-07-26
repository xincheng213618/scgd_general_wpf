using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTokenBudgetMetricsTests
{
    [Fact]
    public async Task SnapshotTracksReportedUsagePeakInputAndDelegatedUsage()
    {
        using var client = CreateClient(new UsageDetails
        {
            InputTokenCount = 100,
            OutputTokenCount = 20,
            TotalTokenCount = 120,
            CachedInputTokenCount = 80,
        });

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Inspect the requested file.")],
            new ChatOptions { Instructions = "Use only verified evidence." });
        client.RecordDelegatedRunUsage(new CopilotDelegatedRunUsage
        {
            ProviderCalls = 3,
            PeakEstimatedInputTokens = 50_000,
            ProviderRetryCount = 2,
            ProviderRateLimitRetryCount = 1,
            ProviderRetryDelayMs = 1_250,
            ContextRecoveryCount = 2,
            ContextRecoveryEstimatedInputTokensBefore = 90_000,
            ContextRecoveryEstimatedInputTokensAfter = 35_000,
            ConsumedTokens = 60,
            Usage = new CopilotTokenUsage(50, 10, 60, 30),
        });

        var snapshot = client.Snapshot;

        Assert.Equal(4, snapshot.ProviderCalls);
        Assert.Equal(180, snapshot.ConsumedTokens);
        Assert.Equal(50_000, snapshot.PeakEstimatedInputTokens);
        Assert.Equal(2, snapshot.ProviderRetryCount);
        Assert.Equal(1, snapshot.ProviderRateLimitRetryCount);
        Assert.Equal(1_250, snapshot.ProviderRetryDelayMs);
        Assert.Equal(2, snapshot.ContextRecoveryCount);
        Assert.Equal(90_000, snapshot.ContextRecoveryEstimatedInputTokensBefore);
        Assert.Equal(35_000, snapshot.ContextRecoveryEstimatedInputTokensAfter);
        Assert.Equal(150, snapshot.ReportedInputTokens);
        Assert.Equal(30, snapshot.ReportedOutputTokens);
        Assert.Equal(180, snapshot.ReportedTotalTokens);
        Assert.Equal(110, snapshot.ReportedCachedInputTokens);
        Assert.False(snapshot.UsedEstimatedUsage);
    }

    [Fact]
    public async Task SnapshotTracksProviderRetryKindAndPlannedDelay()
    {
        using var client = CreateClient(usage: null);

        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "First attempt.")]);
        client.RecordProviderRetry(new CopilotProviderRetryInfo(
            1,
            2,
            3,
            TimeSpan.FromMilliseconds(250),
            "HTTP 429",
            429));
        await client.GetResponseAsync([new ChatMessage(ChatRole.User, "Second attempt.")]);
        client.RecordProviderRetry(new CopilotProviderRetryInfo(
            2,
            3,
            3,
            TimeSpan.FromSeconds(1),
            "HTTP 503",
            503));

        var snapshot = client.Snapshot;

        Assert.Equal(2, snapshot.ProviderRetryCount);
        Assert.Equal(1, snapshot.ProviderRateLimitRetryCount);
        Assert.Equal(1_250, snapshot.ProviderRetryDelayMs);
    }

    [Fact]
    public async Task SnapshotRetainsPeakInputEstimateWhenProviderOmitsUsage()
    {
        using var client = CreateClient(usage: null);

        await client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarize this request.")],
            new ChatOptions { Instructions = "Answer concisely." });

        var snapshot = client.Snapshot;

        Assert.Equal(1, snapshot.ProviderCalls);
        Assert.True(snapshot.ConsumedTokens > 0);
        Assert.True(snapshot.PeakEstimatedInputTokens > 0);
        Assert.Equal(0, snapshot.ReportedInputTokens);
        Assert.Equal(0, snapshot.ReportedOutputTokens);
        Assert.Equal(0, snapshot.ReportedTotalTokens);
        Assert.Null(snapshot.ReportedCachedInputTokens);
        Assert.True(snapshot.UsedEstimatedUsage);
    }

    [Fact]
    public async Task ContextWindowRejectionRetainsTheAttemptedInputPeak()
    {
        using var client = new CopilotTokenBudgetChatClient(
            new UsageReportingChatClient(usage: null),
            new CopilotAgentTokenBudget
            {
                ContextWindowTokens = 64,
                MaxOutputTokens = 32,
                RequestTokenBudget = 128_000,
            });

        await Assert.ThrowsAsync<CopilotAgentContextWindowExceededException>(() =>
            client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, new string('x', 2_000))],
                new ChatOptions { Instructions = new string('r', 200) }));

        var snapshot = client.Snapshot;

        Assert.Equal(0, snapshot.ProviderCalls);
        Assert.True(snapshot.PeakEstimatedInputTokens > snapshot.InputBudgetTokens);
        Assert.True(snapshot.UsedEstimatedUsage);
    }

    private static CopilotTokenBudgetChatClient CreateClient(UsageDetails? usage)
    {
        return new CopilotTokenBudgetChatClient(
            new UsageReportingChatClient(usage),
            new CopilotAgentTokenBudget
            {
                ContextWindowTokens = 64_000,
                MaxOutputTokens = 4_096,
                RequestTokenBudget = 128_000,
            });
    }

    private sealed class UsageReportingChatClient(UsageDetails? usage) : IChatClient
    {
        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var contents = new List<AIContent> { new TextContent("Done.") };
            if (usage != null)
                contents.Add(new UsageContent(usage));
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, contents)));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.CompletedTask;
            yield break;
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
