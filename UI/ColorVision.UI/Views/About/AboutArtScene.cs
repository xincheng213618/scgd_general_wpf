using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Media3D;

#if SPECTRUM_ABOUT
namespace Spectrum.Help.Art;
#else
namespace ColorVision.UI.Views.About;
#endif

public enum AboutArtwork { Vision, Spectrum }

/// <summary>Animated optical illustrations for product About windows; no device or host dependencies.</summary>
public sealed class AboutArtScene : FrameworkElement
{
    private const int Meridians = 160;
    private const int Samples = 56;
    private const double Tau = Math.PI * 2;
    private readonly Point3D[,] _model = new Point3D[Meridians, Samples + 1];
    private readonly Point[,] _screen = new Point[Meridians, Samples + 1];
    private readonly double[] _depth = new double[Meridians];
    private readonly int[] _order = new int[Meridians];
    private readonly Pen[,] _wire = new Pen[Meridians, 5];
    private readonly Brush[] _dustBrushes = new Brush[Meridians];
    private readonly Particle[] _particles = new Particle[100];
    private readonly Stopwatch _clock = new();
    private DrawingGroup _backdrop = new();
    private VisionImageStudy? _vision;
    private Brush _glint = Brushes.White;
    private Brush _halo = Brushes.Transparent;
    private bool _observing;
    private bool _rendering;
    private TimeSpan _lastFrame = TimeSpan.MinValue;
    private Vector _pointer;
    private Vector _targetPointer;

    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.Register(
        nameof(IsDark), typeof(bool), typeof(AboutArtScene), new PropertyMetadata(true, OnPaletteChanged));

    public static readonly DependencyProperty MotionEnabledProperty = DependencyProperty.Register(
        nameof(MotionEnabled), typeof(bool), typeof(AboutArtScene), new PropertyMetadata(true, OnMotionChanged));

    public static readonly DependencyProperty ArtworkProperty = DependencyProperty.Register(
        nameof(Artwork), typeof(AboutArtwork), typeof(AboutArtScene), new PropertyMetadata(AboutArtwork.Vision, OnArtworkChanged));

    public AboutArtwork Artwork { get => (AboutArtwork)GetValue(ArtworkProperty); set => SetValue(ArtworkProperty, value); }

