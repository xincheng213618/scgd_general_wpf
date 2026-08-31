using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotTokenUsageMergeTests
{
    [Theory]
    [InlineData(60, 25, 0, 100)]
    [InlineData(60, 50, 0, 110)]
    [InlineData(60, 50, 80, 110)]
    [InlineData(55, 15, 80, 100)]
    [InlineData(60, 25, 120, 120)]
    public void ProgressPreservesReportedTotalsAndIncludesLargerMergedParts(
        int inputTokens,
        int outputTokens,
        int totalTokens,
        int expectedTotal)
    {
        var previous = new CopilotTokenUsage(60, 20, 100, 10);

        var merged = previous.MergeProgress(new CopilotTokenUsage(inputTokens, outputTokens, totalTokens));

        Assert.Equal(expectedTotal, merged.TotalTokens);
        Assert.Equal(expectedTotal, merged.EffectiveTotalTokens);
        Assert.Equal(10, merged.CachedInputTokens);
    }

    [Fact]
    public void ProgressSaturatesMergedPartsWithoutOverflow()
    {
        var previous = new CopilotTokenUsage(int.MaxValue - 10, 5, int.MaxValue - 5, 100);

        var merged = previous.MergeProgress(new CopilotTokenUsage(int.MaxValue, 20, 0, 90));

        Assert.Equal(int.MaxValue, merged.InputTokens);
        Assert.Equal(20, merged.OutputTokens);
        Assert.Equal(int.MaxValue, merged.TotalTokens);
        Assert.Equal(int.MaxValue, merged.EffectiveTotalTokens);
        Assert.Equal(100, merged.CachedInputTokens);
    }

    [Fact]
    public void ProgressRetainsCacheAndInputOutputHighWaterMarks()
    {
        var previous = new CopilotTokenUsage(100, 40, 180, 70);

        var merged = previous.MergeProgress(new CopilotTokenUsage(110, 30, 0, 60));

        Assert.Equal(new CopilotTokenUsage(110, 40, 180, 70), merged);
        Assert.Equal(merged, merged.MergeProgress(CopilotTokenUsage.Empty));
        Assert.Equal(merged, CopilotTokenUsage.Empty.MergeProgress(merged));
    }

    [Fact]
    public async Task PartialFrameworkUsageCannotReopenAnExhaustedProviderBudget()
    {
        var provider = new PartialUsageChatClient();
        using var client = new CopilotTokenBudgetChatClient(provider, new CopilotAgentTokenBudget
        {
            ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
            MaxOutputTokens = 128,
            RequestTokenBudget = 4_096,
        });
        var messages = new[] { new ChatMessage(ChatRole.User, "Continue the task.") };
        var updateCount = 0;

        await foreach (var update in client.GetStreamingResponseAsync(messages))
        {
            Assert.Single(update.Contents.OfType<UsageContent>());
            updateCount++;
        }

        var snapshot = client.Snapshot;
        Assert.Equal(2, updateCount);
        Assert.Equal(1_000, snapshot.ReportedInputTokens);
        Assert.Equal(600, snapshot.ReportedOutputTokens);
        Assert.Equal(5_000, snapshot.ReportedTotalTokens);
        Assert.Equal(5_000, snapshot.ConsumedTokens);
        Assert.True(snapshot.BudgetExhausted);
        Assert.False(snapshot.UsedEstimatedUsage);
        await Assert.ThrowsAsync<CopilotAgentTokenBudgetExceededException>(() =>
            client.GetResponseAsync(messages));
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, client.Snapshot.ProviderCalls);
    }

    private sealed class PartialUsageChatClient : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatResponse(new ChatMessage(ChatRole.Assistant, "Unexpected extra call.")));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = 1_000,
                    OutputTokenCount = 500,
                    TotalTokenCount = 5_000,
                })],
            };
            cancellationToken.ThrowIfCancellationRequested();
            yield return new ChatResponseUpdate
            {
                Role = ChatRole.Assistant,
                Contents = [new UsageContent(new UsageDetails
                {
                    InputTokenCount = 1_000,
                    OutputTokenCount = 600,
                })],
            };
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
