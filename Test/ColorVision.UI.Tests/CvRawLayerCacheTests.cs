using ColorVision.Engine.Media;
using ColorVision.ImageEditor;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvRawLayerCacheTests
{
    [Fact]
    public void DisplayCacheBoundsCombinedPixelsAndDropsOldEntriesForOversizedLayers()
    {
        WpfTestHost.Invoke(() =>
        {
            using var view = new ImageView();
            using var controller = CvRawLayerController.Create(view, string.Empty, true, 3, 32, true, "cie-srgb");
            // These are real bitmap sizes, so stride, overflow and allocation accounting are exercised.
            var first = new WriteableBitmap(4096, 2048, 96, 96, PixelFormats.Rgba64, null); // 64 MiB
            var second = new WriteableBitmap(4096, 2048, 96, 96, PixelFormats.Rgba64, null);
            Store(controller, "cie-srgb", first);
            Store(controller, "cie-y", second);
            Assert.Same(first, Cached(controller, "_srgbCache"));
            Assert.Same(second, Cached(controller, "_channelCache"));

            var replacement = new WriteableBitmap(4096, 2049, 96, 96, PixelFormats.Rgba64, null);
            Store(controller, "cie-srgb", replacement);
            Assert.Same(replacement, Cached(controller, "_srgbCache"));
            Assert.Null(Cached(controller, "_channelCache"));

            var oversized = new WriteableBitmap(4096, 4097, 96, 96, PixelFormats.Rgba64, null);
            Store(controller, "cie-x", oversized);
            Assert.Null(Cached(controller, "_srgbCache"));
            Assert.Null(Cached(controller, "_channelCache"));
        });
    }

    private static void Store(CvRawLayerController controller, string layer, WriteableBitmap bitmap)
    {
        Type stampType = typeof(CvRawLayerController).GetNestedType("FileStamp", BindingFlags.NonPublic)!;
        typeof(CvRawLayerController).GetMethod("StoreCache", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(controller, [layer, bitmap, CvcieBrightnessMode.Auto, 65535d, Activator.CreateInstance(stampType)]);
    }

    private static WriteableBitmap? Cached(CvRawLayerController controller, string field)
    {
        object? entry = typeof(CvRawLayerController).GetField(field, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(controller);
        return (WriteableBitmap?)entry?.GetType().GetProperty("Bitmap")!.GetValue(entry);
    }
}
