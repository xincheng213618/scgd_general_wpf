using ColorVision.Database;
using ColorVision.Engine.Templates.POI.AlgorithmImp;
using ColorVision.ImageEditor;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;

namespace ColorVision.Engine.Templates.POI
{
    internal readonly record struct LocalPoiMappingPoint(double X, double Y);

    internal sealed class LocalPoiRemappedPoint
    {
        public int PoiId { get; init; }
        public string Name { get; init; } = string.Empty;
        public POIPointTypes PointType { get; init; }
        public int X { get; init; }
        public int Y { get; init; }
        public int Width { get; init; }
        public int Height { get; init; }
    }

    internal static class LocalPoiRemappingCalculator
    {
        private const double SingularTolerance = 1e-10;

        public static List<LocalPoiRemappedPoint> Remap(PoiParam template, LocalPoiMappingPoint[] layout, string? prefixName)
        {
            ArgumentNullException.ThrowIfNull(template);
            ArgumentNullException.ThrowIfNull(layout);
            if (layout.Length != 4) throw new InvalidOperationException($"关注点重映射需要 4 个光区角点，当前为 {layout.Length} 个。");
            if (template.PoiPoints.Count == 0) throw new InvalidOperationException($"POI 模板没有关注点：{template.Name}");

            LocalPoiMappingPoint[] reference = GetReferencePoints(template);
            double[] homography = SolveHomography(reference, layout);
            double factor = GetTopEdgeScale(reference, layout);
            List<LocalPoiRemappedPoint> result = new(template.PoiPoints.Count);
            foreach (PoiPoint point in template.PoiPoints)
            {
                LocalPoiMappingPoint mapped = Transform(homography, point.PixX, point.PixY);
                result.Add(new LocalPoiRemappedPoint
                {
                    PoiId = point.Id,
                    Name = string.Concat(prefixName ?? string.Empty, point.Name ?? point.Id.ToString()),
                    PointType = ToPoiPointType(point.PointType),
                    X = ConvertCoordinate(mapped.X, point.Name),
                    Y = ConvertCoordinate(mapped.Y, point.Name),
                    Width = ScaleSize(point.PixWidth, factor, point.Name),
                    Height = ScaleSize(point.PixHeight, factor, point.Name)
                });
            }
            return result;
        }

        public static void SaveDetails(int masterId, IReadOnlyCollection<LocalPoiRemappedPoint> points)
        {
            if (masterId <= 0) throw new ArgumentOutOfRangeException(nameof(masterId), "关注点布点结果主表 ID 无效。");
            ArgumentNullException.ThrowIfNull(points);
            List<PoiPointResultModel> details = new(points.Count);
            foreach (LocalPoiRemappedPoint point in points)
            {
                details.Add(new PoiPointResultModel
                {
                    Pid = masterId,
                    PoiId = point.PoiId,
                    PoiName = point.Name,
                    PoiType = point.PointType,
                    PoiX = point.X,
                    PoiY = point.Y,
                    PoiWidth = point.Width,
                    PoiHeight = point.Height,
                    Value = null
                });
            }
            int inserted = PoiPointResultDao.Instance.BulkInsert(details);
            if (inserted != details.Count) throw new InvalidOperationException($"保存关注点布点明细失败：应写入 {details.Count} 条，实际写入 {inserted} 条。");
        }

        public static void DeleteDetails(int masterId)
        {
            if (masterId > 0) _ = PoiPointResultDao.Instance.Delete(item => item.Pid == masterId);
        }

        private static LocalPoiMappingPoint[] GetReferencePoints(PoiParam template)
        {
            if (!template.LeftTopX.HasValue || !template.LeftTopY.HasValue
                || !template.RightTopX.HasValue || !template.RightTopY.HasValue
                || !template.RightBottomX.HasValue || !template.RightBottomY.HasValue
                || !template.LeftBottomX.HasValue || !template.LeftBottomY.HasValue)
            {
                throw new InvalidOperationException($"POI 模板没有完整的画布四角参考点：{template.Name}");
            }
            return new[]
            {
                new LocalPoiMappingPoint(template.LeftTopX.Value, template.LeftTopY.Value),
                new LocalPoiMappingPoint(template.RightTopX.Value, template.RightTopY.Value),
                new LocalPoiMappingPoint(template.RightBottomX.Value, template.RightBottomY.Value),
                new LocalPoiMappingPoint(template.LeftBottomX.Value, template.LeftBottomY.Value)
            };
        }

