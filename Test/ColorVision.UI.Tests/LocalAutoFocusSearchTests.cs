using ColorVision.Engine.Services.Devices.Camera.Local;
using ColorVision.Engine.Services.PhyCameras.Configs;

namespace ColorVision.UI.Tests;

public class LocalAutoFocusSearchTests
{
    [Theory]
    [InlineData(FindFuncModel.FindMaxDiff)]
    [InlineData(FindFuncModel.FindDichotomy)]
    [InlineData(FindFuncModel.FindMaxSearch)]
    public void SearchStaysWithinTravelAndFinishesAtTheBestSample(FindFuncModel method)
    {
        var samples = new List<(int Position, double Score)>();
        int finalPosition = -1;
        var result = LocalAutoFocusSearch.Run(LocalCameraMotorTests.Settings(method), (position, remaining) =>
        {
            Assert.InRange(position, 0, 1000);
            Assert.InRange(remaining, 1, 10000);
            double score = 1000000 - Math.Pow(position - 463, 2);
            samples.Add((position, score));
            return score;
        }, (position, _) => finalPosition = position);
        Assert.Equal(result.Position, finalPosition);
        Assert.Equal(samples.Max(point => point.Score), result.Score);
        Assert.InRange(Math.Abs(result.Position - 463), 0, 1);
    }

    [Fact]
    public void TimeoutAndSampleFailureNeverSubmitAFinalMove()
    {
        long elapsed = 0;
        int moves = 0;
        Assert.Throws<TimeoutException>(() => LocalAutoFocusSearch.Run(LocalCameraMotorTests.Settings(), (_, _) =>
        {
            elapsed = 10000;
            return 5;
        }, (_, _) => moves++, elapsedMilliseconds: () => elapsed));
        Assert.Equal(0, moves);
        Assert.Throws<InvalidOperationException>(() => LocalAutoFocusSearch.Run(LocalCameraMotorTests.Settings(), (_, _) => throw new InvalidOperationException("native failed"), (_, _) => moves++));
        Assert.Equal(0, moves);
        Assert.Throws<InvalidOperationException>(() => LocalAutoFocusSearch.Run(LocalCameraMotorTests.Settings(), (_, _) => double.NaN, (_, _) => moves++));
    }

    [Fact]
    public void FailedThresholdDoesNotReportAFocusedPosition()
    {
        bool finalMove = false;
        Assert.Throws<InvalidOperationException>(() => LocalAutoFocusSearch.Run(LocalCameraMotorTests.Settings() with { MinimumScore = 10 }, (_, _) => 5, (_, _) => finalMove = true));
        Assert.False(finalMove);
    }
}
