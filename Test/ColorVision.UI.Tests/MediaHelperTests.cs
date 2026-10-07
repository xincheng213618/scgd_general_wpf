using ColorVision.Engine.Media;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public class MediaHelperTests
{
    [Theory]
    [InlineData(8, 1)]
    [InlineData(8, 3)]
    [InlineData(8, 4)]
    [InlineData(16, 1)]
    [InlineData(16, 3)]
    [InlineData(16, 4)]
    [InlineData(32, 1)]
    public void DirectDisplayCopyPreservesPixelsChannelOrderAndSourceWithPaddedRows(int depth, int channels)
    {
        WpfTestHost.Invoke(() =>
        {
            MatType type = depth switch { 8 => MatType.CV_8UC(channels), 16 => MatType.CV_16UC(channels), _ => MatType.CV_32FC1 };
            byte[] data = new byte[5 * 4 * channels * (depth / 8)];
            for (int i = 0; i < 5 * 4 * channels; i++)
            {
                if (depth == 8) data[i] = (byte)(i * 13);
                else if (depth == 16) BitConverter.GetBytes((ushort)(i * 947)).CopyTo(data, i * 2);
                else BitConverter.GetBytes(i * 0.1234567f).CopyTo(data, i * 4);
            }
            byte[] original = (byte[])data.Clone();
            using Mat parent = Mat.FromPixelData(4, 5, type, data);
            using Mat source = new(parent, new Rect(1, 1, 3, 2));
            Assert.False(source.IsContinuous());
            // The old converter truncates rows of non-contiguous Gray16/Gray32F Mats.
            // Compare against a packed reference and independently compute the exact row/channel bytes.
            using Mat packed = source.Clone();
            WriteableBitmap reference = packed.ToWriteableBitmap();
            WriteableBitmap actual = source.CreateDisplayBitmap();
            Assert.Equal(reference.Format, actual.Format);
            Assert.Equal(reference.DpiX, actual.DpiX);
            Assert.Equal(reference.DpiY, actual.DpiY);
            Assert.False(actual.IsFrozen);
            int stride = 3 * channels * (depth / 8);
            byte[] expected = new byte[stride * 2], pixels = new byte[stride * 2];
            reference.CopyPixels(expected, stride, 0);
            byte[] exact = new byte[expected.Length];
            for (int row = 0; row < 2; row++)
            for (int column = 0; column < 3; column++)
            for (int channel = 0; channel < channels; channel++)
            {
                int sourceChannel = depth == 16 && channels >= 3 && channel < 3 ? 2 - channel : channel;
                int from = (((row + 1) * 5 + column + 1) * channels + sourceChannel) * (depth / 8);
                int to = ((row * 3 + column) * channels + channel) * (depth / 8);
                Buffer.BlockCopy(original, from, exact, to, depth / 8);
            }
            Assert.Equal(exact, expected);
            actual.CopyPixels(pixels, stride, 0);
            Assert.Equal(expected, pixels);
            Assert.Equal(original, data);

            // Updating an existing bitmap must use exactly the same format and channel convention.
            actual.WritePixels(new System.Windows.Int32Rect(0, 0, 3, 2), new byte[pixels.Length], stride, 0);
            Assert.True(source.MatUpdateWriteableBitmap(actual));
            actual.CopyPixels(pixels, stride, 0);
            Assert.Equal(expected, pixels);
            if (depth == 16 && channels >= 3)
            {
                int offset = (5 + 1) * channels * 2;
                Assert.Equal(BitConverter.ToUInt16(original, offset + 4), BitConverter.ToUInt16(pixels, 0));
                Assert.Equal(BitConverter.ToUInt16(original, offset + 2), BitConverter.ToUInt16(pixels, 2));
                Assert.Equal(BitConverter.ToUInt16(original, offset), BitConverter.ToUInt16(pixels, 4));
                if (channels == 4) Assert.Equal(BitConverter.ToUInt16(original, offset + 6), BitConverter.ToUInt16(pixels, 6));
            }
        });
    }

    [Fact]
    public void MatUpdateWriteableBitmapRejectsMatchingFrozenTargetWithoutChangingPixels()
    {
        WpfTestHost.Invoke(() =>
        {
            using Mat source = new(2, 2, MatType.CV_8UC1, Scalar.All(200));
            WriteableBitmap bitmap = new(2, 2, 96, 96, PixelFormats.Gray8, null);
            bitmap.WritePixels(new System.Windows.Int32Rect(0, 0, 2, 2), new byte[] { 10, 11, 12, 13 }, 2, 0);
            bitmap.Freeze();

            Assert.False(source.MatUpdateWriteableBitmap(bitmap));

            byte[] pixels = new byte[4];
            bitmap.CopyPixels(pixels, 2, 0);
            Assert.Equal(new byte[] { 10, 11, 12, 13 }, pixels);
        });
    }

    [Fact]
    public void MatUpdateWriteableBitmapRejectsSameByteCountWithDifferentFormats()
    {
        using Mat floatGray = new(2, 2, MatType.CV_32FC1, Scalar.All(1));
        using Mat byteBgra = new(2, 2, MatType.CV_8UC4, Scalar.All(1));
        WriteableBitmap bgraBitmap = new(2, 2, 96, 96, PixelFormats.Bgra32, null);
        WriteableBitmap floatBitmap = new(2, 2, 96, 96, PixelFormats.Gray32Float, null);

        Assert.False(floatGray.MatUpdateWriteableBitmap(bgraBitmap));
        Assert.False(byteBgra.MatUpdateWriteableBitmap(floatBitmap));
    }

    [Fact]
    public void MatUpdateWriteableBitmapReusesExactFormatAndSize()
    {
        using Mat source = new(2, 2, MatType.CV_8UC4, new Scalar(1, 2, 3, 4));
        WriteableBitmap bitmap = new(2, 2, 96, 96, PixelFormats.Bgra32, null);

        Assert.True(source.MatUpdateWriteableBitmap(bitmap));
    }
}
