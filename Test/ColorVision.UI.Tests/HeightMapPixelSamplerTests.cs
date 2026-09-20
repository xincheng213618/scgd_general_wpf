using ColorVision.ImageEditor;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class HeightMapPixelSamplerTests
{
    private static readonly Type SamplerType = typeof(Window3D).Assembly.GetType(
        "ColorVision.ImageEditor.EditorTools.ThreeD.HeightMapPixelSampler", throwOnError: true)!;
    private static readonly MethodInfo Sample = SamplerType.GetMethod("Sample", BindingFlags.Public | BindingFlags.Static)!;

    [Fact]
    public void CalculateFitSize_PreservesAspectRatioWithinConfiguredBounds()
    {
        Type samplerType = typeof(Window3D).Assembly.GetType(
            "ColorVision.ImageEditor.EditorTools.ThreeD.HeightMapPixelSampler",
            throwOnError: true)!;
        MethodInfo calculateFitSize = samplerType.GetMethod(
            "CalculateFitSize",
            BindingFlags.NonPublic | BindingFlags.Static)!;

        var size = ((int Width, int Height))calculateFitSize.Invoke(null, [5544, 3692, 512, 512])!;

        Assert.Equal(512, size.Width);
        Assert.Equal(341, size.Height);
    }

    [Fact]
    public void ConvertBitmapToGray_DoesNotUpscaleSmallOpaqueImages()
    {
        StaTest.Run(() =>
        {
            byte[] source =
            [
                0, 10, 20, 30,
                40, 50, 60, 70,
                80, 90, 100, 110,
            ];
            WriteableBitmap bitmap = CreateBitmap(4, 3, PixelFormats.Gray8, source, 4);

            var sample = InvokeSampler(bitmap, 512, 512);

            Assert.Equal(4, sample.Width);
            Assert.Equal(3, sample.Height);
            Assert.Equal(source, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_UsesEdgeAlignedBilinearSampling()
    {
        StaTest.Run(() =>
        {
            byte[] source =
            [
                0, 10, 20, 30,
                40, 50, 60, 70,
                80, 90, 100, 110,
            ];
            WriteableBitmap bitmap = CreateBitmap(4, 3, PixelFormats.Gray8, source, 4);

            var sample = InvokeSampler(bitmap, 3, 2);

            Assert.Equal(3, sample.Width);
            Assert.Equal(2, sample.Height);
            Assert.Equal(new byte[] { 0, 15, 30, 80, 95, 110 }, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_PreservesStraightColorAndAlphaSemantics()
    {
        StaTest.Run(() =>
        {
            byte[] source =
            [
                0, 0, 255, 255,
                0, 255, 0, 0,
                255, 0, 0, 128,
                255, 255, 255, 255,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Bgra32, source, 8);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 76, 150, 29, 255 }, sample.Gray);
            Assert.Equal(new byte[] { 255, 0, 128, 255 }, sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_HandlesBgr32AsOpaqueColor()
    {
        StaTest.Run(() =>
        {
            byte[] source =
            [
                0, 0, 255, 0,
                0, 255, 0, 0,
                255, 0, 0, 0,
                255, 255, 255, 0,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Bgr32, source, 8);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 76, 150, 29, 255 }, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_HandlesRgb48WithoutAFullSizeIntermediate()
    {
        StaTest.Run(() =>
        {
            byte[] source =
            [
                0x00, 0x00, 0x00, 0x00, 0x00, 0x00,
                0xFF, 0xFF, 0xFF, 0xFF, 0xFF, 0xFF,
                0x80, 0x80, 0x80, 0x80, 0x80, 0x80,
                0x40, 0x40, 0x40, 0x40, 0x40, 0x40,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Rgb48, source, 12);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 0, 255, 128, 64 }, sample.Gray);
            Assert.Null(sample.Alpha);
        });
    }

    [Fact]
    public void ConvertBitmapToGray_UnpremultipliesPbgra32BeforeSampling()
    {
        StaTest.Run(() =>
        {
            byte[] source =
            [
                25, 50, 100, 128,
                25, 50, 100, 128,
                25, 50, 100, 128,
                25, 50, 100, 128,
            ];
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Pbgra32, source, 8);

            var sample = InvokeSampler(bitmap, 2, 2);

            Assert.Equal(new byte[] { 123, 123, 123, 123 }, sample.Gray);
            Assert.Equal(new byte[] { 128, 128, 128, 128 }, sample.Alpha);
        });
    }

    [Theory]
    [InlineData("Gray8")]
    [InlineData("Gray16")]
    [InlineData("Gray32Float")]
    [InlineData("Bgr24")]
    [InlineData("Bgr32")]
    [InlineData("Bgra32")]
    [InlineData("Pbgra32")]
    [InlineData("Rgb48")]
    public void Sample_MatchesDisplayConversionAndRoundedLumaBeforeInterpolation(string formatName)
    {
        StaTest.Run(() =>
        {
            PixelFormat format = (PixelFormat)typeof(PixelFormats).GetProperty(formatName)!.GetValue(null)!;
            const int width = 73;
            const int height = 61;
            int stride = width * format.BitsPerPixel / 8;
            byte[] source = new byte[stride * height];
            new Random(731).NextBytes(source);
            if (format == PixelFormats.Gray32Float)
            {
                // The existing height contract converts the display source through WPF;
                // it does not introduce physical-unit values or a new invalid-data mapping.
                float[] values = [float.NaN, float.NegativeInfinity, -1, 0, 0.25f, 0.5f, 0.75f, 1, 2, float.PositiveInfinity];
                for (int index = 0; index < width * height; index++)
                    BitConverter.GetBytes(values[index % values.Length]).CopyTo(source, index * sizeof(float));
            }
            WriteableBitmap bitmap = CreateBitmap(width, height, format, source, stride);

            foreach ((int maxWidth, int maxHeight) in new[] { (73, 61), (37, 31), (19, 11), (2, 2) })
            {
                var actual = InvokeSampler(bitmap, maxWidth, maxHeight);
                var expected = ReferenceSample(bitmap, actual.Width, actual.Height);
                Assert.Equal(expected.Gray, actual.Gray);
                Assert.Equal(expected.Alpha, actual.Alpha);
            }
        });
    }

    [Fact]
    public void Sample_PreservesFullyTransparentColorValuesAndMask()
    {
        StaTest.Run(() =>
        {
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Bgra32,
                [0, 0, 255, 0, 0, 255, 0, 0, 255, 0, 0, 0, 255, 255, 255, 0], 8);
            var sample = InvokeSampler(bitmap, 2, 2);
            Assert.Equal(new byte[] { 76, 150, 29, 255 }, sample.Gray);
            Assert.Equal(new byte[] { 0, 0, 0, 0 }, sample.Alpha);
        });
    }

    [Fact]
    public void Sample_PreservesSingletonDuplicationForMeshCompatibility()
    {
        StaTest.Run(() =>
        {
            WriteableBitmap bitmap = CreateBitmap(1, 3, PixelFormats.Gray8, [0, 128, 255], 1);
            var sample = InvokeSampler(bitmap, 512, 512);
            Assert.Equal(2, sample.Width);
            Assert.Equal(3, sample.Height);
            Assert.Equal(new byte[] { 0, 0, 128, 128, 255, 255 }, sample.Gray);
        });
    }

    [Fact]
    public void Sample_CopiesAcrossDenseStripBoundaryWithoutChangingPixels()
    {
        StaTest.Run(() =>
        {
            const int width = 1025;
            const int height = 1031;
            byte[] pixels = new byte[width * height * 4];
            new Random(17).NextBytes(pixels);
            WriteableBitmap bitmap = CreateBitmap(width, height, PixelFormats.Bgra32, pixels, width * 4);
            var sample = InvokeSampler(bitmap, width, height);
            var expected = ReferenceSample(bitmap, width, height);
            Assert.Equal(expected.Gray, sample.Gray);
            Assert.Equal(expected.Alpha, sample.Alpha);
        });
    }

    [Fact]
    public void Sample_ObservesCancellationBeforeReadingPixels()
    {
        StaTest.Run(() =>
        {
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            WriteableBitmap bitmap = CreateBitmap(2, 2, PixelFormats.Gray8, [0, 64, 128, 255], 2);
            TargetInvocationException error = Assert.Throws<TargetInvocationException>(() =>
                Sample.Invoke(null, [bitmap, 2, 2, cancellation.Token]));
            Assert.IsType<OperationCanceledException>(error.InnerException);
        });
    }

    private static (byte[] Gray, byte[]? Alpha, int Width, int Height) InvokeSampler(
        WriteableBitmap bitmap,
        int maxWidth,
        int maxHeight)
    {
        object sample = Sample.Invoke(null, [bitmap, maxWidth, maxHeight, CancellationToken.None])!;
        Type resultType = sample.GetType();
        return ((byte[])resultType.GetProperty("Gray")!.GetValue(sample)!,
            (byte[]?)resultType.GetProperty("Alpha")!.GetValue(sample),
            (int)resultType.GetProperty("Width")!.GetValue(sample)!,
            (int)resultType.GetProperty("Height")!.GetValue(sample)!);
    }

    // Independent full-copy reference to the documented conversion and edge-aligned sampling.
    private static (byte[] Gray, byte[]? Alpha) ReferenceSample(BitmapSource bitmap, int width, int height)
    {
        var converted = new FormatConvertedBitmap(bitmap, PixelFormats.Bgra32, null, 0);
        int stride = bitmap.PixelWidth * 4;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        converted.CopyPixels(pixels, stride, 0);
        byte[] gray = new byte[width * height];
        byte[] alpha = new byte[gray.Length];
        for (int y = 0; y < height; y++)
        {
            double sourceY = y * (double)(bitmap.PixelHeight - 1) / Math.Max(height - 1, 1);
            int top = (int)sourceY;
            int bottom = Math.Min(top + 1, bitmap.PixelHeight - 1);
            for (int x = 0; x < width; x++)
            {
                double sourceX = x * (double)(bitmap.PixelWidth - 1) / Math.Max(width - 1, 1);
                int left = (int)sourceX;
                int right = Math.Min(left + 1, bitmap.PixelWidth - 1);
                double xf = sourceX - left;
                double yf = sourceY - top;
                (int Offset, double Weight)[] neighbors =
                [
                    (top * stride + left * 4, (1 - xf) * (1 - yf)),
                    (top * stride + right * 4, xf * (1 - yf)),
                    (bottom * stride + left * 4, (1 - xf) * yf),
                    (bottom * stride + right * 4, xf * yf),
                ];
                double luminance = 0;
                double opacity = 0;
                foreach (var (offset, weight) in neighbors)
                {
                    luminance += Math.Round(pixels[offset] * 0.114 + pixels[offset + 1] * 0.587 + pixels[offset + 2] * 0.299) * weight;
                    opacity += pixels[offset + 3] * weight;
                }
                gray[y * width + x] = (byte)Math.Clamp(Math.Round(luminance), 0, 255);
                alpha[y * width + x] = (byte)Math.Clamp(Math.Round(opacity), 0, 255);
            }
        }
        return (gray, alpha.All(value => value == byte.MaxValue) ? null : alpha);
    }

    private static WriteableBitmap CreateBitmap(
        int width,
        int height,
        PixelFormat format,
        byte[] pixels,
        int stride)
    {
        WriteableBitmap bitmap = new(width, height, 96, 96, format, null);
        bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
        return bitmap;
    }
}
