namespace ColorVision.Copilot.Tests;

public sealed class CopilotRequestPreparationCancellationTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task WebContextCancellationDoesNotWaitForBlockingLoaderPrefix(bool attachmentRefresh)
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
        var requestTask = Task.Run(() => attachmentRefresh
            ? builder.BuildRequestAttachmentContextBlockAsync(
                [CopilotAttachmentItem.CreateWebPage("https://example.invalid/context", "Saved page", "Old cached evidence")],
                refreshWebPages: true,
                cancellation.Token)
            : builder.BuildUserRequestContentAsync(
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
