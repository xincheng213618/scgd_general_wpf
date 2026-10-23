using ColorVision.ImageEditor.Output;
using ColorVision.ImageEditor.Tif;
using System.IO;
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

        public long SnapshotCacheId => snapshotCapture.CacheId;

        private void UpdateSnapshotCacheSource()
        {
            string title = System.Windows.Window.GetWindow(this)?.Title ?? string.Empty;
            snapshotCapture.SetSourceName(string.IsNullOrEmpty(Name) ? title : $"{title} / {Name}".Trim(' ', '/'));
        }

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
            BitmapSource? pixels = snapshotCapture.CaptureRendered(
                ImageShow,
                Config.GetProperties<double>("DpiX"),
                Config.GetProperties<double>("DpiY"),
                CommitActiveDrawingEditsForOutput);
            return pixels == null ? null : ImageSnapshotMetadata.CaptureRecord(pixels, CaptureParameterRecord());
        }

        public ImageViewSnapshot? CaptureSnapshotForBackgroundSave() => CaptureSnapshotForBackgroundSave(includeOverlays: true);

        /// <summary>Captures the loaded base pixels and optional drawings for an independent background export.</summary>
        public ImageViewSnapshot? CaptureSnapshotForBackgroundSave(bool includeOverlays)
        {
            Dispatcher.VerifyAccess();
            UpdateSnapshotCacheSource();
            ImageViewSnapshot? snapshot = snapshotCapture.CaptureForBackgroundSave(
                ImageShow,
                ViewBitmapSource,
                Config.GetProperties<double>("DpiX"),
                Config.GetProperties<double>("DpiY"),
                includeOverlays,
                CommitActiveDrawingEditsForOutput);
            if (snapshot != null) snapshot.RenderedParametersJson = CaptureParameterRecord();
            return snapshot;
        }

        private string CaptureParameterRecord()
        {
            ColorVisionTiffParameters parameters = _document.ParameterRecord ?? new()
            {
                InputFileName = Path.GetFileName(Config.FilePath), SourceType = Path.GetExtension(Config.FilePath).TrimStart('.').ToUpperInvariant(),
                Rows = Config.GetProperties<int>(ImageViewPropertyKeys.ImageHeight), Cols = Config.GetProperties<int>(ImageViewPropertyKeys.ImageWidth),
                Bpp = Config.GetProperties<int>(ImageViewPropertyKeys.Depth), SourceChannels = Config.GetProperties<int>(ImageViewPropertyKeys.Channel)
            };
            var record = parameters.ToDisplayRecord();
            if (record.Acquisition == null && (Config.GetProperties<string>(ImageViewPropertyKeys.CameraModel) != null
                || Config.GetProperties<string>(ImageViewPropertyKeys.CameraManufacturer) != null))
                record.Acquisition = new()
                {
                    Camera = new()
                    {
                        Model = Config.GetProperties<string>(ImageViewPropertyKeys.CameraModel),
                        Manufacturer = Config.GetProperties<string>(ImageViewPropertyKeys.CameraManufacturer)
                    }
                };
            return record.ToJson();
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
