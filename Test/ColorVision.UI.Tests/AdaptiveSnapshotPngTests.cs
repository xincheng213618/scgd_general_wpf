using ColorVision.ImageEditor.Output;
using ColorVision.ImageEditor.Tif;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AdaptiveSnapshotPngTests
{
    [Fact]
    public void ResultPreservesOverlayAlphaAndMetadataAndRejectsUnsampledTranslucency()
    {
        WpfTestHost.Invoke(() =>
        {
            const int width = 4096, height = 3584;
            const int stride = width * sizeof(uint);
            uint[] pixels = new uint[width * height];
            Array.Fill(pixels, 0xFF000000u);
            for (int y = 100; y < 129; y++)
                Array.Fill(pixels, 0xFFE57324u, y * width + 150, 31);
            // WPF can leave α=254 around otherwise opaque result annotations.
            pixels[width + 1] = 0xFEF090F0;
            pixels[43 * width + 15] = 0xFE43E571;
            pixels[222 * width + 26] = 0xFEB757CD;

            string parameters = new ColorVisionTiffParameters { SourceType = "Camera" }.ToJson();
            BitmapSource CreatePixels()
            {
                BitmapSource result = BitmapSource.Create(width, height, 192, 48, PixelFormats.Pbgra32, null, pixels, stride);
                result.Freeze();
                return result;
            }

            // The export passes frozen pixels to the worker, which creates and encodes its own frame.
            AdaptiveSnapshotPng.EncodedPng? Encode(BitmapSource source, bool halfSize) =>
                Task.Run(() => AdaptiveSnapshotPng.TryEncode(
                    ImageSnapshotMetadata.CreateFrame(source, parameters, "png"), halfSize, default)).GetAwaiter().GetResult();

            BitmapSource source = CreatePixels();
            Assert.Null(Encode(source, halfSize: false));
            var adaptive = Assert.IsType<AdaptiveSnapshotPng.EncodedPng>(Encode(source, halfSize: true));
            using MemoryStream adaptiveFile = new();
            adaptive.Write(adaptiveFile);
            using MemoryStream referenceFile = new();
            PngBitmapEncoder wpf = new();
            wpf.Frames.Add(ImageSnapshotMetadata.CreateFrame(source, parameters, "png"));
            wpf.Save(referenceFile);

            BitmapFrame expected = Decode(referenceFile);
            BitmapFrame actual = Decode(adaptiveFile);
            Assert.Equal((expected.PixelWidth, expected.PixelHeight), (actual.PixelWidth, actual.PixelHeight));
            Assert.Equal((expected.DpiX, expected.DpiY), (actual.DpiX, actual.DpiY));
            Assert.Equal(expected.Format, actual.Format);
            Assert.Equal(ReadMetadata(expected), ReadMetadata(actual));
            Assert.True(ColorVisionTiffParameters.TryParse(ReadMetadata(actual), out var record));
            Assert.Equal((width, height), (record!.RenderedOutput!.Width, record.RenderedOutput.Height));

            byte[] expectedPixels = new byte[32 * stride];
            byte[] actualPixels = new byte[expectedPixels.Length];
            for (int y = 0; y < height; y += 32)
            {
                var region = new Int32Rect(0, y, width, 32);
                expected.CopyPixels(region, expectedPixels, stride, 0);
                actual.CopyPixels(region, actualPixels, stride, 0);
                Assert.True(expectedPixels.AsSpan().SequenceEqual(actualPixels), $"Decoded pixels differ at rows {y}–{y + 31}.");
            }

            // This pixel falls outside the sampled rows, so only checking every row rejects it.
            pixels[222 * width + 777] = 0x3F000000;
            Assert.Null(Encode(CreatePixels(), halfSize: true));
        });
    }

    private static BitmapFrame Decode(MemoryStream stream)
    {
        stream.Position = 0;
        return new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
    }

    private static string ReadMetadata(BitmapFrame frame) => Assert.IsType<string>(
        Assert.IsType<BitmapMetadata>(frame.Metadata).GetQuery(ColorVisionTiffParameters.PngDescriptionQuery));
}
