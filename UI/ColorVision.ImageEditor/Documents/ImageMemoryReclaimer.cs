using log4net;
using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace ColorVision.ImageEditor.Documents;

/// <summary>Reclaims retired large images after their owners and WPF have detached them.</summary>
public static class ImageMemoryReclaimer
{
    // Small images use normal GC. A manual release of a camera-sized buffer warrants
    // returning its POH/LOH pages and finalizable WPF buffers even while the app is idle.
    internal const long MinimumReleaseBytes = 64L * 1024 * 1024;
    private static readonly object Sync = new();
    private static readonly ILog Log = LogManager.GetLogger(typeof(ImageMemoryReclaimer));
    private static Task? _pending;
    private static long _requestedVersion;

    internal static Task PendingCollection { get { lock (Sync) return _pending ?? Task.CompletedTask; } }

    internal static long GetPixelBytes(ImageSource? source) => source is BitmapSource bitmap
        ? ((long)bitmap.PixelWidth * bitmap.Format.BitsPerPixel + 7) / 8 * bitmap.PixelHeight
        : 0;

    /// <summary>Call only after dropping ownership, never for each incoming video frame.</summary>
    public static Task RequestCollection(long releasedBytes)
    {
        if (releasedBytes < MinimumReleaseBytes) return Task.CompletedTask;
        lock (Sync)
        {
            _requestedVersion++;
            return _pending ??= Task.Run(CollectAsync);
        }
    }

    private static async Task CollectAsync()
    {
        try
        {
            while (true)
            {
                // Let the clear/close stack return and the render pass detach its resources.
                // Do not capture the retired bitmap, view or RAW array in this work item.
                Dispatcher? dispatcher = Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.HasShutdownStarted)
                {
                    try { await dispatcher.InvokeAsync(static () => { }, DispatcherPriority.ContextIdle).Task.ConfigureAwait(false); }
                    catch (TaskCanceledException) when (dispatcher.HasShutdownStarted) { }
                }

                // Coalesce the RAW array and display bitmap retired by the same UI action.
                long version;
                lock (Sync) version = _requestedVersion;

                // WPF's WriteableBitmap.GetEstimatedSize uses int multiplication: a
                // 9568 x 6380 RGB48 image reports negative pressure, omitting its native
                // buffers from GC accounting. Normal GC also keeps empty POH regions.
                // This explicit large-image release must finalize buffers AND decommit
                // unused pages. Waiting for finalizers belongs off the UI thread.
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);
                GC.WaitForPendingFinalizers();
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Aggressive, blocking: true, compacting: true);

                lock (Sync)
                {
                    if (version != _requestedVersion) continue;
                    _pending = null;
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            lock (Sync) _pending = null;
            Log.Warn("Unable to finish retired image memory reclamation.", ex);
        }
    }
}
