using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal sealed class PolygonAnnotationModule : IAnnotationModule
    {
        public bool CanExport(DrawingVisualBase visual)
        {
            return visual?.BaseAttribute is PolygonProperties;
        }

        public AnnotationItem ToItem(DrawingVisualBase visual)
        {
            if (visual is not DrawingVisualBase<PolygonProperties> polygonVisual)
                return ToItem(visual.BaseAttribute);

            PolygonAnnotationItem item = new()
            {
                Points = AnnotationMappingHelper.ToAnnotationPoints(polygonVisual.Attribute.Points),
                Style = AnnotationMappingHelper.CreateShapeStyle(polygonVisual.Attribute.Brush ?? polygonVisual.Attribute.Pen?.Brush ?? Brushes.Transparent, polygonVisual.Attribute.Pen),
                IsClosed = visual is DVPolygon dvPolygon && dvPolygon.IsComple,
            };
            AnnotationMappingHelper.CopyBaseProperties(polygonVisual.Attribute, item);
            return item;
        }

        public bool CanExport(BaseProperties properties)
        {
            return properties is PolygonProperties;
        }

        public bool CanImport(AnnotationItem item)
        {
            return item is PolygonAnnotationItem;
        }

        public AnnotationItem ToItem(BaseProperties properties)
        {
            if (properties is not PolygonProperties polygonProperties)
                throw new NotSupportedException($"Unsupported polygon properties type: {properties.GetType().FullName}");

            PolygonAnnotationItem item = new()
            {
                Points = AnnotationMappingHelper.ToAnnotationPoints(polygonProperties.Points),
                Style = AnnotationMappingHelper.CreateShapeStyle(polygonProperties.Brush ?? polygonProperties.Pen?.Brush ?? Brushes.Transparent, polygonProperties.Pen),
                IsClosed = false,
            };
            AnnotationMappingHelper.CopyBaseProperties(polygonProperties, item);
            return item;
        }

        public BaseProperties ToProperties(AnnotationItem item)
        {
            if (item is not PolygonAnnotationItem polygonItem)
                throw new NotSupportedException($"Unsupported polygon annotation type: {item.GetType().FullName}");

            PolygonProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(polygonItem, properties);
            properties.Points = AnnotationMappingHelper.ToPoints(polygonItem.Points);
            AnnotationMappingHelper.ApplyShapeStyle(polygonItem.Style, properties.Brush ?? Brushes.Transparent, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            if (properties.Pen != null)
            {
                properties.Brush = properties.Pen.Brush;
            }
            return properties;
        }

        public DrawingVisualBase ToVisual(AnnotationItem item)
        {
            if (item is not PolygonAnnotationItem polygonItem)
                throw new NotSupportedException($"Unsupported polygon annotation type: {item.GetType().FullName}");

            DVPolygon visual = new((PolygonProperties)ToProperties(polygonItem))
            {
                IsComple = polygonItem.IsClosed,
            };
            return visual;
        }
    }
}