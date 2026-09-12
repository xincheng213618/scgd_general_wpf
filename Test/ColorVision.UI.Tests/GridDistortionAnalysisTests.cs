using ColorVision.Core;

namespace ColorVision.UI.Tests;

public sealed class GridDistortionAnalysisTests
{
    [Fact]
    public void OneGridProducesDistinctTvAndBothPoint9Conventions()
    {
        (double X, double Y)[] positions = [(10, 10), (50, 5), (90, 10), (5, 50), (50, 50), (95, 50), (0, 90), (50, 95), (100, 90)];
        GridDistortionResult result = CreateGrid(3, (row, col) => positions[row * 3 + col]);
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        double sideHeight = Math.Sqrt(6500);
        Assert.Equal(0, analysis.StandardTv.HorizontalPercent, 12);
        Assert.Equal(100 * (sideHeight - 90) / 90, analysis.StandardTv.VerticalPercent, 12);
        Assert.Equal(analysis.StandardTv.VerticalPercent / 2, analysis.HalfTv.VerticalPercent, 12);
        Assert.Equal(-500 / sideHeight, analysis.ReferencePoint9.TopPercent, 12);
        Assert.Equal(-500 / sideHeight, analysis.ReferencePoint9.BottomPercent, 12);
        Assert.Equal(0, analysis.ReferencePoint9.KeystoneHorizontalPercent, 12);
        Assert.Equal(-2000.0 / 90, analysis.ReferencePoint9.KeystoneVerticalPercent, 12);
        Assert.Equal(-2000.0 / 90, analysis.LegacyPoint9.KeystoneHorizontalPercent, 12);
        Assert.Equal(0, analysis.LegacyPoint9.KeystoneVerticalPercent, 12);
        Assert.Equal(-1500 / (2 * sideHeight + 90), analysis.LegacyPoint9.TopPercent, 12);
        Assert.NotEqual(analysis.ReferencePoint9.TopPercent, analysis.LegacyPoint9.TopPercent);
        Assert.True(analysis.Optical.IsAvailable);
        Assert.Equal(-100.0 / 9, analysis.Optical.OpticRatioPercent!.Value, 12);
        Assert.False(analysis.Optical.IsCalibrated);
        Assert.Equal(8, analysis.Optical.Samples.Count);
        Assert.Equal("CentralPitchRadial/v1", analysis.Optical.Method);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void AsymmetricReferencePointsMatchIndependentGoldenValuesForBothConventions(int dimension)
    {
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(CreateAsymmetricGrid(dimension));

        // Golden values come directly from the nine coordinates below: Euclidean
        // edge lengths and signed perpendicular bow, not production metric fields.
        Assert.Equal(4.904889425595004, analysis.StandardTv.HorizontalPercent, 10);
        Assert.Equal(10.938199313180869, analysis.StandardTv.VerticalPercent, 10);
        Assert.Equal(2.452444712797502, analysis.HalfTv.HorizontalPercent, 10);
        Assert.Equal(5.4690996565904345, analysis.HalfTv.VerticalPercent, 10);
        Assert.Equal(4.870668807530388, analysis.ReferencePoint9.TopPercent, 10);
        Assert.Equal(4.74931883645871, analysis.ReferencePoint9.BottomPercent, 10);
        Assert.Equal(5.8090915128504585, analysis.ReferencePoint9.LeftPercent, 10);
        Assert.Equal(-1.5065605805687998, analysis.ReferencePoint9.RightPercent, 10);
        Assert.Equal(4.626065853366646, analysis.ReferencePoint9.KeystoneHorizontalPercent, 10);
        Assert.Equal(4.5379722301900856, analysis.ReferencePoint9.KeystoneVerticalPercent, 10);
        Assert.Equal(5.03618683380576, analysis.LegacyPoint9.TopPercent, 10);
        Assert.Equal(4.910713074298847, analysis.LegacyPoint9.BottomPercent, 10);
        Assert.Equal(5.901060693550142, analysis.LegacyPoint9.LeftPercent, 10);
        Assert.Equal(-1.5304123553192657, analysis.LegacyPoint9.RightPercent, 10);
        Assert.Equal(4.609817128333842, analysis.LegacyPoint9.KeystoneHorizontalPercent, 10);
        Assert.Equal(4.783271633461019, analysis.LegacyPoint9.KeystoneVerticalPercent, 10);
    }

    [Fact]
    public void NonReferenceInteriorPointChangesOnlyItsOpticalSample()
    {
        GridDistortionResult grid = CreateAsymmetricGrid(7);
        GridDistortionAnalysis baseline = GridDistortionAnalysis.Calculate(grid);
        // (row 1, col 1) is neither a representative nine-point position nor a
        // central neighbour used for the optical pitch reference.
        const int changedPointId = 8;
        GridDistortionPoint[] changedPoints = grid.Points.Select(point => point.Id == changedPointId
            ? point with { X = point.X + 5, Y = point.Y - 3 }
            : point).ToArray();
        GridDistortionAnalysis changed = GridDistortionAnalysis.Calculate(grid with { Points = changedPoints });

        Assert.Equal(baseline.StandardTv, changed.StandardTv);
        Assert.Equal(baseline.HalfTv, changed.HalfTv);
        Assert.Equal(baseline.ReferencePoint9, changed.ReferencePoint9);
        Assert.Equal(baseline.LegacyPoint9, changed.LegacyPoint9);
        Assert.True(changed.Optical.IsAvailable);
        Assert.Equal(baseline.Optical.Origin, changed.Optical.Origin);
        Assert.Equal(baseline.Optical.ColumnPitch, changed.Optical.ColumnPitch);
        Assert.Equal(baseline.Optical.RowPitch, changed.Optical.RowPitch);
        Assert.Equal(48, changed.Optical.Samples.Count);
        GridDistortionOpticalSample oldSample = baseline.Optical.Samples.Single(sample => sample.PointId == changedPointId);
        GridDistortionOpticalSample newSample = changed.Optical.Samples.Single(sample => sample.PointId == changedPointId);
        Assert.Equal(oldSample.ReferenceRadiusPixels, newSample.ReferenceRadiusPixels);
        Assert.Equal(oldSample.ReferencePosition, newSample.ReferencePosition);
        Assert.NotEqual(oldSample.ActualRadiusPixels, newSample.ActualRadiusPixels);
        Assert.NotEqual(oldSample.RadialPercent, newSample.RadialPercent);
        Assert.Equal(baseline.Optical.Samples.Where(sample => sample.PointId != changedPointId),
            changed.Optical.Samples.Where(sample => sample.PointId != changedPointId));
    }

    [Fact]
    public void DenseGridUsesCentralAdjacentPitchAndEveryMeasuredOpticalPoint()
    {
        const double k = 0.12;
        GridDistortionResult result = CreateGrid(7, (row, col) =>
        {
            double x = 100 * (col - 3), y = 100 * (row - 3);
            double factor = 1 + k * (x * x + y * y) / 180000;
            return (700 + x * factor, 500 + y * factor);
        });
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        double expected = 100 * ((1 + k) / (1 + k / 18) - 1);
        Assert.Equal(expected, analysis.Optical.OpticRatioPercent!.Value, 10);
        Assert.Equal(expected, analysis.Optical.MaxAbsoluteRatioPercent!.Value, 10);
        Assert.Equal(48, analysis.Optical.Samples.Count);
        Assert.Contains(analysis.Optical.MaxErrorPointId!.Value, new[] { 0, 6, 42, 48 });
        Assert.NotEqual(12.0, analysis.Optical.OpticRatioPercent.Value);
        Assert.Equal(100 * (1 + k / 18), analysis.Optical.ColumnPitch.X, 10);
        Assert.Contains(analysis.Optical.Warnings, warning => warning.Contains("参考"));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void AffineTranslationRotationScaleAndShearDoNotCreateRadialEstimate(int dimension)
    {
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(CreateGrid(dimension,
            (row, col) => (1000 + 82 * col + 17 * row, 800 - 11 * col + 105 * row)));
        Assert.True(analysis.Optical.IsAvailable);
        Assert.Equal(0, analysis.Optical.OpticRatioPercent!.Value, 12);
        Assert.Equal(0, analysis.StandardTv.HorizontalPercent, 12);
        Assert.Equal(0, analysis.StandardTv.VerticalPercent, 12);
        Assert.Equal(0, analysis.ReferencePoint9.TopPercent, 12);
    }

    [Fact]
    public void MissingOrDuplicatePointsAndFailedDetectionCannotProduceFakeZeroMetrics()
    {
        GridDistortionResult grid = CreateGrid(3, (row, col) => (100 + col * 50, 100 + row * 50));
        Assert.Throws<ArgumentException>(() => GridDistortionAnalysis.Calculate(grid with { Success = false }));
        Assert.Throws<ArgumentException>(() => GridDistortionAnalysis.Calculate(grid with { Points = grid.Points.Take(8).ToArray() }));
        GridDistortionPoint[] duplicated = grid.Points.ToArray();
        duplicated[8] = duplicated[0];
        Assert.Throws<ArgumentException>(() => GridDistortionAnalysis.Calculate(grid with { Points = duplicated }));
        Assert.Throws<ArgumentException>(() => GridDistortionAnalysis.Calculate(CreateGrid(3, (row, col) => (100 + (col + row) * 50, 100))));
    }

    private static GridDistortionResult CreateAsymmetricGrid(int dimension)
    {
        (double X, double Y)[] anchors =
        [
            (10, 10), (54, 14), (98, 10),
            (14, 52), (52, 52), (96, 54),
            (8, 94), (52, 88), (92, 90)
        ];
        int half = (dimension - 1) / 2;
        return CreateGrid(dimension, (row, col) =>
        {
            // Piecewise bilinear filling preserves the anchors exactly at rows
            // and columns 0/middle/last, while supplying every dense-grid point.
            int blockRow = Math.Min(row / half, 1), blockCol = Math.Min(col / half, 1);
            double v = (double)(row - blockRow * half) / half, u = (double)(col - blockCol * half) / half;
            var tl = anchors[blockRow * 3 + blockCol];
            var tr = anchors[blockRow * 3 + blockCol + 1];
            var bl = anchors[(blockRow + 1) * 3 + blockCol];
            var br = anchors[(blockRow + 1) * 3 + blockCol + 1];
            return (
                (1 - v) * ((1 - u) * tl.X + u * tr.X) + v * ((1 - u) * bl.X + u * br.X),
                (1 - v) * ((1 - u) * tl.Y + u * tr.Y) + v * ((1 - u) * bl.Y + u * br.Y));
        });
    }

    private static GridDistortionResult CreateGrid(int dimension, Func<int, int, (double X, double Y)> position)
    {
        List<GridDistortionPoint> points = [];
        for (int row = 0; row < dimension; row++)
        {
            for (int col = 0; col < dimension; col++)
            {
                (double x, double y) = position(row, col);
                points.Add(new GridDistortionPoint { Id = row * dimension + col, Row = row, Col = col, X = x, Y = y, Area = 100, Contrast = 0.8 });
            }
        }
        return new GridDistortionResult { Success = true, ExpectedRows = dimension, ExpectedCols = dimension, SelectedCount = points.Count, Points = points };
    }
}
