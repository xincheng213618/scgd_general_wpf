using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Themes;

/// <summary>A tab joined to its pane with concave shoulders and rounded outer corners.</summary>
public sealed class DockingTabBorder : Border
{
    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(Dock), typeof(DockingTabBorder),
        new FrameworkPropertyMetadata(Dock.Top, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsSelectedProperty = DependencyProperty.Register(
        nameof(IsSelected), typeof(bool), typeof(DockingTabBorder),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender,
            (sender, _) => ((DockingTabBorder)sender).UpdateLayoutTracking()));

    private bool trackingLayout;
    private bool joinsStart;
    private bool joinsEnd;

    public DockingTabBorder()
    {
        Loaded += (_, _) => UpdateLayoutTracking();
        Unloaded += (_, _) => UpdateLayoutTracking();
    }

    public Dock Placement { get => (Dock)GetValue(PlacementProperty); set => SetValue(PlacementProperty, value); }
    public bool IsSelected { get => (bool)GetValue(IsSelectedProperty); set => SetValue(IsSelectedProperty, value); }

    protected override HitTestResult HitTestCore(PointHitTestParameters hitTestParameters)
        => new Rect(RenderSize).Contains(hitTestParameters.HitPoint) ? base.HitTestCore(hitTestParameters) : null;

    private void UpdateLayoutTracking()
    {
        bool track = IsLoaded && IsSelected && TemplatedParent is TabItem;
        if (trackingLayout == track) return;
        trackingLayout = track;
        if (track) LayoutUpdated += UpdateEdgeJoins;
        else LayoutUpdated -= UpdateEdgeJoins;
    }

    private void UpdateEdgeJoins(object? sender, EventArgs args)
    {
        if (TemplatedParent is not TabItem tab || VisualTreeHelper.GetParent(tab) is not Panel panel) return;
        Point origin = tab.TranslatePoint(new Point(), panel);
        bool start = origin.X <= 0.01;
        bool end = origin.X + tab.ActualWidth >= panel.ActualWidth - 0.01;
        if (joinsStart == start && joinsEnd == end) return;
        joinsStart = start;
        joinsEnd = end;
        // Closing/reordering a neighbour can move a tab without resizing it. Only an
        // edge transition needs new paint; ordinary layout passes do not redraw it.
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawing)
    {
        if (!IsSelected)
        {
            // The pane's joining line is behind the tabs. An opaque hover fill must
            // leave it visible without shrinking the native tab's layout/hit area.
            double scale = VisualTreeHelper.GetDpi(this).DpiScaleY;
            double joinThickness = UseLayoutRounding ? Math.Round(scale) / scale : 1;
            double paintHeight = Math.Max(0, RenderSize.Height - joinThickness);
            drawing.PushClip(new RectangleGeometry(new Rect(0, Placement == Dock.Bottom ? joinThickness : 0, RenderSize.Width, paintHeight)));
            base.OnRender(drawing);
            drawing.Pop();
            return;
        }

        double width = RenderSize.Width;
        double height = RenderSize.Height;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        double thickness = UseLayoutRounding ? Math.Round(BorderThickness.Left * dpi.DpiScaleX) / dpi.DpiScaleX : BorderThickness.Left;
        double half = thickness / 2;
        double radius = Math.Max(0, Math.Min(CornerRadius.TopLeft, Math.Min(width / 2 - half, height / 2 - half)));
        if (width <= thickness || height <= thickness) return;
        double leftShoulder = joinsStart ? 0 : radius;
        double rightShoulder = joinsEnd ? 0 : radius;

        // Coordinates describe a bottom tab; top document tabs mirror the same contour.
        // Shoulders extend into the neighbouring tab's empty padding, not its hit area.
        Point P(double x, double y) => new(x, Placement == Dock.Bottom ? y : height - y);
        var contour = new StreamGeometry();
        using (StreamGeometryContext path = contour.Open())
        {
            // Fill implicitly closes across the pane; only the curved outside is stroked.
            path.BeginFigure(P(-leftShoulder + half, -half), true, false);
            path.LineTo(P(-leftShoulder + half, half), false, false);
            path.BezierTo(P(half - leftShoulder * 0.448, half), P(half, half + leftShoulder * 0.448), P(half, half + leftShoulder), true, true);
            path.LineTo(P(half, height - half - radius), true, false);
            path.BezierTo(P(half, height - half - radius * 0.448), P(half + radius * 0.448, height - half), P(half + radius, height - half), true, true);
            path.LineTo(P(width - half - radius, height - half), true, false);
            path.BezierTo(P(width - half - radius * 0.448, height - half), P(width - half, height - half - radius * 0.448), P(width - half, height - half - radius), true, true);
            path.LineTo(P(width - half, half + rightShoulder), true, false);
            path.BezierTo(P(width - half, half + rightShoulder * 0.448), P(width - half + rightShoulder * 0.448, half), P(width - half + rightShoulder, half), true, true);
            path.LineTo(P(width - half + rightShoulder, -half), false, false);
        }
        contour.Freeze();
        drawing.DrawGeometry(Background, new Pen(BorderBrush, thickness), contour);
    }
}
