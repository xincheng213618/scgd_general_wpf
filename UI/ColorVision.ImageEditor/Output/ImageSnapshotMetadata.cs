using ColorVision.ImageEditor.Tif;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Output;

internal static class ImageSnapshotMetadata
{
    // Keep the public frozen BitmapSource shape without carrying dispatcher-affine BitmapMetadata
    // across threads. Weak keys give the detached JSON the same lifetime as its captured pixels.
    private static readonly ConditionalWeakTable<BitmapSource, string> CapturedRecords = new();

    internal static BitmapSource CaptureRecord(BitmapSource pixels, string parametersJson)
    {
        CapturedRecords.Add(pixels, parametersJson);
        return pixels;
    }

    internal static BitmapFrame CreateFrame(BitmapSource pixels, string? parametersJson, string format)
    {
        if (parametersJson == null) CapturedRecords.TryGetValue(pixels, out parametersJson);
        ColorVisionTiffParameters? parameters = null;
        if (parametersJson != null && !ColorVisionTiffParameters.TryParse(parametersJson, out parameters)) return BitmapFrame.Create(pixels);
        if (parameters == null) return BitmapFrame.Create(pixels);
        parameters = parameters.ToDisplayRecord();
        parameters.RenderedOutput = new()
        {
            Width = pixels.PixelWidth, Height = pixels.PixelHeight,
            BitsPerPixel = format == "jpg" ? 24 : pixels.Format.BitsPerPixel,
            PixelFormat = format == "jpg" ? "Bgr24" : pixels.Format.ToString()
        };
        BitmapMetadata metadata = new(format);
        if (format == "png") metadata.SetQuery(ColorVisionTiffParameters.PngDescriptionQuery, parameters.ToJson());
        else metadata.Comment = parameters.ToJson();
        BitmapFrame result = BitmapFrame.Create(pixels, null, metadata, null);
        result.Freeze();
        return result;
    }
}
