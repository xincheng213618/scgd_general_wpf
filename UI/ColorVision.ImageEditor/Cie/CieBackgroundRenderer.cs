#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Cie
{
    public static class CieBackgroundRenderer
    {
        private static readonly int[] WavelengthLabels = { 380, 420, 460, 470, 480, 490, 500, 520, 540, 560, 580, 600, 620, 700 };

        public static BitmapSource Render(CieDiagramProfile profile)
        {
            int width = Math.Max(1, (int)Math.Round(profile.BackgroundPixelSize.Width));
            int height = Math.Max(1, (int)Math.Round(profile.BackgroundPixelSize.Height));
            BitmapSource colorField = RenderColorField(profile, width, height);

            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(Brushes.White, null, new Rect(0, 0, width, height));
                dc.DrawImage(colorField, new Rect(0, 0, width, height));
                DrawGrid(dc, profile);
                DrawSpectrumBoundary(dc, profile);
                DrawWavelengthLabels(dc, profile);
                DrawAxisLabels(dc, profile);
            }

            RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource RenderColorField(CieDiagramProfile profile, int width, int height)
        {
            WriteableBitmap bitmap = new(width, height, 96, 96, PixelFormats.Bgra32, null);
            int stride = width * 4;
            byte[] pixels = new byte[stride * height];
            List<CieChromaticity> locus = GetDiagramLocus(profile);

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int offset = y * stride + x * 4;
                    Color color = Colors.White;

                    if (profile.PlotAreaPixels.Contains(new Point(x, y)))
                    {
                        CieChromaticity diagramPoint = ImagePixelToDiagramPoint(profile, new Point(x, y));
                        if (profile.ContainsDiagramPoint(diagramPoint) && IsPointInPolygon(diagramPoint, locus))
                        {
                            CieChromaticity xy = profile.Kind == CieDiagramKind.Cie1976uv
                                ? CieColorConverter.Uv1976ToXy(diagramPoint)
                                : profile.Kind == CieDiagramKind.Cie1960uv
                                ? CieColorConverter.Uv1960ToXy(diagramPoint)
                                : diagramPoint;
                            color = XyToDisplayColor(xy);
                        }
                        else
                        {
                            color = Color.FromRgb(248, 248, 248);
                        }
                    }

                    pixels[offset] = color.B;
                    pixels[offset + 1] = color.G;
                    pixels[offset + 2] = color.R;
                    pixels[offset + 3] = 255;
                }
            }

            bitmap.WritePixels(new Int32Rect(0, 0, width, height), pixels, stride, 0);
            bitmap.Freeze();
            return bitmap;
        }

        private static void DrawGrid(DrawingContext dc, CieDiagramProfile profile)
        {
            Rect plot = profile.PlotAreaPixels;
            Pen majorPen = new(new SolidColorBrush(Color.FromRgb(185, 185, 185)), 1);
            Pen axisPen = new(Brushes.Black, 1.2);

            double xStep = profile.AxisBounds.Width <= 0.65 ? 0.1 : 0.1;
            double yStep = profile.AxisBounds.Height <= 0.65 ? 0.1 : 0.1;

            for (double x = profile.AxisBounds.Left; x <= profile.AxisBounds.Right + 0.0001; x += xStep)
            {
                Point top = profile.DiagramPointToImagePixel(new CieChromaticity(x, profile.AxisBounds.Bottom));
                Point bottom = profile.DiagramPointToImagePixel(new CieChromaticity(x, profile.AxisBounds.Top));
                dc.DrawLine(Math.Abs(x) < 0.0001 ? axisPen : majorPen, top, bottom);
                DrawText(dc, x.ToString("0.0", CultureInfo.InvariantCulture), new Point(bottom.X - 10, plot.Bottom + 8), Brushes.Black, 14);
            }

            for (double y = profile.AxisBounds.Top; y <= profile.AxisBounds.Bottom + 0.0001; y += yStep)
            {
                Point left = profile.DiagramPointToImagePixel(new CieChromaticity(profile.AxisBounds.Left, y));
                Point right = profile.DiagramPointToImagePixel(new CieChromaticity(profile.AxisBounds.Right, y));
                dc.DrawLine(Math.Abs(y) < 0.0001 ? axisPen : majorPen, left, right);
                DrawText(dc, y.ToString("0.0", CultureInfo.InvariantCulture), new Point(plot.Left - 42, left.Y - 9), Brushes.Black, 14);
            }

            dc.DrawRectangle(null, axisPen, plot);
        }

        private static void DrawSpectrumBoundary(DrawingContext dc, CieDiagramProfile profile)
        {
            List<Point> points = CieSpectrumLocus.Points
                .Select(point => profile.ToImagePixel(point.Chromaticity))
                .Where(IsFinite)
                .ToList();

            if (points.Count < 2)
            {
                return;
            }

            StreamGeometry geometry = new();
            using (StreamGeometryContext context = geometry.Open())
            {
                context.BeginFigure(points[0], false, false);
                context.PolyLineTo(points.Skip(1).ToList(), true, true);
                context.LineTo(points[0], true, true);
            }
            geometry.Freeze();

            dc.DrawGeometry(null, new Pen(Brushes.Black, 1.6), geometry);
        }

        private static void DrawWavelengthLabels(DrawingContext dc, CieDiagramProfile profile)
        {
            foreach (int wavelength in WavelengthLabels)
            {
                CieSpectrumPoint? spectrumPoint = CieSpectrumLocus.FindNearest(wavelength);
                if (spectrumPoint == null)
                {
                    continue;
                }

                Point point = profile.ToImagePixel(spectrumPoint.Value.Chromaticity);
                if (!IsFinite(point))
                {
                    continue;
                }

                Vector offset = GetWavelengthLabelOffset(profile, wavelength);
                DrawText(dc, wavelength.ToString(CultureInfo.InvariantCulture), point + offset, Brushes.Blue, 16);
            }
        }

        private static Vector GetWavelengthLabelOffset(CieDiagramProfile profile, int wavelength)
        {
            if (profile.Kind == CieDiagramKind.Cie1976uv)
            {
                return wavelength < 500 ? new Vector(-34, 6) : new Vector(8, -20);
            }

            if (wavelength < 500)
            {
                return new Vector(-42, -2);
            }

            return wavelength > 620 ? new Vector(8, -4) : new Vector(8, -20);
        }

        private static void DrawAxisLabels(DrawingContext dc, CieDiagramProfile profile)
        {
            Rect plot = profile.PlotAreaPixels;
            string xLabel = profile.Kind == CieDiagramKind.Cie1976uv ? "u'" : "x";
            string yLabel = profile.Kind == CieDiagramKind.Cie1976uv ? "v'" : "y";
            if (profile.Kind == CieDiagramKind.Cie1960uv)
            {
                xLabel = "u";
                yLabel = "v";
            }

            DrawText(dc, profile.Name, new Point(plot.Left, 6), Brushes.Black, 18);
            DrawText(dc, xLabel, new Point(plot.Right - 8, plot.Bottom + 34), Brushes.Black, 18);
            DrawText(dc, yLabel, new Point(plot.Left - 34, plot.Top - 4), Brushes.Black, 18);
        }

        private static CieChromaticity ImagePixelToDiagramPoint(CieDiagramProfile profile, Point pixel)
        {
            Rect plot = profile.PlotAreaPixels;
            double x = profile.AxisBounds.Left + (pixel.X - plot.Left) / plot.Width * profile.AxisBounds.Width;
            double y = profile.AxisBounds.Top + (plot.Bottom - pixel.Y) / plot.Height * profile.AxisBounds.Height;
            return new CieChromaticity(x, y);
        }

        private static List<CieChromaticity> GetDiagramLocus(CieDiagramProfile profile)
        {
            return CieSpectrumLocus.Points
                .Select(point => profile.ToDiagramPoint(point.Chromaticity))
                .Where(point => point.IsFinite)
                .ToList();
        }

        private static bool IsPointInPolygon(CieChromaticity point, IReadOnlyList<CieChromaticity> polygon)
        {
            bool inside = false;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                CieChromaticity pi = polygon[i];
                CieChromaticity pj = polygon[j];
                bool intersect = ((pi.Y > point.Y) != (pj.Y > point.Y)) &&
                                 (point.X < (pj.X - pi.X) * (point.Y - pi.Y) / (pj.Y - pi.Y) + pi.X);
                if (intersect)
                {
                    inside = !inside;
                }
            }

            return inside;
        }

        private static Color XyToDisplayColor(CieChromaticity xy)
        {
            if (!xy.IsFinite || xy.Y <= 0)
            {
                return Colors.White;
            }

            double x = xy.X / xy.Y;
            double y = 1;
            double z = (1 - xy.X - xy.Y) / xy.Y;

            double red = 3.2404542 * x - 1.5371385 * y - 0.4985314 * z;
            double green = -0.9692660 * x + 1.8760108 * y + 0.0415560 * z;
            double blue = 0.0556434 * x - 0.2040259 * y + 1.0572252 * z;

            double max = Math.Max(red, Math.Max(green, blue));
            if (max > 0)
            {
                red /= max;
                green /= max;
                blue /= max;
            }

            return Color.FromRgb(ToByte(red), ToByte(green), ToByte(blue));
        }

        private static byte ToByte(double linear)
        {
            double clamped = Math.Clamp(linear, 0, 1);
            double encoded = clamped <= 0.0031308
                ? 12.92 * clamped
                : 1.055 * Math.Pow(clamped, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
        }

        private static void DrawText(DrawingContext dc, string text, Point point, Brush brush, double fontSize)
        {
            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                brush,
                1);
            dc.DrawText(formattedText, point);
        }

        private static bool IsFinite(Point point)
        {
            return !double.IsNaN(point.X) &&
                   !double.IsNaN(point.Y) &&
                   !double.IsInfinity(point.X) &&
                   !double.IsInfinity(point.Y);
        }
    }
}
