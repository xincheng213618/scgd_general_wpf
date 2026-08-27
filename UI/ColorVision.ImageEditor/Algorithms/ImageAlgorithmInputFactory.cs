using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ColorVision.ImageEditor.Algorithms
{
    internal static class ImageAlgorithmInputFactory
    {
        public static AlgorithmInput Acquire(ImageProcessingContext image, string name = "source")
        {
            (AlgorithmImageBuffer buffer, long revision) = AcquireCurrentFrame(image);
            return new AlgorithmInput
            {
                Name = name,
                Image = buffer,
                Ownership = AlgorithmInputOwnership.Transferred,
                SourceRevision = revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ColorSpace = "encoded-device-values",
            };
        }

        /// <summary>
        /// Captures the current document revision through ImageFrameLease while normalizing the
        /// WPF source at the only boundary that still knows its channel, alpha and palette semantics.
        /// </summary>
        public static (AlgorithmImageBuffer Image, long Revision) AcquireCurrentFrame(ImageProcessingContext image)
        {
            ArgumentNullException.ThrowIfNull(image);
            if (!image.Dispatcher.CheckAccess())
                return image.Dispatcher.Invoke(() => AcquireCurrentFrameCore(image));
            return AcquireCurrentFrameCore(image);
        }

        private static (AlgorithmImageBuffer Image, long Revision) AcquireCurrentFrameCore(ImageProcessingContext image)
        {
            using ImageFrameLease? lease = image.AcquireImageFrame();
            if (lease == null) throw new InvalidOperationException("The current image has no readable source frame.");
            BitmapSource source = image.ViewBitmapSource as BitmapSource
                ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
            if (source.PixelWidth != lease.Width || source.PixelHeight != lease.Height)
                throw new InvalidOperationException("The WPF source and leased frame dimensions do not match.");

            AlgorithmImageBuffer buffer = Copy(source);
            if (!image.IsCurrentImageRevision(lease.Revision))
            {
                buffer.Dispose();
                throw new InvalidOperationException("The source image changed while its algorithm snapshot was being captured.");
            }
            return (buffer, lease.Revision);
        }

        /// <summary>
        /// Copies a native buffer whose layout has already been normalized to the declared canonical format.
        /// HImage does not carry RGB/BGR, BGRX or alpha-mode metadata, so callers must declare the format.
        /// </summary>
        public static AlgorithmImageBuffer Copy(HImage image, AlgorithmImageFormat format, double dpiX = 96, double dpiY = 96)
        {
            if (format.Channels() != image.channels || format.BitsPerChannel() != image.depth)
                throw new ArgumentException($"Declared format {format} does not match HImage depth={image.depth}, channels={image.channels}.", nameof(format));
            int rowBytes = checked(image.cols * format.BytesPerPixel());
            int sourceStride = image.stride >= rowBytes ? image.stride : rowBytes;
            byte[] data = new byte[checked(rowBytes * image.rows)];
            for (int row = 0; row < image.rows; row++)
                Marshal.Copy(IntPtr.Add(image.pData, row * sourceStride), data, row * rowBytes, rowBytes);
            return new AlgorithmImageBuffer(image.cols, image.rows, rowBytes, format, data, dpiX, dpiY);
        }

        public static AlgorithmImageBuffer Copy(BitmapSource image)
        {
            ArgumentNullException.ThrowIfNull(image);
            if (image.Format == PixelFormats.Indexed8) return CopyIndexed8(image);

            AlgorithmImageFormat format = FromPixelFormat(image.Format);
            int stride = checked(image.PixelWidth * format.BytesPerPixel());
            byte[] data = new byte[checked(stride * image.PixelHeight)];
            image.CopyPixels(data, stride, 0);
            if (image.Format == PixelFormats.Rgb24) SwapRedBlue(data, stride, image.PixelWidth, image.PixelHeight, 1, 3);
            else if (image.Format == PixelFormats.Rgb48) SwapRedBlue(data, stride, image.PixelWidth, image.PixelHeight, 2, 3);
            else if (image.Format == PixelFormats.Rgba64) SwapRedBlue(data, stride, image.PixelWidth, image.PixelHeight, 2, 4);
            else if (image.Format == PixelFormats.Bgr32) SetOpaqueAlpha(data, stride, image.PixelWidth, image.PixelHeight);
            else if (image.Format == PixelFormats.Pbgra32) UnpremultiplyAlpha(data, stride, image.PixelWidth, image.PixelHeight);
            return new AlgorithmImageBuffer(image.PixelWidth, image.PixelHeight, stride, format, data, image.DpiX, image.DpiY);
        }

        public static WriteableBitmap ToWriteableBitmap(AlgorithmImageBuffer image)
        {
            WriteableBitmap bitmap = new(image.Width, image.Height, image.DpiX, image.DpiY, ToPixelFormat(image.Format), null);
            byte[] data = image.Data.ToArray();
            if (image.Format == AlgorithmImageFormat.Bgr48)
                SwapRedBlue(data, image.Stride, image.Width, image.Height, 2, 3);
            else if (image.Format == AlgorithmImageFormat.Bgra64)
                SwapRedBlue(data, image.Stride, image.Width, image.Height, 2, 4);
            bitmap.WritePixels(new Int32Rect(0, 0, image.Width, image.Height), data, image.Stride, 0);
            return bitmap;
        }

        public static AlgorithmImageFormat FromPixelFormat(PixelFormat format)
        {
            if (format == PixelFormats.Gray8) return AlgorithmImageFormat.Gray8;
            if (format == PixelFormats.Indexed8) return AlgorithmImageFormat.Bgra32;
            if (format == PixelFormats.Gray16) return AlgorithmImageFormat.Gray16;
            if (format == PixelFormats.Gray32Float) return AlgorithmImageFormat.Gray32Float;
            if (format == PixelFormats.Bgr24 || format == PixelFormats.Rgb24) return AlgorithmImageFormat.Bgr24;
            if (format == PixelFormats.Rgb48) return AlgorithmImageFormat.Bgr48;
            if (format == PixelFormats.Bgr32 || format == PixelFormats.Bgra32 || format == PixelFormats.Pbgra32) return AlgorithmImageFormat.Bgra32;
            if (format == PixelFormats.Rgba64) return AlgorithmImageFormat.Bgra64;
            throw new NotSupportedException($"Unsupported pixel format: {format}.");
        }

        public static PixelFormat ToPixelFormat(AlgorithmImageFormat format) => format switch
        {
            AlgorithmImageFormat.Gray8 => PixelFormats.Gray8,
            AlgorithmImageFormat.Gray16 => PixelFormats.Gray16,
            AlgorithmImageFormat.Gray32Float => PixelFormats.Gray32Float,
            AlgorithmImageFormat.Bgr24 => PixelFormats.Bgr24,
            AlgorithmImageFormat.Bgr48 => PixelFormats.Rgb48,
            AlgorithmImageFormat.Bgra32 => PixelFormats.Bgra32,
            AlgorithmImageFormat.Bgra64 => PixelFormats.Rgba64,
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        private static AlgorithmImageBuffer CopyIndexed8(BitmapSource image)
        {
            BitmapPalette palette = image.Palette
                ?? throw new NotSupportedException("Indexed8 input requires a palette.");
            int sourceStride = image.PixelWidth;
            byte[] indices = new byte[checked(sourceStride * image.PixelHeight)];
            image.CopyPixels(indices, sourceStride, 0);

            int targetStride = checked(image.PixelWidth * 4);
            byte[] data = new byte[checked(targetStride * image.PixelHeight)];
            for (int y = 0; y < image.PixelHeight; y++)
            {
                for (int x = 0; x < image.PixelWidth; x++)
                {
                    int index = indices[y * sourceStride + x];
                    if (index >= palette.Colors.Count)
                        throw new InvalidOperationException($"Indexed8 palette has no entry for index {index}.");
                    Color color = palette.Colors[index];
                    int target = y * targetStride + x * 4;
                    data[target] = color.B;
                    data[target + 1] = color.G;
                    data[target + 2] = color.R;
                    data[target + 3] = color.A;
                }
            }
            return new AlgorithmImageBuffer(image.PixelWidth, image.PixelHeight, targetStride, AlgorithmImageFormat.Bgra32, data, image.DpiX, image.DpiY);
        }

        private static void SetOpaqueAlpha(byte[] data, int stride, int width, int height)
        {
            for (int y = 0; y < height; y++)
                for (int x = 0; x < width; x++)
                    data[y * stride + x * 4 + 3] = byte.MaxValue;
        }

        private static void UnpremultiplyAlpha(byte[] data, int stride, int width, int height)
        {
            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int pixel = row + x * 4;
                    int alpha = data[pixel + 3];
                    if (alpha == 0)
                    {
                        data[pixel] = 0;
                        data[pixel + 1] = 0;
                        data[pixel + 2] = 0;
                        continue;
                    }
                    for (int component = 0; component < 3; component++)
                        data[pixel + component] = (byte)Math.Min(byte.MaxValue, (data[pixel + component] * byte.MaxValue + alpha / 2) / alpha);
                }
            }
        }

        private static void SwapRedBlue(byte[] data, int stride, int width, int height, int bytesPerChannel, int channels)
        {
            int bytesPerPixel = checked(bytesPerChannel * channels);
            for (int y = 0; y < height; y++)
            {
                int row = y * stride;
                for (int x = 0; x < width; x++)
                {
                    int pixel = row + x * bytesPerPixel;
                    for (int component = 0; component < bytesPerChannel; component++)
                        (data[pixel + component], data[pixel + 2 * bytesPerChannel + component]) = (data[pixel + 2 * bytesPerChannel + component], data[pixel + component]);
                }
            }
        }
    }
}
