using System;
using System.Runtime.InteropServices;
using ColorVision.FileIO;
using OpenCvSharp;

namespace ColorVision.ShellExtension
{
    /// <summary>
    /// Windows Shell Thumbnail Provider for .cvcie files.
    /// This COM class is loaded by Windows Explorer to generate file thumbnails for CVCIE format.
    /// Implements IInitializeWithStream (preferred by Win11 process isolation)
    /// and IInitializeWithFile (fallback when process isolation is disabled).
    ///
    /// For CIE files with 3 channels (XYZ), only the first channel (X) is used for thumbnail display
    /// since the data is stored as 3 separate planes.
    /// </summary>
    [ComVisible(true)]
    [Guid("8C6F3B4D-9E2A-5F7B-C3D4-2E4F6A8B9C0D")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CVCieShellThumbnailProvider : CVThumbnailProviderBase
    {
        /// <summary>
        /// Gets the file type this provider handles.
        /// </summary>
        protected override CVType FileType => CVType.CIE;

        /// <summary>
        /// Gets the provider name for logging.
        /// </summary>
        protected override string ProviderName => "CVCieProvider";

        /// <summary>
        /// Creates an OpenCV Mat from CVCIEFile data for CVCIE files.
        /// For 3-channel CIE (XYZ), data is stored as 3 separate planes.
        /// The first channel (X) is extracted for thumbnail display.
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
                else if (fileInfo.FileExtType == CVType.CIE)
                {
                    if (fileInfo.Channels == 3)
                    {
                        // For 3-channel CIE (XYZ), data is stored as 3 separate planes
                        // Extract the first channel (X) for thumbnail display
                        int singleChannelLen = fileInfo.Rows * fileInfo.Cols * (fileInfo.Bpp / 8);
                        byte[] channelData = new byte[singleChannelLen];
                        Buffer.BlockCopy(fileInfo.Data, 0, channelData, 0, singleChannelLen);
                        src = Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.MakeType(fileInfo.Depth, 1), channelData);
                    }
                    else
                    {
                        src = Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
                    }

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
