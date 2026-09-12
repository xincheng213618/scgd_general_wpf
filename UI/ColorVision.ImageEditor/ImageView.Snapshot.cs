using ColorVision.ImageEditor.Output;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor
{
    public partial class ImageView
    {
        private readonly ImageSnapshotCapture snapshotCapture = new();

        public void ReleaseSnapshotBuffer() => snapshotCapture.ReleaseBuffer();

        public void Save(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName))
            {
                log.Warn("Skip saving ImageView because file name is empty.");
                return;
            }

            BitmapSource? snapshot = CaptureSnapshot();
            if (snapshot != null)
                ImageSnapshotEncoder.SaveSnapshot(snapshot, fileName);
        }

        /// <summary>Captures and freezes the current WPF image and result visuals on the UI thread.</summary>
        public BitmapSource? CaptureSnapshot()
        {
            Dispatcher.VerifyAccess();
            return snapshotCapture.CaptureRendered(
                ImageShow,
                Config.GetProperties<double>("DpiX"),
                Config.GetProperties<double>("DpiY"),
                CommitActiveDrawingEditsForOutput);
        }

        public ImageViewSnapshot? CaptureSnapshotForBackgroundSave() => CaptureSnapshotForBackgroundSave(includeOverlays: true);

        /// <summary>Captures the loaded base pixels and optional drawings for an independent background export.</summary>
        public ImageViewSnapshot? CaptureSnapshotForBackgroundSave(bool includeOverlays)
        {
            Dispatcher.VerifyAccess();
            return snapshotCapture.CaptureForBackgroundSave(
                ImageShow,
                ViewBitmapSource,
                Config.GetProperties<double>("DpiX"),
                Config.GetProperties<double>("DpiY"),
                includeOverlays,
                CommitActiveDrawingEditsForOutput);
        }

        public static Task SaveSnapshotAsync(BitmapSource snapshot, string fileName, CancellationToken cancellationToken = default)
            => ImageSnapshotEncoder.SaveBitmapAsync(snapshot, fileName, cancellationToken);

        public static Task SaveSnapshotAsync(ImageViewSnapshot snapshot, string fileName, CancellationToken cancellationToken = default)
            => ImageSnapshotEncoder.SaveSnapshotWithOptionsAsync(snapshot, fileName, ImageViewSnapshotSaveOptions.Default, cancellationToken);

        public static Task SaveSnapshotWithOptionsAsync(
            ImageViewSnapshot snapshot,
            string fileName,
            ImageViewSnapshotSaveOptions options,
            CancellationToken cancellationToken = default)
            => ImageSnapshotEncoder.SaveSnapshotWithOptionsAsync(snapshot, fileName, options, cancellationToken);

        public static Task SaveSnapshotExportsAsync(
            ImageViewSnapshot snapshot,
            ImageViewSnapshotExportOptions options,
            CancellationToken cancellationToken = default)
            => ImageSnapshotEncoder.SaveSnapshotExportsAsync(snapshot, options, cancellationToken);

        public static bool CanBmpPreserveSourceBitDepth(PixelFormat format) => ImageSnapshotEncoder.CanBmpPreserveSourceBitDepth(format);
    }
}
