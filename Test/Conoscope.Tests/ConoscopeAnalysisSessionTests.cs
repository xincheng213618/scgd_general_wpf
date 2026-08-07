using Conoscope.Analysis;
using Conoscope.ApplicationServices.Analysis;
using Conoscope.Core;

namespace Conoscope.Tests;

public class ConoscopeAnalysisSessionTests
{
    [Fact]
    public void SessionComputesAlignedFocusPointContrast()
    {
        ConoscopeAnalysisSession session = new();
        session.RecordCapture(CaptureSlot.ContrastWhite, Capture("W", ("center", 100), ("edge", 40)));
        session.RecordCapture(CaptureSlot.ContrastBlack, Capture("B", ("center", 2), ("edge", 1)));

        var (result, error) = session.ComputeContrast();

        Assert.Null(error);
        Assert.NotNull(result);
        Assert.Collection(
            result.Points,
            center => Assert.Equal(50, center.Ratio, precision: 6),
            edge => Assert.Equal(40, edge.Ratio, precision: 6));
    }

    [Fact]
    public void SinglePointReferenceBroadcastsAcrossNamedFocusPoints()
    {
        MeasurementCapture white = Capture("W", ("center", 100), ("edge", 40));
        MeasurementCapture black = Capture("B", ("reference", 2));

        ContrastComputationResult result = ConoscopeAnalysis.CalculateContrast(white, black);

        Assert.Collection(
            result.Points,
            center => Assert.Equal(50, center.Ratio, precision: 6),
            edge => Assert.Equal(20, edge.Ratio, precision: 6));
    }

    [Fact]
    public void MissingSlotsReturnFriendlyErrorInsteadOfThrowing()
    {
        ConoscopeAnalysisSession session = new();

        var (result, error) = session.ComputeContrast();

        Assert.Null(result);
        Assert.False(string.IsNullOrWhiteSpace(error));
    }

    private static MeasurementCapture Capture(string slotName, params (string Key, double Y)[] values)
    {
        MeasurementPoint[] points = values.Select(value => new MeasurementPoint(
            value.Key,
            value.Key,
            new ImageMeasurement(value.Key, value.Y, value.Y, value.Y, ConoscopeColorimetry.Calculate(value.Y, value.Y, value.Y)),
            null,
            null,
            null)).ToArray();
        return new MeasurementCapture(slotName, "test", points);
    }
}
