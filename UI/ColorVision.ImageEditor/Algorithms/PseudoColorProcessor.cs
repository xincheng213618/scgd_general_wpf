using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Maps grayscale and single-channel samples to BGR colors with an integer lookup table.</summary>
    public sealed class PseudoColorProcessor
    {
        private static CachedTable<SampleRangeTableKey>? _sampleRangeTable;
        private static CachedTable<AlgorithmTableKey>? _algorithmTable;
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

        /// <summary>
        /// Renders unsigned 8-bit or 16-bit samples to an independent BGR destination.
        /// The range is measured on the selected channel in Auto mode and is always returned in source pixel values.
        /// </summary>
        public static (double Minimum, double Maximum) RenderSampleRange(Mat source, Mat output, ImageColorChannel channel,
            StandardPseudoColorMap colormap, bool autoRange, double minimum, double maximum, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int index = ImageChannelProcessor.ResolveIndex(source, channel);
            if (source.Depth() != MatType.CV_8U && source.Depth() != MatType.CV_16U)
                throw new ArgumentException("Source pixel ranges require unsigned 8-bit or 16-bit samples.", nameof(source));
            if (!Enum.IsDefined(colormap)) throw new ArgumentOutOfRangeException(nameof(colormap));
            if (!autoRange && (!double.IsFinite(minimum) || !double.IsFinite(maximum) || minimum < 0 || maximum > ushort.MaxValue || minimum >= maximum))
                throw new ArgumentException("A fixed range requires 0 ≤ minimum < maximum ≤ 65535.");
            if (ReferenceEquals(source, output)) throw new ArgumentException("The output image must be independent of the source.", nameof(output));

            bool bandedRange = autoRange && index >= 0 && source.Channels() > 1
                && (long)source.Rows * source.Cols * source.ElemSize1() >= 4 * 1024 * 1024
                && !MemoryOverlaps(source, output);
            using Mat? selected = index < 0 || (autoRange && source.Channels() > 1 && !bandedRange) ? new Mat() : null;
            if (selected != null)
            {
                ImageChannelProcessor.Select(source, selected, channel, cancellationToken);
                index = 0;
            }
            Mat samples = selected ?? source;
            if (bandedRange)
                (minimum, maximum) = FindChannelRange(source, index, cancellationToken);
            else if (autoRange)
                Cv2.MinMaxLoc(samples, out minimum, out maximum);
            cancellationToken.ThrowIfCancellationRequested();

            // Index the LUT directly by source value, preserving narrow windows on 16-bit images.
            var key = new SampleRangeTableKey(samples.Depth() == MatType.CV_16U, minimum, maximum, colormap);
            CachedTable<SampleRangeTableKey>? cached = Volatile.Read(ref _sampleRangeTable);
            if (cached?.Colors is byte[] colors && cached.Key == key)
            {
                output.Create(samples.Rows, samples.Cols, MatType.CV_8UC3);
                Lookup(samples, index, colors, output, cancellationToken);
            }
            else
            {
                using Mat ramp = OpenCvImageAlgorithms.CreateUnsignedRamp(samples.Depth());
                using Mat mapped = MapColors(ramp, minimum, maximum, colormap, useLowestColorForConstant: true);
                output.Create(samples.Rows, samples.Cols, MatType.CV_8UC3);
                Lookup(samples, index, mapped, output, cancellationToken);
                RememberTable(ref _sampleRangeTable, cached, key, mapped);
            }
            return (minimum, maximum);
        }

        private static bool MemoryOverlaps(Mat source, Mat output)
            => (nuint)output.Data < (nuint)source.DataEnd && (nuint)source.Data < (nuint)output.DataEnd;

        private static (double Minimum, double Maximum) FindChannelRange(Mat source, int channel, CancellationToken cancellationToken)
        {
            // Keep one small channel buffer for range detection, then map the input channel directly.
            const int scratchBytes = 1024 * 1024;
            int rowsPerBand = (int)Math.Max(1, scratchBytes / (source.Cols * source.ElemSize1()));
            double minimum = ushort.MaxValue, maximum = 0;
            using Mat selected = new();
            for (int top = 0; top < source.Rows;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int end = top + Math.Min(rowsPerBand, source.Rows - top);
                using Mat part = source.RowRange(top, end);
                Cv2.ExtractChannel(part, selected, channel);
                Cv2.MinMaxLoc(selected, out double bandMinimum, out double bandMaximum);
                minimum = Math.Min(minimum, bandMinimum);
                maximum = Math.Max(maximum, bandMaximum);
                top = end;
            }
            return (minimum, maximum);
        }

        /// <summary>Preserves the static-image mapping, including its two quantization steps.</summary>
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
                if (integer)
                {
                    var key = new AlgorithmTableKey(samples.Depth() == MatType.CV_16U, parameters.Colormap,
                        parameters.UseNominalRange, parameters.AutoRange, parameters.Minimum, parameters.Maximum,
                        parameters.DataMinimum, parameters.DataMaximum);
                    CachedTable<AlgorithmTableKey>? cached = Volatile.Read(ref _algorithmTable);
                    if (cached?.Colors is byte[] colors && cached.Key == key)
                    {
                        Lookup(samples, channel, colors, output, cancellationToken);
                    }
                    else
                    {
                        using Mat ramp = OpenCvImageAlgorithms.CreateUnsignedRamp(samples.Depth());
                        // Build from the captured key so a mutable parameter object cannot publish a mismatched table.
                        PseudoColorParameters mapping = new()
                        {
                            UseNominalRange = key.Nominal,
                            AutoRange = key.Auto,
                            Minimum = key.Minimum,
                            Maximum = key.Maximum,
                            DataMinimum = key.DataMinimum,
                            DataMaximum = key.DataMaximum,
                        };
                        using Mat indices = OpenCvAlgorithmProvider.ConvertPseudoColorSource(ramp, mapping, out double min, out double max);
                        using Mat mapped = MapColors(indices, min, max, key.Map);
                        Lookup(samples, channel, mapped, output, cancellationToken);
                        RememberTable(ref _algorithmTable, cached, key, mapped);
                    }
                }
                else
                {
                    using Mat ramp = OpenCvImageAlgorithms.CreateUnsignedRamp(MatType.CV_8U);
                    normalized = OpenCvAlgorithmProvider.ConvertPseudoColorSource(samples, parameters, out double min, out double max);
                    using Mat lut = MapColors(ramp, min, max, parameters.Colormap);
                    Lookup(normalized, 0, lut, output, cancellationToken);
                }
            }
            finally
            {
                normalized?.Dispose();
                selected?.Dispose();
            }
        }

        // Keep OpenCV's rounding and scalar comparison semantics. Computing this small table
        // replaces the same conversions, masks and masked writes over every source pixel.
        private static Mat MapColors(Mat indices, double minimum, double maximum, StandardPseudoColorMap colormap, bool useLowestColorForConstant = false)
        {
            using Mat scaled = new();
            if (maximum > minimum)
                indices.ConvertTo(scaled, MatType.CV_8U, byte.MaxValue / (maximum - minimum), -minimum * byte.MaxValue / (maximum - minimum));
            else if (useLowestColorForConstant)
            {
                scaled.Create(indices.Rows, indices.Cols, MatType.CV_8UC1);
                scaled.SetTo(Scalar.Black);
            }
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

        // One immutable mapping per call family. In-flight lookups keep their own array alive across replacement.
        private sealed record CachedTable<TKey>(TKey Key, byte[]? Colors);
        private readonly record struct SampleRangeTableKey(bool Wide, double Minimum, double Maximum, StandardPseudoColorMap Map);
        private readonly record struct AlgorithmTableKey(bool Wide, StandardPseudoColorMap Map, bool Nominal, bool Auto,
            uint Minimum, uint Maximum, uint DataMinimum, uint DataMaximum);

        private static void RememberTable<TKey>(ref CachedTable<TKey>? slot, CachedTable<TKey>? previous, TKey key, Mat lut)
            where TKey : struct, IEquatable<TKey>
        {
            // Wait for a repeat before allocating a copy, since automatic ranges may vary between frames.
            byte[]? colors = null;
            if (previous != null && previous.Key.Equals(key))
            {
                colors = GC.AllocateUninitializedArray<byte>(checked(lut.Rows * lut.Cols * 3));
                System.Runtime.InteropServices.Marshal.Copy(lut.Data, colors, 0, colors.Length);
            }
            Interlocked.CompareExchange(ref slot, new(key, colors), previous);
        }

        private static unsafe void Lookup(Mat source, int channel, byte[] lut, Mat output, CancellationToken cancellationToken)
        {
            fixed (byte* table = lut)
                Lookup(source, channel, (nint)table, output, cancellationToken);
        }

        private static void Lookup(Mat source, int channel, Mat lut, Mat output, CancellationToken cancellationToken)
        {
            Lookup(source, channel, lut.Data, output, cancellationToken);
            GC.KeepAlive(lut);
        }

        private static unsafe void Lookup(Mat source, int channel, nint lutAddress, Mat output, CancellationToken cancellationToken)
        {
            nint sourceAddress = source.Data, outputAddress = output.Data;
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
