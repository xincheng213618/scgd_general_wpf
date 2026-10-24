using ColorVision.ImageEditor.Tif;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Abstractions
{
    /// <summary>The lossless decoded samples, independently of any normalized display proxy.</summary>
    public sealed record DecodedImageContent(BitmapSource Pixels, ColorVisionTiffParameters? Parameters, string FilePath);

    public interface IImageContentProvider
    {
        Task<IImageContentCapability?> PrepareAsync(EditorContext context, DecodedImageContent content, CancellationToken cancellationToken);
    }

    public interface IImageContentCapability : IDisposable, IImageOpenEditorToolProvider, IImageOpenEditorToolLifecycle
    {
        /// <summary>Called after publication of the initial display source.</summary>
        void Activate();

        /// <summary>Synchronously detach this activation's resources. The returned cleanup may wait for readers
        /// and runs outside the document ownership lock. Reusable providers must capture only the retiring activation.</summary>
        Action DetachForRetirement(long contentVersion) => Dispose;
    }
}