    private static void OnArtworkChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var scene = (AboutArtScene)d;
        scene.BuildModel();
        if (scene.IsLoaded) scene.BuildPalette();
        scene.InvalidateVisual();
    }

    public bool IsDark { get => (bool)GetValue(IsDarkProperty); set => SetValue(IsDarkProperty, value); }
    public bool MotionEnabled { get => (bool)GetValue(MotionEnabledProperty); set => SetValue(MotionEnabledProperty, value); }

    public AboutArtScene()
    {
        Focusable = false;
        IsHitTestVisible = false;
        BuildModel();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
    }

    public void TrackPointer(Point? point)
    {
        if (!_rendering) return;
        _targetPointer = point.HasValue
            ? new Vector(Math.Clamp((point.Value.X - 440) / 440, -1, 1), Math.Clamp((point.Value.Y - 250) / 250, -1, 1))
            : new Vector();
    }

    private static void OnPaletteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var scene = (AboutArtScene)d;
        if (scene.IsLoaded) scene.BuildPalette();
        scene.InvalidateVisual();
    }

    private static void OnMotionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((AboutArtScene)d).UpdateMotion();

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_observing)
        {
            SystemParameters.StaticPropertyChanged += OnSystemSettingsChanged;
            RenderCapability.TierChanged += OnRenderTierChanged;
            _observing = true;
        }
        BuildPalette();
        UpdateMotion();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        StopMotion();
        if (!_observing) return;
        SystemParameters.StaticPropertyChanged -= OnSystemSettingsChanged;
        RenderCapability.TierChanged -= OnRenderTierChanged;
        _observing = false;
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateMotion();
    private void OnRenderTierChanged(object? sender, EventArgs e) => UpdateMotion();

    private void OnSystemSettingsChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast))) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsLoaded) return;
            BuildPalette();
            UpdateMotion();
            InvalidateVisual();
        });
    }

    private void UpdateMotion()
    {
        bool animate = IsLoaded && IsVisible && MotionEnabled && SystemParameters.ClientAreaAnimation
            && !SystemParameters.HighContrast && (RenderCapability.Tier >> 16) > 0;
        if (!animate) { StopMotion(); return; }
        if (_rendering) return;
        _rendering = true;
        _lastFrame = TimeSpan.MinValue;
        _clock.Start();
        CompositionTarget.Rendering += OnFrame;
    }

    private void StopMotion()
    {
        if (_rendering) CompositionTarget.Rendering -= OnFrame;
        _rendering = false;
        _clock.Stop();
        _targetPointer = new Vector();
    }

    private void OnFrame(object? sender, EventArgs e)
    {
        if (e is not RenderingEventArgs frame) return;
        // Rendering can be raised more than once per presentation; cap the illustration at 30 fps.
        if (_lastFrame != TimeSpan.MinValue && (frame.RenderingTime - _lastFrame).TotalSeconds < 1d / 30) return;
        _lastFrame = frame.RenderingTime;
        _pointer += (_targetPointer - _pointer) * 0.065;
        InvalidateVisual();
    }

    private void BuildModel()
    {
        for (int u = 0; u < Meridians; u++)
        {
            _order[u] = u;
            double a = Tau * u / Meridians;
            double twist = a * 1.5;
            double radius = 158 + 13 * Math.Cos(3 * a);
            for (int v = 0; v <= Samples; v++)
            {
                double b = Tau * v / Samples;
                double radial = 53 * Math.Cos(b) * Math.Cos(twist) - 36 * Math.Sin(b) * Math.Sin(twist);
                double z = 53 * Math.Cos(b) * Math.Sin(twist) + 36 * Math.Sin(b) * Math.Cos(twist);
                _model[u, v] = new Point3D((radius + radial) * Math.Cos(a), (radius + radial) * Math.Sin(a), z + 25 * Math.Sin(a * 2));
            }
        }
        var random = new Random(271828);
        for (int i = 0; i < _particles.Length; i++)
            _particles[i] = new Particle(random.NextDouble() * Tau, 230 + random.NextDouble() * 125,
                random.NextDouble(), random.NextDouble() * Tau, 0.45 + random.NextDouble() * 0.85);
    }

    private void BuildPalette()
    {
        bool dark = IsDark;
        if (Artwork == AboutArtwork.Vision)
        {
            _vision = SystemParameters.HighContrast ? null : new VisionImageStudy(dark);
            return;
        }
        _vision = null;
        for (int u = 0; u < Meridians; u++)
        {
            Color color = FieldColor((double)u / Meridians, dark);
            for (int level = 0; level < 5; level++)
            {
                var pen = new Pen(Solid(color, (byte)((dark ? 42 : 32) + level * (dark ? 33 : 28))), level == 4 ? 0.95 : 0.7);
                pen.Freeze();
                _wire[u, level] = pen;
            }
            _dustBrushes[u] = Solid(color, dark ? (byte)175 : (byte)150);
        }
        _glint = Solid(dark ? Color.FromRgb(216, 241, 255) : Color.FromRgb(59, 111, 153), 210);
        _halo = Glow(dark ? "#8DCDF4" : "#628CBA", dark ? (byte)65 : (byte)26);
        _backdrop = new DrawingGroup();
        using (DrawingContext dc = _backdrop.Open())
        {
            if (!SystemParameters.HighContrast)
            {
                dc.DrawEllipse(Glow(dark ? "#15416B" : "#B2CAE9", dark ? (byte)76 : (byte)125), null, new Point(439, 235), 360, 228);
                dc.DrawEllipse(Glow(dark ? "#146D77" : "#A1E3DB", dark ? (byte)51 : (byte)65), null, new Point(300, 225), 215, 162);
                dc.DrawEllipse(Glow(dark ? "#6D427F" : "#D2BDE4", dark ? (byte)56 : (byte)89), null, new Point(553, 223), 233, 174);
                dc.DrawEllipse(Glow(dark ? "#905551" : "#EBD5C3", dark ? (byte)24 : (byte)76), null, new Point(512, 342), 204, 97);

                Pen line = FrozenPen(dark ? "#728AAB" : "#637A98", dark ? (byte)34 : (byte)36, 0.6);
                if (Artwork == AboutArtwork.Spectrum)
                {
                    dc.PushTransform(new RotateTransform(-17, 442, 251));
                    dc.DrawEllipse(null, line, new Point(442, 251), 284, 153);
                    dc.DrawEllipse(null, FrozenPen(dark ? "#728AAB" : "#637A98", 17, 0.5), new Point(442, 251), 305, 164);
                    dc.Pop();
                    for (int i = 0; i < 65; i++)
                    {
                        double x = 183 + i * 8;
                        double height = i % 8 == 0 ? 5 : 2;
                        dc.DrawLine(line, new Point(x, 416), new Point(x, 416 + height));
                    }
                }
                foreach (Point point in new[] { new Point(167, 88), new Point(714, 91), new Point(166, 402), new Point(713, 405) })
                {
                    dc.DrawLine(line, new Point(point.X - 3, point.Y), new Point(point.X + 3, point.Y));
                    dc.DrawLine(line, new Point(point.X, point.Y - 3), new Point(point.X, point.Y + 3));
                }
            }
        }
        _backdrop.Freeze();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        DrawingContext dc = drawingContext;
        if (ActualWidth <= 0 || ActualHeight <= 0 || SystemParameters.HighContrast) return;
        if (Artwork == AboutArtwork.Vision)
        {
            _vision?.Draw(dc, _clock.Elapsed.TotalSeconds, _pointer, VisualTreeHelper.GetDpi(this).PixelsPerDip);
            return;
        }
        dc.DrawDrawing(_backdrop);
        double time = _clock.Elapsed.TotalSeconds;
        double tilt = 0.87 + Math.Sin(time * 0.13) * 0.13 + _pointer.Y * 0.13;
        double turn = Math.Sin(time * 0.11) * 0.28 + _pointer.X * 0.22;
        double roll = -0.28 + Math.Sin(time * 0.085) * 0.12;
        double cx = Math.Cos(tilt), sx = Math.Sin(tilt);
        double cy = Math.Cos(turn), sy = Math.Sin(turn);
        double cz = Math.Cos(roll), sz = Math.Sin(roll);

        for (int u = 0; u < Meridians; u++)
        {
            double depth = 0;
            for (int v = 0; v <= Samples; v++)
            {
                Point3D p = _model[u, v];
                double y = p.Y * cx - p.Z * sx;
                double z = p.Y * sx + p.Z * cx;
                double x = p.X * cy + z * sy;
                z = -p.X * sy + z * cy;
                double perspective = 900 / (900 - z);
                _screen[u, v] = new Point(444 + (x * cz - y * sz) * perspective * 1.04 + _pointer.X * 6,
                    232 + (x * sz + y * cz) * perspective * 1.12 + _pointer.Y * 4);
                depth += z;
            }
            _depth[u] = depth / (Samples + 1);
        }
        Array.Sort(_order, (a, b) => _depth[a].CompareTo(_depth[b]));
        DrawParticles(dc, time, false);

        foreach (int u in _order)
        {
            int level = Math.Clamp((int)((_depth[u] + 155) / 65), 0, 4);
            var geometry = new StreamGeometry();
            using (StreamGeometryContext path = geometry.Open())
            {
                path.BeginFigure(_screen[u, 0], false, true);
                for (int v = 1; v < Samples; v++) path.LineTo(_screen[u, v], true, false);
            }
            geometry.Freeze();
            dc.DrawGeometry(null, _wire[u, level], geometry);

            // Sparse luminous samples carry depth without creating thousands of WPF elements.
            if (u % 2 != 0) continue;
            for (int v = 3; v < Samples; v += 9)
            {
                double radius = level > 2 ? 0.8 : 0.48;
                dc.DrawEllipse(_dustBrushes[u], null, _screen[u, v], radius, radius);
            }
        }

        // Continuous filaments bridge the meridians; short sections preserve the spectral gradient.
        for (int v = 0; v < Samples; v += 7)
        {
            for (int u = 0; u < Meridians; u += 10)
            {
                var geometry = new StreamGeometry();
                using (StreamGeometryContext path = geometry.Open())
                {
                    path.BeginFigure(_screen[u, v], false, false);
                    for (int step = 1; step <= 10; step++) path.LineTo(ScreenPoint(u + step, v), true, false);
                }
                geometry.Freeze();
                dc.DrawGeometry(null, _wire[u, 0], geometry);
            }
        }
        DrawParticles(dc, time, true);

        // Three travelling highlights, each with a small radial falloff instead of a full-window blur.
        for (int i = 0; i < 3; i++)
        {
            double sweep = (time * (0.025 + i * 0.006) + i / 3d + 0.14) % 1 * Meridians;
            int u = (int)sweep;
            int v = 6 + i * 13;
            Point a = _screen[u, v], b = ScreenPoint(u + 1, v);
            Point point = a + (b - a) * (sweep - u);
            dc.DrawEllipse(_halo, null, point, 14, 14);
            dc.DrawEllipse(_glint, null, point, 1.25, 1.25);
        }
    }

    private Point ScreenPoint(int u, int v)
    {
        return _screen[u % Meridians, v];
    }

    private void DrawParticles(DrawingContext dc, double time, bool foreground)
    {
        for (int i = 0; i < _particles.Length; i++)
        {
            Particle p = _particles[i];
            if ((p.Depth > 0.56) != foreground) continue;
            double angle = p.Angle + time * (0.007 + p.Depth * 0.014);
            double x = Math.Cos(angle) * p.Radius;
            double y = Math.Sin(angle) * p.Radius * 0.44;
            Point point = new(444 + x * 0.97 + y * 0.22 + _pointer.X * (5 + p.Depth * 12),
                249 - x * 0.22 + y * 0.97 + Math.Sin(time * 0.3 + p.Phase) * 4 + _pointer.Y * 8);
            double opacity = (0.28 + p.Depth * 0.42) * (0.65 + 0.35 * Math.Sin(time * 0.6 + p.Phase));
            dc.PushOpacity(opacity);
            dc.DrawEllipse(_dustBrushes[i * 13 % Meridians], null, point, p.Size, p.Size);
            dc.Pop();
        }
    }

    private static Color FieldColor(double position, bool dark)
    {
        // The same hue progression becomes luminous at night and mineral-toned on the pearl surface.
        Color[] colors = dark
            ? [Color.FromRgb(244, 186, 146), Color.FromRgb(221, 148, 196), Color.FromRgb(148, 141, 245), Color.FromRgb(90, 179, 239), Color.FromRgb(102, 225, 209), Color.FromRgb(244, 186, 146)]
            : [Color.FromRgb(177, 111, 67), Color.FromRgb(167, 82, 134), Color.FromRgb(110, 88, 194), Color.FromRgb(48, 118, 181), Color.FromRgb(40, 148, 140), Color.FromRgb(177, 111, 67)];
        double scaled = position * (colors.Length - 1);
        int index = Math.Min((int)scaled, colors.Length - 2);
        double t = scaled - index;
        Color a = colors[index], b = colors[index + 1];
        return Color.FromRgb((byte)(a.R + (b.R - a.R) * t), (byte)(a.G + (b.G - a.G) * t), (byte)(a.B + (b.B - a.B) * t));
    }

    private static SolidColorBrush Solid(Color color, byte alpha)
    {
        color.A = alpha;
        var brush = new SolidColorBrush(color);
        brush.Freeze();
        return brush;
    }

    private static Pen FrozenPen(string hex, byte alpha, double thickness)
    {
        var pen = new Pen(Solid((Color)ColorConverter.ConvertFromString(hex), alpha), thickness);
        pen.Freeze();
        return pen;
    }

    private static RadialGradientBrush Glow(string hex, byte alpha)
    {
        Color color = (Color)ColorConverter.ConvertFromString(hex);
        color.A = alpha;
        var brush = new RadialGradientBrush(color, Color.FromArgb(0, color.R, color.G, color.B));
        brush.Freeze();
        return brush;
    }

    private readonly record struct Particle(double Angle, double Radius, double Depth, double Phase, double Size);
}
