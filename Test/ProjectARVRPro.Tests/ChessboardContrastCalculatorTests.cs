using ColorVision.Engine.Templates.POI.AlgorithmImp;
using CVCommCore.CVAlgorithm;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using ProjectARVRPro.Process.Chessboard;
using Xunit;

namespace ProjectARVRPro.Tests
{
    public class ChessboardContrastCalculatorTests
    {
        [Fact]
        public void AutoInfersSquareAndCorrectsDarkAverageWithoutMutatingPoi()
        {
            var points = CreateGrid(4, 4, firstPointIsBlack: true, brightLuminance: 180, darkLuminance: 1.2)
                .OrderByDescending(point => point.Point.PixelX + point.Point.PixelY * 7)
                .ToList();

            var result = ChessboardContrastCalculator.Calculate(points, 0, 0, ChessboardFirstPointColor.Auto, 0.003, false);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(4, result.RowCount);
            Assert.Equal(4, result.ColumnCount);
            Assert.Equal(ChessboardFirstPointColor.Auto, result.RequestedFirstPointColor);
            Assert.Equal(ChessboardCellColor.Black, result.ResolvedFirstPointColor);
            Assert.Equal(16, result.ClassifiedPoints.Count);
            Assert.Equal(ChessboardCellColor.Black, result.ClassifiedPoints[0].CellColor);
            Assert.Equal(ChessboardCellColor.White, result.ClassifiedPoints[1].CellColor);
            Assert.Equal(180, result.BrightLuminance, 6);
            Assert.Equal(1.2, result.RawDarkLuminance, 6);
            Assert.Equal(0.66, result.CorrectedDarkLuminance, 6);
            Assert.Equal(272.7272727, result.CorrectedContrast, 6);
            AssertGridLuminance(points, firstPointIsBlack: true, expectedBright: 180, expectedDark: 1.2);
        }

        [Fact]
        public void SupportsExplicitRectangularGridAndWhiteFirstPoint()
        {
            var points = CreateGrid(4, 5, firstPointIsBlack: false, brightLuminance: 100, darkLuminance: 2)
                .OrderByDescending(point => point.Point.PixelY)
                .ThenByDescending(point => point.Point.PixelX)
                .ToList();

            var result = ChessboardContrastCalculator.Calculate(points, 4, 5, ChessboardFirstPointColor.White, 0.01, false);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(4, result.RowCount);
            Assert.Equal(5, result.ColumnCount);
            Assert.Equal(ChessboardCellColor.White, result.ResolvedFirstPointColor);
            Assert.Equal(1, result.CorrectedDarkLuminance, 6);
            Assert.Equal(100, result.CorrectedContrast, 6);
            AssertGridLuminance(points, firstPointIsBlack: false, expectedBright: 100, expectedDark: 2);
        }

        [Fact]
        public void NonSquarePointCountRequiresExplicitDimensionsWithoutMutatingPoi()
        {
            var points = CreateGrid(4, 5, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 2);
            var originalValues = points.Select(point => point.Y).ToArray();

            var result = ChessboardContrastCalculator.Calculate(points, 0, 0, ChessboardFirstPointColor.Black, 0.01, false);

            Assert.False(result.Success);
            Assert.Contains("不能自动推断为方阵", result.ErrorMessage);
            Assert.Equal(originalValues, points.Select(point => point.Y));
        }

        [Fact]
        public void NegativeCorrectedDarkAverageIsRejectedWhenSwitchIsOff()
        {
            var points = CreateGrid(4, 4, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 0.5);
            var originalValues = points.Select(point => point.Y).ToArray();

            var result = ChessboardContrastCalculator.Calculate(points, 0, 0, ChessboardFirstPointColor.Black, 0.01, false);

            Assert.False(result.Success);
            Assert.Contains("负值", result.ErrorMessage);
            Assert.Equal(originalValues, points.Select(point => point.Y));
        }

