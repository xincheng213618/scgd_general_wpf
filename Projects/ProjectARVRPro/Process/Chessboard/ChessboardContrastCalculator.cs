using ColorVision.Engine.Templates.POI.AlgorithmImp;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace ProjectARVRPro.Process.Chessboard
{
    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChessboardFirstPointColor
    {
        Auto,
        Black,
        White
    }

    [JsonConverter(typeof(StringEnumConverter))]
    public enum ChessboardCellColor
    {
        Black,
        White
    }

    public sealed class ChessboardPoiClassification
    {
        public PoiResultCIExyuvData Point { get; init; } = null!;
        public int RowIndex { get; init; }
        public int ColumnIndex { get; init; }
        public ChessboardCellColor CellColor { get; init; }
    }

    public sealed class ChessboardContrastCalculationResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public int RowCount { get; init; }
        public int ColumnCount { get; init; }
        public ChessboardFirstPointColor RequestedFirstPointColor { get; init; }
        public ChessboardCellColor ResolvedFirstPointColor { get; init; }
        public IReadOnlyList<ChessboardPoiClassification> ClassifiedPoints { get; init; } = Array.Empty<ChessboardPoiClassification>();
        public double BrightLuminance { get; init; }
        public double BrightMinLuminance { get; init; }
        public double BrightMaxLuminance { get; init; }
        public double BrightUniformity { get; init; }
        public double RawDarkLuminance { get; init; }
        public double DarkMinLuminance { get; init; }
        public double DarkMaxLuminance { get; init; }
        public double DarkUniformity { get; init; }
        public double StrayLightCoefficient { get; init; }
        public double StrayLightOffset { get; init; }
        public double CorrectedDarkLuminance { get; init; }
        public double CorrectedContrast { get; init; }
        public bool AllowNegativeCorrectedDarkLuminance { get; init; }
    }

    public static class ChessboardContrastCalculator
    {
        public static ChessboardContrastCalculationResult Calculate(
            IReadOnlyList<PoiResultCIExyuvData>? points,
            int configuredRowCount,
            int configuredColumnCount,
            ChessboardFirstPointColor firstPointColor,
            double strayLightCoefficient,
            bool allowNegativeCorrectedDarkLuminance)
        {
            if (points == null || points.Count == 0)
                return Fail("未找到可用于本地计算的POI数据。");

            if (!double.IsFinite(strayLightCoefficient) || strayLightCoefficient < 0)
                return Fail("杂散光系数必须是大于或等于0的有限数值。");

            if (!TryResolveDimensions(points.Count, configuredRowCount, configuredColumnCount, out int rowCount, out int columnCount, out string dimensionError))
                return Fail(dimensionError);

            if (points.Any(point => point?.Point == null || !double.IsFinite(point.Y)))
                return Fail("POI中存在无效的位置或亮度数据。");

            var spatiallyOrderedPoints = points
                .OrderBy(point => point.Point.PixelY)
                .ThenBy(point => point.Point.PixelX)
                .Chunk(columnCount)
                .SelectMany(row => row.OrderBy(point => point.Point.PixelX))
                .ToList();

            var indexedPoints = new List<(PoiResultCIExyuvData Point, int Row, int Column, bool UsesFirstPattern)>();
            var firstPatternPoints = new List<PoiResultCIExyuvData>();
            var secondPatternPoints = new List<PoiResultCIExyuvData>();
            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < columnCount; column++)
                {
                    var point = spatiallyOrderedPoints[row * columnCount + column];
                    bool usesFirstPattern = ((row + column) & 1) == 0;
                    indexedPoints.Add((point, row, column, usesFirstPattern));
                    (usesFirstPattern ? firstPatternPoints : secondPatternPoints).Add(point);
                }
            }

            if (firstPatternPoints.Count == 0 || secondPatternPoints.Count == 0)
                return Fail("棋盘格必须同时包含黑格和白格。");

            ChessboardCellColor resolvedFirstPointColor;
            if (firstPointColor == ChessboardFirstPointColor.Auto)
            {
                double firstAverage = firstPatternPoints.Average(point => point.Y);
                double secondAverage = secondPatternPoints.Average(point => point.Y);
                if (AreNearlyEqual(firstAverage, secondAverage))
                    return Fail("Auto无法根据亮度判断左上角黑白属性，请检查POI数据或手动指定左上角颜色。");

                resolvedFirstPointColor = firstAverage < secondAverage
                    ? ChessboardCellColor.Black
                    : ChessboardCellColor.White;
            }
            else
            {
                resolvedFirstPointColor = firstPointColor == ChessboardFirstPointColor.Black
                    ? ChessboardCellColor.Black
                    : ChessboardCellColor.White;
            }

            bool firstPatternIsBlack = resolvedFirstPointColor == ChessboardCellColor.Black;
            var darkPoints = new List<PoiResultCIExyuvData>();
            var brightPoints = new List<PoiResultCIExyuvData>();
            var classifiedPoints = new List<ChessboardPoiClassification>(indexedPoints.Count);
            foreach (var indexedPoint in indexedPoints)
            {
                bool isBlack = indexedPoint.UsesFirstPattern == firstPatternIsBlack;
                (isBlack ? darkPoints : brightPoints).Add(indexedPoint.Point);
                classifiedPoints.Add(new ChessboardPoiClassification
                {
                    Point = indexedPoint.Point,
                    RowIndex = indexedPoint.Row,
                    ColumnIndex = indexedPoint.Column,
                    CellColor = isBlack ? ChessboardCellColor.Black : ChessboardCellColor.White
                });
            }

            double brightLuminance = brightPoints.Average(point => point.Y);
            double rawDarkLuminance = darkPoints.Average(point => point.Y);
            double brightMinLuminance = brightPoints.Min(point => point.Y);
            double brightMaxLuminance = brightPoints.Max(point => point.Y);
            double darkMinLuminance = darkPoints.Min(point => point.Y);
            double darkMaxLuminance = darkPoints.Max(point => point.Y);
            double offset = brightLuminance * strayLightCoefficient;
            if (!double.IsFinite(brightLuminance) || !double.IsFinite(rawDarkLuminance) || !double.IsFinite(offset))
                return Fail("棋盘格亮暗区均值或补偿量无效。");

            double correctedDarkLuminance = rawDarkLuminance - offset;
            if (!double.IsFinite(correctedDarkLuminance) || correctedDarkLuminance == 0)
                return Fail("修正后的暗格平均亮度不能为0，请检查杂散光系数和POI数据。");
            if (correctedDarkLuminance < 0 && !allowNegativeCorrectedDarkLuminance)
                return Fail("修正后的暗格平均亮度为负值；如需保留并显示该结果，请启用负值修正结果开关。");

            double correctedContrast = brightLuminance / correctedDarkLuminance;
            if (!double.IsFinite(correctedContrast))
                return Fail("修正后的棋盘格对比度无效。");

            return new ChessboardContrastCalculationResult
            {
                Success = true,
                RowCount = rowCount,
                ColumnCount = columnCount,
                RequestedFirstPointColor = firstPointColor,
                ResolvedFirstPointColor = resolvedFirstPointColor,
                ClassifiedPoints = classifiedPoints,
                BrightLuminance = brightLuminance,
                BrightMinLuminance = brightMinLuminance,
                BrightMaxLuminance = brightMaxLuminance,
                BrightUniformity = CalculateUniformity(brightMinLuminance, brightMaxLuminance),
                RawDarkLuminance = rawDarkLuminance,
                DarkMinLuminance = darkMinLuminance,
                DarkMaxLuminance = darkMaxLuminance,
                DarkUniformity = CalculateUniformity(darkMinLuminance, darkMaxLuminance),
                StrayLightCoefficient = strayLightCoefficient,
                StrayLightOffset = offset,
                CorrectedDarkLuminance = correctedDarkLuminance,
                CorrectedContrast = correctedContrast,
                AllowNegativeCorrectedDarkLuminance = allowNegativeCorrectedDarkLuminance
            };
        }

        private static bool AreNearlyEqual(double left, double right)
        {
            double scale = Math.Max(1, Math.Max(Math.Abs(left), Math.Abs(right)));
            return Math.Abs(left - right) <= scale * 1e-9;
        }

        private static double CalculateUniformity(double minimum, double maximum) => maximum == 0 ? 0 : minimum / maximum;

        private static bool TryResolveDimensions(int pointCount, int configuredRowCount, int configuredColumnCount, out int rowCount, out int columnCount, out string errorMessage)
        {
            rowCount = configuredRowCount;
            columnCount = configuredColumnCount;
            errorMessage = string.Empty;

            if (configuredRowCount == 0 && configuredColumnCount == 0)
            {
                int squareSize = (int)Math.Sqrt(pointCount);
                if (squareSize * squareSize != pointCount)
                {
                    errorMessage = $"{pointCount}个POI不能自动推断为方阵，请显式设置棋盘格行数和列数。";
                    return false;
                }

                rowCount = squareSize;
                columnCount = squareSize;
                return true;
            }

            if (configuredRowCount <= 0 || configuredColumnCount <= 0)
            {
                errorMessage = "棋盘格行数和列数必须同时为0（自动方阵）或同时大于0。";
                return false;
            }

            if ((long)configuredRowCount * configuredColumnCount != pointCount)
            {
                errorMessage = $"棋盘格配置为{configuredRowCount}x{configuredColumnCount}，但实际有{pointCount}个POI。";
                return false;
            }

            return true;
        }

        private static ChessboardContrastCalculationResult Fail(string errorMessage) => new()
        {
            Success = false,
            ErrorMessage = errorMessage
        };
    }
}
