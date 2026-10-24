using ColorVision.ImageEditor.Tif;
using OpenCvSharp;
using System;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
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
            if (copy != null && (copy.Rows != pixels.PixelHeight || copy.Cols != pixels.PixelWidth
                || !HasMatchingLayout(copy, pixels)
                || !string.Equals(copy.PixelSha256, ColorVisionPixelBinding.ComputeSha256(pixels), StringComparison.OrdinalIgnoreCase)))
                copy.PixelRole = "display";
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
                VExportCIE.DefaultPngCompressionLevel, VExportCIE.DefaultJpegQuality);
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
            ImageFormat format, int tiffCompression, int pngCompression, int jpegQuality)
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
                    BindPixels(parameters, src);
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

        internal static void BindPixels(ColorVisionTiffParameters parameters, Mat image)
        {
            int channels = image.Channels();
            int bytesPerSample = checked((int)image.ElemSize1());
            int width = image.Cols;
            int height = image.Rows;
            int rowLength = checked(width * channels * bytesPerSample);
            byte[] row = new byte[rowLength];
            using IncrementalHash hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            for (int y = 0; y < height; y++)
            {
                Marshal.Copy(image.Ptr(y), row, 0, row.Length);
                if (channels == 3)
                    for (int x = 0; x < width; x++)
                        for (int sampleByte = 0; sampleByte < bytesPerSample; sampleByte++)
                        {
                            int first = x * channels * bytesPerSample + sampleByte;
                            int last = first + 2 * bytesPerSample;
                            (row[first], row[last]) = (row[last], row[first]);
                        }
                hash.AppendData(row);
            }
            parameters.PixelSha256 = Convert.ToHexString(hash.GetHashAndReset());
            parameters.PixelChannels = channels;
            parameters.PixelLayout = channels == 3 ? bytesPerSample == 1 ? "Bgr24" : "Rgb48"
                : image.Depth() == MatType.CV_32F ? "Gray32Float" : bytesPerSample == 2 ? "Gray16" : "Gray8";
        }

        private static bool IsFormat(ImageFormat value, ImageFormat expected) => value.Guid == expected.Guid;
    }
}
