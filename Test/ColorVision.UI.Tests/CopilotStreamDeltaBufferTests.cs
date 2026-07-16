#pragma warning disable CA1707
using ColorVision.Copilot;
using System.Collections.Concurrent;

namespace ColorVision.UI.Tests;

public sealed class CopilotStreamDeltaBufferTests
{
    [Fact]
    public async Task Buffer_CoalescesAdjacentShapesAndPreservesTransitions()
    {
        var context = new QueuedSynchronizationContext();
        var batches = new List<IReadOnlyList<CopilotStreamDelta>>();
        var buffer = new CopilotStreamDeltaBuffer(context, batch => batches.Add(batch), isOnTargetThread: () => context.IsExecuting);

        await Task.Run(() =>
        {
            buffer.Enqueue(new CopilotStreamDelta("reason-1", string.Empty));
            buffer.Enqueue(new CopilotStreamDelta("reason-2", string.Empty));
            buffer.Enqueue(new CopilotStreamDelta(string.Empty, "answer-1"));
            buffer.Enqueue(new CopilotStreamDelta(string.Empty, "answer-2"));
            buffer.Enqueue(new CopilotStreamDelta("reason-3", "answer-3"));
            buffer.Enqueue(new CopilotStreamDelta("reason-4", "answer-4"));
        });

        Assert.Empty(batches);
        Assert.Equal(1, context.PendingCount);
        context.Drain();
        var batch = Assert.Single(batches);
        Assert.Collection(
            batch,
            delta => Assert.Equal(new CopilotStreamDelta("reason-1reason-2", string.Empty), delta),
            delta => Assert.Equal(new CopilotStreamDelta(string.Empty, "answer-1answer-2"), delta),
            delta => Assert.Equal(new CopilotStreamDelta("reason-3", "answer-3"), delta),
            delta => Assert.Equal(new CopilotStreamDelta("reason-4", "answer-4"), delta));
        await CompleteAsync(buffer, context);
    }

    [Fact]
    public async Task Buffer_CompleteFlushesPendingTailExactlyOnce()
    {
        var context = new QueuedSynchronizationContext();
        var applied = new List<CopilotStreamDelta>();
        var buffer = new CopilotStreamDeltaBuffer(context, batch => applied.AddRange(batch), isOnTargetThread: () => context.IsExecuting);

        await Task.Run(() => buffer.Enqueue(new CopilotStreamDelta(string.Empty, "tail")));
        var completion = buffer.CompleteAsync();
        context.Drain();
        await completion;

        Assert.Equal(new[] { new CopilotStreamDelta(string.Empty, "tail") }, applied);
        Assert.Throws<InvalidOperationException>(() => buffer.Enqueue(new CopilotStreamDelta(string.Empty, "late")));
    }

    [Fact]
    public async Task Buffer_AppliesBackpressureWhenPendingTextReachesLimit()
    {
        var context = new QueuedSynchronizationContext();
        var applied = new List<CopilotStreamDelta>();
        var buffer = new CopilotStreamDeltaBuffer(
            context,
            batch => applied.AddRange(batch),
            maximumPendingCharacters: 4,
            maximumPendingSegments: 8,
            isOnTargetThread: () => context.IsExecuting);

        await Task.Run(() => buffer.Enqueue(new CopilotStreamDelta(string.Empty, "bounded")));

        Assert.Equal(new[] { new CopilotStreamDelta(string.Empty, "bounded") }, applied);
        await CompleteAsync(buffer, context);
        context.Drain();
        Assert.Single(applied);
    }

    [Fact]
    public async Task Buffer_CompletionSurfacesAsynchronousApplyFailure()
    {
        var context = new QueuedSynchronizationContext();
        var buffer = new CopilotStreamDeltaBuffer(
            context,
            _ => throw new InvalidOperationException("apply failed"),
            isOnTargetThread: () => context.IsExecuting);

        await Task.Run(() => buffer.Enqueue(new CopilotStreamDelta(string.Empty, "answer")));
        context.Drain();

        var completion = buffer.CompleteAsync();
        context.Drain();
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => completion);
        Assert.Equal("apply failed", error.InnerException?.Message);
    }

    private static async Task CompleteAsync(CopilotStreamDeltaBuffer buffer, QueuedSynchronizationContext context)
    {
        var completion = buffer.CompleteAsync();
        context.Drain();
        await completion;
    }

    private sealed class QueuedSynchronizationContext : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _callbacks = new();

        public bool IsExecuting { get; private set; }

        public int PendingCount => _callbacks.Count;

        public override void Post(SendOrPostCallback d, object? state) => _callbacks.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state) => d(state);

        public void Drain()
        {
            var previous = Current;
            SetSynchronizationContext(this);
            IsExecuting = true;
            try
            {
                while (_callbacks.TryDequeue(out var workItem))
                    workItem.Callback(workItem.State);
            }
            finally
            {
                IsExecuting = false;
                SetSynchronizationContext(previous);
            }
        }
    }
}
