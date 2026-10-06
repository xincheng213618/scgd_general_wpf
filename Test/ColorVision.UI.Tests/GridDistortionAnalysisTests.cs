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
        Assert.False(analysis.Optical.IsCalibrated);
        Assert.Equal("CenteredProjectiveBrownK1/v2", analysis.Optical.Method);
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
    public void NonReferenceInteriorOutlierRejectsOpticalFitWithoutChangingTvOrPoint9()
    {
        GridDistortionResult grid = CreateRadialGrid(7, 7, 0.12, false);
        GridDistortionAnalysis baseline = GridDistortionAnalysis.Calculate(grid);
        // The optical fit must inspect points outside the representative nine.
        const int changedPointId = 8;
        GridDistortionPoint[] changedPoints = grid.Points.Select(point => point.Id == changedPointId
            ? point with { X = point.X + 30, Y = point.Y - 20 }
            : point).ToArray();
        GridDistortionAnalysis changed = GridDistortionAnalysis.Calculate(grid with { Points = changedPoints });

        Assert.Equal(baseline.StandardTv, changed.StandardTv);
        Assert.Equal(baseline.HalfTv, changed.HalfTv);
        Assert.Equal(baseline.ReferencePoint9, changed.ReferencePoint9);
        Assert.Equal(baseline.LegacyPoint9, changed.LegacyPoint9);
        Assert.True(baseline.Optical.IsAvailable);
        Assert.False(changed.Optical.IsAvailable);
        Assert.Null(changed.Optical.OpticRatioPercent);
        Assert.Empty(changed.Optical.Samples);
        Assert.NotEmpty(changed.Optical.Warnings);
    }

    [Theory]
    [InlineData(3, 3, 0.12, false)]
    [InlineData(3, 3, -0.12, false)]
    [InlineData(7, 7, 0.12, false)]
    [InlineData(7, 7, -0.12, false)]
    [InlineData(15, 15, 0.12, false)]
    [InlineData(15, 15, -0.12, false)]
    [InlineData(3, 5, 0.12, true)]
    [InlineData(5, 3, -0.12, true)]
    [InlineData(3, 3, 0.12, true)]
    [InlineData(3, 3, -0.12, true)]
    [InlineData(7, 7, 0.12, true)]
    [InlineData(7, 7, -0.12, true)]
    public void IndependentForwardBrownModelRecoversKnownRadialTruth(int rows, int cols, double k, bool perspective)
    {
        GridDistortionResult result = CreateRadialGrid(rows, cols, k, perspective);
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(result);
        Assert.True(analysis.Optical.IsAvailable, string.Join("; ", analysis.Optical.Warnings));
        Assert.InRange(Math.Abs(analysis.Optical.OpticRatioPercent!.Value - k * 100), 0, 1e-6);
        Assert.InRange(Math.Abs(analysis.Optical.MaxAbsoluteRatioPercent!.Value - Math.Abs(k) * 100), 0, 1e-6);
        Assert.InRange(analysis.Optical.FitRmsPixels!.Value, 0, 1e-6);
        Assert.Equal(rows * cols - 1, analysis.Optical.Samples.Count);
        Assert.False(analysis.Optical.IsCalibrated);
        Assert.Equal("point-grid-metrics/2", analysis.FormulaVersion);
        Assert.Contains(analysis.Optical.Warnings, warning => warning.Contains("光学中心"));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void PurePerspectiveHasZeroRadialDistortionWhileTvStillReportsGeometry(int dimension)
    {
        GridDistortionAnalysis analysis = GridDistortionAnalysis.Calculate(CreateRadialGrid(dimension, dimension, 0, true));
        Assert.True(analysis.Optical.IsAvailable);
        Assert.InRange(Math.Abs(analysis.Optical.OpticRatioPercent!.Value), 0, 1e-6);
        Assert.NotEqual(0, analysis.StandardTv.HorizontalPercent);
    }

    [Theory]
    [InlineData(3)]
    [InlineData(7)]
    public void SubpixelNoiseHasReportedResidualAndSmallTruthError(int dimension)
    {
        var random = new Random(721);
        GridDistortionResult result = CreateRadialGrid(dimension, dimension, 0.12, true);
        result = result with { Points = result.Points.Select(point => point.Row == dimension / 2 && point.Col == dimension / 2
            ? point : point with { X = point.X + (random.NextDouble() - 0.5) * 0.16, Y = point.Y + (random.NextDouble() - 0.5) * 0.16 }).ToArray() };
        GridDistortionOpticalEstimate optical = GridDistortionAnalysis.Calculate(result).Optical;
        Assert.True(optical.IsAvailable);
        Assert.InRange(Math.Abs(optical.OpticRatioPercent!.Value - 12), 0, 0.15);
        Assert.InRange(optical.FitRmsPixels!.Value, 0.001, 0.2);
    }

    [Fact]
    public void UnmodelledHighOrderDistortionCannotBePublishedAsValidK1Estimate()
    {
        GridDistortionResult result = CreateGrid(7, (row, col) =>
        {
            double x = (col - 3) * 100, y = (row - 3) * 100, radiusSquared = (x * x + y * y) / 180000;
            double factor = 1 + 0.3 * radiusSquared * radiusSquared;
            return (700 + x * factor, 500 + y * factor);
        });
        GridDistortionOpticalEstimate optical = GridDistortionAnalysis.Calculate(result).Optical;
        Assert.False(optical.IsAvailable);
        Assert.Null(optical.OpticRatioPercent);
        Assert.Contains(optical.Warnings, warning => warning.Contains("模型"));
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
        => CreateGrid(dimension, dimension, position);

    // Independent forward generator: project the planar chart first, then apply
    // Brown radial displacement in image coordinates. At the furthest ideal dot
    // the known radial distortion is exactly k*100, even with perspective.
    private static GridDistortionResult CreateRadialGrid(int rows, int cols, double k, bool perspective)
    {
        var ideal = new (double X, double Y)[rows, cols];
        double maximumRadiusSquared = 0;
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                double u = 2.0 * col / (cols - 1) - 1, v = 2.0 * row / (rows - 1) - 1;
                double denominator = perspective ? 1 + 0.12 * u - 0.09 * v : 1;
                double x = (280 * u - 100 * v) / denominator, y = (100 * u + 280 * v) / denominator;
                ideal[row, col] = (x, y);
                maximumRadiusSquared = Math.Max(maximumRadiusSquared, x * x + y * y);
            }
        }
        return CreateGrid(rows, cols, (row, col) =>
        {
            var (x, y) = ideal[row, col];
            double radial = 1 + k * (x * x + y * y) / maximumRadiusSquared;
            return (700 + x * radial, 500 + y * radial);
        });
    }

    private static GridDistortionResult CreateGrid(int rows, int cols, Func<int, int, (double X, double Y)> position)
    {
        List<GridDistortionPoint> points = [];
        for (int row = 0; row < rows; row++)
        {
            for (int col = 0; col < cols; col++)
            {
                (double x, double y) = position(row, col);
                points.Add(new GridDistortionPoint { Id = row * cols + col, Row = row, Col = col, X = x, Y = y, Area = 100, Contrast = 0.8 });
            }
        }
        return new GridDistortionResult { Success = true, ExpectedRows = rows, ExpectedCols = cols, SelectedCount = points.Count, Points = points };
    }
}
