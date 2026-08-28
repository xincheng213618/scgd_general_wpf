using ColorVision.Algorithms;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic, precision-preserving two-input comparison for identically encoded images.</summary>
    public sealed class ImageComparisonAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.analysis.image-comparison/v2";
        internal const long MaximumRetainedOutputBytes = 192L * 1024 * 1024;
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.image-comparison.cpu",
            "ColorVision Image Comparison CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            110,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local
                | AlgorithmHostCapabilities.Deterministic | AlgorithmHostCapabilities.MultiInput
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.ImageComparison, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.ImageComparison
                && inputs.Count == 2
                && inputs.All(input => Formats.Contains(input.Image.Format));
            reason = supported ? null : "algorithm_input_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmInput[] references = context.Inputs.Where(input => string.Equals(input.Name, "reference", StringComparison.OrdinalIgnoreCase)).ToArray();
            AlgorithmInput[] candidates = context.Inputs.Where(input => string.Equals(input.Name, "candidate", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (references.Length != 1 || candidates.Length != 1)
                return ValueTask.FromResult(Failure(context, "invalid_input_names", "Exactly one 'reference' and one 'candidate' input are required.", "inputs"));
            AlgorithmInput referenceInput = references[0];
            AlgorithmInput candidateInput = candidates[0];
            AlgorithmImageBuffer reference = referenceInput.Image;
            AlgorithmImageBuffer candidate = candidateInput.Image;
            if (reference.Width != candidate.Width || reference.Height != candidate.Height)
            {
                return ValueTask.FromResult(Failure(context, "dimension_mismatch",
                    $"Reference is {reference.Width}x{reference.Height}; candidate is {candidate.Width}x{candidate.Height}.", "inputs"));
            }
            if (reference.Format != candidate.Format)
            {
                return ValueTask.FromResult(Failure(context, "format_mismatch",
                    $"Reference format {reference.Format} does not match candidate format {candidate.Format}; implicit bit-depth or channel conversion is forbidden.", "inputs"));
            }
            if (string.IsNullOrWhiteSpace(referenceInput.ColorSpace) || string.IsNullOrWhiteSpace(candidateInput.ColorSpace))
                return ValueTask.FromResult(Failure(context, "color_space_unspecified", "Both inputs require an explicit encoded color-space label.", "inputs"));
            if (!string.Equals(referenceInput.ColorSpace, candidateInput.ColorSpace, StringComparison.OrdinalIgnoreCase))
            {
                return ValueTask.FromResult(Failure(context, "color_space_mismatch",
                    $"Reference color space '{referenceInput.ColorSpace}' does not match candidate color space '{candidateInput.ColorSpace}'; implicit conversion is forbidden.", "inputs"));
            }

            ImageComparisonParameters parameters = (ImageComparisonParameters)context.Parameters;
            if (context.Invocation.Roi is not null and not RectangleAlgorithmRoi and not CircleAlgorithmRoi and not PolygonAlgorithmRoi)
                return ValueTask.FromResult(Failure(context, "comparison_roi_unsupported", "Image comparison supports rectangle, circle and polygon ROI only.", "roi"));
            AlgorithmPixelRoi region = context.Invocation.Roi == null
                ? AlgorithmPixelRoi.WholeImage(reference)
                : AlgorithmPixelRoi.Create(context.Invocation.Roi, reference);
            if (region.IsEmpty)
                return ValueTask.FromResult(Failure(context, "comparison_roi_empty", "The comparison ROI contains no reference pixel centers after clipping.", "roi"));
            if (!ImageComparisonOutputPlan.TryResolve(context.Invocation, out ImageComparisonArtifactOutputs requestedOutputs, out string? outputPlanReason))
                return ValueTask.FromResult(Failure(context, "comparison_output_plan_invalid", outputPlanReason!, "metadata"));

            cancellationToken.ThrowIfCancellationRequested();
            int channels = reference.Format.Channels();
            int bytesPerChannel = reference.Format.BitsPerChannel() / 8;
            int exactStride = checked(reference.Width * reference.Format.BytesPerPixel());
            AlgorithmImageFormat signedFormat = SignedFormat(channels);
            int signedStride = checked(reference.Width * signedFormat.BytesPerPixel());
            int visualStride = checked(reference.Width * 3);
            long exactBytes = checked((long)exactStride * reference.Height);
            long signedBytes = checked((long)signedStride * reference.Height);
            long visualBytes = checked((long)visualStride * reference.Height);
            long retainedOutputBytes = EstimateRetainedOutputBytes(requestedOutputs, exactBytes, signedBytes, visualBytes);
            if (retainedOutputBytes > MaximumRetainedOutputBytes
                || RequestedArrayExceedsRuntimeLimit(requestedOutputs, exactBytes, signedBytes, visualBytes))
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "comparison_output_budget_exceeded",
                    $"Requested comparison image artifacts require {retainedOutputBytes.ToString(CultureInfo.InvariantCulture)} bytes; "
                        + $"the retained-output budget is {MaximumRetainedOutputBytes.ToString(CultureInfo.InvariantCulture)} bytes.",
                    "metadata"));
            }

            context.Progress?.Report(new AlgorithmProgress(0.04, "comparison.prepare", "Allocating requested comparison artifacts"));
            byte[]? absoluteData = Has(requestedOutputs, ImageComparisonArtifactOutputs.AbsoluteDifference)
                ? AllocateOutput(exactBytes, cancellationToken)
                : null;
            byte[]? signedData = Has(requestedOutputs, ImageComparisonArtifactOutputs.SignedDifference)
                ? AllocateOutput(signedBytes, cancellationToken)
                : null;
            byte[]? absoluteVisualData = Has(requestedOutputs, ImageComparisonArtifactOutputs.AbsoluteVisualization)
                ? AllocateOutput(visualBytes, cancellationToken)
                : null;
            byte[]? signedVisualData = Has(requestedOutputs, ImageComparisonArtifactOutputs.SignedVisualization)
                ? AllocateOutput(visualBytes, cancellationToken)
                : null;
            byte[]? heatmapData = Has(requestedOutputs, ImageComparisonArtifactOutputs.Heatmap)
                ? AllocateOutput(visualBytes, cancellationToken)
                : null;
            ChannelMetrics[] perChannel = Enumerable.Range(0, channels).Select(_ => new ChannelMetrics()).ToArray();
            ChannelMetrics aggregate = new();
            double nominalPeak = reference.Format.IsFloatingPoint() ? parameters.FloatPeakValue : reference.Format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            double displayMaximum = parameters.HeatmapMaximum > 0 ? parameters.HeatmapMaximum : nominalPeak;
            ReadOnlySpan<byte> left = reference.Data.Span;
            ReadOnlySpan<byte> right = candidate.Data.Span;
            double[] differences = new double[4];
            long floatArtifactOverflowCount = 0;

            for (int y = 0; y < reference.Height; y++)
            {
                if ((y & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    context.Progress?.Report(new AlgorithmProgress(0.05 + 0.75 * y / Math.Max(1, reference.Height), "comparison.scan"));
                }
                int referenceRow = y * reference.Stride;
                int candidateRow = y * candidate.Stride;
                int absoluteRow = y * exactStride;
                int signedRow = y * signedStride;
                int visualRow = y * visualStride;
                for (int x = 0; x < reference.Width; x++)
                {
                    bool inRegion = region.Contains(x, y);
                    bool displayInvalid = false;
                    bool heatmapInvalid = false;
                    double pixelMaximum = 0;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int leftOffset = referenceRow + (x * channels + channel) * bytesPerChannel;
                        int rightOffset = candidateRow + (x * channels + channel) * bytesPerChannel;
                        double leftValue = Read(reference.Format, left, leftOffset);
                        double rightValue = Read(reference.Format, right, rightOffset);
                        double difference = leftValue - rightValue;
                        differences[channel] = difference;
                        if (!double.IsFinite(difference)) displayInvalid = true;
                        double absolute = Math.Abs(difference);
                        if (signedData != null
                            && reference.Format.IsFloatingPoint()
                            && double.IsFinite(difference)
                            && absolute > float.MaxValue)
                        {
                            floatArtifactOverflowCount++;
                        }
                        if (absoluteData != null)
                            WriteAbsolute(reference.Format, absoluteData, absoluteRow + (x * channels + channel) * bytesPerChannel, absolute);
                        if (signedData != null)
                            BinaryPrimitives.WriteSingleLittleEndian(signedData.AsSpan(signedRow + (x * channels + channel) * 4, 4), (float)difference);

                        bool include = channel != 3 || parameters.IncludeAlphaInMetrics;
                        if (!include || !inRegion) continue;
                        if (double.IsFinite(difference))
                        {
                            perChannel[channel].Add(difference);
                            aggregate.Add(difference);
                            pixelMaximum = Math.Max(pixelMaximum, absolute);
                        }
                        else
                        {
                            perChannel[channel].AddInvalid();
                            aggregate.AddInvalid();
                            heatmapInvalid = true;
                        }
                    }
                    if (absoluteVisualData != null)
                        WriteVisualPixel(absoluteVisualData, visualRow + x * 3, differences, channels, displayMaximum, false, displayInvalid);
                    if (signedVisualData != null)
                        WriteVisualPixel(signedVisualData, visualRow + x * 3, differences, channels, displayMaximum, true, displayInvalid);
                    if (heatmapData != null)
                        WriteHeatmapPixel(heatmapData, visualRow + x * 3, pixelMaximum, displayMaximum, inRegion, heatmapInvalid);
                }
            }

            cancellationToken.ThrowIfCancellationRequested();
            if (aggregate.Count == 0)
                return ValueTask.FromResult(Failure(context, "no_finite_samples", "The two inputs contain no finite sample pairs in the selected channels.", "inputs"));
            ImageComparisonQualityResult quality = ImageComparisonQualityAnalyzer.Analyze(
                reference, candidate, region, parameters, cancellationToken, context.Progress);
            context.Progress?.Report(new AlgorithmProgress(0.96, "comparison.artifacts", "Building metrics and images"));
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                if (absoluteData != null)
                    artifacts.Add(new AlgorithmImageArtifact("absolute-difference", "absolute-difference",
                        new AlgorithmImageBuffer(reference.Width, reference.Height, exactStride, reference.Format, absoluteData, reference.DpiX, reference.DpiY),
                        ExactMetadata(reference, candidate, "absolute")));
                if (signedData != null)
                    artifacts.Add(new AlgorithmImageArtifact("signed-difference", "signed-difference",
                        new AlgorithmImageBuffer(reference.Width, reference.Height, signedStride, signedFormat, signedData, reference.DpiX, reference.DpiY),
                        ExactMetadata(reference, candidate, "reference-minus-candidate")));
                if (absoluteVisualData != null)
                    artifacts.Add(new AlgorithmImageArtifact("absolute-difference-visualization", "visualization",
                        new AlgorithmImageBuffer(reference.Width, reference.Height, visualStride, AlgorithmImageFormat.Bgr24, absoluteVisualData, reference.DpiX, reference.DpiY),
                        DisplayMetadata(displayMaximum, "absolute")));
                if (signedVisualData != null)
                    artifacts.Add(new AlgorithmImageArtifact("signed-difference-visualization", "visualization",
                        new AlgorithmImageBuffer(reference.Width, reference.Height, visualStride, AlgorithmImageFormat.Bgr24, signedVisualData, reference.DpiX, reference.DpiY),
                        DisplayMetadata(displayMaximum, "signed-midpoint")));
                if (heatmapData != null)
                    artifacts.Add(new AlgorithmImageArtifact("difference-heatmap", "heatmap",
                        new AlgorithmImageBuffer(reference.Width, reference.Height, visualStride, AlgorithmImageFormat.Bgr24, heatmapData, reference.DpiX, reference.DpiY),
                        DisplayMetadata(displayMaximum, "heatmap")));
                artifacts.Add(BuildMeasurements(perChannel, aggregate, reference.Format, parameters.IncludeAlphaInMetrics, nominalPeak, quality));
                artifacts.Add(BuildTable(perChannel, reference.Format, parameters.IncludeAlphaInMetrics, nominalPeak, quality));
                artifacts.Add(BuildAlignmentTable(quality.Alignment));
                if (context.Invocation.Roi != null)
                {
                    artifacts.Add(new AlgorithmGeometryArtifact("comparison-roi", AlgorithmCoordinateSpace.Pixel, [region.Geometry]));
                    artifacts.Add(new AlgorithmOverlayArtifact("comparison-roi-overlay", AlgorithmOverlayLifetime.Transient,
                        [new AlgorithmOverlayItem(region.Geometry.Id, new AlgorithmOverlayStyle("#FFFFA500", StrokeWidth: 1.5, Label: "Comparison ROI"))]));
                }
                if (quality.Alignment.Status == "ok")
                {
                    artifacts.Add(new AlgorithmGeometryArtifact("alignment-precheck", AlgorithmCoordinateSpace.Pixel,
                    [
                        new AlgorithmGeometry(
                            "alignment-precheck-shift",
                            AlgorithmGeometryKind.Transform,
                            [],
                            Matrix:
                            [
                                1, 0, quality.Alignment.EstimatedShiftX,
                                0, 1, quality.Alignment.EstimatedShiftY,
                                0, 0, 1,
                            ],
                            Residual: 1 - quality.Alignment.BestCorrelation,
                            Confidence: quality.Alignment.Confidence,
                            Measurements: new Dictionary<string, double>
                            {
                                ["shiftX"] = quality.Alignment.EstimatedShiftX,
                                ["shiftY"] = quality.Alignment.EstimatedShiftY,
                                ["correlation"] = quality.Alignment.BestCorrelation,
                                ["overlapFraction"] = quality.Alignment.OverlapFraction,
                            })
                    ]));
                }
                artifacts.Add(new AlgorithmStructuredDataArtifact("image-comparison", ResultSchema, AlgorithmJson.ToElement(new
                {
                    width = reference.Width,
                    height = reference.Height,
                    format = reference.Format.ToString(),
                    colorSpace = referenceInput.ColorSpace,
                    comparedChannels = ComparedChannels(reference.Format, parameters.IncludeAlphaInMetrics),
                    referenceDpi = new { x = reference.DpiX, y = reference.DpiY },
                    candidateDpi = new { x = candidate.DpiX, y = candidate.DpiY },
                    peakValue = nominalPeak,
                    heatmapMaximum = displayMaximum,
                    requestedImageArtifacts = ImageComparisonOutputPlan.Describe(requestedOutputs),
                    retainedOutputBytes,
                    retainedOutputBudgetBytes = MaximumRetainedOutputBytes,
                    region = new
                    {
                        isRoi = context.Invocation.Roi != null,
                        region.MinimumX,
                        region.MinimumY,
                        region.MaximumXExclusive,
                        region.MaximumYExclusive,
                        region.WasClipped,
                    },
                    finiteSampleCount = aggregate.Count,
                    invalidSampleCount = aggregate.InvalidCount,
                    mse = aggregate.MeanSquaredError,
                    rmse = aggregate.RootMeanSquaredError,
                    psnrDb = aggregate.Psnr(nominalPeak),
                    ssim = new
                    {
                        enabled = parameters.EnableSsim,
                        value = quality.Ssim,
                        validWindowCount = quality.ValidSsimWindowCount,
                        invalidWindowCount = quality.InvalidSsimWindowCount,
                        channels = quality.Channels,
                    },
                    alignmentPrecheck = quality.Alignment,
                })));
            }
            catch
            {
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                throw;
            }

            List<AlgorithmDiagnosticMessage> diagnostics = new();
            if (Math.Abs(reference.DpiX - candidate.DpiX) > 0.01 || Math.Abs(reference.DpiY - candidate.DpiY) > 0.01)
                diagnostics.Add(new AlgorithmDiagnosticMessage("dpi_mismatch", "Pixel arrays were compared directly although their DPI metadata differs.", "warning"));
            if (region.WasClipped)
                diagnostics.Add(new AlgorithmDiagnosticMessage("comparison_roi_clipped", "The requested comparison ROI was intersected with the image bounds.", "warning"));
            if (aggregate.InvalidCount > 0)
            {
                diagnostics.Add(new AlgorithmDiagnosticMessage("nonfinite_samples_excluded",
                    $"Excluded {aggregate.InvalidCount.ToString(CultureInfo.InvariantCulture)} non-finite sample pairs from metrics; exact difference artifacts retain NaN."));
            }
            if (floatArtifactOverflowCount > 0)
            {
                diagnostics.Add(new AlgorithmDiagnosticMessage(
                    "float_difference_artifact_overflow",
                    $"{floatArtifactOverflowCount.ToString(CultureInfo.InvariantCulture)} finite double-domain difference sample(s) exceed Float32 artifact range and are stored as IEEE infinity; metrics remain finite double precision.",
                    "warning"));
            }
            if (parameters.EnableSsim && quality.ValidSsimWindowCount == 0)
                diagnostics.Add(new AlgorithmDiagnosticMessage("ssim_unavailable", "No SSIM window met the finite-sample requirement.", "warning"));
            else if (quality.InvalidSsimWindowCount > 0)
                diagnostics.Add(new AlgorithmDiagnosticMessage("ssim_windows_excluded",
                    $"Excluded {quality.InvalidSsimWindowCount.ToString(CultureInfo.InvariantCulture)} SSIM windows with insufficient finite sample pairs."));
            if (quality.Alignment.Status != "ok" && quality.Alignment.Status != "disabled")
                diagnostics.Add(new AlgorithmDiagnosticMessage("alignment_precheck_inconclusive", $"Alignment precheck status: {quality.Alignment.Status}.", "warning"));
            else if (quality.Alignment.Status == "ok"
                     && quality.Alignment.ShiftMagnitudePixels > parameters.AlignmentWarningThresholdPixels)
            {
                diagnostics.Add(new AlgorithmDiagnosticMessage(
                    "alignment_shift_suspected",
                    $"Best sampled correlation occurs at candidate offset ({quality.Alignment.EstimatedShiftX}, {quality.Alignment.EstimatedShiftY}).",
                    "warning",
                    new Dictionary<string, string>
                    {
                        ["shiftMagnitudePixels"] = quality.Alignment.ShiftMagnitudePixels.ToString("R", CultureInfo.InvariantCulture),
                        ["confidence"] = quality.Alignment.Confidence.ToString("R", CultureInfo.InvariantCulture),
                    }));
            }

            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts = artifacts,
                Diagnostics = new AlgorithmExecutionDiagnostics { Messages = diagnostics },
            });
        }

        private static AlgorithmMeasurementArtifact BuildMeasurements(
            ChannelMetrics[] channels,
            ChannelMetrics aggregate,
            AlgorithmImageFormat format,
            bool includeAlpha,
            double peak,
            ImageComparisonQualityResult quality)
        {
            List<AlgorithmMeasurement> values =
            [
                new("comparison.mse", aggregate.MeanSquaredError, "DN^2"),
                new("comparison.rmse", aggregate.RootMeanSquaredError, "DN"),
                new("comparison.psnr_db", aggregate.Psnr(peak), "dB"),
                new("comparison.max_abs_difference", aggregate.MaximumAbsoluteDifference, "DN"),
                new("comparison.finite_sample_count", aggregate.Count, "samples"),
                new("comparison.invalid_sample_count", aggregate.InvalidCount, "samples"),
            ];
            if (double.IsFinite(quality.Ssim)) values.Add(new AlgorithmMeasurement("comparison.ssim", quality.Ssim, "ratio"));
            values.Add(new AlgorithmMeasurement("comparison.ssim.valid_window_count", quality.ValidSsimWindowCount, "windows"));
            values.Add(new AlgorithmMeasurement("comparison.ssim.invalid_window_count", quality.InvalidSsimWindowCount, "windows"));
            values.Add(new AlgorithmMeasurement("comparison.alignment.shift_x", quality.Alignment.EstimatedShiftX, "px"));
            values.Add(new AlgorithmMeasurement("comparison.alignment.shift_y", quality.Alignment.EstimatedShiftY, "px"));
            values.Add(new AlgorithmMeasurement("comparison.alignment.shift_magnitude", quality.Alignment.ShiftMagnitudePixels, "px", Confidence: quality.Alignment.Confidence));
            if (double.IsFinite(quality.Alignment.BestCorrelation))
                values.Add(new AlgorithmMeasurement("comparison.alignment.best_correlation", quality.Alignment.BestCorrelation, "ratio", Confidence: quality.Alignment.Confidence));
            foreach (int channel in ComparedChannelIndexes(format, includeAlpha))
            {
                IReadOnlyDictionary<string, string> qualifiers = new Dictionary<string, string> { ["channelName"] = ChannelName(format, channel) };
                values.Add(new AlgorithmMeasurement("comparison.channel.mse", channels[channel].MeanSquaredError, "DN^2", channel, Qualifiers: qualifiers));
                values.Add(new AlgorithmMeasurement("comparison.channel.rmse", channels[channel].RootMeanSquaredError, "DN", channel, Qualifiers: qualifiers));
                values.Add(new AlgorithmMeasurement("comparison.channel.psnr_db", channels[channel].Psnr(peak), "dB", channel, Qualifiers: qualifiers));
                ImageComparisonChannelSsim? ssim = quality.Channels.SingleOrDefault(item => item.Channel == channel);
                if (ssim != null && double.IsFinite(ssim.Value))
                    values.Add(new AlgorithmMeasurement("comparison.channel.ssim", ssim.Value, "ratio", channel, Qualifiers: qualifiers));
            }
            return new AlgorithmMeasurementArtifact("image-comparison", values);
        }

        private static AlgorithmTableArtifact BuildTable(
            ChannelMetrics[] channels,
            AlgorithmImageFormat format,
            bool includeAlpha,
            double peak,
            ImageComparisonQualityResult quality)
        {
            AlgorithmTableColumn[] columns =
            [
                new("Channel", "string"), new("Index", "integer"), new("MSE", "number", "DN^2"),
                new("RMSE", "number", "DN"), new("PSNRdB", "number", "dB"),
                new("SSIM", "number", "ratio"), new("ValidSsimWindows", "integer", "windows"),
                new("InvalidSsimWindows", "integer", "windows"),
                new("MaxAbsDifference", "number", "DN"), new("FiniteCount", "integer", "samples"),
                new("InvalidCount", "integer", "samples"),
            ];
            IReadOnlyDictionary<string, JsonElement>[] rows = ComparedChannelIndexes(format, includeAlpha).Select(channel =>
            {
                ImageComparisonChannelSsim? ssim = quality.Channels.SingleOrDefault(item => item.Channel == channel);
                return (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>
                {
                    ["Channel"] = AlgorithmJson.ToElement(ChannelName(format, channel)),
                    ["Index"] = AlgorithmJson.ToElement(channel),
                    ["MSE"] = AlgorithmJson.ToElement(channels[channel].MeanSquaredError),
                    ["RMSE"] = AlgorithmJson.ToElement(channels[channel].RootMeanSquaredError),
                    ["PSNRdB"] = AlgorithmJson.ToElement(channels[channel].Psnr(peak)),
                    ["SSIM"] = AlgorithmJson.ToElement(ssim?.Value ?? double.NaN),
                    ["ValidSsimWindows"] = AlgorithmJson.ToElement(ssim?.ValidWindowCount ?? 0),
                    ["InvalidSsimWindows"] = AlgorithmJson.ToElement(ssim?.InvalidWindowCount ?? 0),
                    ["MaxAbsDifference"] = AlgorithmJson.ToElement(channels[channel].MaximumAbsoluteDifference),
                    ["FiniteCount"] = AlgorithmJson.ToElement(channels[channel].Count),
                    ["InvalidCount"] = AlgorithmJson.ToElement(channels[channel].InvalidCount),
                };
            }).ToArray();
            return new AlgorithmTableArtifact("image-comparison-channels", columns, rows);
        }

        private static AlgorithmTableArtifact BuildAlignmentTable(ImageComparisonAlignmentPrecheck alignment)
        {
            AlgorithmTableColumn[] columns =
            [
                new("Status", "string"), new("EstimatedShiftX", "integer", "px"), new("EstimatedShiftY", "integer", "px"),
                new("ShiftMagnitude", "number", "px"), new("BestCorrelation", "number", "ratio"),
                new("ZeroShiftCorrelation", "number", "ratio"), new("PeakMargin", "number", "ratio"),
                new("Confidence", "number", "ratio"), new("OverlapFraction", "number", "ratio"),
                new("SampleCount", "integer", "samples"), new("SampleStep", "integer", "px"),
            ];
            IReadOnlyDictionary<string, JsonElement> row = new Dictionary<string, JsonElement>
            {
                ["Status"] = AlgorithmJson.ToElement(alignment.Status),
                ["EstimatedShiftX"] = AlgorithmJson.ToElement(alignment.EstimatedShiftX),
                ["EstimatedShiftY"] = AlgorithmJson.ToElement(alignment.EstimatedShiftY),
                ["ShiftMagnitude"] = AlgorithmJson.ToElement(alignment.ShiftMagnitudePixels),
                ["BestCorrelation"] = AlgorithmJson.ToElement(alignment.BestCorrelation),
                ["ZeroShiftCorrelation"] = AlgorithmJson.ToElement(alignment.ZeroShiftCorrelation),
                ["PeakMargin"] = AlgorithmJson.ToElement(alignment.PeakMargin),
                ["Confidence"] = AlgorithmJson.ToElement(alignment.Confidence),
                ["OverlapFraction"] = AlgorithmJson.ToElement(alignment.OverlapFraction),
                ["SampleCount"] = AlgorithmJson.ToElement(alignment.SampleCount),
                ["SampleStep"] = AlgorithmJson.ToElement(alignment.SampleStep),
            };
            return new AlgorithmTableArtifact("image-comparison-alignment", columns, [row]);
        }

        private static Dictionary<string, string> ExactMetadata(AlgorithmImageBuffer reference, AlgorithmImageBuffer candidate, string semantics)
            => new Dictionary<string, string>
            {
                ["semantics"] = semantics,
                ["referenceFormat"] = reference.Format.ToString(),
                ["candidateFormat"] = candidate.Format.ToString(),
                ["precision"] = "unscaled-device-values",
                ["metricPrecision"] = reference.Format.IsFloatingPoint() ? "float32-input; float64-difference-and-accumulation" : "exact-integer-input; float64-accumulation",
                ["floatingArtifactOverflow"] = reference.Format.IsFloatingPoint() ? "finite values outside Float32 range are encoded as signed IEEE infinity" : "not-applicable",
            };

        private static Dictionary<string, string> DisplayMetadata(double maximum, string semantics)
            => new()
            {
                ["semantics"] = semantics,
                ["normalizationMaximum"] = maximum.ToString("R", CultureInfo.InvariantCulture),
                ["finiteOutOfRange"] = "saturate-to-display-range",
                ["nonFinite"] = "magenta",
            };

        private static long EstimateRetainedOutputBytes(
            ImageComparisonArtifactOutputs outputs,
            long exactBytes,
            long signedBytes,
            long visualBytes)
        {
            long total = 0;
            if (Has(outputs, ImageComparisonArtifactOutputs.AbsoluteDifference)) total = checked(total + exactBytes);
            if (Has(outputs, ImageComparisonArtifactOutputs.SignedDifference)) total = checked(total + signedBytes);
            if (Has(outputs, ImageComparisonArtifactOutputs.AbsoluteVisualization)) total = checked(total + visualBytes);
            if (Has(outputs, ImageComparisonArtifactOutputs.SignedVisualization)) total = checked(total + visualBytes);
            if (Has(outputs, ImageComparisonArtifactOutputs.Heatmap)) total = checked(total + visualBytes);
            return total;
        }

        private static bool RequestedArrayExceedsRuntimeLimit(
            ImageComparisonArtifactOutputs outputs,
            long exactBytes,
            long signedBytes,
            long visualBytes)
        {
            return (Has(outputs, ImageComparisonArtifactOutputs.AbsoluteDifference) && exactBytes > Array.MaxLength)
                || (Has(outputs, ImageComparisonArtifactOutputs.SignedDifference) && signedBytes > Array.MaxLength)
                || (Has(outputs, ImageComparisonArtifactOutputs.InteractiveVisualizations) && visualBytes > Array.MaxLength);
        }

        private static byte[] AllocateOutput(long bytes, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            return new byte[checked((int)bytes)];
        }

        private static bool Has(ImageComparisonArtifactOutputs outputs, ImageComparisonArtifactOutputs requested)
            => (outputs & requested) != 0;

        private static void WriteVisualPixel(byte[] output, int offset, ReadOnlySpan<double> differences, int channels, double maximum, bool signed, bool invalid)
        {
            if (invalid)
            {
                output[offset] = 255;
                output[offset + 1] = 0;
                output[offset + 2] = 255;
                return;
            }
            for (int color = 0; color < 3; color++)
            {
                int sourceChannel = channels == 1 ? 0 : color;
                double normalized = differences[sourceChannel] / maximum;
                double mapped = signed ? 127.5 + Math.Clamp(normalized, -1, 1) * 127.5 : Math.Clamp(Math.Abs(normalized), 0, 1) * 255;
                output[offset + color] = (byte)Math.Round(mapped, MidpointRounding.AwayFromZero);
            }
        }

        private static void WriteHeatmapPixel(byte[] output, int offset, double value, double maximum, bool included, bool invalid)
        {
            if (!included)
            {
                output[offset] = 0;
                output[offset + 1] = 0;
                output[offset + 2] = 0;
                return;
            }
            if (invalid)
            {
                output[offset] = 255;
                output[offset + 1] = 0;
                output[offset + 2] = 255;
                return;
            }
            double t = Math.Clamp(value / maximum, 0, 1);
            output[offset] = Color(1.5 - Math.Abs(4 * t - 1));
            output[offset + 1] = Color(1.5 - Math.Abs(4 * t - 2));
            output[offset + 2] = Color(1.5 - Math.Abs(4 * t - 3));
        }

        private static byte Color(double value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255, MidpointRounding.AwayFromZero);

        private static double Read(AlgorithmImageFormat format, ReadOnlySpan<byte> data, int offset) => format.BitsPerChannel() switch
        {
            8 => data[offset],
            16 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
            32 when format.IsFloatingPoint() => BinaryPrimitives.ReadSingleLittleEndian(data.Slice(offset, 4)),
            _ => throw new ArgumentOutOfRangeException(nameof(format)),
        };

        private static void WriteAbsolute(AlgorithmImageFormat format, byte[] output, int offset, double value)
        {
            if (format.IsFloatingPoint()) BinaryPrimitives.WriteSingleLittleEndian(output.AsSpan(offset, 4), (float)value);
            else if (format.BitsPerChannel() == 16) BinaryPrimitives.WriteUInt16LittleEndian(output.AsSpan(offset, 2), checked((ushort)value));
            else output[offset] = checked((byte)value);
        }

        private static AlgorithmImageFormat SignedFormat(int channels) => channels switch
        {
            1 => AlgorithmImageFormat.Gray32Float,
            3 => AlgorithmImageFormat.Bgr96Float,
            4 => AlgorithmImageFormat.Bgra128Float,
            _ => throw new ArgumentOutOfRangeException(nameof(channels)),
        };

        private static IEnumerable<int> ComparedChannelIndexes(AlgorithmImageFormat format, bool includeAlpha)
            => Enumerable.Range(0, includeAlpha || format.Channels() < 4 ? format.Channels() : 3);

        private static string[] ComparedChannels(AlgorithmImageFormat format, bool includeAlpha)
            => ComparedChannelIndexes(format, includeAlpha).Select(channel => ChannelName(format, channel)).ToArray();

        private static string ChannelName(AlgorithmImageFormat format, int channel) => format.Channels() switch
        {
            1 => "Gray",
            3 => channel switch { 0 => "B", 1 => "G", _ => "R" },
            4 => channel switch { 0 => "B", 1 => "G", 2 => "R", _ => "A" },
            _ => channel.ToString(CultureInfo.InvariantCulture),
        };

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path = null) => new()
        {
            InvocationId = context.Invocation.InvocationId,
            AlgorithmId = context.Descriptor.Id,
            AlgorithmVersion = context.Descriptor.Version,
            Status = AlgorithmResultStatus.Failed,
            Failures = [new AlgorithmFailure(code, message, path)],
        };

        private sealed class ChannelMetrics
        {
            private double _sumSquared;
            private double _compensation;

            public long Count { get; private set; }
            public long InvalidCount { get; private set; }
            public double MaximumAbsoluteDifference { get; private set; }
            public double MeanSquaredError => Count == 0 ? double.NaN : _sumSquared / Count;
            public double RootMeanSquaredError => Math.Sqrt(MeanSquaredError);

            public void Add(double difference)
            {
                double squared = difference * difference;
                double adjusted = squared - _compensation;
                double total = _sumSquared + adjusted;
                _compensation = (total - _sumSquared) - adjusted;
                _sumSquared = total;
                Count++;
                MaximumAbsoluteDifference = Math.Max(MaximumAbsoluteDifference, Math.Abs(difference));
            }

            public void AddInvalid() => InvalidCount++;

            public double Psnr(double peak) => MeanSquaredError == 0 ? double.PositiveInfinity : 20 * Math.Log10(peak / RootMeanSquaredError);
        }
    }
}
