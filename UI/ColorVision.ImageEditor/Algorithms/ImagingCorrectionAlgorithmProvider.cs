using ColorVision.Algorithms;
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>CPU V1 reference-image correction provider; all caller inputs remain read-only.</summary>
    public sealed class ImagingCorrectionAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public const string ResultSchema = "colorvision.calibration.imaging-correction/v1";
        private const string SourceName = "source";
        private const string DarkName = "dark-frame";
        private const string FlatName = "flat-field";
        private const string ShadingName = "shading-reference";
        private const string BadPixelName = "bad-pixel-map";

        private static readonly IReadOnlySet<AlgorithmImageFormat> Formats = new HashSet<AlgorithmImageFormat>
        {
            AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Gray16, AlgorithmImageFormat.Gray32Float,
            AlgorithmImageFormat.Bgr24, AlgorithmImageFormat.Bgr48, AlgorithmImageFormat.Bgr96Float,
            AlgorithmImageFormat.Bgra32, AlgorithmImageFormat.Bgra64, AlgorithmImageFormat.Bgra128Float,
        };

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.cpu.imaging-correction.v1",
            "ColorVision CPU imaging correction",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            185,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local
                | AlgorithmHostCapabilities.Deterministic | AlgorithmHostCapabilities.MultiInput,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.ImagingCorrection, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            reason = descriptor.Id == StandardAlgorithmIds.ImagingCorrection ? null : "The provider implements only imaging correction.";
            return reason == null;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            ImagingCorrectionParameters parameters = (ImagingCorrectionParameters)context.Parameters;
            if (!TryIndexInputs(context, out Dictionary<string, AlgorithmInput>? inputs, out AlgorithmResult? failure))
                return ValueTask.FromResult(failure!);

            AlgorithmInput sourceInput = inputs![SourceName];
            AlgorithmImageBuffer source = sourceInput.Image;
            if (!TryResolveReference(context, inputs, DarkName, parameters.EnableDarkFrame, source, exactSourceFormat: true, out AlgorithmImageBuffer? dark, out failure)
                || !TryResolveReference(context, inputs, FlatName, parameters.EnableFlatField, source, exactSourceFormat: true, out AlgorithmImageBuffer? flat, out failure)
                || !TryResolveReference(context, inputs, ShadingName, parameters.EnableShading, source, exactSourceFormat: true, out AlgorithmImageBuffer? shading, out failure)
                || !TryResolveReference(context, inputs, BadPixelName, parameters.EnableBadPixelCorrection, source, exactSourceFormat: false, out AlgorithmImageBuffer? badMap, out failure))
            {
                return ValueTask.FromResult(failure!);
            }
            if (badMap != null && badMap.Format != AlgorithmImageFormat.Gray8)
                return ValueTask.FromResult(Failure(context, "bad_pixel_map_format_mismatch", "Input 'bad-pixel-map' must use Gray8; nonzero mask samples mark bad pixels.", "inputs.bad-pixel-map"));

            cancellationToken.ThrowIfCancellationRequested();
            context.Progress?.Report(new AlgorithmProgress(0.05, "imaging-correction.references"));
            int correctedChannels = parameters.CorrectAlpha || source.Format.Channels() != 4 ? source.Format.Channels() : 3;
            StageStatistics darkStatistics = dark == null
                ? StageStatistics.Disabled("dark-frame", correctedChannels)
                : ScanDark(source, dark, correctedChannels, parameters, cancellationToken);
            StageStatistics flatStatistics = flat == null
                ? StageStatistics.Disabled("flat-field", correctedChannels)
                : ScanFlat(source, dark, flat, correctedChannels, parameters, cancellationToken);
            StageStatistics shadingStatistics = shading == null
                ? StageStatistics.Disabled("shading", correctedChannels)
                : ScanShading(source, dark, flat, shading, correctedChannels, parameters, flatStatistics.Targets, cancellationToken);

            foreach (StageStatistics stage in new[] { darkStatistics, flatStatistics, shadingStatistics })
            {
                if (!stage.Enabled) continue;
                if (stage.ValidPixels == 0)
                {
                    return ValueTask.FromResult(Failure(context, "reference_valid_fraction_too_low",
                        $"Reference stage '{stage.Name}' has no pixel for which every corrected channel is valid.",
                        $"inputs.{stage.Name}", new Dictionary<string, string>
                        {
                            ["stage"] = stage.Name,
                            ["validPixels"] = "0",
                            ["invalidPixels"] = stage.InvalidPixels.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        }));
                }
                if (stage.ValidFraction < parameters.MinimumValidReferenceFraction)
                {
                    return ValueTask.FromResult(Failure(context, "reference_valid_fraction_too_low",
                        $"Reference stage '{stage.Name}' valid fraction {stage.ValidFraction:G8} is below {parameters.MinimumValidReferenceFraction:G8}.",
                        $"inputs.{stage.Name}", new Dictionary<string, string>
                        {
                            ["stage"] = stage.Name,
                            ["validFraction"] = stage.ValidFraction.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
                        }));
                }
                if (parameters.InvalidReferencePolicy == InvalidReferencePixelPolicy.RejectInvocation && stage.InvalidSamples > 0)
                    return ValueTask.FromResult(Failure(context, "invalid_reference_pixel", $"Reference stage '{stage.Name}' contains {stage.InvalidSamples} invalid samples.", $"inputs.{stage.Name}"));
            }

            int stride = checked(source.Width * source.Format.BytesPerPixel());
            byte[] outputBytes = new byte[checked(stride * source.Height)];
            byte[] validityBytes = new byte[checked(source.Width * source.Height)];
            Array.Fill(validityBytes, byte.MaxValue);
            CorrectionStatistics correction = new();
            try
            {
                if (dark == null && flat == null && shading == null && badMap == null)
                {
                    int rowBytes = checked(source.Width * source.Format.BytesPerPixel());
                    for (int y = 0; y < source.Height; y++)
                    {
                        if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                        source.Data.Span.Slice(y * source.Stride, rowBytes).CopyTo(outputBytes.AsSpan(y * rowBytes, rowBytes));
                    }
                }
                else
                {
                    ApplyRadiometric(
                        source, dark, flat, shading, outputBytes, validityBytes, correctedChannels,
                        parameters, flatStatistics.Targets, shadingStatistics.Targets[0], correction,
                        cancellationToken, context.Progress);
                }
                if (badMap != null)
                {
                    ApplyBadPixels(source, badMap, outputBytes, validityBytes, correctedChannels, parameters, correction, cancellationToken, context.Progress);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            catch (InvalidReferenceException exception)
            {
                return ValueTask.FromResult(Failure(context, exception.Code, exception.Message, exception.Path));
            }

            long validOutputPixels = validityBytes.LongCount(value => value != 0);
            long totalOutputPixels = (long)source.Width * source.Height;
            double validOutputFraction = validOutputPixels / (double)totalOutputPixels;
            bool hasRadiometricReference = dark != null || flat != null || shading != null;
            if (hasRadiometricReference && (validOutputPixels == 0 || validOutputFraction < parameters.MinimumValidReferenceFraction))
            {
                return ValueTask.FromResult(Failure(
                    context,
                    validOutputPixels == 0 ? "correction_no_valid_pixels" : "correction_valid_fraction_too_low",
                    validOutputPixels == 0
                        ? "The enabled correction stages produced no pixel for which every corrected channel is valid."
                        : $"The corrected output valid fraction {validOutputFraction:G8} is below {parameters.MinimumValidReferenceFraction:G8}.",
                    "output", new Dictionary<string, string>
                    {
                        ["validPixels"] = validOutputPixels.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["totalPixels"] = totalOutputPixels.ToString(System.Globalization.CultureInfo.InvariantCulture),
                        ["validFraction"] = validOutputFraction.ToString("G17", System.Globalization.CultureInfo.InvariantCulture),
                    }));
            }

            AlgorithmImageBuffer output = new(source.Width, source.Height, stride, source.Format, outputBytes, source.DpiX, source.DpiY);
            AlgorithmImageBuffer validity = new(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8, validityBytes, source.DpiX, source.DpiY);
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                artifacts.Add(new AlgorithmImageArtifact("corrected-image", "primary", output, BuildImageMetadata(context, parameters, inputs)));
                artifacts.Add(new AlgorithmImageArtifact("correction-validity-mask", "validity-mask", validity,
                    new Dictionary<string, string> { ["valid"] = "255", ["invalid"] = "0" }));
                artifacts.Add(BuildMeasurements(source, parameters, darkStatistics, flatStatistics, shadingStatistics, correction, validityBytes));
                artifacts.Add(BuildStageTable(darkStatistics, flatStatistics, shadingStatistics));
                artifacts.Add(BuildProvenanceTable(inputs));
                artifacts.Add(new AlgorithmStructuredDataArtifact("imaging-correction", ResultSchema, AlgorithmJson.ToElement(new
                {
                    stageOrder = new[] { "dark-frame subtraction", "per-channel flat-field gain", "shared-channel residual shading gain", "bad-pixel neighborhood median" },
                    sampleSemantics = "integer inputs are normalized by their nominal peak; float inputs retain native values and references are expected in the calibrated 0..1 range",
                    invalidReferencePolicy = parameters.InvalidReferencePolicy.ToString(),
                    outputRangePolicy = parameters.OutputRangePolicy.ToString(),
                    parameters.CorrectAlpha,
                    source = new { source.Width, source.Height, format = source.Format.ToString(), source.DpiX, source.DpiY },
                    stages = new[] { StageData(darkStatistics), StageData(flatStatistics), StageData(shadingStatistics) },
                    badPixels = new { enabled = badMap != null, correction.BadPixelsMarked, correction.BadPixelsCorrected, correction.BadPixelsUnresolved, parameters.BadPixelRadius },
                    output = new { correction.InvalidOutputPixels, correction.ClippedLowSamples, correction.ClippedHighSamples, correction.NonFiniteOutputSamples, validPixels = validityBytes.LongCount(value => value != 0) },
                    calibration = new { source = parameters.CalibrationSource, version = parameters.CalibrationVersion, checksum = parameters.CalibrationChecksum },
                    referenceLocators = new { parameters.DarkFramePath, parameters.FlatFieldPath, parameters.ShadingReferencePath, parameters.BadPixelMapPath },
                    inputs = inputs.Values.Select(value => new { value.Name, value.SourceUri, value.SourceRevision, value.Checksum, value.ColorSpace }).ToArray(),
                    presetId = context.Invocation.PresetId,
                    parameterSchemaVersion = context.Invocation.ParameterSchemaVersion,
                })));
                context.Progress?.Report(new AlgorithmProgress(1, "imaging-correction.complete"));
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = artifacts,
                    Diagnostics = BuildDiagnostics(parameters, darkStatistics, flatStatistics, shadingStatistics, correction),
                });
            }
            catch
            {
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                if (artifacts.Count == 0)
                {
                    output.Dispose();
                    validity.Dispose();
                }
                throw;
            }
        }

        private static bool TryIndexInputs(
            AlgorithmExecutionContext context,
            out Dictionary<string, AlgorithmInput>? inputs,
            out AlgorithmResult? failure)
        {
            inputs = new Dictionary<string, AlgorithmInput>(StringComparer.Ordinal);
            failure = null;
            HashSet<string> supported = new(StringComparer.Ordinal) { SourceName, DarkName, FlatName, ShadingName, BadPixelName };
            foreach (AlgorithmInput input in context.Inputs)
            {
                if (!supported.Contains(input.Name))
                {
                    failure = Failure(context, "unknown_input_role", $"Input role '{input.Name}' is unsupported.", "inputs");
                    return false;
                }
                if (!inputs.TryAdd(input.Name, input))
                {
                    failure = Failure(context, "duplicate_input_role", $"Input role '{input.Name}' occurs more than once.", $"inputs.{input.Name}");
                    return false;
                }
            }
            if (!inputs.ContainsKey(SourceName))
            {
                failure = Failure(context, "source_input_missing", "Input 'source' is required.", "inputs.source");
                return false;
            }
            return true;
        }

        private static bool TryResolveReference(
            AlgorithmExecutionContext context,
            IReadOnlyDictionary<string, AlgorithmInput> inputs,
            string name,
            bool enabled,
            AlgorithmImageBuffer source,
            bool exactSourceFormat,
            out AlgorithmImageBuffer? reference,
            out AlgorithmResult? failure)
        {
            reference = null;
            failure = null;
            bool present = inputs.TryGetValue(name, out AlgorithmInput? input);
            if (!enabled && present)
            {
                failure = Failure(context, "disabled_reference_supplied", $"Input '{name}' was supplied while its correction stage is disabled.", $"inputs.{name}");
                return false;
            }
            if (!enabled) return true;
            if (!present)
            {
                failure = Failure(context, "reference_input_missing", $"Enabled correction stage requires input '{name}'.", $"inputs.{name}");
                return false;
            }
            reference = input!.Image;
            if (reference.Width != source.Width || reference.Height != source.Height)
            {
                failure = Failure(context, "reference_size_mismatch", $"Input '{name}' must have the same dimensions as 'source'.", $"inputs.{name}");
                return false;
            }
            if (exactSourceFormat && reference.Format != source.Format)
            {
                failure = Failure(context, "reference_format_mismatch", $"Input '{name}' format {reference.Format} does not match source format {source.Format}.", $"inputs.{name}");
                return false;
            }
            string? sourceColor = inputs[SourceName].ColorSpace;
            if (exactSourceFormat && !string.Equals(sourceColor, input.ColorSpace, StringComparison.Ordinal))
            {
                failure = Failure(context, "reference_color_space_mismatch", $"Input '{name}' color space does not match 'source'.", $"inputs.{name}");
                return false;
            }
            return true;
        }

        private static StageStatistics ScanDark(
            AlgorithmImageBuffer source,
            AlgorithmImageBuffer dark,
            int channels,
            ImagingCorrectionParameters parameters,
            CancellationToken cancellationToken)
        {
            long validPixels = 0;
            long invalidPixels = 0;
            long validSamples = 0;
            long invalidSamples = 0;
            long[] channelValid = new long[channels];
            double[] sums = new double[channels];
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> row = dark.Data.Span.Slice(y * dark.Stride, dark.Stride);
                for (int x = 0; x < source.Width; x++)
                {
                    bool pixelValid = true;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        double value = ReadSample(row, dark.Format, x * dark.Format.Channels() + channel);
                        if (ValidDarkReference(value, parameters))
                        {
                            validSamples++;
                            channelValid[channel]++;
                            sums[channel] += value;
                        }
                        else
                        {
                            invalidSamples++;
                            pixelValid = false;
                        }
                    }
                    if (pixelValid) validPixels++;
                    else invalidPixels++;
                }
            }
            double[] targets = new double[channels];
            for (int channel = 0; channel < channels; channel++) targets[channel] = channelValid[channel] == 0 ? 0 : sums[channel] / channelValid[channel];
            return new StageStatistics("dark-frame", true, validPixels, invalidPixels, validSamples, invalidSamples, targets);
        }

        private static StageStatistics ScanFlat(
            AlgorithmImageBuffer source,
            AlgorithmImageBuffer? dark,
            AlgorithmImageBuffer flat,
            int channels,
            ImagingCorrectionParameters parameters,
            CancellationToken cancellationToken)
        {
            long validPixels = 0;
            long invalidPixels = 0;
            long validSamples = 0;
            long invalidSamples = 0;
            long[] channelValid = new long[channels];
            double[] sums = new double[channels];
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> flatRow = flat.Data.Span.Slice(y * flat.Stride, flat.Stride);
                ReadOnlySpan<byte> darkRow = dark == null ? ReadOnlySpan<byte>.Empty : dark.Data.Span.Slice(y * dark.Stride, dark.Stride);
                for (int x = 0; x < source.Width; x++)
                {
                    bool pixelValid = true;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        double raw = ReadSample(flatRow, flat.Format, x * flat.Format.Channels() + channel);
                        double darkValue = dark == null ? 0 : ReadSample(darkRow, dark.Format, x * dark.Format.Channels() + channel);
                        double response = raw - darkValue;
                        if (ValidReference(raw, response, parameters))
                        {
                            validSamples++; channelValid[channel]++; sums[channel] += response;
                        }
                        else
                        {
                            invalidSamples++;
                            pixelValid = false;
                        }
                    }
                    if (pixelValid) validPixels++;
                    else invalidPixels++;
                }
            }
            double[] targets = new double[channels];
            for (int channel = 0; channel < channels; channel++) targets[channel] = channelValid[channel] == 0 ? 0 : sums[channel] / channelValid[channel];
            return new StageStatistics("flat-field", true, validPixels, invalidPixels, validSamples, invalidSamples, targets);
        }

        private static StageStatistics ScanShading(
            AlgorithmImageBuffer source,
            AlgorithmImageBuffer? dark,
            AlgorithmImageBuffer? flat,
            AlgorithmImageBuffer shading,
            int channels,
            ImagingCorrectionParameters parameters,
            double[] flatTargets,
            CancellationToken cancellationToken)
        {
            long validPixels = 0;
            long invalidPixels = 0;
            long validSamples = 0;
            long invalidSamples = 0;
            double sum = 0;
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                ReadOnlySpan<byte> shadingRow = shading.Data.Span.Slice(y * shading.Stride, shading.Stride);
                ReadOnlySpan<byte> darkRow = dark == null ? ReadOnlySpan<byte>.Empty : dark.Data.Span.Slice(y * dark.Stride, dark.Stride);
                ReadOnlySpan<byte> flatRow = flat == null ? ReadOnlySpan<byte>.Empty : flat.Data.Span.Slice(y * flat.Stride, flat.Stride);
                for (int x = 0; x < source.Width; x++)
                {
                    double pixelSum = 0;
                    bool pixelValid = true;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        int sampleIndex = x * shading.Format.Channels() + channel;
                        double raw = ReadSample(shadingRow, shading.Format, sampleIndex);
                        double darkValue = dark == null ? 0 : ReadSample(darkRow, dark.Format, sampleIndex);
                        double value = raw - darkValue;
                        bool channelIsValid = ValidReference(raw, value, parameters);
                        if (flat != null)
                        {
                            double flatRaw = ReadSample(flatRow, flat.Format, sampleIndex);
                            double flatResponse = flatRaw - darkValue;
                            if (!ValidReference(flatRaw, flatResponse, parameters)) channelIsValid = false;
                            else if (channelIsValid) value *= ClampGain(flatTargets[channel] / flatResponse, parameters);
                        }
                        if (channelIsValid)
                        {
                            validSamples++;
                            pixelSum += value;
                        }
                        else
                        {
                            invalidSamples++;
                            pixelValid = false;
                        }
                    }
                    if (pixelValid)
                    {
                        validPixels++;
                        sum += pixelSum / channels;
                    }
                    else invalidPixels++;
                }
            }
            double target = validPixels == 0 ? 0 : sum / validPixels;
            return new StageStatistics("shading", true, validPixels, invalidPixels, validSamples, invalidSamples, Enumerable.Repeat(target, channels).ToArray());
        }

        private static void ApplyRadiometric(
            AlgorithmImageBuffer source,
            AlgorithmImageBuffer? dark,
            AlgorithmImageBuffer? flat,
            AlgorithmImageBuffer? shading,
            byte[] destination,
            byte[] validity,
            int correctedChannels,
            ImagingCorrectionParameters parameters,
            double[] flatTargets,
            double shadingTarget,
            CorrectionStatistics statistics,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int sourceChannels = source.Format.Channels();
            int bytesPerPixel = source.Format.BytesPerPixel();
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.2 + 0.55 * y / Math.Max(1, source.Height), "imaging-correction.apply"));
                }
                ReadOnlySpan<byte> sourceRow = source.Data.Span.Slice(y * source.Stride, source.Stride);
                ReadOnlySpan<byte> darkRow = dark == null ? ReadOnlySpan<byte>.Empty : dark.Data.Span.Slice(y * dark.Stride, dark.Stride);
                ReadOnlySpan<byte> flatRow = flat == null ? ReadOnlySpan<byte>.Empty : flat.Data.Span.Slice(y * flat.Stride, flat.Stride);
                ReadOnlySpan<byte> shadingRow = shading == null ? ReadOnlySpan<byte>.Empty : shading.Data.Span.Slice(y * shading.Stride, shading.Stride);
                Span<byte> outputRow = destination.AsSpan(y * source.Width * bytesPerPixel, source.Width * bytesPerPixel);
                for (int x = 0; x < source.Width; x++)
                {
                    bool pixelValid = true;
                    double shadingGain = 1;
                    if (shading != null)
                    {
                        double responseSum = 0;
                        bool valid = true;
                        for (int channel = 0; channel < correctedChannels; channel++)
                        {
                            int sampleIndex = x * sourceChannels + channel;
                            double raw = ReadSample(shadingRow, shading.Format, sampleIndex);
                            double darkValue = dark == null ? 0 : ReadSample(darkRow, dark.Format, sampleIndex);
                            double response = raw - darkValue;
                            if (!ValidReference(raw, response, parameters)) { valid = false; break; }
                            if (flat != null)
                            {
                                double flatRaw = ReadSample(flatRow, flat.Format, sampleIndex);
                                double flatResponse = flatRaw - darkValue;
                                if (!ValidReference(flatRaw, flatResponse, parameters)) { valid = false; break; }
                                response *= ClampGain(flatTargets[channel] / flatResponse, parameters);
                            }
                            responseSum += response;
                        }
                        if (valid) shadingGain = ClampGain(shadingTarget / (responseSum / correctedChannels), parameters);
                        else pixelValid = false;
                    }

                    for (int channel = 0; channel < sourceChannels; channel++)
                    {
                        int sampleIndex = x * sourceChannels + channel;
                        double original = ReadSample(sourceRow, source.Format, sampleIndex);
                        double corrected = original;
                        bool valid = double.IsFinite(original);
                        if (channel < correctedChannels && valid)
                        {
                            if (dark != null)
                            {
                                double darkValue = ReadSample(darkRow, dark.Format, sampleIndex);
                                if (!ValidDarkReference(darkValue, parameters)) valid = false;
                                else corrected -= darkValue;
                            }
                            if (flat != null && valid)
                            {
                                double flatRaw = ReadSample(flatRow, flat.Format, sampleIndex);
                                double darkValue = dark == null ? 0 : ReadSample(darkRow, dark.Format, sampleIndex);
                                double response = flatRaw - darkValue;
                                if (!ValidReference(flatRaw, response, parameters)) valid = false;
                                else corrected *= ClampGain(flatTargets[channel] / response, parameters);
                            }
                            if (shading != null)
                            {
                                if (!pixelValid) valid = false;
                                else corrected *= shadingGain;
                            }
                        }
                        string invalidMessage = $"Invalid source/reference or non-representable output sample at ({x}, {y}), channel {channel}.";
                        if (!valid) corrected = ResolveInvalid(parameters, original, invalidMessage);
                        if (!TryWriteSample(outputRow, source.Format, sampleIndex, corrected, parameters.OutputRangePolicy, statistics))
                        {
                            statistics.NonFiniteOutputSamples++;
                            if (valid) corrected = ResolveInvalid(parameters, original, invalidMessage);
                            if (!TryWriteSample(outputRow, source.Format, sampleIndex, corrected, parameters.OutputRangePolicy, statistics))
                            {
                                // PreserveSource cannot preserve NaN/Infinity. A finite zero is the only
                                // format-independent safe representation; the validity mask and diagnostics
                                // retain the loss instead of publishing a non-finite sample as valid.
                                if (!TryWriteSample(outputRow, source.Format, sampleIndex, 0, parameters.OutputRangePolicy, statistics))
                                    throw new InvalidReferenceException("invalid_output_sample", invalidMessage, "output");
                            }
                            valid = false;
                        }
                        if (!valid) pixelValid = false;
                    }
                    if (!pixelValid)
                    {
                        validity[y * source.Width + x] = 0;
                        statistics.InvalidOutputPixels++;
                    }
                }
            }
        }

        private static void ApplyBadPixels(
            AlgorithmImageBuffer source,
            AlgorithmImageBuffer badMap,
            byte[] destination,
            byte[] validity,
            int correctedChannels,
            ImagingCorrectionParameters parameters,
            CorrectionStatistics statistics,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int channels = source.Format.Channels();
            int bytesPerPixel = source.Format.BytesPerPixel();
            double threshold = parameters.BadPixelThresholdNormalized;
            List<double> neighborhood = new((parameters.BadPixelRadius * 2 + 1) * (parameters.BadPixelRadius * 2 + 1));
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.76 + 0.18 * y / Math.Max(1, source.Height), "imaging-correction.bad-pixels"));
                }
                ReadOnlySpan<byte> mapRow = badMap.Data.Span.Slice(y * badMap.Stride, badMap.Stride);
                for (int x = 0; x < source.Width; x++)
                {
                    if (ReadSample(mapRow, AlgorithmImageFormat.Gray8, x) <= threshold) continue;
                    statistics.BadPixelsMarked++;
                    bool resolved = true;
                    for (int channel = 0; channel < correctedChannels; channel++)
                    {
                        neighborhood.Clear();
                        for (int neighborY = Math.Max(0, y - parameters.BadPixelRadius); neighborY <= Math.Min(source.Height - 1, y + parameters.BadPixelRadius); neighborY++)
                        {
                            ReadOnlySpan<byte> neighborMap = badMap.Data.Span.Slice(neighborY * badMap.Stride, badMap.Stride);
                            ReadOnlySpan<byte> outputRow = destination.AsSpan(neighborY * source.Width * bytesPerPixel, source.Width * bytesPerPixel);
                            for (int neighborX = Math.Max(0, x - parameters.BadPixelRadius); neighborX <= Math.Min(source.Width - 1, x + parameters.BadPixelRadius); neighborX++)
                            {
                                if (neighborX == x && neighborY == y) continue;
                                if (ReadSample(neighborMap, AlgorithmImageFormat.Gray8, neighborX) > threshold) continue;
                                if (validity[neighborY * source.Width + neighborX] == 0) continue;
                                neighborhood.Add(ReadSample(outputRow, source.Format, neighborX * channels + channel));
                            }
                        }
                        if (neighborhood.Count == 0) { resolved = false; break; }
                        neighborhood.Sort();
                        int middle = neighborhood.Count / 2;
                        double median = neighborhood.Count % 2 == 0
                            ? (neighborhood[middle - 1] + neighborhood[middle]) / 2
                            : neighborhood[middle];
                        Span<byte> targetRow = destination.AsSpan(y * source.Width * bytesPerPixel, source.Width * bytesPerPixel);
                        WriteSample(targetRow, source.Format, x * channels + channel, median, parameters.OutputRangePolicy, statistics);
                    }
                    if (resolved)
                    {
                        statistics.BadPixelsCorrected++;
                        validity[y * source.Width + x] = byte.MaxValue;
                    }
                    else
                    {
                        statistics.BadPixelsUnresolved++;
                        validity[y * source.Width + x] = 0;
                        if (parameters.InvalidReferencePolicy == InvalidReferencePixelPolicy.RejectInvocation)
                            throw new InvalidReferenceException("bad_pixel_unresolved", $"Bad pixel ({x}, {y}) has no valid neighbor within radius {parameters.BadPixelRadius}.", "inputs.bad-pixel-map");
                        Span<byte> targetRow = destination.AsSpan(y * source.Width * bytesPerPixel, source.Width * bytesPerPixel);
                        ReadOnlySpan<byte> sourceRow = source.Data.Span.Slice(y * source.Stride, source.Stride);
                        for (int channel = 0; channel < correctedChannels; channel++)
                        {
                            double value = parameters.InvalidReferencePolicy == InvalidReferencePixelPolicy.FillConstant
                                ? parameters.InvalidReferenceFillNormalized
                                : ReadSample(sourceRow, source.Format, x * channels + channel);
                            WriteSample(targetRow, source.Format, x * channels + channel, value, parameters.OutputRangePolicy, statistics);
                        }
                    }
                }
            }
        }

        private static bool ValidReference(double raw, double response, ImagingCorrectionParameters parameters)
            => double.IsFinite(raw) && double.IsFinite(response)
                && response > parameters.ReferenceZeroThresholdNormalized
                && (!parameters.RejectSaturatedReferencePixels || raw < parameters.ReferenceSaturationThresholdNormalized);

        private static bool ValidDarkReference(double value, ImagingCorrectionParameters parameters)
            => double.IsFinite(value)
                && (!parameters.RejectSaturatedReferencePixels || value < parameters.ReferenceSaturationThresholdNormalized);

        private static double ClampGain(double value, ImagingCorrectionParameters parameters)
            => Math.Clamp(value, parameters.MinimumGain, parameters.MaximumGain);

        private static double ResolveInvalid(ImagingCorrectionParameters parameters, double original, string message)
            => parameters.InvalidReferencePolicy switch
            {
                InvalidReferencePixelPolicy.RejectInvocation => throw new InvalidReferenceException("invalid_reference_pixel", message, "inputs"),
                InvalidReferencePixelPolicy.FillConstant => parameters.InvalidReferenceFillNormalized,
                _ => original,
            };

        private static double ReadSample(ReadOnlySpan<byte> row, AlgorithmImageFormat format, int sampleIndex)
        {
            int bits = format.BitsPerChannel();
            return bits switch
            {
                8 => row[sampleIndex] / 255d,
                16 => BinaryPrimitives.ReadUInt16LittleEndian(row.Slice(sampleIndex * 2, 2)) / 65535d,
                32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(row.Slice(sampleIndex * 4, 4))),
                _ => throw new ArgumentOutOfRangeException(nameof(format)),
            };
        }

        private static void WriteSample(
            Span<byte> row,
            AlgorithmImageFormat format,
            int sampleIndex,
            double value,
            ImagingCorrectionOutputRangePolicy rangePolicy,
            CorrectionStatistics statistics)
        {
            if (!TryWriteSample(row, format, sampleIndex, value, rangePolicy, statistics))
                throw new InvalidReferenceException("invalid_output_sample", "The correction produced a sample that cannot be represented by the target format.", "output");
        }

        private static bool TryWriteSample(
            Span<byte> row,
            AlgorithmImageFormat format,
            int sampleIndex,
            double value,
            ImagingCorrectionOutputRangePolicy rangePolicy,
            CorrectionStatistics statistics)
        {
            if (!double.IsFinite(value)) return false;
            if (format.IsFloatingPoint())
            {
                if (rangePolicy == ImagingCorrectionOutputRangePolicy.ClampToNominalRange)
                {
                    if (value < 0) statistics.ClippedLowSamples++;
                    if (value > 1) statistics.ClippedHighSamples++;
                    value = Math.Clamp(value, 0, 1);
                }
                else if (value is < -float.MaxValue or > float.MaxValue)
                {
                    return false;
                }
                float single = (float)value;
                if (!float.IsFinite(single)) return false;
                BinaryPrimitives.WriteInt32LittleEndian(row.Slice(sampleIndex * 4, 4), BitConverter.SingleToInt32Bits(single));
                return true;
            }
            double peak = format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            if (value < 0) statistics.ClippedLowSamples++;
            if (value > 1) statistics.ClippedHighSamples++;
            ulong integer = (ulong)Math.Round(Math.Clamp(value, 0, 1) * peak, MidpointRounding.AwayFromZero);
            if (format.BitsPerChannel() == 8) row[sampleIndex] = (byte)integer;
            else BinaryPrimitives.WriteUInt16LittleEndian(row.Slice(sampleIndex * 2, 2), (ushort)integer);
            return true;
        }

        private static IReadOnlyDictionary<string, string> BuildImageMetadata(
            AlgorithmExecutionContext context,
            ImagingCorrectionParameters parameters,
            IReadOnlyDictionary<string, AlgorithmInput> inputs)
            => new Dictionary<string, string>
            {
                ["calibrationSource"] = parameters.CalibrationSource,
                ["calibrationVersion"] = parameters.CalibrationVersion,
                ["calibrationChecksum"] = parameters.CalibrationChecksum,
                ["presetId"] = context.Invocation.PresetId ?? string.Empty,
                ["referenceCount"] = (inputs.Count - 1).ToString(System.Globalization.CultureInfo.InvariantCulture),
            };

        private static AlgorithmMeasurementArtifact BuildMeasurements(
            AlgorithmImageBuffer source,
            ImagingCorrectionParameters parameters,
            StageStatistics dark,
            StageStatistics flat,
            StageStatistics shading,
            CorrectionStatistics correction,
            byte[] validity)
        {
            long validPixels = validity.LongCount(value => value != 0);
            long totalPixels = (long)source.Width * source.Height;
            return new AlgorithmMeasurementArtifact("imaging-correction-summary",
            [
                new("imaging-correction.valid_pixel_count", validPixels, "px"),
                new("imaging-correction.invalid_pixel_count", totalPixels - validPixels, "px"),
                new("imaging-correction.valid_fraction", validPixels / (double)totalPixels, "ratio"),
                new("imaging-correction.dark_valid_fraction", dark.ValidFraction, "ratio"),
                new("imaging-correction.flat_valid_fraction", flat.ValidFraction, "ratio"),
                new("imaging-correction.shading_valid_fraction", shading.ValidFraction, "ratio"),
                new("imaging-correction.dark_valid_sample_fraction", dark.ValidSampleFraction, "ratio"),
                new("imaging-correction.flat_valid_sample_fraction", flat.ValidSampleFraction, "ratio"),
                new("imaging-correction.shading_valid_sample_fraction", shading.ValidSampleFraction, "ratio"),
                new("imaging-correction.bad_pixels_marked", correction.BadPixelsMarked, "px"),
                new("imaging-correction.bad_pixels_corrected", correction.BadPixelsCorrected, "px"),
                new("imaging-correction.bad_pixels_unresolved", correction.BadPixelsUnresolved, "px"),
                new("imaging-correction.clipped_low_samples", correction.ClippedLowSamples, "sample"),
                new("imaging-correction.clipped_high_samples", correction.ClippedHighSamples, "sample"),
                new("imaging-correction.non_finite_output_sample_count", correction.NonFiniteOutputSamples, "sample"),
                new("imaging-correction.alpha_corrected", parameters.CorrectAlpha ? 1 : 0, "boolean"),
            ]);
        }

        private static AlgorithmTableArtifact BuildStageTable(
            StageStatistics dark,
            StageStatistics flat,
            StageStatistics shading)
        {
            StageStatistics[] stages = [dark, flat, shading];
            IReadOnlyDictionary<string, JsonElement>[] rows = stages.Select(stage =>
                (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>
                {
                    ["Stage"] = AlgorithmJson.ToElement(stage.Name),
                    ["Enabled"] = AlgorithmJson.ToElement(stage.Enabled),
                    ["ValidPixels"] = AlgorithmJson.ToElement(stage.ValidPixels),
                    ["InvalidPixels"] = AlgorithmJson.ToElement(stage.InvalidPixels),
                    ["ValidSamples"] = AlgorithmJson.ToElement(stage.ValidSamples),
                    ["InvalidSamples"] = AlgorithmJson.ToElement(stage.InvalidSamples),
                    ["ValidFraction"] = AlgorithmJson.ToElement(stage.ValidFraction),
                    ["ValidSampleFraction"] = AlgorithmJson.ToElement(stage.ValidSampleFraction),
                    ["Target0"] = AlgorithmJson.ToElement(stage.Targets.ElementAtOrDefault(0)),
                    ["Target1"] = AlgorithmJson.ToElement(stage.Targets.ElementAtOrDefault(1)),
                    ["Target2"] = AlgorithmJson.ToElement(stage.Targets.ElementAtOrDefault(2)),
                    ["Target3"] = AlgorithmJson.ToElement(stage.Targets.ElementAtOrDefault(3)),
                }).ToArray();
            return new AlgorithmTableArtifact("imaging-correction-stages",
            [
                new("Stage", "string"), new("Enabled", "boolean"), new("ValidPixels", "integer"), new("InvalidPixels", "integer"),
                new("ValidSamples", "integer"), new("InvalidSamples", "integer"), new("ValidFraction", "number", "ratio"),
                new("ValidSampleFraction", "number", "ratio"),
                new("Target0", "number"), new("Target1", "number"), new("Target2", "number"), new("Target3", "number"),
            ], rows);
        }

        private static AlgorithmTableArtifact BuildProvenanceTable(IReadOnlyDictionary<string, AlgorithmInput> inputs)
        {
            IReadOnlyDictionary<string, JsonElement>[] rows = inputs.Values.OrderBy(value => value.Name, StringComparer.Ordinal).Select(value =>
                (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>
                {
                    ["Role"] = AlgorithmJson.ToElement(value.Name),
                    ["Uri"] = AlgorithmJson.ToElement(value.SourceUri ?? string.Empty),
                    ["Revision"] = AlgorithmJson.ToElement(value.SourceRevision ?? string.Empty),
                    ["Checksum"] = AlgorithmJson.ToElement(value.Checksum ?? string.Empty),
                    ["ColorSpace"] = AlgorithmJson.ToElement(value.ColorSpace ?? string.Empty),
                    ["Format"] = AlgorithmJson.ToElement(value.Image.Format.ToString()),
                }).ToArray();
            return new AlgorithmTableArtifact("imaging-correction-provenance",
                [new("Role", "string"), new("Uri", "string"), new("Revision", "string"), new("Checksum", "string"), new("ColorSpace", "string"), new("Format", "string")], rows);
        }

        private static AlgorithmExecutionDiagnostics BuildDiagnostics(
            ImagingCorrectionParameters parameters,
            StageStatistics dark,
            StageStatistics flat,
            StageStatistics shading,
            CorrectionStatistics correction)
        {
            List<AlgorithmDiagnosticMessage> messages = new();
            if (!dark.Enabled && !flat.Enabled && !shading.Enabled && !parameters.EnableBadPixelCorrection)
                messages.Add(new("imaging_correction_identity", "No correction stage was enabled; the source was copied unchanged.", "info"));
            foreach (StageStatistics stage in new[] { dark, flat, shading })
                if (stage.Enabled && stage.InvalidSamples > 0)
                    messages.Add(new("imaging_correction_invalid_reference_samples", $"Stage '{stage.Name}' contains {stage.InvalidSamples} invalid reference samples handled by {parameters.InvalidReferencePolicy}.", "warning"));
            if (correction.BadPixelsUnresolved > 0)
                messages.Add(new("imaging_correction_unresolved_bad_pixels", $"{correction.BadPixelsUnresolved} bad pixels could not be resolved from the configured neighborhood.", "warning"));
            if (correction.NonFiniteOutputSamples > 0)
                messages.Add(new("imaging_correction_non_finite_output", $"{correction.NonFiniteOutputSamples} output samples were not finite or representable by the target format and were marked invalid.", "warning"));
            return new AlgorithmExecutionDiagnostics { Messages = messages };
        }

        private static object StageData(StageStatistics stage) => new
        {
            name = stage.Name,
            stage.Enabled,
            stage.ValidPixels,
            stage.InvalidPixels,
            stage.ValidSamples,
            stage.InvalidSamples,
            stage.ValidFraction,
            stage.ValidSampleFraction,
            targets = stage.Targets,
        };

        private static AlgorithmResult Failure(
            AlgorithmExecutionContext context,
            string code,
            string message,
            string? path,
            IReadOnlyDictionary<string, string>? details = null)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path, details)],
            };

        private sealed record StageStatistics(
            string Name,
            bool Enabled,
            long ValidPixels,
            long InvalidPixels,
            long ValidSamples,
            long InvalidSamples,
            double[] Targets)
        {
            public double ValidFraction => !Enabled || ValidPixels + InvalidPixels == 0 ? 1 : ValidPixels / (double)(ValidPixels + InvalidPixels);
            public double ValidSampleFraction => !Enabled || ValidSamples + InvalidSamples == 0 ? 1 : ValidSamples / (double)(ValidSamples + InvalidSamples);

            public static StageStatistics Disabled(string name, int channels) => new(name, false, 0, 0, 0, 0, new double[channels]);
        }

        private sealed class CorrectionStatistics
        {
            public long InvalidOutputPixels;
            public long BadPixelsMarked;
            public long BadPixelsCorrected;
            public long BadPixelsUnresolved;
            public long ClippedLowSamples;
            public long ClippedHighSamples;
            public long NonFiniteOutputSamples;
        }

        private sealed class InvalidReferenceException(string code, string message, string path) : Exception(message)
        {
            public string Code { get; } = code;
            public string Path { get; } = path;
        }
    }
}
