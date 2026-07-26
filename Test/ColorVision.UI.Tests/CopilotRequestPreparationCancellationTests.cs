using ColorVision.Copilot;

namespace ColorVision.UI.Tests;

public sealed class CopilotRequestPreparationCancellationTests
{
    [Fact]
    public async Task WebContextCancellationDoesNotWaitForBlockingLoaderPrefix()
    {
        using var started = new ManualResetEventSlim();
        using var release = new ManualResetEventSlim();
        using var completed = new ManualResetEventSlim();
        using var cancellation = new CancellationTokenSource();
        var builder = new CopilotConversationRequestBuilder((_, _) =>
        {
            started.Set();
            try
            {
                release.Wait(CancellationToken.None);
                return Task.FromException<CopilotFetchedWebPageContent>(
                    new InvalidOperationException("late loader failure"));
            }
            finally
            {
                completed.Set();
            }
        });
        var requestTask = Task.Run(() => builder.BuildUserRequestContentAsync(
            "inspect https://example.invalid/context",
            liveContext: null,
            cancellation.Token));

        try
        {
            Assert.True(started.Wait(TimeSpan.FromSeconds(1)));
            cancellation.Cancel();

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => requestTask.WaitAsync(TimeSpan.FromSeconds(1)));
            Assert.False(release.IsSet);
        }
        finally
        {
            release.Set();
            _ = completed.Wait(TimeSpan.FromSeconds(2));
        }
    }
}
