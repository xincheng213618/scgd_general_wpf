namespace ColorVision.ImageEditor.Draw;

/// <summary>A freehand contour is stored as an ordinary editable closed polygon.</summary>
public sealed class LassoManager : PolygonManager
{
    public LassoManager(DrawEditorContext context) : base(context)
    {
        Order = 6;
        Icon = CompactInspectorIcons.CreateGlyph("\uEF20", 18);
    }

    public override string? GuidId => "自由套索";
    protected override bool IsFreehand => true;
    protected override bool CompleteOnMouseUp => true;
    protected override void OnVisualCompleted(DVPolygon visual)
    {
        CloseOnComplete = true;
        base.OnVisualCompleted(visual);
    }
}
