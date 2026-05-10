using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal static class AnnotationMappingHelper
    {
        internal static void CopyBaseProperties(BaseProperties properties, AnnotationItem item)
        {
            item.Id = properties.Id;
            item.Name = properties.Name;
            item.Msg = properties.Msg;
        }

        internal static void ApplyBaseProperties(AnnotationItem item, BaseProperties properties)
        {
            properties.Id = item.Id;
            properties.Name = item.Name ?? string.Empty;
            properties.Msg = item.Msg;
        }

        internal static AnnotationShapeStyle CreateShapeStyle(Brush fillBrush, Pen? pen)
        {
            return new AnnotationShapeStyle
            {
                FillColor = TextStyleSerialization.SerializeBrush(fillBrush),
                StrokeColor = TextStyleSerialization.SerializeBrush(pen?.Brush ?? fillBrush),
                StrokeThickness = pen?.Thickness ?? 0,
            };
        }

        internal static void ApplyShapeStyle(AnnotationShapeStyle? style, Brush fillFallback, Pen? penFallback, Action<Brush> setFill, Action<Pen> setPen)
        {
            if (style == null)
                return;

            Brush fillBrush = TextStyleSerialization.DeserializeBrush(style.FillColor ?? string.Empty, fillFallback);
            Brush strokeBrush = TextStyleSerialization.DeserializeBrush(style.StrokeColor ?? string.Empty, penFallback?.Brush ?? fillBrush);
            double strokeThickness = style.StrokeThickness > 0 ? style.StrokeThickness : penFallback?.Thickness ?? 1;

            setFill(fillBrush);
            setPen(new Pen(strokeBrush, strokeThickness));
        }

        internal static AnnotationTextStyle CreateTextStyle(TextAttribute textAttribute, bool visible, Brush? background = null)
        {
            return new AnnotationTextStyle
            {
                Text = textAttribute.Text,
                Visible = visible,
                FontSize = textAttribute.FontSize,
                ForegroundColor = TextStyleSerialization.SerializeBrush(textAttribute.Brush),
                BackgroundColor = background == null ? null : TextStyleSerialization.SerializeBrush(background),
                FontFamily = TextStyleSerialization.SerializeFontFamily(textAttribute.FontFamily),
                FontStyle = TextStyleSerialization.SerializeFontStyle(textAttribute.FontStyle),
                FontWeight = TextStyleSerialization.SerializeFontWeight(textAttribute.FontWeight),
                FontStretch = TextStyleSerialization.SerializeFontStretch(textAttribute.FontStretch),
                FlowDirection = TextStyleSerialization.SerializeFlowDirection(textAttribute.FlowDirection),
            };
        }

        internal static void ApplyTextStyle(AnnotationTextStyle? style, TextAttribute textAttribute)
        {
            if (style == null)
                return;

            textAttribute.Text = style.Text ?? string.Empty;
            if (style.FontSize > 0)
                textAttribute.FontSize = style.FontSize;
            textAttribute.Brush = TextStyleSerialization.DeserializeBrush(style.ForegroundColor ?? string.Empty, textAttribute.Brush);
            textAttribute.FontFamily = TextStyleSerialization.DeserializeFontFamily(style.FontFamily ?? string.Empty, textAttribute.FontFamily);
            textAttribute.FontStyle = TextStyleSerialization.DeserializeFontStyle(style.FontStyle ?? string.Empty, textAttribute.FontStyle);
            textAttribute.FontWeight = TextStyleSerialization.DeserializeFontWeight(style.FontWeight, textAttribute.FontWeight);
            textAttribute.FontStretch = TextStyleSerialization.DeserializeFontStretch(style.FontStretch ?? string.Empty, textAttribute.FontStretch);
            textAttribute.FlowDirection = TextStyleSerialization.DeserializeFlowDirection(style.FlowDirection ?? string.Empty, textAttribute.FlowDirection);
        }

        internal static AnnotationPoint ToAnnotationPoint(Point point)
        {
            return new AnnotationPoint { X = point.X, Y = point.Y };
        }

        internal static Point ToPoint(AnnotationPoint point)
        {
            return new Point(point?.X ?? 0, point?.Y ?? 0);
        }

        internal static AnnotationRect ToAnnotationRect(Rect rect)
        {
            return new AnnotationRect { X = rect.X, Y = rect.Y, Width = rect.Width, Height = rect.Height };
        }

        internal static List<AnnotationPoint> ToAnnotationPoints(IReadOnlyList<Point>? points)
        {
            List<AnnotationPoint> result = new();
            if (points == null)
                return result;

            foreach (Point point in points)
            {
                result.Add(ToAnnotationPoint(point));
            }

            return result;
        }

        internal static List<Point> ToPoints(IReadOnlyList<AnnotationPoint>? points)
        {
            List<Point> result = new();
            if (points == null)
                return result;

            foreach (AnnotationPoint point in points)
            {
                result.Add(ToPoint(point));
            }

            return result;
        }

        internal static Rect ToRect(AnnotationRect rect)
        {
            return new Rect(rect?.X ?? 0, rect?.Y ?? 0, rect?.Width ?? 0, rect?.Height ?? 0);
        }

        internal static AnnotationRectangleTextPosition ToAnnotationRectangleTextPosition(RectangleTextPosition position)
        {
            return position switch
            {
                RectangleTextPosition.Top => AnnotationRectangleTextPosition.Top,
                RectangleTextPosition.Bottom => AnnotationRectangleTextPosition.Bottom,
                RectangleTextPosition.Left => AnnotationRectangleTextPosition.Left,
                RectangleTextPosition.Right => AnnotationRectangleTextPosition.Right,
                _ => AnnotationRectangleTextPosition.Center,
            };
        }

        internal static RectangleTextPosition ToRectangleTextPosition(AnnotationRectangleTextPosition position)
        {
            return position switch
            {
                AnnotationRectangleTextPosition.Top => RectangleTextPosition.Top,
                AnnotationRectangleTextPosition.Bottom => RectangleTextPosition.Bottom,
                AnnotationRectangleTextPosition.Left => RectangleTextPosition.Left,
                AnnotationRectangleTextPosition.Right => RectangleTextPosition.Right,
                _ => RectangleTextPosition.Center,
            };
        }
    }
}