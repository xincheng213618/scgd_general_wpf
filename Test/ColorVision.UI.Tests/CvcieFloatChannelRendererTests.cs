using ColorVision.Engine.Media;
using ColorVision.FileIO;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvcieFloatChannelRendererTests
{
    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void RenderUsesGrayMinMaxAndLeavesMeasurementDataUnchanged(int bpp)
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreatePlane(bpp, -10, 0, 10);
            byte[] original = (byte[])file.Data.Clone();

            WriteableBitmap bitmap = MediaHelper.RenderFloatChannel(file);

            Assert.Equal(PixelFormats.Gray8, bitmap.Format);
            Assert.True(bitmap.IsFrozen);
            Assert.Equal(new byte[] { 0, 128, 255 }, ReadPixels(bitmap));
            Assert.Equal(original, file.Data);
        });
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void RenderConstantChannelAsBlack(int bpp)
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreatePlane(bpp, 7, 7, 7);

            Assert.Equal(new byte[3], ReadPixels(MediaHelper.RenderFloatChannel(file)));
        });
    }

    [Fact]
    public void RenderHandlesFiniteDoubleExtremesWithoutOverflow()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreatePlane(64, -double.MaxValue, 0, double.MaxValue);

            Assert.Equal(new byte[] { 0, 128, 255 }, ReadPixels(MediaHelper.RenderFloatChannel(file)));
        });
    }

    [Fact]
    public void RenderHandlesSubnormalRangeWithoutInfiniteScale()
    {
        StaTest.Run(() =>
        {
            using CVCIEFile file = CreatePlane(64, 0, double.Epsilon, double.Epsilon * 2);

            Assert.Equal(new byte[] { 0, 128, 255 }, ReadPixels(MediaHelper.RenderFloatChannel(file)));
        });
    }

    [Theory]
    [InlineData(32, double.NaN)]
    [InlineData(32, double.PositiveInfinity)]
    [InlineData(64, double.NegativeInfinity)]
    public void RenderRejectsNonFiniteValues(int bpp, double value)
    {
        using CVCIEFile file = CreatePlane(bpp, 1, value);

        Assert.Throws<InvalidDataException>(() => MediaHelper.RenderFloatChannel(file));
    }

    [Fact]
    public void RenderHonorsCancellationBeforeAllocatingBitmap()
    {
        using CVCIEFile file = CreatePlane(32, 1, 2);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        Assert.Throws<OperationCanceledException>(() => MediaHelper.RenderFloatChannel(file, cancellation.Token));
    }

    [Fact]
    public void RenderRejectsShortArrayForLargeImageBeforeAllocatingOutput()
    {
        using CVCIEFile file = CreatePlane(32, 1);
        file.Cols = 14208;
        file.Rows = 10640;

        Assert.Throws<InvalidDataException>(() => MediaHelper.RenderFloatChannel(file));
    }

    [Theory]
    [InlineData(32)]
    [InlineData(64)]
    public void ParallelRenderMatchesSerialReferenceForEveryPixel(int bpp)
    {
        StaTest.Run(() =>
        {
            double[] values = new double[2048 * 513];
            for (int index = 0; index < values.Length; index++)
                values[index] = ((index * 104729L) % 1000003 - 500001) * 0.125;
            using CVCIEFile file = CreatePlane(bpp, values);
            file.Cols = 2048;
            file.Rows = 513;
            double minimum = values.Min();
            double range = values.Max() - minimum;
            byte[] expected = values.Select(value => (byte)Math.Round(Math.Clamp((value - minimum) / range, 0, 1) * 255)).ToArray();

            WriteableBitmap bitmap = MediaHelper.RenderFloatChannel(file);

            Assert.Equal(expected, ReadPixels(bitmap));
        });
    }

    [Fact]
    public void ParallelRenderKeepsNonFiniteValidationException()
    {
        using CVCIEFile file = new()
        {
            FileExtType = CVType.Raw,
            Rows = 1024, Cols = 1024, Channels = 1, Bpp = 32,
            Data = new byte[1024 * 1024 * sizeof(float)]
        };
        BitConverter.TryWriteBytes(file.Data.AsSpan(file.Data.Length - sizeof(float)), float.NaN);

        Assert.Throws<InvalidDataException>(() => MediaHelper.RenderFloatChannel(file));
    }

    private static CVCIEFile CreatePlane(int bpp, params double[] values)
    {
        byte[] data = new byte[values.Length * (bpp / 8)];
        for (int index = 0; index < values.Length; index++)
        {
            if (bpp == 32) BitConverter.TryWriteBytes(data.AsSpan(index * sizeof(float)), (float)values[index]);
            else BitConverter.TryWriteBytes(data.AsSpan(index * sizeof(double)), values[index]);
        }
        return new CVCIEFile
        {
            FileExtType = CVType.Raw,
            Rows = 1, Cols = values.Length, Channels = 1, Bpp = bpp, Data = data
        };
    }

    private static byte[] ReadPixels(WriteableBitmap bitmap)
    {
        byte[] pixels = new byte[bitmap.PixelWidth * bitmap.PixelHeight];
        bitmap.CopyPixels(pixels, bitmap.PixelWidth, 0);
        return pixels;
    }
}
