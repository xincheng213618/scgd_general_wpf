#pragma warning disable CA1707
using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotAgentTaskHostTests
{
    [Fact]
    public async Task Host_PausesOnlyAfterCheckpointBoundaryAndClearsCompletedRun()
    {
        var host = new CopilotAgentTaskHost();
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var changes = new List<CopilotAgentTaskHostChangeKind>();
        host.Changed += (_, _) => throw new InvalidOperationException("Subscriber failures must be isolated.");
        host.Changed += (_, args) => changes.Add(args.Kind);

        var run = host.Start("conversation-1", CopilotAgentMode.Auto, _ => release.Task);

        Assert.Same(run, host.ActiveRun);
        Assert.StartsWith("run:", run.Id);
        Assert.False(host.MarkCheckpointReady("run:stale"));
        Assert.False(host.RequestCancel("run:stale"));
        Assert.False(host.RequestPause(run.Id));
        Assert.True(host.MarkCheckpointReady(run.Id));
        Assert.True(host.RequestPause(run.Id));
        Assert.True(run.CancellationToken.IsCancellationRequested);
        Assert.Equal(CopilotAgentControlIntent.Pause, run.RunControl?.Intent);

        release.TrySetResult(null);
        await run.Completion;

        Assert.False(host.IsActive);
        Assert.Equal(CopilotHostedRunState.Completed, run.State);
        Assert.Equal(
            new[]
            {
                CopilotAgentTaskHostChangeKind.Started,
                CopilotAgentTaskHostChangeKind.CheckpointReady,
                CopilotAgentTaskHostChangeKind.ControlRequested,
                CopilotAgentTaskHostChangeKind.Completed,
            },
            changes);
    }

    [Fact]
    public async Task Host_CancelOverridesPauseAndRejectsConcurrentRun()
    {
        var host = new CopilotAgentTaskHost();
        var release = new TaskCompletionSource<object?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = host.Start("conversation-1", CopilotAgentMode.Auto, _ => release.Task);

        Assert.Throws<InvalidOperationException>(() => host.Start("conversation-2", CopilotAgentMode.Chat, _ => Task.CompletedTask));
        Assert.True(host.MarkCheckpointReady(run.Id));
        Assert.True(host.RequestPause(run.Id));
        Assert.True(host.RequestCancel(run.Id));
        Assert.Equal(CopilotAgentControlIntent.Cancel, run.RunControl?.Intent);
        Assert.Equal(CopilotHostedRunState.CancelRequested, run.State);

        release.TrySetResult(null);
        await run.Completion;

        Assert.False(host.IsActive);
    }

    [Fact]
    public async Task Host_PropagatesOperationFailureAfterReleasingActiveSlot()
    {
        var host = new CopilotAgentTaskHost();
        var run = host.Start("conversation-1", CopilotAgentMode.Chat, _ => Task.FromException(new InvalidOperationException("boom")));

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => run.Completion);

        Assert.Equal("boom", error.Message);
        Assert.False(host.IsActive);
        Assert.Equal(CopilotHostedRunState.Completed, run.State);
    }
}
