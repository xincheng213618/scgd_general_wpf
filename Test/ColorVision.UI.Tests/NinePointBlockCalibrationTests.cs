using ColorVision.Core;
using System.Threading;

namespace ColorVision.UI.Tests;

public sealed class NinePointBlockCalibrationTests
{
    private static readonly HImage Image = new() { cols = 1400, rows = 1400, channels = 1, depth = 8, stride = 1400, pData = new IntPtr(1) };
    private static readonly LuminousAreaPoint[] Seeds =
        [new(300, 300), new(650, 300), new(1000, 300), new(300, 650), new(650, 650), new(1000, 650), new(300, 1000), new(650, 1000), new(1000, 1000)];

    [Fact]
    public void KeepsInputIdentityAndUsesDiagonalIntersectionInsteadOfCornerAverage()
    {
        int calls = 0;
        IReadOnlyList<NinePointBlockCalibrationPoint> points = NinePointBlockCalibrator.RefineWithDetector(Image, Seeds, new(), (_, roi, _) =>
        {
            LuminousAreaPoint seed = Seeds[calls++];
            Assert.Equal((int)seed.X - 160, roi.X);
            Assert.Equal((int)seed.Y - 160, roi.Y);
            Assert.Equal(321, roi.Width);
            return Detection([new(seed.X - 20, seed.Y - 20), new(seed.X + 20, seed.Y - 20),
                new(seed.X + 10, seed.Y + 20), new(seed.X - 10, seed.Y + 20)]);
        });

        Assert.Equal(9, calls);
        for (int index = 0; index < points.Count; index++)
        {
            Assert.Equal(index, points[index].Index);
            Assert.Equal(Seeds[index], points[index].Seed);
            Assert.Equal(Seeds[index].X, points[index].Center.X, 8);
            Assert.Equal(Seeds[index].Y + 20d / 3, points[index].Center.Y, 8);
        }
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("ambiguous")]
    [InlineData("touching")]
    [InlineData("shift")]
    [InlineData("side")]
    public void UnreliableBlockKeepsItsSeedWhileOtherPointsRemainCalibrated(string problem)
    {
        int calls = 0;
        IReadOnlyList<NinePointBlockCalibrationPoint> points =
            NinePointBlockCalibrator.RefineWithDetector(Image, Seeds, new(), (_, roi, _) =>
            {
                LuminousAreaPoint seed = Seeds[calls++];
                if (calls != 9) return Block(new(seed.X + 3, seed.Y + 4));
                return problem switch
                {
                    "missing" => LuminousAreaDetectionResult.CreateFailure("RobustV2", "NoCandidate"),
                    "ambiguous" => Block(seed, warnings: ["AmbiguousCandidates"]),
                    "touching" => Detection([new(roi.X, seed.Y - 20), new(seed.X + 20, seed.Y - 20),
                        new(seed.X + 20, seed.Y + 20), new(roi.X, seed.Y + 20)]),
                    "shift" => Block(new(seed.X + 81, seed.Y)),
                    "side" => Detection(Block(seed).Corners, sides: []),
                    _ => throw new InvalidOperationException()
                };
            });
        Assert.Equal(9, calls);
        Assert.Equal(9, points.Count);
        Assert.All(points.Take(8), point =>
        {
            Assert.True(point.Success);
            Assert.Equal(new(point.Seed.X + 3, point.Seed.Y + 4), point.Center);
        });
        Assert.False(points[8].Success);
        Assert.Equal(Seeds[8], points[8].Center);
        Assert.NotEmpty(points[8].FailureReason);
    }

    [Fact]
    public void OverlappingTargetsBothKeepTheirSeedsWithoutFavoringTheFirst()
    {
        LuminousAreaPoint[] seeds = Seeds.ToArray();
        seeds[1] = new(330, 300);
        int calls = 0;
        IReadOnlyList<NinePointBlockCalibrationPoint> points =
            NinePointBlockCalibrator.RefineWithDetector(Image, seeds, new(), (_, _, _) => Block(seeds[calls++]));
        Assert.Equal(9, calls);
        Assert.False(points[0].Success);
        Assert.False(points[1].Success);
        Assert.Contains("重叠", points[0].FailureReason);
        Assert.Contains("重叠", points[1].FailureReason);
        Assert.All(points.Skip(2), point => Assert.True(point.Success));
    }

