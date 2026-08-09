using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Net.Http;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotProviderConnectionRecoveryTests
{
    [Fact]
    public void RecoveryPolicyKeepsDelegatedAndInternalRunsBounded()
    {
        var rootRequest = new CopilotAgentRequest
        {
            ConversationId = "conversation",
            TaskId = "task",
            WorkspacePath = @"C:\workspace",
        };
        rootRequest.RuntimeExecutionScope = CopilotExecutionScope.ForAgentRun(rootRequest);
        var childRequest = new CopilotAgentRequest
        {
            RuntimeExecutionScope = rootRequest.RuntimeExecutionScope.DeriveChild(
                "run:" + Guid.NewGuid().ToString("N")),
        };
        var internalRequest = new CopilotAgentRequest
        {
            RuntimeExecutionScope = rootRequest.RuntimeExecutionScope,
            RuntimePurpose = CopilotAgentRuntimePurpose.DelegatedEvidenceFinalization,
        };

        Assert.True(CopilotProviderConnectionRecoveryChatClient.IsEligibleRootRequest(rootRequest));
        Assert.False(CopilotProviderConnectionRecoveryChatClient.IsEligibleRootRequest(childRequest));
        Assert.False(CopilotProviderConnectionRecoveryChatClient.IsEligibleRootRequest(internalRequest));
    }

    [Fact]
    public async Task RootRecoveryWaitsExponentiallyWithoutMultiplyingTokenBudget()
    {
        var provider = new RecoveringChatClient(connectionFailuresBeforeSuccess: 2);
        var recoveries = new List<CopilotProviderConnectionRecoveryInfo>();
        var delays = new List<TimeSpan>();
        using var recoveryClient = new CopilotProviderConnectionRecoveryChatClient(
            provider,
            recoveries.Add,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        using var budgetedClient = new CopilotTokenBudgetChatClient(
            recoveryClient,
            CreateBudget());
        var messages = new[] { new ChatMessage(ChatRole.User, "Keep the root Agent alive.") };

        var response = await budgetedClient.GetResponseAsync(messages, cancellationToken: CancellationToken.None);

        Assert.Equal("recovered", response.Text);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)],
            delays);
        Assert.Equal([1, 2], recoveries.Select(item => item.RecoveryAttempt));
        Assert.All(recoveries, item =>
        {
            CopilotProviderConnectionRecoveryProtocol.Validate(item);
            Assert.Contains("token accounting remain unchanged", item.ToDiagnosticText(), StringComparison.Ordinal);
        });
        Assert.Equal(1, budgetedClient.Snapshot.ProviderCalls);

        var baselineProvider = new RecoveringChatClient(connectionFailuresBeforeSuccess: 0);
        using var baselineClient = new CopilotTokenBudgetChatClient(
            baselineProvider,
            CreateBudget());
        await baselineClient.GetResponseAsync(messages, cancellationToken: CancellationToken.None);

        Assert.Equal(
            baselineClient.Snapshot.ConsumedTokens,
            budgetedClient.Snapshot.ConsumedTokens);
        Assert.Equal(
            baselineClient.Snapshot.UsedEstimatedUsage,
            budgetedClient.Snapshot.UsedEstimatedUsage);
    }

    [Fact]
    public async Task RecoveryDoesNotReplayAStreamAfterContentWasPublished()
    {
        var provider = new RecoveringChatClient(
            connectionFailuresBeforeSuccess: 0,
            failAfterStreamingContent: true);
        var recoveries = new List<CopilotProviderConnectionRecoveryInfo>();
        using var client = new CopilotProviderConnectionRecoveryChatClient(
            provider,
            recoveries.Add,
            (_, _) => Task.CompletedTask);
        var received = new List<string>();

        var exception = await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "stream")],
                cancellationToken: CancellationToken.None))
            {
                received.Add(update.Text);
            }
        });

        Assert.Contains("connection dropped", exception.Message, StringComparison.Ordinal);
        Assert.Equal(["partial"], received);
        Assert.Equal(1, provider.CallCount);
        Assert.Empty(recoveries);
    }

    [Fact]
    public async Task StreamingRecoveryRetriesOnlyBeforeTheFirstPublishedContent()
    {
        var provider = new RecoveringChatClient(connectionFailuresBeforeSuccess: 2);
        var recoveries = new List<CopilotProviderConnectionRecoveryInfo>();
        var delays = new List<TimeSpan>();
        using var client = new CopilotProviderConnectionRecoveryChatClient(
            provider,
            recoveries.Add,
            (delay, _) =>
            {
                delays.Add(delay);
                return Task.CompletedTask;
            });
        var received = new List<string>();

        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "stream")],
            cancellationToken: CancellationToken.None))
        {
            received.Add(update.Text);
        }

        Assert.Equal(["partial"], received);
        Assert.Equal(3, provider.CallCount);
        Assert.Equal([1, 2], recoveries.Select(item => item.RecoveryAttempt));
        Assert.Equal(
            [TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(10)],
            delays);
    }

    [Fact]
    public async Task TimeoutStillUsesTheExistingBoundedRetryPath()
    {
        var provider = new RecoveringChatClient(
            connectionFailuresBeforeSuccess: 0,
            nonConnectionFailure: new TimeoutException("provider timeout"));
        var recoveries = new List<CopilotProviderConnectionRecoveryInfo>();
        using var client = new CopilotProviderConnectionRecoveryChatClient(
            provider,
            recoveries.Add,
            (_, _) => Task.CompletedTask);

        var exception = await Assert.ThrowsAsync<TimeoutException>(() =>
            client.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "timeout")],
                cancellationToken: CancellationToken.None));

        Assert.Equal("provider timeout", exception.Message);
        Assert.Equal(1, provider.CallCount);
        Assert.Empty(recoveries);
    }

    [Fact]
    public async Task CancellingTheOfflineWaitDoesNotEstimateUnsentTokenUsage()
    {
        var provider = new RecoveringChatClient(connectionFailuresBeforeSuccess: int.MaxValue);
        using var cancellation = new CancellationTokenSource();
        using var recoveryClient = new CopilotProviderConnectionRecoveryChatClient(
            provider,
            onRecovery: _ => cancellation.Cancel(),
            delayAsync: (_, token) => Task.FromCanceled(token));
        using var budgetedClient = new CopilotTokenBudgetChatClient(
            recoveryClient,
            CreateBudget());

        var exception = await Assert.ThrowsAsync<CopilotProviderConnectionRecoveryCancelledException>(() =>
            budgetedClient.GetResponseAsync(
                [new ChatMessage(ChatRole.User, "wait for network")],
                cancellationToken: cancellation.Token));

        Assert.True(exception.CancellationToken.IsCancellationRequested);
        Assert.Equal(1, provider.CallCount);
        Assert.Equal(1, budgetedClient.Snapshot.ProviderCalls);
        Assert.Equal(0, budgetedClient.Snapshot.ConsumedTokens);
        Assert.False(budgetedClient.Snapshot.UsedEstimatedUsage);
    }

    private static CopilotAgentTokenBudget CreateBudget() => new()
    {
        ContextWindowTokens = CopilotAgentTokenBudget.MinimumContextWindowTokens,
        MaxOutputTokens = 128,
        RequestTokenBudget = 8_192,
    };

    private sealed class RecoveringChatClient(
        int connectionFailuresBeforeSuccess,
        bool failAfterStreamingContent = false,
        Exception? nonConnectionFailure = null) : IChatClient
    {
        public int CallCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (nonConnectionFailure != null)
                return Task.FromException<ChatResponse>(nonConnectionFailure);
            if (CallCount <= connectionFailuresBeforeSuccess)
            {
                return Task.FromException<ChatResponse>(
                    new HttpRequestException("provider connection unavailable"));
            }

            return Task.FromResult(new ChatResponse(
                new ChatMessage(ChatRole.Assistant, "recovered"))
            {
                FinishReason = ChatFinishReason.Stop,
            });
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            CallCount++;
            await Task.Yield();
            cancellationToken.ThrowIfCancellationRequested();
            if (CallCount <= connectionFailuresBeforeSuccess)
                throw new HttpRequestException("provider connection unavailable");

            yield return new ChatResponseUpdate(ChatRole.Assistant, "partial");
            if (failAfterStreamingContent)
                throw new HttpRequestException("connection dropped after content");
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;

        public void Dispose()
        {
        }
    }
}
