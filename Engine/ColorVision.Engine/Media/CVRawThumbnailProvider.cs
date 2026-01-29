using ColorVision.FileIO;
using ColorVision.UI;
using log4net;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;
using System;
using System.ComponentModel;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
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

        /// <summary>
        /// Priority order - lower values are checked first.
        /// </summary>
        public int Order => 10;

        /// <summary>
        /// Checks if the file has a supported extension (.cvraw or .cvcie).
        /// </summary>
        public bool CanHandle(string filePath)
        {
            if (string.IsNullOrEmpty(filePath))
                return false;

            var ext = Path.GetExtension(filePath).ToLowerInvariant();
            return ext == ".cvraw" || ext == ".cvcie";
        }

        /// <summary>
        /// Gets the image dimensions from the file header without fully loading the image data.
        /// </summary>
        public (int width, int height) GetImageDimensions(string filePath)
        {
            if (!File.Exists(filePath) || !CanHandle(filePath))
                return (0, 0);

            try
            {
                CVCIEFile fileInfo = new CVCIEFile();
                int ret = CVFileUtil.ReadCIEFileHeader(filePath, ref fileInfo);
                if (ret == 0)
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

                    // Read file header first to get dimensions
                    CVCIEFile fileInfo = new CVCIEFile();
                    int ret = CVFileUtil.ReadCIEFileHeader(filePath, ref fileInfo);
                    if (ret != 0)
                    {
                        log.Warn($"ReadCIEFileHeader returned {ret} for {filePath}");
                        return null;
                    }

                    // Read the full file data
                    ret = CVFileUtil.ReadCIEFileData(filePath, ref fileInfo, 0);
                    if (ret != 0)
                    {
                        log.Warn($"ReadCIEFileData returned {ret} for {filePath}");
                        return null;
                    }

                    using (var mat = CreateMatFromCVCIEFile(fileInfo))
                    {
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
                        using (var thumbnail = new Mat())
                        {
                            Cv2.Resize(mat, thumbnail, new Size(thumbWidth, thumbHeight), 0, 0, InterpolationFlags.Area);

                            // Convert to WriteableBitmap on UI thread
                            Application.Current?.Dispatcher.Invoke(() =>
                            {
                                var bitmap = thumbnail.ToWriteableBitmap();
                                bitmap?.Freeze();
                                result = bitmap;
                            });
                        }
                    }

                    fileInfo.Dispose();
                    return result;
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
                            byte[] data = new byte[len];
                            Buffer.BlockCopy(fileInfo.Data, len, data, 0, data.Length);
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
