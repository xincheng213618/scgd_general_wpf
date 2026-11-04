using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System.Buffers.Binary;
using System.Reflection;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossPoolingTests
{
    // Pooling can connect unrelated source pixels into a small T-shaped candidate.
    // A real extra cross must still make the configured array ambiguous, even if it is dim and single-color.
    [Theory]
    [InlineData("pooled-noise", true)]
    [InlineData("extra-cross", false)]
    [InlineData("extra-dim-red-cross", false)]
    public async Task SourceConnectivityRejectsPoolingArtifactsWithoutDiscardingExtraCrosses(string extra, bool expectedValid)
    {
        const int width = 3204, height = 600, step = 3;
        var data = new byte[width * height * 6];
        void Write(int x, int y, int channel, ushort value) =>
            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 6 + channel * 2, 2), value);
        void Cross(int centerX, int centerY, bool dimRedOnly = false)
        {
            for (int y = centerY - 40; y <= centerY + 40; y++)
                for (int x = centerX - 40; x <= centerX + 40; x++)
                    for (int channel = 0; channel < 3; channel++)
                    {
                        if (dimRedOnly && channel != 2) continue;
                        int shift = channel == 2 ? 1 : channel == 0 ? -2 : 0;
                        int u = x - centerX - shift, v = y - centerY;
                        if (Math.Abs(u) <= 2 && Math.Abs(v) <= 36 || Math.Abs(v) <= 2 && Math.Abs(u) <= 36)
                            Write(x, y, channel, dimRedOnly ? (ushort)1966 : (ushort)52428);
                    }
        }
        foreach (int y in new[] { 180, 340, 500 })
            foreach (int x in new[] { 600, 1400, 2200 }) Cross(x, y);
        if (extra == "pooled-noise")
        {
            // ..# / ### / ..# / ..# in the locator, but six isolated pixels in the source.
            foreach (var (x, y) in new[] { (2, 0), (0, 1), (1, 1), (2, 1), (2, 2), (2, 3) })
                Write(60 + x * step + 1, 60 + y * step + 1, 2, 6554);
        }
        else Cross(120, 80, extra == "extra-dim-red-cross");
        using var image = new AlgorithmImageBuffer(width, height, width * 6, AlgorithmImageFormat.Bgr48, data);
        using var result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbCrossRegistration, new RgbCrossRegistrationParameters()),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows;
        Assert.Equal(9, rows.Count);
        Assert.Equal(Enumerable.Range(1, 9).Select(index => "P" + index), rows.Select(row => row["point"].GetString()));
        Assert.All(rows, row =>
        {
            Assert.Equal(expectedValid, row["valid"].GetBoolean());
            if (expectedValid)
            {
                Assert.Equal(1, row["rToGMaximumEdge_px"].GetDouble());
                Assert.Equal(2, row["bToGMaximumEdge_px"].GetDouble());
            }
            else
            {
                Assert.Contains("duplicate_or_extra_candidates", row["reason"].GetString());
                Assert.Equal(System.Text.Json.JsonValueKind.Null, row["rToGMaximumEdge_px"].ValueKind);
                Assert.Equal(System.Text.Json.JsonValueKind.Null, row["bToGMaximumEdge_px"].ValueKind);
            }
        });
    }

    [Fact]
    public void SourceComponentKeepsMinimalCrossAndUsesDecodedSignalFourConnectivityAndRoi()
    {
        const int width = 40, height = 32, stride = width * 6 + 7;
        byte[] data = new byte[stride * height];
        void Write(int x, int y) => BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + x * 6 + 4, 2), 32768);
        var check = typeof(DisplayMetrologyProvider).GetMethod("HasSourceCrossComponent", BindingFlags.Static | BindingFlags.NonPublic)!;
        bool Has(AlgorithmImageBuffer image, Rect roi, double floor, double exponent) =>
            (bool)check.Invoke(null, [image, roi, floor, exponent, CancellationToken.None])!;
        Rect region = new(7, 9, 3, 3);
        // The smallest supported source cross: one connected component, five pixels, bounds 3×3.
        foreach (var (x, y) in new[] { (8, 9), (7, 10), (8, 10), (9, 10), (8, 11) }) Write(x, y);
        using (var image = new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Bgr48, data))
        {
            Assert.True(Has(image, region, 0.2, 2.2));
            Assert.False(Has(image, region, 0.3, 2.2)); // Raw 0.5 passes, but decoded 0.5^2.2 does not.
            Assert.False(Has(image, new Rect(20, 9, 3, 3), 0.2, 2.2)); // Do not borrow support outside the requested footprint.
        }
        data = new byte[stride * height];
        // Same area and bounding box, but diagonal adjacency cannot create a four-connected cross.
        foreach (var (x, y) in new[] { (7, 9), (9, 9), (8, 10), (7, 11), (9, 11) }) Write(x, y);
        using var disconnected = new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Bgr48, data);
        Assert.False(Has(disconnected, region, 0.2, 2.2));
    }

    [Theory]
    [InlineData(37, 31, 0.1)]
    [InlineData(1603, 31, 1.0)]
    [InlineData(3203, 31, 2.2)]
    [InlineData(8003, 31, 5.0)]
    [InlineData(3200, 32, 1.0)]
    [InlineData(37, 3203, 2.2)]
    public void PoolingPreservesEveryChannelWithPartialCellsAndUnalignedStride(int searchWidth, int searchHeight, double exponent)
    {
        int width = searchWidth + 11, height = searchHeight + 13;
        AlgorithmImageBuffer Buffer(AlgorithmImageFormat format)
        {
            int count = format.Channels(), stride = width * count * 2 + 7;
            var data = new byte[stride * height];
            Array.Fill(data, (byte)255); // Bright padding must stay outside the ROI.
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    for (int c = 0; c < count; c++)
                    {
                        ushort value = c == 3 ? ushort.MaxValue : unchecked((ushort)(x * 73856093 ^ y * 19349663 ^ c * 83492791));
                        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + (x * count + c) * 2, 2), value);
                    }
            return new AlgorithmImageBuffer(width, height, stride, format, data);
        }
        using var bgr = Buffer(AlgorithmImageFormat.Bgr48);
        using var bgra = Buffer(AlgorithmImageFormat.Bgra64);
        var pool = typeof(DisplayMetrologyProvider).GetMethod("CrossLocatorImage", BindingFlags.Static | BindingFlags.NonPublic)!;
        (float[][] Channels, int Width, int Height, int Step) Pool(AlgorithmImageBuffer image)
            => ((float[][], int, int, int))pool.Invoke(null, [image, new Rect(3, 5, searchWidth, searchHeight), exponent, CancellationToken.None])!;
        var actual = Pool(bgr);
        var expected = Pool(bgra); // Opaque BGRA retains the independent generic path.
        Assert.Equal((expected.Width, expected.Height, expected.Step), (actual.Width, actual.Height, actual.Step));
        for (int c = 0; c < 3; c++)
            Assert.True(actual.Channels[c].AsSpan().SequenceEqual(expected.Channels[c]), $"Channel {c} differs from the generic reader.");
    }
}
