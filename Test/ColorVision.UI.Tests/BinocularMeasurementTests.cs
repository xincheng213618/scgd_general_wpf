using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;

namespace ColorVision.UI.Tests;

public sealed class BinocularMeasurementTests
{
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PaddedBgr48PreservesEachChannelMeanAndTheSelectedAlignment(bool measureAlignment)
    {
        const int width = 321, height = 257, stride = width * 6 + 3;
        const int grid = 3, spotArea = 25, selectedChannel = 2;
        ushort[] leftBackground = [3000, 13000, 18000], leftPeak = [50000, 48000, 40000];
        ushort[] rightBackground = [2000, 16000, 8000], rightPeak = [35000, 39000, 62000];
        using var left = Frame(leftBackground, leftPeak, 0, 0);
        using var right = Frame(rightBackground, rightPeak, 3, -2);
        using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
        {
            Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Binocular, new BinocularQualityParameters
            {
                Columns = grid, Rows = grid, Channel = selectedChannel, DecodeExponent = 2, MeasureAlignment = measureAlignment,
            }),
            Inputs =
            [
                new AlgorithmInput { Name = "left", Image = left, ColorSpace = "encoded-device-values" },
                new AlgorithmInput { Name = "right", Image = right, ColorSpace = "encoded-device-values" },
            ],
            RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
        });

        Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(Environment.NewLine, result.Failures.Select(failure => failure.Message)));
        var field = result.GetArtifact<AlgorithmTableArtifact>("binocular-field")!.Rows;
        var channels = result.GetArtifact<AlgorithmTableArtifact>("binocular-channel-consistency")!.Rows;
        Assert.Equal(grid * grid, field.Count);
        Assert.Equal(grid * grid * 3, channels.Count);
        for (int cell = 0; cell < field.Count; cell++)
        {
            var (x0, x1, y0, y1) = CellBounds(cell);
            int area = (x1 - x0) * (y1 - y0);
            Assert.Equal(measureAlignment, field[cell]["alignmentValid"].GetBoolean());
            if (measureAlignment)
            {
                Assert.Equal(3, field[cell]["dx_px"].GetDouble(), 10);
                Assert.Equal(-2, field[cell]["dy_px"].GetDouble(), 10);
            }
            for (int channel = 0; channel < 3; channel++)
            {
                var row = channels[channel * field.Count + cell];
                double expectedLeft = Mean(leftBackground[channel], leftPeak[channel], area);
                double expectedRight = Mean(rightBackground[channel], rightPeak[channel], area);
                Assert.Equal(cell, row["cell"].GetInt32());
                Assert.Equal("BGR"[channel].ToString(), row["channel"].GetString());
                Assert.Equal(expectedLeft, row["leftSignal"].GetDouble(), 12);
                Assert.Equal(expectedRight, row["rightSignal"].GetDouble(), 12);
                Assert.Equal(expectedRight / expectedLeft, row["rightOverLeft"].GetDouble(), 12);
                if (channel == selectedChannel)
                {
                    Assert.Equal(row["leftSignal"].GetDouble(), field[cell]["leftSignal"].GetDouble());
                    Assert.Equal(row["rightSignal"].GetDouble(), field[cell]["rightSignal"].GetDouble());
                }
            }
        }

        static double Mean(ushort background, ushort peak, int area)
            => ((area - spotArea) * Decode(background) + spotArea * Decode(peak)) / area;

        static double Decode(ushort code) => (float)Math.Pow(code / 65535d, 2);

        static (int X0, int X1, int Y0, int Y1) CellBounds(int cell)
        {
            int x = cell % grid, y = cell / grid;
            return (x * width / grid, (x + 1) * width / grid, y * height / grid, (y + 1) * height / grid);
        }

        static AlgorithmImageBuffer Frame(ushort[] background, ushort[] peak, int dx, int dy)
        {
            var data = new byte[stride * height];
            Array.Fill(data, byte.MaxValue);
            for (int cell = 0; cell < grid * grid; cell++)
            {
                var (x0, x1, y0, y1) = CellBounds(cell);
                int centerX = (x0 + x1) / 2 + dx, centerY = (y0 + y1) / 2 + dy;
                for (int y = y0; y < y1; y++)
                    for (int x = x0; x < x1; x++)
                    {
                        var codes = Math.Abs(x - centerX) <= 2 && Math.Abs(y - centerY) <= 2 ? peak : background;
                        for (int channel = 0; channel < 3; channel++)
                            BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + (x * 3 + channel) * 2), codes[channel]);
                    }
            }
            return new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Bgr48, data);
        }
    }
}
