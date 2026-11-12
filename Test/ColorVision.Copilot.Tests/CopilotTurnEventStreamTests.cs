namespace ColorVision.Copilot.Tests;

public sealed class CopilotTurnEventStreamTests
{
    private const string TurnId = "turn:event-stream-test";

    [Fact]
    public async Task PublishesProgressInOrderThenExactlyOneCompletion()
    {
        var expectedResult = CreateResult();
        var events = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in RunTurnAsync(
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
            item => Assert.Equal(TurnId, Assert.IsType<CopilotTurnStartedEvent>(item).TurnId),
            item => Assert.Equal("prepared", Assert.IsType<CopilotTurnRequestPreparedEvent>(item).Request.Content),
            item => Assert.Equal("partial", Assert.IsType<CopilotTurnChatDeltaEvent>(item).Delta.Content),
            item =>
            {
                var completed = Assert.IsType<CopilotTurnCompletedEvent>(item);
                Assert.Equal(CopilotTurnStatus.Completed, completed.Status);
                Assert.Same(expectedResult, completed.Result);
            });
        Assert.Single(events.OfType<CopilotTurnCompletedEvent>());
    }

    [Fact]
    public async Task ProducerFailureEmitsFailedTerminalEventBeforeRethrowing()
    {
        var events = new List<CopilotTurnEvent>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var turnEvent in RunTurnAsync(
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
        Assert.Collection(
            events,
            item => Assert.IsType<CopilotTurnStartedEvent>(item),
            item => Assert.IsType<CopilotTurnChatDeltaEvent>(item),
            item =>
            {
                var error = Assert.IsType<CopilotTurnErrorEvent>(item);
                Assert.Equal("turn_failed", error.Error.Code);
                Assert.DoesNotContain("provider failed", error.Error.Message, StringComparison.Ordinal);
            },
            item =>
            {
                var completed = Assert.IsType<CopilotTurnCompletedEvent>(item);
                Assert.Equal(CopilotTurnStatus.Failed, completed.Status);
                Assert.Null(completed.Result);
                Assert.Equal("turn_failed", completed.Error?.Code);
                Assert.DoesNotContain("provider failed", completed.Error?.Message, StringComparison.Ordinal);
                Assert.Same(Assert.IsType<CopilotTurnErrorEvent>(events[2]).Error, completed.Error);
            });
    }

    [Fact]
    public async Task CancellingStreamEmitsInterruptedTerminalEventBeforeRethrowing()
    {
        using var cancellation = new CancellationTokenSource();
        var producerCancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var events = new List<CopilotTurnEvent>();

        var enumeration = Task.Run(async () =>
        {
            await foreach (var turnEvent in RunTurnAsync(
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
                if (turnEvent is CopilotTurnChatDeltaEvent)
                    cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => enumeration);
        await producerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.IsType<CopilotTurnStartedEvent>(events[0]);
        Assert.IsType<CopilotTurnChatDeltaEvent>(events[1]);
        var terminal = Assert.IsType<CopilotTurnCompletedEvent>(events[2]);
        Assert.Equal(CopilotTurnStatus.Interrupted, terminal.Status);
        Assert.Null(terminal.Result);
        Assert.Null(terminal.Error);
    }

    [Fact]
    public async Task DisposingEnumeratorEarlyCancelsProducer()
    {
        var producerCancelled = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = RunTurnAsync(
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
            Assert.IsType<CopilotTurnStartedEvent>(enumerator.Current);
        }

        await producerCancelled.Task.WaitAsync(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BlockingProducerCancellationCallbackCannotBlockStreamShutdown(bool cancelCaller)
    {
        var testTimeout = TimeSpan.FromSeconds(5);
        using var cancellation = new CancellationTokenSource();
        using var releaseCallback = new ManualResetEventSlim();
        var producerReady = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var callbackFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProducer = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var producerFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = RunTurnAsync(
            async (_, token) =>
            {
                try
                {
                    using var registration = token.Register(() =>
                    {
                        callbackStarted.TrySetResult();
                        releaseCallback.Wait(CancellationToken.None);
                        callbackFinished.TrySetResult();
                    });
                    producerReady.TrySetResult();
                    await releaseProducer.Task;
                    return CreateResult();
                }
                finally
                {
                    producerFinished.TrySetResult();
                }
            },
            cancellation.Token,
            TimeSpan.FromMilliseconds(50));
        var enumerator = stream.GetAsyncEnumerator();
        Task? stopTask = null;

        try
        {
            Assert.True(await enumerator.MoveNextAsync());
            Assert.IsType<CopilotTurnStartedEvent>(enumerator.Current);
            await producerReady.Task.WaitAsync(testTimeout);
            stopTask = cancelCaller
                ? Task.Factory.StartNew(
                    cancellation.Cancel,
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default)
                : Task.Factory.StartNew(
                    () => enumerator.DisposeAsync().AsTask(),
                    CancellationToken.None,
                    TaskCreationOptions.LongRunning,
                    TaskScheduler.Default).Unwrap();

            await callbackStarted.Task.WaitAsync(testTimeout);
            await stopTask.WaitAsync(testTimeout);
            Assert.False(releaseCallback.IsSet);
            Assert.False(producerFinished.Task.IsCompleted);
            if (cancelCaller)
            {
                Assert.True(await enumerator.MoveNextAsync().AsTask().WaitAsync(testTimeout));
                Assert.Equal(
                    CopilotTurnStatus.Interrupted,
                    Assert.IsType<CopilotTurnCompletedEvent>(enumerator.Current).Status);
                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    () => enumerator.MoveNextAsync().AsTask().WaitAsync(testTimeout));
            }
        }
        finally
        {
            releaseCallback.Set();
            releaseProducer.TrySetResult();
            if (stopTask != null)
                await stopTask.WaitAsync(testTimeout);
            await callbackFinished.Task.WaitAsync(testTimeout);
            await producerFinished.Task.WaitAsync(testTimeout);
            await enumerator.DisposeAsync();
        }
    }

    [Fact]
    public async Task ProducerCancellationCallbackFailureDoesNotReplaceTheTurnFailure()
    {
        var producerFailure = new InvalidOperationException("original provider failure");
        var callbackFinished = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationTokenRegistration registration = default;
        var events = new List<CopilotTurnEvent>();

        try
        {
            var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
            {
                await foreach (var turnEvent in RunTurnAsync(
                    (_, token) =>
                    {
                        registration = token.Register(() =>
                        {
                            callbackFinished.TrySetResult();
                            throw new InvalidOperationException("cancellation callback failure");
                        });
                        throw producerFailure;
                    },
                    CancellationToken.None))
                {
                    events.Add(turnEvent);
                }
            });

            Assert.Same(producerFailure, exception);
            Assert.Equal(
                CopilotTurnStatus.Failed,
                Assert.Single(events.OfType<CopilotTurnCompletedEvent>()).Status);
            await callbackFinished.Task.WaitAsync(TimeSpan.FromSeconds(5));
        }
        finally
        {
            registration.Dispose();
        }
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
            await foreach (var turnEvent in RunTurnAsync(
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
                if (turnEvent is CopilotTurnChatDeltaEvent)
                    cancellation.Cancel();
            }
        });

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => enumeration.WaitAsync(TimeSpan.FromSeconds(2)));
        Assert.Collection(
            events,
            item => Assert.IsType<CopilotTurnStartedEvent>(item),
            item => Assert.IsType<CopilotTurnChatDeltaEvent>(item),
            item => Assert.Equal(
                CopilotTurnStatus.Interrupted,
                Assert.IsType<CopilotTurnCompletedEvent>(item).Status));

        releaseProducer.TrySetResult();
        await producerFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task TurnStreamKeepsLargeStreamingBurstLosslessWithSmallBacklog()
    {
        const int chunkCount = 20_000;
        var expectedResult = CreateResult();
        var events = new List<CopilotTurnEvent>();

        await foreach (var turnEvent in RunTurnAsync(
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

    [Fact]
    public async Task BoundedBufferFailsBeforeACoalescedStreamEventExceedsItsCharacterBudget()
    {
        var buffer = new CopilotTurnEventBuffer(
            maximumPendingEvents: 2,
            maximumPendingStreamCharacters: 5);
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("abc"))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("de"))));

        var exception = Assert.Throws<InvalidOperationException>(() =>
            buffer.TryWrite(
                new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("f"))));

        Assert.Contains("5-character safety limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(1, buffer.PendingCount);
        Assert.Equal(5, buffer.PendingStreamCharacters);
        Assert.True(buffer.TryComplete());
        var turnEvent = Assert.Single(await DrainAsync(buffer));
        Assert.Equal(0, buffer.PendingStreamCharacters);
        var agentEvent = Assert.IsType<CopilotTurnAgentEvent>(turnEvent).Event;
        Assert.Equal("abcde", agentEvent.Text);
    }

    [Fact]
    public async Task BoundedBufferPreservesWhitespaceAndReleasesItsExactCharacterBudget()
    {
        var buffer = new CopilotTurnEventBuffer(
            maximumPendingEvents: 4,
            maximumPendingStreamCharacters: 2);
        Assert.True(buffer.TryWrite(
            new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta(" ", "x"))));

        Assert.Equal(2, buffer.PendingStreamCharacters);
        Assert.True(buffer.TryComplete());
        var turnEvent = Assert.Single(await DrainAsync(buffer));
        Assert.Equal(0, buffer.PendingStreamCharacters);
        var delta = Assert.IsType<CopilotTurnChatDeltaEvent>(turnEvent).Delta;
        Assert.Equal(" ", delta.ReasoningContent);
        Assert.Equal("x", delta.Content);
    }

    private static async Task<List<CopilotTurnEvent>> DrainAsync(
        CopilotTurnEventBuffer buffer)
    {
        var events = new List<CopilotTurnEvent>();
        await foreach (var turnEvent in buffer.ReadAllAsync())
            events.Add(turnEvent);
        return events;
    }

    private static IAsyncEnumerable<CopilotTurnEvent> RunTurnAsync(
        Func<CopilotTurnEventSink, CancellationToken, Task<CopilotTurnResult>> runTurn,
        CancellationToken cancellationToken,
        TimeSpan? producerShutdownTimeout = null,
        int maximumPendingEvents = CopilotTurnEventStream.DefaultMaximumPendingEvents) =>
        CopilotTurnEventStream.RunAsync(
            TurnId,
            CopilotAgentMode.Chat,
            runTurn,
            cancellationToken,
            producerShutdownTimeout,
            maximumPendingEvents);

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
