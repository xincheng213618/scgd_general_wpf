using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;
using System.Text.Json;

namespace ColorVision.UI.Tests;

public sealed class RgbCrossProfileTests
{
    [Theory]
    [InlineData(0.1)]
    [InlineData(1.0)]
    [InlineData(2.2)]
    [InlineData(5.0)]
    public async Task ProfilesKeepIndependentValuesAndNullEdgesWhenSomeSectionsAreMissing(double exponent)
    {
        const int width = 600, height = 500;
        var data = new byte[width * height * 6];
        for (int y = 0; y < height; y++)
            for (int x = 0; x < width; x++)
                for (int channel = 0; channel < 3; channel++)
                {
                    int shift = channel == 2 ? 2 : channel == 0 ? -3 : 0;
                    int u = x - 300 - shift, v = y - 250;
                    bool bright = Math.Abs(u) <= 2 && Math.Abs(v) <= 80 || Math.Abs(v) <= 2 && Math.Abs(u) <= 80;
                    if (channel == 0 && x - 300 is >= -60 and <= -55) bright = false;
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan((y * width + x) * 6 + channel * 2, 2), bright ? (ushort)52428 : (ushort)0);
                }
        using var image = new AlgorithmImageBuffer(width, height, width * 6, AlgorithmImageFormat.Bgr48, data);
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
