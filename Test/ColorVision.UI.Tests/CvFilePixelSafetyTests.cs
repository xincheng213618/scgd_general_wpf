using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using OpenCvSharp;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.UI.Tests;

public sealed class CvFilePixelSafetyTests
{
    [Theory]
    [InlineData(8, 1)]
    [InlineData(16, 1)]
    [InlineData(8, 3)]
    [InlineData(16, 3)]
    [InlineData(8, 4)]
    public void FlowBitmapDecodePreservesTightlyPackedNativePixels(int bpp, int sourceChannels)
    {
        StaTest.Run(() =>
        {
            const int width = 3, height = 2;
            PixelFormat format = (bpp, sourceChannels) switch
            {
                (8, 1) => PixelFormats.Gray8,
                (16, 1) => PixelFormats.Gray16,
                (8, 3) => PixelFormats.Bgr24,
                (16, 3) => PixelFormats.Rgb48,
                _ => PixelFormats.Bgra32
            };
            int sourcePixelBytes = sourceChannels * (bpp / 8);
            int targetChannels = Math.Min(sourceChannels, 3);
            int targetPixelBytes = targetChannels * (bpp / 8);
            byte[] source = new byte[width * height * sourcePixelBytes];
            byte[] expected = new byte[width * height * targetPixelBytes];
            for (int pixel = 0; pixel < width * height; pixel++)
            {
                int input = pixel * sourcePixelBytes, output = pixel * targetPixelBytes;
                for (int i = 0; i < sourcePixelBytes; i++) source[input + i] = (byte)(11 + input + i);
                if (format == PixelFormats.Bgra32) source[input + 3] = byte.MaxValue;
                if (format == PixelFormats.Rgb48)
                {
                    for (int channel = 0; channel < 3; channel++)
                        Array.Copy(source, input + 2 * channel, expected, output + 2 * (2 - channel), 2);
                }
                else Array.Copy(source, input, expected, output, targetPixelBytes);
            }

            bool png = format == PixelFormats.Bgra32;
            string path = Path.Combine(Path.GetTempPath(), $"FlowBitmapPixels_{Guid.NewGuid():N}.{(png ? "png" : "tif")}");
            try
            {
                BitmapEncoder encoder = png ? new PngBitmapEncoder() : new TiffBitmapEncoder { Compression = TiffCompressOption.None };
                BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, format, null, source, width * sourcePixelBytes);
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream output = File.Create(path)) encoder.Save(output);

                using LocalFlowFrame frame = LocalFrameFileService.Load(path, [17], 1.25f);
                using LocalFlowFrameLease lease = frame.Acquire();
                Assert.Equal((width, height, bpp, targetChannels),
                    (frame.Metadata.Width, frame.Metadata.Height, frame.Metadata.SourceBpp, frame.Metadata.Channels));
                Assert.Equal(new float[] { 17 }, frame.Metadata.Exposure);
                Assert.Equal(1.25f, frame.Metadata.Gain);
                Assert.Equal(expected, lease.CopyRawToArray());
            }
            finally { File.Delete(path); }
        });
    }

    [Fact]
    public void FlowBitmapDecodeDoesNotAllocateAFrameSizedManagedBuffer()
    {
        StaTest.Run(() =>
        {
            const int width = 2049, height = 1025, stride = width * sizeof(ushort), length = stride * height;
            string path = Path.Combine(Path.GetTempPath(), $"FlowBitmapAllocation_{Guid.NewGuid():N}.tif");
            try
            {
                BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Gray16, null, new byte[length], stride);
                TiffBitmapEncoder encoder = new() { Compression = TiffCompressOption.None };
                encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (FileStream output = File.Create(path)) encoder.Save(output);
                using (LocalFlowFrame warmup = LocalFrameFileService.Load(path)) { }

                long before = GC.GetAllocatedBytesForCurrentThread();
                using LocalFlowFrame frame = LocalFrameFileService.Load(path);
                long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
                using LocalFlowFrameLease lease = frame.Acquire();
                Assert.Equal(length, lease.RawLength);
                Assert.True(allocated < length, $"Allocated {allocated} managed bytes for a {length}-byte image.");
            }
            finally { File.Delete(path); }
        });
    }

    [Theory]
    [InlineData(1u)]
    [InlineData(2u)]
    [InlineData(3u)]
    public void DiskCachedAndByteWritersRoundTripVersionedHeaderAndPixels(uint version)
    {
        string name = $"CieVersionLayout_{Guid.NewGuid():N}";
        string diskPath = Path.Combine(Path.GetTempPath(), name + ".cvcie");
        string cachePath = Path.Combine(Path.GetTempPath(), name + ".cvraw");
        bool previousCache = CVFileReadCache.IsEnabled;
        using CVCIEFile fixture = new()
        {
            Version = version, NDPort = -17, SrcFileName = "原图.cvraw", Gain = 1.75f,
            Cols = 4, Rows = 2, Channels = 3, Bpp = 8, Exp = [2, 3],
            Data = Enumerable.Range(1, 24).Select(value => (byte)value).ToArray()
        };
        try
        {
            CVFileReadCache.IsEnabled = true;
            Assert.True(CVFileUtil.WriteCIEFile(fixture, out byte[] encoded));
            Assert.True(CVFileUtil.WriteCIEFile(diskPath, fixture));
            Assert.True(CVFileUtil.WriteCVRaw(cachePath, fixture, fixture.Data.Length,
                stream => stream.Write(fixture.Data), CVFileSaveMode.MemoryOnly));
            Assert.Equal(encoded, File.ReadAllBytes(diskPath));
            Assert.False(File.Exists(cachePath));
            using (Stream cache = CVFileReadCache.OpenRead(cachePath, populateCache: false))
            {
                Assert.Equal(encoded.LongLength, cache.Length);
                byte[] cached = new byte[encoded.Length];
                cache.ReadExactly(cached);
                Assert.Equal(encoded, cached);
            }

            Assert.True(CVFileUtil.Read(encoded, out CVCIEFile fromBytes));
            AssertImage(fromBytes);
            Assert.True(CVFileUtil.Read(diskPath, out CVCIEFile fromDisk));
            AssertImage(fromDisk);
            Assert.True(CVFileUtil.Read(cachePath, out CVCIEFile fromCache));
            AssertImage(fromCache);
        }
        finally
        {
            CVFileReadCache.IsEnabled = previousCache;
            File.Delete(diskPath);
        }

        void AssertImage(CVCIEFile image)
        {
            using (image)
            {
                Assert.Equal(version, image.Version);
                Assert.Equal(version == 3 ? fixture.NDPort : 0, image.NDPort);
                Assert.Equal(fixture.SrcFileName, image.SrcFileName);
                Assert.Equal(fixture.Gain, image.Gain);
                Assert.Equal((fixture.Cols, fixture.Rows, fixture.Channels, fixture.Bpp),
                    (image.Cols, image.Rows, image.Channels, image.Bpp));
                Assert.Equal(new float[] { 2, 3, 0 }, image.Exp);
                Assert.Equal(fixture.Data, image.Data);
            }
        }
    }

    [Theory]
    [InlineData(0u)]
    [InlineData(4u)]
    public void UnsupportedVersionLeavesExistingRawAndCacheIntact(uint version)
    {
        string path = Path.Combine(Path.GetTempPath(), $"CieUnsupportedVersion_{Guid.NewGuid():N}.cvraw");
        bool previousCache = CVFileReadCache.IsEnabled;
        using CVCIEFile fixture = new() { Version = 2, Cols = 2, Rows = 1, Channels = 1, Bpp = 8, Data = [1, 2] };
        try
        {
            CVFileReadCache.IsEnabled = true;
            Assert.True(CVFileUtil.WriteCVRaw(path, fixture));
            byte[] original = File.ReadAllBytes(path);
            fixture.Version = version;
            bool callbackRan = false;
            void WriteData(Stream stream) { callbackRan = true; stream.Write(fixture.Data); }

            Assert.False(CVFileUtil.WriteCVRaw(path, fixture, fixture.Data.Length, WriteData));
            Assert.False(CVFileUtil.WriteCVRaw(path, fixture, fixture.Data.Length, WriteData, CVFileSaveMode.MemoryOnly));
            Assert.False(callbackRan);
            Assert.False(CVFileUtil.WriteCIEFile(fixture, out byte[] encoded));
            Assert.Null(encoded);
            Assert.Equal(original, File.ReadAllBytes(path));
            Assert.Equal(original.LongLength, CVFileReadCache.GetCachedLength(path));
            using Stream cache = CVFileReadCache.OpenRead(path, populateCache: false);
            byte[] cached = new byte[original.Length];
            cache.ReadExactly(cached);
            Assert.Equal(original, cached);
        }
        finally
        {
            CVFileReadCache.IsEnabled = previousCache;
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(CVType.Raw)]
    [InlineData(CVType.CIE)]
    public void DisplayRejectsLargeDimensionsWithShortArrayBeforeNativeAccess(CVType fileType)
    {
        using CVCIEFile file = new()
        {
            FileExtType = fileType,
            Cols = 14208,
            Rows = 10640,
            Bpp = 32,
            Channels = fileType == CVType.CIE ? 3 : 1,
            Data = new byte[4]
        };

        Assert.Null(file.ToMat(showErrors: false));
        Assert.Null(file.ToWriteableBitmap(showErrors: false));
    }

    [Theory]
    [InlineData(0, 1, 8, 1, 1)]
    [InlineData(1, 0, 8, 1, 1)]
    [InlineData(1, 1, 24, 1, 3)]
    [InlineData(1, 1, 8, 0, 1)]
    [InlineData(1, 1, 8, 2, 2)]
    [InlineData(1, 1, 8, 1, 2)]
    [InlineData(int.MaxValue, int.MaxValue, 64, 4, 1)]
    public void DisplayRejectsInvalidLayoutsAndOversizedPayload(int rows, int cols, int bpp, int channels, int length)
    {
        using CVCIEFile file = new()
        {
            FileExtType = CVType.Raw,
            Rows = rows, Cols = cols, Bpp = bpp, Channels = channels, Data = new byte[length]
        };

        Assert.Null(file.ToMat(showErrors: false));
        Assert.Null(file.ToWriteableBitmap(showErrors: false));
    }

    [Fact]
    public void ThreePlaneCieRequiresAllDeclaredPlanesBeforeNativeAccess()
    {
        using CVCIEFile file = new()
        {
            FileExtType = CVType.CIE,
            Rows = 1, Cols = 2, Bpp = 32, Channels = 3,
            Data = new byte[2 * sizeof(float)]
        };

        Assert.Null(file.ToMat(showErrors: false));
        Assert.Null(file.ToWriteableBitmap(showErrors: false));
    }

    [Theory]
    [InlineData(1u, 0)]
    [InlineData(1u, 1)]
    [InlineData(1u, 2)]
    [InlineData(2u, 0)]
    [InlineData(2u, 1)]
    [InlineData(2u, 2)]
    public void LegacyChannelReaderReturnsCompleteSelectedPlane(uint version, int channel)
    {
        string path = CreateFixture(version);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, channel, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(0, status);
                Assert.Equal(CVType.Raw, plane.FileExtType);
                Assert.Equal(1, plane.Channels);
                Assert.Equal(2, plane.Cols);
                Assert.Equal(1, plane.Rows);
                Assert.Equal(2 * sizeof(float), plane.Data.Length);
                Assert.Equal(channel * 2 + 1f, BitConverter.ToSingle(plane.Data, 0));
                Assert.Equal(channel * 2 + 2f, BitConverter.ToSingle(plane.Data, sizeof(float)));
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderRejectsTruncatedPayloadWithoutReturningPartialPixels()
    {
        string path = CreateFixture(2);
        try
        {
            using (FileStream stream = new(path, FileMode.Open, FileAccess.Write))
                stream.SetLength(stream.Length - 1);

            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderRejectsLargeMetadataWithTinyPayloadWithoutAllocatingImage()
    {
        string path = CreateFixture(2, cols: 14208, rows: 10640);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(3)]
    public void LegacyChannelReaderRejectsInvalidChannel(int channel)
    {
        string path = CreateFixture(1);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, channel, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderKeepsHeaderFailureReturnCode()
    {
        string path = Path.GetTempFileName();
        try
        {
            File.WriteAllBytes(path, [67, 86, 67, 73, 69]);
            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-1, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void LegacyChannelReaderKeepsSingleChannelFailureReturnCode()
    {
        string path = CreateFixture(1, channels: 1);
        try
        {
            int status = CVFileUtil.ReadCVCIEXYZ(path, 0, out CVCIEFile plane);
            using (plane)
            {
                Assert.Equal(-2, status);
                Assert.Null(plane.Data);
            }
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CvcieThumbnailUsesEmbeddedXOnlyAfterValidatingTheCompletePayload(bool missingPlanes)
    {
        string basePath = Path.Combine(Path.GetTempPath(), $"CieThumbnail_{Guid.NewGuid():N}");
        string ciePath = basePath + ".cvcie";
        string rawPath = basePath + ".cvraw";
        try
        {
            using CVCIEFile raw = new()
            {
                Version = 2, Cols = 2, Rows = 1, Bpp = 8, Channels = 1, FileExtType = CVType.Raw,
                Exp = [1], Data = [0, 255],
            };
            Assert.True(CVFileUtil.WriteCIEFile(rawPath, raw));
            float[] samples = missingPlanes ? [2, 1] : [2, 1, 1, 2, 1, 2];
            using CVCIEFile cie = new()
            {
                Version = 2, Cols = 2, Rows = 1, Bpp = 32, Channels = 3, FileExtType = CVType.CIE,
                SrcFileName = Path.GetFileName(rawPath), Exp = [1, 1, 1], Data = samples.SelectMany(BitConverter.GetBytes).ToArray(),
            };
            Assert.True(CVFileUtil.WriteCIEFile(ciePath, cie));

            var provider = new CVRawThumbnailProvider();
            BitmapSource? thumbnail = await WpfTestHost.Invoke(() => provider.GenerateThumbnailAsync(ciePath, 384));
            if (missingPlanes)
            {
                Assert.Null(thumbnail);
                return;
            }

            Assert.NotNull(thumbnail);
            Assert.True(thumbnail.IsFrozen);
            Assert.Equal(PixelFormats.Gray8, thumbnail.Format);
            Assert.Equal((2, 1), (thumbnail.PixelWidth, thumbnail.PixelHeight));
            byte[] pixels = new byte[2];
            thumbnail.CopyPixels(pixels, 2, 0);
            Assert.Equal(new byte[] { 255, 0 }, pixels);
        }
        finally
        {
            File.Delete(ciePath);
            File.Delete(rawPath);
        }
    }

    [Theory]
    [InlineData("varied")]
    [InlineData("constant")]
    [InlineData("nonfinite")]
    public async Task LargeCvcieThumbnailMatchesLegacyAreaResizeWithTheUiDispatcherBlocked(string input)
    {
        const int width = 311, height = 233, thumbWidth = 97, thumbHeight = 72;
        const int planePixels = width * height;
        float[] samples = new float[3 * planePixels];
        for (int y = 0; y < height; y++)
        for (int x = 0; x < width; x++)
        {
            int pattern = (x * 7919 + y * 7381 + x * y * 13) % 10007 - 5003;
            samples[y * width + x] = pattern * 0.03125f + (y - x) * 0.0001f;
        }
        if (input == "constant") Array.Fill(samples, 12.5f, 0, planePixels);
        if (input == "nonfinite")
        {
            samples[7] = float.NaN;
            samples[width + 1] = float.PositiveInfinity;
            samples[planePixels - 3] = float.NegativeInfinity;
        }
        Array.Fill(samples, 1000f, planePixels, planePixels);
        Array.Fill(samples, -1000f, 2 * planePixels, planePixels);
        byte[] data = new byte[samples.Length * sizeof(float)];
        Buffer.BlockCopy(samples, 0, data, 0, data.Length);

        using Mat source = Mat.FromPixelData(height, width, MatType.CV_32FC1, samples);
        Cv2.Normalize(source, source, 0, 255, NormTypes.MinMax);
        source.ConvertTo(source, MatType.CV_8U);
        using Mat expected = new();
        Cv2.Resize(source, expected, new OpenCvSharp.Size(thumbWidth, thumbHeight), 0, 0, InterpolationFlags.Area);
        byte[] expectedPixels = new byte[thumbWidth * thumbHeight];
        Marshal.Copy(expected.Data, expectedPixels, 0, expectedPixels.Length);

        string path = Path.Combine(Path.GetTempPath(), $"CieThumbnail_{Guid.NewGuid():N}.cvcie");
        try
        {
            using CVCIEFile file = new()
            {
                Version = 2, Cols = width, Rows = height, Bpp = 32, Channels = 3, FileExtType = CVType.CIE,
                Exp = [1, 1, 1], Data = data,
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));

            var dispatcher = WpfTestHost.Invoke(() => Application.Current.Dispatcher);
            using ManualResetEventSlim release = new();
            TaskCompletionSource entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Task blocked = dispatcher.InvokeAsync(() =>
            {
                entered.SetResult();
                if (!release.Wait(TimeSpan.FromSeconds(30))) throw new TimeoutException("UI dispatcher gate was not released.");
            }).Task;
            Task<BitmapSource?>? generation = null;
            TimeSpan testWait = TimeSpan.FromSeconds(10);
            try
            {
                await entered.Task.WaitAsync(testWait);
                generation = new CVRawThumbnailProvider().GenerateThumbnailAsync(path, thumbWidth);
                BitmapSource? thumbnail = await generation.WaitAsync(testWait);

                Assert.NotNull(thumbnail);
                Assert.True(thumbnail.IsFrozen);
                Assert.Equal(PixelFormats.Gray8, thumbnail.Format);
                Assert.Equal((thumbWidth, thumbHeight), (thumbnail.PixelWidth, thumbnail.PixelHeight));
                byte[] pixels = new byte[expectedPixels.Length];
                thumbnail.CopyPixels(pixels, thumbWidth, 0);
                Assert.Equal(expectedPixels, pixels);
            }
            finally
            {
                release.Set();
                await blocked.WaitAsync(testWait);
                if (generation != null) await generation.WaitAsync(testWait);
            }
        }
        finally { File.Delete(path); }
    }

    [Theory]
    [InlineData(".CVRAW", false)]
    [InlineData(".cvraw", true)]
    public async Task RawThumbnailPreservesInterleavedBgrAndRejectsShortPayload(string extension, bool truncated)
    {
        string path = Path.Combine(Path.GetTempPath(), $"RawThumbnail_{Guid.NewGuid():N}{extension}");
        try
        {
            byte[] bgr = [11, 42, 73, 21, 52, 83, 31, 62, 93, 41, 72, 103];
            using CVCIEFile file = new()
            {
                Version = 2, Cols = 2, Rows = 2, Bpp = 8, Channels = 3, FileExtType = CVType.Raw,
                Exp = [1, 1, 1], Data = truncated ? bgr[..^1] : bgr,
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));

            var provider = new CVRawThumbnailProvider();
            BitmapSource? thumbnail = await WpfTestHost.Invoke(() => provider.GenerateThumbnailAsync(path, 2));
            if (truncated)
            {
                Assert.Null(thumbnail);
                return;
            }

            Assert.NotNull(thumbnail);
            Assert.True(thumbnail.IsFrozen);
            Assert.Equal(PixelFormats.Bgr24, thumbnail.Format);
            Assert.Equal((2, 2), (thumbnail.PixelWidth, thumbnail.PixelHeight));
            byte[] actual = new byte[bgr.Length];
            thumbnail.CopyPixels(actual, 2 * 3, 0);
            Assert.Equal(bgr, actual);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public async Task RawThumbnailAndDimensionsUseDiskWhenTheOpenedCacheStillHasOlderPixels()
    {
        string path = Path.Combine(Path.GetTempPath(), $"RawThumbnailRevision_{Guid.NewGuid():N}.cvraw");
        bool previousCache = CVFileReadCache.IsEnabled;
        try
        {
            CVFileReadCache.IsEnabled = true;
            using CVCIEFile original = new() { Version = 2, Cols = 2, Rows = 2, Channels = 1, Bpp = 8, Data = [11, 12, 13, 14] };
            Assert.True(CVFileUtil.WriteCVRaw(path, original));
            Assert.NotNull(CVFileReadCache.GetCachedLength(path));
            using CVCIEFile replacement = new() { Version = 2, Cols = 3, Rows = 2, Channels = 1, Bpp = 8, Data = [81, 82, 83, 84, 85, 86] };
            Assert.True(CVFileUtil.WriteCIEFile(replacement, out byte[] encoded));
            File.WriteAllBytes(path, encoded);

            var provider = new CVRawThumbnailProvider();
            var dimensions = provider.GetImageDimensions(path);
            var result = await provider.GenerateThumbnailWithDimensionsAsync(path, 3, CancellationToken.None);
            var (thumbnail, width, height) = Assert.NotNull(result);
            Assert.Equal((3, 2), dimensions);
            Assert.Equal((3, 2), (width, height));
            Assert.True(thumbnail.IsFrozen);
            Assert.Equal(PixelFormats.Gray8, thumbnail.Format);
            byte[] pixels = new byte[6];
            thumbnail.CopyPixels(pixels, 3, 0);
            Assert.Equal(replacement.Data, pixels);

            Assert.True(CVFileUtil.ReadCVRaw(path, out CVCIEFile opened));
            using (opened) Assert.Equal(original.Data, opened.Data);
        }
        finally
        {
            CVFileReadCache.IsEnabled = previousCache;
            File.Delete(path);
        }
    }

    [Fact]
    public async Task CvThumbnailDecodeSlotsAreSharedAndQueuedCancellationDoesNotConsumeASlot()
    {
        string path = Path.Combine(Path.GetTempPath(), $"CvThumbnailSlots_{Guid.NewGuid():N}.cvcie");
        var slots = Assert.IsType<SemaphoreSlim>(typeof(CVRawThumbnailProvider)
            .GetField("DecodeSlots", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null));
        TimeSpan testWait = TimeSpan.FromSeconds(10);
        using var cancellation = new CancellationTokenSource();
        var requests = new List<Task>();
        int heldSlots = 0;
        try
        {
            using CVCIEFile file = new()
            {
                Version = 2, Cols = 4, Rows = 2, Bpp = 32, Channels = 1, FileExtType = CVType.CIE,
                Exp = [1], Data = new byte[4 * 2 * sizeof(float)],
            };
            Assert.True(CVFileUtil.WriteCIEFile(path, file));
            for (int i = 0; i < 2; i++)
            {
                Assert.True(await slots.WaitAsync(testWait));
                heldSlots++;
            }
            Assert.Equal(0, slots.CurrentCount);

            var firstProvider = new CVRawThumbnailProvider();
            var secondProvider = new CVRawThumbnailProvider();
            var first = firstProvider.GenerateThumbnailWithDimensionsAsync(path, 2, CancellationToken.None);
            requests.Add(first);
            var canceled = secondProvider.GenerateThumbnailWithDimensionsAsync(path, 2, cancellation.Token);
            requests.Add(canceled);
            var last = secondProvider.GenerateThumbnailWithDimensionsAsync(path, 2, CancellationToken.None);
            requests.Add(last);
            Assert.All(requests, request => Assert.False(request.IsCompleted));

            cancellation.Cancel();
            var error = await Assert.ThrowsAnyAsync<OperationCanceledException>(() => canceled.WaitAsync(testWait));
            Assert.Equal(cancellation.Token, error.CancellationToken);
            Assert.Equal(0, slots.CurrentCount);
            Assert.False(first.IsCompleted);
            Assert.False(last.IsCompleted);

            slots.Release();
            heldSlots--;
            var survivors = await Task.WhenAll(first, last).WaitAsync(testWait);
            Assert.All(survivors, result =>
            {
                var (thumbnail, width, height) = Assert.NotNull(result);
                Assert.Equal((4, 2), (width, height));
                Assert.Equal((2, 1), (thumbnail.PixelWidth, thumbnail.PixelHeight));
                Assert.True(thumbnail.IsFrozen);
            });
            Assert.Equal(1, slots.CurrentCount);
        }
        finally
        {
            cancellation.Cancel();
            if (heldSlots > 0) slots.Release(heldSlots);
            try { await Task.WhenAll(requests).WaitAsync(testWait); }
            catch (OperationCanceledException) when (requests.All(request => request.IsCompleted)) { }
            finally { File.Delete(path); }
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void StrictPixelReadStopsBeforePublishingWhenCancelledOrLengthDoesNotMatch(bool extraPayload)
    {
        const int width = 256, height = 256, channels = 3;
        int payloadBytes = width * height * channels * sizeof(float);
        using CVCIEFile fixture = new()
        {
            Version = 2, Cols = width, Rows = height, Bpp = 32, Channels = channels, FileExtType = CVType.CIE,
            Exp = [1, 1, 1], Data = new byte[payloadBytes + (extraPayload ? 1 : 0)],
        };
        Assert.True(CVFileUtil.WriteCIEFile(fixture, out byte[] bytes));
        using var cancellation = new CancellationTokenSource();
        using var stream = new CancelOnPayloadReadStream(bytes, cancellation);
        int offset = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile header);
        using (header)
        {
            Assert.True(offset > 0);
            stream.PayloadStart = offset + sizeof(long);
            if (extraPayload)
            {
                Assert.False(CVFileUtil.ReadCIEFilePixels(stream, header, offset, requireExactPayload: true, cancellation.Token));
                Assert.False(cancellation.IsCancellationRequested);
                Assert.Equal(stream.PayloadStart, stream.Position);
            }
            else
            {
                OperationCanceledException error = Assert.Throws<OperationCanceledException>(() =>
                    CVFileUtil.ReadCIEFilePixels(stream, header, offset, requireExactPayload: true, cancellation.Token));
                Assert.Equal(cancellation.Token, error.CancellationToken);
                Assert.InRange(stream.Position, stream.PayloadStart + 1, stream.Length - 1);
            }
            Assert.Null(header.Data);
        }
    }

    private sealed class CancelOnPayloadReadStream(byte[] bytes, CancellationTokenSource cancellation) : MemoryStream(bytes, writable: false)
    {
        public long PayloadStart { get; set; } = long.MaxValue;

        public override int Read(byte[] buffer, int offset, int count)
        {
            bool readingPixels = Position >= PayloadStart;
            int read = base.Read(buffer, offset, count);
            if (readingPixels && read > 0) cancellation.Cancel();
            return read;
        }
    }

    private static string CreateFixture(uint version, int cols = 2, int rows = 1, int channels = 3)
    {
        string path = Path.Combine(Path.GetTempPath(), $"CiePixelSafety_{Guid.NewGuid():N}.cvcie");
        float[] values = Enumerable.Range(1, channels * 2).Select(value => (float)value).ToArray();
        byte[] data = new byte[values.Length * sizeof(float)];
        Buffer.BlockCopy(values, 0, data, 0, data.Length);
        using CVCIEFile file = new()
        {
            Version = version,
            FileExtType = CVType.CIE,
            Cols = cols, Rows = rows, Channels = channels, Bpp = 32,
            Gain = 1, Exp = [1, 1, 1], Data = data
        };
        Assert.True(CVFileUtil.WriteCIEFile(path, file));
        return path;
    }
}
