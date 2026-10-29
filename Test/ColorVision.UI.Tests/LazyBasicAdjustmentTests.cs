using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Runtime.InteropServices;

namespace ColorVision.UI.Tests;

public sealed class LazyBasicAdjustmentTests
{
    [Theory]
    [InlineData(0d)]
    [InlineData(5d)]
    public void PreCancelledTokenDoesNotCreateDestination(double brightness)
    {
        using Mat source = CreateSource("Gray16");
        byte[] original = PackedBytes(source);
        using ExternalDestination output = new();
        using CancellationTokenSource cancellation = new();
        cancellation.Cancel();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            OpenCvImageAlgorithms.AdjustBasicTo(source, () => output.Create(source), 0, brightness, 0, 1, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(0, output.FactoryCalls);
        Assert.Null(output.Data);
        Assert.Null(output.Destination);
        Assert.Equal(original, PackedBytes(source));
    }

    [Fact]
    public void UnsupportedSourceDepthDoesNotCreateDestination()
    {
        using Mat source = new(2, 3, MatType.CV_8SC1, Scalar.All(7));
        byte[] original = PackedBytes(source);
        using ExternalDestination output = new();

        Assert.Throws<NotSupportedException>(() =>
            OpenCvImageAlgorithms.AdjustBasicTo(source, () => output.Create(source), 0.65, -15, 28, 1.8));

        Assert.Equal(0, output.FactoryCalls);
        Assert.Null(output.Data);
        Assert.Null(output.Destination);
        Assert.Equal(original, PackedBytes(source));
    }

    [Theory]
    [InlineData("Gray16", true)]
    [InlineData("Gray16", false)]
    [InlineData("Bgra64", false)]
    public void SuccessCreatesDestinationOnceAndPreservesItsExternalStorage(string format, bool identity)
    {
        using Mat source = CreateSource(format);
        byte[] original = PackedBytes(source);
        MatType sourceType = source.Type();
        int rows = source.Rows;
        int columns = source.Cols;
        double exposure = identity ? 0 : 0.65;
        double brightness = identity ? 0 : -15;
        double contrast = identity ? 0 : 28;
        double gamma = identity ? 1 : 1.8;
        using Mat expected = source.Clone();
        BasicAdjustmentReference.Apply(source, expected, exposure, brightness, contrast, gamma);
        byte[] expectedBytes = PackedBytes(expected);
        using ExternalDestination output = new();

        OpenCvImageAlgorithms.AdjustBasicTo(source, () => output.Create(source), exposure, brightness, contrast, gamma);

        Assert.Equal(1, output.FactoryCalls);
        Mat destination = Assert.IsType<Mat>(output.Destination);
        Assert.Equal((rows, columns, sourceType), (destination.Rows, destination.Cols, destination.Type()));
        Assert.Equal(output.Pointer, destination.Data);
        Assert.Equal((long)output.Stride, destination.Step());
        Assert.Equal(expectedBytes, PackedBytes(destination));
        Assert.Equal(expectedBytes, output.Data);
        Assert.Equal(original, PackedBytes(source));
        if (source.Channels() == 4)
        {
            ushort[] sourceValues = MemoryMarshal.Cast<byte, ushort>(original.AsSpan()).ToArray();
            ushort[] outputValues = MemoryMarshal.Cast<byte, ushort>(output.Data!.AsSpan()).ToArray();
            Assert.Equal(sourceValues.Where((_, index) => index % 4 == 3),
                outputValues.Where((_, index) => index % 4 == 3));
        }

        output.Dispose();
        Assert.True(destination.IsDisposed);
        Assert.Equal(expectedBytes, output.Data);
    }

    [Fact]
    public void CancellationInsideFactoryLeavesDestinationUnwrittenAndOwnedByCaller()
    {
        using Mat source = CreateSource("Gray16");
        byte[] original = PackedBytes(source);
        using ExternalDestination output = new();
        using CancellationTokenSource cancellation = new();

        OperationCanceledException exception = Assert.Throws<OperationCanceledException>(() =>
            OpenCvImageAlgorithms.AdjustBasicTo(source, () =>
            {
                Mat destination = output.Create(source);
                cancellation.Cancel();
                return destination;
            }, 0.65, -15, 28, 1.8, cancellation.Token));

        Assert.Equal(cancellation.Token, exception.CancellationToken);
        Assert.Equal(1, output.FactoryCalls);
        Mat destination = Assert.IsType<Mat>(output.Destination);
        Assert.False(destination.IsDisposed);
        Assert.Equal(output.Pointer, destination.Data);
        Assert.Equal((long)output.Stride, destination.Step());
        Assert.Equal(Enumerable.Repeat(ExternalDestination.UnwrittenValue, output.Data!.Length).ToArray(), output.Data);
        Assert.Equal(original, PackedBytes(source));

        output.Dispose();
        Assert.True(destination.IsDisposed);
    }

    [Fact]
    public void FactoryFailureLeavesSourceReadableAndUnchanged()
    {
        using Mat source = CreateSource("Bgra64");
        byte[] original = PackedBytes(source);
        InvalidOperationException expected = new("Destination creation failed.");
        int factoryCalls = 0;

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            OpenCvImageAlgorithms.AdjustBasicTo(source, () =>
            {
                factoryCalls++;
                throw expected;
            }, 0.65, -15, 28, 1.8));

        Assert.Same(expected, exception);
        Assert.Equal(1, factoryCalls);
        Assert.False(source.IsDisposed);
        Assert.Equal(original, PackedBytes(source));
    }

    private static Mat CreateSource(string format)
    {
        if (format == "Gray16")
            return Mat.FromArray(new ushort[,] { { 0, 1, 32767 }, { 32768, 65534, 65535 } });
        if (format == "Bgra64")
        {
            ushort[] values =
            [
                0, 1, 32767, 7, 32768, 65534, 65535, 32768, 1203, 19997, 40321, 65535,
                65535, 0, 65534, 0, 1, 4095, 32768, 12345, 17, 32767, 60003, 54321,
            ];
            Mat source = new(2, 3, MatType.CV_16UC4);
            byte[] bytes = MemoryMarshal.AsBytes(values.AsSpan()).ToArray();
            Marshal.Copy(bytes, 0, source.Data, bytes.Length);
            return source;
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

    private sealed class ExternalDestination : IDisposable
    {
        public const byte UnwrittenValue = 0xCD;
        private GCHandle _pin;

        public int FactoryCalls { get; private set; }
        public byte[]? Data { get; private set; }
        public Mat? Destination { get; private set; }
        public IntPtr Pointer { get; private set; }
        public int Stride { get; private set; }

        public Mat Create(Mat source)
        {
            FactoryCalls++;
            if (Destination != null) throw new InvalidOperationException("The destination was created more than once.");
            Stride = checked(source.Cols * (int)source.ElemSize());
            Data = Enumerable.Repeat(UnwrittenValue, checked(Stride * source.Rows)).ToArray();
            _pin = GCHandle.Alloc(Data, GCHandleType.Pinned);
            try
            {
                Pointer = _pin.AddrOfPinnedObject();
                Destination = Mat.FromPixelData(source.Rows, source.Cols, source.Type(), Pointer, Stride);
                return Destination;
            }
            catch
            {
                _pin.Free();
                throw;
            }
        }

        public void Dispose()
        {
            Destination?.Dispose();
            if (_pin.IsAllocated) _pin.Free();
        }
    }
}
