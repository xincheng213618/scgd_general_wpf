using ColorVision.Algorithms;
using OpenCvSharp;
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
    /// <summary>
    /// Deterministic luminance DFT analysis. Full-frame image artifacts are display-normalized
    /// Gray8 views; quantitative magnitude and power stay in the tables and structured result.
    /// </summary>
    public sealed class FrequencySpectrumAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public const string ResultSchema = "colorvision.frequency.spectrum-analysis/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.frequency-spectrum.cpu",
            "ColorVision Frequency Spectrum CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            145,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.FrequencySpectrum, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.FrequencySpectrum
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_input_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer source = context.Inputs[0].Image;
            FrequencySpectrumParameters parameters = (FrequencySpectrumParameters)context.Parameters;
            long pixelCount = checked((long)source.Width * source.Height);
            if (pixelCount > parameters.MaximumPixels)
            {
                return ValueTask.FromResult(Failure(context, "frequency_pixel_limit_exceeded",
                    $"The input contains {pixelCount.ToString(CultureInfo.InvariantCulture)} pixels; the configured limit is {parameters.MaximumPixels.ToString(CultureInfo.InvariantCulture)}.",
                    nameof(parameters.MaximumPixels)));
            }

            context.Progress?.Report(new AlgorithmProgress(0.03, "frequency.scan", "Scanning luminance and validating finite samples"));
            if (source.Format.IsFloatingPoint()
                && !TryValidateNormalizedFloat(source, cancellationToken, out int rangeX, out int rangeY, out int rangeChannel, out float rangeValue))
            {
                return ValueTask.FromResult(Failure(context, "frequency_float_out_of_nominal_range",
                    $"Floating-point luminance channel {rangeChannel} at ({rangeX}, {rangeY}) is {rangeValue.ToString("G9", CultureInfo.InvariantCulture)}; V1 requires finite normalized samples in [0,1].",
                    "inputs.source"));
            }
            if (!TryMean(source, cancellationToken, context.Progress, out double sourceMean, out int invalidX, out int invalidY))
            {
                return ValueTask.FromResult(Failure(context, "frequency_nonfinite_input",
                    $"The luminance sample at ({invalidX}, {invalidY}) is NaN or infinity; implicit replacement is forbidden.", "inputs.source"));
            }

            float[] windowX = CreateWindow(source.Width, parameters.WindowFunction);
            float[] windowY = CreateWindow(source.Height, parameters.WindowFunction);
            double windowSum = windowX.Sum(value => (double)value) * windowY.Sum(value => (double)value);
            double windowEnergy = windowX.Sum(value => (double)value * value) * windowY.Sum(value => (double)value * value);
            if (!(windowSum > 0) || !(windowEnergy > 0))
                return ValueTask.FromResult(Failure(context, "frequency_window_degenerate", "The selected window has zero coherent gain for this image size.", nameof(parameters.WindowFunction)));

            AlgorithmImageBuffer? magnitudeImage = null;
            AlgorithmImageBuffer? powerImage = null;
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                using Mat spatial = new(source.Height, source.Width, MatType.CV_32FC1, Scalar.All(0));
                FillSpatial(spatial, source, windowX, windowY, parameters.RemoveMean ? sourceMean : 0, cancellationToken, context.Progress);
                cancellationToken.ThrowIfCancellationRequested();
                context.Progress?.Report(new AlgorithmProgress(0.29, "frequency.dft", "Computing two-dimensional DFT"));
                using Mat spectrum = new();
                Cv2.Dft(spatial, spectrum, DftFlags.ComplexOutput);
                cancellationToken.ThrowIfCancellationRequested();

                context.Progress?.Report(new AlgorithmProgress(0.52, "frequency.aggregate", "Aggregating radial and directional spectra"));
                SpectrumAggregation aggregation = Aggregate(spectrum, windowSum, parameters, cancellationToken, context.Progress);
                (byte[] magnitudeDisplay, byte[] powerDisplay) = CreateDisplays(
                    spectrum, windowSum, aggregation.MaximumMagnitude, aggregation.MaximumPower, parameters, cancellationToken, context.Progress);
                SpectrumPeak[] peaks = DetectPeaks(
                    spectrum, windowSum, aggregation.MaximumEligiblePeakPower, parameters, cancellationToken, context.Progress);

                context.Progress?.Report(new AlgorithmProgress(0.86, "frequency.inverse", "Verifying inverse transform"));
                using Mat reconstructed = new();
                Cv2.Idft(spectrum, reconstructed, DftFlags.RealOutput | DftFlags.Scale);
                (double inverseRmse, double inverseMaximumError) = InverseError(spatial, reconstructed, cancellationToken);

                magnitudeImage = new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8,
                    magnitudeDisplay, 96, 96);
                powerImage = new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8,
                    powerDisplay, 96, 96);
                IReadOnlyDictionary<string, string> displayMetadata = new Dictionary<string, string>
                {
                    ["valueSemantics"] = parameters.VisualizationScale == FrequencySpectrumVisualizationScale.Logarithmic
                        ? "log1p-normalized-to-artifact-maximum"
                        : "linear-normalized-to-artifact-maximum",
                    ["centered"] = parameters.CenterSpectrum.ToString(CultureInfo.InvariantCulture),
                    ["quantitativeValues"] = "frequency-radial-spectrum; frequency-directional-spectrum; frequency-peaks",
                    ["frequencyUnit"] = "cycles/pixel",
                };
                artifacts.Add(new AlgorithmImageArtifact("magnitude-spectrum", "spectrum-magnitude-display", magnitudeImage, displayMetadata));
                magnitudeImage = null;
                artifacts.Add(new AlgorithmImageArtifact("power-spectrum", "spectrum-power-display", powerImage, displayMetadata));
                powerImage = null;
                artifacts.Add(BuildMeasurements(source, sourceMean, windowSum, windowEnergy, aggregation, peaks, inverseRmse, inverseMaximumError));
                artifacts.Add(BuildRadialTable(aggregation.RadialBins));
                artifacts.Add(BuildDirectionalTable(aggregation.DirectionalBins));
                artifacts.Add(BuildPeakTable(peaks));
                artifacts.Add(new AlgorithmStructuredDataArtifact("frequency-spectrum", ResultSchema, AlgorithmJson.ToElement(new
                {
                    input = new { source.Width, source.Height, format = source.Format.ToString(), source.DpiX, source.DpiY, colorSpace = context.Inputs[0].ColorSpace },
                    luminance = new { scale = "nominal-8bit-DN", coefficients = "0.114B+0.587G+0.299R", alphaIgnored = true, floatingPointConvention = "normalized-0..1-mapped-to-0..255" },
                    preprocessing = new { windowFunction = parameters.WindowFunction.ToString(), parameters.RemoveMean, sourceMean, windowSum, windowEnergy },
                    transform = new { implementation = "OpenCV two-dimensional complex DFT", width = source.Width, height = source.Height, inverseScale = true },
                    spectrum = new
                    {
                        magnitudeDefinition = "sqrt(re^2+im^2)/windowSum",
                        powerDefinition = "magnitude^2",
                        coordinateUnit = "cycles/pixel",
                        display = new { parameters.CenterSpectrum, scale = parameters.VisualizationScale.ToString(), format = AlgorithmImageFormat.Gray8.ToString() },
                        radialBinWidth = parameters.RadialBinWidthCyclesPerPixel,
                        directionBinWidthDegrees = parameters.DirectionBinWidthDegrees,
                    },
                    peakDetection = new
                    {
                        parameters.MinimumPeakFrequencyCyclesPerPixel,
                        parameters.MaximumPeakFrequencyCyclesPerPixel,
                        parameters.PeakRelativePowerThreshold,
                        parameters.PeakNeighborhoodRadius,
                        parameters.MaximumPeaks,
                        conjugatePairs = "one canonical half-plane representative",
                    },
                    inverseVerification = new { target = "mean-adjusted-windowed-luminance", rmse = inverseRmse, maximumAbsoluteError = inverseMaximumError },
                    dominantPeak = peaks.Length == 0 ? null : PeakData(peaks[0]),
                    parameterSchemaVersion = context.Invocation.ParameterSchemaVersion,
                })));
                context.Progress?.Report(new AlgorithmProgress(1, "frequency.complete"));

                List<AlgorithmDiagnosticMessage> messages = new();
                if (peaks.Length == 0)
                    messages.Add(new AlgorithmDiagnosticMessage("frequency_no_peak_above_threshold", "No non-DC local maximum passed the configured frequency and relative-power thresholds.", "warning"));
                if (parameters.WindowFunction != FrequencyWindowFunction.Rectangular)
                    messages.Add(new AlgorithmDiagnosticMessage("frequency_inverse_windowed_target", "Inverse error is measured against the windowed, mean-adjusted spatial signal rather than the unwindowed source."));
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = artifacts,
                    Diagnostics = new AlgorithmExecutionDiagnostics { Messages = messages },
                });
            }
            catch
            {
                magnitudeImage?.Dispose();
                powerImage?.Dispose();
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                throw;
            }
        }

        internal static bool TryMean(
            AlgorithmImageBuffer source,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress,
            out double mean,
            out int invalidX,
            out int invalidY)
        {
            double sum = 0;
            double compensation = 0;
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 31) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.03 + 0.09 * y / Math.Max(1, source.Height), "frequency.scan"));
                }
                for (int x = 0; x < source.Width; x++)
                {
                    double value = AlgorithmIntensitySampler.ReadLuminanceNominal(source, x, y);
                    if (!double.IsFinite(value))
                    {
                        mean = double.NaN;
                        invalidX = x;
                        invalidY = y;
                        return false;
                    }
                    double adjusted = value - compensation;
                    double next = sum + adjusted;
                    compensation = (next - sum) - adjusted;
                    sum = next;
                }
            }
            mean = sum / checked((long)source.Width * source.Height);
            invalidX = invalidY = -1;
            return true;
        }

        internal static bool TryValidateNormalizedFloat(
            AlgorithmImageBuffer source,
            CancellationToken cancellationToken,
            out int invalidX,
            out int invalidY,
            out int invalidChannel,
            out float invalidValue)
        {
            ReadOnlySpan<byte> data = source.Data.Span;
            int channels = source.Format.Channels() == 1 ? 1 : 3;
            int bytesPerPixel = source.Format.BytesPerPixel();
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < source.Width; x++)
                {
                    int pixel = y * source.Stride + x * bytesPerPixel;
                    for (int channel = 0; channel < channels; channel++)
                    {
                        float value = BinaryPrimitives.ReadSingleLittleEndian(data.Slice(pixel + channel * 4, 4));
                        if (!float.IsFinite(value) || value < 0 || value > 1)
                        {
                            invalidX = x;
                            invalidY = y;
                            invalidChannel = channel;
                            invalidValue = value;
                            return false;
                        }
                    }
                }
            }
            invalidX = invalidY = invalidChannel = -1;
            invalidValue = 0;
            return true;
        }

        internal static float[] CreateWindow(int length, FrequencyWindowFunction function)
        {
            float[] values = new float[length];
            if (length <= 2 || function == FrequencyWindowFunction.Rectangular)
            {
                Array.Fill(values, 1f);
                return values;
            }
            for (int index = 0; index < length; index++)
            {
                double phase = 2 * Math.PI * index / (length - 1);
                values[index] = (float)(function switch
                {
                    FrequencyWindowFunction.Hann => 0.5 - 0.5 * Math.Cos(phase),
                    FrequencyWindowFunction.Hamming => 0.54 - 0.46 * Math.Cos(phase),
                    FrequencyWindowFunction.Blackman => 0.42 - 0.5 * Math.Cos(phase) + 0.08 * Math.Cos(2 * phase),
                    _ => 1,
                });
            }
            return values;
        }

        internal static unsafe void FillSpatial(
            Mat spatial,
            AlgorithmImageBuffer source,
            float[] windowX,
            float[] windowY,
            double removedMean,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.13 + 0.15 * y / Math.Max(1, source.Height), "frequency.window"));
                }
                float* row = (float*)spatial.Ptr(y);
                double wy = windowY[y];
                for (int x = 0; x < source.Width; x++)
                    row[x] = (float)((AlgorithmIntensitySampler.ReadLuminanceNominal(source, x, y) - removedMean) * windowX[x] * wy);
            }
        }

        internal static unsafe SpectrumAggregation Aggregate(
            Mat spectrum,
            double windowSum,
            FrequencySpectrumParameters parameters,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            double maximumFrequency = Math.Sqrt(0.5);
            int radialCount = Math.Max(1, (int)Math.Ceiling(maximumFrequency / parameters.RadialBinWidthCyclesPerPixel));
            int directionalCount = Math.Max(1, (int)Math.Ceiling(180 / parameters.DirectionBinWidthDegrees));
            MutableBin[] radial = Enumerable.Range(0, radialCount).Select(_ => new MutableBin()).ToArray();
            MutableBin[] directional = Enumerable.Range(0, directionalCount).Select(_ => new MutableBin()).ToArray();
            double maximumMagnitude = 0;
            double maximumPower = 0;
            double maximumEligiblePeakPower = 0;
            double normalization = windowSum * windowSum;
            int rows = spectrum.Rows;
            int cols = spectrum.Cols;
            for (int y = 0; y < rows; y++)
            {
                if ((y & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.52 + 0.12 * y / Math.Max(1, rows), "frequency.aggregate"));
                }
                float* row = (float*)spectrum.Ptr(y);
                double fy = SignedBin(y, rows) / (double)rows;
                for (int x = 0; x < cols; x++)
                {
                    double fx = SignedBin(x, cols) / (double)cols;
                    double real = row[x * 2];
                    double imaginary = row[x * 2 + 1];
                    double power = (real * real + imaginary * imaginary) / normalization;
                    double magnitude = Math.Sqrt(power);
                    maximumMagnitude = Math.Max(maximumMagnitude, magnitude);
                    maximumPower = Math.Max(maximumPower, power);
                    double frequency = Math.Sqrt(fx * fx + fy * fy);
                    int radialIndex = Math.Min(radial.Length - 1, (int)(frequency / parameters.RadialBinWidthCyclesPerPixel));
                    radial[radialIndex].Add(magnitude, power);
                    if (frequency > 0)
                    {
                        double direction = NormalizeDirection(Math.Atan2(fy, fx) * 180 / Math.PI);
                        int directionIndex = Math.Min(directional.Length - 1, (int)(direction / parameters.DirectionBinWidthDegrees));
                        directional[directionIndex].Add(magnitude, power);
                    }
                    if (frequency >= parameters.MinimumPeakFrequencyCyclesPerPixel
                        && frequency <= parameters.MaximumPeakFrequencyCyclesPerPixel)
                    {
                        maximumEligiblePeakPower = Math.Max(maximumEligiblePeakPower, power);
                    }
                }
            }
            return new SpectrumAggregation(
                maximumMagnitude,
                maximumPower,
                maximumEligiblePeakPower,
                radial.Select((bin, index) => bin.Freeze(index * parameters.RadialBinWidthCyclesPerPixel,
                    Math.Min(maximumFrequency, (index + 1) * parameters.RadialBinWidthCyclesPerPixel))).ToArray(),
                directional.Select((bin, index) => bin.Freeze(index * parameters.DirectionBinWidthDegrees,
                    Math.Min(180, (index + 1) * parameters.DirectionBinWidthDegrees))).ToArray());
        }

        internal static unsafe (byte[] Magnitude, byte[] Power) CreateDisplays(
            Mat spectrum,
            double windowSum,
            double maximumMagnitude,
            double maximumPower,
            FrequencySpectrumParameters parameters,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int rows = spectrum.Rows;
            int cols = spectrum.Cols;
            byte[] magnitude = new byte[checked(rows * cols)];
            byte[] power = new byte[magnitude.Length];
            double normalization = windowSum * windowSum;
            for (int y = 0; y < rows; y++)
            {
                if ((y & 15) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.64 + 0.1 * y / Math.Max(1, rows), "frequency.visualize"));
                }
                float* row = (float*)spectrum.Ptr(y);
                int destinationY = parameters.CenterSpectrum ? (y + rows / 2) % rows : y;
                for (int x = 0; x < cols; x++)
                {
                    double real = row[x * 2];
                    double imaginary = row[x * 2 + 1];
                    double p = (real * real + imaginary * imaginary) / normalization;
                    double m = Math.Sqrt(p);
                    int destinationX = parameters.CenterSpectrum ? (x + cols / 2) % cols : x;
                    int destination = destinationY * cols + destinationX;
                    magnitude[destination] = DisplayValue(m, maximumMagnitude, parameters.VisualizationScale);
                    power[destination] = DisplayValue(p, maximumPower, parameters.VisualizationScale);
                }
            }
            return (magnitude, power);
        }

        private static byte DisplayValue(double value, double maximum, FrequencySpectrumVisualizationScale scale)
        {
            if (!(maximum > 0) || !(value > 0)) return 0;
            double normalized = scale == FrequencySpectrumVisualizationScale.Logarithmic
                ? Math.Log(1 + value) / Math.Log(1 + maximum)
                : value / maximum;
            return (byte)Math.Clamp((int)Math.Round(normalized * byte.MaxValue), 0, byte.MaxValue);
        }

        internal static unsafe SpectrumPeak[] DetectPeaks(
            Mat spectrum,
            double windowSum,
            double maximumEligiblePower,
            FrequencySpectrumParameters parameters,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            if (!(maximumEligiblePower > 0)) return [];
            double threshold = maximumEligiblePower * parameters.PeakRelativePowerThreshold;
            double normalization = windowSum * windowSum;
            List<SpectrumPeak> candidates = new();
            int rows = spectrum.Rows;
            int cols = spectrum.Cols;
            for (int y = 0; y < rows; y++)
            {
                if ((y & 7) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.74 + 0.1 * y / Math.Max(1, rows), "frequency.peaks"));
                }
                int sy = SignedBin(y, rows);
                double fy = sy / (double)rows;
                for (int x = 0; x < cols; x++)
                {
                    int sx = SignedBin(x, cols);
                    if (!IsCanonicalHalfPlane(sx, sy, cols, rows)) continue;
                    double fx = sx / (double)cols;
                    double frequency = Math.Sqrt(fx * fx + fy * fy);
                    if (frequency < parameters.MinimumPeakFrequencyCyclesPerPixel
                        || frequency > parameters.MaximumPeakFrequencyCyclesPerPixel) continue;
                    double power = PowerAt(spectrum, x, y, normalization);
                    if (power < threshold || !IsLocalMaximum(spectrum, x, y, power, normalization, parameters.PeakNeighborhoodRadius)) continue;
                    double frequencyDirection = NormalizeDirection(Math.Atan2(fy, fx) * 180 / Math.PI);
                    candidates.Add(new SpectrumPeak(
                        x, y,
                        parameters.CenterSpectrum ? (x + cols / 2) % cols : x,
                        parameters.CenterSpectrum ? (y + rows / 2) % rows : y,
                        fx, fy, frequency, 1 / frequency, frequencyDirection,
                        NormalizeDirection(frequencyDirection + 90), Math.Sqrt(power), power,
                        maximumEligiblePower > 0 ? power / maximumEligiblePower : 0));
                }
            }
            return candidates.OrderByDescending(value => value.Power)
                .ThenBy(value => value.RawY).ThenBy(value => value.RawX)
                .Take(parameters.MaximumPeaks).ToArray();
        }

        private static unsafe bool IsLocalMaximum(Mat spectrum, int x, int y, double value, double normalization, int radius)
        {
            int rows = spectrum.Rows;
            int cols = spectrum.Cols;
            int currentIndex = y * cols + x;
            double equalityTolerance = Math.Max(1, value) * 1e-12;
            for (int offsetY = -radius; offsetY <= radius; offsetY++)
            {
                for (int offsetX = -radius; offsetX <= radius; offsetX++)
                {
                    if (offsetX == 0 && offsetY == 0) continue;
                    int nx = Mod(x + offsetX, cols);
                    int ny = Mod(y + offsetY, rows);
                    double neighbor = PowerAt(spectrum, nx, ny, normalization);
                    if (neighbor > value + equalityTolerance) return false;
                    if (Math.Abs(neighbor - value) <= equalityTolerance && ny * cols + nx < currentIndex) return false;
                }
            }
            return true;
        }

        internal static unsafe double PowerAt(Mat spectrum, int x, int y, double normalization)
        {
            float* row = (float*)spectrum.Ptr(y);
            double real = row[x * 2];
            double imaginary = row[x * 2 + 1];
            return (real * real + imaginary * imaginary) / normalization;
        }

        private static unsafe (double Rmse, double MaximumError) InverseError(Mat expected, Mat actual, CancellationToken cancellationToken)
        {
            double squared = 0;
            double maximum = 0;
            int rows = expected.Rows;
            int cols = expected.Cols;
            long count = checked((long)rows * cols);
            for (int y = 0; y < rows; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                float* expectedRow = (float*)expected.Ptr(y);
                float* actualRow = (float*)actual.Ptr(y);
                for (int x = 0; x < cols; x++)
                {
                    double error = actualRow[x] - expectedRow[x];
                    squared += error * error;
                    maximum = Math.Max(maximum, Math.Abs(error));
                }
            }
            return (Math.Sqrt(squared / count), maximum);
        }

        private static AlgorithmMeasurementArtifact BuildMeasurements(
            AlgorithmImageBuffer source,
            double sourceMean,
            double windowSum,
            double windowEnergy,
            SpectrumAggregation aggregation,
            IReadOnlyList<SpectrumPeak> peaks,
            double inverseRmse,
            double inverseMaximumError)
        {
            List<AlgorithmMeasurement> values =
            [
                new("frequency.source_width", source.Width, "px"),
                new("frequency.source_height", source.Height, "px"),
                new("frequency.source_mean", sourceMean, "nominal-8bit-DN"),
                new("frequency.window_sum", windowSum),
                new("frequency.window_energy", windowEnergy),
                new("frequency.maximum_magnitude", aggregation.MaximumMagnitude, "nominal-8bit-DN"),
                new("frequency.maximum_power", aggregation.MaximumPower, "nominal-8bit-DN^2"),
                new("frequency.peak_count", peaks.Count, "peak"),
                new("frequency.inverse_rmse", inverseRmse, "nominal-8bit-DN"),
                new("frequency.inverse_maximum_error", inverseMaximumError, "nominal-8bit-DN"),
            ];
            if (peaks.Count > 0)
            {
                SpectrumPeak peak = peaks[0];
                values.Add(new("frequency.dominant.cycles_per_pixel", peak.Frequency, "cycles/pixel"));
                values.Add(new("frequency.dominant.period_pixels", peak.Period, "px"));
                values.Add(new("frequency.dominant.frequency_direction_degrees", peak.FrequencyDirection, "degree"));
                values.Add(new("frequency.dominant.spatial_direction_degrees", peak.SpatialDirection, "degree"));
                values.Add(new("frequency.dominant.power", peak.Power, "nominal-8bit-DN^2"));
            }
            return new AlgorithmMeasurementArtifact("frequency-spectrum-summary", values);
        }

        private static AlgorithmTableArtifact BuildRadialTable(IReadOnlyList<SpectrumBin> bins)
            => new("frequency-radial-spectrum",
            [
                new("LowerFrequency", "number", "cycles/pixel"), new("UpperFrequency", "number", "cycles/pixel"),
                new("CenterFrequency", "number", "cycles/pixel"), new("EquivalentPeriod", "number", "px"),
                new("SampleCount", "integer", "frequency-bin"), new("MeanMagnitude", "number", "nominal-8bit-DN"),
                new("MeanPower", "number", "nominal-8bit-DN^2"), new("MaximumPower", "number", "nominal-8bit-DN^2"),
            ], bins.Select(bin => Row(
                ("LowerFrequency", bin.Lower), ("UpperFrequency", bin.Upper), ("CenterFrequency", bin.Center),
                ("EquivalentPeriod", bin.Center > 0 ? 1 / bin.Center : null), ("SampleCount", bin.Count),
                ("MeanMagnitude", bin.MeanMagnitude), ("MeanPower", bin.MeanPower), ("MaximumPower", bin.MaximumPower))).ToArray());

        private static AlgorithmTableArtifact BuildDirectionalTable(IReadOnlyList<SpectrumBin> bins)
            => new("frequency-directional-spectrum",
            [
                new("LowerDirection", "number", "degree"), new("UpperDirection", "number", "degree"),
                new("CenterDirection", "number", "degree"), new("SpatialDirection", "number", "degree"),
                new("SampleCount", "integer", "frequency-bin"), new("MeanMagnitude", "number", "nominal-8bit-DN"),
                new("MeanPower", "number", "nominal-8bit-DN^2"), new("TotalPower", "number", "nominal-8bit-DN^2"),
                new("MaximumPower", "number", "nominal-8bit-DN^2"),
            ], bins.Select(bin => Row(
                ("LowerDirection", bin.Lower), ("UpperDirection", bin.Upper), ("CenterDirection", bin.Center),
                ("SpatialDirection", NormalizeDirection(bin.Center + 90)), ("SampleCount", bin.Count),
                ("MeanMagnitude", bin.MeanMagnitude), ("MeanPower", bin.MeanPower),
                ("TotalPower", bin.TotalPower), ("MaximumPower", bin.MaximumPower))).ToArray());

        private static AlgorithmTableArtifact BuildPeakTable(IReadOnlyList<SpectrumPeak> peaks)
            => new("frequency-peaks",
            [
                new("Rank", "integer"), new("RawX", "integer", "frequency-bin"), new("RawY", "integer", "frequency-bin"),
                new("DisplayX", "integer", "px"), new("DisplayY", "integer", "px"),
                new("FrequencyX", "number", "cycles/pixel"), new("FrequencyY", "number", "cycles/pixel"),
                new("Frequency", "number", "cycles/pixel"), new("Period", "number", "px"),
                new("FrequencyDirection", "number", "degree"), new("SpatialDirection", "number", "degree"),
                new("Magnitude", "number", "nominal-8bit-DN"), new("Power", "number", "nominal-8bit-DN^2"),
                new("RelativePower", "number", "ratio"),
            ], peaks.Select((peak, index) => Row(
                ("Rank", index + 1), ("RawX", peak.RawX), ("RawY", peak.RawY), ("DisplayX", peak.DisplayX), ("DisplayY", peak.DisplayY),
                ("FrequencyX", peak.FrequencyX), ("FrequencyY", peak.FrequencyY), ("Frequency", peak.Frequency), ("Period", peak.Period),
                ("FrequencyDirection", peak.FrequencyDirection), ("SpatialDirection", peak.SpatialDirection),
                ("Magnitude", peak.Magnitude), ("Power", peak.Power), ("RelativePower", peak.RelativePower))).ToArray());

        private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(value => value.Name, value => AlgorithmJson.ToElement(value.Value), StringComparer.Ordinal);

        private static object PeakData(SpectrumPeak peak) => new
        {
            peak.FrequencyX,
            peak.FrequencyY,
            cyclesPerPixel = peak.Frequency,
            periodPixels = peak.Period,
            frequencyDirectionDegrees = peak.FrequencyDirection,
            spatialDirectionDegrees = peak.SpatialDirection,
            peak.Magnitude,
            peak.Power,
            peak.RelativePower,
        };

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path = null)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        internal static int SignedBin(int index, int length) => index <= (length - 1) / 2 ? index : index - length;

        private static bool IsCanonicalHalfPlane(int signedX, int signedY, int width, int height)
        {
            bool yNyquist = height % 2 == 0 && signedY == -height / 2;
            if (signedY > 0) return true;
            if (signedY < 0 && !yNyquist) return false;
            if (signedX > 0) return true;
            if (signedX == 0) return yNyquist;
            return width % 2 == 0 && signedX == -width / 2;
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

        private static double NormalizeDirection(double degrees)
        {
            degrees %= 180;
            return degrees < 0 ? degrees + 180 : degrees;
        }

        private sealed class MutableBin
        {
            private double _magnitude;
            private double _power;
            private double _maximumPower;
            private long _count;

            public void Add(double magnitude, double power)
            {
                _magnitude += magnitude;
                _power += power;
                _maximumPower = Math.Max(_maximumPower, power);
                _count++;
            }

            public SpectrumBin Freeze(double lower, double upper)
                => new(lower, upper, _count, _count == 0 ? 0 : _magnitude / _count,
                    _count == 0 ? 0 : _power / _count, _power, _maximumPower);
        }

        internal sealed record SpectrumAggregation(
            double MaximumMagnitude,
            double MaximumPower,
            double MaximumEligiblePeakPower,
            IReadOnlyList<SpectrumBin> RadialBins,
            IReadOnlyList<SpectrumBin> DirectionalBins);

        internal sealed record SpectrumBin(
            double Lower,
            double Upper,
            long Count,
            double MeanMagnitude,
            double MeanPower,
            double TotalPower,
            double MaximumPower)
        {
            public double Center => (Lower + Upper) / 2;
        }

        internal sealed record SpectrumPeak(
            int RawX,
            int RawY,
            int DisplayX,
            int DisplayY,
            double FrequencyX,
            double FrequencyY,
            double Frequency,
            double Period,
            double FrequencyDirection,
            double SpatialDirection,
            double Magnitude,
            double Power,
            double RelativePower);
    }
}
