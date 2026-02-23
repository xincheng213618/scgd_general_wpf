using System;
using System.IO;
using System.Runtime.InteropServices;
using ColorVision.FileIO;
using ColorVision.ShellExtension.Interop;
using OpenCvSharp;

namespace ColorVision.ShellExtension
{
    /// <summary>
    /// Windows Shell Thumbnail Provider for .cvraw and .cvcie files.
    /// This COM class is loaded by Windows Explorer to generate file thumbnails.
    /// It reads the custom image format using ColorVision.FileIO and OpenCvSharp,
    /// then returns an HBITMAP that Explorer displays as the file thumbnail.
    /// </summary>
    [ComVisible(true)]
    [Guid("7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CVRawShellThumbnailProvider : IShellThumbnailProvider, IInitializeWithFile
    {
        private const int S_OK = 0;
        private const int E_FAIL = unchecked((int)0x80004005);
        private const int E_INVALIDARG = unchecked((int)0x80070057);

        private string? _filePath;

        /// <summary>
        /// Called by Explorer to provide the file path before requesting a thumbnail.
        /// </summary>
        public int Initialize(string pszFilePath, uint grfMode)
        {
            if (string.IsNullOrEmpty(pszFilePath))
                return E_INVALIDARG;
            _filePath = pszFilePath;
            ShellLog.Log($"Initialize: {pszFilePath}");
            return S_OK;
        }

        /// <summary>
        /// Called by Explorer to get the thumbnail bitmap.
        /// </summary>
        public int GetThumbnail(uint cx, out IntPtr phbmp, out uint pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WtsAlphaType.WTSAT_RGB;

            try
            {
                if (string.IsNullOrEmpty(_filePath) || !File.Exists(_filePath))
                {
                    ShellLog.Log($"GetThumbnail: file not found or empty path: {_filePath}");
                    return E_FAIL;
                }

                if (cx == 0)
                    return E_INVALIDARG;

                ShellLog.Log($"GetThumbnail: start, file={_filePath}, cx={cx}");

                // Read file into memory to avoid file locking issues
                byte[] fileData = File.ReadAllBytes(_filePath);

                int index = CVFileUtil.ReadCIEFileHeader(fileData, out CVCIEFile fileInfo);
                if (index <= 0)
                {
                    ShellLog.Log($"GetThumbnail: ReadCIEFileHeader returned {index} for {_filePath}");
                    return E_FAIL;
                }

                Mat? mat = null;
                Mat? thumbnail = null;

                try
                {
                    bool ret = CVFileUtil.ReadCIEFileData(fileData, ref fileInfo, index);
                    if (!ret || fileInfo.Data == null)
                    {
                        ShellLog.Log($"GetThumbnail: ReadCIEFileData failed for {_filePath}");
                        return E_FAIL;
                    }

                    ShellLog.Log($"GetThumbnail: file read OK, type={fileInfo.FileExtType}, {fileInfo.Cols}x{fileInfo.Rows}, bpp={fileInfo.Bpp}, ch={fileInfo.Channels}");

                    mat = CreateMatFromCVCIEFile(fileInfo);
                    if (mat == null || mat.Empty())
                    {
                        ShellLog.Log($"GetThumbnail: CreateMatFromCVCIEFile returned null for {_filePath}");
                        return E_FAIL;
                    }

                    // Calculate thumbnail dimensions maintaining aspect ratio
                    double scale = Math.Min((double)cx / mat.Cols, (double)cx / mat.Rows);
                    scale = Math.Min(scale, 1.0); // Don't upscale

                    int thumbWidth = Math.Max(1, (int)(mat.Cols * scale));
                    int thumbHeight = Math.Max(1, (int)(mat.Rows * scale));

                    // Resize using OpenCV
                    thumbnail = new Mat();
                    Cv2.Resize(mat, thumbnail, new Size(thumbWidth, thumbHeight), 0, 0, InterpolationFlags.Area);

                    // Convert to HBITMAP via raw pixel data
                    phbmp = MatToHBitmap(thumbnail);
                    if (phbmp == IntPtr.Zero)
                    {
                        ShellLog.Log($"GetThumbnail: MatToHBitmap returned null for {_filePath}");
                        return E_FAIL;
                    }

                    ShellLog.Log($"GetThumbnail: success, {thumbWidth}x{thumbHeight} for {_filePath}");
                    return S_OK;
                }
                finally
                {
                    thumbnail?.Dispose();
                    mat?.Dispose();
                    fileInfo.Dispose();
                }
            }
            catch (Exception ex)
            {
                // Shell extensions must never throw - return error HRESULT
                ShellLog.Log($"GetThumbnail: exception for {_filePath}: {ex}");
                return E_FAIL;
            }
        }

        /// <summary>
        /// Creates an OpenCV Mat from CVCIEFile data.
        /// Matches the logic in CVRawThumbnailProvider.CreateMatFromCVCIEFile.
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
                        Cv2.Normalize(src, src, 0, 255, NormTypes.MinMax);
                        src.ConvertTo(src, MatType.CV_8U);
                    }
                }

                return src;
            }
            catch (Exception ex)
            {
                ShellLog.Log($"CreateMatFromCVCIEFile error: {ex.Message}");
                return null;
            }
        }

        /// <summary>
        /// Converts an OpenCV Mat to a Windows HBITMAP handle.
        /// Creates a DIB section and copies the pixel data.
        /// </summary>
        private static IntPtr MatToHBitmap(Mat mat)
        {
            // Ensure mat is 8-bit for display
            using var displayMat = new Mat();
            if (mat.Depth() != MatType.CV_8U)
            {
                Cv2.Normalize(mat, displayMat, 0, 255, NormTypes.MinMax);
                displayMat.ConvertTo(displayMat, MatType.CV_8U);
            }
            else
            {
                mat.CopyTo(displayMat);
            }

            // Convert to BGR 3-channel if needed
            using var bgrMat = new Mat();
            if (displayMat.Channels() == 1)
            {
                Cv2.CvtColor(displayMat, bgrMat, ColorConversionCodes.GRAY2BGR);
            }
            else if (displayMat.Channels() == 4)
            {
                Cv2.CvtColor(displayMat, bgrMat, ColorConversionCodes.BGRA2BGR);
            }
            else
            {
                displayMat.CopyTo(bgrMat);
            }

            int width = bgrMat.Cols;
            int height = bgrMat.Rows;

            // Create BITMAPINFO for a 24bpp DIB
            var bmi = new BITMAPINFO();
            bmi.bmiHeader.biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>();
            bmi.bmiHeader.biWidth = width;
            bmi.bmiHeader.biHeight = -height; // top-down
            bmi.bmiHeader.biPlanes = 1;
            bmi.bmiHeader.biBitCount = 24;
            bmi.bmiHeader.biCompression = 0; // BI_RGB

            IntPtr hdc = CreateCompatibleDC(IntPtr.Zero);
            IntPtr hbmp = CreateDIBSection(hdc, ref bmi, 0, out IntPtr ppvBits, IntPtr.Zero, 0);
            DeleteDC(hdc);

            if (hbmp == IntPtr.Zero || ppvBits == IntPtr.Zero)
                return IntPtr.Zero;

            // Copy pixel data row by row (DIB stride is DWORD-aligned)
            int dibStride = ((width * 3 + 3) / 4) * 4;
            int matStride = (int)bgrMat.Step();

            unsafe
            {
                byte* dst = (byte*)ppvBits;
                byte* src = (byte*)bgrMat.Data;
                int copyLen = width * 3;

                for (int y = 0; y < height; y++)
                {
                    Buffer.MemoryCopy(src + y * matStride, dst + y * dibStride, dibStride, copyLen);
                }
            }

            return hbmp;
        }

        #region Native methods

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteDC(IntPtr hdc);

        [DllImport("gdi32.dll")]
        private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BITMAPINFO pbmi, uint usage, out IntPtr ppvBits, IntPtr hSection, uint offset);

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFOHEADER
        {
            public uint biSize;
            public int biWidth;
            public int biHeight;
            public ushort biPlanes;
            public ushort biBitCount;
            public uint biCompression;
            public uint biSizeImage;
            public int biXPelsPerMeter;
            public int biYPelsPerMeter;
            public uint biClrUsed;
            public uint biClrImportant;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct BITMAPINFO
        {
            public BITMAPINFOHEADER bmiHeader;
        }

        #endregion
    }
}
