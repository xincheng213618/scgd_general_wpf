using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ColorVision.Themes;

/// <summary>Clips the template-owned content host to the inside of a rounded pane border.</summary>
public sealed class DockingSurfaceBorder : Border
{
    protected override Size ArrangeOverride(Size finalSize)
    {
        Size result = base.ArrangeOverride(finalSize);
        if (Child == null) return result;

        // Border rounds its own paint, not its child. Clip this template's presenter/grid,
        // leaving the hosted editor and any clip it already uses untouched.
        Thickness border = BorderThickness;
        DpiScale dpi = VisualTreeHelper.GetDpi(this);
        if (UseLayoutRounding)
            border = new Thickness(Round(border.Left, dpi.DpiScaleX), Round(border.Top, dpi.DpiScaleY),
                Round(border.Right, dpi.DpiScaleX), Round(border.Bottom, dpi.DpiScaleY));
        Thickness padding = Padding;
        Vector offset = VisualTreeHelper.GetOffset(Child);
        var bounds = new Rect(border.Left + padding.Left - offset.X, border.Top + padding.Top - offset.Y,
            Math.Max(0, finalSize.Width - border.Left - border.Right - padding.Left - padding.Right),
            Math.Max(0, finalSize.Height - border.Top - border.Bottom - padding.Top - padding.Bottom));
        CornerRadius radius = CornerRadius;
        Size topLeft = InnerRadius(radius.TopLeft, border.Left, border.Top, padding.Left, padding.Top, bounds);
        Size topRight = InnerRadius(radius.TopRight, border.Right, border.Top, padding.Right, padding.Top, bounds);
        Size bottomRight = InnerRadius(radius.BottomRight, border.Right, border.Bottom, padding.Right, padding.Bottom, bounds);
        Size bottomLeft = InnerRadius(radius.BottomLeft, border.Left, border.Bottom, padding.Left, padding.Bottom, bounds);
        var clip = new StreamGeometry();
        using (StreamGeometryContext path = clip.Open())
        {
            path.BeginFigure(new Point(bounds.Left + topLeft.Width, bounds.Top), true, true);
            path.LineTo(new Point(bounds.Right - topRight.Width, bounds.Top), true, false);
            Arc(path, new Point(bounds.Right, bounds.Top + topRight.Height), topRight);
            path.LineTo(new Point(bounds.Right, bounds.Bottom - bottomRight.Height), true, false);
            Arc(path, new Point(bounds.Right - bottomRight.Width, bounds.Bottom), bottomRight);
            path.LineTo(new Point(bounds.Left + bottomLeft.Width, bounds.Bottom), true, false);
            Arc(path, new Point(bounds.Left, bounds.Bottom - bottomLeft.Height), bottomLeft);
            path.LineTo(new Point(bounds.Left, bounds.Top + topLeft.Height), true, false);
            Arc(path, new Point(bounds.Left + topLeft.Width, bounds.Top), topLeft);
        }
        clip.Freeze();
        Child.SetCurrentValue(ClipProperty, clip);
        return result;
    }

    private static double Round(double value, double scale) => Math.Round(value * scale) / scale;

    private static Size InnerRadius(double radius, double horizontalBorder, double verticalBorder,
        double horizontalPadding, double verticalPadding, Rect bounds)
        => new(Math.Min(bounds.Width / 2, Math.Max(0, radius - horizontalBorder / 2 - horizontalPadding)),
            Math.Min(bounds.Height / 2, Math.Max(0, radius - verticalBorder / 2 - verticalPadding)));

    private static void Arc(StreamGeometryContext path, Point end, Size radius)
    {
        if (radius.Width == 0 || radius.Height == 0) path.LineTo(end, true, false);
        else path.ArcTo(end, radius, 0, false, SweepDirection.Clockwise, true, true);
    }
}
