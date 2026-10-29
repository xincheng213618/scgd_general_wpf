using ColorVision.Algorithms;
using ColorVision.Core;
using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Threading;
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

        public static AlgorithmInput Acquire(
            ImageProcessingContext image,
            ImageSelectionScope? expectedScope,
            string name = "source")
        {
            AlgorithmInput input = Acquire(image, name);
            if (expectedScope == null)
            {
                return input;
            }

            bool revisionMatches = long.TryParse(
                input.SourceRevision,
                System.Globalization.NumberStyles.Integer,
                System.Globalization.CultureInfo.InvariantCulture,
                out long revision)
                && revision == expectedScope.SourceRevision;
            bool scopeMatches = image.DocumentInstanceId == expectedScope.DocumentInstanceId
                && revisionMatches
                && input.Image.Width == expectedScope.PixelWidth
                && input.Image.Height == expectedScope.PixelHeight
                && input.Image.DpiX.Equals(expectedScope.DpiX)
                && input.Image.DpiY.Equals(expectedScope.DpiY)
                && image.IsCurrentImageRevision(expectedScope.SourceRevision);
            if (scopeMatches)
            {
                return input;
            }

            input.Image.Dispose();
            throw new InvalidOperationException("The image changed while the ROI was being selected. Select the ROI again on the current image.");
        }

        /// <summary>
        /// Captures the shared document revision without materializing a native frame when the
        /// host supports source-state capture. WPF still owns channel, alpha and palette semantics.
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
            if (image.CaptureSourceState() is { } state)
            {
                BitmapSource capturedSource = state.Source as BitmapSource
                    ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
                return CopyCurrentSource(image, capturedSource, state.Revision);
            }

            // Older hosts provide only native leases. Keep their lifetime and layout validation.
            using ImageFrameLease? lease = image.AcquireImageFrame();
            if (lease == null) throw new InvalidOperationException("The current image has no readable source frame.");
            BitmapSource source = image.ViewBitmapSource as BitmapSource
                ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
            if (source.PixelWidth != lease.Width || source.PixelHeight != lease.Height)
                throw new InvalidOperationException("The WPF source and leased frame dimensions do not match.");

            return CopyCurrentSource(image, source, lease.Revision);
        }

        private static (AlgorithmImageBuffer Image, long Revision) CopyCurrentSource(
            ImageProcessingContext image, BitmapSource source, long revision)
        {
            AlgorithmImageBuffer buffer = Copy(source);
            if (!image.IsCurrentImageRevision(revision) || !ReferenceEquals(image.ViewBitmapSource, source))
            {
                buffer.Dispose();
                throw new InvalidOperationException("The source image changed while its algorithm snapshot was being captured.");
            }
            return (buffer, revision);
        }

        /// <summary>
        /// Copies a native buffer whose layout has already been normalized to the declared canonical format.
        /// HImage does not carry RGB/BGR, BGRX or alpha-mode metadata, so callers must declare the format.
        /// </summary>
        public static AlgorithmImageBuffer Copy(HImage image, AlgorithmImageFormat format, double dpiX = 96, double dpiY = 96)
        {
            if (image.pData == IntPtr.Zero || image.rows <= 0 || image.cols <= 0)
                throw new ArgumentException("HImage must contain a nonempty pixel buffer with positive dimensions.", nameof(image));
            if (format.Channels() != image.channels || format.BitsPerChannel() != image.depth)
                throw new ArgumentException($"Declared format {format} does not match HImage depth={image.depth}, channels={image.channels}.", nameof(format));
            int rowBytes = checked(image.cols * format.BytesPerPixel());
            int sourceStride = image.stride == 0 ? rowBytes : image.stride;
            if (sourceStride < rowBytes)
                throw new ArgumentException("HImage stride must be zero or at least the size of one pixel row.", nameof(image));
            byte[] data = new byte[checked(rowBytes * image.rows)];
            for (int row = 0; row < image.rows; row++)
                Marshal.Copy(new IntPtr(checked(image.pData.ToInt64() + (long)row * sourceStride)), data, row * rowBytes, rowBytes);
            return new AlgorithmImageBuffer(image.cols, image.rows, rowBytes, format, data, dpiX, dpiY);
        }

        public static unsafe AlgorithmImageBuffer Copy(BitmapSource image)
        {
            ArgumentNullException.ThrowIfNull(image);
            if (image.Format == PixelFormats.Indexed8) return CopyIndexed8(image);

            AlgorithmImageFormat format = FromPixelFormat(image.Format);
            int stride = checked(image.PixelWidth * format.BytesPerPixel());
            byte[] data = new byte[checked(stride * image.PixelHeight)];
            image.CopyPixels(data, stride, 0);
            if (image.Format == PixelFormats.Rgb24) SwapRedBlue(data, stride, image.PixelWidth, image.PixelHeight, 1, 3);
            else if (image.Format == PixelFormats.Rgb48 || image.Format == PixelFormats.Rgba64)
            {
                fixed (byte* pixels = data)
                    CopyColor16(pixels, stride, pixels, stride, image.PixelWidth, image.PixelHeight, format.BytesPerPixel());
            }
            else if (image.Format == PixelFormats.Bgr32) SetOpaqueAlpha(data, stride, image.PixelWidth, image.PixelHeight);
            else if (image.Format == PixelFormats.Pbgra32) UnpremultiplyAlpha(data, stride, image.PixelWidth, image.PixelHeight);
            return new AlgorithmImageBuffer(image.PixelWidth, image.PixelHeight, stride, format, data, image.DpiX, image.DpiY);
        }

        public static unsafe WriteableBitmap ToWriteableBitmap(AlgorithmImageBuffer image)
        {
            WriteableBitmap bitmap = new(image.Width, image.Height, image.DpiX, image.DpiY, ToPixelFormat(image.Format), null);
            Int32Rect bounds = new(0, 0, image.Width, image.Height);
            if (image.Format is AlgorithmImageFormat.Bgr48 or AlgorithmImageFormat.Bgra64)
            {
                bitmap.Lock();
                try
                {
                    using MemoryHandle pin = image.Data.Pin();
                    CopyColor16((byte*)pin.Pointer, image.Stride, (byte*)bitmap.BackBuffer, bitmap.BackBufferStride,
                        image.Width, image.Height, image.Format.BytesPerPixel());
                    bitmap.AddDirtyRect(bounds);
                }
                finally { bitmap.Unlock(); }
            }
            else
            {
                ReadOnlyMemory<byte> data = image.Data;
                using MemoryHandle pin = data.Pin();
                bitmap.WritePixels(bounds, (IntPtr)pin.Pointer, data.Length, image.Stride);
            }
            return bitmap;
        }

        // Supports disjoint output and in-place normalization of a private snapshot.
        internal static unsafe void CopyColor16(byte* sourceBase, int sourceStride, byte* destinationBase, int destinationStride,
            int width, int height, int pixelBytes, CancellationToken cancellationToken = default)
        {
            Vector128<byte> swap = pixelBytes == 6
                ? Vector128.Create((byte)4, 5, 2, 3, 0, 1, 10, 11, 8, 9, 6, 7, 128, 128, 128, 128)
                : Vector128.Create((byte)4, 5, 2, 3, 0, 1, 6, 7, 12, 13, 10, 11, 8, 9, 14, 15);
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte* source = sourceBase + (long)y * sourceStride;
                byte* destination = destinationBase + (long)y * destinationStride;
                int x = 0;
                if (Ssse3.IsSupported)
                {
                    if (pixelBytes == 6)
                    {
                        // A 16-byte load needs three valid 6-byte pixels. Emit exactly two pixels.
                        for (; x + 2 < width; x += 2)
                        {
                            Vector128<byte> pixels = Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(source), swap);
                            Unsafe.WriteUnaligned(destination, pixels.AsUInt64().GetElement(0));
                            Unsafe.WriteUnaligned(destination + 8, pixels.AsUInt32().GetElement(2));
                            source += 12;
                            destination += 12;
                        }
                    }
                    else
                    {
                        for (; x + 1 < width; x += 2)
                        {
                            Vector128<byte> pixels = Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(source), swap);
                            Unsafe.WriteUnaligned(destination, pixels);
                            source += 16;
                            destination += 16;
                        }
                    }
                }
                for (; x < width; x++)
                {
                    byte blueLow = source[0];
                    byte blueHigh = source[1];
                    destination[0] = source[4];
                    destination[1] = source[5];
                    destination[2] = source[2];
                    destination[3] = source[3];
                    destination[4] = blueLow;
                    destination[5] = blueHigh;
                    if (pixelBytes == 8)
                    {
                        destination[6] = source[6];
                        destination[7] = source[7];
                    }
                    source += pixelBytes;
                    destination += pixelBytes;
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
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
