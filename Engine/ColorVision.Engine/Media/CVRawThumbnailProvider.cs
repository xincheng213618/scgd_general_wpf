using ColorVision.FileIO;
using ColorVision.UI;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.ComponentModel;
using System.IO;
using System.Linq;
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
                if (index > 0)
                {
                    return (fileInfo.Cols, fileInfo.Rows);
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
            if (!File.Exists(filePath) || !CanHandle(filePath))
                return null;

            try
            {
                return await Task.Run(() =>
                {
                    BitmapSource? result = null;
                    CVCIEFile? fileInfo = null;
                    Mat? mat = null;
                    Mat? thumbnail = null;

                    try
                    {
                        // Read file header first to get dimensions
                     int index = CVFileUtil.ReadCIEFileHeader(filePath, out fileInfo);
                        if (index <= 0)
                        {
                            log.Warn($"ReadCIEFileHeader returned {index} for {filePath}");
                            return null;
                        }

                        // Read the full file data
                        bool ret = CVFileUtil.ReadCIEFileData(filePath, ref fileInfo, index);
                        if (!ret)
                        {
                            log.Warn($"ReadCIEFileData returned {ret} for {filePath}");
                            return null;
                        }

                        mat = CreateMatFromCVCIEFile(fileInfo);
                        if (mat == null)
                            return null;

                        // Calculate thumbnail dimensions maintaining aspect ratio
                        double scale = Math.Min((double)maxSize / mat.Cols, (double)maxSize / mat.Rows);
                        scale = Math.Min(scale, 1.0); // Don't upscale small images

                        int thumbWidth = (int)(mat.Cols * scale);
                        int thumbHeight = (int)(mat.Rows * scale);

                        if (thumbWidth <= 0 || thumbHeight <= 0)
                            return null;

                        // Resize using OpenCV
                        thumbnail = new Mat();
                        Cv2.Resize(mat, thumbnail, new Size(thumbWidth, thumbHeight), 0, 0, InterpolationFlags.Area);

                        // Convert to WriteableBitmap
                        // Note: ToWriteableBitmap creates a frozen copy, safe to use across threads
                        if (System.Windows.Application.Current != null)
                        {
                            System.Windows.Application.Current.Dispatcher.Invoke(() =>
                            {
                                var bitmap = thumbnail.ToWriteableBitmap();
                                bitmap?.Freeze();
                                result = bitmap;
                            });
                        }
                        else
                        {
                            // Fallback if Application.Current is not available
                            log.Warn("Application.Current is null, cannot convert Mat to WriteableBitmap");
                        }

                        return result;
                    }
                    finally
                    {
                        // Ensure proper cleanup of all resources
                        thumbnail?.Dispose();
                        mat?.Dispose();
                        fileInfo?.Dispose();
                    }
                });
            }
            catch (Exception ex)
            {
                log.Error($"GenerateThumbnailAsync Error for {filePath}: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Creates an OpenCV Mat from CVCIEFile data.
        /// Based on the pattern from MediaHelper.ToWriteableBitmap().
        /// </summary>
        private static Mat? CreateMatFromCVCIEFile(CVCIEFile fileInfo)
        {
            try
            {
                Mat? src = null;

                if (fileInfo.FileExtType == CVType.Tif)
                {
                    src = Cv2.ImDecode(fileInfo.Data, ImreadModes.Unchanged);
                }
                else if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src || fileInfo.FileExtType == CVType.CIE)
                {
                    if (fileInfo.FileExtType == CVType.CIE)
                    {
                        int len = (int)(fileInfo.Rows * fileInfo.Cols * (fileInfo.Bpp / 8));
                        if (fileInfo.Channels == 3)
                        {
                            // For 3-channel CIE, use first channel (Y component)
                            src = Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, MatType.MakeType(fileInfo.Depth, 1), fileInfo.Data);
                        }
                        else
                        {
                            src = Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                        }
                    }
                    else
                    {
                        src = Mat.FromPixelData(fileInfo.Cols, fileInfo.Rows, MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                    }

                    // Normalize 32-bit float images to 8-bit for display
                    if (fileInfo.Bpp == 32 && src != null)
                    {
                        // Normalize in-place and convert to 8-bit
                        Cv2.Normalize(src, src, 0, 255, NormTypes.MinMax);
                        src.ConvertTo(src, MatType.CV_8U);
                    }
                }

                return src;
            }
            catch (Exception ex)
            {
                log.Error($"CreateMatFromCVCIEFile Error: {ex.Message}");
                return null;
            }
        }
    }
}
