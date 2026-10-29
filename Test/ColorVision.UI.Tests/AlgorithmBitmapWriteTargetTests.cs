using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Reflection;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class AlgorithmBitmapWriteTargetTests
{
    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr48)]
    [InlineData(AlgorithmImageFormat.Bgra64)]
    public async Task RetainedInputProducesIndependentWritablePixelsAndTransfersOnlyOnce(AlgorithmImageFormat format)
    {
        int pixelBytes = format.BytesPerPixel();
        int rowBytes = 3 * pixelBytes;
        int sourceStride = rowBytes + 3;
        byte[] sourcePixels = CreatePixels(pixelBytes, sourceStride);
        byte[] sourceBefore = sourcePixels.ToArray();
        byte[] expected = ExpectedRgb(sourcePixels, pixelBytes, sourceStride);
        using AlgorithmImageBuffer source = new(3, 2, sourceStride, format, sourcePixels, 144, 120);
        AlgorithmBitmapWriteTarget target = WpfTestHost.Invoke(() => new AlgorithmBitmapWriteTarget(source));
        AlgorithmImageBufferLease retainedInput = RetainedInput(target);
        try
        {
            source.Dispose();
            Assert.False(retainedInput.IsDisposed);
            await target.FillAsync();
            await Task.Run(() => Assert.Throws<InvalidOperationException>(() => target.TakeBitmap()));
            WriteableBitmap output = WpfTestHost.Invoke(() => target.TakeBitmap());
            Assert.True(retainedInput.IsDisposed);
            WpfTestHost.Invoke(() =>
            {
                Assert.Throws<ObjectDisposedException>(() => target.TakeBitmap());
                Assert.Equal(format == AlgorithmImageFormat.Bgra64 ? PixelFormats.Rgba64 : PixelFormats.Rgb48, output.Format);
                Assert.Equal((3, 2, 144d, 120d), (output.PixelWidth, output.PixelHeight, output.DpiX, output.DpiY));
                Assert.True(output.BackBufferStride >= rowBytes);
                Assert.NotEqual(sourceStride, output.BackBufferStride);
                Assert.False(output.IsFrozen);
                Assert.Equal(expected, ReadPixels(output, rowBytes));
                byte[] replacement = Enumerable.Repeat((byte)0xA5, pixelBytes).ToArray();
                output.WritePixels(new Int32Rect(0, 0, 1, 1), replacement, pixelBytes, 0);
                replacement.CopyTo(expected, 0);
                Assert.Equal(expected, ReadPixels(output, rowBytes));
                Assert.Equal(sourceBefore, sourcePixels);
            });
            await target.DisposeAsync();
            await target.DisposeAsync();
            WpfTestHost.Invoke(() => Assert.Equal(expected, ReadPixels(output, rowBytes)));
        }
        finally
        {
            await target.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr24)]
    [InlineData(AlgorithmImageFormat.Gray32Float)]
    public async Task RowCopyPreservesPaddedColorRowsAndRawFloatBits(AlgorithmImageFormat format)
    {
        int pixelBytes = format.BytesPerPixel();
        int rowBytes = 3 * pixelBytes;
        int sourceStride = format == AlgorithmImageFormat.Bgr24 ? 13 : 17;
        byte[] expected = format == AlgorithmImageFormat.Bgr24
            ? [1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14, 15, 16, 17, 18, 19]
            : [
                0x45, 0x23, 0xC1, 0x7F, // NaN with a noncanonical payload.
                0x00, 0x00, 0xA0, 0xBF, // -1.25.
                0x00, 0x00, 0x00, 0x80, // Negative zero.
                0x21, 0x43, 0xC5, 0xFF, // Negative NaN with a different payload.
                0x00, 0x00, 0x80, 0x7F, // Positive infinity.
                0x00, 0x00, 0x40, 0x3F, // 0.75.
            ];
        byte[] sourcePixels = Enumerable.Repeat((byte)0xCD, sourceStride * 2).ToArray();
        for (int row = 0; row < 2; row++)
            expected.AsSpan(row * rowBytes, rowBytes).CopyTo(sourcePixels.AsSpan(row * sourceStride, rowBytes));
        byte[] sourceBefore = sourcePixels.ToArray();
        using AlgorithmImageBuffer source = new(3, 2, sourceStride, format, sourcePixels, 144, 120);
        AlgorithmBitmapWriteTarget target = WpfTestHost.Invoke(() => new AlgorithmBitmapWriteTarget(source));
        AlgorithmImageBufferLease retainedInput = RetainedInput(target);
        try
        {
            source.Dispose();
            await target.FillAsync();
            WriteableBitmap output = WpfTestHost.Invoke(() => target.TakeBitmap());
            Assert.True(retainedInput.IsDisposed);
            WpfTestHost.Invoke(() =>
            {
                Assert.Equal(format == AlgorithmImageFormat.Bgr24 ? PixelFormats.Bgr24 : PixelFormats.Gray32Float, output.Format);
                Assert.Equal((3, 2, 144d, 120d), (output.PixelWidth, output.PixelHeight, output.DpiX, output.DpiY));
                Assert.NotEqual(sourceStride, output.BackBufferStride);
                Assert.False(output.IsFrozen);
                Assert.Equal(expected, ReadPixels(output, rowBytes));
                byte[] replacement = Enumerable.Repeat((byte)0xA5, pixelBytes).ToArray();
                output.WritePixels(new Int32Rect(0, 0, 1, 1), replacement, pixelBytes, 0);
                replacement.CopyTo(expected, 0);
                Assert.Equal(expected, ReadPixels(output, rowBytes));
                Assert.Equal(sourceBefore, sourcePixels);
            });
        }
        finally
        {
            await target.DisposeAsync();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CancellationRejectsTransferAndDisposalReleasesTheRetainedInput(bool cancelBeforeFill)
    {
        using AlgorithmImageBuffer source = new(3, 2, 21, AlgorithmImageFormat.Bgr48, CreatePixels(6, 21));
        AlgorithmBitmapWriteTarget target = WpfTestHost.Invoke(() => new AlgorithmBitmapWriteTarget(source));
        AlgorithmImageBufferLease retainedInput = RetainedInput(target);
        using CancellationTokenSource cancellation = new();
        try
        {
            source.Dispose();
            if (cancelBeforeFill)
            {
                cancellation.Cancel();
                await Assert.ThrowsAnyAsync<OperationCanceledException>(() => target.FillAsync(cancellation.Token));
                WpfTestHost.Invoke(() => Assert.Throws<InvalidOperationException>(() => target.TakeBitmap()));
            }
            else
            {
                await target.FillAsync(cancellation.Token);
                cancellation.Cancel();
                WpfTestHost.Invoke(() => Assert.ThrowsAny<OperationCanceledException>(() => target.TakeBitmap(cancellation.Token)));
            }
            await target.DisposeAsync();
            await target.DisposeAsync();
            Assert.True(retainedInput.IsDisposed);
            WpfTestHost.Invoke(() => Assert.Throws<ObjectDisposedException>(() => target.TakeBitmap()));
        }
        finally
        {
            await target.DisposeAsync();
        }
    }

    [Fact]
    public async Task DisposalWaitsForTheStartedFillAndItsTaskDoesNotReturnTheBitmap()
    {
        Assert.Equal(typeof(Task), typeof(AlgorithmBitmapWriteTarget).GetMethod(nameof(AlgorithmBitmapWriteTarget.FillAsync))!.ReturnType);
        using AlgorithmImageBuffer source = new(3, 2, 21, AlgorithmImageFormat.Bgr48, CreatePixels(6, 21));
        AlgorithmBitmapWriteTarget target = WpfTestHost.Invoke(() => new AlgorithmBitmapWriteTarget(source));
        AlgorithmImageBufferLease retainedInput = RetainedInput(target);
        try
        {
            source.Dispose();
            Task fill = target.FillAsync();
            // A tiny image may already have finished. The contract is the terminal state
            // after cleanup, rather than a timing-dependent assertion about overlap.
            await target.DisposeAsync();
            Assert.True(fill.IsCompleted);
            Assert.True(retainedInput.IsDisposed);
            WpfTestHost.Invoke(() => Assert.Throws<ObjectDisposedException>(() => target.TakeBitmap()));
        }
        finally
        {
            await target.DisposeAsync();
        }
    }

    private static AlgorithmImageBufferLease RetainedInput(AlgorithmBitmapWriteTarget target)
        => Assert.IsType<AlgorithmImageBufferLease>(typeof(AlgorithmBitmapWriteTarget)
            .GetField("_source", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(target));

    private static byte[] CreatePixels(int pixelBytes, int stride)
    {
        byte[] pixels = Enumerable.Repeat((byte)0xCD, stride * 2).ToArray();
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 3; column++)
                for (int value = 0; value < pixelBytes; value++)
                    pixels[row * stride + column * pixelBytes + value] = (byte)(row * 70 + column * 17 + value + 1);
        return pixels;
    }

    private static byte[] ExpectedRgb(byte[] source, int pixelBytes, int sourceStride)
    {
        byte[] pixels = new byte[3 * pixelBytes * 2];
        for (int row = 0; row < 2; row++)
            for (int column = 0; column < 3; column++)
            {
                int input = row * sourceStride + column * pixelBytes;
                int output = (row * 3 + column) * pixelBytes;
                source.AsSpan(input + 4, 2).CopyTo(pixels.AsSpan(output, 2));
                source.AsSpan(input + 2, 2).CopyTo(pixels.AsSpan(output + 2, 2));
                source.AsSpan(input, 2).CopyTo(pixels.AsSpan(output + 4, 2));
                if (pixelBytes == 8) source.AsSpan(input + 6, 2).CopyTo(pixels.AsSpan(output + 6, 2));
            }
        return pixels;
    }

    private static byte[] ReadPixels(BitmapSource source, int stride)
    {
        byte[] pixels = new byte[stride * source.PixelHeight];
        source.CopyPixels(pixels, stride, 0);
        return pixels;
    }
}
