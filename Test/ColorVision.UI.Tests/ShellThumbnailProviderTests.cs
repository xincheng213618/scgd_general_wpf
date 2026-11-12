using ColorVision.FileIO;
using ColorVision.ShellExtension;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class ShellThumbnailProviderTests
{
    [Fact]
    public void CieStreamReadsOnlyXAcrossShortReadsAndRendersAfterInputCloses()
    {
        StaTest.Run(() =>
        {
            byte[] file = CreateCieFile(missingPlanes: false);
            int headerBytes = CVFileUtil.ReadCIEFileHeader(file, out CVCIEFile header);
            using (header)
            {
                var provider = new CVCieShellThumbnailProvider();
                using (var stream = new ShortReadStream(file))
                {
                    Assert.Equal(0, provider.Initialize(stream, 0));
                    Assert.Equal(headerBytes + sizeof(long) + 2 * sizeof(float), stream.BytesRead);
                }
                AssertThumbnail(provider, [255, 255, 255, 0, 0, 0]);
                Assert.NotEqual(0, provider.GetThumbnail(2, out IntPtr consumed, out _));
                Assert.Equal(IntPtr.Zero, consumed);
            }
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void CiePathChecksTheFullLayoutBeforeDisplayingX(bool missingPlanes)
    {
        string path = Path.Combine(Path.GetTempPath(), $"colorvision-shell-thumbnail-{Guid.NewGuid():N}.cvcie");
        try
        {
            File.WriteAllBytes(path, CreateCieFile(missingPlanes));
            StaTest.Run(() =>
            {
                var provider = new CVCieShellThumbnailProvider();
                Assert.Equal(0, provider.Initialize(path, 0));
                if (!missingPlanes) AssertThumbnail(provider, [255, 255, 255, 0, 0, 0]);
                else
                {
                    Assert.NotEqual(0, provider.GetThumbnail(2, out IntPtr bitmap, out _));
                    Assert.Equal(IntPtr.Zero, bitmap);
                }
            });
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void RawStreamRendersAllInterleavedChannelsWithoutReadingExtraPayloadBytes()
    {
        StaTest.Run(() =>
        {
            byte[] pixels = [1, 2, 3, 4, 5, 6];
            using CVCIEFile raw = new()
            {
                Version = 1, Rows = 1, Cols = 2, Channels = 3, Bpp = 8, FileExtType = CVType.Raw,
                Exp = [1, 1, 1], Data = [.. pixels, 100, 101, 102],
            };
            Assert.True(CVFileUtil.WriteCIEFile(raw, out byte[] data));
            var provider = new CVRawShellThumbnailProvider();
            using var stream = new ShortReadStream(data);
            Assert.Equal(0, provider.Initialize(stream, 0));
            Assert.Equal(data.Length - 3, stream.BytesRead);
            AssertThumbnail(provider, pixels);
        });
    }

    [Theory]
    [InlineData(2, 1, 3, 8, 5)]
    [InlineData(14208, 10640, 3, 16, 16)]
    [InlineData(2, 1, 2, 8, 4)]
    [InlineData(2, 1, 5, 8, 10)]
    [InlineData(2, 1, 3, 12, 6)]
    [InlineData(0, 1, 3, 8, 6)]
    [InlineData(int.MaxValue, int.MaxValue, 4, 64, 1)]
    public void RawStreamRejectsInvalidLayoutBeforeReadingPixels(int width, int height, int channels, int bpp, int length)
    {
        using CVCIEFile raw = new()
        {
            Version = 2, Cols = width, Rows = height, Channels = channels, Bpp = bpp, FileExtType = CVType.Raw,
            Exp = new float[channels], Data = new byte[length],
        };
        Assert.True(CVFileUtil.WriteCIEFile(raw, out byte[] data));
        using var stream = new ShortReadStream(data);

        Assert.NotEqual(0, new CVRawShellThumbnailProvider().Initialize(stream, 0));
        Assert.InRange(stream.BytesRead, 1, data.Length - length);
    }

    [Fact]
    public void StreamRejectsAnOverflowingExposureCountBeforeReadingItsValues()
    {
        byte[] data = CreateCieFile(missingPlanes: false);
        // Version 2 with an empty source name has the count after the magic, version, name length and gain.
        const int channelOffset = 5 + 3 * sizeof(int);
        BitConverter.TryWriteBytes(data.AsSpan(channelOffset), 1 << 30);
        using var stream = new ShortReadStream(data);

        Assert.NotEqual(0, new CVCieShellThumbnailProvider().Initialize(stream, 0));
        Assert.Equal(channelOffset + sizeof(int), stream.BytesRead);
    }

    private static void AssertThumbnail(CVThumbnailProviderBase provider, byte[] expected)
    {
        int result = provider.GetThumbnail(2, out IntPtr bitmap, out _);
        try
        {
            Assert.Equal(0, result);
            Assert.NotEqual(IntPtr.Zero, bitmap);
            BitmapSource source = Imaging.CreateBitmapSourceFromHBitmap(bitmap, IntPtr.Zero, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            Assert.Equal((2, 1), (source.PixelWidth, source.PixelHeight));
            var bgr = new FormatConvertedBitmap(source, PixelFormats.Bgr24, null, 0);
            byte[] pixels = new byte[6];
            bgr.CopyPixels(pixels, 6, 0);
            Assert.Equal(expected, pixels);
        }
        finally { if (bitmap != IntPtr.Zero) DeleteObject(bitmap); }
    }

    private static byte[] CreateCieFile(bool missingPlanes)
    {
        float[] samples = missingPlanes ? [2, 1] : [2, 1, 1, 2, 1, 2];
        using CVCIEFile xyz = new()
        {
            Version = 2, Rows = 1, Cols = 2, Channels = 3, Bpp = 32, FileExtType = CVType.CIE,
            Exp = [1, 1, 1], Data = samples.SelectMany(BitConverter.GetBytes).ToArray(),
        };
        Assert.True(CVFileUtil.WriteCIEFile(xyz, out byte[] data));
        return data;
    }

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr bitmap);

    private sealed class ShortReadStream(byte[] bytes) : IStream, IDisposable
    {
        private readonly MemoryStream _stream = new(bytes, writable: false);
        public int BytesRead { get; private set; }

        public void Read(byte[] buffer, int count, IntPtr bytesRead)
        {
            int actual = _stream.Read(buffer, 0, Math.Min(count, 3));
            BytesRead += actual;
            if (bytesRead != IntPtr.Zero) Marshal.WriteInt32(bytesRead, actual);
        }

        public void Seek(long offset, int origin, IntPtr newPosition)
        {
            long position = _stream.Seek(offset, (SeekOrigin)origin);
            if (newPosition != IntPtr.Zero) Marshal.WriteInt64(newPosition, position);
        }

        public void Stat(out STATSTG stat, int flags) => stat = new() { cbSize = _stream.Length };
        public void Dispose() => _stream.Dispose();
        public void Write(byte[] buffer, int count, IntPtr bytesWritten) => throw new NotSupportedException();
        public void SetSize(long value) => throw new NotSupportedException();
        public void CopyTo(IStream stream, long count, IntPtr bytesRead, IntPtr bytesWritten) => throw new NotSupportedException();
        public void Commit(int flags) => throw new NotSupportedException();
        public void Revert() => throw new NotSupportedException();
        public void LockRegion(long offset, long count, int type) => throw new NotSupportedException();
        public void UnlockRegion(long offset, long count, int type) => throw new NotSupportedException();
        public void Clone(out IStream stream) => throw new NotSupportedException();
    }
}
