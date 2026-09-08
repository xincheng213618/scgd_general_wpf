using System;
using System.Globalization;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

#if SPECTRUM_ABOUT
namespace Spectrum.Help.Art;
#else
namespace ColorVision.UI.Views.About;
#endif

/// <summary>A single optical image separated into acquisition, color sampling and measured geometry.</summary>
internal sealed class VisionImageStudy
{
    private const double Tau = Math.PI * 2;
    private static readonly Rect ImageBounds = new(-172, -119, 344, 238);
    private static readonly Color[] Colors = [Color.FromRgb(61, 157, 234), Color.FromRgb(85, 215, 203), Color.FromRgb(189, 179, 238), Color.FromRgb(232, 154, 174), Color.FromRgb(246, 203, 134)];
    private static readonly string[] RegistrationColors = ["#E1A1B6", "#A3DDBA", "#83C7F3"];
    private static readonly Typeface CaptionTypeface = new("Consolas");
    private readonly DrawingGroup _image = new();
    private readonly DrawingGroup _samples = new();
    private readonly DrawingGroup _geometry = new();
    private readonly DrawingGroup _atmosphere = new();
    private readonly Pen _construction;
    private readonly Pen _edge;
    private readonly Brush _spark;
    private readonly Brush _halo;
    private readonly Brush _caption;
    private readonly Brush _plane;
    private readonly Pen _scan = Pen("#91DAEE", 48, 0.8);

    public VisionImageStudy(bool dark)
    {
        _construction = Pen(dark ? "#526C86" : "#7890A6", 65, 0.7);
        _edge = Pen(dark ? "#B0D8EB" : "#557688", 135, 0.8);
        _spark = Brush(dark ? "#D1F4FF" : "#287A93", 240);
        _halo = Glow("#60CFF3", dark ? (byte)85 : (byte)40);
        _caption = Brush(dark ? "#879AAF" : "#617689", 225);
        _plane = Brush(dark ? "#142133" : "#E6EEF4", dark ? (byte)100 : (byte)18);
        using (DrawingContext dc = _atmosphere.Open())
        {
            dc.DrawEllipse(Glow(dark ? "#146A87" : "#65C4D4", dark ? (byte)64 : (byte)32), null, new Point(551, 294), 256, 217);
            dc.DrawEllipse(Glow(dark ? "#664582" : "#B092C9", dark ? (byte)44 : (byte)22), null, new Point(660, 235), 203, 170);
            var random = new Random(57721);
            for (int i = 0; i < 68; i++)
            {
                Point p = new(364 + random.NextDouble() * 461, 107 + random.NextDouble() * 347);
                dc.DrawEllipse(Brush(dark ? "#A4C2D9" : "#4D758C", (byte)(15 + random.Next(30))), null, p, 0.65, 0.65);
            }
        }
        _atmosphere.Freeze();
        BuildImage(dark);
        _image.Freeze();
        BuildSamples(dark);
        BuildGeometry(dark);
    }

