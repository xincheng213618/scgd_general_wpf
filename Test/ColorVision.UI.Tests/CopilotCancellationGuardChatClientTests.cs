using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Runtime.CompilerServices;

namespace ColorVision.UI.Tests;

public sealed class CopilotCancellationGuardChatClientTests
{
    [Fact]
    public async Task NonStreamingCallReturnsCancellationWhenProviderIgnoresToken()
    {
        using var provider = new IgnoringCancellationChatClient();
        using var client = new CopilotCancellationGuardChatClient(
            provider,
            TimeSpan.FromMilliseconds(50));
        using var cancellation = new CancellationTokenSource();

        var responseTask = client.GetResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: cancellation.Token);
        await provider.ResponseStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => responseTask.WaitAsync(TimeSpan.FromSeconds(1)));

        provider.ReleaseResponse.TrySetResult();
        await provider.ResponseFinished.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StreamingCallReturnsCancellationAndDisposesAfterLateMove()
    {
        using var provider = new IgnoringCancellationChatClient();
        using var client = new CopilotCancellationGuardChatClient(
            provider,
            TimeSpan.FromMilliseconds(50));
        using var cancellation = new CancellationTokenSource();
        var updates = new List<ChatResponseUpdate>();

        var enumeration = Task.Run(async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "test")],
                cancellationToken: cancellation.Token))
            {
                updates.Add(update);
            }
        });
        await provider.StreamStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumeration.WaitAsync(TimeSpan.FromSeconds(1)));
        Assert.Empty(updates);

        provider.ReleaseStream.TrySetResult();
        await provider.StreamDisposed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StreamingCallPreservesNormalUpdates()
    {
        using var provider = new IgnoringCancellationChatClient();
        using var client = new CopilotCancellationGuardChatClient(provider);
        provider.ReleaseStream.TrySetResult();
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "test")],
            cancellationToken: CancellationToken.None))
        {
            updates.Add(update);
        }

        Assert.Equal("late update", Assert.Single(updates).Text);
        await provider.StreamDisposed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task StreamingCallDoesNotWaitIndefinitelyForProviderDisposal()
    {
        using var provider = new IgnoringCancellationChatClient
        {
            DelayStreamDisposal = true,
        };
        using var client = new CopilotCancellationGuardChatClient(
            provider,
            TimeSpan.FromMilliseconds(50));
        provider.ReleaseStream.TrySetResult();

        var enumeration = Task.Run(async () =>
        {
            await foreach (var _ in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "test")],
                cancellationToken: CancellationToken.None))
            {
                break;
            }
        });
        await provider.StreamDisposalStarted.Task.WaitAsync(TimeSpan.FromSeconds(1));

        await enumeration.WaitAsync(TimeSpan.FromSeconds(1));
        Assert.False(provider.StreamDisposed.Task.IsCompleted);

        provider.ReleaseStreamDisposal.TrySetResult();
        await provider.StreamDisposed.Task.WaitAsync(TimeSpan.FromSeconds(1));
    }

    private sealed class IgnoringCancellationChatClient : IChatClient
    {
        public bool DelayStreamDisposal { get; init; }

        public TaskCompletionSource ResponseStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseResponse { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ResponseFinished { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource StreamStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStream { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource StreamDisposed { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource StreamDisposalStarted { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseStreamDisposal { get; } = new(
            TaskCreationOptions.RunContinuationsAsynchronously);

        public async Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            CancellationToken cancellationToken = default)
        {
            ResponseStarted.TrySetResult();
            await ReleaseResponse.Task;
            ResponseFinished.TrySetResult();
            return new ChatResponse(new ChatMessage(ChatRole.Assistant, "late response"));
        }

        public async IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages,
            ChatOptions? options = null,
            [EnumeratorCancellation] CancellationToken cancellationToken = default)
        {
            try
            {
                StreamStarted.TrySetResult();
                await ReleaseStream.Task;
                yield return new ChatResponseUpdate(ChatRole.Assistant, "late update");
            }
            finally
            {
                StreamDisposalStarted.TrySetResult();
                if (DelayStreamDisposal)
                    await ReleaseStreamDisposal.Task;
                StreamDisposed.TrySetResult();
            }
        }

        public object? GetService(Type serviceType, object? serviceKey = null) =>
            serviceType.IsInstanceOfType(this) ? this : null;

        public void Dispose()
        {
        }
    }
}