        private static double[] SolveHomography(LocalPoiMappingPoint[] source, LocalPoiMappingPoint[] destination)
        {
            double[,] matrix = new double[8, 9];
            for (int index = 0; index < 4; index++)
            {
                double x = source[index].X;
                double y = source[index].Y;
                double u = destination[index].X;
                double v = destination[index].Y;
                int row = index * 2;

                matrix[row, 0] = x;
                matrix[row, 1] = y;
                matrix[row, 2] = 1;
                matrix[row, 6] = -u * x;
                matrix[row, 7] = -u * y;
                matrix[row, 8] = u;

                matrix[row + 1, 3] = x;
                matrix[row + 1, 4] = y;
                matrix[row + 1, 5] = 1;
                matrix[row + 1, 6] = -v * x;
                matrix[row + 1, 7] = -v * y;
                matrix[row + 1, 8] = v;
            }

            for (int column = 0; column < 8; column++)
            {
                int pivotRow = column;
                double pivotValue = Math.Abs(matrix[pivotRow, column]);
                for (int row = column + 1; row < 8; row++)
                {
                    double candidate = Math.Abs(matrix[row, column]);
                    if (candidate <= pivotValue) continue;
                    pivotRow = row;
                    pivotValue = candidate;
                }
                if (pivotValue < SingularTolerance) throw new InvalidOperationException("POI 模板或光区四角参考点退化，无法计算透视映射。");

                if (pivotRow != column)
                {
                    for (int item = column; item < 9; item++)
                    {
                        (matrix[column, item], matrix[pivotRow, item]) = (matrix[pivotRow, item], matrix[column, item]);
                    }
                }

                double divisor = matrix[column, column];
                for (int item = column; item < 9; item++) matrix[column, item] /= divisor;
                for (int row = 0; row < 8; row++)
                {
                    if (row == column) continue;
                    double multiplier = matrix[row, column];
                    if (Math.Abs(multiplier) < SingularTolerance) continue;
                    for (int item = column; item < 9; item++) matrix[row, item] -= multiplier * matrix[column, item];
                }
            }

            double[] homography = new double[8];
            for (int index = 0; index < homography.Length; index++) homography[index] = matrix[index, 8];
            return homography;
        }

        private static LocalPoiMappingPoint Transform(double[] homography, double x, double y)
        {
            double denominator = homography[6] * x + homography[7] * y + 1;
            if (Math.Abs(denominator) < SingularTolerance) throw new InvalidOperationException("POI 点位于透视映射的无效区域。");
            return new LocalPoiMappingPoint(
                (homography[0] * x + homography[1] * y + homography[2]) / denominator,
                (homography[3] * x + homography[4] * y + homography[5]) / denominator);
        }

        private static double GetTopEdgeScale(LocalPoiMappingPoint[] source, LocalPoiMappingPoint[] destination)
        {
            double sourceDistance = Distance(source[0], source[1]);
            if (sourceDistance < SingularTolerance) throw new InvalidOperationException("POI 模板的上边参考点重合，无法缩放关注点尺寸。");
            double scale = Distance(destination[0], destination[1]) / sourceDistance;
            if (!double.IsFinite(scale)) throw new InvalidOperationException("关注点尺寸缩放比例无效。");
            return scale;
        }

        private static double Distance(LocalPoiMappingPoint first, LocalPoiMappingPoint second)
        {
            double x = first.X - second.X;
            double y = first.Y - second.Y;
            return Math.Sqrt(x * x + y * y);
        }

        private static int ConvertCoordinate(double value, string? pointName)
        {
            if (!double.IsFinite(value) || value < int.MinValue || value > int.MaxValue)
            {
                throw new InvalidOperationException($"关注点映射坐标超出整数范围：{pointName}");
            }
            return checked((int)value);
        }

        private static int ScaleSize(double value, double factor, string? pointName)
        {
            double scaled = value * factor;
            if (!double.IsFinite(scaled) || scaled < 0 || scaled > int.MaxValue)
            {
                throw new InvalidOperationException($"关注点映射尺寸无效：{pointName}");
            }
            return checked((int)scaled);
        }

        private static POIPointTypes ToPoiPointType(PoiShape type)
        {
            return type switch
            {
                PoiShape.Point or PoiShape.LegacySolidPoint => POIPointTypes.SolidPoint,
                PoiShape.Circle => POIPointTypes.Circle,
                PoiShape.Rect => POIPointTypes.Rect,
                PoiShape.LeftTopRect or PoiShape.Quadrilateral => POIPointTypes.LTRect,
                PoiShape.Polygon => POIPointTypes.Polygon,
                _ => throw new NotSupportedException($"本地关注点重映射暂不支持形状：{type}")
            };
        }
    }
}