        [Fact]
        public void NegativeCorrectedDarkAverageIsReturnedWhenSwitchIsOn()
        {
            var points = CreateGrid(4, 4, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 0.5);

            var result = ChessboardContrastCalculator.Calculate(points, 0, 0, ChessboardFirstPointColor.Auto, 0.01, true);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(-0.5, result.CorrectedDarkLuminance, 6);
            Assert.Equal(-200, result.CorrectedContrast, 6);
            Assert.True(result.AllowNegativeCorrectedDarkLuminance);
        }

        [Fact]
        public void LowIndividualDarkPoiDoesNotRejectValidCorrectedAverage()
        {
            var points = CreateGrid(4, 4, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 5);
            points.First(point => point.Point.PixelX == 0 && point.Point.PixelY == 0).Y = 0.5;
            var originalValues = points.Select(point => point.Y).ToArray();

            var result = ChessboardContrastCalculator.Calculate(points, 0, 0, ChessboardFirstPointColor.Black, 0.036, false);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(4.4375, result.RawDarkLuminance, 6);
            Assert.Equal(0.8375, result.CorrectedDarkLuminance, 6);
            Assert.Equal(119.402985, result.CorrectedContrast, 6);
            Assert.Equal(originalValues, points.Select(point => point.Y));
        }

        [Fact]
        public void ZeroCoefficientPreservesPoiAndCalculatesRawContrast()
        {
            var points = CreateGrid(5, 5, firstPointIsBlack: true, brightLuminance: 90, darkLuminance: 3);

            var result = ChessboardContrastCalculator.Calculate(points, 0, 0, ChessboardFirstPointColor.Auto, 0, false);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(3, result.CorrectedDarkLuminance, 6);
            Assert.Equal(30, result.CorrectedContrast, 6);
            AssertGridLuminance(points, firstPointIsBlack: true, expectedBright: 90, expectedDark: 3);
        }

        [Fact]
        public void AutoDetectsWhiteFirstPointAndCalculatesGroupUniformity()
        {
            var points = CreateGrid(2, 2, firstPointIsBlack: false, brightLuminance: 100, darkLuminance: 10);
            points.Single(point => point.Point.PixelX == 0 && point.Point.PixelY == 0).Y = 80;
            points.Single(point => point.Point.PixelX == 10 && point.Point.PixelY == 0).Y = 5;

            var result = ChessboardContrastCalculator.Calculate(points, 2, 2, ChessboardFirstPointColor.Auto, 0, false);

            Assert.True(result.Success, result.ErrorMessage);
            Assert.Equal(ChessboardCellColor.White, result.ResolvedFirstPointColor);
            Assert.Equal(90, result.BrightLuminance, 6);
            Assert.Equal(7.5, result.RawDarkLuminance, 6);
            Assert.Equal(0.8, result.BrightUniformity, 6);
            Assert.Equal(0.5, result.DarkUniformity, 6);
        }

        [Fact]
        public void AutoRejectsIndistinguishableAlternatingGroups()
        {
            var points = CreateGrid(2, 2, firstPointIsBlack: true, brightLuminance: 10, darkLuminance: 10);

            var result = ChessboardContrastCalculator.Calculate(points, 2, 2, ChessboardFirstPointColor.Auto, 0, false);

            Assert.False(result.Success);
            Assert.Contains("Auto无法", result.ErrorMessage);
        }

        [Fact]
        public void ConfigDefaultsToDatabaseResultNameAndSquareAutoInference()
        {
            var config = new ChessboardProcessConfig();
            var dynamicConfig = new ChessboardDynamicProcessConfig();

            Assert.Equal("Chessboard_Contrast", config.ChessboardContrastResultName);
            Assert.Equal(0, config.RowCount);
            Assert.Equal(0, config.ColumnCount);
            Assert.Equal(ChessboardFirstPointColor.Auto, config.FirstPointColor);
            Assert.Equal(0, config.StrayLightCoefficient);
            Assert.False(config.AllowNegativeCorrectedDarkLuminance);
            Assert.Equal("Chessboard_Contrast", dynamicConfig.ChessboardContrastResultName);
            Assert.Equal(0, dynamicConfig.RowCount);
            Assert.Equal(0, dynamicConfig.ColumnCount);
            Assert.Equal(ChessboardFirstPointColor.Auto, dynamicConfig.FirstPointColor);
            Assert.Equal(0, dynamicConfig.StrayLightCoefficient);
            Assert.False(dynamicConfig.AllowNegativeCorrectedDarkLuminance);
        }

