using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using ColorVision.FileIO;
using ColorVision.ShellExtension.Interop;
using OpenCvSharp;

namespace ColorVision.ShellExtension
{
    /// <summary>
    /// Windows Shell Thumbnail Provider for .cvraw and .cvcie files.
    /// This COM class is loaded by Windows Explorer to generate file thumbnails.
    /// Implements IInitializeWithStream (preferred by Win11 process isolation)
    /// and IInitializeWithFile (fallback when process isolation is disabled).
    /// </summary>
    [ComVisible(true)]
    [Guid("7B5E2A3C-8F1D-4E6A-B9C2-1D3E5F7A8B9C")]
    [ClassInterface(ClassInterfaceType.None)]
    public class CVRawShellThumbnailProvider : IShellThumbnailProvider, IInitializeWithStream, IInitializeWithFile
    {
        private const int S_OK = 0;
        private const int E_FAIL = unchecked((int)0x80004005);
        private const int E_INVALIDARG = unchecked((int)0x80070057);

        private string? _filePath;
        private byte[]? _streamData;

        /// <summary>
        /// Called by Explorer (with process isolation) to provide file data as a stream.
        /// This is preferred on Windows 11.
        /// </summary>
        public int Initialize(IStream pstream, uint grfMode)
        {
            try
            {
                ShellLog.Log($"IInitializeWithStream.Initialize called, grfMode={grfMode}");
                _streamData = ReadAllBytesFromStream(pstream);
                ShellLog.Log($"IInitializeWithStream.Initialize: read {_streamData?.Length ?? 0} bytes");
                return _streamData != null ? S_OK : E_FAIL;
            }
            catch (Exception ex)
            {
                ShellLog.Log($"IInitializeWithStream.Initialize exception: {ex}");
                return E_FAIL;
            }
        }

        /// <summary>
        /// Called by Explorer (without process isolation) to provide the file path.
        /// </summary>
        public int Initialize(string pszFilePath, uint grfMode)
        {
            if (string.IsNullOrEmpty(pszFilePath))
                return E_INVALIDARG;
            _filePath = pszFilePath;
            ShellLog.Log($"IInitializeWithFile.Initialize: {pszFilePath}");
            return S_OK;
        }

