using System.Collections.Generic;

namespace ColorVision.ImageEditor.Abstractions
{
    public interface IImageComponent
    {
        void Execute(ImageView imageView);
    }

    public interface IImageOpen
    {
        void OpenImage(EditorContext context, string? filePath);
    }

    public interface IImageOpenEditorToolProvider
    {
        IEnumerable<IEditorTool> GetEditorTools();
    }

    public interface IImageOpenEditorToolLifecycle
    {
        void OnEditorToolsActivated(EditorContext context);

        void OnEditorToolsDeactivated(EditorContext context);
    }
}
