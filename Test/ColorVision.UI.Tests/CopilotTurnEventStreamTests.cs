using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotTurnEventStreamTests
{
    [Fact]
    public async Task PublishesProgressInOrderThenExactlyOneCompletion()
    {
        var expectedResult = CreateResult();
        var events = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(
            (sink, _) =>
            {
                sink.OnRequestPrepared(new CopilotPreparedTurnRequest("prepared", true));
                sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "partial"));
                return Task.FromResult(expectedResult);
            },
            CancellationToken.None))
        {
            events.Add(turnEvent);
        }

        Assert.Collection(
            events,
            item => Assert.Equal("prepared", Assert.IsType<CopilotTurnRequestPreparedEvent>(item).Request.Content),
            item => Assert.Equal("partial", Assert.IsType<CopilotTurnChatDeltaEvent>(item).Delta.Content),
            item => Assert.Same(expectedResult, Assert.IsType<CopilotTurnCompletedEvent>(item).Result));
        Assert.Single(events.OfType<CopilotTurnCompletedEvent>());
    }

    [Fact]
    public async Task ProducerFailureFaultsAfterProgressWithoutEmittingCompletion()
    {
        var events = new List<CopilotTurnEvent>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(
                (sink, _) =>
                {
                    sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "partial"));
                    throw new InvalidOperationException("provider failed");
                },
                CancellationToken.None))
            {
                events.Add(turnEvent);
            }
        });

        Assert.Equal("provider failed", exception.Message);
        Assert.IsType<CopilotTurnChatDeltaEvent>(Assert.Single(events));
        Assert.Empty(events.OfType<CopilotTurnCompletedEvent>());
    }

    [Fact]
    public async Task CancellingStreamCancelsProducerWithoutEmittingCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var producerCancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<CopilotTurnEvent>();

        var enumeration = Task.Run(async () =>
        {
            await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(
                async (sink, cancellationToken) =>
                {
                    sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "partial"));
                    try
                    {
                        await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    }
                    catch (OperationCanceledException)
                    {
                        producerCancelled.TrySetResult();
                        throw;
                    }

                    return CreateResult();
                },
                cancellation.Token))
            {
                events.Add(turnEvent);
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumeration);
        await producerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<CopilotTurnChatDeltaEvent>(Assert.Single(events));
        Assert.Empty(events.OfType<CopilotTurnCompletedEvent>());
    }

    [Fact]
    public async Task DisposingEnumeratorEarlyCancelsProducer()
    {
        var producerCancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = CopilotTurnEventStream.RunAsync(
            async (sink, cancellationToken) =>
            {
                sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "partial"));
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    producerCancelled.TrySetResult();
                    throw;
                }

                return CreateResult();
            },
            CancellationToken.None);

        await using (var enumerator = stream.GetAsyncEnumerator())
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.IsType<CopilotTurnChatDeltaEvent>(enumerator.Current);
        }

        await producerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GracefulCancellationDrainsFinalEventsAndCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var expectedResult = CreateResult();
        var events = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(
            async (sink, cancellationToken) =>
            {
                sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "partial"));
                try
                {
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "finalized"));
                }

                return expectedResult;
            },
            cancellation.Token))
        {
            events.Add(turnEvent);
            if (events.Count == 1)
                cancellation.Cancel();
        }

        Assert.Collection(
            events,
            item => Assert.Equal("partial", Assert.IsType<CopilotTurnChatDeltaEvent>(item).Delta.Content),
            item => Assert.Equal("finalized", Assert.IsType<CopilotTurnChatDeltaEvent>(item).Delta.Content),
            item => Assert.Same(expectedResult, Assert.IsType<CopilotTurnCompletedEvent>(item).Result));
    }

    [Fact]
    public async Task CancellationDeadlineReleasesReaderWhenProducerIgnoresCancellation()
    {
        using var cancellation = new CancellationTokenSource();
        var releaseProducer = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var producerFinished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<CopilotTurnEvent>();

        var enumeration = Task.Run(async () =>
        {
            await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(
                async (sink, _) =>
                {
                    try
                    {
                        sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "partial"));
                        await releaseProducer.Task;
                        return CreateResult();
                    }
                    finally
                    {
                        producerFinished.TrySetResult();
                    }
                },
                cancellation.Token,
                TimeSpan.FromMilliseconds(50)))
            {
                events.Add(turnEvent);
                cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumeration.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.IsType<CopilotTurnChatDeltaEvent>(Assert.Single(events));
        Assert.Empty(events.OfType<CopilotTurnCompletedEvent>());

        releaseProducer.TrySetResult();
        await producerFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task ProducerSynchronousPrefixDoesNotBlockEnumeratorThread()
    {
        using var producerStarted = new ManualResetEventSlim();
        using var releaseProducer = new ManualResetEventSlim();
        var enumeratorThreadId = 0;
        var producerThreadId = 0;
        var stream = CopilotTurnEventStream.RunAsync(
            (_, _) =>
            {
                producerThreadId = Environment.CurrentManagedThreadId;
                producerStarted.Set();
                releaseProducer.Wait(CancellationToken.None);
                return Task.FromResult(CreateResult());
            },
            CancellationToken.None);
        await using var enumerator = stream.GetAsyncEnumerator();
        var invocation = Task.Factory.StartNew(
            () =>
            {
                enumeratorThreadId = Environment.CurrentManagedThreadId;
                return enumerator.MoveNextAsync().AsTask();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
        Task<bool>? pendingMove = null;

        try
        {
            Assert.True(producerStarted.Wait(TimeSpan.FromSeconds(1)));
            pendingMove = await invocation.WaitAsync(TimeSpan.FromSeconds(1));

            Assert.NotEqual(enumeratorThreadId, producerThreadId);
            Assert.False(pendingMove.IsCompleted);
        }
        finally
        {
            releaseProducer.Set();
            if (pendingMove == null)
            {
                try
                {
                    pendingMove = await invocation.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                }
            }

            if (pendingMove != null)
            {
                try
                {
                    _ = await pendingMove.WaitAsync(TimeSpan.FromSeconds(2));
                }
                catch
                {
                }
            }
        }
    }

    private static CopilotTurnResult CreateResult()
    {
        var usage = new CopilotTokenUsage(4, 2, 6);
        return CopilotTurnResult.FromChat(
            usage,
            "prepared",
            chatAttachmentContextCaptured: true,
            new CopilotChatStreamResult(usage, CopilotChatFinishKind.Complete, "stop"));
    }
}
