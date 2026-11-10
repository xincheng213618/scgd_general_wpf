using ColorVision.ImageEditor.Tif;

namespace ColorVision.UI.Tests;

public sealed class BitmapOpenRequestsTests
{
    [Fact]
    public async Task CancelledMiddleRequestDoesNotReleaseActivePreparation()
    {
        var requests = new BitmapOpenRequests();
        var releaseFirst = new TaskCompletionSource<int>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        TimeSpan timeout = TimeSpan.FromSeconds(10);
        Task drain;
        using (var first = requests.Start(default))
        {
            Task<int> active = requests.PrepareAsync(() =>
            {
                firstEntered.SetResult();
                return releaseFirst.Task;
            }, first.Token);
            try
            {
                await firstEntered.Task.WaitAsync(timeout);

                using (var middle = requests.Start(default))
                {
                    Task<int> waiting = requests.PrepareAsync<int>(
                        () => throw new InvalidOperationException("The cancelled request entered preparation."), middle.Token);
                    Assert.False(waiting.IsCompleted);

                    drain = requests.CancelAndDrain();
                    await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiting.WaitAsync(timeout));
                }

                // The drain snapshot only covers the first request now; the new request is admitted later.
                using var latest = requests.Start(default);
                Task<int> next = requests.PrepareAsync(() =>
                {
                    Assert.True(releaseFirst.Task.IsCompleted);
                    return Task.FromResult(3);
                }, latest.Token);
                Assert.False(next.IsCompleted);
                Assert.False(drain.IsCompleted);

                releaseFirst.SetResult(1);
                Assert.Equal(1, await active.WaitAsync(timeout));
                Assert.Equal(3, await next.WaitAsync(timeout));
            }
            finally
            {
                releaseFirst.TrySetResult(1);
            }
        }

        await drain.WaitAsync(timeout);
    }
}
