using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.POI;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class CvRawProfileTests
{
    [Theory]
    [InlineData(8, 0, true)]
    [InlineData(16, 0, false)]
    [InlineData(16, 1, true)]
    [InlineData(8, 1, false)]
    [InlineData(16, 2, true)]
    public void RawAndXyzMatchOriginalPixelsAndNativeCalibrationBeforeInterpolation(int bpp, int kind, bool interleaved)
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-profile-{Guid.NewGuid():N}.cvraw");
        int channels = kind == 2 ? 1 : 3;
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, bpp, channels);
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.Kind = kind; transform.Channels = channels; transform.InterleavedBgr = interleaved ? 1 : 0;
        transform.Coefficients = [0.123456789, -0.025, 0.007, 0.009, 0.22, -0.05, 0.03, 0.009, 0.32];
        Assert.True(CVFileUtil.WriteCIEFile(path, raw));
        var snapshot = ColorCalibrationSnapshot.Create(transform, 3, 2, bpp, raw.Exp, "profile");
        snapshot.Save(path, true);
        byte[] original = File.ReadAllBytes(path);
        float[] expected = new float[6 * channels];
        GCHandle input = GCHandle.Alloc(raw.Data, GCHandleType.Pinned);
        GCHandle output = GCHandle.Alloc(expected, GCHandleType.Pinned);
        try
        {
            Assert.Equal(OpenCVCalibration.PoiOk, OpenCVMediaHelper.M_TransformRawColorV1(3, 2, bpp,
                input.AddrOfPinnedObject(), (ulong)raw.Data.Length, in transform, -1, output.AddrOfPinnedObject(), (ulong)expected.Length));
            using (var source = CvRawProfileSource.CreateOptions(path)[0].Open(default))
            using (var result = Run(source))
            {
                var batch = Assert.IsAssignableFrom<IBatchedImageProfileMeasurementSource>(source);
                (int X, int Y)[] pixels = [(2, 1), (0, 0), (1, 1), (2, 1)];
                double[] values = new double[pixels.Length * source.RawChannelCount];
                batch.ReadPixels(pixels, values);
                for (int index = 0; index < pixels.Length; index++)
                    for (int channel = 0; channel < source.RawChannelCount; channel++)
                        Assert.Equal(source.Read(pixels[index].X, pixels[index].Y, channel), values[index * source.RawChannelCount + channel]);
                Assert.Equal(channels == 1 ? 2 : 8, source.ChannelNames.Count);
                Assert.DoesNotContain("Luminance", source.ChannelNames);
                var rows = result.GetArtifact<AlgorithmTableArtifact>()!.Rows;
                string[] names = channels == 1 ? ["CIE Y"] : ["CIE X", "CIE Y", "CIE Z"];
                for (int channel = 0; channel < channels; channel++)
                {
                    Assert.Equal(expected[channel * 6], rows[0][names[channel]].GetDouble());
                    Assert.Equal(((double)expected[channel * 6] + expected[channel * 6 + 1]) / 2, rows[1][names[channel]].GetDouble());
                }
                if (channels == 3)
                {
                    double x = rows[1]["CIE X"].GetDouble(), y = rows[1]["CIE Y"].GetDouble(), z = rows[1]["CIE Z"].GetDouble();
                    Assert.Equal(x / (x + y + z), rows[1]["CIE x"].GetDouble());
                    int offset = interleaved ? 0 : 12 * (bpp / 8);
                    Assert.Equal(bpp == 8 ? raw.Data[offset] : BitConverter.ToUInt16(raw.Data, offset), rows[0]["B"].GetDouble());
                }
            }
            Assert.Equal(original, File.ReadAllBytes(path));
            CVFileReadCache.Release(); // Exercise the disk-backed profile's file identity check.
            var choices = CvRawProfileSource.CreateOptions(path);
            File.SetLastWriteTimeUtc(path, File.GetLastWriteTimeUtc(path).AddMinutes(1));
            Assert.Throws<IOException>(() => choices[0].Open(default));
            using CancellationTokenSource cancellation = new();
            var reader = new CvRawProfileSource(path, true, cancellation.Token);
            cancellation.Cancel();
            Assert.Throws<OperationCanceledException>(() => reader.Read(0, 0, 0));
            reader.Dispose();
            Assert.Throws<ObjectDisposedException>(() => reader.Read(0, 0, 0));
            snapshot.Save(path, false);
            Assert.Throws<InvalidDataException>(() => new CvRawProfileSource(path, true));
        }
        finally { input.Free(); output.Free(); File.Delete(path); }
    }

    [Theory]
    [InlineData(ImageProfileInterpolation.Bilinear, ImageProfileBoundaryMode.Clamp)]
    [InlineData(ImageProfileInterpolation.Nearest, ImageProfileBoundaryMode.Skip)]
    [InlineData(ImageProfileInterpolation.Bilinear, ImageProfileBoundaryMode.Reject)]
    public void BatchedRawProfileMatchesScalarAcrossBlocksAndBoundaries(ImageProfileInterpolation interpolation, ImageProfileBoundaryMode boundary)
    {
        const int width = 600, height = 7;
        using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(width, height, 16, 3);
        RawColorTransformV1 transform = RawColorTransformV1.Create();
        transform.Kind = 0; transform.Channels = 3; transform.InterleavedBgr = 0;
        transform.Coefficients = [0.123456789, -0.025, 0.007, 0.009, 0.22, -0.05, 0.03, 0.009, 0.32];
        var snapshot = ColorCalibrationSnapshot.Create(transform, width, height, 16, raw.Exp, "profile");
        using RawColorMeasurementSource rawSource = new(raw, snapshot);
        using var batched = new CvRawProfileSource(rawSource, snapshot, default, () => { });
        using var scalar = new ScalarProfileSource(new CvRawProfileSource(rawSource, snapshot, default, () => { }));
        ImageProfileParameters parameters = new() { Interpolation = interpolation, BoundaryMode = boundary, SampleSpacingPixels = 0.75 };
        var invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters,
            new PolylineAlgorithmRoi([new(0.2, 0.3), new(610.3, 5.8)]));
        var scope = new ImageSelectionScope(Guid.NewGuid(), 1, width, height, 96, 120);
        ImageProfileAlgorithmProvider provider = new();

        using AlgorithmResult expected = provider.ExecuteMeasurement(invocation, parameters, scalar, scope, default);
        using AlgorithmResult actual = provider.ExecuteMeasurement(invocation, parameters, batched, scope, default);

        if (boundary == ImageProfileBoundaryMode.Reject)
        {
            Assert.Equal("profile_sample_out_of_bounds", Assert.Single(actual.Failures).Code);
            Assert.Equal(expected.Failures, actual.Failures);
            Assert.Empty(actual.Artifacts);
        }
        else
        {
            Assert.Equal(AlgorithmResultStatus.Succeeded, actual.Status);
            AlgorithmTableArtifact table = actual.GetArtifact<AlgorithmTableArtifact>()!;
            Assert.True(table.Rows.Count > 512);
            Assert.Equal(JsonSerializer.Serialize(expected.GetArtifact<AlgorithmTableArtifact>(), AlgorithmJson.Options),
                JsonSerializer.Serialize(table, AlgorithmJson.Options));
        }
    }

    [Fact]
    public void CacheOnlyCalibratedRawSupportsChannelsAndProfileAfterCacheRelease()
    {
        string path = Path.Combine(Path.GetTempPath(), $"cv-profile-memory-{Guid.NewGuid():N}.cvraw");
        bool enabled = CVFileReadCache.IsEnabled;
        CVFileReadCache.IsEnabled = true;
        try
        {
            using CVCIEFile raw = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
            var transform = RawColorTransformV1.Create();
            transform.Kind = 0; transform.Channels = 3; transform.InterleavedBgr = 1;
            transform.Coefficients = [1, 0, 0, 0, 1, 0, 0, 0, 1];
            Assert.True(CVFileUtil.WriteCVRaw(path, raw, CVFileSaveMode.MemoryOnly));
            ColorCalibrationSnapshot.Create(transform, 3, 2, 16, raw.Exp, "memory").Save(path, true);
            CVFileReadCacheSnapshot before = CVFileReadCache.GetSnapshot();
            using var channelReader = new CalibratedRawFileReader(path);
            CVFileReadCacheSnapshot opened = CVFileReadCache.GetSnapshot();
            Assert.Equal(1, opened.HitCount - before.HitCount);
            Assert.Equal(before.ActiveReaders, opened.ActiveReaders);
            using var profile = new CvRawProfileSource(path, true);

            using CVCIEFile replacement = RawColorCalibrationTests.CreateRaw(3, 2, 16, 3);
            Array.Clear(replacement.Data);
            for (int offset = 0; offset < replacement.Data.Length; offset += sizeof(ushort)) replacement.Data[offset] = 7;
            transform.Coefficients[4] = 3;
            Assert.True(CVFileUtil.WriteCVRaw(path, replacement, CVFileSaveMode.MemoryOnly));
            ColorCalibrationSnapshot.Create(transform, 3, 2, 16, replacement.Exp, "replacement").Save(path, true);
            using var replacementReader = new CalibratedRawFileReader(path);
            CVFileReadCache.Release();
            using CVCIEFile y = channelReader.ReadChannel(1);
            float[] values = new float[6];
            Buffer.BlockCopy(y.Data, 0, values, 0, y.Data.Length);
            Assert.Equal((float)BitConverter.ToUInt16(raw.Data, sizeof(ushort)), values[0]);
            for (int x = 0; x < 3; x++) Assert.Equal(values[x], profile.Read(x, 0, 4));
            using CVCIEFile replacementY = replacementReader.ReadChannel(1);
            Assert.Equal(21f, BitConverter.ToSingle(replacementY.Data, 0));
            Assert.False(File.Exists(path));
        }
        finally { CVFileReadCache.Release(); CVFileReadCache.IsEnabled = enabled; }
    }

    [Theory]
    [InlineData("Signal Δ")]
    [InlineData("XPixel")]
    public void MeasurementRowsRetainIndependentDoubleValuesAndNonfiniteStatusCells(string channelName)
    {
        const double precise = 0.12345678901234567;
        double[] samples = Enumerable.Range(0, 515).Select(index => (index % 4) switch
        {
            0 => precise + index,
            1 => double.NaN,
            2 => double.PositiveInfinity,
            _ => double.NegativeInfinity,
        }).ToArray();
        using var source = new DoubleProfileSource(samples, channelName);
        ImageProfileParameters parameters = new() { Interpolation = ImageProfileInterpolation.Nearest };
        var invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters,
            new PolylineAlgorithmRoi([new(0, 0), new(samples.Length - 1, 0)]));
        using var result = new ImageProfileAlgorithmProvider().ExecuteMeasurement(invocation, parameters, source,
            new ImageSelectionScope(Guid.NewGuid(), 1, source.Width, source.Height, 96, 96), default);

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        AlgorithmTableArtifact table = result.GetArtifact<AlgorithmTableArtifact>("image-profile-samples")!;
        Assert.Equal(new[] { "SampleIndex", "RequestedIndex", "SegmentIndex", "DistancePixels", "DistanceMillimetres", "XPixel", "YPixel", channelName, channelName + "Status" },
            table.Columns.Select(column => column.Name));
        Assert.Equal(samples.Length, table.Rows.Count);
        Assert.Equal(precise, table.Rows[0][channelName].GetDouble());
        string[] statuses = ["Finite", "NaN", "+Infinity", "-Infinity"];
        for (int index = 0; index < table.Rows.Count; index++)
        {
            var row = table.Rows[index];
            Assert.Equal(index, row["SampleIndex"].GetInt32());
            Assert.Equal(index, row["DistancePixels"].GetDouble());
            Assert.Equal(statuses[index % statuses.Length], row[channelName + "Status"].GetString());
            Assert.True(row.TryGetValue(channelName, out JsonElement value));
            if (double.IsFinite(samples[index])) Assert.Equal(samples[index], value.GetDouble());
            else Assert.Equal(JsonValueKind.Null, value.ValueKind);
        }
        Assert.Equal(table.Columns.Select(column => column.Name).Distinct(), table.Rows[^1].Select(cell => cell.Key));
        Assert.False(table.Rows[0].TryGetValue("missing", out _));
    }

    private sealed class DoubleProfileSource(double[] values, string channelName) : IImageProfileMeasurementSource
    {
        public int Width => values.Length;
        public int Height => 1;
        public int RawChannelCount => 1;
        public IReadOnlyList<string> ChannelNames { get; } = [channelName];
        public string ValueDescription => "Original double measurement values";
        public string FormatName => "Float64";
        public string? GetUnit(int channel) => "cd/m²";
        public double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues) => sampledValues[0];
        public double Read(int x, int y, int channel) => values[x];
        public void Dispose() { }
    }

    private sealed class ScalarProfileSource(IImageProfileMeasurementSource source) : IImageProfileMeasurementSource
    {
        public int Width => source.Width;
        public int Height => source.Height;
        public int RawChannelCount => source.RawChannelCount;
        public IReadOnlyList<string> ChannelNames => source.ChannelNames;
        public string ValueDescription => source.ValueDescription;
        public string FormatName => source.FormatName;
        public string? GetUnit(int channel) => source.GetUnit(channel);
        public double GetChannelValue(int channel, ReadOnlySpan<double> sampledValues) => source.GetChannelValue(channel, sampledValues);
        public double Read(int x, int y, int channel) => source.Read(x, y, channel);
        public void Dispose() => source.Dispose();
    }

    internal static AlgorithmResult Run(IImageProfileMeasurementSource source)
    {
        ImageProfileParameters parameters = new() { SampleSpacingPixels = 0.5 };
        var invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters, new PolylineAlgorithmRoi([new(0, 0), new(2, 0)]));
        return new ImageProfileAlgorithmProvider().ExecuteMeasurement(invocation, parameters, source,
            new ImageSelectionScope(Guid.NewGuid(), 1, source.Width, source.Height, 96, 96), default);
    }
}
