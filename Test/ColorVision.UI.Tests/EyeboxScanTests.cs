using ColorVision.Algorithms;
using ColorVision.ImageEditor.Algorithms;

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
}
