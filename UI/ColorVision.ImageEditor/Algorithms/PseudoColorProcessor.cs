using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Preserves the static-image mapping, including its two quantization steps and threshold comparisons.</summary>
    internal sealed class PseudoColorProcessor
    {
        private readonly object _cacheSync = new();
        private readonly long _cacheByteLimit;
        private Guid _sourceStorageId;
        private AlgorithmImageBuffer? _gray;

        // One managed grayscale snapshot per provider, never an unbounded document/frame cache.
        // Retained handles let in-flight work finish safely when another source evicts the entry.
        internal PseudoColorProcessor(long cacheByteLimit = 128L * 1024 * 1024) => _cacheByteLimit = cacheByteLimit;

        internal unsafe AlgorithmImageBuffer Execute(Mat source, AlgorithmImageBuffer input,
            PseudoColorParameters parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using AlgorithmImageBuffer? prepared = AcquireGray(source, input, parameters.Channel, cancellationToken);
            using AlgorithmImageMatLease? grayLease = prepared == null ? null : AlgorithmImageInterop.BorrowReadOnly(prepared);
            int stride = checked(input.Width * 3);
            byte[] pixels = GC.AllocateUninitializedArray<byte>(checked(stride * input.Height));
            fixed (byte* address = pixels)
            {
                using Mat output = Mat.FromPixelData(input.Height, input.Width, MatType.CV_8UC3, (IntPtr)address, stride);
                Render(grayLease?.Mat ?? source, output, parameters, cancellationToken);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new AlgorithmImageBuffer(input.Width, input.Height, stride, AlgorithmImageFormat.Bgr24, pixels, input.DpiX, input.DpiY);
        }

        internal unsafe AlgorithmImageBuffer? AcquireGray(Mat source, AlgorithmImageBuffer input, int channel, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            bool composite = source.Channels() > 1 && (channel < 0 || channel >= source.Channels());
            bool integer = source.Depth() == MatType.CV_8U || source.Depth() == MatType.CV_16U;
            long size = (long)source.Rows * source.Cols * source.ElemSize1();
            lock (_cacheSync)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!composite || !integer || size > _cacheByteLimit)
                {
                    ClearCache();
                    return null;
                }
                if (_gray != null && _sourceStorageId == input.StorageId) return _gray.RetainReadOnly();
                ClearCache();
                int stride = checked(source.Cols * (int)source.ElemSize1());
                byte[] pixels = GC.AllocateUninitializedArray<byte>(checked((int)size));
                fixed (byte* address = pixels)
                {
                    using Mat gray = Mat.FromPixelData(source.Rows, source.Cols, source.Depth(), (IntPtr)address, stride);
                    Cv2.CvtColor(source, gray, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
                }
                cancellationToken.ThrowIfCancellationRequested();
                _gray = new AlgorithmImageBuffer(source.Cols, source.Rows, stride,
                    source.Depth() == MatType.CV_16U ? AlgorithmImageFormat.Gray16 : AlgorithmImageFormat.Gray8,
                    pixels, input.DpiX, input.DpiY);
                _sourceStorageId = input.StorageId;
                return _gray.RetainReadOnly();
            }
        }

        private void ClearCache()
        {
            _gray?.Dispose();
            _gray = null;
            _sourceStorageId = default;
        }

        internal static void Render(Mat source, Mat output, PseudoColorParameters parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (source.Empty()) throw new ArgumentException("Pseudo-color requires a nonempty source.", nameof(source));
            if (output.Rows != source.Rows || output.Cols != source.Cols || output.Type() != MatType.CV_8UC3)
                throw new ArgumentException("Pseudo-color output must be a matching Bgr24 image.", nameof(output));

            Mat? selected = null;
            Mat? normalized = null;
            try
            {
                Mat samples = source;
                int channel = source.Channels() > 1 ? parameters.Channel : 0;
                bool integer = source.Depth() == MatType.CV_8U || source.Depth() == MatType.CV_16U;
                if (source.Channels() > 1 && (channel < 0 || channel >= source.Channels() || !integer))
                {
                    selected = new Mat();
                    if (channel >= 0 && channel < source.Channels()) Cv2.ExtractChannel(source, selected, channel);
                    else Cv2.CvtColor(source, selected, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
                    samples = selected;
                    channel = 0;
                }
                cancellationToken.ThrowIfCancellationRequested();
                using Mat ramp = CreateRamp(integer ? samples.Depth() : MatType.CV_8U);
                Mat lut;
                if (integer)
                {
                    using Mat indices = OpenCvAlgorithmProvider.ConvertPseudoColorSource(ramp, parameters, out double min, out double max);
                    lut = MapColors(indices, min, max, parameters.Colormap);
                }
                else
                {
                    normalized = OpenCvAlgorithmProvider.ConvertPseudoColorSource(samples, parameters, out double min, out double max);
                    samples = normalized;
                    channel = 0;
                    lut = MapColors(ramp, min, max, parameters.Colormap);
                }
                using (lut) Lookup(samples, channel, lut, output, cancellationToken);
            }
            finally
            {
                normalized?.Dispose();
                selected?.Dispose();
            }
        }

        private static unsafe Mat CreateRamp(MatType depth)
        {
            int length = depth == MatType.CV_16U ? 65536 : 256;
            Mat ramp = new(1, length, depth);
            if (depth == MatType.CV_16U)
            {
                ushort* pixels = (ushort*)ramp.Data;
                for (int i = 0; i < length; i++) pixels[i] = (ushort)i;
            }
            else
            {
                byte* pixels = (byte*)ramp.Data;
                for (int i = 0; i < length; i++) pixels[i] = (byte)i;
            }
            return ramp;
        }

        // Keep OpenCV's rounding and scalar comparison semantics. Computing this small table
        // replaces the same conversions, masks and masked writes over every source pixel.
        private static Mat MapColors(Mat indices, double minimum, double maximum, StandardPseudoColorMap colormap)
        {
            using Mat scaled = new();
            if (maximum > minimum)
                indices.ConvertTo(scaled, MatType.CV_8U, byte.MaxValue / (maximum - minimum), -minimum * byte.MaxValue / (maximum - minimum));
            else indices.CopyTo(scaled);
            Mat result = new();
            try
            {
                Cv2.ApplyColorMap(scaled, result, (ColormapTypes)(int)colormap);
                using Mat below = new();
                using Mat above = new();
                Cv2.Compare(indices, minimum, below, CmpTypes.LT);
                Cv2.Compare(indices, maximum, above, CmpTypes.GT);
                result.SetTo(Scalar.Black, below);
                result.SetTo(Scalar.White, above);
                return result;
            }
            catch { result.Dispose(); throw; }
        }

        private static unsafe void Lookup(Mat source, int channel, Mat lut, Mat output, CancellationToken cancellationToken)
        {
            nint sourceAddress = source.Data, outputAddress = output.Data, lutAddress = lut.Data;
            long sourceStride = source.Step(), outputStride = output.Step();
            int width = source.Cols, rows = source.Rows, channels = source.Channels();
            bool sixteenBit = source.Depth() == MatType.CV_16U;
            void Row(int y)
            {
                cancellationToken.ThrowIfCancellationRequested();
                byte* destination = (byte*)(outputAddress + y * outputStride);
                byte* table = (byte*)lutAddress;
                if (sixteenBit)
                {
                    ushort* samples = (ushort*)(sourceAddress + y * sourceStride) + channel;
                    for (int x = 0; x < width; x++, samples += channels, destination += 3)
                    {
                        byte* color = table + *samples * 3;
                        destination[0] = color[0]; destination[1] = color[1]; destination[2] = color[2];
                    }
                }
                else
                {
                    byte* samples = (byte*)(sourceAddress + y * sourceStride) + channel;
                    for (int x = 0; x < width; x++, samples += channels, destination += 3)
                    {
                        byte* color = table + *samples * 3;
                        destination[0] = color[0]; destination[1] = color[1]; destination[2] = color[2];
                    }
                }
            }
            if ((long)rows * width < 65536)
            {
                for (int y = 0; y < rows; y++) Row(y);
            }
            else
            {
                Parallel.For(0, rows, new ParallelOptions
                {
                    CancellationToken = cancellationToken,
                    MaxDegreeOfParallelism = Math.Min(Environment.ProcessorCount, 16),
                }, Row);
            }
            cancellationToken.ThrowIfCancellationRequested();
        }
    }
}
