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
        private static readonly PoiOverlayStyle DefaultStyle = new();

        public static DrawingVisualBase? CreateVisual(PoiPoint point, string? message = null, PoiOverlayStyle? style = null)
        {
            style ??= DefaultStyle;

            DrawingVisualBase? visual = CreateVisualCore(point, message, style);
            if (visual == null)
                return null;

            visual.BaseAttribute.Tag = point;
            visual.Render();
            return visual;
        }

        private static DrawingVisualBase? CreateVisualCore(PoiPoint point, string? message, PoiOverlayStyle style)
        {
            ArgumentNullException.ThrowIfNull(point);
            return point.PointType switch
            {
                PoiShape.Circle => CreateCircle(point, message, style),
                PoiShape.Rect => CreateRectangle(point, message, style, isLeftTop: false),
                PoiShape.LeftTopRect => CreateRectangle(point, message, style, isLeftTop: true),
                PoiShape.Point or PoiShape.LegacySolidPoint => CreatePoint(point, style),
                _ => null
            };
        }

        public static bool Add(ImageView imageView, PoiPoint point, string? message = null, PoiOverlayStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            DrawingVisualBase? visual = CreateVisualCore(point, message, style ?? DefaultStyle);
            if (visual == null) return false;

            PrepareVisual(visual, point, CreateScaleContext(imageView));
            imageView.ImageShow.AddVisual(visual);
            return true;
        }

        public static int AddRange(ImageView imageView, IEnumerable<PoiPoint> points, Func<PoiPoint, string?>? messageFactory = null, PoiOverlayStyle? style = null)
        {
            ArgumentNullException.ThrowIfNull(imageView);
            ArgumentNullException.ThrowIfNull(points);

            PoiOverlayStyle effectiveStyle = style ?? DefaultStyle;
            List<Visual> visuals = new();
            DrawingVisualScaleContext scaleContext = CreateScaleContext(imageView);
            foreach (PoiPoint point in points)
            {
                DrawingVisualBase? visual = CreateVisualCore(point, messageFactory?.Invoke(point), effectiveStyle);
                if (visual == null)
                    continue;

                PrepareVisual(visual, point, scaleContext);
                visuals.Add(visual);
            }
            return imageView.ImageShow.AddVisuals(visuals);
        }

        private static DrawingVisualScaleContext CreateScaleContext(ImageView imageView)
        {
            return new DrawingVisualScaleContext(
                imageView.ImageShow.IsLayoutUpdated,
                imageView.ImageShow.Scale,
                imageView.ImageShow.TextFontSizeOverride);
        }

        private static void PrepareVisual(DrawingVisualBase visual, PoiPoint point, DrawingVisualScaleContext scaleContext)
        {
            visual.BaseAttribute.Tag = point;
            if (visual is ILayoutScaleDrawingVisual scalableVisual)
                scalableVisual.ApplyLayoutScale(scaleContext);
            if (visual.Drawing == null)
                visual.Render();
        }

        private static DVCircleText CreateCircle(PoiPoint point, string? message, PoiOverlayStyle style)
        {
            CircleTextProperties properties = new()
            {
                Center = new Point(point.PixelX, point.PixelY),
                Radius = point.Radius,
                Brush = style.Fill,
                Pen = new Pen(style.Stroke, style.StrokeThickness),
                Id = point.Id,
                Text = point.Name,
                Msg = message,
                IsShowText = style.ShowText,
            };
            DVCircleText circle = new(properties);
            circle.TextAttribute.FontSize = style.FontSize;
            return circle;
        }

        private static DVRectangleText CreateRectangle(PoiPoint point, string? message, PoiOverlayStyle style, bool isLeftTop)
        {
            double left = isLeftTop ? point.PixelX : point.PixelX - point.Width / 2;
            double top = isLeftTop ? point.PixelY : point.PixelY - point.Height / 2;
            RectangleTextProperties properties = new()
            {
                Rect = new Rect(left, top, point.Width, point.Height),
                Brush = style.Fill,
                Pen = new Pen(style.Stroke, style.StrokeThickness),
                Id = point.Id,
                Text = point.Name,
                Msg = message,
                IsShowText = style.ShowText,
            };
            DVRectangleText rectangle = new(properties);
            rectangle.TextAttribute.FontSize = style.FontSize;
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
