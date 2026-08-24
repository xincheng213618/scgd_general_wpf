using Newtonsoft.Json;
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

            AnnotationTextStyle textStyle = textItem.TextStyle
                ?? throw new JsonSerializationException("Text annotation is missing TextStyle.");
            TextProperties properties = new();
            AnnotationMappingHelper.ApplyBaseProperties(textItem, properties);
            properties.Position = AnnotationMappingHelper.ToFinitePoint(textItem.Position, "Text position");
            AnnotationMappingHelper.ApplyTextStyle(textStyle, properties.TextAttribute);
            properties.IsShowText = textStyle.Visible;
            if (!string.IsNullOrWhiteSpace(textStyle.BackgroundColor))
            {
                properties.Background = TextStyleSerialization.DeserializeBrush(textStyle.BackgroundColor, properties.Background);
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
