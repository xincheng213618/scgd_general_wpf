using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    public sealed class OpenCvAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private readonly PseudoColorProcessor _pseudoColor = new();
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();
        private static readonly HashSet<AlgorithmId> SupportedIds = new()
        {
            StandardAlgorithmIds.Invert,
            StandardAlgorithmIds.Canny,
            StandardAlgorithmIds.BasicAdjustment,
            StandardAlgorithmIds.Threshold,
            StandardAlgorithmIds.Sharpen,
            StandardAlgorithmIds.GaussianBlur,
            StandardAlgorithmIds.MedianBlur,
            StandardAlgorithmIds.Morphology,
            StandardAlgorithmIds.Denoise,
            StandardAlgorithmIds.AutoLevels,
            StandardAlgorithmIds.WhiteBalance,
            StandardAlgorithmIds.HistogramEqualization,
            StandardAlgorithmIds.PseudoColor,
        };

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.opencv.cpu",
            "ColorVision OpenCV CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            100,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Copilot,
            Formats,
            typeof(Cv2).Assembly.GetName().Version?.ToString());

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, SupportedIds, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            if (!SupportedIds.Contains(descriptor.Id))
            {
                reason = "algorithm_not_implemented";
                return false;
            }
            if (inputs.Count != 1 || !Formats.Contains(inputs[0].Image.Format))
            {
                reason = "input_format_unsupported";
                return false;
            }
            reason = null;
            return true;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmInput input = context.Inputs[0];
            if (TryGetCompatibilityFailure(context.Descriptor.Id, context.Parameters, input.Image.Format, out AlgorithmFailure? failure))
            {
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Failed,
                    Failures = new[] { failure! },
                });
            }
            using AlgorithmImageMatLease source = AlgorithmImageInterop.BorrowReadOnly(input.Image);
            AlgorithmImageBuffer output;
            if (context.Descriptor.Id == StandardAlgorithmIds.BasicAdjustment)
            {
                output = ExecuteBasicAdjustment(source.Mat, input.Image, (BasicAdjustmentParameters)context.Parameters, cancellationToken);
            }
            else if (context.Descriptor.Id == StandardAlgorithmIds.PseudoColor)
            {
                output = _pseudoColor.Execute(source.Mat, input.Image, (PseudoColorParameters)context.Parameters, cancellationToken);
            }
            else
            {
                output = Execute(source.Mat, input.Image, context.Descriptor.Id, context.Parameters, cancellationToken);
            }
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = new AlgorithmArtifact[] { new AlgorithmImageArtifact("image", "primary", output) },
                });
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }

        private static unsafe AlgorithmImageBuffer ExecuteBasicAdjustment(Mat source, AlgorithmImageBuffer input,
            BasicAdjustmentParameters parameters, CancellationToken cancellationToken)
        {
            int stride = checked(input.Width * input.Format.BytesPerPixel());
            int length = checked(stride * input.Height);
            cancellationToken.ThrowIfCancellationRequested();
            byte[]? data = null;
            MemoryHandle pin = default;
            Mat? destination = null;
            try
            {
                OpenCvImageAlgorithms.AdjustBasicTo(source, () =>
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    if (data != null) throw new InvalidOperationException("Basic adjustment requested its output more than once.");
                    data = new byte[length];
                    pin = data.AsMemory().Pin();
                    destination = Mat.FromPixelData(input.Height, input.Width, AlgorithmImageInterop.ToMatType(input.Format),
                        (IntPtr)pin.Pointer, stride);
                    return destination;
                }, parameters.Exposure, parameters.Brightness,
                    parameters.Contrast, parameters.Gamma, cancellationToken);
                if (destination == null || data == null)
                    throw new InvalidOperationException("Basic adjustment did not create its output.");
                if (destination.Data != (IntPtr)pin.Pointer || destination.Step() != stride)
                    throw new InvalidOperationException("Basic adjustment replaced its externally owned output storage.");
            }
            finally
            {
                destination?.Dispose();
                pin.Dispose();
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new AlgorithmImageBuffer(input.Width, input.Height, stride, input.Format, data, input.DpiX, input.DpiY);
        }

        private static bool TryGetCompatibilityFailure(
            AlgorithmId id,
            IAlgorithmParameters parameters,
            AlgorithmImageFormat format,
            out AlgorithmFailure? failure)
        {
            if (id == StandardAlgorithmIds.MedianBlur
                && format.BitsPerChannel() != 8
                && ((MedianBlurParameters)parameters).KernelSize > 5)
            {
                failure = new AlgorithmFailure(
                    "parameter_format_unsupported",
                    "OpenCV median blur supports kernels larger than 5 only for 8-bit images.",
                    nameof(MedianBlurParameters.KernelSize),
                    new Dictionary<string, string>
                    {
                        ["format"] = format.ToString(),
                        ["maximumKernelSize"] = "5",
                    });
                return true;
            }
            if (id == StandardAlgorithmIds.Threshold)
            {
                ThresholdParameters value = (ThresholdParameters)parameters;
                double maximum = GetMaximum(format);
                if (!value.UseNominalRange && value.Threshold > maximum)
                {
                    failure = new AlgorithmFailure(
                        "parameter_format_unsupported",
                        $"Absolute threshold {value.Threshold} exceeds the nominal maximum {maximum} for {format}.",
                        nameof(ThresholdParameters.Threshold),
                        new Dictionary<string, string>
                        {
                            ["format"] = format.ToString(),
                            ["maximum"] = maximum.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        });
                    return true;
                }
            }
            failure = null;
            return false;
        }

        private static unsafe AlgorithmImageBuffer Execute(Mat source, AlgorithmImageBuffer input, AlgorithmId id,
            IAlgorithmParameters parameters, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using Mat? nativeOutput = id switch
            {
                var value when value == StandardAlgorithmIds.Canny => ApplyCanny(source, (CannyParameters)parameters),
                var value when value == StandardAlgorithmIds.HistogramEqualization => ApplyHistogramEqualization(source),
                _ => null,
            };
            if (nativeOutput != null)
                return AlgorithmImageInterop.FromMat(nativeOutput, input.DpiX, input.DpiY, cancellationToken);

            // Same-layout operations can fill the result buffer without a native staging image.
            int stride = checked(input.Width * input.Format.BytesPerPixel());
            byte[] data = new byte[checked(stride * input.Height)];
            fixed (byte* pixels = data)
            {
                using Mat destination = Mat.FromPixelData(input.Height, input.Width, source.Type(), (IntPtr)pixels, stride);
                cancellationToken.ThrowIfCancellationRequested();
                ExecuteInto(source, destination, id, parameters, cancellationToken);
                if (destination.Data != (IntPtr)pixels || destination.Step() != stride)
                    throw new InvalidOperationException($"OpenCV operation '{id}' replaced its externally owned output storage.");
            }
            cancellationToken.ThrowIfCancellationRequested();
            return new AlgorithmImageBuffer(input.Width, input.Height, stride, input.Format, data, input.DpiX, input.DpiY);
        }

        private static void ExecuteInto(Mat source, Mat destination, AlgorithmId id, IAlgorithmParameters parameters, CancellationToken cancellationToken)
        {
            if (id == StandardAlgorithmIds.Invert) Cv2.BitwiseNot(source, destination);
            else if (id == StandardAlgorithmIds.Threshold)
            {
                ThresholdParameters value = (ThresholdParameters)parameters;
                double maximum = GetMaximum(source.Depth());
                double threshold = ResolveNominal8BitValue(value.Threshold, value.UseNominalRange, maximum);
                Cv2.Threshold(source, destination, threshold, maximum, ThresholdTypes.Binary);
            }
            else if (id == StandardAlgorithmIds.Sharpen) OpenCvImageAlgorithms.Sharpen(source, destination);
            else if (id == StandardAlgorithmIds.GaussianBlur)
            {
                GaussianBlurParameters value = (GaussianBlurParameters)parameters;
                OpenCvImageAlgorithms.GaussianBlur(source, destination, value.KernelSize, value.Sigma);
            }
            else if (id == StandardAlgorithmIds.MedianBlur)
            {
                MedianBlurParameters value = (MedianBlurParameters)parameters;
                OpenCvImageAlgorithms.MedianBlur(source, destination, value.KernelSize);
            }
            else if (id == StandardAlgorithmIds.Morphology)
            {
                MorphologyParameters value = (MorphologyParameters)parameters;
                OpenCvImageAlgorithms.Morphology(source, destination, (MorphologyOperation)value.Operation, value.KernelSize, value.Iterations);
            }
            else if (id == StandardAlgorithmIds.Denoise)
            {
                DenoiseParameters value = (DenoiseParameters)parameters;
                double sigmaColor = ResolveNominal8BitValue(value.SigmaColor, value.UseNominalColorSigma, GetMaximum(source.Depth()));
                OpenCvImageAlgorithms.FilterDenoise(source, destination, (FilterDenoiseOperation)value.Operation, value.KernelSize, sigmaColor, value.SigmaSpace);
            }
            else if (id == StandardAlgorithmIds.AutoLevels)
                Cv2.Normalize(source, destination, 0, GetMaximum(source.Depth()), NormTypes.MinMax);
            else if (id == StandardAlgorithmIds.WhiteBalance)
                ApplyWhiteBalance(source, destination, (WhiteBalanceParameters)parameters, cancellationToken);
            else throw new NotSupportedException($"OpenCV provider does not implement '{id}'.");
        }

        internal static Mat ApplyCanny(Mat source, CannyParameters parameters)
        {
            using Mat gray = ConvertToGray(source);
            using Mat gray8 = ConvertTo8BitNominal(gray);
            Mat result = new();
            Cv2.Canny(gray8, result, parameters.LowThreshold, parameters.HighThreshold, parameters.ApertureSize, parameters.L2Gradient);
            return result;
        }

        private static void ApplyWhiteBalance(Mat source, Mat destination, WhiteBalanceParameters parameters, CancellationToken cancellationToken)
        {
            int channels = source.Channels();
            if (channels < 3) throw new NotSupportedException("White balance requires a three- or four-channel color image.");
            if (parameters.BlueScale == 1 && parameters.GreenScale == 1 && parameters.RedScale == 1)
            {
                source.CopyTo(destination);
                return;
            }

            MatType depth = source.Depth();
            int minimumLookupPixels = depth == MatType.CV_16U ? 1024 * 1024 : 512 * 512;
            if ((depth == MatType.CV_8U || depth == MatType.CV_16U) && source.Total() >= minimumLookupPixels)
            {
                using Mat ramp = OpenCvImageAlgorithms.CreateUnsignedRamp(depth);
                using Mat blue = new(), green = new(), red = new(), table = new();
                // Generate the table with the same ConvertTo rounding and saturation as the channel path.
                ramp.ConvertTo(blue, depth, parameters.BlueScale);
                ramp.ConvertTo(green, depth, parameters.GreenScale);
                ramp.ConvertTo(red, depth, parameters.RedScale);
                Cv2.Merge(channels == 4 ? [blue, green, red, ramp] : [blue, green, red], table);
                cancellationToken.ThrowIfCancellationRequested();
                Cv2.LUT(source, table, destination);
                return;
            }

            int height = source.Rows;
            int rowBytes = checked(source.Cols * (int)source.ElemSize());
            int rowsPerBlock = Math.Max(1, 1024 * 1024 / rowBytes);
            for (int top = 0; top < height;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int end = top + Math.Min(rowsPerBlock, height - top);
                using Mat sourceBlock = source.RowRange(top, end);
                using Mat targetBlock = destination.RowRange(top, end);
                Mat[] planes = Cv2.Split(sourceBlock);
                try
                {
                    planes[0].ConvertTo(planes[0], depth, parameters.BlueScale);
                    planes[1].ConvertTo(planes[1], depth, parameters.GreenScale);
                    planes[2].ConvertTo(planes[2], depth, parameters.RedScale);
                    Cv2.Merge(planes, targetBlock);
                }
                finally
                {
                    foreach (Mat plane in planes) plane.Dispose();
                }
                top = end;
            }
        }

        internal static Mat ApplyHistogramEqualization(Mat source)
        {
            using Mat source8 = ConvertTo8BitRange(source);
            if (source8.Channels() == 1)
            {
                Mat grayResult = new();
                Cv2.EqualizeHist(source8, grayResult);
                return grayResult;
            }

            using Mat bgr = ConvertToBgr(source8);
            using Mat yCrCb = new();
            Cv2.CvtColor(bgr, yCrCb, ColorConversionCodes.BGR2YCrCb);
            using Mat luminance = new();
            Cv2.ExtractChannel(yCrCb, luminance, 0);
            Cv2.EqualizeHist(luminance, luminance);
            Cv2.InsertChannel(luminance, yCrCb, 0);
            Mat result = new();
            Cv2.CvtColor(yCrCb, result, ColorConversionCodes.YCrCb2BGR);
            return result;
        }

        internal static Mat ConvertPseudoColorSource(Mat source, PseudoColorParameters parameters, out double minimum, out double maximum)
        {
            if (parameters.UseNominalRange)
            {
                minimum = 0;
                maximum = byte.MaxValue;
                return ConvertTo8BitNominal(source);
            }

            minimum = parameters.Minimum;
            maximum = parameters.Maximum;
            if (source.Depth() == MatType.CV_8U) return source.Clone();

            Mat result = new();
            if (source.Depth() == MatType.CV_16U)
            {
                if (parameters.AutoRange)
                {
                    double range = parameters.DataMaximum - (double)parameters.DataMinimum;
                    source.ConvertTo(result, MatType.CV_8U, byte.MaxValue / range, -parameters.DataMinimum * byte.MaxValue / range);
                    minimum = Math.Clamp((parameters.Minimum - (double)parameters.DataMinimum) / range * byte.MaxValue, 0, byte.MaxValue);
                    maximum = Math.Clamp((parameters.Maximum - (double)parameters.DataMinimum) / range * byte.MaxValue, 0, byte.MaxValue);
                }
                else
                {
                    source.ConvertTo(result, MatType.CV_8U, 1d / 257d);
                    minimum = Math.Min(byte.MaxValue, parameters.Minimum >> 8);
                    maximum = Math.Min(byte.MaxValue, parameters.Maximum >> 8);
                }
                return result;
            }

            if (source.Depth() == MatType.CV_32F || source.Depth() == MatType.CV_64F)
            {
                Cv2.Normalize(source, result, 0, byte.MaxValue, NormTypes.MinMax, MatType.CV_8U);
                if (parameters.AutoRange)
                {
                    minimum = 0;
                    maximum = byte.MaxValue;
                }
                else
                {
                    minimum = Math.Min(byte.MaxValue, minimum);
                    maximum = Math.Min(byte.MaxValue, maximum);
                }
                return result;
            }

            throw new NotSupportedException($"Unsupported pseudo-color image depth: {source.Depth()}.");
        }

        internal static Mat ConvertToGray(Mat source)
        {
            if (source.Channels() == 1) return source.Clone();
            Mat result = new();
            Cv2.CvtColor(source, result, source.Channels() == 4 ? ColorConversionCodes.BGRA2GRAY : ColorConversionCodes.BGR2GRAY);
            return result;
        }

        internal static Mat ConvertToBgr(Mat source)
        {
            if (source.Channels() == 3) return source.Clone();
            Mat result = new();
            Cv2.CvtColor(source, result, source.Channels() == 4 ? ColorConversionCodes.BGRA2BGR : ColorConversionCodes.GRAY2BGR);
            return result;
        }

        internal static Mat ConvertTo8BitNominal(Mat source)
        {
            if (source.Depth() == MatType.CV_8U) return source.Clone();
            Mat result = new();
            MatType depth = source.Depth();
            double scale;
            if (depth == MatType.CV_16U) scale = byte.MaxValue / (double)ushort.MaxValue;
            else if (depth == MatType.CV_32F || depth == MatType.CV_64F) scale = byte.MaxValue;
            else throw new NotSupportedException($"Unsupported image depth: {depth}");
            source.ConvertTo(result, MatType.MakeType(MatType.CV_8U, source.Channels()), scale);
            return result;
        }

        private static Mat ConvertTo8BitRange(Mat source)
        {
            if (source.Depth() == MatType.CV_8U) return source.Clone();
            using Mat normalized = new();
            Cv2.Normalize(source, normalized, 0, byte.MaxValue, NormTypes.MinMax);
            Mat result = new();
            normalized.ConvertTo(result, MatType.MakeType(MatType.CV_8U, source.Channels()));
            return result;
        }

        private static double GetMaximum(MatType depth)
        {
            if (depth == MatType.CV_8U) return byte.MaxValue;
            if (depth == MatType.CV_16U) return ushort.MaxValue;
            if (depth == MatType.CV_32F || depth == MatType.CV_64F) return 1;
            throw new NotSupportedException($"Unsupported image depth: {depth}");
        }

        private static double GetMaximum(AlgorithmImageFormat format)
            => format.IsFloatingPoint() ? 1 : format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;

        internal static double ResolveNominal8BitValue(double value, bool useNominalRange, double formatMaximum)
            => useNominalRange ? value / byte.MaxValue * formatMaximum : value;
    }
}
