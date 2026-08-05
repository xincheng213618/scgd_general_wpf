using ColorVision.Engine.Templates.POI;
using ColorVision.ImageEditor;
using ColorVision.ImageEditor.Draw;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Engine
{
    public sealed class PoiOverlayStyle
    {
        public Brush Stroke { get; init; } = Brushes.Red;
        public Brush Fill { get; init; } = Brushes.Transparent;
        public double StrokeThickness { get; init; } = 1;
        public double FontSize { get; init; } = 10;
        public double PointRadius { get; init; } = 10;
        public bool ShowText { get; init; } = true;
    }

    /// <summary>
    /// The single POI-to-ImageEditor rendering path used by result handlers and projects.
    /// </summary>
    public static class PoiOverlayRenderer
    {
        public static DrawingVisualBase? CreateVisual(PoiPoint point, string? message = null, PoiOverlayStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(point);
            style ??= new PoiOverlayStyle();

            DrawingVisualBase? visual = point.PointType switch
            {
                PoiShape.Circle => CreateCircle(point, message, style),
                PoiShape.Rect => CreateRectangle(point, message, style, isLeftTop: false),
                PoiShape.LeftTopRect => CreateRectangle(point, message, style, isLeftTop: true),
                PoiShape.Point or PoiShape.LegacySolidPoint => CreatePoint(point, style),
                _ => null
            };

            if (visual != null)
            {
                visual.BaseAttribute.Tag = point;
                visual.Render();
            }
            return visual;
        }

        public static bool Add(ImageView imageView, PoiPoint point, string? message = null, PoiOverlayStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            DrawingVisualBase? visual = CreateVisual(point, message, style);
            if (visual == null) return false;

            imageView.ImageShow.AddVisual(visual);
            return true;
        }

        public static int AddRange(ImageView imageView, IEnumerable<PoiPoint> points, Func<PoiPoint, string?>? messageFactory = null, PoiOverlayStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ArgumentNullException.ThrowIfNull(points);

            List<Visual> visuals = new();
            foreach (PoiPoint point in points)
            {
                DrawingVisualBase? visual = CreateVisual(point, messageFactory?.Invoke(point), style);
                if (visual != null) visuals.Add(visual);
            }
            return imageView.ImageShow.AddVisuals(visuals);
        }

        private static DVCircleText CreateCircle(PoiPoint point, string? message, PoiOverlayStyle style)
        {
            DVCircleText circle = new();
            circle.Attribute.Center = new Point(point.PixelX, point.PixelY);
            circle.Attribute.Radius = point.Radius;
            circle.Attribute.Brush = style.Fill;
            circle.Attribute.Pen = new Pen(style.Stroke, style.StrokeThickness);
            circle.Attribute.Id = point.Id;
            circle.Attribute.Text = point.Name;
            circle.Attribute.Msg = message;
            circle.Attribute.FontSize = style.FontSize;
            circle.Attribute.IsShowText = style.ShowText;
            return circle;
        }

        private static DVRectangleText CreateRectangle(PoiPoint point, string? message, PoiOverlayStyle style, bool isLeftTop)
        {
            double left = isLeftTop ? point.PixelX : point.PixelX - point.Width / 2;
            double top = isLeftTop ? point.PixelY : point.PixelY - point.Height / 2;
            DVRectangleText rectangle = new();
            rectangle.Attribute.Rect = new Rect(left, top, point.Width, point.Height);
            rectangle.Attribute.Brush = style.Fill;
            rectangle.Attribute.Pen = new Pen(style.Stroke, style.StrokeThickness);
            rectangle.Attribute.Id = point.Id;
            rectangle.Attribute.Text = point.Name;
            rectangle.Attribute.Msg = message;
            rectangle.Attribute.FontSize = style.FontSize;
            rectangle.Attribute.IsShowText = style.ShowText;
            return rectangle;
        }

        private static DVCircle CreatePoint(PoiPoint point, PoiOverlayStyle style)
        {
            CircleProperties properties = new()
            {
                Center = new Point(point.PixelX, point.PixelY),
                Radius = style.PointRadius,
                Brush = style.Stroke,
                Pen = new Pen(style.Stroke, style.StrokeThickness),
                Id = point.Id,
                Tag = point
            };
            return new DVCircle(properties);
        }
    }
}
