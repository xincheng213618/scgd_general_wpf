using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotExceptionViewModelTests
{
    [Fact]
    public async Task LatestLogRefreshWinsWhenAnOlderCaptureCompletesLate()
    {
        var captures = new List<PendingCapture>();
        using var viewModel = CopilotExceptionViewModel.Create(
            new InvalidOperationException("boom"),
            "test",
            (mode, maxLines, maxChars, cancellationToken) =>
            {
                var capture = new PendingCapture(mode, cancellationToken);
                captures.Add(capture);
                return capture.Completion.Task;
            });
        var firstRefresh = viewModel.CurrentLogRefreshTask;

        viewModel.ApplyLogOptions(CopilotRecentLogMode.FullDay, 160);
        var latestRefresh = viewModel.CurrentLogRefreshTask;

        Assert.Equal(2, captures.Count);
        Assert.True(captures[0].CancellationToken.IsCancellationRequested);
        Assert.Equal(CopilotRecentLogMode.FullDay, captures[1].Mode);

        captures[1].Completion.SetResult(Snapshot("latest"));
        await latestRefresh;
        Assert.Equal("latest", viewModel.RecentLogContent);

        captures[0].Completion.SetResult(Snapshot("stale"));
        await firstRefresh;
        Assert.Equal("latest", viewModel.RecentLogContent);
    }

    [Fact]
    public async Task DisposeCancelsPendingLogRefreshWithoutApplyingItsResult()
    {
        var completion = new TaskCompletionSource<CopilotRecentLogSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedToken = default;
        var viewModel = CopilotExceptionViewModel.Create(
            new InvalidOperationException("boom"),
            "test",
            (_, _, _, cancellationToken) =>
            {
                observedToken = cancellationToken;
                return completion.Task;
            });
        var refresh = viewModel.CurrentLogRefreshTask;

        viewModel.Dispose();
        Assert.True(observedToken.IsCancellationRequested);

        completion.SetResult(Snapshot("late"));
        await refresh;
        Assert.NotEqual("late", viewModel.RecentLogContent);
    }

    private static CopilotRecentLogSnapshot Snapshot(string content)
    {
        return new CopilotRecentLogSnapshot
        {
            Success = true,
            Summary = "captured",
            FilePath = @"C:\logs\latest.log",
            Content = content,
        };
    }

    private sealed record PendingCapture(
        CopilotRecentLogMode Mode,
        CancellationToken CancellationToken)
    {
        public TaskCompletionSource<CopilotRecentLogSnapshot> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
