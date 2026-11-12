using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class PseudoColorProviderTests
{
    [Theory]
    [InlineData(AlgorithmImageFormat.Gray8)]
    [InlineData(AlgorithmImageFormat.Gray16)]
    [InlineData(AlgorithmImageFormat.Bgr24)]
    [InlineData(AlgorithmImageFormat.Bgr48)]
    [InlineData(AlgorithmImageFormat.Bgra32)]
    [InlineData(AlgorithmImageFormat.Bgra64)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    [InlineData(AlgorithmImageFormat.Bgr96Float)]
    [InlineData(AlgorithmImageFormat.Bgra128Float)]
    public async Task PaddedSourcesMatchExistingColorSemanticsAndKeepIndependentOutput(AlgorithmImageFormat format)
    {
        using AlgorithmImageBuffer input = CreateInput(format);
        byte[] original = input.Data.ToArray();
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input);
        OpenCvAlgorithmProvider provider = new();
        foreach (int channel in new[] { -1, format.Channels() - 1, 3 }.Distinct())
        foreach (PseudoColorParameters parameters in Parameters(channel))
        {
            using Mat expected = PseudoColorReference.Apply(source.Mat, parameters);
            using AlgorithmResult result = await RunAsync(provider, input, parameters);
            AlgorithmImageBuffer output = Output(result);
            Assert.Equal((input.Width, input.Height, input.Width * 3, AlgorithmImageFormat.Bgr24, input.DpiX, input.DpiY),
                (output.Width, output.Height, output.Stride, output.Format, output.DpiX, output.DpiY));
            Assert.Equal(Bytes(expected), output.Data.ToArray());
            Assert.Equal(original, input.Data.ToArray());
            Assert.True(MemoryMarshal.TryGetArray(input.Data, out ArraySegment<byte> sourceArray));
            Assert.True(MemoryMarshal.TryGetArray(output.Data, out ArraySegment<byte> outputArray));
            Assert.NotSame(sourceArray.Array, outputArray.Array);
            using AlgorithmImageBuffer retained = output.RetainReadOnly();
            result.Dispose();
            Assert.True(output.IsDisposed);
            Assert.Equal(Bytes(expected), retained.Data.ToArray());
        }
    }

    [Fact]
    public async Task EveryUshortValuePreservesTwoStageRoundingAndThresholdBoundaries()
    {
        ushort[] values = Enumerable.Range(0, 65536).Select(x => (ushort)x).ToArray();
        using AlgorithmImageBuffer input = new(256, 256, 512, AlgorithmImageFormat.Gray16, MemoryMarshal.AsBytes(values.AsSpan()).ToArray());
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input);
        OpenCvAlgorithmProvider provider = new();
        foreach (PseudoColorParameters p in Parameters(-1))
        {
            using Mat expected = PseudoColorReference.Apply(source.Mat, p);
            using AlgorithmResult result = await RunAsync(provider, input, p);
            Assert.Equal(Bytes(expected), Output(result).Data.ToArray());
        }
    }

    [Fact]
    public async Task EverySupportedColorMapMatchesTheExistingPalette()
    {
        using AlgorithmImageBuffer input = new(256, 1, 256, AlgorithmImageFormat.Gray8, Enumerable.Range(0, 256).Select(x => (byte)x).ToArray());
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input);
        OpenCvAlgorithmProvider provider = new();
        foreach (StandardPseudoColorMap map in Enum.GetValues<StandardPseudoColorMap>())
        {
            PseudoColorParameters p = new() { Colormap = map };
            using Mat expected = PseudoColorReference.Apply(source.Mat, p);
            using AlgorithmResult result = await RunAsync(provider, input, p);
            Assert.Equal(Bytes(expected), Output(result).Data.ToArray());
        }
    }

    [Fact]
    public void CacheUsesSnapshotIdentityAndEvictionDoesNotInvalidateActiveReaders()
    {
        PseudoColorProcessor processor = new();
        using AlgorithmImageBuffer input = CreateInput(AlgorithmImageFormat.Bgr48);
        using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input);
        using AlgorithmImageBuffer first = processor.AcquireGray(source.Mat, input, -1, default)!;
        using AlgorithmImageBuffer retainedInput = input.RetainReadOnly();
        using AlgorithmImageBuffer hit = processor.AcquireGray(source.Mat, retainedInput, -1, default)!;
        Assert.Equal(first.StorageId, hit.StorageId);
        byte[] expected = first.Data.ToArray();

        Assert.Null(processor.AcquireGray(source.Mat, input, 1, default));
        using AlgorithmImageBuffer afterChannelChange = processor.AcquireGray(source.Mat, input, -1, default)!;
        Assert.NotEqual(first.StorageId, afterChannelChange.StorageId);
        Assert.Equal(expected, first.Data.ToArray());

        using AlgorithmImageBuffer replacement = CreateInput(AlgorithmImageFormat.Bgr48, seed: 81);
        using AlgorithmImageMatLease replacementSource = AlgorithmImageInterop.BorrowReadOnly(replacement);
        using AlgorithmImageBuffer changed = processor.AcquireGray(replacementSource.Mat, replacement, -1, default)!;
        using Mat expectedChanged = OpenCvAlgorithmProvider.ConvertToGray(replacementSource.Mat);
        Assert.Equal(Bytes(expectedChanged), changed.Data.ToArray());
        Assert.NotEqual(afterChannelChange.StorageId, changed.StorageId);
        Assert.Equal(expected, first.Data.ToArray());

        PseudoColorProcessor capped = new(cacheByteLimit: 1);
        Assert.Null(capped.AcquireGray(source.Mat, input, -1, default));
        using AlgorithmImageBuffer fallback = capped.Execute(source.Mat, input, new(), default);
        using Mat fallbackExpected = PseudoColorReference.Apply(source.Mat, new());
        Assert.Equal(Bytes(fallbackExpected), fallback.Data.ToArray());
    }

    [Fact]
    public async Task ConcurrentDocumentsAndChannelsDoNotSharePixelsOrResults()
    {
        OpenCvAlgorithmProvider provider = new();
        using AlgorithmImageBuffer first = CreateInput(AlgorithmImageFormat.Bgr48, seed: 3);
        using AlgorithmImageBuffer second = CreateInput(AlgorithmImageFormat.Bgr48, seed: 53);
        await Task.WhenAll(new[] { (first, -1), (second, -1), (first, 2), (second, 1) }.Select(pair => Task.Run(async () =>
        {
            PseudoColorParameters p = new() { UseNominalRange = false, Minimum = 6000, Maximum = 40000, Channel = pair.Item2 };
            using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(pair.Item1);
            using Mat expected = PseudoColorReference.Apply(source.Mat, p);
            using AlgorithmResult result = await RunAsync(provider, pair.Item1, p);
            Assert.Equal(Bytes(expected), Output(result).Data.ToArray());
        })));
    }

    [Fact]
    public void PreCancelledRenderLeavesCallerOwnedDestinationAndInputUntouched()
    {
        using Mat source = new(4, 7, MatType.CV_16UC3, Scalar.All(12500));
        using Mat destination = new(4, 7, MatType.CV_8UC3, Scalar.All(37));
        byte[] before = Bytes(destination);
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();
        Assert.Throws<OperationCanceledException>(() => PseudoColorProcessor.Render(source, destination, new(), cancellation.Token));
        Assert.Equal(before, Bytes(destination));
        Assert.Equal(12500, source.At<Vec3w>(0, 0).Item0);
    }

    private static IEnumerable<PseudoColorParameters> Parameters(int channel)
    {
        yield return new() { Channel = channel };
        yield return new() { Channel = channel, UseNominalRange = false, Maximum = 65535 };
        yield return new() { Channel = channel, UseNominalRange = false, Minimum = 5000, Maximum = 40000 };
        yield return new() { Channel = channel, UseNominalRange = false, Minimum = 6003, Maximum = 40009, AutoRange = true, DataMinimum = 1001, DataMaximum = 50003 };
        yield return new() { Channel = channel, UseNominalRange = false, Minimum = 128, Maximum = 128 };
    }

    private static async Task<AlgorithmResult> RunAsync(OpenCvAlgorithmProvider provider, AlgorithmImageBuffer input, PseudoColorParameters p)
    {
        using AlgorithmExecutionScheduler scheduler = new(cpuConcurrency: 1);
        AlgorithmRunner runner = new(StandardAlgorithmCatalog.Create(), [provider], scheduler);
        return await runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.PseudoColor, p),
            Inputs = [new AlgorithmInput { Name = "source", Image = input }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
    }

    private static AlgorithmImageBuffer Output(AlgorithmResult result)
    {
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        Assert.Empty(result.Failures);
        return Assert.IsType<AlgorithmImageArtifact>(Assert.Single(result.Artifacts)).Image;
    }

    private static AlgorithmImageBuffer CreateInput(AlgorithmImageFormat format, int seed = 17)
    {
        const int width = 19, height = 7;
        int stride = width * format.BytesPerPixel() + 8;
        byte[] bytes = Enumerable.Repeat((byte)0xCD, stride * height).ToArray();
        for (int y = 0; y < height; y++)
        {
            Span<byte> row = bytes.AsSpan(y * stride, width * format.BytesPerPixel());
            for (int i = 0; i < width * format.Channels(); i++)
            {
                int value = (i * 113 + y * 257 + seed) * 19;
                if (format.BitsPerChannel() == 8) row[i] = (byte)value;
                else if (format.BitsPerChannel() == 16) MemoryMarshal.Cast<byte, ushort>(row)[i] = (ushort)value;
                else MemoryMarshal.Cast<byte, float>(row)[i] = (value % 137 - 17) / 71f;
            }
        }
        return new(width, height, stride, format, bytes, 144, 120);
    }

    private static byte[] Bytes(Mat mat)
    {
        byte[] result = new byte[checked((int)(mat.Total() * mat.ElemSize()))];
        Marshal.Copy(mat.Data, result, 0, result.Length);
        return result;
    }
}
