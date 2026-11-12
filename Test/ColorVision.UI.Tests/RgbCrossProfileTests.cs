using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossProfileTests
{
    [Theory]
    [InlineData(false, "complete", -18)]
    [InlineData(true, "complete", -18)]
    [InlineData(false, "clipped", -18)]
    [InlineData(false, "neighbor", -18)]
    [InlineData(false, "complete", -50)]
    [InlineData(true, "complete", -50)]
    public async Task DimArmRecoveryPreservesIndependentEdgesAndSearchBoundaries(bool transpose, string boundary, int brightArmStart)
    {
        const int width = 800, height = 500;
        var data = new byte[width * height * 6];
        void DrawCross(int centerX, bool dimArm)
        {
            for (int y = 150; y <= 350; y++)
                for (int x = centerX - 100; x <= centerX + 100; x++)
                    for (int channel = 0; channel < 3; channel++)
                    {
                        int shift = channel == 2 ? 2 : channel == 0 ? -3 : 0;
                        int u = x - centerX - (transpose ? 0 : shift), v = y - 250 - (transpose ? shift : 0);
                        if (transpose) (u, v) = (v, u);
                        ushort value = 0;
                        if (Math.Abs(v) <= 3 && Math.Abs(u) <= 80)
                            value = dimArm && u < brightArmStart ? (ushort)13107 : (ushort)39321;
                        if (Math.Abs(u) <= 6 && Math.Abs(v) <= 80) value = 52428;
                        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 6 + channel * 2, 2), value);
                    }
        }
        DrawCross(300, true);
        if (boundary == "neighbor") DrawCross(520, false);
        using var image = new AlgorithmImageBuffer(width, height, width * 6, AlgorithmImageFormat.Bgr48, data);
        using var result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbCrossRegistration,
                new RgbCrossRegistrationParameters { Rows = 1, Columns = boundary == "neighbor" ? 2 : 1 },
                boundary == "clipped" ? new RectangleAlgorithmRoi(260, 0, 540, height) : null),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Borrowed }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var measurement = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows[0];
        Assert.True(measurement["valid"].GetBoolean());
        if (boundary != "complete")
        {
            // A clipped footprint or a neighbor inside the expanded ROI cannot repair coverage.
            Assert.Contains("partial_arm_coverage", measurement["warning"].GetString());
            return;
        }
        Assert.DoesNotContain("partial_arm_coverage", measurement["warning"].GetString());
        // Coverage can already be 100% inside a shortened peak envelope. Recover
        // the dim arm's sampling extent too, not just its validity or separation.
        var portable = result.Artifacts.OfType<AlgorithmStructuredDataArtifact>().Single(a => a.Schema == RgbCrossMeasurementExporter.SchemaId);
        var arm = portable.Data.GetProperty("points")[0].GetProperty("channels").GetProperty("R")
            .GetProperty(transpose ? "verticalArm" : "horizontalArm");
        Assert.True(arm.GetProperty(transpose ? "height" : "width").GetDouble() >= 160);
        Assert.Equal(2, measurement["rToGMaximumEdge_px"].GetDouble());
        Assert.Equal(3, measurement["bToGMaximumEdge_px"].GetDouble());
        foreach (string channel in new[] { "r", "g", "b" })
        {
            Assert.Equal(1, measurement[channel + "HorizontalCoverage"].GetDouble());
            Assert.Equal(1, measurement[channel + "VerticalCoverage"].GetDouble());
            Assert.Equal(0, measurement[channel + "RejectedProfiles"].GetInt32());
        }
        // Diagnostics must describe only the accepted sampling geometry, not the discarded attempt.
        var profiles = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-profile-quality")!.Rows;
        Assert.All(profiles, profile => Assert.Equal("valid", profile["status"].GetString()));
        Assert.Equal(profiles.Count, profiles.Select(profile => string.Join("/", new[] { "channel", "arm", "side", "samplePosition_px" }
            .Select(key => profile[key].ToString()))).Distinct().Count());
    }

    [Theory]
    [InlineData(AlgorithmImageFormat.Bgr48, 0.1)]
    [InlineData(AlgorithmImageFormat.Bgr48, 1.0)]
    [InlineData(AlgorithmImageFormat.Bgr48, 2.2)]
    [InlineData(AlgorithmImageFormat.Bgr48, 5.0)]
    [InlineData(AlgorithmImageFormat.Bgr24, 0.1)]
    [InlineData(AlgorithmImageFormat.Bgra32, 2.2)]
    [InlineData(AlgorithmImageFormat.Bgra64, 5.0)]
    public async Task ProfilesKeepIndependentValuesAndNullEdgesWhenSomeSectionsAreMissing(AlgorithmImageFormat format, double exponent)
    {
        const int width = 600, height = 500;
        int channels = format.Channels(), bytes = format.BitsPerChannel() / 8, stride = width * format.BytesPerPixel() + 7;
        var data = new byte[stride * height];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                for (int channel = 0; channel < channels; channel++)
                {
                    int shift = channel == 2 ? 2 : channel == 0 ? -3 : 0;
                    int u = x - 300 - shift, v = y - 250;
                    bool bright = Math.Abs(u) <= 2 && Math.Abs(v) <= 80 || Math.Abs(v) <= 2 && Math.Abs(u) <= 80;
                    if (channel == 0 && x - 300 is >= -60 and <= -55) bright = false;
                    // A nonzero background exercises the decoded median, including 8-bit
                    // normalization and 16-bit BGRA with an unaligned source row stride.
                    ushort value = channel == 3 ? ushort.MaxValue : bright ? (ushort)52428 : (ushort)514;
                    int offset = y * stride + (x * channels + channel) * bytes;
                    if (bytes == 1) data[offset] = (byte)(value / 257);
                    else BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(offset, 2), value);
                }
        using var image = new AlgorithmImageBuffer(width, height, stride, format, data);
        using var result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbCrossRegistration,
                new RgbCrossRegistrationParameters { Rows = 1, Columns = 1, DecodeExponent = exponent }),
            Inputs = [new AlgorithmInput { Name = "source", Image = image, Ownership = AlgorithmInputOwnership.Borrowed, ColorSpace = "linear-device-values" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });
        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var measurement = Assert.Single(result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-separation")!.Rows);
        Assert.True(measurement["valid"].GetBoolean());
        Assert.Equal(2, measurement["rToGMaximumEdge_px"].GetDouble());
        Assert.Equal(3, measurement["bToGMaximumEdge_px"].GetDouble());
        var profiles = result.GetArtifact<AlgorithmTableArtifact>("RGB-cross-profile-quality")!.Rows;
        Assert.Contains(profiles, row => row["status"].GetString() == "valid");
        Assert.Contains(profiles, row => row["status"].GetString() == "low_contrast");
        foreach (var row in profiles)
        {
            Assert.Equal(9, row.Count);
            Assert.Equal("P1", row["point"].GetString());
            if (row["status"].GetString() == "low_contrast")
            {
                Assert.Equal("B", row["channel"].GetString());
                Assert.Equal(JsonValueKind.Null, row["firstEdge_px"].ValueKind);
                Assert.Equal(JsonValueKind.Null, row["lastEdge_px"].ValueKind);
            }
            else
            {
                Assert.True(double.IsFinite(row["firstEdge_px"].GetDouble()));
                Assert.True(row["lastEdge_px"].GetDouble() > row["firstEdge_px"].GetDouble());
            }
        }
    }
}