        [Theory]
        [InlineData(true, ChessboardFirstPointColor.Black)]
        [InlineData(false, ChessboardFirstPointColor.White)]
        public void LegacyFirstPointBooleanConfigStillDeserializes(bool legacyValue, ChessboardFirstPointColor expected)
        {
            string json = $"{{\"FirstPointIsBlack\":{legacyValue.ToString().ToLowerInvariant()}}}";

            var config = JsonConvert.DeserializeObject<ChessboardProcessConfig>(json);
            var dynamicConfig = JsonConvert.DeserializeObject<ChessboardDynamicProcessConfig>(json);

            Assert.NotNull(config);
            Assert.NotNull(dynamicConfig);
            Assert.Equal(expected, config!.FirstPointColor);
            Assert.Equal(expected, dynamicConfig!.FirstPointColor);
        }

        [Fact]
        public void NewConfigSerializesOnlyTheEnumFirstPointSetting()
        {
            JObject json = JObject.Parse(JsonConvert.SerializeObject(new ChessboardProcessConfig()));

            Assert.Equal("Auto", json["FirstPointColor"]?.Value<string>());
            Assert.Null(json["FirstPointIsBlack"]);
        }

        [Fact]
        public void EnhancedCsvAddsCellColorsAndChessboardStatistics()
        {
            var points = CreateGrid(2, 2, firstPointIsBlack: true, brightLuminance: 100, darkLuminance: 10);
            var calculation = ChessboardContrastCalculator.Calculate(points, 2, 2, ChessboardFirstPointColor.Auto, 0.02, false);
            string header = string.Join(",", Enumerable.Range(0, 15).Select(index => $"column_{index}"));
            string baseCsv = string.Join(Environment.NewLine, new[]
            {
                header,
                "P_0_0,,,,,,,,,,,,,,",
                "P_0_1,,,,,,,,,,,,,,",
                "P_1_0,,,,,,,,,,,,,,",
                "P_1_1,,,,,,,,,,,,,,",
                ",,,,,,,,,,,,,,",
                "Measurement Item,Value,Unit,,,,,,,,,,,,"
            });

            string csv = ChessboardCsvExporter.BuildEnhancedCsvContent(baseCsv, calculation, 12.5, "Chessboard_Contrast", "local");
            string[] lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

            Assert.True(calculation.Success, calculation.ErrorMessage);
            Assert.EndsWith("column_14,cell_color", lines[0]);
            Assert.EndsWith(",Black", lines[1]);
            Assert.EndsWith(",White", lines[2]);
            Assert.EndsWith(",White", lines[3]);
            Assert.EndsWith(",Black", lines[4]);
            Assert.Contains("Requested First Point Color,Auto,", csv);
            Assert.Contains("Resolved First Point Color,Black,", csv);
            Assert.Contains("White Average Luminance,100,cd/m^2", csv);
            Assert.Contains("Black Raw Average Luminance,10,cd/m^2", csv);
            Assert.Contains("White Luminance Uniformity (Min/Max*100%),100,%", csv);
            Assert.Contains("Black Corrected Average Luminance,8,cd/m^2", csv);
            Assert.Contains("Reported Chessboard Contrast,12.5,", csv);
        }

        [Fact]
        public void SocketChessboardResultJsonKeepsExistingFieldsOnly()
        {
            JObject json = JObject.Parse(JsonConvert.SerializeObject(new ChessboardTestResult()));
            string[] propertyNames = json.Properties().Select(property => property.Name).Order().ToArray();

            Assert.Equal(2, propertyNames.Length);
            Assert.Equal("AverageBlackLuminance", propertyNames[0]);
            Assert.Equal("ChessboardContrast", propertyNames[1]);
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
