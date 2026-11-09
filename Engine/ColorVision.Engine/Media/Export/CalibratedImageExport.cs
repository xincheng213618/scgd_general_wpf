using ColorVision.ImageEditor.Tif;
using OpenCvSharp;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Security.Cryptography;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.Engine.Media
{
    /// <summary>Exports one immutable image-content snapshot, independently of its original file.</summary>
    public static class CalibratedImageExport
    {
        public static string Save(BitmapSource pixels, ColorVisionTiffParameters? parameters, string fileName)
        {
            ArgumentNullException.ThrowIfNull(pixels);
            ColorVisionTiffParameters? copy = null;
            if (parameters != null) ColorVisionTiffParameters.TryParse(parameters.ToJson(), out copy);
            string? expectedPixelHash = null;
            if (copy != null)
            {
                if (copy.Rows != pixels.PixelHeight || copy.Cols != pixels.PixelWidth
                    || !HasMatchingLayout(copy, pixels) || string.IsNullOrEmpty(copy.PixelSha256))
                    copy.PixelRole = "display";
                else
                    expectedPixelHash = copy.PixelSha256;
            }
            System.Windows.Media.PixelFormat pixelFormat = pixels.Format;
            MatType type = pixelFormat == PixelFormats.Gray8 ? MatType.CV_8UC1
                : pixelFormat == PixelFormats.Gray16 ? MatType.CV_16UC1
                : pixelFormat == PixelFormats.Gray32Float ? MatType.CV_32FC1
                : pixelFormat == PixelFormats.Rgb48 ? MatType.CV_16UC3
                : pixelFormat == PixelFormats.Rgb24 || pixelFormat == PixelFormats.Bgr24 ? MatType.CV_8UC3
                : throw new NotSupportedException($"Lossless measurement export does not support {pixelFormat}.");
            using Mat image = new(pixels.PixelHeight, pixels.PixelWidth, type);
            int stride = checked((int)image.Step());
            pixels.CopyPixels(System.Windows.Int32Rect.Empty, image.Data, checked(stride * pixels.PixelHeight), stride);
            if (pixelFormat == PixelFormats.Rgb24 || pixelFormat == PixelFormats.Rgb48)
                Cv2.CvtColor(image, image, ColorConversionCodes.RGB2BGR);
            ImageFormat format = Path.GetExtension(fileName).ToLowerInvariant() switch
            {
                ".tif" or ".tiff" => ImageFormat.Tiff,
                ".png" => ImageFormat.Png,
                _ => throw new NotSupportedException("Measurement export supports TIFF and PNG only.")
            };
            return SaveCore(image, copy, fileName, format, VExportCIE.DefaultTiffCompression,
                VExportCIE.DefaultPngCompressionLevel, VExportCIE.DefaultJpegQuality, expectedPixelHash);
        }

        private static bool HasMatchingLayout(ColorVisionTiffParameters parameters, BitmapSource pixels)
        {
            bool rgb24 = pixels.Format == PixelFormats.Rgb24 || pixels.Format == PixelFormats.Bgr24;
            bool layoutMatches = string.Equals(parameters.PixelLayout, pixels.Format.ToString(), StringComparison.Ordinal)
                || rgb24 && parameters.PixelLayout is "Rgb24" or "Bgr24";
            int channels = rgb24 || pixels.Format == PixelFormats.Rgb48 ? 3 : 1;
            return layoutMatches && parameters.PixelChannels == channels
                && parameters.Bpp == pixels.Format.BitsPerPixel / channels;
        }

        internal static string SaveCore(Mat src, ColorVisionTiffParameters? parameters, string fileName,
            ImageFormat format, int tiffCompression, int pngCompression, int jpegQuality, string? expectedPixelHash = null)
        {
            string fullPath = Path.GetFullPath(fileName);
            Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
            string temporaryPath = Path.Combine(Path.GetDirectoryName(fullPath)!, $".__colorvision_{Guid.NewGuid():N}{Path.GetExtension(fullPath)}");
            try
            {
                // PNG has no floating point sample representation. Never let the codec silently quantize XYZ.
                if (IsFormat(format, ImageFormat.Png) && src.Depth() != MatType.CV_8U && src.Depth() != MatType.CV_16U)
                    throw new NotSupportedException("PNG cannot preserve floating point measurement samples; export TIFF instead.");
                if (parameters != null)
                {
                    BindPixels(parameters, src);
                    if (expectedPixelHash != null && !string.Equals(expectedPixelHash, parameters.PixelSha256, StringComparison.OrdinalIgnoreCase))
                        parameters.PixelRole = "display";
                }
                if (IsFormat(format, ImageFormat.Tiff))
                {
                    if (!Cv2.ImWrite(temporaryPath, src, [new ImageEncodingParam(ImwriteFlags.TiffCompression, tiffCompression)]))
                        throw new IOException("Unable to encode TIFF image.");
                    if (parameters != null) TiffImageDescriptionWriter.Write(temporaryPath, parameters.ToJson());
                }
                else if (IsFormat(format, ImageFormat.Png))
                {
                    bool written = pngCompression == VExportCIE.AutomaticPngCompressionLevel
                        ? Cv2.ImWrite(temporaryPath, src)
                        : Cv2.ImWrite(temporaryPath, src, [new ImageEncodingParam(ImwriteFlags.PngCompression, pngCompression)]);
                    if (!written) throw new IOException("Unable to encode PNG image.");
                    if (parameters != null) PngParameterWriter.Write(temporaryPath, parameters.ToJson());
                }
                else if (IsFormat(format, ImageFormat.Bmp) || IsFormat(format, ImageFormat.Jpeg))
                {
                    using Mat image = VExportCIE.CreateBmpCompatibleMat(src);
                    bool written = IsFormat(format, ImageFormat.Jpeg)
                        ? Cv2.ImWrite(temporaryPath, image, [new ImageEncodingParam(ImwriteFlags.JpegQuality, jpegQuality)])
                        : Cv2.ImWrite(temporaryPath, image);
                    if (!written) throw new IOException("Unable to encode image.");
                }
                else throw new NotSupportedException($"Unsupported export image format: {format}.");
                File.Move(temporaryPath, fullPath, overwrite: true);
                return fullPath;
            }
            finally
            {
                if (File.Exists(temporaryPath)) File.Delete(temporaryPath);
            }
        }

        internal static unsafe void BindPixels(ColorVisionTiffParameters parameters, Mat image)
        {
            int channels = image.Channels();
            int bytesPerSample = checked((int)image.ElemSize1());
            int width = image.Cols;
            int height = image.Rows;
            int rowLength = checked(width * channels * bytesPerSample);
            MatType depth = image.Depth();
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            if (channels != 3)
            {
                if (image.IsContinuous() && (long)rowLength * height <= int.MaxValue)
                    hash.AppendData(new ReadOnlySpan<byte>((void*)image.Data, rowLength * height));
                else
                    for (int row = 0; row < height; row++)
                        hash.AppendData(new ReadOnlySpan<byte>((void*)image.Ptr(row), rowLength));
            }
            else if (rowLength > 0 && (depth == MatType.CV_8U || depth == MatType.CV_16U || depth == MatType.CV_32F))
            {
                // Hash canonical RGB without changing encoder pixels or allocating another full frame.
                int rowsPerBlock = Math.Max(1, (512 * 1024) / rowLength);
                using Mat rgb = new();
                for (int top = 0; top < height;)
                {
                    int rows = Math.Min(rowsPerBlock, height - top);
                    using Mat bgr = image.RowRange(top, top + rows);
                    Cv2.CvtColor(bgr, rgb, ColorConversionCodes.BGR2RGB);
                    hash.AppendData(new ReadOnlySpan<byte>((void*)rgb.Data, rowLength * rows));
                    top += rows;
                }
            }
            else
            {
                // Keep byte-wise canonicalization for depths unsupported by CvtColor.
                byte[] row = new byte[rowLength];
                for (int y = 0; y < height; y++)
                {
                    new ReadOnlySpan<byte>((void*)image.Ptr(y), rowLength).CopyTo(row);
                    for (int x = 0; x < width; x++)
                        for (int sampleByte = 0; sampleByte < bytesPerSample; sampleByte++)
                        {
                            int first = x * channels * bytesPerSample + sampleByte;
                            int last = first + 2 * bytesPerSample;
                            (row[first], row[last]) = (row[last], row[first]);
                        }
                    hash.AppendData(row);
                }
            }
            parameters.PixelSha256 = Convert.ToHexString(hash.GetHashAndReset());
            parameters.PixelChannels = channels;
            parameters.PixelLayout = channels == 3 ? bytesPerSample == 1 ? "Bgr24" : "Rgb48"
                : depth == MatType.CV_32F ? "Gray32Float" : bytesPerSample == 2 ? "Gray16" : "Gray8";
        }

        private static bool IsFormat(ImageFormat value, ImageFormat expected) => value.Guid == expected.Guid;
    }
}
