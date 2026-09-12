using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Core
{
    public enum GridTvFormula
    {
        [Description("标准 TV")]
        Standard,
        [Description("半值 TV")]
        Half
    }

    public enum GridPoint9Formula
    {
        [Description("对边平均参考")]
        OppositeEdgeMean,
        [Description("旧 P9 三跨度口径")]
        LegacyThreeSpanMean
    }

    public sealed record GridDistortionTvMetrics(double HorizontalPercent, double VerticalPercent);

    public sealed record GridDistortionPoint9Metrics
    {
        public double TopPercent { get; init; }
        public double BottomPercent { get; init; }
        public double LeftPercent { get; init; }
        public double RightPercent { get; init; }
        public double KeystoneHorizontalPercent { get; init; }
        public double KeystoneVerticalPercent { get; init; }
    }

    public sealed record GridDistortionVector(double X, double Y);

    public sealed record GridDistortionOpticalSample
    {
        public int PointId { get; init; }
        public double ActualRadiusPixels { get; init; }
        public double ReferenceRadiusPixels { get; init; }
        public double RadialPercent { get; init; }
        public GridDistortionVector ReferencePosition { get; init; } = new(0, 0);
    }

    /// <summary>
    /// Relative radial distortion using a central-pitch affine reference. This
    /// reference is estimated from the target, not a calibrated ideal image.
    /// Perspective and an off-centre optical axis are not separated by this model.
    /// </summary>
    public sealed record GridDistortionOpticalEstimate
    {
        public bool IsAvailable { get; init; }
        public string Method { get; init; } = "CentralPitchRadial/v1";
        public bool IsCalibrated => false;
        public double? OpticRatioPercent { get; init; }
        public double? MaxAbsoluteRatioPercent { get; init; }
        public int? MaxErrorPointId { get; init; }
        public GridDistortionVector Origin { get; init; } = new(0, 0);
        public GridDistortionVector ColumnPitch { get; init; } = new(0, 0);
        public GridDistortionVector RowPitch { get; init; } = new(0, 0);
        public IReadOnlyList<GridDistortionOpticalSample> Samples { get; init; } = Array.Empty<GridDistortionOpticalSample>();
        public string ReferenceDescription { get; init; } = "以中心点及其上下左右相邻点建立节距参考，D=100×(实际半径−参考半径)/参考半径；最大绝对值保留符号。";
        public IReadOnlyList<string> Warnings { get; init; } = new[]
        {
            "中心节距是图卡自身估计的参考，不能视为已标定的理想放大率或供应商光学畸变等价值。",
            "该相对估计不分离透视与光轴偏心；稀疏九点的参考跨度较大，中心区域畸变会影响结果。"
        };
    }

    /// <summary>Computes every supported convention from one measured grid.</summary>
    public sealed record GridDistortionAnalysis
    {
        public string FormulaVersion => "point-grid-metrics/1";
        public GridDistortionTvMetrics StandardTv { get; init; } = new(0, 0);
        public GridDistortionTvMetrics HalfTv { get; init; } = new(0, 0);
        public GridDistortionPoint9Metrics ReferencePoint9 { get; init; } = new();
        public GridDistortionPoint9Metrics LegacyPoint9 { get; init; } = new();
        public GridDistortionOpticalEstimate Optical { get; init; } = new();

        public static GridDistortionAnalysis Calculate(GridDistortionResult result)
        {
            ArgumentNullException.ThrowIfNull(result);
            int rows = result.ExpectedRows, cols = result.ExpectedCols;
            if (!result.Success || !GridDistortionOptions.IsValidDimension(rows) || !GridDistortionOptions.IsValidDimension(cols)
                || result.Points.Count != rows * cols)
                throw new ArgumentException("指标计算需要完整且成功的奇数点阵。", nameof(result));

            var grid = new GridDistortionPoint[rows, cols];
            var ids = new HashSet<int>();
            foreach (GridDistortionPoint point in result.Points)
            {
                if (point.Row < 0 || point.Row >= rows || point.Col < 0 || point.Col >= cols
                    || !double.IsFinite(point.X) || !double.IsFinite(point.Y)
                    || grid[point.Row, point.Col] != null || !ids.Add(point.Id))
                    throw new ArgumentException("点阵坐标无效或包含重复点。", nameof(result));
                grid[point.Row, point.Col] = point;
            }

            int mr = rows / 2, mc = cols / 2;
            GridDistortionPoint tl = grid[0, 0], tc = grid[0, mc], tr = grid[0, cols - 1];
            GridDistortionPoint ml = grid[mr, 0], center = grid[mr, mc], right = grid[mr, cols - 1];
            GridDistortionPoint bl = grid[rows - 1, 0], bc = grid[rows - 1, mc], br = grid[rows - 1, cols - 1];
            double top = Distance(tl, tr), middle = Distance(ml, right), bottom = Distance(bl, br);
            double left = Distance(tl, bl), verticalCenter = Distance(tc, bc), rightHeight = Distance(tr, br);
            double widthMean = (top + bottom) / 2, heightMean = (left + rightHeight) / 2;
            double widthLegacy = (top + middle + bottom) / 3, heightLegacy = (left + verticalCenter + rightHeight) / 3;
            if (new[] { top, middle, bottom, left, verticalCenter, rightHeight, widthMean, heightMean, widthLegacy, heightLegacy }
                .Any(value => !double.IsFinite(value) || value <= 1e-9))
                throw new ArgumentException("点阵参考跨度退化。", nameof(result));
            double cross = (right.X - ml.X) * (bc.Y - tc.Y) - (right.Y - ml.Y) * (bc.X - tc.X);
            if (!double.IsFinite(cross) || Math.Abs(cross) <= 1e-6 * middle * verticalCenter)
                throw new ArgumentException("点阵横纵方向退化为共线。", nameof(result));

            double topBow = SignedDistance(tc, tl, tr), bottomBow = -SignedDistance(bc, bl, br);
            double leftBow = -SignedDistance(ml, tl, bl), rightBow = SignedDistance(right, tr, br);
            double tvH = 100 * (widthMean - middle) / middle;
            double tvV = 100 * (heightMean - verticalCenter) / verticalCenter;
            var reference = new GridDistortionPoint9Metrics
            {
                TopPercent = 100 * topBow / heightMean, BottomPercent = 100 * bottomBow / heightMean,
                LeftPercent = 100 * leftBow / widthMean, RightPercent = 100 * rightBow / widthMean,
                KeystoneHorizontalPercent = 100 * (left - rightHeight) / heightMean,
                KeystoneVerticalPercent = 100 * (top - bottom) / widthMean
            };
            // Preserve the legacy P9 labels as well as its three-span denominator.
            var legacy = new GridDistortionPoint9Metrics
            {
                TopPercent = 100 * topBow / heightLegacy, BottomPercent = 100 * bottomBow / heightLegacy,
                LeftPercent = 100 * leftBow / widthLegacy, RightPercent = 100 * rightBow / widthLegacy,
                KeystoneHorizontalPercent = 100 * (top - bottom) / widthLegacy,
                KeystoneVerticalPercent = 100 * (left - rightHeight) / heightLegacy
            };
            return new GridDistortionAnalysis
            {
                StandardTv = new(tvH, tvV), HalfTv = new(tvH / 2, tvV / 2),
                ReferencePoint9 = reference, LegacyPoint9 = legacy,
                Optical = CalculateOptical(grid, rows, cols, center)
            };
        }

        private static GridDistortionOpticalEstimate CalculateOptical(GridDistortionPoint[,] grid, int rows, int cols, GridDistortionPoint center)
        {
            int mr = rows / 2, mc = cols / 2;
            var columnPitch = new GridDistortionVector((grid[mr, mc + 1].X - grid[mr, mc - 1].X) / 2, (grid[mr, mc + 1].Y - grid[mr, mc - 1].Y) / 2);
            var rowPitch = new GridDistortionVector((grid[mr + 1, mc].X - grid[mr - 1, mc].X) / 2, (grid[mr + 1, mc].Y - grid[mr - 1, mc].Y) / 2);
            double determinant = columnPitch.X * rowPitch.Y - columnPitch.Y * rowPitch.X;
            double scale = Math.Sqrt(columnPitch.X * columnPitch.X + columnPitch.Y * columnPitch.Y)
                * Math.Sqrt(rowPitch.X * rowPitch.X + rowPitch.Y * rowPitch.Y);
            var estimate = new GridDistortionOpticalEstimate { Origin = new(center.X, center.Y), ColumnPitch = columnPitch, RowPitch = rowPitch };
            if (!double.IsFinite(scale) || scale <= 1e-9 || !double.IsFinite(determinant) || Math.Abs(determinant) <= 1e-6 * scale)
                return estimate with { Warnings = new[] { "中央节距参考退化，无法计算相对光学畸变。" } };

            var samples = new List<GridDistortionOpticalSample>(rows * cols - 1);
            for (int row = 0; row < rows; row++)
            {
                for (int col = 0; col < cols; col++)
                {
                    if (row == mr && col == mc) continue;
                    double referenceX = (col - mc) * columnPitch.X + (row - mr) * rowPitch.X;
                    double referenceY = (col - mc) * columnPitch.Y + (row - mr) * rowPitch.Y;
                    double predicted = Math.Sqrt(referenceX * referenceX + referenceY * referenceY);
                    double actual = Distance(grid[row, col], center);
                    if (!double.IsFinite(predicted) || predicted <= 1e-9 || !double.IsFinite(actual))
                        return estimate with { Warnings = new[] { "存在无效的光学参考半径。" } };
                    double ratio = 100 * (actual - predicted) / predicted;
                    if (!double.IsFinite(ratio)) return estimate;
                    samples.Add(new GridDistortionOpticalSample
                    {
                        PointId = grid[row, col].Id, ActualRadiusPixels = actual, ReferenceRadiusPixels = predicted,
                        RadialPercent = ratio, ReferencePosition = new(center.X + referenceX, center.Y + referenceY)
                    });
                }
            }
            GridDistortionOpticalSample worst = samples.OrderByDescending(sample => Math.Abs(sample.RadialPercent)).ThenBy(sample => sample.PointId).First();
            return estimate with
            {
                IsAvailable = true, OpticRatioPercent = worst.RadialPercent,
                MaxAbsoluteRatioPercent = Math.Abs(worst.RadialPercent), MaxErrorPointId = worst.PointId, Samples = samples
            };
        }

        private static double Distance(GridDistortionPoint a, GridDistortionPoint b) => Math.Sqrt((a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y));

        private static double SignedDistance(GridDistortionPoint point, GridDistortionPoint start, GridDistortionPoint end) =>
            ((end.X - start.X) * (point.Y - start.Y) - (end.Y - start.Y) * (point.X - start.X)) / Distance(start, end);
    }
}
