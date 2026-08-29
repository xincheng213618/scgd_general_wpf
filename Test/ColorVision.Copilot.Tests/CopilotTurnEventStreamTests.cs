using ColorVision.Copilot;

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

    [Fact]
    public async Task DisposingEnumeratorEarlyReleasesQueuedEventsWhenProducerIgnoresCancellation()
    {
        var queuedEvent = new TaskCompletionSource<WeakReference<CopilotAgentEvent>>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseProducer = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var producerFinished = new TaskCompletionSource(
            TaskCreationOptions.RunContinuationsAsynchronously);
        var stream = RunTurnAsync(
            async (sink, _) =>
            {
                try
                {
                    queuedEvent.TrySetResult(PublishWeaklyReferencedStatus(sink));
                    await releaseProducer.Task;
                    return CreateResult();
                }
                finally
                {
                    producerFinished.TrySetResult();
                }
            },
            CancellationToken.None,
            TimeSpan.FromMilliseconds(50));

        try
        {
            WeakReference<CopilotAgentEvent> eventReference;
            await using (var enumerator = stream.GetAsyncEnumerator())
            {
                Assert.True(await enumerator.MoveNextAsync());
                Assert.IsType<CopilotTurnStartedEvent>(enumerator.Current);
                eventReference = await queuedEvent.Task.WaitAsync(TimeSpan.FromSeconds(2));
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.False(eventReference.TryGetTarget(out _));
        }
        finally
        {
            releaseProducer.TrySetResult();
            await producerFinished.Task.WaitAsync(TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task GracefulCancellationDrainsFinalEventsAndCompletion()
    {
        using var cancellation = new CancellationTokenSource();
        var expectedResult = CreateResult();
        var events = new List<CopilotTurnEvent>();

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
                    sink.OnChatDelta(new CopilotStreamDelta(string.Empty, "finalized"));
                }

                return expectedResult;
            },
            cancellation.Token))
        {
            events.Add(turnEvent);
            if (turnEvent is CopilotTurnChatDeltaEvent chatDelta
                && chatDelta.Delta.Content == "partial")
                cancellation.Cancel();
        }

        Assert.Collection(
            events,
            item => Assert.IsType<CopilotTurnStartedEvent>(item),
            item => Assert.Equal("partial", Assert.IsType<CopilotTurnChatDeltaEvent>(item).Delta.Content),
            item => Assert.Equal("finalized", Assert.IsType<CopilotTurnChatDeltaEvent>(item).Delta.Content),
            item =>
            {
                var completed = Assert.IsType<CopilotTurnCompletedEvent>(item);
                Assert.Equal(CopilotTurnStatus.Interrupted, completed.Status);
                Assert.Same(expectedResult, completed.Result);
            });
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
    public async Task ProducerSynchronousPrefixDoesNotBlockEnumeratorThread()
    {
        using var producerStarted = new ManualResetEventSlim();
        using var releaseProducer = new ManualResetEventSlim();
        var enumeratorThreadId = 0;
        var producerThreadId = 0;
        var stream = RunTurnAsync(
            (_, _) =>
            {
                producerThreadId = Environment.CurrentManagedThreadId;
                producerStarted.Set();
                releaseProducer.Wait(CancellationToken.None);
                return Task.FromResult(CreateResult());
            },
            CancellationToken.None);
        await using var enumerator = stream.GetAsyncEnumerator();
        Assert.True(await enumerator.MoveNextAsync());
        Assert.IsType<CopilotTurnStartedEvent>(enumerator.Current);
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
    public async Task BoundedBufferPreservesReviewLifecycleAroundCoalescedAnswer()
    {
        var buffer = new CopilotTurnEventBuffer(maximumPendingEvents: 5);
        var expectedResult = CopilotTurnResult.FromAgent(
            CopilotAgentMode.Review,
            CopilotTokenUsage.Empty,
            new CopilotAgentRunResult());

        Assert.True(buffer.TryWrite(
            new CopilotTurnReviewEnteredEvent(CopilotWorkspaceReviewTargetContext.WorkingTree())));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("finding-1"))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("+finding-2"))));
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.Completed())));
        Assert.True(buffer.TryWrite(
            new CopilotTurnReviewExitedEvent(
                CopilotWorkspaceReviewTargetContext.WorkingTree(),
                "finding-1+finding-2",
                false)));
        Assert.True(buffer.TryWrite(new CopilotTurnCompletedEvent(expectedResult)));
        Assert.True(buffer.TryComplete());

        var events = await DrainAsync(buffer);

        Assert.Collection(
            events,
            item => Assert.IsType<CopilotTurnReviewEnteredEvent>(item),
            item => Assert.Equal(
                "finding-1+finding-2",
                Assert.IsType<CopilotTurnAgentEvent>(item).Event.Text),
            item => Assert.Equal(
                CopilotAgentEventType.Completed,
                Assert.IsType<CopilotTurnAgentEvent>(item).Event.Type),
            item => Assert.IsType<CopilotTurnReviewExitedEvent>(item),
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
    public async Task FailureWriteClosesFullBufferAtomicallyAndRethrowsAfterTerminal()
    {
        var buffer = new CopilotTurnEventBuffer(maximumPendingEvents: 1);
        var failure = new InvalidOperationException("original failure");
        var error = CopilotTurnError.FromException(failure);
        Assert.True(buffer.TryWrite(new CopilotTurnStartedEvent(TurnId, CopilotAgentMode.Chat)));
        Assert.True(buffer.TryWriteFailureAndComplete(
            new CopilotTurnErrorEvent(TurnId, CopilotAgentMode.Chat, error),
            CopilotTurnCompletedEvent.Failed(TurnId, CopilotAgentMode.Chat, error),
            failure));
        Assert.False(buffer.TryWrite(
            new CopilotTurnChatDeltaEvent(new CopilotStreamDelta(string.Empty, "late"))));
        var events = new List<CopilotTurnEvent>();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await foreach (var turnEvent in buffer.ReadAllAsync())
                events.Add(turnEvent);
        });

        Assert.Same(failure, exception);
        Assert.Collection(
            events,
            item => Assert.IsType<CopilotTurnStartedEvent>(item),
            item => Assert.Same(error, Assert.IsType<CopilotTurnErrorEvent>(item).Error),
            item => Assert.Equal(
                CopilotTurnStatus.Failed,
                Assert.IsType<CopilotTurnCompletedEvent>(item).Status));
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
    public void BoundedBufferRejectsAnInitialStreamEventLargerThanItsCharacterBudget()
    {
        var buffer = new CopilotTurnEventBuffer(
            maximumPendingEvents: 4,
            maximumPendingStreamCharacters: 4);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            buffer.TryWrite(
                new CopilotTurnChatDeltaEvent(
                    new CopilotStreamDelta("12", "345"))));

        Assert.Contains("4-character safety limit", exception.Message, StringComparison.Ordinal);
        Assert.Equal(0, buffer.PendingCount);
        Assert.Equal(0, buffer.PendingStreamCharacters);
    }

    [Fact]
    public async Task BoundedBufferAccountsForBothSidesOfANonCoalescedChatDelta()
    {
        var buffer = new CopilotTurnEventBuffer(
            maximumPendingEvents: 4,
            maximumPendingStreamCharacters: 5);
        Assert.True(buffer.TryWrite(
            new CopilotTurnChatDeltaEvent(
                new CopilotStreamDelta("ab", "cde"))));

        Assert.Equal(1, buffer.PendingCount);
        Assert.Equal(5, buffer.PendingStreamCharacters);
        Assert.True(buffer.TryComplete());
        var turnEvent = Assert.Single(await DrainAsync(buffer));
        Assert.Equal(0, buffer.PendingStreamCharacters);
        var delta = Assert.IsType<CopilotTurnChatDeltaEvent>(turnEvent).Delta;
        Assert.Equal("ab", delta.ReasoningContent);
        Assert.Equal("cde", delta.Content);
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

    [Fact]
    public async Task AbandonClearsPendingEventsCharactersAndCompletionError()
    {
        var buffer = new CopilotTurnEventBuffer(
            maximumPendingEvents: 4,
            maximumPendingStreamCharacters: 4);
        Assert.True(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("test"))));
        Assert.True(buffer.TryComplete(new InvalidOperationException("must be discarded")));

        buffer.Abandon();

        Assert.Equal(0, buffer.PendingCount);
        Assert.Equal(0, buffer.PendingStreamCharacters);
        Assert.False(buffer.TryWrite(
            new CopilotTurnAgentEvent(CopilotAgentEvent.AnswerDelta("late"))));
        Assert.Empty(await DrainAsync(buffer));
    }

    private static async Task<List<CopilotTurnEvent>> DrainAsync(
        CopilotTurnEventBuffer buffer)
    {
        var events = new List<CopilotTurnEvent>();
        await foreach (var turnEvent in buffer.ReadAllAsync())
            events.Add(turnEvent);
        return events;
    }

    private static WeakReference<CopilotAgentEvent> PublishWeaklyReferencedStatus(
        CopilotTurnEventSink sink)
    {
        var agentEvent = CopilotAgentEvent.Status(new string('x', 4096));
        var reference = new WeakReference<CopilotAgentEvent>(agentEvent);
        sink.OnAgentEvent(agentEvent);
        return reference;
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
