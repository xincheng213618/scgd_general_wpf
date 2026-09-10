using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw;

public class RegionProperties : BaseProperties
{
    [Category("几何"), DisplayName("旋转角度（°）")]
    [Description("围绕图形中心顺时针旋转，仅用于图像标注及图像 POI 测量。")]
    public double Rotation
    {
        get => rotation;
        set
        {
            if (!double.IsFinite(value)) return;
            double next = value % 360;
            if (rotation == next) return;
            rotation = next;
            InvalidateMeasurementMessage();
            OnPropertyChanged();
        }
    }
    private double rotation;
}

public static class RegionGeometry
{
    public static Rect LocalBounds(RegionProperties properties) => properties switch
    {
        CircleProperties circle => ShapeGeometry.TryGetEllipseBounds(circle.Center, circle.Radius, circle.RadiusY, out Rect bounds) ? bounds : Rect.Empty,
        RectangleProperties rectangle => rectangle.Rect.IsEmpty || ShapeGeometry.IsFinite(rectangle.Rect) ? rectangle.Rect : Rect.Empty,
        PolygonProperties polygon => PointCollectionGeometry.GetBounds(polygon.Points),
        _ => Rect.Empty
    };

    public static RotateTransform Transform(RegionProperties properties)
    {
        Rect bounds = LocalBounds(properties);
        if (bounds.IsEmpty) return new RotateTransform();
        return new(properties.Rotation, bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
    }

    public static Rect Bounds(RegionProperties properties)
    {
        Rect local = LocalBounds(properties);
        if (local.IsEmpty || properties.Rotation == 0) return local;
        if (properties is CircleProperties circle && circle.Radius > 0 && circle.RadiusY > 0)
            return ClosedPixelRegion.Ellipse(circle.Center, circle.Radius, circle.RadiusY, properties.Rotation).Bounds;
        return Transform(properties).TransformBounds(local);
    }

    // Rotated resize preserves aspect ratio; independent axis sizes remain editable in properties.
    public static Rect ResizeLocalBounds(RegionProperties properties, Rect target)
    {
        if (properties.Rotation == 0) return target;
        Rect local = LocalBounds(properties), world = Bounds(properties);
        if (local.IsEmpty || world.Width <= 0 || world.Height <= 0) return target;
        double scale = Math.Min(target.Width / world.Width, target.Height / world.Height);
        double width = local.Width * scale, height = local.Height * scale;
        return new(target.X + (target.Width - width) / 2, target.Y + (target.Height - height) / 2, width, height);
    }
}
