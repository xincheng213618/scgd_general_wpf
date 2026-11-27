using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;

namespace ColorVision.UI.Tests;

public sealed class RgbRegistrationTests
{
    [Fact]
    public async Task RunnerUpMustExceedFivePercentOfTheBrightestTarget()
    {
        const int width = 53, height = 55, stride = width * 3 + 3;
        byte[] pixels = new byte[height * stride];
        for (int cell = 0; cell < 4; cell++)
        {
            int left = (cell % 2) * width / 2, top = (cell / 2) * height / 2;
            for (int y = top + 10; y < top + 20; y++)
                for (int x = left + 10; x < left + 20; x++)
                    pixels.AsSpan(y * stride + x * 3, 3).Fill(byte.MaxValue);

            // These candidates precede the 100-pixel target in scan order.
            int secondaryArea = cell == 0 ? 5 : cell == 1 ? 6 : 0;
            for (int x = left + 5; x < left + 5 + secondaryArea; x++)
                pixels[(top + 5) * stride + x * 3] = byte.MaxValue;
        }

        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Bgr24, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbRegistration,
                new RgbRegistrationParameters { Rows = 2, Columns = 2 }),
            Inputs = [new AlgorithmInput { Image = image, Name = "source", ColorSpace = "sensor" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var blues = result.GetArtifact<AlgorithmTableArtifact>("RGB-displacement")!.Rows
            .Where(row => row["pair"].GetString() == "B-G")
            .ToArray();
        Assert.Equal(4, blues.Length);
        Assert.True(blues[0]["valid"].GetBoolean());
        Assert.Equal(0, blues[0]["dx_px"].GetDouble());
        Assert.False(blues[1]["valid"].GetBoolean());
        Assert.Equal("ambiguous_targets", blues[1]["reason"].GetString());
        Assert.True(blues[2]["valid"].GetBoolean());
        Assert.True(blues[3]["valid"].GetBoolean());
    }

    [Fact]
    public async Task PaddedBgr48MeasuresBothColorOffsetsAcrossUnevenTiles()
    {
        const int width = 267, height = 269, stride = width * 6 + 5;
        byte[] pixels = new byte[height * stride];
        (int X, int Y)[] offsets = [(-3, 1), (0, 0), (2, -1)];
        for (int cell = 0; cell < 4; cell++)
        {
            int centerX = (2 * (cell % 2) + 1) * width / 4;
            int centerY = (2 * (cell / 2) + 1) * height / 4;
            for (int channel = 0; channel < offsets.Length; channel++)
            {
                if (cell == 0 && channel == 1) continue;
                (int dx, int dy) = offsets[channel];
                for (int y = centerY + dy - 2; y <= centerY + dy + 2; y++)
                    for (int x = centerX + dx - 2; x <= centerX + dx + 2; x++)
                        BinaryPrimitives.WriteUInt16LittleEndian(pixels.AsSpan(y * stride + x * 6 + channel * 2, 2), 40001);
            }
        }

        using AlgorithmImageBuffer image = new(width, height, stride, AlgorithmImageFormat.Bgr48, pixels);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.RgbRegistration,
                new RgbRegistrationParameters { Rows = 2, Columns = 2, DecodeExponent = 2.2 }),
            Inputs = [new AlgorithmInput { Image = image, Name = "source", ColorSpace = "sensor" }],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local,
        });

        Assert.Equal(AlgorithmResultStatus.Succeeded, result.Status);
        var rows = result.GetArtifact<AlgorithmTableArtifact>("RGB-displacement")!.Rows;
        Assert.Equal(8, rows.Count);
        for (int index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            int cell = index % 4;
            bool red = index < 4;
            Assert.Equal(cell, row["cell"].GetInt32());
            Assert.Equal(red ? "R-G" : "B-G", row["pair"].GetString());
            Assert.Equal(cell != 0, row["valid"].GetBoolean());
            if (cell == 0)
            {
                Assert.Equal("G:low_contrast", row["reason"].GetString());
                continue;
            }
            Assert.Equal(red ? 2d : -3d, row["dx_px"].GetDouble());
            Assert.Equal(red ? -1d : 1d, row["dy_px"].GetDouble());
        }
    }
}
