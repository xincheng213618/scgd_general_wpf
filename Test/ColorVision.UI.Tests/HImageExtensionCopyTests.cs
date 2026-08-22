using ColorVision.Core;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class HImageExtensionCopyTests
{
    [Theory]
    [InlineData(5, 3, 8)]
    [InlineData(5, 3, 16)]
    public async Task ToHImageCopiesFrozenBitmapIntoPackedOwnedBuffer(int width, int channels, int depth)
    {
        const int height = 4;
        PixelFormat format = depth == 8 ? PixelFormats.Bgr24 : PixelFormats.Rgb48;
        int stride = width * channels * (depth / 8);
        byte[] expected = CreatePixels(height, stride, stride);
        WriteableBitmap bitmap = WpfTestHost.Invoke(() =>
        {
            WriteableBitmap value = new(width, height, 96, 96, format, null);
            value.WritePixels(new Int32Rect(0, 0, width, height), expected, stride, 0);
            value.Freeze();
            return value;
        });

        HImage image = await Task.Run(bitmap.ToHImage);
        try
        {
            Assert.Equal(height, image.rows);
            Assert.Equal(width, image.cols);
            Assert.Equal(channels, image.channels);
            Assert.Equal(depth, image.depth);
            Assert.Equal(stride, image.stride);

            byte[] actual = new byte[expected.Length];
            Marshal.Copy(image.pData, actual, 0, actual.Length);
            Assert.Equal(expected, actual);
        }
        finally
        {
            image.Dispose();
        }
    }

    [Fact]
    public void ToHImageCopiesMutablePaddedRowsIntoPackedOwnedBuffer()
    {
        const int width = 5;
        const int height = 4;
        const int stride = width * 3;
        byte[] expected = CreatePixels(height, stride, stride);

        HImage image = WpfTestHost.Invoke(() =>
        {
            WriteableBitmap bitmap = new(width, height, 96, 96, PixelFormats.Bgr24, null);
            bitmap.WritePixels(new Int32Rect(0, 0, width, height), expected, stride, 0);
            return bitmap.ToHImage();
        });

        try
        {
            Assert.Equal(stride, image.stride);
            byte[] actual = new byte[expected.Length];
            Marshal.Copy(image.pData, actual, 0, actual.Length);
            Assert.Equal(expected, actual);
        }
        finally
        {
            image.Dispose();
        }
    }

    [Theory]
    [InlineData(4, 0)]
    [InlineData(5, 0)]
    [InlineData(4, 3)]
    public async Task UpdateWriteableBitmapAsyncCopiesTightAndPaddedRows(int width, int sourcePadding)
    {
        const int height = 4;
        const int channels = 3;
        const int depth = 8;
        int bytesPerRow = width * channels;
        int sourceStride = bytesPerRow + sourcePadding;
        byte[] source = CreatePixels(height, bytesPerRow, sourceStride);
        WriteableBitmap bitmap = WpfTestHost.Invoke(
            () => new WriteableBitmap(width, height, 96, 96, PixelFormats.Bgr24, null));
        IntPtr buffer = Marshal.AllocCoTaskMem(source.Length);
        Marshal.Copy(source, 0, buffer, source.Length);
        HImage image = new()
        {
            rows = height,
            cols = width,
            channels = channels,
            depth = depth,
            stride = sourceStride,
            isDispose = true,
            pData = buffer
        };

        try
        {
            Assert.True(await HImageExtension.UpdateWriteableBitmapAsync(bitmap, image));

            byte[] actual = WpfTestHost.Invoke(() =>
            {
                byte[] pixels = new byte[height * bytesPerRow];
                bitmap.CopyPixels(pixels, bytesPerRow, 0);
                return pixels;
            });
            Assert.Equal(ExtractActivePixels(source, height, bytesPerRow, sourceStride), actual);
        }
        finally
        {
            Marshal.FreeCoTaskMem(buffer);
        }
    }

    private static byte[] CreatePixels(int rows, int bytesPerRow, int stride)
    {
        byte[] pixels = new byte[rows * stride];
        Array.Fill(pixels, (byte)0xEE);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < bytesPerRow; x++)
            {
                pixels[y * stride + x] = (byte)((y * 41 + x * 7 + 3) % 251);
            }
        }

        return pixels;
    }

    private static byte[] ExtractActivePixels(byte[] pixels, int rows, int bytesPerRow, int stride)
    {
        byte[] activePixels = new byte[rows * bytesPerRow];
        for (int y = 0; y < rows; y++)
        {
            Buffer.BlockCopy(pixels, y * stride, activePixels, y * bytesPerRow, bytesPerRow);
        }

        return activePixels;
    }
}
