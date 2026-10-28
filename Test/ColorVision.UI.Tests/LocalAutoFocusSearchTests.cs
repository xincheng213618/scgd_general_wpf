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
            Assert.DoesNotContain(samples, point => point.Position == position);
            double score = 1000000 - Math.Pow(position - 463, 2);
            samples.Add((position, score));
            return score;
        }, (position, _) => finalPosition = position);
        Assert.Equal(result.Position, finalPosition);
        Assert.Equal(samples.Max(point => point.Score), result.Score);
        Assert.InRange(Math.Abs(result.Position - 463), 0, 1);
    }

    [Theory]
    [InlineData(480, 480)]
    [InlineData(-10, 0)]
    [InlineData(1010, 1000)]
    public void LocalPeakSearchStartsFromCurrentPositionWithinTheTemplateRange(int initialPosition, int expectedStart)
    {
        var positions = new List<int>();
        var result = LocalAutoFocusSearch.Run(LocalCameraMotorTests.Settings(FindFuncModel.FindDichotomy), (position, _) =>
        {
            positions.Add(position);
            return 1000000 - Math.Pow(position - 463, 2);
        }, (_, _) => { }, initialPosition: initialPosition);
        Assert.Equal(expectedStart, positions[0]);
        Assert.Equal(positions.Count, positions.Distinct().Count());
        Assert.InRange(Math.Abs(result.Position - 463), 0, 1);
    }

    [Theory]
    [InlineData("parabola")]
    [InlineData("gaussian")]
    [InlineData("asymmetric")]
    [InlineData("multiple")]
    public void InterpolationKeepsGlobalCoarseCoverageAndReturnsAVerifiedPeak(string curve)
    {
        const int peak = 405321;
        var settings = LocalCameraMotorTests.Settings() with
        {
            Minimum = 300000, Maximum = 450000, InitialStep = 5000, FineStep = 2000, Reduction = 0.3, StopStep = 200
        };
        var measured = new Dictionary<int, double>();
        double Score(int position)
        {
            double delta = position - peak;
            return curve switch
            {
                "parabola" => 1e12 - delta * delta,
                "gaussian" => 1000 * Math.Exp(-Math.Pow(delta / 11000, 2)),
                "asymmetric" => 1000 * Math.Exp(-Math.Pow(delta / (delta < 0 ? 8000 : 18000), 2)),
                _ => Math.Max(1000 * Math.Exp(-Math.Pow(delta / 11000, 2)), 900 * Math.Exp(-Math.Pow((position - 335000) / 5000.0, 2)))
            };
        }
        int finalPosition = -1;
        var result = LocalAutoFocusSearch.Run(settings, (position, _) =>
        {
            Assert.True(measured.TryAdd(position, Score(position)), "A fixed position was acquired twice in one search.");
            return measured[position];
        }, (position, _) => finalPosition = position);
        for (int position = settings.Minimum; position <= settings.Maximum; position += settings.InitialStep)
            Assert.Contains(position, measured.Keys);
        Assert.Equal(result.Position, finalPosition);
        Assert.Equal(measured[result.Position], result.Score);
        Assert.Equal(measured.Values.Max(), result.Score);
        Assert.InRange(Math.Abs(result.Position - peak), 0, settings.StopStep);
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
