using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Output
{
    /// <summary>Serializes WPF snapshot rendering on one reusable STA dispatcher.</summary>
    internal static class SnapshotStaWorker
    {
        private static readonly Task<Dispatcher> DispatcherTask = StartDispatcher();

        private const long MaxOpaqueBackgroundBytes = 512L * 1024 * 1024;

        // Accessed only by the render dispatcher. Retain one matching bitmap between
        // consecutive exports, then release the native buffers after a short idle period.
        private static WriteableBitmap? opaqueBackground;
        private static DispatcherTimer? opaqueBackgroundIdleTimer;

        internal static WriteableBitmap? TakeOpaqueBackground(int width, int height, double dpiX, double dpiY)
        {
            DispatcherTask.GetAwaiter().GetResult().VerifyAccess();
            WriteableBitmap? available = opaqueBackground;
            ReleaseOpaqueBackground();
            // WriteableBitmap retains a native front and back buffer at 4 bytes per pixel each.
            if (width * 8L * height > MaxOpaqueBackgroundBytes)
                return null;

            if (available != null && available.PixelWidth == width && available.PixelHeight == height
                && available.DpiX == dpiX && available.DpiY == dpiY)
                return available;

            return new WriteableBitmap(width, height, dpiX, dpiY, PixelFormats.Pbgra32, null);
        }

        internal static void ReturnOpaqueBackground(WriteableBitmap background)
        {
            DispatcherTask.GetAwaiter().GetResult().VerifyAccess();
            opaqueBackground = background;

            if (opaqueBackgroundIdleTimer == null)
            {
                opaqueBackgroundIdleTimer = new DispatcherTimer(DispatcherPriority.Background)
                {
                    Interval = TimeSpan.FromSeconds(5),
                };
                opaqueBackgroundIdleTimer.Tick += (_, _) => ReleaseOpaqueBackground();
            }
            opaqueBackgroundIdleTimer.Stop();
            opaqueBackgroundIdleTimer.Start();
        }

        internal static void ReleaseOpaqueBackground()
        {
            opaqueBackgroundIdleTimer?.Stop();
            opaqueBackground = null;
        }

        internal static async Task RunAsync(Action action, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();

            Dispatcher dispatcher = await DispatcherTask.ConfigureAwait(false);
            DispatcherOperation operation = dispatcher.InvokeAsync(
                action,
                DispatcherPriority.Normal,
                cancellationToken);
            await operation.Task.ConfigureAwait(false);
        }

        internal static async Task<TResult> RunAsync<TResult>(
            Func<TResult> action,
            CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(action);
            cancellationToken.ThrowIfCancellationRequested();

            Dispatcher dispatcher = await DispatcherTask.ConfigureAwait(false);
            DispatcherOperation<TResult> operation = dispatcher.InvokeAsync(
                action,
                DispatcherPriority.Normal,
                cancellationToken);
            return await operation.Task.ConfigureAwait(false);
        }

        private static Task<Dispatcher> StartDispatcher()
        {
            TaskCompletionSource<Dispatcher> completion = new(
                TaskCreationOptions.RunContinuationsAsynchronously);
            Thread thread = new(() =>
            {
                try
                {
                    Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
                    completion.TrySetResult(dispatcher);
                    Dispatcher.Run();
                }
                catch (Exception ex)
                {
                    completion.TrySetException(ex);
                }
            })
            {
                IsBackground = true,
                Name = "ColorVision Image Snapshot Renderer",
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            return completion.Task;
        }
    }
}
