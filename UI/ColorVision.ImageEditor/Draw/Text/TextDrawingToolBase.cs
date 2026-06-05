namespace ColorVision.ImageEditor.Draw
{
    public abstract class TextDrawingToolBase : DrawEditorToggleToolBase
    {
        protected TextDrawingToolBase(TextEditingContext textContext)
        {
            TextContext = textContext;
            ToolBarLocal = ToolBarLocal.Draw;
        }

        protected TextEditingContext TextContext { get; }

        protected DrawCanvas DrawCanvas => TextContext.DrawCanvas;

        protected Zoombox Zoombox => TextContext.Zoombox;

        protected SelectEditorVisual SelectionVisual => TextContext.SelectionVisual;
    }
}
