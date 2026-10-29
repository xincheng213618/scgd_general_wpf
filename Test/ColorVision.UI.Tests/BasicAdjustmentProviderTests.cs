using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class BasicAdjustmentProviderTests
{
    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8)]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    public async Task NonNeutralProviderMatchesTheSeparatePassOracleExactly(AlgorithmImageFormat format)
    {
        using Mat source = CreateGraySource(format);
        byte[] original = PackedBytes(source);
        using AlgorithmImageBuffer input = new(source.Cols, source.Rows, source.Cols * format.BytesPerPixel(), format, original);
        using Mat expected = source.Clone();
        BasicAdjustmentReference.Apply(source, expected, 0.65, -15, 28, 1.8);

        using AlgorithmResult result = await RunAsync(input, NonNeutralParameters());

        AlgorithmImageBuffer output = AssertOutput(result);
        Assert.Equal((source.Cols, source.Rows, source.Cols * format.BytesPerPixel(), format),
            (output.Width, output.Height, output.Stride, output.Format));
        Assert.Equal(PackedBytes(expected), output.Data.ToArray());
        Assert.Equal(PackedBytes(source), input.Data.ToArray());
        AssertIndependentManagedStorage(input, output);
    }

    [Fact]
    public async Task PaddedBgra64InputProducesAnIndependentPackedImageWithAlphaDpiAndOwnedLifetime()
    {
        const int width = 3;
        const int height = 2;
        const int rowBytes = width * 4 * sizeof(ushort);
        const int inputStride = rowBytes + 8;
        ushort[] pixels =
        [
            0, 1, 32767, 7, 32768, 65534, 65535, 32768, 1203, 19997, 40321, 65535,
            65535, 0, 65534, 0, 1, 4095, 32768, 12345, 17, 32767, 60003, 54321,
        ];
        byte[] packed = MemoryMarshal.AsBytes(pixels.AsSpan()).ToArray();
        byte[] padded = Enumerable.Repeat((byte)0xCD, inputStride * height).ToArray();
        for (int row = 0; row < height; row++)
        {
            packed.AsSpan(row * rowBytes, rowBytes).CopyTo(padded.AsSpan(row * inputStride, rowBytes));
        }
        byte[] original = padded.ToArray();
        using AlgorithmImageBuffer input = new(width, height, inputStride, AlgorithmImageFormat.Bgra64, padded, 144, 192);
        using AlgorithmImageBuffer retainedInput = input.RetainReadOnly();
        using Mat source = new(height, width, MatType.CV_16UC4);
        Marshal.Copy(packed, 0, source.Data, packed.Length);
        using Mat expected = source.Clone();
        BasicAdjustmentReference.Apply(source, expected, 0.65, -15, 28, 1.8);
        byte[] expectedBytes = PackedBytes(expected);

        using AlgorithmResult result = await RunAsync(input, NonNeutralParameters());

        AlgorithmImageBuffer output = AssertOutput(result);
        Assert.Equal((width, height, rowBytes, AlgorithmImageFormat.Bgra64, 144d, 192d),
            (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
        Assert.Equal(expectedBytes, output.Data.ToArray());
        Assert.Equal(pixels.Where((_, index) => index % 4 == 3),
            MemoryMarshal.Cast<byte, ushort>(output.Data.Span).ToArray().Where((_, index) => index % 4 == 3));
        Assert.Equal(original, input.Data.ToArray());
        AssertIndependentManagedStorage(input, output);

        input.Dispose();
        Assert.Equal(expectedBytes, output.Data.ToArray());
        result.Dispose();
        Assert.True(output.IsDisposed);
        Assert.Throws<ObjectDisposedException>(() => _ = output.Data);
        Assert.False(retainedInput.IsDisposed);
        Assert.Equal(original, retainedInput.Data.ToArray());
    }

    [Fact]
    public async Task NeutralIntegerProviderStillReturnsIndependentOwnedPixels()
    {
        ushort[] values = [0, 1, 16384, 32767, 32768, 65534, 65535];
        byte[] original = MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
        using AlgorithmImageBuffer input = new(values.Length, 1, original.Length, AlgorithmImageFormat.Gray16, original);

        using AlgorithmResult result = await RunAsync(input, new BasicAdjustmentParameters());

        AlgorithmImageBuffer output = AssertOutput(result);
        Assert.Equal(original, output.Data.ToArray());
        AssertIndependentManagedStorage(input, output);
        result.Dispose();
        Assert.True(output.IsDisposed);
        Assert.False(input.IsDisposed);
        Assert.Equal(original, input.Data.ToArray());
    }

    private static BasicAdjustmentParameters NonNeutralParameters()
        => new() { Exposure = 0.65, Brightness = -15, Contrast = 28, Gamma = 1.8 };

    private static async Task<AlgorithmResult> RunAsync(AlgorithmImageBuffer input, BasicAdjustmentParameters parameters)
    {
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRunner runner = new(StandardAlgorithmCatalog.Create(), [new OpenCvAlgorithmProvider()], scheduler);
        return await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.BasicAdjustment, parameters),
            Inputs = [new AlgorithmInput { Name = "source", Image = input, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
    }

    private static AlgorithmImageBuffer AssertOutput(AlgorithmResult result)
    {
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Empty(result.Failures);
        Assert.Equal("colorvision.opencv.cpu", result.Diagnostics.ProviderId);
        AlgorithmImageArtifact artifact = Assert.IsType<AlgorithmImageArtifact>(Assert.Single(result.Artifacts));
        Assert.Equal("primary", artifact.Role);
        return artifact.Image;
    }

    private static void AssertIndependentManagedStorage(AlgorithmImageBuffer input, AlgorithmImageBuffer output)
    {
        Assert.NotSame(input, output);
        Assert.True(MemoryMarshal.TryGetArray(input.Data, out ArraySegment<byte> inputStorage));
        Assert.True(MemoryMarshal.TryGetArray(output.Data, out ArraySegment<byte> outputStorage));
        Assert.NotSame(inputStorage.Array, outputStorage.Array);
    }

    private static Mat CreateGraySource(AlgorithmImageFormat format)
    {
        if (format == AlgorithmImageFormat.Gray8)
        {
            byte[,] values = new byte[16, 16];
            for (int index = 0; index <= byte.MaxValue; index++) values[index / 16, index % 16] = (byte)index;
            return Mat.FromArray(values);
        }
        if (format == AlgorithmImageFormat.Gray16)
        {
            ushort[,] values = new ushort[256, 256];
            for (int index = 0; index <= ushort.MaxValue; index++) values[index / 256, index % 256] = (ushort)index;
            return Mat.FromArray(values);
        }
        if (format == AlgorithmImageFormat.Gray32Float)
        {
            return Mat.FromArray(new float[,] { { -0.5f, 0, 1e-7f, 0.001f, 0.1f, 0.25f, 0.49999f, 0.5f, 0.9f, 1, 1.5f } });
        }
        throw new ArgumentOutOfRangeException(nameof(format));
    }

    private static byte[] PackedBytes(Mat mat)
    {
        Assert.True(mat.IsContinuous());
        byte[] bytes = new byte[checked((int)(mat.Total() * mat.ElemSize()))];
        Marshal.Copy(mat.Data, bytes, 0, bytes.Length);
        return bytes;
    }
}
