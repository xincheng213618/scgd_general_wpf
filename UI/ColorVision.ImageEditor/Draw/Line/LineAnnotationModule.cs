using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal sealed class LineAnnotationModule : IAnnotationModule
    {
        public bool CanExport(DrawingVisualBase visual)
        {
            return visual?.BaseAttribute is LineProperties;
        }

        public AnnotationItem ToItem(DrawingVisualBase visual)
        {
            return ToItem(visual.BaseAttribute);
        }

        public bool CanExport(BaseProperties properties)
        {
            return properties is LineProperties;
        }

        public bool CanImport(AnnotationItem item)
        {
            return item is LineAnnotationItem;
        }

        public AnnotationItem ToItem(BaseProperties properties)
        {
            if (properties is not LineProperties lineProperties)
                throw new NotSupportedException($"Unsupported line properties type: {properties.GetType().FullName}");

            LineAnnotationItem item = new()
            {
                Points = AnnotationMappingHelper.ToAnnotationPoints(lineProperties.Points),
                Style = AnnotationMappingHelper.CreateShapeStyle(lineProperties.Brush ?? lineProperties.Pen?.Brush ?? Brushes.Transparent, lineProperties.Pen),
            };
            AnnotationMappingHelper.CopyBaseProperties(lineProperties, item);
            return item;
        }

        public BaseProperties ToProperties(AnnotationItem item)
        {
            if (item is not LineAnnotationItem lineItem)
                throw new NotSupportedException($"Unsupported line annotation type: {item.GetType().FullName}");

            LineProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(lineItem, properties);
            properties.Points = AnnotationMappingHelper.ToPoints(lineItem.Points);
            AnnotationMappingHelper.ApplyShapeStyle(lineItem.Style, properties.Brush ?? Brushes.Transparent, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            if (properties.Pen != null)
            {
                properties.Brush = properties.Pen.Brush;
            }
            return properties;
        }

        public DrawingVisualBase ToVisual(AnnotationItem item)
        {
            if (item is not LineAnnotationItem lineItem)
                throw new NotSupportedException($"Unsupported line annotation type: {item.GetType().FullName}");

            return new DVLine((LineProperties)ToProperties(lineItem));
        }
    }
}