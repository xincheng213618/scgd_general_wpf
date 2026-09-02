using ColorVision.Engine.Media;
using ColorVision.FileIO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvcieSrgbRendererTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void D65WhiteBlackAndSrgbPrimariesRenderInBgrOrder(int bpp)
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateFile(bpp, (0.95047, 1, 1.08883), (0, 0, 0),
                (0.4124564, 0.2126729, 0.0193339), (0.3575761, 0.7151522, 0.1191920), (0.1804375, 0.0721750, 0.9503041));

            WriteableBitmap bitmap = CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.ReferenceWhite, 1);

            Assert.Equal(PixelFormats.Bgr24, bitmap.Format);
            Assert.Equal(5, bitmap.PixelWidth);
            Assert.Equal(1, bitmap.PixelHeight);
            Assert.True(bitmap.IsFrozen);
            Assert.Equal(new byte[] { 255, 255, 255, 0, 0, 0, 0, 0, 255, 0, 255, 0, 255, 0, 0 }, ReadPixels(bitmap));
        });
    }

    [Theory]
    [InlineData(0.001, 3)]
    [InlineData(0.0031308, 10)]
    [InlineData(0.18, 118)]
    [InlineData(0.5, 188)]
    public void ReferenceWhiteUsesSrgbTransferFunction(double linear, byte encoded)
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateFile(32, White(linear));

            byte[] pixels = ReadPixels(CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.ReferenceWhite, 1));

            Assert.Equal(new[] { encoded, encoded, encoded }, pixels);
        });
    }

    [Fact]
    public void AutomaticBrightnessUsesOneScaleForAllPixelsAndComponents()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateFile(32, FromLinearRgb(4, 1, 0.25), FromLinearRgb(1, 0.25, 0.0625));

            byte[] pixels = ReadPixels(CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, double.NaN));

            Assert.Equal(new byte[] { 71, 137, 255, 34, 71, 137 }, pixels);
        });
    }

    [Fact]
    public void AutomaticBrightnessPreservesBlackImage()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateFile(32, (0, 0, 0), (0, 0, 0));

            Assert.Equal(new byte[6], ReadPixels(CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1)));
        });
    }

    [Fact]
    public void FixedReferenceWhitePreservesBrightnessAcrossImagesAndClipsHighlights()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile dim = CreateFile(32, White(25));
            using CVCIEFile bright = CreateFile(32, White(100), White(200));

            Assert.Equal(new byte[] { 137, 137, 137 }, ReadPixels(CvcieSrgbRenderer.Render(dim, CvcieBrightnessMode.ReferenceWhite, 100)));
            Assert.Equal(new byte[] { 255, 255, 255, 255, 255, 255 }, ReadPixels(CvcieSrgbRenderer.Render(bright, CvcieBrightnessMode.ReferenceWhite, 100)));
        });
    }

    [Fact]
    public void RenderingIgnoresExposureAndGainAndDoesNotModifyMeasurementData()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateFile(32, FromLinearRgb(0.5, 0.25, 0.1));
            byte[] originalData = (byte[])file.Data.Clone();
            byte[] baseline = ReadPixels(CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.ReferenceWhite, 1));
            file.Gain = float.NaN;
            file.Exp = [float.PositiveInfinity, -1, 0];

            Assert.Equal(baseline, ReadPixels(CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.ReferenceWhite, 1)));
            Assert.Equal(originalData, file.Data);
            Assert.Equal(new float[] { float.PositiveInfinity, -1, 0 }, file.Exp);
        });
    }

    [Fact]
    public void NegativeXyzAndOutOfGamutComponentsAreClippedOnlyForDisplay()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateFile(32, (-1, 0, 0));

            byte[] pixels = ReadPixels(CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.ReferenceWhite, 1));

            Assert.Equal(0, pixels[0]);
            Assert.InRange(pixels[1], (byte)250, (byte)254);
            Assert.Equal(0, pixels[2]);
        });
    }

    [Theory]
    [InlineData(3, 32, true)]
    [InlineData(3, 64, true)]
    [InlineData(1, 32, false)]
    [InlineData(4, 32, false)]
    [InlineData(3, 8, false)]
    [InlineData(3, 16, false)]
    public void OnlyThreeFloatingPointPlanesAreSupported(int channels, int bpp, bool supported)
    {
        Assert.Equal(supported, CvcieSrgbRenderer.Supports(channels, bpp));
        if (!supported)
        {
            using CVCIEFile file = CreateFile(32, White(1));
            file.Channels = channels;
            file.Bpp = bpp;
            Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1));
        }
    }

    [Theory]
    [InlineData(0, 1)]
    [InlineData(1, 0)]
    [InlineData(-1, -1)]
    [InlineData(int.MaxValue, 2)]
    [InlineData(int.MaxValue, 1)]
    public void InvalidOrOverflowingDimensionsAreRejected(int cols, int rows)
    {
        using CVCIEFile file = CreateFile(32, White(1));
        file.Cols = cols;
        file.Rows = rows;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1));

        Assert.Contains("尺寸", error.Message);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(4)]
    [InlineData(11)]
    [InlineData(13)]
    public void MissingTruncatedOrExtraPayloadIsRejected(int length)
    {
        using CVCIEFile file = CreateFile(32, White(1));
        file.Data = length < 0 ? null! : new byte[length];

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1));

        Assert.Contains("数据长度不匹配", error.Message);
    }

    [Theory]
    [InlineData(32, double.NaN, CvcieBrightnessMode.Auto)]
    [InlineData(32, double.PositiveInfinity, CvcieBrightnessMode.ReferenceWhite)]
    [InlineData(32, double.NegativeInfinity, CvcieBrightnessMode.Auto)]
    [InlineData(64, double.NaN, CvcieBrightnessMode.ReferenceWhite)]
    [InlineData(64, double.PositiveInfinity, CvcieBrightnessMode.Auto)]
    [InlineData(64, double.NegativeInfinity, CvcieBrightnessMode.ReferenceWhite)]
    public void NonFiniteValuesInAnyPlaneAreRejected(int bpp, double invalid, CvcieBrightnessMode mode)
    {
        foreach (var xyz in new[] { (invalid, 1d, 1d), (1d, invalid, 1d), (1d, 1d, invalid) })
        {
            using CVCIEFile file = CreateFile(bpp, White(1), xyz);

            InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, mode, 1));

            Assert.Contains("第 2 个像素", error.Message);
            Assert.Contains("NaN 或无穷值", error.Message);
        }
    }

    [Fact]
    public void FiniteXyzThatOverflowsTheConversionIsRejected()
    {
        using CVCIEFile file = CreateFile(64, (double.MaxValue, 0, 0));

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1));

        Assert.Contains("超出真彩转换范围", error.Message);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    public void ReferenceWhiteMustBePositiveAndFinite(double referenceWhite)
    {
        using CVCIEFile file = CreateFile(32, White(1));

        Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.ReferenceWhite, referenceWhite));
    }

    [Fact]
    public void UnknownBrightnessModeIsRejected()
    {
        using CVCIEFile file = CreateFile(32, White(1));

        Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, (CvcieBrightnessMode)99, 1));
    }

    [Theory]
    [InlineData(32, CvcieBrightnessMode.Auto)]
    [InlineData(64, CvcieBrightnessMode.Auto)]
    [InlineData(32, CvcieBrightnessMode.ReferenceWhite)]
    [InlineData(64, CvcieBrightnessMode.ReferenceWhite)]
    public void ParallelRenderingExactlyMatchesTheOriginalSerialConversion(int bpp, CvcieBrightnessMode mode)
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreateLargeRandomFile(bpp);
            byte[] expected = RenderSerialReference(file, mode, 0.7);

            byte[] actual = ReadPixels(CvcieSrgbRenderer.Render(file, mode, 0.7));

            Assert.Equal(expected, actual);
        });
    }

    [Theory]
    [InlineData(CvcieBrightnessMode.Auto)]
    [InlineData(CvcieBrightnessMode.ReferenceWhite)]
    public void ParallelValidationStillReturnsAReadableDataError(CvcieBrightnessMode mode)
    {
        using CVCIEFile file = CreateLargeRandomFile(32);
        int invalidPixel = 65536 * 8 + 17;
        MemoryMarshal.Cast<byte, float>(file.Data.AsSpan())[invalidPixel] = float.NaN;

        InvalidOperationException error = Assert.Throws<InvalidOperationException>(() => CvcieSrgbRenderer.Render(file, mode, 1));

        Assert.Contains($"第 {invalidPixel + 1} 个像素", error.Message);
        Assert.Contains("NaN 或无穷值", error.Message);
    }

    [Fact]
    public void CancelledRenderingDoesNotCreateABitmap()
    {
        using CVCIEFile file = CreateFile(32, White(1));
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException error = Assert.Throws<OperationCanceledException>(() => CvcieSrgbRenderer.Render(file, CvcieBrightnessMode.Auto, 1, cancellation.Token));

        Assert.Equal(cancellation.Token, error.CancellationToken);
    }

    private static CVCIEFile CreateLargeRandomFile(int bpp)
    {
        const int width = 2048;
        const int height = 513; // Crosses the parallel threshold and ends in a partial chunk.
        byte[] data = new byte[width * height * 3 * (bpp / 8)];
        Random random = new(20260902);
        if (bpp == 32)
        {
            Span<float> planes = MemoryMarshal.Cast<byte, float>(data.AsSpan());
            for (int index = 0; index < planes.Length; index++) planes[index] = (float)(random.NextDouble() * 2 - 0.2);
        }
        else
        {
            Span<double> planes = MemoryMarshal.Cast<byte, double>(data.AsSpan());
            for (int index = 0; index < planes.Length; index++) planes[index] = random.NextDouble() * 2 - 0.2;
        }
        return new CVCIEFile { Rows = height, Cols = width, Channels = 3, Bpp = bpp, Data = data };
    }

    // Independent serial oracle retained from the pre-optimization rendering math.
    private static byte[] RenderSerialReference(CVCIEFile file, CvcieBrightnessMode mode, double referenceWhite)
    {
        int count = file.Cols * file.Rows;
        double divisor = referenceWhite;
        if (mode == CvcieBrightnessMode.Auto)
        {
            double maximum = 0;
            for (int pixel = 0; pixel < count; pixel++)
            {
                var (red, green, blue) = ReadRgb(pixel);
                maximum = Math.Max(maximum, Math.Max(red, Math.Max(green, blue)));
            }
            divisor = maximum > 0 ? maximum : 1;
        }
        byte[] result = new byte[count * 3];
        for (int pixel = 0; pixel < count; pixel++)
        {
            var (red, green, blue) = ReadRgb(pixel);
            result[pixel * 3] = Encode(blue / divisor);
            result[pixel * 3 + 1] = Encode(green / divisor);
            result[pixel * 3 + 2] = Encode(red / divisor);
        }
        return result;

        (double Red, double Green, double Blue) ReadRgb(int pixel)
        {
            int sampleBytes = file.Bpp / 8;
            double x = ReadValue(pixel * sampleBytes);
            double y = ReadValue((count + pixel) * sampleBytes);
            double z = ReadValue((count * 2 + pixel) * sampleBytes);
            return (3.2404542 * x - 1.5371385 * y - 0.4985314 * z,
                -0.9692660 * x + 1.8760108 * y + 0.0415560 * z,
                0.0556434 * x - 0.2040259 * y + 1.0572252 * z);
        }

        double ReadValue(int offset) => file.Bpp == 32 ? BitConverter.ToSingle(file.Data, offset) : BitConverter.ToDouble(file.Data, offset);

        static byte Encode(double linear)
        {
            double clamped = Math.Clamp(linear, 0, 1);
            double encoded = clamped <= 0.0031308 ? 12.92 * clamped : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
        }
    }

    private static (double X, double Y, double Z) White(double luminance) => (0.95047 * luminance, luminance, 1.08883 * luminance);

    private static (double X, double Y, double Z) FromLinearRgb(double red, double green, double blue) => (
        0.4124564 * red + 0.3575761 * green + 0.1804375 * blue,
        0.2126729 * red + 0.7151522 * green + 0.0721750 * blue,
        0.0193339 * red + 0.1191920 * green + 0.9503041 * blue);

    private static CVCIEFile CreateFile(int bpp, params (double X, double Y, double Z)[] values)
    {
        double[] planes = new double[values.Length * 3];
        for (int index = 0; index < values.Length; index++)
        {
            planes[index] = values[index].X;
            planes[values.Length + index] = values[index].Y;
            planes[values.Length * 2 + index] = values[index].Z;
        }
        byte[] data = new byte[planes.Length * (bpp / 8)];
        if (bpp == 32)
            Buffer.BlockCopy(Array.ConvertAll(planes, value => (float)value), 0, data, 0, data.Length);
        else
            Buffer.BlockCopy(planes, 0, data, 0, data.Length);
        return new CVCIEFile { FileExtType = CVType.CIE, Rows = 1, Cols = values.Length, Channels = 3, Bpp = bpp, Data = data, Gain = 1, Exp = [1, 1, 1] };
    }

    private static byte[] ReadPixels(WriteableBitmap bitmap)
    {
        int stride = bitmap.PixelWidth * 3;
        byte[] pixels = new byte[stride * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
