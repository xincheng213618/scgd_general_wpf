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
            => CreateInput(AcquireCurrentFrame(image), name);

        private static AlgorithmInput CreateInput((AlgorithmImageBuffer Image, long Revision) frame, string name)
        {
            return new AlgorithmInput
            {
                Name = name,
                Image = frame.Image,
                Ownership = AlgorithmInputOwnership.Transferred,
                SourceRevision = frame.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
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

        internal static AlgorithmInput AcquireRegion(ImageProcessingContext image, ImageSelectionScope scope, Int32Rect region)
        {
            if (TransientRoiSelectionSession.IsSourceScopeCurrent(image, scope))
            {
                var frame = AcquireCurrentFrame(image, region);
                if (frame.Revision == scope.SourceRevision && TransientRoiSelectionSession.IsSourceScopeCurrent(image, scope))
                    return CreateInput(frame, "source");

                frame.Image.Dispose();
            }

            throw new InvalidOperationException("The image changed while the ROI was being selected. Select the ROI again on the current image.");
        }

        /// <summary>
        /// Captures the shared document revision without materializing a native frame when the
        /// host supports source-state capture. WPF still owns channel, alpha and palette semantics.
        /// </summary>
        public static (AlgorithmImageBuffer Image, long Revision) AcquireCurrentFrame(ImageProcessingContext image)
            => AcquireCurrentFrame(image, null);

        public static (AlgorithmImageBuffer Image, long Revision) AcquireCurrentFrame(ImageProcessingContext image, Int32Rect? region)
        {
            ArgumentNullException.ThrowIfNull(image);
            if (!image.Dispatcher.CheckAccess())
                return image.Dispatcher.Invoke(() => AcquireCurrentFrameCore(image, region));
            return AcquireCurrentFrameCore(image, region);
        }

        private static (AlgorithmImageBuffer Image, long Revision) AcquireCurrentFrameCore(ImageProcessingContext image, Int32Rect? region)
        {
            if (image.CaptureSourceState() is { } state)
            {
                BitmapSource capturedSource = state.Source as BitmapSource
                    ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
                return CopyCurrentSource(image, capturedSource, state.Revision, region);
            }

            // Older hosts provide only native leases. Keep their lifetime and layout validation.
            using ImageFrameLease? lease = image.AcquireImageFrame();
            if (lease == null) throw new InvalidOperationException("The current image has no readable source frame.");
            BitmapSource source = image.ViewBitmapSource as BitmapSource
                ?? throw new InvalidOperationException("The current image has no WPF bitmap source.");
            if (source.PixelWidth != lease.Width || source.PixelHeight != lease.Height)
                throw new InvalidOperationException("The WPF source and leased frame dimensions do not match.");

            return CopyCurrentSource(image, source, lease.Revision, region);
        }

        private static (AlgorithmImageBuffer Image, long Revision) CopyCurrentSource(
            ImageProcessingContext image, BitmapSource source, long revision, Int32Rect? region)
        {
            AlgorithmImageBuffer buffer = Copy(source, region);
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
            byte[] data = GC.AllocateUninitializedArray<byte>(checked(rowBytes * image.rows));
            if (sourceStride == rowBytes)
                Marshal.Copy(image.pData, data, 0, data.Length);
            else
                for (int row = 0; row < image.rows; row++)
                    Marshal.Copy(new IntPtr(checked(image.pData.ToInt64() + (long)row * sourceStride)), data, row * rowBytes, rowBytes);
            return new AlgorithmImageBuffer(image.cols, image.rows, rowBytes, format, data, dpiX, dpiY);
        }

        public static AlgorithmImageBuffer Copy(BitmapSource image) => Copy(image, null);

        public static unsafe AlgorithmImageBuffer Copy(BitmapSource image, Int32Rect? region)
        {
            ArgumentNullException.ThrowIfNull(image);
            Int32Rect bounds = region ?? new Int32Rect(0, 0, image.PixelWidth, image.PixelHeight);
            int width = bounds.Width, height = bounds.Height;
            PixelFormat sourceFormat = image.Format;
            if (sourceFormat == PixelFormats.Indexed8) return CopyIndexed8(image, bounds);

            AlgorithmImageFormat format = FromPixelFormat(sourceFormat);
            int stride = checked(width * format.BytesPerPixel());
            byte[] data = GC.AllocateUninitializedArray<byte>(checked(stride * height));
            image.CopyPixels(bounds, data, stride, 0);
            NormalizePackedPixels(data, sourceFormat, width, height, stride, format.BytesPerPixel());
            return new AlgorithmImageBuffer(width, height, stride, format, data, image.DpiX, image.DpiY);
        }

        internal static unsafe void NormalizePackedPixels(byte[] data, PixelFormat sourceFormat, int width, int height,
            int stride, int pixelBytes)
        {
            if (sourceFormat == PixelFormats.Rgb24) SwapRedBlue24(data);
            else if (sourceFormat == PixelFormats.Rgb48 || sourceFormat == PixelFormats.Rgba64)
            {
                fixed (byte* pixels = data)
                    CopyColor16(pixels, stride, pixels, stride, width, height, pixelBytes);
            }
            else if (sourceFormat == PixelFormats.Bgr32) SetOpaqueAlpha(data);
            else if (sourceFormat == PixelFormats.Pbgra32) UnpremultiplyAlpha(data);
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

        private static AlgorithmImageBuffer CopyIndexed8(BitmapSource image, Int32Rect bounds)
        {
            BitmapPalette palette = image.Palette
                ?? throw new NotSupportedException("Indexed8 input requires a palette.");
            int pixelCount = checked(bounds.Width * bounds.Height);
            int targetStride = checked(bounds.Width * 4);
            byte[] data = GC.AllocateUninitializedArray<byte>(checked(targetStride * bounds.Height));
            // Each forward BGRA write ends before the next index in the tail of the output buffer.
            int indicesOffset = data.Length - pixelCount;
            image.CopyPixels(bounds, data, bounds.Width, indicesOffset);
            var colors = palette.Colors;
            Span<uint> paletteValues = stackalloc uint[colors.Count];
            for (int i = 0; i < paletteValues.Length; i++)
            {
                Color color = colors[i];
                paletteValues[i] = color.B | (uint)color.G << 8 | (uint)color.R << 16 | (uint)color.A << 24;
            }
            ReadOnlySpan<byte> indices = data.AsSpan(indicesOffset);
            Span<uint> pixels = MemoryMarshal.Cast<byte, uint>(data.AsSpan());
            for (int i = 0; i < pixels.Length; i++)
            {
                int index = indices[i];
                if (index >= paletteValues.Length)
                    throw new InvalidOperationException($"Indexed8 palette has no entry for index {index}.");
                pixels[i] = paletteValues[index];
            }
            return new AlgorithmImageBuffer(bounds.Width, bounds.Height, targetStride, AlgorithmImageFormat.Bgra32, data, image.DpiX, image.DpiY);
        }

        private static void SetOpaqueAlpha(byte[] data)
        {
            const uint opaque = 0xff000000;
            Span<uint> pixels = MemoryMarshal.Cast<byte, uint>(data.AsSpan());
            int offset = 0;
            if (Vector128.IsHardwareAccelerated)
            {
                Vector128<uint> alpha = Vector128.Create(opaque);
                Span<Vector128<uint>> blocks = MemoryMarshal.Cast<uint, Vector128<uint>>(pixels);
                for (int i = 0; i < blocks.Length; i++) blocks[i] |= alpha;
                offset = blocks.Length * Vector128<uint>.Count;
            }
            for (; offset < pixels.Length; offset++) pixels[offset] |= opaque;
        }

        private static void UnpremultiplyAlpha(byte[] data)
        {
            Span<uint> pixels = MemoryMarshal.Cast<byte, uint>(data.AsSpan());
            byte[] values = StraightColorTable.Values;
            for (int i = 0; i < pixels.Length; i++)
            {
                uint pixel = pixels[i];
                int alpha = (int)(pixel >> 24);
                if (alpha == byte.MaxValue) continue;
                if (alpha == 0)
                {
                    pixels[i] = 0;
                    continue;
                }
                int offset = alpha << 8;
                pixels[i] = (pixel & 0xff000000u) | values[offset + (byte)pixel]
                    | (uint)values[offset + (byte)(pixel >> 8)] << 8
                    | (uint)values[offset + (byte)(pixel >> 16)] << 16;
            }
        }

        private static class StraightColorTable
        {
            public static readonly byte[] Values = Build();

            private static byte[] Build()
            {
                var values = new byte[256 * 256];
                // Partial-alpha rows preserve the integer rounding and clamping of the scalar conversion.
                for (int alpha = 1; alpha < byte.MaxValue; alpha++)
                for (int component = 0; component <= byte.MaxValue; component++)
                    values[(alpha << 8) + component] = (byte)Math.Min(byte.MaxValue, (component * byte.MaxValue + alpha / 2) / alpha);
                return values;
            }
        }

        internal static void SwapRedBlue24(Span<byte> data)
        {
            int offset = 0;
            if (Ssse3.IsSupported)
            {
                Vector128<byte> swap = Vector128.Create((byte)2, 1, 0, 5, 4, 3, 8, 7, 6, 11, 10, 9, 128, 128, 128, 128);
                // The packed snapshot is contiguous. Store four pixels and leave the next input bytes untouched.
                for (; offset <= data.Length - Vector128<byte>.Count; offset += 12)
                {
                    Vector128<byte> pixels = Ssse3.Shuffle(Unsafe.ReadUnaligned<Vector128<byte>>(ref data[offset]), swap);
                    Unsafe.WriteUnaligned(ref data[offset], pixels.AsUInt64().GetElement(0));
                    Unsafe.WriteUnaligned(ref data[offset + 8], pixels.AsUInt32().GetElement(2));
                }
            }
            for (; offset < data.Length; offset += 3)
                (data[offset], data[offset + 2]) = (data[offset + 2], data[offset]);
        }
    }
}