        /// <summary>
        /// Called by Explorer to get the thumbnail bitmap.
        /// Handles both stream-based (Win11 process isolation) and file-based initialization.
        /// </summary>
        public int GetThumbnail(uint cx, out IntPtr phbmp, out uint pdwAlpha)
        {
            phbmp = IntPtr.Zero;
            pdwAlpha = WtsAlphaType.WTSAT_RGB;

            try
            {
                if (cx == 0)
                    return E_INVALIDARG;

                CVCIEFile fileInfo;
                int index;

                // Prefer stream data (from IInitializeWithStream), fall back to file path
                if (_streamData != null)
                {
                    ShellLog.Log($"GetThumbnail: using stream data ({_streamData.Length} bytes), cx={cx}");
                    index = CVFileUtil.ReadCIEFileHeader(_streamData, out fileInfo);
                    if (index <= 0)
                    {
                        ShellLog.Log($"GetThumbnail: ReadCIEFileHeader(bytes) returned {index}");
                        return E_FAIL;
                    }
                    // Infer file type from channels: 3-channel data is CIE, otherwise Raw
                    if (fileInfo.Channels == 3)
                        fileInfo.FileExtType = CVType.CIE;
                    else
                        fileInfo.FileExtType = CVType.Raw;

                    // Override with file path extension if available
                    if (!string.IsNullOrEmpty(_filePath))
                    {
                        if (_filePath.Contains(".cvcie", StringComparison.OrdinalIgnoreCase))
                            fileInfo.FileExtType = CVType.CIE;
                        else if (_filePath.Contains(".cvraw", StringComparison.OrdinalIgnoreCase))
                            fileInfo.FileExtType = CVType.Raw;
                        else if (_filePath.Contains(".cvsrc", StringComparison.OrdinalIgnoreCase))
                            fileInfo.FileExtType = CVType.Src;
                    }
                }
                else if (!string.IsNullOrEmpty(_filePath) && File.Exists(_filePath))
                {
                    ShellLog.Log($"GetThumbnail: using file path={_filePath}, cx={cx}");
                    index = CVFileUtil.ReadCIEFileHeader(_filePath, out fileInfo);
                    if (index <= 0)
                    {
                        ShellLog.Log($"GetThumbnail: ReadCIEFileHeader(path) returned {index} for {_filePath}");
                        return E_FAIL;
                    }
                }
                else
                {
                    ShellLog.Log($"GetThumbnail: no data available (stream={_streamData != null}, path={_filePath})");
                    return E_FAIL;
                }

                Mat? mat = null;
                Mat? thumbnail = null;

                try
                {
                    bool ret;
                    if (_streamData != null)
                        ret = CVFileUtil.ReadCIEFileData(_streamData, ref fileInfo, index);
                    else
                        ret = CVFileUtil.ReadCIEFileData(_filePath!, ref fileInfo, index);

                    if (!ret || fileInfo.Data == null)
                    {
                        ShellLog.Log($"GetThumbnail: ReadCIEFileData failed");
                        return E_FAIL;
                    }

                    ShellLog.Log($"GetThumbnail: data read OK, type={fileInfo.FileExtType}, {fileInfo.Cols}x{fileInfo.Rows}, bpp={fileInfo.Bpp}, ch={fileInfo.Channels}");

                    mat = CreateMatFromCVCIEFile(fileInfo);
                    if (mat == null || mat.Empty())
                    {
                        ShellLog.Log($"GetThumbnail: CreateMatFromCVCIEFile returned null/empty");
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
                        ShellLog.Log($"GetThumbnail: MatToHBitmap returned null");
                        return E_FAIL;
                    }

                    ShellLog.Log($"GetThumbnail: success, {thumbWidth}x{thumbHeight}");
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
                ShellLog.Log($"GetThumbnail: exception: {ex}");
                return E_FAIL;
            }
        }

        /// <summary>
        /// Reads all bytes from a COM IStream.
        /// </summary>
        private static byte[]? ReadAllBytesFromStream(IStream stream)
        {
            try
            {
                // Get stream size via Stat
                stream.Stat(out STATSTG stat, 1); // STATFLAG_NONAME = 1
                long size = stat.cbSize;

                if (size <= 0 || size > int.MaxValue)
                    return null;

                // Seek to beginning
                stream.Seek(0, 0, IntPtr.Zero); // STREAM_SEEK_SET = 0

                byte[] buffer = new byte[size];
                IntPtr bytesReadPtr = Marshal.AllocCoTaskMem(sizeof(long));
                try
                {
                    Marshal.WriteInt64(bytesReadPtr, 0);
                    stream.Read(buffer, (int)size, bytesReadPtr);
                    long bytesRead = Marshal.ReadInt64(bytesReadPtr);
                    if (bytesRead != size)
                    {
                        // Partial read - resize buffer
                        if (bytesRead > 0)
                        {
                            byte[] partial = new byte[bytesRead];
                            Buffer.BlockCopy(buffer, 0, partial, 0, (int)bytesRead);
                            return partial;
                        }
                        return null;
                    }
                }
                finally
                {
                    Marshal.FreeCoTaskMem(bytesReadPtr);
                }

                return buffer;
            }
            catch (Exception ex)
            {
                ShellLog.Log($"ReadAllBytesFromStream error: {ex.Message}");
                return null;
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
                    }
                    else
                    {
                        src = Mat.FromPixelData(fileInfo.Rows, fileInfo.Cols, MatType.MakeType(fileInfo.Depth, fileInfo.Channels), fileInfo.Data);
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

                if (matStride == dibStride)
                {
                    // Strides match - copy entire image in one call
                    Buffer.MemoryCopy(src, dst, (long)dibStride * height, (long)dibStride * height);
                }
                else
                {
                    for (int y = 0; y < height; y++)
                    {
                        Buffer.MemoryCopy(src + y * matStride, dst + y * dibStride, dibStride, copyLen);
                    }
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
