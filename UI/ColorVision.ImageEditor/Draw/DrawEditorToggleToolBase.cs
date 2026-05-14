namespace ColorVision.ImageEditor.Draw
{
    public interface IDrawEditorToggleTool
    {
        bool IsChecked { get; set; }
    }

    public abstract class DrawEditorToggleToolBase : IEditorToggleToolBase, IDrawEditorToggleTool
    {
    }
}