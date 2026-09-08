using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision;

/// <summary>
/// Retained optical field: geometry is rebuilt only for palette changes, three composition transforms animate.
/// No frame callbacks, layout animations, bitmap effects or external assets.
/// </summary>
public sealed class StartupScene : FrameworkElement
{
    private readonly VisualCollection _layers;
    private readonly RotateTransform _fieldRotation = new(0, 480, 216);
    private readonly RotateTransform _orbitRotation = new(0, 480, 216);
    private readonly TranslateTransform _scanTranslation = new();
    private bool _observingSettings;
    private bool _highContrast;

    public static readonly DependencyProperty IsDarkProperty = DependencyProperty.Register(
        nameof(IsDark), typeof(bool), typeof(StartupScene), new PropertyMetadata(true, OnPaletteChanged));

    public bool IsDark { get => (bool)GetValue(IsDarkProperty); set => SetValue(IsDarkProperty, value); }

    private static void OnPaletteChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((StartupScene)d).BuildScene();

    public StartupScene()
    {
        _layers = new VisualCollection(this);
        IsHitTestVisible = false;
        Focusable = false;
        BuildScene();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
        IsVisibleChanged += OnVisibilityChanged;
    }

    protected override int VisualChildrenCount => _layers?.Count ?? 0;
    protected override Visual GetVisualChild(int index) => _layers[index];

    protected override Size ArrangeOverride(Size finalSize)
    {
        foreach (DrawingLayer layer in _layers)
            layer.Arrange(new Rect(finalSize));
        return finalSize;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (!_observingSettings)
        {
            SystemParameters.StaticPropertyChanged += OnSystemParametersChanged;
            RenderCapability.TierChanged += OnRenderTierChanged;
            _observingSettings = true;
        }
        if (_highContrast != SystemParameters.HighContrast) BuildScene();
        else UpdateMotion();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        if (_observingSettings)
        {
            SystemParameters.StaticPropertyChanged -= OnSystemParametersChanged;
            RenderCapability.TierChanged -= OnRenderTierChanged;
            _observingSettings = false;
        }
        StopMotion();
    }

