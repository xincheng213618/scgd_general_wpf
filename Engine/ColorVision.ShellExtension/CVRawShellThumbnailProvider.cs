using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using ColorVision.FileIO;
using ColorVision.ShellExtension.Interop;

namespace ColorVision.ShellExtension
{
    /// <summary>
    /// Windows Shell Thumbnail Provider for .cvraw and .cvcie files.
    /// This COM class is loaded by Windows Explorer to generate file thumbnails.
    /// It reads the custom image format using ColorVision.FileIO and returns
    /// an HBITMAP that Explorer displays as the file thumbnail.
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
                    return E_FAIL;

                if (cx == 0)
                    return E_INVALIDARG;

                // Read file into memory to avoid file locking issues
                byte[] fileData = File.ReadAllBytes(_filePath);

                int index = CVFileUtil.ReadCIEFileHeader(fileData, out CVCIEFile fileInfo);
                if (index <= 0)
                    return E_FAIL;

                try
                {
                    bool ret = CVFileUtil.ReadCIEFileData(fileData, ref fileInfo, index);
                    if (!ret || fileInfo.Data == null)
                        return E_FAIL;

                    using Bitmap? sourceBitmap = CreateBitmapFromCVFile(fileInfo);
                    if (sourceBitmap == null)
                        return E_FAIL;

                    // Calculate thumbnail dimensions maintaining aspect ratio
                    int width = sourceBitmap.Width;
                    int height = sourceBitmap.Height;
                    double scale = Math.Min((double)cx / width, (double)cx / height);
                    scale = Math.Min(scale, 1.0); // Don't upscale
                    int thumbWidth = Math.Max(1, (int)(width * scale));
                    int thumbHeight = Math.Max(1, (int)(height * scale));

                    // Create resized thumbnail
                    using var thumbnail = new Bitmap(thumbWidth, thumbHeight, PixelFormat.Format24bppRgb);
                    using (var g = Graphics.FromImage(thumbnail))
                    {
                        g.InterpolationMode = InterpolationMode.HighQualityBilinear;
                        g.CompositingQuality = CompositingQuality.HighQuality;
                        g.DrawImage(sourceBitmap, 0, 0, thumbWidth, thumbHeight);
                    }

                    // Return HBITMAP - Explorer takes ownership and will call DeleteObject
                    phbmp = thumbnail.GetHbitmap();
                    return phbmp != IntPtr.Zero ? S_OK : E_FAIL;
                }
                finally
                {
                    fileInfo.Dispose();
                }
            }
            catch
            {
                // Shell extensions must never throw - return error HRESULT
                return E_FAIL;
            }
        }

        /// <summary>
        /// Creates a System.Drawing.Bitmap from CVCIEFile data.
        /// Handles different file types (Tif, Raw, Src, CIE) and bit depths (8, 16, 32).
        /// </summary>
        private static Bitmap? CreateBitmapFromCVFile(CVCIEFile fileInfo)
        {
            try
            {
                // For TIFF-encoded data, decode directly
                if (fileInfo.FileExtType == CVType.Tif)
                {
                    using var ms = new MemoryStream(fileInfo.Data);
                    return new Bitmap(ms);
                }

                // For Raw/Src/CIE formats, convert pixel data to bitmap
                if (fileInfo.FileExtType == CVType.Raw || fileInfo.FileExtType == CVType.Src || fileInfo.FileExtType == CVType.CIE)
                {
                    int width = fileInfo.Cols;
                    int height = fileInfo.Rows;
                    if (width <= 0 || height <= 0)
                        return null;

                    byte[] grayPixels = NormalizeTo8BitGrayscale(fileInfo);
                    if (grayPixels is null)
                        return null;

                    return CreateGrayscaleBitmap(grayPixels, width, height);
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Normalizes pixel data of any supported bit depth to 8-bit grayscale.
        /// For multi-channel CIE data, uses only the first channel (luminance/Y).
        /// </summary>
        private static byte[]? NormalizeTo8BitGrayscale(CVCIEFile fileInfo)
        {
            int width = fileInfo.Cols;
            int height = fileInfo.Rows;
            int channels = fileInfo.Channels;
            byte[] data = fileInfo.Data;

            if (data == null || width <= 0 || height <= 0 || channels <= 0)
                return null;

            int pixelCount = width * height;

            // For CIE files with 3 channels, use first channel only (Y/luminance)
            int effectiveChannels = (fileInfo.FileExtType == CVType.CIE && channels == 3) ? 1 : channels;
            if (effectiveChannels > 1)
                effectiveChannels = 1; // For any multi-channel, use first channel

            byte[] result = new byte[pixelCount];
            int bytesPerSample = fileInfo.Bpp / 8;
            int stride = channels * bytesPerSample;

            switch (fileInfo.Bpp)
            {
                case 8:
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int srcIdx = i * stride; // stride = channels * bytesPerSample
                        if (srcIdx < data.Length)
                            result[i] = data[srcIdx];
                    }
                    break;

                case 16:
                {
                    // First pass: find min/max for normalization
                    ushort min = ushort.MaxValue, max = 0;
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int srcIdx = i * stride;
                        if (srcIdx + 1 < data.Length)
                        {
                            ushort val = BitConverter.ToUInt16(data, srcIdx);
                            if (val < min) min = val;
                            if (val > max) max = val;
                        }
                    }

                    float range = max - min;
                    if (range < 1) range = 1;

                    // Second pass: normalize to 0-255
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int srcIdx = i * stride;
                        if (srcIdx + 1 < data.Length)
                        {
                            ushort val = BitConverter.ToUInt16(data, srcIdx);
                            result[i] = (byte)(255.0f * (val - min) / range);
                        }
                    }
                    break;
                }

                case 32:
                {
                    // First pass: find min/max (skip NaN/Infinity)
                    float fmin = float.MaxValue, fmax = float.MinValue;
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int srcIdx = i * stride;
                        if (srcIdx + 3 < data.Length)
                        {
                            float val = BitConverter.ToSingle(data, srcIdx);
                            if (!float.IsNaN(val) && !float.IsInfinity(val))
                            {
                                if (val < fmin) fmin = val;
                                if (val > fmax) fmax = val;
                            }
                        }
                    }

                    float frange = fmax - fmin;
                    if (frange < float.Epsilon) frange = 1;

                    // Second pass: normalize to 0-255
                    for (int i = 0; i < pixelCount; i++)
                    {
                        int srcIdx = i * stride;
                        if (srcIdx + 3 < data.Length)
                        {
                            float val = BitConverter.ToSingle(data, srcIdx);
                            if (float.IsNaN(val) || float.IsInfinity(val))
                                result[i] = 0;
                            else
                                result[i] = (byte)Math.Clamp(255.0f * (val - fmin) / frange, 0, 255);
                        }
                    }
                    break;
                }

                default:
                    return null;
            }

            return result;
        }

        /// <summary>
        /// Creates a 24bpp RGB Bitmap from 8-bit grayscale pixel data (R=G=B=gray).
        /// </summary>
        private static Bitmap? CreateGrayscaleBitmap(byte[] grayPixels, int width, int height)
        {
            if (grayPixels.Length < width * height)
                return null;

            var bitmap = new Bitmap(width, height, PixelFormat.Format24bppRgb);
            var bmpData = bitmap.LockBits(
                new Rectangle(0, 0, width, height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);

            try
            {
                int bmpStride = bmpData.Stride;
                byte[] bmpPixels = new byte[bmpStride * height];

                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        byte gray = grayPixels[y * width + x];
                        int bmpIdx = y * bmpStride + x * 3;
                        bmpPixels[bmpIdx] = gray;     // B
                        bmpPixels[bmpIdx + 1] = gray;  // G
                        bmpPixels[bmpIdx + 2] = gray;  // R
                    }
                }

                Marshal.Copy(bmpPixels, 0, bmpData.Scan0, bmpPixels.Length);
            }
            finally
            {
                bitmap.UnlockBits(bmpData);
            }

            return bitmap;
        }
    }
}
