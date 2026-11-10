using ColorVision.ImageEditor.Presentation;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

public sealed class EditableBitmapCopyTests
{
    [Fact]
    public async Task CancellationWaitsForTheWorkerWhileTheDispatcherRemainsAvailable()
    {
        Dispatcher dispatcher = WpfTestHost.Invoke(() => Dispatcher.CurrentDispatcher);
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        using var cancellation = new CancellationTokenSource();
        using var release = new ManualResetEventSlim();
        TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        TaskCompletionSource exited = new(TaskCreationOptions.RunContinuationsAsynchronously);

        Task<WriteableBitmap> pending = dispatcher.InvokeAsync<Task<WriteableBitmap>>(() =>
            EditableBitmapCopy.CreateAsync(3, 2, 91, 107, PixelFormats.Gray8, null, (_, _) =>
            {
                entered.SetResult(dispatcher.CheckAccess());
                try
                {
                    if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("Bitmap copy gate was not released.");
                }
                finally { exited.SetResult(); }
            }, cancellation.Token)).Task.Unwrap();

        try
        {
            Assert.False(await entered.Task.WaitAsync(testWait));
            await dispatcher.InvokeAsync(() => cancellation.Cancel()).Task.WaitAsync(testWait);
            await dispatcher.InvokeAsync(() => Assert.False(pending.IsCompleted), DispatcherPriority.Background).Task.WaitAsync(testWait);
        }
        finally
        {
            release.Set();
            if (entered.Task.IsCompleted) await exited.Task.WaitAsync(testWait);
        }

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending.WaitAsync(testWait));

        byte[] expected = [1, 2, 3, 41, 42, 43];
        WriteableBitmap bitmap = await WpfTestHost.Invoke(() =>
            EditableBitmapCopy.CreateAsync(3, 2, 91, 107, PixelFormats.Gray8, null, (address, stride) =>
            {
                Marshal.Copy(expected, 0, address, 3);
                Marshal.Copy(expected, 3, IntPtr.Add(address, stride), 3);
            }, CancellationToken.None)).WaitAsync(testWait);

        WpfTestHost.Invoke(() =>
        {
            Assert.False(bitmap.IsFrozen);
            Assert.Equal(PixelFormats.Gray8, bitmap.Format);
            Assert.Equal((91.0, 107.0), (bitmap.DpiX, bitmap.DpiY));
            byte[] actual = new byte[expected.Length];
            bitmap.CopyPixels(actual, 3, 0);
            Assert.Equal(expected, actual);
        });
    }
}
