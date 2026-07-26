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

    [Fact]
    public async Task BoundedBufferCoalescesStreamingBurstWithoutChangingTextOrder()
    {
        var buffer = new CopilotTurnEventBuffer(maximumPendingEvents: 4);
        var expectedResult = CreateResult();

        Assert.True(buffer.TryWrite(
            new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", true))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "A"))));
        foreach (var text in new[] { "B", "C", "D", "E" })
        {
            Assert.True(buffer.TryWrite(
                new CopilotTurnChatDeltaEvent(
                    new CopilotStreamDelta(string.Empty, text))));
        }
        Assert.True(buffer.TryWrite(
            new CopilotTurnProviderRetryEvent(
                new CopilotProviderRetryInfo(
                    1,
                    2,
                    3,
                    TimeSpan.Zero,
                    "connection failure",
                    null))));
        Assert.True(buffer.TryWrite(new CopilotTurnCompletedEvent(expectedResult)));
        Assert.True(buffer.TryComplete());

        var events = await DrainAsync(buffer);

        Assert.Equal(4, events.Count);
        Assert.Equal("prepared", Assert.IsType<CopilotTurnRequestPreparedEvent>(events[0]).Request.Content);
        Assert.Equal("ABCDE", Assert.IsType<CopilotTurnChatDeltaEvent>(events[1]).Delta.Content);
        Assert.IsType<CopilotTurnProviderRetryEvent>(events[2]);
        Assert.Same(expectedResult, Assert.IsType<CopilotTurnCompletedEvent>(events[3]).Result);
    }

    [Fact]
    public async Task TurnStreamKeepsLargeStreamingBurstLosslessWithSmallBacklog()
    {
        const int chunkCount = 20_000;
        var expectedResult = CreateResult();
        var events = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in CopilotTurnEventStream.RunAsync(
            (sink, _) =>
            {
                sink.OnRequestPrepared(
                    new CopilotPreparedTurnRequest("prepared", true));
                for (var index = 0; index < chunkCount; index++)
                {
                    sink.OnChatDelta(
                        new CopilotStreamDelta(
                            string.Empty,
                            ((char)('0' + index % 10)).ToString()));
                }
                return Task.FromResult(expectedResult);
            },
            CancellationToken.None,
            maximumPendingEvents: 4))
        {
            events.Add(turnEvent);
        }

        var streamedText = string.Concat(
            events
                .OfType<CopilotTurnChatDeltaEvent>()
                .Select(item => item.Delta.Content));
        Assert.Equal(chunkCount, streamedText.Length);
        for (var index = 0; index < chunkCount; index++)
            Assert.Equal((char)('0' + index % 10), streamedText[index]);
        Assert.Single(events.OfType<CopilotTurnCompletedEvent>());
    }

    [Fact]
    public async Task BoundedBufferPreservesReasoningToAnswerBoundary()
    {
        var buffer = new CopilotTurnEventBuffer(maximumPendingEvents: 4);
        var expectedResult = CreateResult();

        Assert.True(buffer.TryWrite(
            new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", true))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.ReasoningDelta("reason-1"))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.ReasoningDelta("+reason-2"))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("answer-1"))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("+answer-2"))));
        Assert.True(buffer.TryWrite(new CopilotTurnCompletedEvent(expectedResult)));
        Assert.True(buffer.TryComplete());

        var events = await DrainAsync(buffer);

        Assert.Collection(
            events,
            item => Assert.IsType<CopilotTurnRequestPreparedEvent>(item),
            item =>
            {
                var agentEvent = Assert.IsType<CopilotTurnAgentEvent>(item).Event;
                Assert.Equal(CopilotAgentEventType.ReasoningDelta, agentEvent.Type);
                Assert.Equal("reason-1+reason-2", agentEvent.Text);
            },
            item =>
            {
                var agentEvent = Assert.IsType<CopilotTurnAgentEvent>(item).Event;
                Assert.Equal(CopilotAgentEventType.AnswerDelta, agentEvent.Type);
                Assert.Equal("answer-1+answer-2", agentEvent.Text);
            },
            item => Assert.Same(expectedResult, Assert.IsType<CopilotTurnCompletedEvent>(item).Result));
    }

    [Fact]
    public async Task BoundedBufferWakesReaderAcrossSeparateIdlePeriods()
    {
        var buffer = new CopilotTurnEventBuffer(maximumPendingEvents: 4);
        await using var reader = buffer.ReadAllAsync().GetAsyncEnumerator();

        var firstMove = reader.MoveNextAsync().AsTask();
        Assert.False(firstMove.IsCompleted);
        Assert.True(buffer.TryWrite(
            new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "first"))));
        Assert.True(await firstMove.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(
            "first",
            Assert.IsType<CopilotTurnChatDeltaEvent>(reader.Current).Delta.Content);

        var secondMove = reader.MoveNextAsync().AsTask();
        Assert.False(secondMove.IsCompleted);
        Assert.True(buffer.TryWrite(
            new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(string.Empty, "second"))));
        Assert.True(await secondMove.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Equal(
            "second",
            Assert.IsType<CopilotTurnChatDeltaEvent>(reader.Current).Delta.Content);

        var completionMove = reader.MoveNextAsync().AsTask();
        Assert.False(completionMove.IsCompleted);
        Assert.True(buffer.TryComplete());
        Assert.False(await completionMove.WaitAsync(TimeSpan.FromSeconds(2)));
    }

    [Fact]
    public async Task BoundedBufferFailsExplicitlyBeforeNonStreamingEventsCanGrowWithoutLimit()
    {
        var buffer = new CopilotTurnEventBuffer(maximumPendingEvents: 2);
        Assert.True(buffer.TryWrite(
            new CopilotTurnRequestPreparedEvent(
                new CopilotPreparedTurnRequest("prepared", true))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnProviderRetryEvent(
                new CopilotProviderRetryInfo(
                    1,
                    2,
                    3,
                    TimeSpan.Zero,
                    "connection failure",
                    null))));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            buffer.TryWrite(
                new CopilotTurnAgentEvent(
                    CopilotAgentEvent.RuntimeDiagnostic("must not be dropped"))));

        Assert.Contains("2-event safety limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(2, buffer.PendingCount);
        Assert.True(buffer.TryComplete());
        Assert.Equal(2, (await DrainAsync(buffer)).Count);
    }

    private static async Task<List<CopilotTurnEvent>> DrainAsync(
        CopilotTurnEventBuffer buffer)
    {
        var events = new List<CopilotTurnEvent>();
        await foreach (var turnEvent in buffer.ReadAllAsync())
            events.Add(turnEvent);
        return events;
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
