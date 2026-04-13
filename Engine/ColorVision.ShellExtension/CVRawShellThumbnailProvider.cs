using System;
using System.Runtime.InteropServices;
using ColorVision.FileIO;
using OpenCvSharp;

namespace ColorVision.ShellExtension
{
    /// <summary>
    /// Windows Shell Thumbnail Provider for .cvraw files.
    /// This COM class is loaded by Windows Explorer to generate file thumbnails for CVRAW format.
    /// Implements IInitializeWithStream (preferred by Win11 process isolation)
    /// and IInitializeWithFile (fallback when process isolation is disabled).
    /// </summary>
    [ComVisible(true)]
    [Guid("7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CVRawShellThumbnailProvider : CVThumbnailProviderBase
    {
        /// <summary>
        /// Gets the file type this provider handles.
        /// </summary>
        protected override CVType FileType => CVType.Raw;

        /// <summary>
        /// Gets the provider name for logging.
        /// </summary>
        protected override string ProviderName => "CVRawProvider";

        /// <summary>
        /// Creates an OpenCV Mat from CVCIEFile data for CVRAW files.
        /// CVRAW files are treated as standard multi-channel image data.
        /// </summary>
        protected override Mat? CreateMatFromFileData(CVCIEFile fileInfo)
        {
            try
            {
                Mat? src = null;

                if (fileInfo.FileExtType == CVType.Tif)
                {
                    src = Cv2.ImDecode(fileInfo.Data, ImreadModes.Unchanged);
                }
                else if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src)
                {
                    // CVRAW: Direct pixel data interpretation
                    src = Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);

                    // Normalize 32-bit float images to 8-bit for display
                    if (fileInfo.Bpp != 8 && src != null)
                    {
                        Cv2.Normalize(src, src, 0, 255, NormTypes.MinMax);
                        src.ConvertTo(src, MatType.CV_8U);
                    }
                }

                return src;
            }
            catch (Exception ex)
            {
                ShellLog.Log($"{ProviderName}: CreateMatFromFileData error: {ex.Message}");
                return null;
            }
        }
    }
}
