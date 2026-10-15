using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace ColorVision.UI.Controls;

public enum CircularProgressState { Waiting, Running, Completed, Failed }

/// <summary>A progress ring. NaN represents unknown progress; running values remain below 100 until completion.</summary>
public sealed class CircularProgressIndicator : Control
{
    public static readonly DependencyProperty StateProperty = DependencyProperty.Register(nameof(State), typeof(CircularProgressState), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(CircularProgressState.Waiting, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged));
    public static readonly DependencyProperty ValueProperty = DependencyProperty.Register(nameof(Value), typeof(double), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(double.NaN, FrameworkPropertyMetadataOptions.AffectsRender, OnProgressChanged));
    public static readonly DependencyProperty StepNumberProperty = DependencyProperty.Register(nameof(StepNumber), typeof(int), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty TrackBrushProperty = DependencyProperty.Register(nameof(TrackBrush), typeof(Brush), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(Brushes.LightGray, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty ActiveBrushProperty = DependencyProperty.Register(nameof(ActiveBrush), typeof(Brush), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(Brushes.DodgerBlue, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty CompletedBrushProperty = DependencyProperty.Register(nameof(CompletedBrush), typeof(Brush), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(Brushes.SeaGreen, FrameworkPropertyMetadataOptions.AffectsRender));
    public static readonly DependencyProperty FailureBrushProperty = DependencyProperty.Register(nameof(FailureBrush), typeof(Brush), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(Brushes.IndianRed, FrameworkPropertyMetadataOptions.AffectsRender));
    private static readonly DependencyProperty RotationProperty = DependencyProperty.Register("Rotation", typeof(double), typeof(CircularProgressIndicator), new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public CircularProgressState State { get => (CircularProgressState)GetValue(StateProperty); set => SetValue(StateProperty, value); }
    public double Value { get => (double)GetValue(ValueProperty); set => SetValue(ValueProperty, value); }
    public int StepNumber { get => (int)GetValue(StepNumberProperty); set => SetValue(StepNumberProperty, value); }
    public Brush TrackBrush { get => (Brush)GetValue(TrackBrushProperty); set => SetValue(TrackBrushProperty, value); }
    public Brush ActiveBrush { get => (Brush)GetValue(ActiveBrushProperty); set => SetValue(ActiveBrushProperty, value); }
    public Brush CompletedBrush { get => (Brush)GetValue(CompletedBrushProperty); set => SetValue(CompletedBrushProperty, value); }
    public Brush FailureBrush { get => (Brush)GetValue(FailureBrushProperty); set => SetValue(FailureBrushProperty, value); }
    private bool _isAnimating;

    public CircularProgressIndicator()
    {
        Focusable = false;
        IsHitTestVisible = false;
        SetResourceReference(ForegroundProperty, "GlobalTextBrush");
        SetResourceReference(TrackBrushProperty, "BorderBrush");
        SetResourceReference(ActiveBrushProperty, "PrimaryBrush");
        SetResourceReference(CompletedBrushProperty, "SuccessBrush");
        SetResourceReference(FailureBrushProperty, "DangerBrush");
        Loaded += (_, _) => UpdateAnimation();
        Unloaded += (_, _) => StopAnimation();
        IsVisibleChanged += (_, _) => UpdateAnimation();
    }

    protected override Size MeasureOverride(Size constraint) => new(Math.Min(48, constraint.Width), Math.Min(48, constraint.Height));

    private static void OnProgressChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => ((CircularProgressIndicator)d).UpdateAnimation();

    private void UpdateAnimation()
    {
        bool animate = IsLoaded && IsVisible && State == CircularProgressState.Running && !double.IsFinite(Value) && SystemParameters.ClientAreaAnimation;
        if (animate == _isAnimating) return;
        if (!animate) { StopAnimation(); return; }
        _isAnimating = true;
        BeginAnimation(RotationProperty, new DoubleAnimation(0, 360, TimeSpan.FromSeconds(1.4)) { RepeatBehavior = RepeatBehavior.Forever });
    }

    private void StopAnimation()
    {
        _isAnimating = false;
        BeginAnimation(RotationProperty, null);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);
        double radius = Math.Max(0, Math.Min(ActualWidth, ActualHeight) / 2 - 2);
        if (radius <= 0) return;
        Point center = new(ActualWidth / 2, ActualHeight / 2);
        drawingContext.DrawEllipse(null, new Pen(TrackBrush, 2), center, radius, radius);
        Brush brush = State switch
        {
            CircularProgressState.Completed => CompletedBrush,
            CircularProgressState.Failed => FailureBrush,
            CircularProgressState.Running => ActiveBrush,
            _ => Foreground,
        };
        bool known = double.IsFinite(Value);
        double percentage = State == CircularProgressState.Completed ? 100 : known ? Math.Clamp(Value, 0, 99) : 0;
        var pen = new Pen(brush, 2) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (State is CircularProgressState.Completed or CircularProgressState.Failed)
            drawingContext.DrawEllipse(null, pen, center, radius, radius);
        else if (State != CircularProgressState.Waiting)
        {
            double start = -90 + (!known && State == CircularProgressState.Running ? (double)GetValue(RotationProperty) : 0);
            double sweep = known ? percentage * 3.6 : 85;
            if (sweep > 0) DrawArc(drawingContext, pen, center, radius, start, sweep);
        }
        string text = State == CircularProgressState.Running
            ? Math.Floor(percentage).ToString(CultureInfo.CurrentCulture)
            : StepNumber.ToString(CultureInfo.CurrentCulture);
        var typeface = new Typeface(FontFamily, FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var formatted = new FormattedText(text, CultureInfo.CurrentCulture, FlowDirection, typeface, text.Length > 2 ? 9 : 11, Foreground, VisualTreeHelper.GetDpi(this).PixelsPerDip);
        drawingContext.DrawText(formatted, new Point(center.X - formatted.Width / 2, center.Y - formatted.Height / 2));
    }

    private static void DrawArc(DrawingContext context, Pen pen, Point center, double radius, double start, double sweep)
    {
        static Point Position(Point center, double radius, double degrees)
        {
            double angle = degrees * Math.PI / 180;
            return new Point(center.X + radius * Math.Cos(angle), center.Y + radius * Math.Sin(angle));
        }
        var geometry = new StreamGeometry();
        using (StreamGeometryContext builder = geometry.Open())
        {
            builder.BeginFigure(Position(center, radius, start), false, false);
            builder.ArcTo(Position(center, radius, start + sweep), new Size(radius, radius), 0, sweep > 180, SweepDirection.Clockwise, true, false);
        }
        geometry.Freeze();
        context.DrawGeometry(null, pen, geometry);
    }
}
