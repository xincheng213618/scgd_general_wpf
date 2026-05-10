namespace ColorVision.ImageEditor.Draw.Annotations
{
    internal interface IAnnotationModule
    {
        bool CanExport(DrawingVisualBase visual);

        AnnotationItem ToItem(DrawingVisualBase visual);

        bool CanExport(BaseProperties properties);

        bool CanImport(AnnotationItem item);

        AnnotationItem ToItem(BaseProperties properties);

        BaseProperties ToProperties(AnnotationItem item);

        DrawingVisualBase ToVisual(AnnotationItem item);
    }
}