    private void OnVisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) => UpdateMotion();

    private void OnSystemParametersChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(SystemParameters.ClientAreaAnimation) or nameof(SystemParameters.HighContrast))) return;
        Dispatcher.InvokeAsync(() =>
        {
            if (!IsLoaded) return;
            if (e.PropertyName == nameof(SystemParameters.HighContrast)) BuildScene();
            else UpdateMotion();
        });
    }

    private void OnRenderTierChanged(object? sender, EventArgs e) => UpdateMotion();

    private void UpdateMotion()
    {
        StopMotion();
        if (!IsLoaded || !IsVisible || !SystemParameters.ClientAreaAnimation || SystemParameters.HighContrast || (RenderCapability.Tier >> 16) == 0)
            return;

        Animate(_fieldRotation, RotateTransform.AngleProperty, -4, 4, 7, true);
        Animate(_orbitRotation, RotateTransform.AngleProperty, 0, 360, 32, false);
        Animate(_scanTranslation, TranslateTransform.XProperty, -170, 190, 4.8, true);
    }

    private void StopMotion()
    {
        _fieldRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _orbitRotation.BeginAnimation(RotateTransform.AngleProperty, null);
        _scanTranslation.BeginAnimation(TranslateTransform.XProperty, null);
    }

    private static void Animate(Animatable target, DependencyProperty property, double from, double to, double seconds, bool reverse)
    {
        var animation = new DoubleAnimation(from, to, TimeSpan.FromSeconds(seconds))
        {
            AutoReverse = reverse,
            RepeatBehavior = RepeatBehavior.Forever
        };
        if (reverse)
            animation.EasingFunction = new SineEase { EasingMode = EasingMode.EaseInOut };
        Timeline.SetDesiredFrameRate(animation, 30);
        animation.Freeze();
        target.BeginAnimation(property, animation);
    }

    private void BuildScene()
    {
        StopMotion();
        foreach (DrawingLayer layer in _layers)
        {
            layer.RenderTransform = Transform.Identity;
            layer.CacheMode = null;
        }
        _layers.Clear();
        _highContrast = SystemParameters.HighContrast;
        AddLayer(DrawBackground);
        if (!_highContrast)
        {
            var orbitTransform = new TransformGroup();
            orbitTransform.Children.Add(_orbitRotation);
            orbitTransform.Children.Add(new ScaleTransform(1, 160d / 189, 480, 216));
            AddLayer(DrawOrbitMarkers, orbitTransform);
            AddLayer(DrawOpticalField, _fieldRotation);
            AddLayer(DrawScan, _scanTranslation);
            AddLayer(DrawForeground);
        }
        InvalidateMeasure();
        InvalidateArrange();
        UpdateMotion();
    }

    private void AddLayer(Action<DrawingContext> draw, Transform? transform = null)
    {
        var layer = new DrawingLayer(draw) { RenderTransform = transform ?? Transform.Identity };
        // Cache each immutable drawing BELOW its animated transform: caching the entire
        // scene would be invalidated every frame by the children that move inside it.
        layer.CacheMode = new BitmapCache();
        _layers.Add(layer);
    }

    private void DrawBackground(DrawingContext dc)
    {
        dc.DrawRectangle(_highContrast ? SystemColors.WindowBrush : Brush(IsDark ? "#080E19" : "#F4F5F7"), null, new Rect(0, 0, 820, 460));
        if (_highContrast) return;
        DrawGlow(dc, new Point(470, 213), 315, 206, IsDark ? "#23324F" : "#BACFE1", IsDark ? 0.52 : 0.30);
        DrawGlow(dc, new Point(391, 219), 200, 130, IsDark ? "#125B68" : "#8EC3C9", IsDark ? 0.20 : 0.18);
        DrawGlow(dc, new Point(594, 240), 206, 165, IsDark ? "#463A80" : "#B6ACD9", IsDark ? 0.23 : 0.18);

        // A quiet sensor grid; all paths and brushes are immutable render resources.
        Pen gridPen = Pen(IsDark ? "#101E30" : "#E0E6ED", 0.65);
        for (int x = 38; x < 820; x += 38)
            dc.DrawLine(gridPen, new Point(x, 75), new Point(x, 329));
        for (int y = 101; y < 330; y += 38)
            dc.DrawLine(gridPen, new Point(38, y), new Point(782, y));
        Pen crossPen = Pen(IsDark ? "#30415A" : "#A6B8CB", 0.7);
        foreach (Point point in new[] { new Point(76, 139), new Point(228, 291), new Point(722, 101), new Point(722, 291) })
        {
            dc.DrawLine(crossPen, new Point(point.X - 3, point.Y), new Point(point.X + 3, point.Y));
            dc.DrawLine(crossPen, new Point(point.X, point.Y - 3), new Point(point.X, point.Y + 3));
        }

        DrawRays(dc);
        DrawOrbit(dc);
    }

    private void DrawScan(DrawingContext dc)
    {
        // Narrow moving exposure plane, composed behind the lower text fade.
        var scan = new LinearGradientBrush();
        Color color = IsDark ? Color.FromRgb(84, 217, 255) : Color.FromRgb(35, 147, 174);
        scan.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 0));
        scan.GradientStops.Add(new GradientStop(Color.FromArgb(13, color.R, color.G, color.B), 0.92));
        scan.GradientStops.Add(new GradientStop(Color.FromArgb(0, color.R, color.G, color.B), 1));
        scan.Freeze();
        dc.DrawRectangle(scan, null, new Rect(442, 100, 38, 220));
        dc.DrawLine(Pen(IsDark ? "#4567A6BD" : "#703D7D96", 0.8), new Point(477, 112), new Point(477, 306));
        dc.DrawLine(Pen(IsDark ? "#7CACDEE9" : "#A02F718B", 1.1), new Point(473, 112), new Point(481, 112));
        dc.DrawLine(Pen(IsDark ? "#7CACDEE9" : "#A02F718B", 1.1), new Point(473, 306), new Point(481, 306));
    }

    private void DrawForeground(DrawingContext dc)
    {
        Color surface = IsDark ? Color.FromRgb(8, 14, 25) : Color.FromRgb(244, 245, 247);
        var fade = new LinearGradientBrush { StartPoint = new Point(0, 0), EndPoint = new Point(0, 1) };
        fade.GradientStops.Add(new GradientStop(Color.FromArgb(0, surface.R, surface.G, surface.B), 0));
        fade.GradientStops.Add(new GradientStop(Color.FromArgb(234, surface.R, surface.G, surface.B), 0.68));
        fade.GradientStops.Add(new GradientStop(surface, 1));
        fade.Freeze();
        dc.DrawRectangle(fade, null, new Rect(0, 285, 820, 175));
    }

    private void DrawRays(DrawingContext dc)
    {
        for (int i = 0; i < 17; i++)
        {
            double spread = i - 8;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext path = geometry.Open())
            {
                path.BeginFigure(new Point(42, 212 + spread * 0.32), false, false);
                path.BezierTo(new Point(253, 213 + spread * 0.3), new Point(330, 211 + spread * 1.6), new Point(469, 215 + spread * 2.1), true, false);
                path.BezierTo(new Point(592, 212 + spread * 3.5), new Point(682, 210 + spread * 7.3), new Point(827, 198 + spread * 8.4), true, false);
            }
            geometry.Freeze();
            var light = new LinearGradientBrush { StartPoint = new Point(0, 0.5), EndPoint = new Point(1, 0.5) };
            light.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(0, 114, 223, 243) : Color.FromArgb(0, 32, 138, 161), 0));
            light.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(70, 132, 231, 244) : Color.FromArgb(80, 32, 138, 161), 0.35));
            light.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(100, 98, 167, 240) : Color.FromArgb(110, 53, 102, 172), 0.60));
            light.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(4, 160, 135, 239) : Color.FromArgb(4, 120, 91, 167), 1));
            light.Freeze();
            var pen = new Pen(light, i == 8 ? 1.25 : 0.65);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
        DrawGlow(dc, new Point(313, 214), 75, 5, IsDark ? "#A2F6FF" : "#3D9AAE", IsDark ? 0.35 : 0.18);
    }

    private void DrawOpticalField(DrawingContext dc)
    {
        // Parametric toroidal wavefront, projected into an oblique optical volume.
        for (int ring = 0; ring < 48; ring++)
        {
            double v = ring * Math.PI * 2 / 48;
            var geometry = new StreamGeometry();
            using (StreamGeometryContext path = geometry.Open())
            {
                for (int sample = 0; sample <= 160; sample++)
                {
                    double u = sample * Math.PI * 2 / 160;
                    double radius = 111 + 37 * Math.Cos(v);
                    double x = radius * Math.Cos(u);
                    double y = radius * Math.Sin(u);
                    double z = 37 * Math.Sin(v) + 14 * Math.Sin(u * 2);
                    double projectedX = x * 0.84 + z * 0.78;
                    double projectedY = y * 0.99 + z * 0.28;
                    Point point = new(480 + projectedX * 1.12 + projectedY * 0.42, 213 + projectedY * 0.88 - projectedX * 0.30);
                    if (sample == 0)
                        path.BeginFigure(point, false, true);
                    else
                        path.LineTo(point, true, false);
                }
            }
            geometry.Freeze();
            byte alpha = (byte)((IsDark ? 90 : 100) + (IsDark ? 95 : 100) * (0.5 + 0.5 * Math.Sin(v)));
            var spectrum = new LinearGradientBrush { StartPoint = new Point(0, 0.2), EndPoint = new Point(1, 0.8) };
            spectrum.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(alpha, 84, 232, 219) : Color.FromArgb(alpha, 17, 129, 129), 0));
            spectrum.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(alpha, 94, 181, 247) : Color.FromArgb(alpha, 41, 108, 173), 0.43));
            spectrum.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(alpha, 130, 120, 239) : Color.FromArgb(alpha, 111, 90, 170), 0.76));
            spectrum.GradientStops.Add(new GradientStop(IsDark ? Color.FromArgb(alpha, 211, 154, 228) : Color.FromArgb(alpha, 165, 94, 142), 1));
            spectrum.Freeze();
            var pen = new Pen(spectrum, ring % 12 == 0 ? 1.25 : 0.72);
            pen.Freeze();
            dc.DrawGeometry(null, pen, geometry);
        }
    }

    private void DrawOrbit(DrawingContext dc)
    {
        Point center = new(480, 216);
        dc.DrawEllipse(null, Pen(IsDark ? "#23364D" : "#A8B9C8", 0.8), center, 189, 160);
        dc.DrawEllipse(null, Pen(IsDark ? "#18273C" : "#CDD7E2", 0.6), center, 199, 168);
        Pen majorTick = Pen(IsDark ? "#526680" : "#6C87A0", 0.65);
        Pen minorTick = Pen(IsDark ? "#263951" : "#B7C7D4", 0.65);
        for (int i = 0; i < 72; i++)
        {
            double angle = i * Math.PI / 36;
            double tick = i % 6 == 0 ? 5 : 2;
            dc.DrawLine(i % 6 == 0 ? majorTick : minorTick,
                new Point(center.X + 193 * Math.Cos(angle), center.Y + 164 * Math.Sin(angle)),
                new Point(center.X + (193 + tick) * Math.Cos(angle), center.Y + (164 + tick) * Math.Sin(angle)));
        }
    }

    private void DrawOrbitMarkers(DrawingContext dc)
    {
        dc.DrawEllipse(Brush(IsDark ? "#ADEBF2" : "#347E90"), null, new Point(669, 216), 2, 2);
        DrawGlow(dc, new Point(669, 216), 12, 12, IsDark ? "#71CEDF" : "#4193AB", IsDark ? 0.4 : 0.22);
        dc.DrawEllipse(Brush(IsDark ? "#9D98D5" : "#736BA9"), null, new Point(291, 216), 1.5, 1.5);
    }

    private static void DrawGlow(DrawingContext dc, Point center, double radiusX, double radiusY, string color, double opacity)
    {
        Color inner = (Color)ColorConverter.ConvertFromString(color);
        var brush = new RadialGradientBrush(inner, Color.FromArgb(0, inner.R, inner.G, inner.B)) { Opacity = opacity };
        brush.Freeze();
        dc.DrawEllipse(brush, null, center, radiusX, radiusY);
    }

    private static SolidColorBrush Brush(string color)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(color));
        brush.Freeze();
        return brush;
    }

    private static Pen Pen(string color, double thickness)
    {
        var pen = new Pen(Brush(color), thickness);
        pen.Freeze();
        return pen;
    }

    private sealed class DrawingLayer : FrameworkElement
    {
        private readonly DrawingGroup _drawing = new();

        public DrawingLayer(Action<DrawingContext> draw)
        {
            using (DrawingContext context = _drawing.Open())
                draw(context);
            _drawing.Freeze();
            IsHitTestVisible = false;
        }

        protected override void OnRender(DrawingContext drawingContext) => drawingContext.DrawDrawing(_drawing);
    }
}
