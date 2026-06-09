#pragma warning disable CA1861
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Cie
{
    public sealed class CieOverlayVisual : DrawingVisual
    {
        private static readonly double[] GuideDashArray = { 4.0, 3.0 };

        public void Render(
            CieDiagramProfile profile,
            Size canvasSize,
            Size bitmapPixelSize,
            double layoutScale,
            IReadOnlyList<CieGamut> gamuts,
            IReadOnlyList<CieMarker> markers,
            bool showCctReference,
            bool showDaylightReference,
            CieMarker? selectedMarker)
        {
            using DrawingContext dc = RenderOpen();

            if (canvasSize.Width <= 0 || canvasSize.Height <= 0 || bitmapPixelSize.Width <= 0 || bitmapPixelSize.Height <= 0)
            {
                return;
            }

            double scale = CoerceScale(layoutScale);
            double pixelsPerDip = VisualTreeHelper.GetDpi(this).PixelsPerDip;

            DrawGamuts(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip, gamuts);
            if (showCctReference)
            {
                DrawCctReference(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip);
            }
            if (showDaylightReference)
            {
                DrawDaylightReference(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip);
            }
            DrawMarkers(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip, markers, false);

            if (selectedMarker != null)
            {
                DrawSelection(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip, selectedMarker);
            }
        }

        private static void DrawGamuts(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip, IReadOnlyList<CieGamut> gamuts)
        {
            foreach (CieGamut gamut in gamuts)
            {
                List<Point> points = gamut.Vertices
                    .Select(vertex => ToCanvasPoint(profile, canvasSize, bitmapPixelSize, vertex))
                    .Where(IsFinite)
                    .ToList();

                if (points.Count < 2)
                {
                    continue;
                }

                StreamGeometry geometry = new();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(points[0], gamut.Fill != null, true);
                    context.PolyLineTo(points.Skip(1).ToList(), true, true);
                }
                geometry.Freeze();

                Pen pen = new(gamut.Stroke, 1.8 * scale)
                {
                    LineJoin = PenLineJoin.Round
                };
                dc.DrawGeometry(gamut.Fill, pen, geometry);

                Point labelPoint = GetCentroid(points);
                DrawText(dc, gamut.Name, labelPoint + new Vector(6 * scale, -18 * scale), gamut.Stroke, 12 * scale, scale, pixelsPerDip);
            }
        }

        private static void DrawMarkers(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip, IReadOnlyList<CieMarker> markers, bool emphasize)
        {
            foreach (CieMarker marker in markers)
            {
                DrawMarker(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip, marker, emphasize);
            }
        }

        private static void DrawCctReference(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip)
        {
            int[] temperatures = { 1500, 2000, 2500, 3000, 4000, 6000, 10000 };
            List<Point> locus = new();
            for (int temperature = 1500; temperature <= 25000; temperature += 100)
            {
                CieChromaticity xy = CieColorConverter.CctToApproximatePlanckianXy(temperature);
                Point point = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, xy);
                if (IsFinite(point))
                {
                    locus.Add(point);
                }
            }

            if (locus.Count > 1)
            {
                StreamGeometry geometry = new();
                using (StreamGeometryContext context = geometry.Open())
                {
                    context.BeginFigure(locus[0], false, false);
                    context.PolyLineTo(locus.Skip(1).ToList(), true, true);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, new Pen(Brushes.Black, 1.3 * scale), geometry);
            }

            DrawText(dc, "Tc(K)", ToCanvasPoint(profile, canvasSize, bitmapPixelSize, new CieChromaticity(0.285, 0.430)), Brushes.Black, 13 * scale, scale, pixelsPerDip);

            foreach (int temperature in temperatures)
            {
                DrawCctTick(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip, temperature);
            }
        }

        private static void DrawDaylightReference(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip)
        {
            List<Point> locus = new();
            for (int temperature = 4000; temperature <= 25000; temperature += 250)
            {
                Point point = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, CieColorConverter.DaylightCctToXy(temperature));
                if (IsFinite(point))
                {
                    locus.Add(point);
                }
            }

            if (locus.Count <= 1)
            {
                return;
            }

            StreamGeometry geometry = new();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(locus[0], false, false);
                context.PolyLineTo(locus.Skip(1).ToList(), true, true);
            }
            geometry.Freeze();

            Pen pen = new(new SolidColorBrush(Color.FromRgb(32, 86, 180)), 1.2 * scale)
            {
                DashStyle = new DashStyle(new[] { 6.0, 3.0 }, 0)
            };
            dc.DrawGeometry(null, pen, geometry);

            Point labelPoint = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, CieColorConverter.DaylightCctToXy(6500));
            if (IsFinite(labelPoint))
            {
                DrawText(dc, "D locus", labelPoint + new Vector(10 * scale, 10 * scale), pen.Brush, 11 * scale, scale, pixelsPerDip);
            }
        }

        private static void DrawCctTick(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip, int temperature)
        {
            CieChromaticity centerXy = CieColorConverter.CctToApproximatePlanckianXy(temperature);
            CieChromaticity beforeXy = CieColorConverter.CctToApproximatePlanckianXy(Math.Max(1500, temperature - 100));
            CieChromaticity afterXy = CieColorConverter.CctToApproximatePlanckianXy(Math.Min(25000, temperature + 100));

            Point center = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, centerXy);
            Point before = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, beforeXy);
            Point after = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, afterXy);
            if (!IsFinite(center) || !IsFinite(before) || !IsFinite(after))
            {
                return;
            }

            Vector tangent = after - before;
            if (tangent.Length <= 0)
            {
                return;
            }

            tangent.Normalize();
            Vector normal = new(-tangent.Y, tangent.X);
            double halfLength = 25 * scale;
            Point p1 = center - normal * halfLength;
            Point p2 = center + normal * halfLength;
            dc.DrawLine(new Pen(Brushes.Black, 1.2 * scale), p1, p2);

            Vector labelOffset = temperature <= 2000 ? new Vector(12 * scale, -4 * scale) : normal * (halfLength + 6 * scale);
            DrawText(dc, temperature.ToString(CultureInfo.InvariantCulture), center + labelOffset, Brushes.Black, 11 * scale, scale, pixelsPerDip);
        }

        private static void DrawSelection(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip, CieMarker marker)
        {
            Point point = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, marker.Chromaticity);
            if (!IsFinite(point))
            {
                return;
            }

            Rect plotRect = ToCanvasRect(profile.PlotAreaPixels, canvasSize, bitmapPixelSize);
            Pen guideUnderlay = new(Brushes.White, 1.8 * scale)
            {
                DashStyle = new DashStyle(GuideDashArray, 0)
            };
            Pen guidePen = new(Brushes.Black, 1.0 * scale)
            {
                DashStyle = new DashStyle(GuideDashArray, 0)
            };

            dc.DrawLine(guideUnderlay, new Point(plotRect.Left, point.Y), new Point(plotRect.Right, point.Y));
            dc.DrawLine(guideUnderlay, new Point(point.X, plotRect.Top), new Point(point.X, plotRect.Bottom));
            dc.DrawLine(guidePen, new Point(plotRect.Left, point.Y), new Point(plotRect.Right, point.Y));
            dc.DrawLine(guidePen, new Point(point.X, plotRect.Top), new Point(point.X, plotRect.Bottom));

            DrawMarker(dc, profile, canvasSize, bitmapPixelSize, scale, pixelsPerDip, marker, true);
        }

        private static void DrawMarker(DrawingContext dc, CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, double scale, double pixelsPerDip, CieMarker marker, bool emphasize)
        {
            Point point = ToCanvasPoint(profile, canvasSize, bitmapPixelSize, marker.Chromaticity);
            if (!IsFinite(point))
            {
                return;
            }

            double radius = emphasize ? 6 * scale : 4.5 * scale;
            SolidColorBrush fill = new(marker.Color);
            Pen whitePen = new(Brushes.White, 2.2 * scale);
            Pen blackPen = new(Brushes.Black, 1.1 * scale);

            dc.DrawEllipse(fill, whitePen, point, radius, radius);
            dc.DrawEllipse(null, blackPen, point, radius, radius);

            if (!string.IsNullOrWhiteSpace(marker.Name))
            {
                DrawText(dc, marker.Name, point + new Vector(radius + 4 * scale, -radius - 8 * scale), Brushes.Black, 12 * scale, scale, pixelsPerDip);
            }
        }

        private static void DrawText(DrawingContext dc, string text, Point point, Brush brush, double fontSize, double scale, double pixelsPerDip)
        {
            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                Math.Max(8 * scale, fontSize),
                brush,
                pixelsPerDip);

            Rect background = new(point, new Size(formattedText.Width + 6 * scale, formattedText.Height + 2 * scale));
            dc.DrawRoundedRectangle(new SolidColorBrush(Color.FromArgb(185, 255, 255, 255)), null, background, 2 * scale, 2 * scale);
            dc.DrawText(formattedText, point + new Vector(3 * scale, 1 * scale));
        }

        private static Point ToCanvasPoint(CieDiagramProfile profile, Size canvasSize, Size bitmapPixelSize, CieChromaticity xy)
        {
            Point imagePixel = profile.ToImagePixel(xy);
            if (!IsFinite(imagePixel))
            {
                return imagePixel;
            }

            return new Point(
                imagePixel.X / bitmapPixelSize.Width * canvasSize.Width,
                imagePixel.Y / bitmapPixelSize.Height * canvasSize.Height);
        }

        private static Rect ToCanvasRect(Rect imagePixelRect, Size canvasSize, Size bitmapPixelSize)
        {
            Point topLeft = new(
                imagePixelRect.Left / bitmapPixelSize.Width * canvasSize.Width,
                imagePixelRect.Top / bitmapPixelSize.Height * canvasSize.Height);
            Point bottomRight = new(
                imagePixelRect.Right / bitmapPixelSize.Width * canvasSize.Width,
                imagePixelRect.Bottom / bitmapPixelSize.Height * canvasSize.Height);

            return new Rect(topLeft, bottomRight);
        }

        private static Point GetCentroid(List<Point> points)
        {
            if (points.Count == 0)
            {
                return new Point();
            }

            return new Point(points.Average(point => point.X), points.Average(point => point.Y));
        }

        private static bool IsFinite(Point point)
        {
            return !double.IsNaN(point.X) &&
                   !double.IsNaN(point.Y) &&
                   !double.IsInfinity(point.X) &&
                   !double.IsInfinity(point.Y);
        }

        private static double CoerceScale(double scale)
        {
            return double.IsNaN(scale) || double.IsInfinity(scale) || scale <= 0 ? 1 : scale;
        }
    }
}
