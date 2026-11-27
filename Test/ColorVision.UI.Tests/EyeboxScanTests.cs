using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;
using System.Buffers.Binary;

namespace ColorVision.UI.Tests;

public sealed class EyeboxScanTests
{
    [Fact]
    public async Task MovingFramesKeepTheirOwnRatiosWhenTheReferenceIsInTheMiddle()
    {
        byte[] intensities = [64, 192, 96, 240];
        AlgorithmImageBuffer[] frames = intensities.Select(value =>
            new AlgorithmImageBuffer(32, 32, 32, AlgorithmImageFormat.Gray8, Enumerable.Repeat(value, 32 * 32).ToArray())).ToArray();
        try
        {
            using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Eyebox,
                    new EyeboxScanParameters { Columns = 2, Rows = 2, ReferenceIndex = 1, MinimumMeanRatio = 0.5, MinimumPixelRatio = 0.5 }),
                Inputs = frames.Select((frame, i) => new AlgorithmInput
                {
                    Name = $"sample-{i}",
                    Image = frame,
                    ColorSpace = "encoded-device-values",
                }).ToArray(),
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            });

            Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(Environment.NewLine, result.Failures.Select(failure => failure.Message)));
            IReadOnlyList<IReadOnlyDictionary<string, System.Text.Json.JsonElement>> rows =
                result.GetArtifact<AlgorithmTableArtifact>("eyebox-scan")!.Rows;
            Assert.Equal(4, rows.Count);
            for (int i = 0; i < rows.Count; i++)
            {
                Assert.Equal(intensities[i] / 192d, rows[i]["meanOverReference"].GetDouble(), 7);
                Assert.Equal(i != 0, rows[i]["accepted"].GetBoolean());
                Assert.All(frames[i].Data.ToArray(), value => Assert.Equal(intensities[i], value));
            }
            Assert.Equal(new byte[] { 0, 255, 255, 255 }, result.GetArtifact<AlgorithmImageArtifact>("eyebox-sample-grid")!.Image.Data.ToArray());
        }
        finally
        {
            foreach (AlgorithmImageBuffer frame in frames) frame.Dispose();
        }
    }

    [Fact]
    public async Task PaddedBgr48FramesMeasureTheSelectedChannelWithinTheReferenceFootprint()
    {
        const int width = 321, height = 257, stride = width * 6 + 3;
        ushort[] firstHalf = [12000, 38000, 40000, 55000];
        ushort[] secondHalf = [12000, 22000, 40000, 55000];
        AlgorithmImageBuffer[] frames = Enumerable.Range(0, 4).Select(index =>
        {
            var data = new byte[stride * height];
            Array.Fill(data, byte.MaxValue);
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                {
                    ushort green = x < 64 ? firstHalf[index] : x < 128 ? secondHalf[index]
                        : index == 2 ? (ushort)800 : ushort.MaxValue;
                    BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(y * stride + x * 6 + 2), green);
                }
            return new AlgorithmImageBuffer(width, height, stride, AlgorithmImageFormat.Bgr48, data);
        }).ToArray();

        try
        {
            using AlgorithmResult result = await ImageAlgorithmPlatform.Runner.RunAsync(new AlgorithmRunRequest
            {
                Invocation = AlgorithmInvocation.Create(DisplayMetrologyIds.Eyebox, new EyeboxScanParameters
                {
                    Columns = 2, Rows = 2, ReferenceIndex = 2, Channel = 1, DecodeExponent = 2,
                    MinimumMeanRatio = 0.5, MinimumPixelRatio = 0.5, MinimumCoverage = 0.75,
                }),
                Inputs = frames.Select((image, index) => new AlgorithmInput
                {
                    Name = $"sample-{index}", Image = image, ColorSpace = "encoded-device-values",
                }).ToArray(),
                RequiredCapabilities = AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.MultiInput,
            });

            Assert.True(result.Status == AlgorithmResultStatus.Succeeded, string.Join(Environment.NewLine, result.Failures.Select(failure => failure.Message)));
            var rows = result.GetArtifact<AlgorithmTableArtifact>("eyebox-scan")!.Rows;
            Assert.Equal(4, rows.Count);
            double[] expectedCoverage = [0, 0.5, 1, 1];
            for (int i = 0; i < rows.Count; i++)
            {
                double expectedRatio = (Decode(firstHalf[i]) + Decode(secondHalf[i])) / (2 * Decode(firstHalf[2]));
                Assert.Equal(expectedRatio, rows[i]["meanOverReference"].GetDouble(), 12);
                Assert.Equal(expectedCoverage[i], rows[i]["coverage"].GetDouble());
                Assert.Equal(i >= 2, rows[i]["accepted"].GetBoolean());
            }
            var summary = result.GetArtifact<AlgorithmMeasurementArtifact>("summary-1")!;
            Assert.Equal(128 * height, summary.Measurements.Single(measurement => measurement.Name == "reference_valid_pixels").Value);
            Assert.Equal(new byte[] { 0, 0, 255, 255 }, result.GetArtifact<AlgorithmImageArtifact>("eyebox-sample-grid")!.Image.Data.ToArray());
        }
        finally
        {
            foreach (AlgorithmImageBuffer frame in frames) frame.Dispose();
        }

        static double Decode(ushort code) => (float)Math.Pow(code / 65535d, 2);
    }
}
