using System;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal sealed class TextAnnotationModule : IAnnotationModule
    {
        public bool CanExport(DrawingVisualBase visual)
        {
            return visual?.BaseAttribute is TextProperties;
        }

        public AnnotationItem ToItem(DrawingVisualBase visual)
        {
            return ToItem(visual.BaseAttribute);
        }

        public bool CanExport(BaseProperties properties)
        {
            return properties is TextProperties;
        }

        public bool CanImport(AnnotationItem item)
        {
            return item is TextAnnotationItem;
        }

        public AnnotationItem ToItem(BaseProperties properties)
        {
            if (properties is not TextProperties textProperties)
                throw new NotSupportedException($"Unsupported text properties type: {properties.GetType().FullName}");

            TextAnnotationItem item = new()
            {
                Position = AnnotationMappingHelper.ToAnnotationPoint(textProperties.Position),
                TextStyle = AnnotationMappingHelper.CreateTextStyle(textProperties.TextAttribute, textProperties.IsShowText, textProperties.Background),
            };
            AnnotationMappingHelper.CopyBaseProperties(textProperties, item);
            return item;
        }

        public BaseProperties ToProperties(AnnotationItem item)
        {
            if (item is not TextAnnotationItem textItem)
                throw new NotSupportedException($"Unsupported text annotation type: {item.GetType().FullName}");

            TextProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(textItem, properties);
            properties.Position = AnnotationMappingHelper.ToPoint(textItem.Position);
            AnnotationMappingHelper.ApplyTextStyle(textItem.TextStyle, properties.TextAttribute);
            properties.IsShowText = textItem.TextStyle.Visible;
            if (!string.IsNullOrWhiteSpace(textItem.TextStyle.BackgroundColor))
            {
                properties.Background = TextStyleSerialization.DeserializeBrush(textItem.TextStyle.BackgroundColor, properties.Background);
            }

            return properties;
        }

        public DrawingVisualBase ToVisual(AnnotationItem item)
        {
            if (item is not TextAnnotationItem textItem)
                throw new NotSupportedException($"Unsupported text annotation type: {item.GetType().FullName}");

            return new DVText((TextProperties)ToProperties(textItem));
        }
    }
}