using ColorVision.Engine.Templates.POI.AlgorithmImp;

namespace ProjectARVRPro.Process.Chessboard
{
    public sealed class ChessboardContrastCalculationResult
    {
        public bool Success { get; init; }
        public string ErrorMessage { get; init; } = string.Empty;
        public int RowCount { get; init; }
        public int ColumnCount { get; init; }
        public double BrightLuminance { get; init; }
        public double RawDarkLuminance { get; init; }
        public double CorrectedDarkLuminance { get; init; }
        public double CorrectedContrast { get; init; }
    }

    public static class ChessboardContrastCalculator
    {
        public static ChessboardContrastCalculationResult CalculateAndApply(
            IReadOnlyList<PoiResultCIExyuvData>? points,
            int configuredRowCount,
            int configuredColumnCount,
            bool firstPointIsBlack,
            double strayLightCoefficient)
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

            var darkPoints = new List<PoiResultCIExyuvData>();
            var brightPoints = new List<PoiResultCIExyuvData>();
            for (int row = 0; row < rowCount; row++)
            {
                for (int column = 0; column < columnCount; column++)
                {
                    var point = spatiallyOrderedPoints[row * columnCount + column];
                    bool isBlack = (((row + column) & 1) == 0) == firstPointIsBlack;
                    (isBlack ? darkPoints : brightPoints).Add(point);
                }
            }

            if (darkPoints.Count == 0 || brightPoints.Count == 0)
                return Fail("棋盘格必须同时包含黑格和白格。");

            double brightLuminance = brightPoints.Average(point => point.Y);
            double rawDarkLuminance = darkPoints.Average(point => point.Y);
            double offset = brightLuminance * strayLightCoefficient;
            if (!double.IsFinite(brightLuminance) || !double.IsFinite(rawDarkLuminance) || !double.IsFinite(offset))
                return Fail("棋盘格亮暗区均值或补偿量无效。");

            double correctedDarkLuminance = rawDarkLuminance - offset;
            if (!double.IsFinite(correctedDarkLuminance) || correctedDarkLuminance <= 0)
                return Fail("修正后的暗格平均亮度必须大于0，请检查杂散光系数和POI数据。");

            double correctedContrast = brightLuminance / correctedDarkLuminance;
            if (!double.IsFinite(correctedContrast))
                return Fail("修正后的棋盘格对比度无效。");

            return new ChessboardContrastCalculationResult
            {
                Success = true,
                RowCount = rowCount,
                ColumnCount = columnCount,
                BrightLuminance = brightLuminance,
                RawDarkLuminance = rawDarkLuminance,
                CorrectedDarkLuminance = correctedDarkLuminance,
                CorrectedContrast = correctedContrast
            };
        }

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
