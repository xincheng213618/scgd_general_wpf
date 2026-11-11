using ColorVision.FileIO;
using ColorVision.UI;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.ComponentModel;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    /// <summary>
    /// Thumbnail provider for .cvraw and .cvcie files.
    /// Uses OpenCV for image processing and conversion.
    /// </summary>
    [FileExtension(".cvraw|.cvcie")]
    public class CVRawThumbnailProvider : IThumbnailProviderWithDimensions
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawThumbnailProvider));
        // Bound mapped pages and native conversion buffers across every browser and direct caller.
        private static readonly SemaphoreSlim DecodeSlots = new(2, 2);

        // Cache the supported extensions from the attribute for CanHandle consistency
        private static readonly string[] SupportedExtensions;

        static CVRawThumbnailProvider()
        {
            var attr = typeof(CVRawThumbnailProvider)
                .GetCustomAttributes(typeof(FileExtensionAttribute), false)
                .FirstOrDefault() as FileExtensionAttribute;

            SupportedExtensions = attr?.Extensions ?? new[] { ".cvraw", ".cvcie" };
        }

        /// <summary>
        /// Priority order - lower values are checked first.
        /// </summary>
        public int Order => 10;

        // Earlier CVRAW thumbnails could contain process-cache pixels stamped with disk metadata.
        public int GetCacheVersion(string filePath)
            => string.Equals(Path.GetExtension(filePath), ".cvraw", StringComparison.OrdinalIgnoreCase) ? 1 : 0;

        /// <summary>
        /// Checks if the file has a supported extension (.cvraw or .cvcie).
        /// Uses the extensions from the FileExtensionAttribute for consistency.
        /// </summary>
        public bool CanHandle(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return SupportedExtensions.Any(e => e.Equals(ext, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>
        /// Gets the image dimensions from the file header without fully loading the image data.
        /// This is a fast operation that only reads the file header.
        /// </summary>
        public (int width, int height) GetImageDimensions(string filePath)
        {
            if (!File.Exists(filePath) || !CanHandle(filePath))
                return (0, 0);

            try
            {
                using FileStream file = OpenThumbnailSource(filePath);
                int index = CVFileUtil.ReadCIEFileHeader(file, out CVCIEFile fileInfo, maximumChannels: 4);
                using (fileInfo)
                {
                    if (index > 0) return (fileInfo.Cols, fileInfo.Rows);
                }
            }
            catch (Exception ex)
            {
                log.Error($"GetImageDimensions Error for {filePath}: {ex.Message}");
            }

            return (0, 0);
        }

        /// <summary>
        /// Generates a thumbnail for the cvraw/cvcie file.
        /// </summary>
        public async Task<BitmapSource?> GenerateThumbnailAsync(string filePath, int maxSize)
            => (await GenerateThumbnailWithDimensionsAsync(filePath, maxSize, CancellationToken.None).ConfigureAwait(false))?.Thumbnail;

        public async Task<(BitmapSource Thumbnail, int OriginalWidth, int OriginalHeight)?> GenerateThumbnailWithDimensionsAsync(
            string filePath, int maxSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (maxSize <= 0 || !CanHandle(filePath)) return null;

            await DecodeSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var result = await Task.Run(() => ReadThumbnail(filePath, maxSize, cancellationToken), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception ex)
            {
                log.Error($"GenerateThumbnailAsync Error for {filePath}: {ex.Message}");
                return null;
            }
            finally { DecodeSlots.Release(); }
        }

        private static (BitmapSource Thumbnail, int OriginalWidth, int OriginalHeight)? ReadThumbnail(
            string filePath, int maxSize, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(filePath)) return null;
            cancellationToken.ThrowIfCancellationRequested();
            using FileStream file = OpenThumbnailSource(filePath);
            int headerEnd = CVFileUtil.ReadCIEFileHeader(file, out CVCIEFile header, maximumChannels: 4);
            using (header)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (headerEnd <= 0 || header.Rows <= 0 || header.Cols <= 0
                    || header.Bpp is not (8 or 16 or 32 or 64) || header.Channels is not (1 or 3 or 4))
                    return null;

                double scale = Math.Min(1, Math.Min((double)maxSize / header.Cols, (double)maxSize / header.Rows));
                Size size = new((int)(header.Cols * scale), (int)(header.Rows * scale));
                if (size.Width <= 0 || size.Height <= 0) return null;

                // XYZ thumbnails use the embedded X plane, regardless of any associated RAW.
                bool embeddedX = header.Channels == 3 && string.Equals(Path.GetExtension(filePath), ".cvcie", StringComparison.OrdinalIgnoreCase);
                int channels = embeddedX ? 1 : header.Channels;
                long planeBytes = checked((long)header.Rows * header.Cols * (header.Bpp / 8));
                long requiredBytes = checked(planeBytes * header.Channels);
                long sourceBytes = checked(planeBytes * channels);
                if (sourceBytes > Array.MaxLength) return null;
                MatType type = MatType.MakeType(header.Depth, channels);
                CvcieDiskPixels pixels = CvcieDiskPixels.Locate(file, header, headerEnd, requiredBytes, requireExactLength: false);
                BitmapSource thumbnail = ReadDiskThumbnail(file, pixels, header.Rows, header.Cols, type, (int)sourceBytes, size, cancellationToken);
                return (thumbnail, header.Cols, header.Rows);
            }
        }

        // MultiImageViewer indexes thumbnails by disk metadata. A previously opened CVRAW cache
        // entry may contain different pixels after an external rewrite of the file.
        private static FileStream OpenThumbnailSource(string filePath)
            => new(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);

        private static unsafe BitmapSource ReadDiskThumbnail(FileStream file, CvcieDiskPixels pixels,
            int rows, int columns, MatType type, int sourceBytes, Size size, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                using var mapping = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
                using var view = mapping.CreateViewAccessor(pixels.Offset, sourceBytes, MemoryMappedFileAccess.Read);
                byte* address = null;
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref address);
                try
                {
                    using Mat source = Mat.FromPixelData(rows, columns, type, (IntPtr)(address + view.PointerOffset));
                    return ResizeToBitmap(source, size, cancellationToken);
                }
                finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                cancellationToken.ThrowIfCancellationRequested();
                using Mat source = new(rows, columns, type);
                pixels.ReadExactly(new Span<byte>((void*)source.Data, sourceBytes), 0, cancellationToken);
                return ResizeToBitmap(source, size, cancellationToken);
            }
        }

        private static BitmapSource ResizeToBitmap(Mat source, Size size, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Mat? bytes = source.Depth() == MatType.CV_32F ? MediaHelper.ConvertFloatToDisplayBytes(source, cancellationToken) : null;
            cancellationToken.ThrowIfCancellationRequested();
            using Mat thumbnail = new();
            Cv2.Resize(bytes ?? source, thumbnail, size, 0, 0, InterpolationFlags.Area);
            cancellationToken.ThrowIfCancellationRequested();
            var bitmap = thumbnail.ToWriteableBitmap();
            bitmap.Freeze();
            cancellationToken.ThrowIfCancellationRequested();
            return bitmap;
        }

    }
}
