using System.IO;
using System.Windows.Threading;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotStreamDeltaBufferTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(10);

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void SameDispatcherBurstIsDeferredAndCompleteCommitsTheTailWithoutDuplicateApply(bool targetCompletion)
    {
        StaTest.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            Task? backgroundCompletion = null;
            try
            {
                var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
                var applied = new List<string>();
                var buffer = new CopilotStreamDeltaBuffer(new DispatcherSynchronizationContext(dispatcher), batch =>
                {
                    foreach (var delta in batch)
                        CopilotAssistantMessagePresenter.ApplyStreamDelta(assistant, delta);
                    applied.Add(assistant.Content);
                }, isOnTargetThread: dispatcher.CheckAccess);

                buffer.Enqueue(new CopilotStreamDelta(string.Empty, "Accepted "));
                buffer.Enqueue(new CopilotStreamDelta(string.Empty, "tail."));
                Assert.Empty(applied);
                Assert.Empty(assistant.Content);

                if (targetCompletion)
                {
                    Assert.True(buffer.CompleteAsync().IsCompletedSuccessfully);
                }
                else
                {
                    var submitted = new TaskCompletionSource<Task>(TaskCreationOptions.RunContinuationsAsynchronously);
                    backgroundCompletion = Task.Run(async () =>
                    {
                        try
                        {
                            var completion = buffer.CompleteAsync();
                            submitted.TrySetResult(completion);
                            await completion.ConfigureAwait(false);
                        }
                        catch (Exception exception)
                        {
                            submitted.TrySetException(exception);
                            throw;
                        }
                    });
                    var completion = submitted.Task.WaitAsync(TestTimeout).GetAwaiter().GetResult();
                    Assert.False(completion.IsCompleted);
                    Assert.True(buffer.FlushAsync().IsCompletedSuccessfully);
                }
                Assert.Equal("Accepted tail.", assistant.Content);
                Assert.Equal(["Accepted tail."], applied);
                DrainPostedCallbacks(dispatcher);
                backgroundCompletion?.WaitAsync(TestTimeout).GetAwaiter().GetResult();
                Assert.Equal(["Accepted tail."], applied);
                Assert.Throws<InvalidOperationException>(() => buffer.Enqueue(new CopilotStreamDelta(string.Empty, "Late text.")));
            }
            finally
            {
                try
                {
                    DrainPostedCallbacks(dispatcher);
                    backgroundCompletion?.WaitAsync(TestTimeout).GetAwaiter().GetResult();
                }
                finally
                {
                    dispatcher.InvokeShutdown();
                }
            }
        });
    }

    [Fact]
    public void FlushAppliesTheOldAnswerBeforeResetAndCompletePreservesOnlyTheNewAnswer()
    {
        StaTest.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                var assistant = new CopilotChatMessage(CopilotChatRole.Assistant, string.Empty);
                var appliedDeltas = new List<CopilotStreamDelta>();
                var buffer = new CopilotStreamDeltaBuffer(new DispatcherSynchronizationContext(dispatcher), batch =>
                {
                    foreach (var delta in batch)
                    {
                        appliedDeltas.Add(delta);
                        CopilotAssistantMessagePresenter.ApplyStreamDelta(assistant, delta);
                    }
                }, isOnTargetThread: dispatcher.CheckAccess);

                buffer.Enqueue(new CopilotStreamDelta("First thought. ", string.Empty));
                buffer.Enqueue(new CopilotStreamDelta(string.Empty, "Old "));
                buffer.Enqueue(new CopilotStreamDelta("Second thought. ", string.Empty));
                buffer.Enqueue(new CopilotStreamDelta("Mixed thought. ", "answer."));
                Assert.True(buffer.FlushAsync().IsCompletedSuccessfully);
                Assert.Equal("Old answer.", assistant.Content);
                Assert.Equal(
                    [new CopilotStreamDelta("First thought. ", string.Empty), new CopilotStreamDelta(string.Empty, "Old "),
                        new CopilotStreamDelta("Second thought. ", string.Empty), new CopilotStreamDelta("Mixed thought. ", "answer.")],
                    appliedDeltas);

                assistant.ResetResponseTimelineText();
                buffer.Enqueue(new CopilotStreamDelta("Refreshed thought. ", string.Empty));
                buffer.Enqueue(new CopilotStreamDelta("Final thought.", "New answer."));
                Assert.Empty(assistant.Content);
                Assert.True(buffer.CompleteAsync().IsCompletedSuccessfully);
                DrainPostedCallbacks(dispatcher);
                Assert.Equal("New answer.", assistant.Content);
                Assert.Equal("First thought. Second thought. Mixed thought. Refreshed thought. Final thought.", assistant.ReasoningContent);
                Assert.False(assistant.IsReasoningInProgress);
                Assert.False(assistant.IsReasoningExpanded);
            }
            finally
            {
                dispatcher.InvokeShutdown();
            }
        });
    }

    [Fact]
    public void PostedApplyFailureIsObservedByFlushAndCompleteWithItsOriginalCause()
    {
        StaTest.Run(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            try
            {
                var cause = new IOException("Expected posted Chat application failure.");
                var buffer = new CopilotStreamDeltaBuffer(new DispatcherSynchronizationContext(dispatcher),
                    _ => throw cause, isOnTargetThread: dispatcher.CheckAccess);

                buffer.Enqueue(new CopilotStreamDelta(string.Empty, "Accepted tail."));
                DrainPostedCallbacks(dispatcher);

                var flushError = Assert.Throws<InvalidOperationException>(() => { _ = buffer.FlushAsync(); });
                Assert.Same(cause, flushError.InnerException);
                var completionError = Assert.Throws<InvalidOperationException>(() => { _ = buffer.CompleteAsync(); });
                Assert.Same(cause, completionError.InnerException);
            }
            finally
            {
                dispatcher.InvokeShutdown();
            }
        });
    }

    private static void DrainPostedCallbacks(Dispatcher dispatcher)
    {
        var frame = new DispatcherFrame();
        dispatcher.BeginInvoke(DispatcherPriority.Background, new Action(() => frame.Continue = false));
        Dispatcher.PushFrame(frame);
    }
}
