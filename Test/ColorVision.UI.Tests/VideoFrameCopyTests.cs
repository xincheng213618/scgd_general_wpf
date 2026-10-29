using ColorVision.Core;
using ColorVision.ImageEditor.Video;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class VideoFrameCopyTests
{
    [Fact]
    public void PrepareFrameCopiesContiguousFrames()
    {
        StaTest.Run(() =>
        {
            AssertCopy(width: 4, height: 3, PixelFormats.Bgr24, channels: 3, depth: 8, sourcePadding: 0, expectTargetPadding: false);
            AssertCopy(width: 3, height: 3, PixelFormats.Bgr32, channels: 4, depth: 8, sourcePadding: 0, expectTargetPadding: false);
            AssertCopy(width: 2, height: 3, PixelFormats.Rgb48, channels: 3, depth: 16, sourcePadding: 0, expectTargetPadding: false);
        });
    }

    [Fact]
    public void PrepareFrameCopiesRowsWithSourcePadding()
    {
        StaTest.Run(() => AssertCopy(width: 2, height: 3, PixelFormats.Bgr32, channels: 4, depth: 8, sourcePadding: 5, expectTargetPadding: false));
    }

    [Fact]
    public void PrepareFrameCopiesRowsWithTargetPadding()
    {
        StaTest.Run(() => AssertCopy(width: 1, height: 3, PixelFormats.Bgr24, channels: 3, depth: 8, sourcePadding: 0, expectTargetPadding: true));
    }

    [Fact]
    public void PrepareFrameRejectsInvalidStrideBeforeCopying()
    {
        StaTest.Run(() =>
        {
            const int width = 2, height = 2, bytesPerRow = width * 3;
            IntPtr data = Marshal.AllocCoTaskMem(bytesPerRow * height);
            try
            {
                HImage image = new() { rows = height, cols = width, channels = 3, depth = 8, stride = bytesPerRow - 1, isDispose = true, pData = data };
                Assert.Throws<ArgumentException>(() => VideoPlaybackSession.PrepareFrame(image));
            }
            finally { Marshal.FreeCoTaskMem(data); }
        });
    }

    private static void AssertCopy(int width, int height, PixelFormat format, int channels, int depth, int sourcePadding, bool expectTargetPadding)
    {
        int bytesPerRow = width * channels * (depth / 8);
        int sourceStride = bytesPerRow + sourcePadding;
        byte[] source = CreateSource(height, bytesPerRow, sourceStride);
        WriteableBitmap bitmap;
        IntPtr data = Marshal.AllocCoTaskMem(source.Length);
        try
        {
            Marshal.Copy(source, 0, data, source.Length);
            HImage image = new() { rows = height, cols = width, channels = channels, depth = depth, stride = sourceStride, isDispose = true, pData = data };
            bitmap = VideoPlaybackSession.PrepareFrame(image);
        }
        finally { Marshal.FreeCoTaskMem(data); }

        Assert.True(bitmap.IsFrozen);
        Assert.Equal(format, bitmap.Format);
        Assert.Equal(expectTargetPadding, bitmap.BackBufferStride > bytesPerRow);
        if (!expectTargetPadding) Assert.Equal(bytesPerRow, bitmap.BackBufferStride);
        byte[] actual = new byte[bitmap.BackBufferStride * height];
        bitmap.CopyPixels(actual, bitmap.BackBufferStride, 0);
        Assert.Equal(ExtractActivePixels(source, height, bytesPerRow, sourceStride), ExtractActivePixels(actual, height, bytesPerRow, bitmap.BackBufferStride));
    }
    private static byte[] CreateSource(int rows, int bytesPerRow, int stride)
    {
        byte[] source = new byte[rows * stride];
        Array.Fill(source, (byte)0xEE);
        for (int y = 0; y < rows; y++)
        {
            for (int x = 0; x < bytesPerRow; x++)
            {
                source[y * stride + x] = (byte)((y * 41 + x * 7 + 3) % 251);
            }
        }

        return source;
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
