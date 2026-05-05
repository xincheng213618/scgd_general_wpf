using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace ColorVision.ImageEditor.Draw.Annotations
{
    public static class AnnotationMapper
    {
        private static readonly IReadOnlyList<IAnnotationModule> Modules = new IAnnotationModule[]
        {
            new CircleAnnotationModule(),
            new RectangleAnnotationModule(),
            new TextAnnotationModule(),
            new LineAnnotationModule(),
            new PolygonAnnotationModule(),
            new BezierCurveAnnotationModule(),
        };

        public static AnnotationDocument CreateDocument(IEnumerable<DrawingVisualBase> visuals)
        {
            ArgumentNullException.ThrowIfNull(visuals);

            AnnotationDocument document = new();
            foreach (DrawingVisualBase visual in visuals)
            {
                AnnotationItem? item = ToItem(visual);
                if (item != null)
                    document.Items.Add(item);
            }

            return document;
        }

        public static AnnotationDocument CreateDocument(IEnumerable<BaseProperties> properties)
        {
            ArgumentNullException.ThrowIfNull(properties);

            AnnotationDocument document = new();
            foreach (BaseProperties property in properties)
            {
                AnnotationItem? item = ToItem(property);
                if (item != null)
                    document.Items.Add(item);
            }

            return document;
        }

        public static AnnotationItem? ToItem(DrawingVisualBase? visual)
        {
            if (visual == null)
                return null;

            foreach (IAnnotationModule module in Modules)
            {
                if (module.CanExport(visual))
                    return module.ToItem(visual);
            }

            return ToItem(visual.BaseAttribute);
        }

        public static AnnotationItem? ToItem(BaseProperties? properties)
        {
            if (properties == null)
                return null;

            foreach (IAnnotationModule module in Modules)
            {
                if (module.CanExport(properties))
                    return module.ToItem(properties);
            }

            return null;
        }

        public static BaseProperties ToProperties(AnnotationItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            foreach (IAnnotationModule module in Modules)
            {
                if (module.CanImport(item))
                    return module.ToProperties(item);
            }

            throw new NotSupportedException($"Unsupported annotation item type: {item.GetType().FullName}");
        }

        public static DrawingVisualBase ToVisual(AnnotationItem item)
        {
            ArgumentNullException.ThrowIfNull(item);

            foreach (IAnnotationModule module in Modules)
            {
                if (module.CanImport(item))
                    return module.ToVisual(item);
            }

            throw new NotSupportedException($"Unsupported annotation item type: {item.GetType().FullName}");
        }

        public static IReadOnlyList<DrawingVisualBase> ToVisuals(AnnotationDocument document)
        {
            ArgumentNullException.ThrowIfNull(document);

            List<DrawingVisualBase> visuals = new(document.Items.Count);
            foreach (AnnotationItem item in document.Items)
            {
                visuals.Add(ToVisual(item));
            }

            return visuals;
        }

        public static string Serialize(AnnotationDocument document, Formatting formatting = Formatting.Indented)
        {
            ArgumentNullException.ThrowIfNull(document);
            return JsonConvert.SerializeObject(document, formatting);
        }

        public static AnnotationDocument Deserialize(string json)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(json);
            return JsonConvert.DeserializeObject<AnnotationDocument>(json) ?? new AnnotationDocument();
        }
    }
}