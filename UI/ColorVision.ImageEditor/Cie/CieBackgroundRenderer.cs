#pragma warning disable CA1859
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Cie
{
    public static class CieBackgroundRenderer
    {
        private const int DisplayChannelBins = 4096;
        private static readonly int[] WavelengthLabels = { 380, 420, 460, 470, 480, 490, 500, 520, 540, 560, 580, 600, 620, 700 };

        public static BitmapSource Render(CieDiagramProfile profile, bool dark = false)
        {
            int width = Math.Max(1, (int)Math.Round(profile.BackgroundPixelSize.Width));
            int height = Math.Max(1, (int)Math.Round(profile.BackgroundPixelSize.Height));
            BitmapSource colorField = RenderColorField(profile, width, height, dark);

            DrawingVisual visual = new();
            using (DrawingContext dc = visual.RenderOpen())
            {
                dc.DrawRectangle(dark ? Brushes.Black : Brushes.White, null, new Rect(0, 0, width, height));
                dc.DrawImage(colorField, new Rect(0, 0, width, height));
                DrawGrid(dc, profile, dark);
                DrawSpectrumBoundary(dc, profile, dark);
                DrawWavelengthLabels(dc, profile, dark);
                DrawAxisLabels(dc, profile, dark);
            }

            RenderTargetBitmap bitmap = new(width, height, 96, 96, PixelFormats.Pbgra32);
            bitmap.Render(visual);
            bitmap.Freeze();
            return bitmap;
        }

        private static BitmapSource RenderColorField(CieDiagramProfile profile, int width, int height, bool dark)
        {
            int stride = width * 4;
            uint[] pixels = new uint[width * height];
            List<CieChromaticity> locus = GetDiagramLocus(profile);
            Rect plot = profile.PlotAreaPixels;
            Rect axes = profile.AxisBounds;
            uint background = dark ? 0xFF00_0000u : 0xFFFF_FFFFu;
            uint outsideSpectrum = dark ? background : 0xFFF8_F8F8u;

            int workers = Math.Min(4, Environment.ProcessorCount);
            if (workers > 1 && pixels.Length >= 256 * 1024)
            {
                Parallel.For(0, workers, new ParallelOptions { MaxDegreeOfParallelism = workers },
                    worker => FillRows((int)((long)height * worker / workers), (int)((long)height * (worker + 1) / workers)));
            }
            else
            {
                FillRows(0, height);
            }

            BitmapSource bitmap = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            bitmap.Freeze();
            return bitmap;

            void FillRows(int firstRow, int lastRow)
            {
                Span<double> intersections = stackalloc double[locus.Count];
                for (int y = firstRow; y < lastRow; y++)
                {
                    double diagramY = axes.Top + (plot.Bottom - y) / plot.Height * axes.Height;
                    int intersectionCount = GetRowIntersections(diagramY, locus, intersections);
                    ReadOnlySpan<double> rowIntersections = intersections[..intersectionCount];
                    for (int x = 0; x < width; x++)
                    {
                        uint pixel = background;

                        if (plot.Contains(new Point(x, y)))
                        {
                            double diagramX = axes.Left + (x - plot.Left) / plot.Width * axes.Width;
                            CieChromaticity diagramPoint = new(diagramX, diagramY);
                            if (profile.ContainsDiagramPoint(diagramPoint) && IsInsideSpectrum(diagramX, rowIntersections))
                            {
                                CieChromaticity xy = profile.FromDiagramPoint(diagramPoint);
                                pixel = XyToDisplayPixel(xy);
                            }
                            else
                            {
                                pixel = outsideSpectrum;
                            }
                        }

                        pixels[y * width + x] = pixel;
                    }
                }
            }
        }

        private static void DrawGrid(DrawingContext dc, CieDiagramProfile profile, bool dark)
        {
            Rect plot = profile.PlotAreaPixels;
            Pen majorPen = new(new SolidColorBrush(dark ? Color.FromRgb(85, 85, 85) : Color.FromRgb(185, 185, 185)), 1);
            Brush textBrush = dark ? Brushes.LightGray : Brushes.Black;
            Pen axisPen = new(textBrush, 1.2);

            double xStep = profile.AxisBounds.Width <= 0.65 ? 0.1 : 0.1;
            double yStep = profile.AxisBounds.Height <= 0.65 ? 0.1 : 0.1;

            for (double x = profile.AxisBounds.Left; x <= profile.AxisBounds.Right + 0.0001; x += xStep)
            {
                Point top = profile.DiagramPointToImagePixel(new CieChromaticity(x, profile.AxisBounds.Bottom));
                Point bottom = profile.DiagramPointToImagePixel(new CieChromaticity(x, profile.AxisBounds.Top));
                dc.DrawLine(Math.Abs(x) < 0.0001 ? axisPen : majorPen, top, bottom);
                DrawText(dc, x.ToString("0.0", CultureInfo.InvariantCulture), new Point(bottom.X - 10, plot.Bottom + 8), textBrush, 14);
            }

            for (double y = profile.AxisBounds.Top; y <= profile.AxisBounds.Bottom + 0.0001; y += yStep)
            {
                Point left = profile.DiagramPointToImagePixel(new CieChromaticity(profile.AxisBounds.Left, y));
                Point right = profile.DiagramPointToImagePixel(new CieChromaticity(profile.AxisBounds.Right, y));
                dc.DrawLine(Math.Abs(y) < 0.0001 ? axisPen : majorPen, left, right);
                DrawText(dc, y.ToString("0.0", CultureInfo.InvariantCulture), new Point(plot.Left - 42, left.Y - 9), textBrush, 14);
            }

            dc.DrawRectangle(null, axisPen, plot);
        }

        private static void DrawSpectrumBoundary(DrawingContext dc, CieDiagramProfile profile, bool dark)
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

            dc.DrawGeometry(null, new Pen(dark ? Brushes.LightGray : Brushes.Black, 1.6), geometry);
        }

        private static void DrawWavelengthLabels(DrawingContext dc, CieDiagramProfile profile, bool dark)
        {
            List<Rect> occupied = new();
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
                Rect labelBounds = new(point + offset, new Size(34, 20));
                labelBounds.Inflate(3, 3);
                if (occupied.Any(rect => rect.IntersectsWith(labelBounds))) continue;
                occupied.Add(labelBounds);
                DrawText(dc, wavelength.ToString(CultureInfo.InvariantCulture), point + offset, dark ? Brushes.LightSkyBlue : Brushes.Blue, 16, dark);
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

        private static void DrawAxisLabels(DrawingContext dc, CieDiagramProfile profile, bool dark)
        {
            Rect plot = profile.PlotAreaPixels;
            string xLabel = profile.Kind == CieDiagramKind.Cie1976uv ? "u'" : "x";
            string yLabel = profile.Kind == CieDiagramKind.Cie1976uv ? "v'" : "y";
            if (profile.Kind == CieDiagramKind.Cie1960uv)
            {
                xLabel = "u";
                yLabel = "v";
            }

            Brush textBrush = dark ? Brushes.LightGray : Brushes.Black;
            DrawText(dc, profile.Name, new Point(plot.Left, 6), textBrush, 18);
            DrawText(dc, xLabel, new Point(plot.Right - 8, plot.Bottom + 34), textBrush, 18);
            DrawText(dc, yLabel, new Point(plot.Left - 34, plot.Top - 4), textBrush, 18);
        }

        private static List<CieChromaticity> GetDiagramLocus(CieDiagramProfile profile)
        {
            return CieSpectrumLocus.Points
                .Select(point => profile.ToDiagramPoint(point.Chromaticity))
                .Where(point => point.IsFinite)
                .ToList();
        }

        private static int GetRowIntersections(double y, IReadOnlyList<CieChromaticity> polygon, Span<double> intersections)
        {
            int count = 0;
            for (int i = 0, j = polygon.Count - 1; i < polygon.Count; j = i++)
            {
                CieChromaticity pi = polygon[i];
                CieChromaticity pj = polygon[j];
                // Match the half-open edge test at polygon vertices.
                if ((pi.Y > y) != (pj.Y > y))
                    intersections[count++] = (pj.X - pi.X) * (y - pi.Y) / (pj.Y - pi.Y) + pi.X;
            }

            return count;
        }

        private static bool IsInsideSpectrum(double x, ReadOnlySpan<double> intersections)
        {
            bool inside = false;
            foreach (double intersection in intersections)
                inside ^= x < intersection;
            return inside;
        }

        private static uint XyToDisplayPixel(CieChromaticity xy)
        {
            if (!xy.IsFinite || xy.Y <= 0)
            {
                return 0xFFFF_FFFFu;
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

            // Bgra32 consumes the low byte first.
            return 0xFF00_0000u | (uint)ToByte(red) << 16 | (uint)ToByte(green) << 8 | ToByte(blue);
        }

        private static byte ToByte(double linear)
        {
            if (linear <= 0) return 0;
            if (linear >= 1) return byte.MaxValue;
            if (!double.IsNaN(linear))
            {
                short code = DisplayChannelLookup.Codes[(int)(linear * DisplayChannelBins)];
                if (code >= 0) return (byte)code;
            }
            return EncodeChannel(linear);
        }

        private static byte EncodeChannel(double linear)
        {
            double encoded = linear <= 0.0031308
                ? 12.92 * linear
                : 1.055 * Math.Pow(linear, 1 / 2.4) - 0.055;
            return (byte)Math.Round(Math.Clamp(encoded, 0, 1) * 255);
        }

        private static class DisplayChannelLookup
        {
            public static readonly short[] Codes = Build();

            private static short[] Build()
            {
                Span<byte> endpoints = stackalloc byte[DisplayChannelBins + 1];
                for (int i = 0; i < endpoints.Length; i++)
                    endpoints[i] = EncodeChannel(i / (double)DisplayChannelBins);

                short[] codes = new short[DisplayChannelBins];
                for (int i = 0; i < codes.Length; i++)
                {
                    // Leave a neighboring bin on each side of a rounding boundary to use the original calculation.
                    byte lower = endpoints[Math.Max(0, i - 1)];
                    byte upper = endpoints[Math.Min(DisplayChannelBins, i + 2)];
                    codes[i] = lower == upper ? lower : (short)-1;
                }
                return codes;
            }
        }

        private static void DrawText(DrawingContext dc, string text, Point point, Brush brush, double fontSize, bool outline = false)
        {
            FormattedText formattedText = new(
                text,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                new Typeface("Segoe UI"),
                fontSize,
                brush,
                1);
            if (outline) dc.DrawGeometry(null, new Pen(Brushes.Black, 2) { LineJoin = PenLineJoin.Round }, formattedText.BuildGeometry(point));
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
