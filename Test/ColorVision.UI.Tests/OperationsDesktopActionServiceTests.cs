using ColorVision.UI.Desktop.Operations;
using System.Diagnostics;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests
{
    [CollectionDefinition(CollectionName, DisableParallelization = true)]
    public sealed class OperationsDesktopActionCollection
    {
        public const string CollectionName = "Operations desktop action dispatcher";
    }

    [Collection(OperationsDesktopActionCollection.CollectionName)]
    public sealed class OperationsDesktopActionServiceTests
    {
        [Fact]
        public async Task CaptureStateReturnsUnavailableWithinBoundWhenUiDispatcherIsBlocked()
        {
            WpfTestHost.Invoke(() => { });
            Dispatcher dispatcher = Application.Current!.Dispatcher;
            using ManualResetEventSlim blockerStarted = new();
            using ManualResetEventSlim releaseBlocker = new();
            DispatcherOperation blocker = dispatcher.InvokeAsync(() =>
            {
                blockerStarted.Set();
                releaseBlocker.Wait(TimeSpan.FromSeconds(5));
            }, DispatcherPriority.Send);
            Assert.True(blockerStarted.Wait(TimeSpan.FromSeconds(2)));

            try
            {
                Stopwatch stopwatch = Stopwatch.StartNew();
                OperationsDesktopState state = OperationsDesktopActionService.CaptureState();
                stopwatch.Stop();

                Assert.InRange(stopwatch.ElapsedMilliseconds, 800, 4_000);
                Assert.True(state.DispatcherAvailable);
                Assert.False(state.Exists);
                Assert.False(state.IsVisible);
                Assert.Equal("Unavailable", state.WindowState);
            }
            finally
            {
                releaseBlocker.Set();
                await blocker.Task.WaitAsync(TimeSpan.FromSeconds(2));
                Assert.True(blocker.Task.IsCompletedSuccessfully);
            }
        }
    }
}
