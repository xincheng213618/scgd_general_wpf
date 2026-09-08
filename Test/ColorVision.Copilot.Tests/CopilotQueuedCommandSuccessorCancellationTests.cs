using ColorVision.Copilot;
using System.Collections.Concurrent;

namespace ColorVision.Copilot.Tests;

public sealed class CopilotQueuedCommandSuccessorCancellationTests
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(5);

    [Fact]
    public async Task CancelledCommandCannotAdvertiseSuccessorAdmissionWhileItDrains()
    {
        await using var fixture = new DrainingCommandFixture();
        await fixture.StartCommandAsync();
        Assert.True(fixture.Host.RequestCancel(fixture.Command.Id));
        Assert.Equal(CopilotHostedRunState.CancelRequested, fixture.Command.State);
        Assert.True(fixture.Command.CancellationToken.IsCancellationRequested);
        Assert.False(fixture.Command.Completion.IsCompleted);

        var admission = fixture.Host.EvaluateQueuedCommandSuccessorAdmission(fixture.Command.Id, "conversation");

        Assert.False(admission.IsAllowed);
        Assert.Equal(CopilotRequestAdmissionReason.NoActiveRun, admission.Reason);
    }

    [Fact]
    public async Task CancelledCommandCannotAdmitOrExecuteANewSuccessorWhileItDrains()
    {
        await using var fixture = new DrainingCommandFixture();
        await fixture.StartCommandAsync();
        Assert.True(fixture.Host.RequestCancel(fixture.Command.Id));
        var successorCalls = 0;

        var scheduled = fixture.Host.TryScheduleQueuedCommandSuccessor(
            fixture.Command.Id,
            "conversation",
            CopilotAgentMode.Code,
            _ =>
            {
                Interlocked.Increment(ref successorCalls);
                return Task.CompletedTask;
            },
            out var successor,
            out var admission);
        fixture.ReleaseCommand();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Command.Completion.WaitAsync(TestTimeout));
        // On the old implementation an admitted successor really executes, so
        // observe its completion before checking the failed admission contract.
        if (successor != null)
            await successor.Completion.WaitAsync(TestTimeout);

        Assert.Equal(0, Volatile.Read(ref successorCalls));
        Assert.False(scheduled);
        Assert.Null(successor);
        Assert.Equal(CopilotRequestAdmissionReason.NoActiveRun, admission.Reason);
        Assert.DoesNotContain(fixture.Events, item => item.Kind == CopilotAgentTaskHostChangeKind.Queued
            && item.Run.Id != fixture.Command.Id);
    }

    [Fact]
    public async Task CancellationDoesNotRevokeASuccessorAcceptedBeforeTheCommandWasCancelled()
    {
        await using var fixture = new DrainingCommandFixture();
        await fixture.StartCommandAsync();
        var successorCalls = 0;
        Assert.True(fixture.Host.TryScheduleQueuedCommandSuccessor(
            fixture.Command.Id,
            "conversation",
            CopilotAgentMode.Code,
            run =>
            {
                Assert.False(run.CancellationToken.IsCancellationRequested);
                Interlocked.Increment(ref successorCalls);
                return Task.CompletedTask;
            },
            out var successor,
            out var admission));
        Assert.True(admission.IsAllowed);

        Assert.True(fixture.Host.RequestCancel(fixture.Command.Id));
        fixture.ReleaseCommand();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Command.Completion.WaitAsync(TestTimeout));
        await successor!.Completion.WaitAsync(TestTimeout);

        Assert.Equal(1, Volatile.Read(ref successorCalls));
        Assert.Equal(CopilotHostedRunState.Completed, successor.State);
        Assert.False(successor.CancellationToken.IsCancellationRequested);
    }

    [Fact]
    public async Task CommandCancellationDoesNotBlockAnIndependentUserRequestAlreadyInTheQueue()
    {
        await using var fixture = new DrainingCommandFixture();
        var independentCalls = 0;
        // Independent requests can queue while the original Agent is active.
        // Once the local command is active, the existing exclusive policy applies.
        Assert.True(fixture.Host.TrySchedule(
            "independent-conversation",
            CopilotAgentMode.Code,
            run =>
            {
                Assert.False(run.CancellationToken.IsCancellationRequested);
                Interlocked.Increment(ref independentCalls);
                return Task.CompletedTask;
            },
            out var independent));
        await fixture.StartCommandAsync();
        Assert.True(fixture.Host.RequestCancel(fixture.Command.Id));

        fixture.ReleaseCommand();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.Command.Completion.WaitAsync(TestTimeout));
        await independent!.Completion.WaitAsync(TestTimeout);

        Assert.Equal(1, Volatile.Read(ref independentCalls));
        Assert.False(independent.CancellationToken.IsCancellationRequested);
    }

    private sealed class DrainingCommandFixture : IAsyncDisposable
    {
        private readonly TaskCompletionSource _releaseInitial = NewSignal();
        private readonly TaskCompletionSource _commandStarted = NewSignal();
        private readonly TaskCompletionSource _releaseCommand = NewSignal();
        private readonly CopilotHostedAgentRun _initial;

        public DrainingCommandFixture()
        {
            Host.Changed += (_, item) => Events.Enqueue(item);
            _initial = Host.Start("conversation", CopilotAgentMode.Auto, _ => _releaseInitial.Task);
            Assert.True(Host.TryScheduleLocalCommandFollowUp(
                "conversation",
                CopilotAgentMode.Auto,
                _ =>
                {
                    _commandStarted.TrySetResult();
                    // Model a local command whose previously started preparation
                    // must drain after cancellation before releasing the host slot.
                    return _releaseCommand.Task;
                },
                runNext: false,
                out var command,
                out _));
            Command = Assert.IsType<CopilotHostedAgentRun>(command);
        }

        public CopilotAgentTaskHost Host { get; } = new();
        public ConcurrentQueue<CopilotAgentTaskHostChangedEventArgs> Events { get; } = new();
        public CopilotHostedAgentRun Command { get; }

        public async Task StartCommandAsync()
        {
            _releaseInitial.TrySetResult();
            await _commandStarted.Task.WaitAsync(TestTimeout);
            await _initial.Completion.WaitAsync(TestTimeout);
            Assert.Same(Command, Host.ActiveRun);
            Assert.False(Command.IsAgent);
            Assert.Equal(CopilotHostedRunState.Running, Command.State);
        }

        public void ReleaseCommand() => _releaseCommand.TrySetResult();

        public async ValueTask DisposeAsync()
        {
            var pending = Host.ScheduledRuns.Append(_initial).Append(Command).Distinct().ToArray();
            Host.Shutdown();
            _releaseInitial.TrySetResult();
            _releaseCommand.TrySetResult();
            foreach (var run in pending)
            {
                try
                {
                    await run.Completion.WaitAsync(TestTimeout);
                }
                catch (OperationCanceledException)
                {
                }
            }
        }
    }

    private static TaskCompletionSource NewSignal() => new(TaskCreationOptions.RunContinuationsAsynchronously);
}
