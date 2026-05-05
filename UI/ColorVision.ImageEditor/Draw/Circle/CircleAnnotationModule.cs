using System;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal sealed class CircleAnnotationModule : IAnnotationModule
    {
        public bool CanExport(DrawingVisualBase visual)
        {
            return visual?.BaseAttribute is CircleProperties;
        }

        public AnnotationItem ToItem(DrawingVisualBase visual)
        {
            return ToItem(visual.BaseAttribute);
        }

        public bool CanExport(BaseProperties properties)
        {
            return properties is CircleProperties;
        }

        public bool CanImport(AnnotationItem item)
        {
            return item is CircleAnnotationItem;
        }

        public AnnotationItem ToItem(BaseProperties properties)
        {
            return properties switch
            {
                CircleTextProperties circleTextProperties => ToCircleItem(circleTextProperties),
                CircleProperties circleProperties => ToCircleItem(circleProperties),
                _ => throw new NotSupportedException($"Unsupported circle properties type: {properties.GetType().FullName}"),
            };
        }

        public BaseProperties ToProperties(AnnotationItem item)
        {
            if (item is not CircleAnnotationItem circleItem)
                throw new NotSupportedException($"Unsupported circle annotation type: {item.GetType().FullName}");

            return circleItem.TextStyle != null ? ToCircleTextProperties(circleItem) : ToCircleProperties(circleItem);
        }

        public DrawingVisualBase ToVisual(AnnotationItem item)
        {
            if (item is not CircleAnnotationItem circleItem)
                throw new NotSupportedException($"Unsupported circle annotation type: {item.GetType().FullName}");

            return circleItem.TextStyle != null
                ? new DVCircleText((CircleTextProperties)ToProperties(circleItem))
                : new DVCircle((CircleProperties)ToProperties(circleItem));
        }

        private static CircleAnnotationItem ToCircleItem(CircleProperties properties)
        {
            CircleAnnotationItem item = new()
            {
                Center = AnnotationMappingHelper.ToAnnotationPoint(properties.Center),
                RadiusX = properties.Radius,
                RadiusY = properties.RadiusY,
                Style = AnnotationMappingHelper.CreateShapeStyle(properties.Brush, properties.Pen),
            };
            AnnotationMappingHelper.CopyBaseProperties(properties, item);
            return item;
        }

        private static CircleAnnotationItem ToCircleItem(CircleTextProperties properties)
        {
            CircleAnnotationItem item = ToCircleItem((CircleProperties)properties);
            item.TextStyle = AnnotationMappingHelper.CreateTextStyle(properties.TextAttribute, properties.IsShowText);
            return item;
        }

        private static CircleProperties ToCircleProperties(CircleAnnotationItem item)
        {
            CircleProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(item, properties);
            properties.Center = AnnotationMappingHelper.ToPoint(item.Center);
            properties.Radius = item.RadiusX;
            properties.RadiusY = item.RadiusY;
            AnnotationMappingHelper.ApplyShapeStyle(item.Style, properties.Brush, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            return properties;
        }

        private static CircleTextProperties ToCircleTextProperties(CircleAnnotationItem item)
        {
            CircleTextProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(item, properties);
            properties.Center = AnnotationMappingHelper.ToPoint(item.Center);
            properties.Radius = item.RadiusX;
            properties.RadiusY = item.RadiusY;
            AnnotationMappingHelper.ApplyShapeStyle(item.Style, properties.Brush, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            AnnotationMappingHelper.ApplyTextStyle(item.TextStyle, properties.TextAttribute);
            properties.IsShowText = item.TextStyle?.Visible ?? properties.IsShowText;
            return properties;
        }
    }
}