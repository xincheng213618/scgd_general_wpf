using System;
using System.Windows.Media;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal sealed class BezierCurveAnnotationModule : IAnnotationModule
    {
        public bool CanExport(DrawingVisualBase visual)
        {
            return visual?.BaseAttribute is BezierCurveProperties;
        }

        public AnnotationItem ToItem(DrawingVisualBase visual)
        {
            return ToItem(visual.BaseAttribute);
        }

        public bool CanExport(BaseProperties properties)
        {
            return properties is BezierCurveProperties;
        }

        public bool CanImport(AnnotationItem item)
        {
            return item is BezierCurveAnnotationItem;
        }

        public AnnotationItem ToItem(BaseProperties properties)
        {
            if (properties is not BezierCurveProperties bezierCurveProperties)
                throw new NotSupportedException($"Unsupported bezier curve properties type: {properties.GetType().FullName}");

            BezierCurveAnnotationItem item = new()
            {
                Points = AnnotationMappingHelper.ToAnnotationPoints(bezierCurveProperties.Points),
                Style = AnnotationMappingHelper.CreateShapeStyle(bezierCurveProperties.Brush ?? bezierCurveProperties.Pen?.Brush ?? Brushes.Transparent, bezierCurveProperties.Pen),
            };
            AnnotationMappingHelper.CopyBaseProperties(bezierCurveProperties, item);
            return item;
        }

        public BaseProperties ToProperties(AnnotationItem item)
        {
            if (item is not BezierCurveAnnotationItem bezierCurveItem)
                throw new NotSupportedException($"Unsupported bezier curve annotation type: {item.GetType().FullName}");

            BezierCurveProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(bezierCurveItem, properties);
            properties.Points = AnnotationMappingHelper.ToPoints(bezierCurveItem.Points);
            AnnotationMappingHelper.ApplyShapeStyle(bezierCurveItem.Style, properties.Brush ?? Brushes.Transparent, properties.Pen, brush => properties.Brush = brush, pen => properties.Pen = pen);
            if (properties.Pen != null)
            {
                properties.Brush = properties.Pen.Brush;
            }

            return properties;
        }

        public DrawingVisualBase ToVisual(AnnotationItem item)
        {
            if (item is not BezierCurveAnnotationItem bezierCurveItem)
                throw new NotSupportedException($"Unsupported bezier curve annotation type: {item.GetType().FullName}");

            return new DVBezierCurve((BezierCurveProperties)ToProperties(bezierCurveItem));
        }
    }
}