using ColorVision.Engine.Templates.POI.BuildPoi;
using ColorVision.ImageEditor;
using CVCommCore.CVAlgorithm;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Engine.Templates.POI
{
    internal static class LocalPoiLayoutCalculator
    {
        public static List<LocalPoiRemappedPoint> Build(ParamBuildPoi parameter, PoiParam layoutTemplate)
        {
            ArgumentNullException.ThrowIfNull(parameter);
            ArgumentNullException.ThrowIfNull(layoutTemplate);
            return parameter.POILayout switch
            {
                POILayoutTypes.Circle => BuildCircle(parameter, GetSingleLayoutPoint(layoutTemplate, GraphicTypes.Circle)),
                POILayoutTypes.Rect => BuildRectangle(parameter, GetRectangleLayoutPoint(layoutTemplate)),
                POILayoutTypes.PolygonFour => BuildPolygon(parameter, GetPolygon(layoutTemplate)),
                _ => throw new NotSupportedException($"本地参数布点不支持布局类型：{parameter.POILayout}")
            };
        }

        private static List<LocalPoiRemappedPoint> BuildCircle(ParamBuildPoi parameter, PoiPoint layout)
        {
            if (parameter.LayoutCircleNum <= 0) throw new InvalidOperationException("参数模板的圆形布点数量必须大于 0。");
            double centerX = Convert.ToInt32(layout.PixX);
            double centerY = Convert.ToInt32(layout.PixY);
            double radius = Convert.ToInt32(layout.PixWidth) / 2d;
            List<LocalPoiRemappedPoint> points = new(parameter.LayoutCircleNum);
            for (int index = 0; index < parameter.LayoutCircleNum; index++)
            {
                double angle = index * 2 * Math.PI / parameter.LayoutCircleNum + Math.PI / 180 * parameter.LayoutCircleAngle;
                double radiusX = radius;
                double radiusY = radius;
                if (parameter.PointPosition == DrawingGraphicPosition.Internal)
                {
                    radiusX -= Convert.ToInt32(parameter.PointWidth) / 2;
                    radiusY -= Convert.ToInt32(parameter.PointHeight) / 2;
                }
                else if (parameter.PointPosition == DrawingGraphicPosition.External)
                {
                    radiusX += Convert.ToInt32(parameter.PointWidth) / 2;
                    radiusY += Convert.ToInt32(parameter.PointHeight) / 2;
                }
                points.Add(CreatePoint(parameter, index + 1, centerX + radiusX * Math.Cos(angle), centerY + radiusY * Math.Sin(angle)));
            }
            return points;
        }

        private static List<LocalPoiRemappedPoint> BuildRectangle(ParamBuildPoi parameter, PoiPoint layout)
        {
            ValidateGrid(parameter);
            double width = Convert.ToInt32(layout.PixWidth);
            double height = Convert.ToInt32(layout.PixHeight);
            double top = Convert.ToInt32(layout.PixY) - height / 2;
            double bottom = Convert.ToInt32(layout.PixY) + height / 2;
            double left = Convert.ToInt32(layout.PixX) - width / 2;
            double right = Convert.ToInt32(layout.PixX) + width / 2;
            ApplyPointPosition(parameter, ref top, ref bottom, ref left, ref right);
            ApplyRectangleMargins(parameter, width, height, ref top, ref bottom, ref left, ref right);

            double rowStep = (bottom - top) / (parameter.LayoutRows - 1);
            double columnStep = (right - left) / (parameter.LayoutCols - 1);
            List<LocalPoiRemappedPoint> points = new(parameter.LayoutRows * parameter.LayoutCols);
            int id = 1;
            for (int row = 0; row < parameter.LayoutRows; row++)
            {
                for (int column = 0; column < parameter.LayoutCols; column++)
                {
                    points.Add(CreatePoint(parameter, id++, left + columnStep * column, top + rowStep * row));
                }
            }
            return points;
        }

        private static List<LocalPoiRemappedPoint> BuildPolygon(ParamBuildPoi parameter, LocalPoiMappingPoint[] polygon)
        {
            ValidateGrid(parameter);
            ApplyPolygonMargins(parameter, polygon);
            LocalPoiMappingPoint[] points = SortPolygon(polygon);
            double rowStep = 1d / (parameter.LayoutRows - 1);
            double columnStep = 1d / (parameter.LayoutCols - 1);
            List<LocalPoiRemappedPoint> result = new(parameter.LayoutRows * parameter.LayoutCols);
            int id = 1;
            for (int row = 0; row < parameter.LayoutRows; row++)
            {
                for (int column = 0; column < parameter.LayoutCols; column++)
                {
                    double rowRatio = row * rowStep;
                    double columnRatio = column * columnStep;
                    double x = (1 - rowRatio) * (1 - columnRatio) * points[0].X
                        + (1 - rowRatio) * columnRatio * points[1].X
                        + rowRatio * (1 - columnRatio) * points[3].X
                        + rowRatio * columnRatio * points[2].X;
                    double y = (1 - rowRatio) * (1 - columnRatio) * points[0].Y
                        + (1 - rowRatio) * columnRatio * points[1].Y
                        + rowRatio * (1 - columnRatio) * points[3].Y
                        + rowRatio * columnRatio * points[2].Y;
                    result.Add(CreatePoint(parameter, id++, x, y));
                }
            }
            return result;
        }

        private static LocalPoiRemappedPoint CreatePoint(ParamBuildPoi parameter, int id, double x, double y)
        {
            return new LocalPoiRemappedPoint
            {
                Name = $"POI_{id}",
                PointType = parameter.PointType,
                X = Convert.ToInt32(Convert.ToSingle(x)),
                Y = Convert.ToInt32(Convert.ToSingle(y)),
                Width = Convert.ToInt32(parameter.PointWidth),
                Height = Convert.ToInt32(parameter.PointHeight)
            };
        }

        private static PoiPoint GetSingleLayoutPoint(PoiParam template, GraphicTypes expectedType)
        {
            PoiPoint point = template.PoiPoints.Count == 1
                ? template.PoiPoints[0]
                : throw new InvalidOperationException($"布点 ROI 必须包含 1 个{expectedType}区域：{template.Name}。");
            if (point.PointType != expectedType)
            {
                throw new InvalidOperationException($"布点 ROI 类型与参数模板不一致：参数为 {expectedType}，区域为 {point.PointType}。");
            }
            return point;
        }

        private static PoiPoint GetRectangleLayoutPoint(PoiParam template)
        {
            PoiPoint point = template.PoiPoints.Count == 1
                ? template.PoiPoints[0]
                : throw new InvalidOperationException($"布点 ROI 必须包含 1 个矩形区域：{template.Name}。");
            if (point.PointType is not GraphicTypes.Rect and not GraphicTypes.Quadrilateral)
            {
                throw new InvalidOperationException($"布点 ROI 类型与参数模板不一致：参数为 Rect，区域为 {point.PointType}。");
            }
            return point;
        }

        private static LocalPoiMappingPoint[] GetPolygon(PoiParam template)
        {
            if (template.PoiPoints.Count != 4)
            {
                throw new InvalidOperationException($"布点 ROI 必须包含 4 个角点：{template.Name}，当前为 {template.PoiPoints.Count} 个。");
            }
            return template.PoiPoints
                .Select(point => new LocalPoiMappingPoint(Convert.ToInt32(point.PixX), Convert.ToInt32(point.PixY)))
                .ToArray();
        }

        private static void ValidateGrid(ParamBuildPoi parameter)
        {
            if (parameter.LayoutRows < 2 || parameter.LayoutCols < 2)
            {
                throw new InvalidOperationException("参数模板的布点行数和列数必须至少为 2。");
            }
        }

        private static void ApplyPointPosition(
            ParamBuildPoi parameter,
            ref double top,
            ref double bottom,
            ref double left,
            ref double right)
        {
            int halfWidth = Convert.ToInt32(parameter.PointWidth) / 2;
            int halfHeight = Convert.ToInt32(parameter.PointHeight) / 2;
            if (parameter.PointPosition == DrawingGraphicPosition.Internal)
            {
                top += halfWidth;
                bottom += halfWidth;
                left += halfHeight;
                right += halfHeight;
            }
            else if (parameter.PointPosition == DrawingGraphicPosition.External)
            {
                top -= halfWidth;
                bottom -= halfWidth;
                left -= halfHeight;
                right -= halfHeight;
            }
        }

        private static void ApplyRectangleMargins(
            ParamBuildPoi parameter,
            double width,
            double height,
            ref double top,
            ref double bottom,
            ref double left,
            ref double right)
        {
            if (parameter.MarginType == GraphicBorderType.Absolute)
            {
                left += parameter.MarginLeft;
                top += parameter.MarginTop;
                right -= parameter.MarginRight;
                bottom -= parameter.MarginBottom;
            }
            else
            {
                left += width * parameter.MarginLeft / 100;
                top += height * parameter.MarginTop / 100;
                right -= width * parameter.MarginRight / 100;
                bottom -= height * parameter.MarginBottom / 100;
            }
        }

        private static void ApplyPolygonMargins(ParamBuildPoi parameter, LocalPoiMappingPoint[] polygon)
        {
            double x1 = polygon[0].X;
            double y1 = polygon[0].Y;
            double x2 = polygon[1].X;
            double y2 = polygon[1].Y;
            double x3 = polygon[2].X;
            double y3 = polygon[2].Y;
            double x4 = polygon[3].X;
            double y4 = polygon[3].Y;
            if (parameter.MarginType == GraphicBorderType.Absolute)
            {
                x1 += parameter.MarginLeft;
                y1 += parameter.MarginTop;
                x2 -= parameter.MarginRight;
                y2 += parameter.MarginTop;
                x3 -= parameter.MarginRight;
                y3 -= parameter.MarginBottom;
                x4 += parameter.MarginLeft;
                y4 -= parameter.MarginBottom;
            }
            else
            {
                int[] edgeLengths =
                {
                    EdgeLength(polygon[0], polygon[1]),
                    EdgeLength(polygon[1], polygon[2]),
                    EdgeLength(polygon[2], polygon[3]),
                    EdgeLength(polygon[3], polygon[0])
                };
                x1 += edgeLengths[0] * parameter.MarginLeft / 100;
                y1 += edgeLengths[3] * parameter.MarginTop / 100;
                x2 -= edgeLengths[0] * parameter.MarginRight / 100;
                y2 += edgeLengths[1] * parameter.MarginTop / 100;
                x3 -= edgeLengths[2] * parameter.MarginRight / 100;
                y3 -= edgeLengths[1] * parameter.MarginBottom / 100;
                x4 += edgeLengths[2] * parameter.MarginLeft / 100;
                y4 -= edgeLengths[3] * parameter.MarginBottom / 100;
            }
            polygon[0] = new LocalPoiMappingPoint(x1, y1);
            polygon[1] = new LocalPoiMappingPoint(x2, y2);
            polygon[2] = new LocalPoiMappingPoint(x3, y3);
            polygon[3] = new LocalPoiMappingPoint(x4, y4);
        }

        private static int EdgeLength(LocalPoiMappingPoint first, LocalPoiMappingPoint second)
        {
            double x = Math.Abs(first.X - second.X);
            double y = Math.Abs(first.Y - second.Y);
            return (int)Math.Sqrt(x * x + y * y);
        }

        private static LocalPoiMappingPoint[] SortPolygon(LocalPoiMappingPoint[] polygon)
        {
            LocalPoiMappingPoint[] points = polygon.ToArray();
            double centerX = points.Average(point => point.X);
            double centerY = points.Average(point => point.Y);
            for (int index = 0; index < points.Length - 1; index++)
            {
                for (int item = 0; item < points.Length - index - 1; item++)
                {
                    double determinant = (points[item].X - centerX) * (points[item + 1].Y - centerY)
                        - (points[item + 1].X - centerX) * (points[item].Y - centerY);
                    if (determinant < 0)
                    {
                        (points[item], points[item + 1]) = (points[item + 1], points[item]);
                    }
                }
            }
            return points;
        }
    }
}