    private void BuildImage(bool dark)
    {
        // A deterministic optical study, generated once per palette; no frame-by-frame bitmap uploads.
        const int width = 688, height = 476;
        byte[] pixels = new byte[width * height * 4];
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                double px = (x / (width - 1d) - 0.5) * 344;
                double py = (y / (height - 1d) - 0.5) * 238;
                Color c = Sample(px, py);
                int offset = (y * width + x) * 4;
                pixels[offset] = c.B; pixels[offset + 1] = c.G; pixels[offset + 2] = c.R; pixels[offset + 3] = 255;
            }
        }
        BitmapSource bitmap = BitmapSource.Create(width, height, 192, 192, PixelFormats.Bgra32, null, pixels, width * 4);
        bitmap.Freeze();
        using DrawingContext dc = _image.Open();
        dc.DrawRoundedRectangle(Brush("#050C15", 50), null, new Rect(-173, -116, 346, 241), 5, 5);
        dc.PushClip(new RectangleGeometry(ImageBounds, 3, 3));
        dc.DrawImage(bitmap, ImageBounds);
        Pen scan = Pen("#A9D5EA", 13, 0.4);
        for (int y = -118; y < 120; y += 4) dc.DrawLine(scan, new Point(-172, y), new Point(172, y));
        dc.Pop();
        dc.DrawRoundedRectangle(null, Pen(dark ? "#93D2E0" : "#3B697B", 165, 0.85), ImageBounds, 3, 3);
        // A quiet RGB edge is a photographic registration detail, not a loading indicator.
        for (int i = 0; i < 3; i++)
            dc.DrawLine(Pen(RegistrationColors[i], 180, 1.2), new Point(-172 + i * 3, -111), new Point(-172 + i * 3, -84));
    }

    private static Color Sample(double x, double y)
    {
        double radius = Radius(x, y);
        double edge = Math.Exp(-Math.Pow((radius - 1) * 15, 2));
        double inside = 1 / (1 + Math.Exp((radius - 0.99) * 38));
        double band = 0.5 + 0.5 * Math.Sin(x * 0.028 - y * 0.038 + Math.Sin(y * 0.029) * 1.5);
        double fold = Math.Pow(0.5 + 0.5 * Math.Cos(radius * 9.8 - x * 0.018 + y * 0.022), 6);
        double light = inside * (0.20 + 0.50 * band + 0.42 * fold) + edge * 0.56;
        double hue = Math.Clamp((x + 115) / 255 + y * 0.0018, 0, 1);
        Color color = Hue(hue);
        return Color.FromRgb(Channel(9 + color.R * light), Channel(18 + color.G * light), Channel(29 + color.B * light));
    }

    private static byte Channel(double value) => (byte)Math.Clamp(value, 0, 255);

    private static double Radius(double x, double y)
    {
        double a = x * 0.974 + y * 0.225;
        double b = -x * 0.225 + y * 0.974;
        return Math.Pow(Math.Pow(Math.Abs(a / 124), 3.4) + Math.Pow(Math.Abs(b / 77), 3.4), 1 / 3.4);
    }

    private static Color Hue(double position)
    {
        double f = Math.Clamp(position, 0, 1) * (Colors.Length - 1);
        int i = Math.Min((int)f, Colors.Length - 2);
        double t = f - i;
        Color a = Colors[i], b = Colors[i + 1];
        return Color.FromRgb(Channel(a.R + (b.R - a.R) * t), Channel(a.G + (b.G - a.G) * t), Channel(a.B + (b.B - a.B) * t));
    }

    private void BuildSamples(bool dark)
    {
        using (DrawingContext dc = _samples.Open())
        {
            dc.DrawRoundedRectangle(_plane, _construction, ImageBounds, 3, 3);
            Pen grid = Pen(dark ? "#74A0BE" : "#658BA4", 30, 0.45);
            for (int x = -160; x <= 160; x += 16) dc.DrawLine(grid, new Point(x, -112), new Point(x, 112));
            for (int y = -112; y <= 112; y += 16) dc.DrawLine(grid, new Point(-160, y), new Point(160, y));
            for (int y = -104; y <= 104; y += 8)
            {
                for (int x = -152; x <= 152; x += 8)
                {
                    double radius = Radius(x, y);
                    if (radius > 1.08) continue;
                    Color color = Sample(x, y);
                    color.A = (byte)(dark ? 180 : 220);
                    var brush = new SolidColorBrush(color);
                    brush.Freeze();
                    double size = 1.1 + Math.Max(0, 1 - radius) * 0.9;
                    dc.DrawRectangle(brush, null, new Rect(x - size / 2, y - size / 2, size, size));
                }
            }
        }
        _samples.Freeze();
    }

    private void BuildGeometry(bool dark)
    {
        using (DrawingContext dc = _geometry.Open())
        {
            dc.DrawRoundedRectangle(Brush(dark ? "#182B3B" : "#F1F7FC", dark ? (byte)28 : (byte)10), _construction, ImageBounds, 3, 3);
            for (int ring = 0; ring < 8; ring++)
            {
                double scale = 1 - ring * 0.017;
                dc.DrawGeometry(null, Pen(dark ? "#9BD8E8" : "#39778F", (byte)(ring == 0 ? 190 : 48 - ring * 4), ring == 0 ? 1 : 0.55), Contour(scale));
            }
            // The ROI and dimensions refer to the same image contour on all three planes.
            Rect roi = new(-138, -89, 276, 178);
            Pen roiPen = Pen(dark ? "#BCD2E1" : "#3E6D83", 180, 0.85);
            foreach (Point p in new[] { roi.TopLeft, roi.TopRight, roi.BottomLeft, roi.BottomRight })
            {
                double dx = p.X < 0 ? 14 : -14, dy = p.Y < 0 ? 14 : -14;
                dc.DrawLine(roiPen, p, p + new Vector(dx, 0));
                dc.DrawLine(roiPen, p, p + new Vector(0, dy));
                dc.DrawEllipse(_spark, null, p, 1.4, 1.4);
            }
            dc.DrawLine(_construction, new Point(-119, 104), new Point(119, 104));
            dc.DrawLine(_construction, new Point(151, -71), new Point(151, 71));
            foreach (double x in new[] { -119d, 119d }) dc.DrawLine(roiPen, new Point(x, 99), new Point(x, 109));
            foreach (double y in new[] { -71d, 71d }) dc.DrawLine(roiPen, new Point(146, y), new Point(156, y));
            dc.DrawLine(roiPen, new Point(-8, 0), new Point(8, 0));
            dc.DrawLine(roiPen, new Point(0, -8), new Point(0, 8));
            dc.DrawEllipse(null, _construction, new Point(), 16, 16);
        }
        _geometry.Freeze();
    }

    private static StreamGeometry Contour(double scale)
    {
        var geometry = new StreamGeometry();
        using (StreamGeometryContext path = geometry.Open())
        {
            for (int i = 0; i <= 160; i++)
            {
                double angle = Tau * i / 160;
                double a = 124 * Math.CopySign(Math.Pow(Math.Abs(Math.Cos(angle)), 2 / 3.4), Math.Cos(angle)) * scale;
                double b = 77 * Math.CopySign(Math.Pow(Math.Abs(Math.Sin(angle)), 2 / 3.4), Math.Sin(angle)) * scale;
                Point point = new(a * 0.974 - b * 0.225, a * 0.225 + b * 0.974);
                if (i == 0) path.BeginFigure(point, false, true);
                else path.LineTo(point, true, false);
            }
        }
        geometry.Freeze();
        return geometry;
    }

    public void Draw(DrawingContext dc, double time, Vector pointer, double pixelsPerDip)
    {
        dc.DrawDrawing(_atmosphere);
        double lift = Math.Sin(time * 0.38) * 3;
        Matrix Projection(int layer) => new(0.94, 0.19 + pointer.X * 0.014, -0.34 + pointer.Y * 0.018, 0.82,
            578 + layer * 15 + pointer.X * (3 + layer * 2), 329 - layer * 51 + lift + pointer.Y * (3 + layer * 2));
        Matrix back = Projection(0), front = Projection(2);
        foreach (Point point in new[] { ImageBounds.TopLeft, ImageBounds.TopRight, ImageBounds.BottomLeft, ImageBounds.BottomRight })
            dc.DrawLine(_construction, back.Transform(point), front.Transform(point));
        Drawing[] layers = [_image, _samples, _geometry];
        for (int layer = 0; layer < layers.Length; layer++)
        {
            Matrix transform = Projection(layer);
            dc.PushTransform(new MatrixTransform(transform));
            dc.DrawDrawing(layers[layer]);
            if (layer == 2)
            {
                double x = -137 + (time * 13 + 86) % 274;
                dc.DrawLine(_scan, new Point(x, -85), new Point(x, 85));
                dc.DrawEllipse(_halo, null, new Point(x, 0), 22, 22);
                dc.DrawEllipse(_spark, null, new Point(x, 0), 1.25, 1.25);
            }
            dc.Pop();
        }

        // Three connected terminals echo the flow editor without pretending to run a device or algorithm.
        Point start = front.Transform(new Point(138, 89));
        Point[] route = [start, start + new Vector(24, 6), new(788, 395), new(746, 444), new(664, 444)];
        var path = new StreamGeometry();
        using (StreamGeometryContext ctx = path.Open())
        {
            ctx.BeginFigure(route[0], false, false);
            for (int i = 1; i < route.Length; i++) ctx.LineTo(route[i], true, false);
        }
        dc.DrawGeometry(null, _construction, path);
        for (int i = 2; i < route.Length; i++) dc.DrawEllipse(_plane, _edge, route[i], 3, 3);
        double phase = (time * 0.11 + 0.35) % 1 * (route.Length - 1);
        int segment = (int)phase;
        Point traveler = route[segment] + (route[segment + 1] - route[segment]) * (phase - segment);
        dc.DrawEllipse(_halo, null, traveler, 10, 10);
        dc.DrawEllipse(_spark, null, traveler, 1.4, 1.4);
        Caption(dc, "IMAGE", back.Transform(new Point(-166, 134)), pixelsPerDip);
        Caption(dc, "COLOR", Projection(1).Transform(new Point(-188, 108)), pixelsPerDip);
        Caption(dc, "GEOMETRY", front.Transform(new Point(-166, -135)), pixelsPerDip);
    }

    private void Caption(DrawingContext dc, string value, Point point, double pixelsPerDip)
    {
        var text = new FormattedText(value, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, CaptionTypeface, 7.5, _caption, pixelsPerDip);
        dc.DrawText(text, point);
    }

    private static SolidColorBrush Brush(string hex, byte alpha)
    {
        Color color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = alpha;
        var brush = new SolidColorBrush(color); brush.Freeze(); return brush;
    }

    private static Pen Pen(string hex, byte alpha, double width)
    {
        var pen = new Pen(Brush(hex, alpha), width); pen.Freeze(); return pen;
    }

    private static RadialGradientBrush Glow(string hex, byte alpha)
    {
        Color color = (Color)ColorConverter.ConvertFromString(hex); color.A = alpha;
        var brush = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B)); brush.Freeze(); return brush;
    }
}
