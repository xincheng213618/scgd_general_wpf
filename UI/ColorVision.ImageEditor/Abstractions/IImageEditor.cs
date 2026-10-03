using System.Collections.Generic;
using System.Threading.Tasks;

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

    /// <summary>Lets an opener expose cached content without requiring a disk file.</summary>
    public interface IImageOpenFileCache
    {
        bool TryGetCachedLength(string filePath, out long length);
    }

    /// <summary>Ends the current file's lifetime, independently of toolbar activation.
    /// Invalidate pending publication before returning; asynchronously retire any active readers.</summary>
    public interface IImageOpenContentLifetime
    {
        /// <param name="reuseBuffers">The same opener will immediately load another file. Previous consumers must still retire.</param>
        Task ReleaseContentAsync(bool reuseBuffers);
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
