using ColorVision.ImageEditor.Tif;
using System.IO;
using System.Reflection;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CommonImageOpenDecodeTests
{
    private const int Width = 6;
    private const int Height = 4;
    private const byte Blue = 32;
    private const byte Green = 96;
    private const byte Red = 224;
    private const string Manufacturer = "ColorVision Test Camera";
    private const string Model = "CV-JPEG-1";

    [Fact]
    public void DecodeImageJpegMetadataProbePreservesMetadataAndLoadedPixels()
    {
        string filePath = Path.Combine(Path.GetTempPath(), $"{nameof(CommonImageOpenDecodeTests)}-{Guid.NewGuid():N}.jpg");
        try
        {
            WpfTestHost.Invoke(() => WriteJpeg(filePath));

            DecodedState decoded = WpfTestHost.Invoke(() => Decode(filePath));

            File.Delete(filePath);
            Assert.False(File.Exists(filePath));
            Assert.Equal(Width, decoded.Bitmap.PixelWidth);
            Assert.Equal(Height, decoded.Bitmap.PixelHeight);
            Assert.Equal(Manufacturer, decoded.CameraManufacturer);
            Assert.Equal(Model, decoded.CameraModel);

            BitmapSource bgr = new FormatConvertedBitmap(decoded.Bitmap, PixelFormats.Bgr24, null, 0.0);
            byte[] pixels = new byte[Width * Height * 3];
            bgr.CopyPixels(pixels, Width * 3, 0);
            for (int offset = 0; offset < pixels.Length; offset += 3)
            {
                Assert.InRange(pixels[offset], Blue - 3, Blue + 3);
                Assert.InRange(pixels[offset + 1], Green - 3, Green + 3);
                Assert.InRange(pixels[offset + 2], Red - 3, Red + 3);
            }
        }
        finally
        {
            if (File.Exists(filePath))
                File.Delete(filePath);
        }
    }

    private static void WriteJpeg(string filePath)
    {
        byte[] pixels = new byte[Width * Height * 3];
        for (int offset = 0; offset < pixels.Length; offset += 3)
        {
            pixels[offset] = Blue;
            pixels[offset + 1] = Green;
            pixels[offset + 2] = Red;
        }

        BitmapSource source = BitmapSource.Create(
            Width,
            Height,
            96,
            96,
            PixelFormats.Bgr24,
            null,
            pixels,
            Width * 3);
        BitmapMetadata metadata = new("jpg")
        {
            CameraManufacturer = Manufacturer,
            CameraModel = Model,
        };
        JpegBitmapEncoder encoder = new() { QualityLevel = 100 };
        encoder.Frames.Add(BitmapFrame.Create(source, null, metadata, null));
        using FileStream stream = File.Create(filePath);
        encoder.Save(stream);
    }

    private static DecodedState Decode(string filePath)
    {
        MethodInfo decodeImage = typeof(CommonImageOpen).GetMethod("DecodeImage", BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new MissingMethodException(typeof(CommonImageOpen).FullName, "DecodeImage");
        object decoded = decodeImage.Invoke(null, [filePath])
            ?? throw new InvalidOperationException("DecodeImage returned null.");
        Type decodedType = decoded.GetType();
        BitmapSource bitmap = (BitmapSource)(decodedType.GetProperty("BitmapSource")?.GetValue(decoded)
            ?? throw new InvalidOperationException("Decoded bitmap was missing."));
        object metadata = decodedType.GetProperty("Metadata")?.GetValue(decoded)
            ?? throw new InvalidOperationException("Decoded metadata was missing.");
        Type metadataType = metadata.GetType();
        return new DecodedState(
            bitmap,
            metadataType.GetProperty("CameraManufacturer")?.GetValue(metadata) as string,
            metadataType.GetProperty("CameraModel")?.GetValue(metadata) as string);
    }

    private sealed record DecodedState(BitmapSource Bitmap, string? CameraManufacturer, string? CameraModel);
}
