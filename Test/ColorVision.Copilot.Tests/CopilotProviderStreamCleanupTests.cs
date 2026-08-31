using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.IO;
using System.Net;
using System.Net.Http;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderStreamCleanupTests
{
    [Theory]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, false)]
    [InlineData(false, false, true)]
    [InlineData(false, true, true)]
    [InlineData(true, false, true)]
    [InlineData(true, true, true)]
    public async Task CleanupFailureDoesNotReplaceAuthenticationFailureOrStartRetries(bool partialText, bool synchronousCleanup, bool synchronousMove)
    {
        var original = new HttpRequestException("Controlled authentication failure.", null, HttpStatusCode.Unauthorized);
        CopilotProviderRequestId.Preserve(original, "req-original-failure");
        var source = new FailingStreamClient(original, new IOException("Controlled cleanup failure."), partialText, synchronousCleanup, synchronousMove);
        var retries = new List<CopilotProviderRetryInfo>();
        using var client = new CopilotProviderRetryChatClient(
            new CopilotCancellationGuardChatClient(source),
            retries.Add,
            delayAsync: (_, _) => Task.CompletedTask);
        var updates = new List<ChatResponseUpdate>();

        var error = await Record.ExceptionAsync(async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Summarize the result.")]))
                updates.Add(update);
        });

        Assert.Same(original, error);
        Assert.Equal("req-original-failure", CopilotProviderRequestId.Find(error!));
        Assert.Equal(1, source.CallCount);
        Assert.Equal(1, source.DisposalCount);
        Assert.Empty(retries);
        Assert.Equal(partialText ? ["Partial answer."] : Array.Empty<string>(), updates.Select(update => update.Text).ToArray());
    }

    [Fact]
    public async Task CancellationBetweenUpdatesIsNotReplacedByCleanupFailure()
    {
        using var cancellation = new CancellationTokenSource();
        var source = new FailingStreamClient(null, new IOException("Controlled cleanup failure."), partialText: true, synchronousCleanup: false);
        using var client = new CopilotCancellationGuardChatClient(source);

        var error = await Record.ExceptionAsync(async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync(
                [new ChatMessage(ChatRole.User, "Summarize the result.")], cancellationToken: cancellation.Token))
            {
                Assert.Equal("Partial answer.", update.Text);
                cancellation.Cancel();
            }
        });

        var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(error);
        Assert.Equal(cancellation.Token, cancelled.CancellationToken);
        Assert.Equal(1, source.DisposalCount);
    }

    [Fact]
    public async Task CleanupFailureWithoutAnEarlierFailureStillSurfaces()
    {
        var cleanupFailure = new IOException("Controlled cleanup failure.");
        var source = new FailingStreamClient(null, cleanupFailure, partialText: true, synchronousCleanup: false);
        using var client = new CopilotCancellationGuardChatClient(source);
        var updates = new List<ChatResponseUpdate>();

        var error = await Record.ExceptionAsync(async () =>
        {
            await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Summarize the result.")]))
                updates.Add(update);
        });

        Assert.Same(cleanupFailure, error);
        Assert.Equal("Partial answer.", Assert.Single(updates).Text);
        Assert.Equal(1, source.DisposalCount);
    }

    [Fact]
    public async Task SuccessfulStreamIsStillDisposedExactlyOnce()
    {
        var source = new FailingStreamClient(null, null, partialText: true, synchronousCleanup: false);
        using var client = new CopilotCancellationGuardChatClient(source);
        var updates = new List<ChatResponseUpdate>();

        await foreach (var update in client.GetStreamingResponseAsync([new ChatMessage(ChatRole.User, "Summarize the result.")]))
            updates.Add(update);

        Assert.Equal("Partial answer.", Assert.Single(updates).Text);
        Assert.Equal(1, source.CallCount);
        Assert.Equal(1, source.DisposalCount);
    }

    private sealed class FailingStreamClient(
        Exception? primaryFailure,
        Exception? cleanupFailure,
        bool partialText,
        bool synchronousCleanup,
        bool synchronousMove = false) : IChatClient
    {
        public int CallCount { get; private set; }
        public int DisposalCount { get; private set; }

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return new Stream(this);
        }

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }

        private sealed class Stream(FailingStreamClient owner) : IAsyncEnumerable<ChatResponseUpdate>, IAsyncEnumerator<ChatResponseUpdate>
        {
            private bool _started;
            public ChatResponseUpdate Current { get; } = new(ChatRole.Assistant, "Partial answer.");
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

            public ValueTask<bool> MoveNextAsync()
            {
                if (!_started)
                {
                    _started = true;
                    if (owner.HasPartialText)
                        return ValueTask.FromResult(true);
                }
                if (owner.PrimaryFailure != null && owner.SynchronousMove)
                    throw owner.PrimaryFailure;
                return owner.PrimaryFailure == null
                    ? ValueTask.FromResult(false)
                    : ValueTask.FromException<bool>(owner.PrimaryFailure);
            }

            public ValueTask DisposeAsync()
            {
                owner.DisposalCount++;
                if (owner.CleanupFailure == null)
                    return ValueTask.CompletedTask;
                if (owner.SynchronousCleanup)
                    throw owner.CleanupFailure;
                return ValueTask.FromException(owner.CleanupFailure);
            }
        }

        private Exception? PrimaryFailure => primaryFailure;
        private Exception? CleanupFailure => cleanupFailure;
        private bool HasPartialText => partialText;
        private bool SynchronousCleanup => synchronousCleanup;
        private bool SynchronousMove => synchronousMove;
    }
}
