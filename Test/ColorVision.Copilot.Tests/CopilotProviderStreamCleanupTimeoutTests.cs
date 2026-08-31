using ColorVision.Copilot;
using Microsoft.Extensions.AI;
using System.Diagnostics;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotProviderStreamCleanupTimeoutTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CleanupTimeoutExceptionIsNotMistakenForTheHostCleanupBudget(bool synchronousCleanup)
    {
        var original = new TimeoutException("The controlled provider cleanup failed.");
        var source = new ControlledStreamClient(original, synchronousCleanup: synchronousCleanup);
        using var client = new CopilotCancellationGuardChatClient(source);

        var error = await Record.ExceptionAsync(() => ConsumeAsync(client).WaitAsync(TestTimeout));

        Assert.Same(original, error);
        Assert.Equal(1, source.DisposalCount);
        Assert.True(source.DisposalCompleted.Task.IsCompletedSuccessfully);
    }

    [Fact]
    public async Task PendingCleanupReturnsWithinItsBudgetAndCanFinishAfterTheCallerReturns()
    {
        var source = new ControlledStreamClient(gateDisposal: true);
        using var client = new CopilotCancellationGuardChatClient(source, TimeSpan.FromMilliseconds(30));
        var consumption = ConsumeAsync(client);
        try
        {
            await source.DisposalStarted.Task.WaitAsync(TestTimeout);
            var updates = await consumption.WaitAsync(TestTimeout);

            Assert.Equal("Controlled answer.", Assert.Single(updates).Text);
            Assert.Equal(1, source.DisposalCount);
            Assert.False(source.ReleaseDisposal.Task.IsCompleted);
            Assert.False(source.DisposalCompleted.Task.IsCompleted);
        }
        finally
        {
            source.ReleaseDisposal.TrySetResult();
            await source.DisposalCompleted.Task.WaitAsync(TestTimeout);
            await consumption.WaitAsync(TestTimeout);
        }

        Assert.Equal(1, source.DisposalCount);
    }

    [Fact]
    public async Task CancelledPendingMoveFinishesBeforeDeferredCleanupAndItsCleanupFaultIsObserved()
    {
        using var cancellation = new CancellationTokenSource();
        using var listener = new CleanupFailureTraceListener();
        Trace.Listeners.Add(listener);
        var source = new ControlledStreamClient(new DeferredCleanupTestException(), gateMove: true, gateDisposal: true);
        using var client = new CopilotCancellationGuardChatClient(source);
        var consumption = ConsumeAsync(client, cancellation.Token);
        try
        {
            await source.MoveStarted.Task.WaitAsync(TestTimeout);
            cancellation.Cancel();
            var error = await Record.ExceptionAsync(() => consumption.WaitAsync(TestTimeout));

            var cancelled = Assert.IsAssignableFrom<OperationCanceledException>(error);
            Assert.Equal(cancellation.Token, cancelled.CancellationToken);
            Assert.False(source.ReleaseMove.Task.IsCompleted);
            Assert.Equal(0, source.DisposalCount);
            Assert.False(source.DisposalStarted.Task.IsCompleted);

            source.ReleaseMove.TrySetResult(false);
            await source.DisposalStarted.Task.WaitAsync(TestTimeout);
            Assert.Equal(1, source.DisposalCount);
            Assert.False(source.DisposalCompleted.Task.IsCompleted);

            source.ReleaseDisposal.TrySetResult();
            await listener.FailureObserved.Task.WaitAsync(TestTimeout);
            Assert.True(source.DisposalCompleted.Task.IsCompletedSuccessfully);
            Assert.Equal(1, source.DisposalCount);
        }
        finally
        {
            cancellation.Cancel();
            source.ReleaseMove.TrySetResult(false);
            source.ReleaseDisposal.TrySetResult();
            try
            {
                await source.DisposalCompleted.Task.WaitAsync(TestTimeout);
                await Record.ExceptionAsync(() => consumption.WaitAsync(TestTimeout));
            }
            finally
            {
                Trace.Listeners.Remove(listener);
            }
        }
    }

    private static async Task<List<ChatResponseUpdate>> ConsumeAsync(IChatClient client, CancellationToken cancellationToken = default)
    {
        var updates = new List<ChatResponseUpdate>();
        await foreach (var update in client.GetStreamingResponseAsync(
            [new ChatMessage(ChatRole.User, "Summarize the controlled result.")], cancellationToken: cancellationToken))
        {
            updates.Add(update);
        }
        return updates;
    }

    private sealed class ControlledStreamClient(
        Exception? cleanupFailure = null,
        bool synchronousCleanup = false,
        bool gateMove = false,
        bool gateDisposal = false) : IChatClient
    {
        private int _disposalCount;
        public int DisposalCount => Volatile.Read(ref _disposalCount);
        public TaskCompletionSource MoveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<bool> ReleaseMove { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposalStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource ReleaseDisposal { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource DisposalCompleted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<ChatResponse> GetResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public IAsyncEnumerable<ChatResponseUpdate> GetStreamingResponseAsync(
            IEnumerable<ChatMessage> messages, ChatOptions? options = null, CancellationToken cancellationToken = default)
            => new ControlledStream(this);

        public object? GetService(Type serviceType, object? serviceKey = null) => null;
        public void Dispose() { }

        private sealed class ControlledStream(ControlledStreamClient owner) : IAsyncEnumerable<ChatResponseUpdate>, IAsyncEnumerator<ChatResponseUpdate>
        {
            private bool _yielded;
            public ChatResponseUpdate Current { get; } = new(ChatRole.Assistant, "Controlled answer.");
            public IAsyncEnumerator<ChatResponseUpdate> GetAsyncEnumerator(CancellationToken cancellationToken = default) => this;

            public ValueTask<bool> MoveNextAsync()
            {
                if (owner.GateMove)
                {
                    owner.MoveStarted.TrySetResult();
                    return new ValueTask<bool>(owner.ReleaseMove.Task);
                }
                var hasNext = !_yielded;
                _yielded = true;
                return ValueTask.FromResult(hasNext);
            }

            public ValueTask DisposeAsync()
            {
                Interlocked.Increment(ref owner._disposalCount);
                owner.DisposalStarted.TrySetResult();
                if (owner.SynchronousCleanup && owner.CleanupFailure != null)
                {
                    owner.DisposalCompleted.TrySetResult();
                    throw owner.CleanupFailure;
                }
                return new ValueTask(DisposeCoreAsync());
            }

            private async Task DisposeCoreAsync()
            {
                try
                {
                    if (owner.GateDisposal)
                        await owner.ReleaseDisposal.Task.ConfigureAwait(false);
                    if (owner.CleanupFailure != null)
                        throw owner.CleanupFailure;
                }
                finally
                {
                    owner.DisposalCompleted.TrySetResult();
                }
            }
        }

        private Exception? CleanupFailure => cleanupFailure;
        private bool SynchronousCleanup => synchronousCleanup;
        private bool GateMove => gateMove;
        private bool GateDisposal => gateDisposal;
    }

    private sealed class DeferredCleanupTestException : Exception
    {
        public DeferredCleanupTestException() : base(nameof(DeferredCleanupTestException)) { }
    }

    private sealed class CleanupFailureTraceListener : TraceListener
    {
        public TaskCompletionSource FailureObserved { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override void Write(string? message)
        {
            if (message?.Contains(nameof(DeferredCleanupTestException), StringComparison.Ordinal) == true)
                FailureObserved.TrySetResult();
        }

        public override void WriteLine(string? message) => Write(message);
    }
}
