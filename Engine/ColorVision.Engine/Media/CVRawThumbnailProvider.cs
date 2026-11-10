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
    public class CVRawThumbnailProvider : IThumbnailProvider
    {
        private static readonly ILog log = LogManager.GetLogger(typeof(CVRawThumbnailProvider));
        private const int FloatChunkSamples = 64 * 1024;
        private const double DoubleMachineEpsilon = 2.2204460492503131e-16;

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
                int index = CVFileUtil.ReadCIEFileHeader(filePath, out CVCIEFile fileInfo);
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
        {
            if (maxSize <= 0 || !CanHandle(filePath)) return null;

            try
            {
                return await Task.Run(() => ReadThumbnail(filePath, maxSize)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                log.Error($"GenerateThumbnailAsync Error for {filePath}: {ex.Message}");
                return null;
            }
        }

        private static BitmapSource? ReadThumbnail(string filePath, int maxSize)
        {
            if (!File.Exists(filePath)) return null;
            using Stream stream = CVFileReadCache.OpenRead(filePath, populateCache: false);
            int headerEnd = CVFileUtil.ReadCIEFileHeader(stream, out CVCIEFile header, maximumChannels: 4);
            using (header)
            {
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
                if (stream is FileStream file)
                {
                    CvcieDiskPixels pixels = CvcieDiskPixels.Locate(file, header, headerEnd, requiredBytes, requireExactLength: false);
                    return ReadDiskThumbnail(file, pixels, header.Rows, header.Cols, type, (int)sourceBytes, size);
                }

                // An existing CVRAW cache entry is a borrowed stream, not a disk file that can be mapped.
                if (!CVFileUtil.ReadCIEFilePixels(stream, header, headerEnd)) return null;
                using Mat source = Mat.FromPixelData(header.Rows, header.Cols, type, header.Data);
                return ResizeToBitmap(source, size);
            }
        }

        private static unsafe BitmapSource ReadDiskThumbnail(FileStream file, CvcieDiskPixels pixels,
            int rows, int columns, MatType type, int sourceBytes, Size size)
        {
            try
            {
                using var mapping = MemoryMappedFile.CreateFromFile(file, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, leaveOpen: true);
                using var view = mapping.CreateViewAccessor(pixels.Offset, sourceBytes, MemoryMappedFileAccess.Read);
                byte* address = null;
                view.SafeMemoryMappedViewHandle.AcquirePointer(ref address);
                try
                {
                    using Mat source = Mat.FromPixelData(rows, columns, type, (IntPtr)(address + view.PointerOffset));
                    return ResizeToBitmap(source, size);
                }
                finally { view.SafeMemoryMappedViewHandle.ReleasePointer(); }
            }
            catch (Exception error) when (error is IOException or UnauthorizedAccessException or NotSupportedException)
            {
                using Mat source = new(rows, columns, type);
                pixels.ReadExactly(new Span<byte>((void*)source.Data, sourceBytes), 0, CancellationToken.None);
                return ResizeToBitmap(source, size);
            }
        }

        private static BitmapSource ResizeToBitmap(Mat source, Size size)
        {
            using Mat? bytes = source.Depth() == MatType.CV_32F ? ConvertFloatToBytes(source) : null;
            using Mat thumbnail = new();
            Cv2.Resize(bytes ?? source, thumbnail, size, 0, 0, InterpolationFlags.Area);
            var bitmap = thumbnail.ToWriteableBitmap();
            bitmap.Freeze();
            return bitmap;
        }

        private static Mat ConvertFloatToBytes(Mat source)
        {
            using Mat samples = source.Reshape(1, 1);
            Cv2.MinMaxLoc(samples, out double min, out double max);
            // Match OpenCV's CV_32F NORM_MINMAX scale and shift, then retain its separate rounding to 8-bit before Area resize.
            float scale = (float)(255 * (max - min > DoubleMachineEpsilon ? 1 / (max - min) : 0));
            float shift = 0f - (float)(min * scale);
            Mat bytes = new(source.Rows, source.Cols, MatType.MakeType(MatType.CV_8U, source.Channels()));
            try
            {
                using Mat normalized = new();
                int sampleCount = samples.Cols;
                IntPtr output = bytes.Data;
                for (int start = 0; start < sampleCount;)
                {
                    int count = Math.Min(FloatChunkSamples, sampleCount - start);
                    using Mat block = samples.ColRange(start, start + count);
                    using Mat destination = Mat.FromPixelData(1, count, MatType.CV_8UC1, IntPtr.Add(output, start));
                    block.ConvertTo(normalized, MatType.CV_32F, scale, shift);
                    normalized.ConvertTo(destination, MatType.CV_8U);
                    start += count;
                }
                return bytes;
            }
            catch
            {
                bytes.Dispose();
                throw;
            }
        }
    }
}
