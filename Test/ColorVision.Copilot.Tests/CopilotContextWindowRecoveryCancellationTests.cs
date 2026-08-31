using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotContextWindowRecoveryCancellationTests
{
    [Theory]
    [InlineData(false, 1)]
    [InlineData(true, 1)]
    [InlineData(false, 2)]
    [InlineData(true, 2)]
    public async Task CancellationWithASettledContextRejectionDoesNotBecomeContextExhaustion(bool streaming, int cancelOnAttempt)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new ContextRejectingProvider(cancellation, cancelOnAttempt);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateClient(provider, recoveries, out var budgetClient);
        var messages = CreateHistory();

        var failure = await Record.ExceptionAsync(() => InvokeAsync(client, messages, streaming, cancellation.Token));

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
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task AnUncancelledSecondRejectionStillReportsOneBoundedContextRecovery(bool streaming)
    {
        using var cancellation = new CancellationTokenSource();
        var provider = new ContextRejectingProvider(cancellation, cancelOnAttempt: 0);
        var recoveries = new List<CopilotContextWindowRecoveryInfo>();
        using var client = CreateClient(provider, recoveries, out var budgetClient);

        var failure = await Record.ExceptionAsync(() => InvokeAsync(client, CreateHistory(), streaming, cancellation.Token));

        var exhausted = Assert.IsType<CopilotAgentContextWindowRecoveryExhaustedException>(failure);
        Assert.Same(provider.Rejections[1], exhausted.InnerException);
        Assert.Equal(2, provider.Requests.Count);
        Assert.Equal(2, budgetClient.Snapshot.ProviderCalls);
        Assert.Single(recoveries);
        Assert.True(provider.Requests[1].Length < provider.Requests[0].Length);
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

    private static async Task InvokeAsync(IChatClient client, ChatMessage[] messages, bool streaming, CancellationToken cancellationToken)
    {
        if (streaming)
        {
            await foreach (var update in client.GetStreamingResponseAsync(messages, cancellationToken: cancellationToken))
                Assert.Fail("A rejected provider request must not yield a response update.");
        }
        else
        {
            await client.GetResponseAsync(messages, cancellationToken: cancellationToken);
        }
    }

    private sealed class ContextRejectingProvider(CancellationTokenSource callerCancellation, int cancelOnAttempt) : IChatClient
    {
        public List<ChatMessage[]> Requests { get; } = [];
        public List<HttpRequestException> Rejections { get; } = [];

        public Task<ChatResponse> GetResponseAsync(IEnumerable<ChatMessage> messages, ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            Requests.Add(messages.ToArray());
            var rejection = new HttpRequestException("maximum context length exceeded", null, HttpStatusCode.BadRequest);
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