    [Fact]
    public void BlackImageKeepsAllNineSeedsAndDoesNotRequireAnyBlock()
    {
        IReadOnlyList<NinePointBlockCalibrationPoint> points = NinePointBlockCalibrator.RefineWithDetector(Image, Seeds, new(),
            (_, _, _) => LuminousAreaDetectionResult.CreateFailure("RobustV2", "NoSignal"));
        Assert.Equal(9, points.Count);
        Assert.All(points, point =>
        {
            Assert.False(point.Success);
            Assert.Equal(point.Seed, point.Center);
            Assert.Contains("NoSignal", point.FailureReason);
        });
    }

    [Theory]
    [InlineData("count")]
    [InlineData("invalid")]
    [InlineData("duplicate")]
    public void ValidatesAllSeedsBeforeCallingNative(string problem)
    {
        LuminousAreaPoint[] seeds = Seeds.ToArray();
        if (problem == "count") seeds = seeds[..8];
        if (problem == "invalid") seeds[8] = new(double.NaN, 30);
        if (problem == "duplicate") seeds[8] = seeds[0];
        int calls = 0;
        Assert.Throws<ArgumentException>(() => NinePointBlockCalibrator.RefineWithDetector(Image, seeds, new(), (_, _, _) =>
        {
            calls++;
            return Block(Seeds[0]);
        }));
        Assert.Equal(0, calls);
    }

    [Fact]
    public void SearchRegionOutsideImageKeepsThatPointWithoutCallingNativeForIt()
    {
        LuminousAreaPoint[] seeds = Seeds.ToArray();
        seeds[8] = new(30, 30);
        int calls = 0;
        IReadOnlyList<NinePointBlockCalibrationPoint> points = NinePointBlockCalibrator.RefineWithDetector(Image, seeds, new(),
            (_, _, _) => Block(seeds[calls++]));
        Assert.Equal(8, calls);
        Assert.False(points[8].Success);
        Assert.Equal(seeds[8], points[8].Center);
    }

    [Fact]
    public void NativeInfrastructureFailureIsNotReportedAsAMissingBlock()
    {
        Assert.Throws<InvalidOperationException>(() => NinePointBlockCalibrator.RefineWithDetector(Image, Seeds, new(),
            (_, _, _) => LuminousAreaDetectionResult.CreateFailure("RobustV2", "NativeLibraryUnavailable")));
    }

    [Fact]
    public void CancellationAfterDetectionDoesNotReturnPartialCalibration()
    {
        using CancellationTokenSource cancellation = new();
        int calls = 0;
        Assert.Throws<OperationCanceledException>(() => NinePointBlockCalibrator.RefineWithDetector(Image, Seeds, new(), (_, _, _) =>
        {
            cancellation.Cancel();
            return Block(Seeds[calls++]);
        }, cancellation.Token));
        Assert.Equal(1, calls);
    }

    private static LuminousAreaDetectionResult Block(LuminousAreaPoint center, string[]? warnings = null, double confidence = 0.95) =>
        Detection([new(center.X - 20, center.Y - 20), new(center.X + 20, center.Y - 20),
            new(center.X + 20, center.Y + 20), new(center.X - 20, center.Y + 20)], warnings, confidence);

    private static LuminousAreaDetectionResult Detection(IReadOnlyList<LuminousAreaPoint> corners, string[]? warnings = null,
        double confidence = 0.95, IReadOnlyList<LuminousAreaSideQuality>? sides = null) =>
        new(true, "RobustV2", corners, confidence, sides ?? new[] { "Top", "Right", "Bottom", "Left" }
            .Select(name => new LuminousAreaSideQuality(name, 0.95, new Dictionary<string, double>
            {
                ["Coverage"] = 1, ["InlierCount"] = 40, ["SampleCount"] = 40, ["ContrastP10"] = 0.8
            })).ToArray(), string.Empty, warnings);
}
