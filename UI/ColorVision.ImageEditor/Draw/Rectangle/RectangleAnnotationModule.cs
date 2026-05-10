using System;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal sealed class RectangleAnnotationModule : IAnnotationModule
    {
        public bool CanExport(DrawingVisualBase visual)
        {
            return visual?.BaseAttribute is RectangleProperties;
        }

        public AnnotationItem ToItem(DrawingVisualBase visual)
        {
            return ToItem(visual.BaseAttribute);
        }

        public bool CanExport(BaseProperties properties)
        {
            return properties is RectangleProperties;
        }

        public bool CanImport(AnnotationItem item)
        {
            return item is RectangleAnnotationItem;
        }

        public AnnotationItem ToItem(BaseProperties properties)
        {
            return properties switch
            {
                RectangleTextProperties rectangleTextProperties => ToRectangleItem(rectangleTextProperties),
                RectangleProperties rectangleProperties => ToRectangleItem(rectangleProperties),
                _ => throw new NotSupportedException($"Unsupported rectangle properties type: {properties.GetType().FullName}"),
            };
        }

        public BaseProperties ToProperties(AnnotationItem item)
        {
            if (item is not RectangleAnnotationItem rectangleItem)
                throw new NotSupportedException($"Unsupported rectangle annotation type: {item.GetType().FullName}");

            return rectangleItem.TextStyle != null ? ToRectangleTextProperties(rectangleItem) : ToRectangleProperties(rectangleItem);
        }

        public DrawingVisualBase ToVisual(AnnotationItem item)
        {
            if (item is not RectangleAnnotationItem rectangleItem)
                throw new NotSupportedException($"Unsupported rectangle annotation type: {item.GetType().FullName}");

            return rectangleItem.TextStyle != null
                ? new DVRectangleText((RectangleTextProperties)ToProperties(rectangleItem))
                : new DVRectangle((RectangleProperties)ToProperties(rectangleItem));
        }

        private static RectangleAnnotationItem ToRectangleItem(RectangleProperties properties)
        {
            RectangleAnnotationItem item = new()
            {
                Rect = AnnotationMappingHelper.ToAnnotationRect(properties.Rect),
                Style = AnnotationMappingHelper.CreateShapeStyle(properties.Brush, properties.Pen),
            };
            AnnotationMappingHelper.CopyBaseProperties(properties, item);
            return item;
        }

        private static RectangleAnnotationItem ToRectangleItem(RectangleTextProperties properties)
        {
            RectangleAnnotationItem item = ToRectangleItem((RectangleProperties)properties);
            item.TextStyle = AnnotationMappingHelper.CreateTextStyle(properties.TextAttribute, properties.IsShowText);
            item.TextPosition = AnnotationMappingHelper.ToAnnotationRectangleTextPosition(properties.Position);
            return item;
        }

        private static RectangleProperties ToRectangleProperties(RectangleAnnotationItem item)
        {
            RectangleProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(item, properties);
            properties.Rect = AnnotationMappingHelper.ToRect(item.Rect);
            AnnotationMappingHelper.ApplyShapeStyle(item.Style, properties.Brush, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            return properties;
        }

        private static RectangleTextProperties ToRectangleTextProperties(RectangleAnnotationItem item)
        {
            RectangleTextProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(item, properties);
            properties.Rect = AnnotationMappingHelper.ToRect(item.Rect);
            properties.Position = AnnotationMappingHelper.ToRectangleTextPosition(item.TextPosition);
            AnnotationMappingHelper.ApplyShapeStyle(item.Style, properties.Brush, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            AnnotationMappingHelper.ApplyTextStyle(item.TextStyle, properties.TextAttribute);
            properties.IsShowText = item.TextStyle?.Visible ?? properties.IsShowText;
            return properties;
        }
    }
}