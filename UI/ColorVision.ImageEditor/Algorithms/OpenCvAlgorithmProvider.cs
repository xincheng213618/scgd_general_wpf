using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    public sealed class OpenCvAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
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
            using Mat result = Execute(source.Mat, context.Descriptor.Id, context.Parameters, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer output = AlgorithmImageInterop.FromMat(result, input.Image.DpiX, input.Image.DpiY);
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = new AlgorithmArtifact[] { new AlgorithmImageArtifact("image", "primary", output) },
            });
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

        private static Mat Execute(Mat source, AlgorithmId id, IAlgorithmParameters parameters, CancellationToken cancellationToken)
        {
            Mat result = source.Clone();
            try
            {
                if (id == StandardAlgorithmIds.Invert) OpenCvImageAlgorithms.Invert(result);
                else if (id == StandardAlgorithmIds.BasicAdjustment)
                {
                    BasicAdjustmentParameters value = (BasicAdjustmentParameters)parameters;
                    OpenCvImageAlgorithms.AdjustBasic(result, value.Exposure, value.Brightness, value.Contrast, value.Gamma);
                }
                else if (id == StandardAlgorithmIds.Threshold)
                {
                    ThresholdParameters value = (ThresholdParameters)parameters;
                    double maximum = GetMaximum(result.Depth());
                    double threshold = ResolveNominal8BitValue(value.Threshold, value.UseNominalRange, maximum);
                    OpenCvImageAlgorithms.Threshold(result, threshold, maximum);
                }
                else if (id == StandardAlgorithmIds.Sharpen) OpenCvImageAlgorithms.Sharpen(result);
                else if (id == StandardAlgorithmIds.GaussianBlur)
                {
                    GaussianBlurParameters value = (GaussianBlurParameters)parameters;
                    OpenCvImageAlgorithms.GaussianBlur(result, value.KernelSize, value.Sigma);
                }
                else if (id == StandardAlgorithmIds.MedianBlur)
                {
                    MedianBlurParameters value = (MedianBlurParameters)parameters;
                    OpenCvImageAlgorithms.MedianBlur(result, value.KernelSize);
                }
                else if (id == StandardAlgorithmIds.Morphology)
                {
                    MorphologyParameters value = (MorphologyParameters)parameters;
                    OpenCvImageAlgorithms.Morphology(result, (MorphologyOperation)value.Operation, value.KernelSize, value.Iterations);
                }
                else if (id == StandardAlgorithmIds.Denoise)
                {
                    DenoiseParameters value = (DenoiseParameters)parameters;
                    double sigmaColor = ResolveNominal8BitValue(value.SigmaColor, value.UseNominalColorSigma, GetMaximum(result.Depth()));
                    OpenCvImageAlgorithms.FilterDenoise(result, (FilterDenoiseOperation)value.Operation, value.KernelSize, sigmaColor, value.SigmaSpace);
                }
                else if (id == StandardAlgorithmIds.AutoLevels)
                {
                    Cv2.Normalize(source, result, 0, GetMaximum(source.Depth()), NormTypes.MinMax);
                }
                else if (id == StandardAlgorithmIds.WhiteBalance)
                {
                    result.Dispose();
                    result = ApplyWhiteBalance(source, (WhiteBalanceParameters)parameters);
                }
                else if (id == StandardAlgorithmIds.Canny)
                {
                    result.Dispose();
                    result = ApplyCanny(source, (CannyParameters)parameters);
                }
                else if (id == StandardAlgorithmIds.HistogramEqualization)
                {
                    result.Dispose();
                    result = ApplyHistogramEqualization(source);
                }
                else if (id == StandardAlgorithmIds.PseudoColor)
                {
                    result.Dispose();
                    result = ApplyPseudoColor(source, (PseudoColorParameters)parameters);
                }
                else throw new NotSupportedException($"OpenCV provider does not implement '{id}'.");

                cancellationToken.ThrowIfCancellationRequested();
                return result;
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        internal static Mat ApplyCanny(Mat source, CannyParameters parameters)
        {
            using Mat gray = ConvertToGray(source);
            using Mat gray8 = ConvertTo8BitNominal(gray);
            Mat result = new();
            Cv2.Canny(gray8, result, parameters.LowThreshold, parameters.HighThreshold, parameters.ApertureSize, parameters.L2Gradient);
            return result;
        }

        internal static Mat ApplyWhiteBalance(Mat source, WhiteBalanceParameters parameters)
        {
            if (source.Channels() < 3) throw new NotSupportedException("White balance requires a three- or four-channel color image.");
            Mat[] channels = Cv2.Split(source);
            try
            {
                channels[0].ConvertTo(channels[0], channels[0].Type(), parameters.BlueScale);
                channels[1].ConvertTo(channels[1], channels[1].Type(), parameters.GreenScale);
                channels[2].ConvertTo(channels[2], channels[2].Type(), parameters.RedScale);
                Mat result = new();
                Cv2.Merge(channels, result);
                return result;
            }
            finally
            {
                foreach (Mat channel in channels) channel.Dispose();
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

        internal static Mat ApplyPseudoColor(Mat source, PseudoColorParameters parameters)
        {
            using Mat gray = SelectChannel(source, parameters.Channel);
            using Mat gray8 = ConvertPseudoColorSource(gray, parameters, out double minimum, out double maximum);
            using Mat scaled = new();
            if (maximum > minimum)
            {
                gray8.ConvertTo(scaled, MatType.CV_8U, byte.MaxValue / (maximum - minimum), -minimum * byte.MaxValue / (maximum - minimum));
            }
            else
            {
                gray8.CopyTo(scaled);
            }
            Mat result = new();
            Cv2.ApplyColorMap(scaled, result, (OpenCvSharp.ColormapTypes)(int)parameters.Colormap);
            using Mat below = new();
            using Mat above = new();
            Cv2.Compare(gray8, minimum, below, CmpTypes.LT);
            Cv2.Compare(gray8, maximum, above, CmpTypes.GT);
            result.SetTo(Scalar.Black, below);
            result.SetTo(Scalar.White, above);
            return result;
        }

        private static Mat SelectChannel(Mat source, int channel)
        {
            if (source.Channels() == 1) return source.Clone();
            if (channel >= 0 && channel < source.Channels())
            {
                Mat selected = new();
                Cv2.ExtractChannel(source, selected, channel);
                return selected;
            }
            return ConvertToGray(source);
        }

        private static Mat ConvertPseudoColorSource(Mat source, PseudoColorParameters parameters, out double minimum, out double maximum)
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
