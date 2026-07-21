using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using ProjectARVRPro.Process.Chessboard;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class ChessboardContrastCalculatorTests
    {
        [Fact]
        public void AutoInfersSquareAndCorrectsBlackPoiWorkingData()
        {
            var points = CreateGrid(4, 4, firstPointIsBlack: true, brightLuminance: 180, darkLuminance: 1.2)
                .OrderByDescending(point => point.Point.PixelX + point.Point.PixelY * 7)
                .ToList();

            var result = ChessboardContrastCalculator.CalculateAndApply(points, 0, 0, true, 0.003);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(4, result.RowCount);
            Assert.Equal(4, result.ColumnCount);
            Assert.Equal(180, result.BrightLuminance, 6);
            Assert.Equal(1.2, result.RawDarkLuminance, 6);
            Assert.Equal(0.66, result.CorrectedDarkLuminance, 6);
            Assert.Equal(272.7272727, result.CorrectedContrast, 6);
            AssertGridLuminance(points, firstPointIsBlack: true, expectedBright: 180, expectedDark: 0.66);
        }

        [Fact]
        public void SupportsExplicitRectangularGridAndWhiteFirstPoint()
        {
            var points = CreateGrid(4, 5, firstPointIsBlack: false, brightLuminance: 100, darkLuminance: 2)
                .OrderByDescending(point => point.Point.PixelY)
                .ThenByDescending(point => point.Point.PixelX)
                .ToList();

            var result = ChessboardContrastCalculator.CalculateAndApply(points, 4, 5, false, 0.01);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(4, result.RowCount);
            Assert.Equal(5, result.ColumnCount);
            Assert.Equal(1, result.CorrectedDarkLuminance, 6);
            Assert.Equal(100, result.CorrectedContrast, 6);
            AssertGridLuminance(points, firstPointIsBlack: false, expectedBright: 100, expectedDark: 1);
        }

        [Fact]
        public void NonSquarePointCountRequiresExplicitDimensionsWithoutMutatingPoi()
        {
            var points = CreateGrid(4, 5, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 2);
            var originalValues = points.Select(point => point.Y).ToArray();

            var result = ChessboardContrastCalculator.CalculateAndApply(points, 0, 0, true, 0.01);

            Assert.False(result.Success);
            Assert.Contains("不能自动推断为方阵", result.ErrorMessage);
            Assert.Equal(originalValues, points.Select(point => point.Y));
        }

        [Fact]
        public void InvalidCorrectedDarkValueDoesNotPartiallyMutatePoi()
        {
            var points = CreateGrid(4, 4, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 0.5);
            var originalValues = points.Select(point => point.Y).ToArray();

            var result = ChessboardContrastCalculator.CalculateAndApply(points, 0, 0, true, 0.01);

            Assert.False(result.Success);
            Assert.Contains("必须大于0", result.ErrorMessage);
            Assert.Equal(originalValues, points.Select(point => point.Y));
        }

        [Fact]
        public void ZeroCoefficientPreservesPoiAndCalculatesRawContrast()
        {
            var points = CreateGrid(5, 5, firstPointIsBlack: true, brightLuminance: 90, darkLuminance: 3);

            var result = ChessboardContrastCalculator.CalculateAndApply(points, 0, 0, true, 0);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(3, result.CorrectedDarkLuminance, 6);
            Assert.Equal(30, result.CorrectedContrast, 6);
            AssertGridLuminance(points, firstPointIsBlack: true, expectedBright: 90, expectedDark: 3);
        }

        [Fact]
        public void ConfigDefaultsToDatabaseResultNameAndSquareAutoInference()
        {
            var config = new ChessboardProcessConfig();
            var dynamicConfig = new ChessboardDynamicProcessConfig();

            Assert.Equal("Chessboard_Contrast", config.ChessboardContrastResultName);
            Assert.Equal(0, config.RowCount);
            Assert.Equal(0, config.ColumnCount);
            Assert.True(config.FirstPointIsBlack);
            Assert.Equal(0, config.StrayLightCoefficient);
            Assert.Equal("Chessboard_Contrast", dynamicConfig.ChessboardContrastResultName);
            Assert.Equal(0, dynamicConfig.RowCount);
            Assert.Equal(0, dynamicConfig.ColumnCount);
            Assert.True(dynamicConfig.FirstPointIsBlack);
            Assert.Equal(0, dynamicConfig.StrayLightCoefficient);
        }

        private static List<PoiResultCIExyuvData> CreateGrid(int rows, int columns, bool firstPointIsBlack, double brightLuminance, double darkLuminance)
        {
            var points = new List<PoiResultCIExyuvData>();
            for (int row = 0; row < rows; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    bool isBlack = (((row + column) & 1) == 0) == firstPointIsBlack;
                    points.Add(new PoiResultCIExyuvData
                    {
                        Point = new POIPoint(row * columns + column, -1, $"P_{row}_{column}", POIPointTypes.Rect, column * 10, row * 10, 5, 5),
                        Y = isBlack ? darkLuminance : brightLuminance
                    });
                }
            }

            return points;
        }

        private static void AssertGridLuminance(IEnumerable<PoiResultCIExyuvData> points, bool firstPointIsBlack, double expectedBright, double expectedDark)
        {
            foreach (var point in points)
            {
                int row = (int)(point.Point.PixelY / 10);
                int column = (int)(point.Point.PixelX / 10);
                bool isBlack = (((row + column) & 1) == 0) == firstPointIsBlack;
                Assert.Equal(isBlack ? expectedDark : expectedBright, point.Y, 6);
            }
        }
    }
}
