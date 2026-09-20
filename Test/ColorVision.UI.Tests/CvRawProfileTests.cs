using ColorVision.Algorithms;
using ColorVision.Core;
using ColorVision.Engine.Media;
using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.FileIO;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Algorithms;
using System.IO;
using System.Runtime.InteropServices;

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

    internal static AlgorithmResult Run(IImageProfileMeasurementSource source)
    {
        ImageProfileParameters parameters = new() { SampleSpacingPixels = 0.5 };
        var invocation = AlgorithmInvocation.Create(StandardAlgorithmIds.ImageProfile, parameters, new PolylineAlgorithmRoi([new(0, 0), new(2, 0)]));
        return new ImageProfileAlgorithmProvider().ExecuteMeasurement(invocation, parameters, source,
            new ImageSelectionScope(Guid.NewGuid(), 1, source.Width, source.Height, 96, 96), default);
    }
}
