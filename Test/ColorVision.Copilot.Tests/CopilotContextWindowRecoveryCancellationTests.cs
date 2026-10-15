using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotContextWindowRecoveryCancellationTests
{
    [Theory]
    [InlineData(false, 1, false)]
    [InlineData(true, 1, false)]
    [InlineData(false, 2, false)]
    [InlineData(true, 2, false)]
    [InlineData(false, 1, true)]
    [InlineData(false, 2, true)]
    public async Task CancellationWithASettledContextRejectionDoesNotBecomeContextExhaustion(bool streaming, int cancelOnAttempt, bool hasReportedUsage)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new ContextRejectingProvider(cancellation, cancelOnAttempt, hasReportedUsage);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateClient(provider, recoveries, out var budgetClient);
        var messages = CreateHistory();
        var updates = new List<ChatResponseUpdate>();

        var failure = await Record.ExceptionAsync(() => InvokeAsync(client, messages, streaming, updates, cancellation.Token));

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        Assert.Equal(cancelOnAttempt, provider.Requests.Count);
        Assert.Equal(cancelOnAttempt, budgetClient.Snapshot.ProviderCalls);
        Assert.Equal(cancelOnAttempt - 1, recoveries.Count);
        Assert.Equal(messages.Length, provider.Requests[0].Length);
        if (cancelOnAttempt == 2)
            Assert.True(provider.Requests[1].Length < provider.Requests[0].Length);
        Assert.Equal(11, messages.Length);
        Assert.Equal("original-0 " + new string('a', 2_000), messages[0].Text);
        Assert.Empty(updates);
        var expectedTokens = hasReportedUsage ? cancelOnAttempt * 20 : 0;
        Assert.Equal(expectedTokens, CopilotProviderRetryChatClient.ExtractFailureUsage(cancelled).EffectiveTotalTokens);
        Assert.Equal(expectedTokens, budgetClient.Snapshot.ReportedTotalTokens);
        Assert.Equal(expectedTokens, budgetClient.Snapshot.ConsumedTokens);
        Assert.False(budgetClient.Snapshot.UsedEstimatedUsage);
        if (hasReportedUsage)
            Assert.Equal(cancelOnAttempt * 3, CopilotProviderRetryChatClient.ExtractFailureUsage(cancelled).CachedInputTokens);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public async Task AnUncancelledSecondRejectionStillReportsOneBoundedContextRecovery(bool streaming, bool hasReportedUsage)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new ContextRejectingProvider(cancellation, cancelOnAttempt: 0, hasReportedUsage);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateClient(provider, recoveries, out var budgetClient);
        var updates = new List<ChatResponseUpdate>();

        var failure = await Record.ExceptionAsync(() => InvokeAsync(client, CreateHistory(), streaming, updates, cancellation.Token));

        var exhausted = Assert.IsType<CopilotAgentContextWindowRecoveryExhaustedException>(failure);
        Assert.Same(provider.Rejections[1], exhausted.InnerException);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(2, budgetClient.Snapshot.ProviderCalls);
        Assert.Single(recoveries);
        Assert.True(provider.Requests[1].Length < provider.Requests[0].Length);
        var expectedTokens = hasReportedUsage ? 40 : 0;
        var usage = streaming
            ? updates.Aggregate(CopilotTokenUsage.Empty, (current, update) => current.Add(CopilotTokenBudgetChatClient.ExtractUsage(update.Contents)))
            : CopilotProviderRetryChatClient.ExtractFailureUsage(exhausted);
        Assert.Equal(expectedTokens, usage.EffectiveTotalTokens);
        Assert.Equal(expectedTokens, budgetClient.Snapshot.ReportedTotalTokens);
        Assert.Equal(expectedTokens, budgetClient.Snapshot.ConsumedTokens);
        Assert.False(budgetClient.Snapshot.UsedEstimatedUsage);
        if (hasReportedUsage)
            Assert.Equal(6, usage.CachedInputTokens);
        else
            Assert.Empty(updates);
    }

    [Theory]
    [InlineData(false, "stop", false)]
    [InlineData(false, "length", false)]
    [InlineData(true, "stop", false)]
    [InlineData(true, "length", false)]
    [InlineData(true, "stop", true)]
    [InlineData(true, "length", true)]
    public async Task CancellationWithSettledRecoveryTerminalUsageRetainsBothAttemptBills(bool streaming, string finishReason, bool failCleanup)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new SettledRecoveryTerminalProvider(cancellation, new ChatFinishReason(finishReason), failCleanup);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateClient(provider, recoveries, out var budgetClient);
        var messages = CreateHistory();
        var updates = new List<ChatResponseUpdate>();

        var failure = await Record.ExceptionAsync(() => InvokeAsync(client, messages, streaming, updates, cancellation.Token));

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(failure);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(2, budgetClient.Snapshot.ProviderCalls);
        Assert.Equal(failCleanup ? 1 : 0, provider.CleanupFailureCount);
        Assert.Single(recoveries);
        Assert.True(provider.Requests[1].Length < provider.Requests[0].Length);
        Assert.Equal(11, messages.Length);
        Assert.Equal("original-0 " + new string('a', 2_000), messages[0].Text);
        var usage = streaming
            ? updates.Aggregate(CopilotTokenUsage.Empty,
                (current, update) => current.Add(CopilotTokenBudgetChatClient.ExtractUsage(update.Contents)))
            : CopilotProviderRetryChatClient.ExtractFailureUsage(cancelled);
        Assert.Equal(new CopilotTokenUsage(22, 13, 35, 5), usage);
        Assert.Equal(35, budgetClient.Snapshot.ReportedTotalTokens);
        Assert.Equal(35, budgetClient.Snapshot.ConsumedTokens);
        Assert.False(budgetClient.Snapshot.UsedEstimatedUsage);
        if (!streaming)
            Assert.Empty(updates);
        Assert.All(updates, update =>
        {
            Assert.Null(update.Role);
            Assert.Null(update.FinishReason);
            Assert.Null(update.RawRepresentation);
            Assert.All(update.Contents, content => Assert.IsType<UsageContent>(content));
        });
    }

    private static CopilotContextWindowRecoveryChatClient CreateClient(
        IChatClient provider,
        List<CopilotContextWindowRecoveryInfo> recoveries,
        out CopilotTokenBudgetChatClient budgetClient)
    {
        // Preserve the production ordering: cancellation and inactivity guards,
        // per-call accounting, bounded provider retry, then context recovery.
        var budget = new CopilotAgentTokenBudget
        {
            ContextWindowTokens = 65_536,
            MaxOutputTokens = 128,
            RequestTokenBudget = 131_072,
        };
        budgetClient = new CopilotTokenBudgetChatClient(
            new CopilotProviderInactivityChatClient(new CopilotCancellationGuardChatClient(provider)), budget);
        var retryClient = new CopilotProviderRetryChatClient(budgetClient,
            delayAsync: (_, _) => throw new InvalidOperationException("A context rejection is not a transient provider retry."));
        return new CopilotContextWindowRecoveryChatClient(retryClient, budget.InputBudgetTokens, recoveries.Add);
    }

    private static ChatMessage[] CreateHistory() => Enumerable.Range(0, 11)
        .Select(index => new ChatMessage(index % 2 == 0 ? ChatRole.User : ChatRole.Assistant,
            $"original-{index} " + new string((char)('a' + index), 2_000)))
        .ToArray();

    private static async Task InvokeAsync(IChatClient client, ChatMessage[] messages, bool streaming, List<ChatResponseUpdate> updates, CancellationToken cancellationToken)
    {
        if (streaming)
        {
            await foreach (var update in client.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
                updates.Add(update);
        }
        else
        {
            await client.GetResponseAsync(messages, cancellationToken: cancellationToken);
        }
    }

    private sealed class SettledRecoveryTerminalProvider(CancellationTokenSource callerCancellation, ChatFinishReason finishReason, bool failCleanup) : IChatClient
    {
        public List<ChatMessage[]> Requests { get; } = [];
        public int CleanupFailureCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToArray());
            if (Requests.Count == 1)
            {
                return Task.FromException<ChatResponse>(new CopilotProviderPayloadException(
                    "maximum context length exceeded", "context_length_exceeded", false, string.Empty,
                    new CopilotTokenUsage(12, 8, 20, 3)));
            }
            if (Requests.Count != 2)
                throw new InvalidOperationException("Cancellation must not start another provider request.");

            var response = new ChatResponse(new ChatMessage(ChatRole.Assistant, "Settled provider response."))
            {
                FinishReason = finishReason,
                Usage = new UsageDetails
                {
                    InputTokenCount = 10,
                    OutputTokenCount = 5,
                    TotalTokenCount = 15,
                    CachedInputTokenCount = 2,
                },
            };
            // Both response tasks and MoveNext complete synchronously with a settled bill.
            // Cancellation stops response effects, but cannot erase that provider usage.
            callerCancellation.Cancel();
            return Task.FromResult(response);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                var response = await GetResponseAsync(messages, options, cancellationToken);
                yield return new ChatResponseUpdate
                {
                    Role = ChatRole.Assistant,
                    FinishReason = response.FinishReason,
                    Contents = [new TextContent(response.Text), new UsageContent(response.Usage!)],
                };
            }
            finally
            {
                ThrowCleanupFailure();
            }
        }

        private void ThrowCleanupFailure()
        {
            if (!failCleanup || Requests.Count != 2)
                return;
            CleanupFailureCount++;
            throw new IOException("Controlled cleanup failure after settled terminal cancellation.");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }

    private sealed class ContextRejectingProvider(CancellationTokenSource callerCancellation, int cancelOnAttempt, bool hasReportedUsage) : IChatClient
    {
        public List<ChatMessage[]> Requests { get; } = [];
        public List<Exception> Rejections { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToArray());
            Exception rejection = hasReportedUsage
                ? new CopilotProviderPayloadException("maximum context length exceeded", "context_length_exceeded", false, string.Empty,
                    new CopilotTokenUsage(12, 8, 20, 3))
                : new HttpRequestException("maximum context length exceeded", null, HttpStatusCode.BadRequest);
            Rejections.Add(rejection);
            if (Requests.Count == cancelOnAttempt)
                callerCancellation.Cancel();
            // The HTTP rejection has already settled when cancellation is seen.
            // WaitAsync correctly retains that task's fault; the recovery decision
            // must still honor the caller's cancellation before reclassifying it.
            return Task.FromException<ChatResponse>(rejection);
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(IEnumerable<ChatMessage> messages,
            ChatOptions? options = null, [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            var response = await GetResponseAsync(messages, options, cancellationToken);
            yield return new ChatResponseUpdate(ChatRole.Assistant, response.Text);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }
    }
}
