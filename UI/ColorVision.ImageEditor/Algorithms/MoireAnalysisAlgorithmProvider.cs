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
    /// <summary>Periodic moire evidence and optional symmetric Gaussian notch filter built on the M10 DFT contract.</summary>
    public sealed class MoireAnalysisAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        public const string ResultSchema = "colorvision.frequency.moire-analysis/v1";
        private const double RadialBinWidth = 0.005;
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.moire-analysis.cpu", "ColorVision Moire Analysis CPU", AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local, 146,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
            Formats, "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.MoireAnalysis, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.MoireAnalysis && inputs.Count == 1 && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_input_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            AlgorithmImageBuffer source = context.Inputs[0].Image;
            MoireAnalysisParameters parameters = (MoireAnalysisParameters)context.Parameters;
            long pixels = checked((long)source.Width * source.Height);
            if (pixels > parameters.MaximumPixels)
                return ValueTask.FromResult(Failure(context, "moire_pixel_limit_exceeded", $"Input pixel count {pixels} exceeds {parameters.MaximumPixels}.", nameof(parameters.MaximumPixels)));
            if (source.Format.IsFloatingPoint()
                && !FrequencySpectrumAlgorithmProvider.TryValidateNormalizedFloat(source, cancellationToken, out int rx, out int ry, out int rc, out float rv))
                return ValueTask.FromResult(Failure(context, "moire_float_out_of_nominal_range", $"Floating-point luminance channel {rc} at ({rx}, {ry}) is {rv:G9}; V1 requires [0,1].", "inputs.source"));
            if (!FrequencySpectrumAlgorithmProvider.TryMean(source, cancellationToken, context.Progress, out double mean, out int ix, out int iy))
                return ValueTask.FromResult(Failure(context, "moire_nonfinite_input", $"Non-finite luminance at ({ix}, {iy}).", "inputs.source"));

            FrequencySpectrumParameters spectrumParameters = new()
            {
                WindowFunction = parameters.WindowFunction,
                RemoveMean = parameters.RemoveMean,
                CenterSpectrum = true,
                VisualizationScale = FrequencySpectrumVisualizationScale.Logarithmic,
                RadialBinWidthCyclesPerPixel = RadialBinWidth,
                DirectionBinWidthDegrees = 2,
                MinimumPeakFrequencyCyclesPerPixel = parameters.MinimumFrequencyCyclesPerPixel,
                MaximumPeakFrequencyCyclesPerPixel = parameters.MaximumFrequencyCyclesPerPixel,
                PeakRelativePowerThreshold = parameters.RelativePowerThreshold,
                PeakNeighborhoodRadius = parameters.PeakNeighborhoodRadius,
                MaximumPeaks = Math.Min(10_000, Math.Max(parameters.MaximumSuggestions * 16, 64)),
                MaximumPixels = parameters.MaximumPixels,
            };

            AlgorithmImageBuffer? spectrumImage = null;
            AlgorithmImageBuffer? heatmapImage = null;
            AlgorithmImageBuffer? filteredImage = null;
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                context.Progress?.Report(new AlgorithmProgress(0.08, "moire.window"));
                float[] wx = FrequencySpectrumAlgorithmProvider.CreateWindow(source.Width, parameters.WindowFunction);
                float[] wy = FrequencySpectrumAlgorithmProvider.CreateWindow(source.Height, parameters.WindowFunction);
                double windowSum = wx.Sum(value => (double)value) * wy.Sum(value => (double)value);
                using Mat spatial = new(source.Height, source.Width, MatType.CV_32FC1, Scalar.All(0));
                FrequencySpectrumAlgorithmProvider.FillSpatial(spatial, source, wx, wy, parameters.RemoveMean ? mean : 0, cancellationToken, context.Progress);
                using Mat spectrum = new();
                context.Progress?.Report(new AlgorithmProgress(0.28, "moire.dft"));
                Cv2.Dft(spatial, spectrum, DftFlags.ComplexOutput);
                cancellationToken.ThrowIfCancellationRequested();
                FrequencySpectrumAlgorithmProvider.SpectrumAggregation aggregation = FrequencySpectrumAlgorithmProvider.Aggregate(
                    spectrum, windowSum, spectrumParameters, cancellationToken, context.Progress);
                FrequencySpectrumAlgorithmProvider.SpectrumPeak[] peaks = FrequencySpectrumAlgorithmProvider.DetectPeaks(
                    spectrum, windowSum, aggregation.MaximumEligiblePeakPower, spectrumParameters, cancellationToken, context.Progress);
                MoireCandidate[] candidates = SelectCandidates(peaks, aggregation, parameters).Take(parameters.MaximumSuggestions).ToArray();
                double totalPower = aggregation.RadialBins.Sum(value => value.TotalPower);
                double candidateFraction = totalPower > 0 ? Math.Clamp(2 * candidates.Sum(value => value.Peak.Power) / totalPower, 0, 1) : 0;
                double maximumProminence = candidates.Length == 0 ? 1 : candidates.Max(value => value.Prominence);
                double score = 100 * Math.Sqrt(candidateFraction * Math.Max(0, 1 - 1 / maximumProminence));

                (byte[] magnitude, _) = FrequencySpectrumAlgorithmProvider.CreateDisplays(
                    spectrum, windowSum, aggregation.MaximumMagnitude, aggregation.MaximumPower, spectrumParameters, cancellationToken, context.Progress);
                byte[] heatmap = BuildHeatmap(source.Width, source.Height, candidates, parameters, cancellationToken);
                spectrumImage = new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8, magnitude, 96, 96);
                heatmapImage = new AlgorithmImageBuffer(source.Width, source.Height, source.Width, AlgorithmImageFormat.Gray8, heatmap, 96, 96);
                artifacts.Add(new AlgorithmImageArtifact("moire-magnitude-spectrum", "spectrum-magnitude-display", spectrumImage,
                    new Dictionary<string, string> { ["valueSemantics"] = "log1p-normalized-display", ["centered"] = "true" }));
                spectrumImage = null;
                artifacts.Add(new AlgorithmImageArtifact("moire-frequency-heatmap", "moire-frequency-evidence-display", heatmapImage,
                    new Dictionary<string, string> { ["valueSemantics"] = "candidate-prominence-gaussian-display", ["coordinateSpace"] = "centered-frequency-grid" }));
                heatmapImage = null;

                double filteredRetainedPower = 1;
                if (parameters.EnableNotchFilter)
                {
                    context.Progress?.Report(new AlgorithmProgress(0.8, "moire.notch-filter"));
                    (filteredImage, filteredRetainedPower) = FilterLuminance(source, mean, candidates, parameters, cancellationToken);
                    artifacts.Add(new AlgorithmImageArtifact("moire-filtered-luminance", "filtered-luminance", filteredImage,
                        new Dictionary<string, string> { ["formatSemantics"] = "normalized-gray32float", ["meanRestored"] = "true" }));
                    filteredImage = null;
                }

                artifacts.Add(BuildMeasurements(score, candidateFraction, maximumProminence, candidates, filteredRetainedPower));
                artifacts.Add(BuildSuggestions(candidates, parameters));
                artifacts.Add(new AlgorithmStructuredDataArtifact("moire-analysis", ResultSchema, AlgorithmJson.ToElement(new
                {
                    score,
                    scoreDefinition = "100*sqrt(clamp(2*sum(candidatePower)/totalPower,0,1)*(1-1/maxProminence))",
                    classification = Classification(score),
                    input = new { source.Width, source.Height, format = source.Format.ToString(), luminance = "nominal-8bit-DN" },
                    detection = new
                    {
                        parameters.WindowFunction, parameters.RemoveMean, parameters.MinimumFrequencyCyclesPerPixel,
                        parameters.MaximumFrequencyCyclesPerPixel, parameters.RelativePowerThreshold,
                        parameters.MinimumProminenceRatio, parameters.PeakNeighborhoodRadius, parameters.MaximumSuggestions,
                        prominence = "peakPower/radial-bin-meanPower",
                        conjugatePolicy = "one canonical peak; suggestion/filter always applies symmetric pair",
                    },
                    filtering = new
                    {
                        parameters.EnableNotchFilter, parameters.NotchSigmaCyclesPerPixel, parameters.NotchAttenuation,
                        response = "product(1-attenuation*exp(-distance^2/(2*sigma^2))) over peak and conjugate",
                        output = parameters.EnableNotchFilter ? "normalized Gray32Float luminance with source mean restored" : null,
                        estimatedCandidatePowerRetention = filteredRetainedPower,
                    },
                    candidates = candidates.Select(value => CandidateData(value, parameters)).ToArray(),
                    parameterSchemaVersion = context.Invocation.ParameterSchemaVersion,
                })));
                context.Progress?.Report(new AlgorithmProgress(1, "moire.complete"));
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = artifacts,
                    Diagnostics = new AlgorithmExecutionDiagnostics
                    {
                        Messages = candidates.Length == 0
                            ? [new AlgorithmDiagnosticMessage("moire_no_periodic_candidate", "No peak met the relative-power and radial-prominence criteria.")]
                            : [new AlgorithmDiagnosticMessage("moire_score_is_evidence", "The score quantifies periodic spectral evidence; it is not a causal proof of display/camera moire.")],
                    },
                });
            }
            catch
            {
                spectrumImage?.Dispose();
                heatmapImage?.Dispose();
                filteredImage?.Dispose();
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                throw;
            }
        }

        private static IEnumerable<MoireCandidate> SelectCandidates(
            IEnumerable<FrequencySpectrumAlgorithmProvider.SpectrumPeak> peaks,
            FrequencySpectrumAlgorithmProvider.SpectrumAggregation aggregation,
            MoireAnalysisParameters parameters)
        {
            foreach (FrequencySpectrumAlgorithmProvider.SpectrumPeak peak in peaks)
            {
                int index = Math.Min(aggregation.RadialBins.Count - 1, (int)(peak.Frequency / RadialBinWidth));
                double background = aggregation.RadialBins[index].MeanPower;
                double prominence = background > 0 ? peak.Power / background : peak.Power > 0 ? double.PositiveInfinity : 0;
                if (prominence >= parameters.MinimumProminenceRatio)
                    yield return new MoireCandidate(peak, prominence, background);
            }
        }

        private static byte[] BuildHeatmap(int width, int height, IReadOnlyList<MoireCandidate> candidates, MoireAnalysisParameters parameters, CancellationToken cancellationToken)
        {
            byte[] heatmap = new byte[checked(width * height)];
            foreach (MoireCandidate candidate in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Draw(candidate.Peak.FrequencyX, candidate.Peak.FrequencyY, candidate);
                Draw(-candidate.Peak.FrequencyX, -candidate.Peak.FrequencyY, candidate);
            }
            return heatmap;

            void Draw(double fx, double fy, MoireCandidate candidate)
            {
                double sx = Math.Max(1, parameters.NotchSigmaCyclesPerPixel * width);
                double sy = Math.Max(1, parameters.NotchSigmaCyclesPerPixel * height);
                int centerX = Mod((int)Math.Round(fx * width) + width / 2, width);
                int centerY = Mod((int)Math.Round(fy * height) + height / 2, height);
                int radiusX = Math.Max(1, (int)Math.Ceiling(3 * sx));
                int radiusY = Math.Max(1, (int)Math.Ceiling(3 * sy));
                double strength = Math.Clamp((candidate.Prominence - parameters.MinimumProminenceRatio + 1) / Math.Max(1, candidate.Prominence), 0, 1);
                for (int oy = -radiusY; oy <= radiusY; oy++)
                {
                    int y = Mod(centerY + oy, height);
                    for (int ox = -radiusX; ox <= radiusX; ox++)
                    {
                        int x = Mod(centerX + ox, width);
                        double gaussian = Math.Exp(-0.5 * (ox * ox / (sx * sx) + oy * oy / (sy * sy)));
                        byte value = (byte)Math.Clamp((int)Math.Round(255 * strength * gaussian), 0, 255);
                        int offset = y * width + x;
                        if (value > heatmap[offset]) heatmap[offset] = value;
                    }
                }
            }
        }

        private static unsafe (AlgorithmImageBuffer Image, double RetainedPower) FilterLuminance(
            AlgorithmImageBuffer source,
            double mean,
            MoireCandidate[] candidates,
            MoireAnalysisParameters parameters,
            CancellationToken cancellationToken)
        {
            float[] onesX = FrequencySpectrumAlgorithmProvider.CreateWindow(source.Width, FrequencyWindowFunction.Rectangular);
            float[] onesY = FrequencySpectrumAlgorithmProvider.CreateWindow(source.Height, FrequencyWindowFunction.Rectangular);
            using Mat spatial = new(source.Height, source.Width, MatType.CV_32FC1, Scalar.All(0));
            FrequencySpectrumAlgorithmProvider.FillSpatial(spatial, source, onesX, onesY, mean, cancellationToken, null);
            using Mat spectrum = new();
            Cv2.Dft(spatial, spectrum, DftFlags.ComplexOutput);
            double retainedAtCandidates = 0;
            int retainedCount = 0;
            int rows = spectrum.Rows;
            int cols = spectrum.Cols;
            for (int y = 0; y < rows; y++)
            {
                if ((y & 15) == 0) cancellationToken.ThrowIfCancellationRequested();
                double fy = FrequencySpectrumAlgorithmProvider.SignedBin(y, rows) / (double)rows;
                float* row = (float*)spectrum.Ptr(y);
                for (int x = 0; x < cols; x++)
                {
                    double fx = FrequencySpectrumAlgorithmProvider.SignedBin(x, cols) / (double)cols;
                    double response = NotchResponse(fx, fy, candidates, parameters);
                    row[x * 2] *= (float)response;
                    row[x * 2 + 1] *= (float)response;
                }
            }
            foreach (MoireCandidate candidate in candidates)
            {
                double response = NotchResponse(candidate.Peak.FrequencyX, candidate.Peak.FrequencyY, candidates, parameters);
                retainedAtCandidates += response * response;
                retainedCount++;
            }
            using Mat reconstructed = new();
            Cv2.Idft(spectrum, reconstructed, DftFlags.RealOutput | DftFlags.Scale);
            byte[] data = new byte[checked(source.Width * source.Height * 4)];
            for (int y = 0; y < source.Height; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                float* row = (float*)reconstructed.Ptr(y);
                for (int x = 0; x < source.Width; x++)
                {
                    float normalized = (float)Math.Clamp((row[x] + mean) / AlgorithmIntensitySampler.NominalPeak, 0, 1);
                    BinaryPrimitives.WriteSingleLittleEndian(data.AsSpan((y * source.Width + x) * 4, 4), normalized);
                }
            }
            return (new AlgorithmImageBuffer(source.Width, source.Height, source.Width * 4, AlgorithmImageFormat.Gray32Float, data, source.DpiX, source.DpiY),
                retainedCount == 0 ? 1 : retainedAtCandidates / retainedCount);
        }

        private static double NotchResponse(double fx, double fy, MoireCandidate[] candidates, MoireAnalysisParameters parameters)
        {
            double sigmaSquared2 = 2 * parameters.NotchSigmaCyclesPerPixel * parameters.NotchSigmaCyclesPerPixel;
            double response = 1;
            for (int index = 0; index < candidates.Length; index++)
            {
                MoireCandidate candidate = candidates[index];
                response *= One(candidate.Peak.FrequencyX, candidate.Peak.FrequencyY);
                response *= One(-candidate.Peak.FrequencyX, -candidate.Peak.FrequencyY);
            }
            return response;

            double One(double cx, double cy)
            {
                double dx = WrappedFrequencyDistance(fx, cx);
                double dy = WrappedFrequencyDistance(fy, cy);
                return 1 - parameters.NotchAttenuation * Math.Exp(-(dx * dx + dy * dy) / sigmaSquared2);
            }
        }

        private static double WrappedFrequencyDistance(double left, double right)
        {
            double distance = Math.Abs(left - right);
            return Math.Min(distance, 1 - Math.Min(distance, 1));
        }

        private static AlgorithmMeasurementArtifact BuildMeasurements(double score, double fraction, double prominence, IReadOnlyList<MoireCandidate> candidates, double retained)
        {
            List<AlgorithmMeasurement> values =
            [
                new("moire.score", score, "score-0..100"), new("moire.candidate_count", candidates.Count, "candidate"),
                new("moire.candidate_power_fraction", fraction, "ratio"), new("moire.maximum_prominence", prominence, "ratio"),
                new("moire.filtered_candidate_power_retention", retained, "ratio"),
            ];
            if (candidates.Count > 0)
            {
                FrequencySpectrumAlgorithmProvider.SpectrumPeak peak = candidates[0].Peak;
                values.Add(new("moire.dominant.cycles_per_pixel", peak.Frequency, "cycles/pixel"));
                values.Add(new("moire.dominant.period_pixels", peak.Period, "px"));
                values.Add(new("moire.dominant.frequency_direction_degrees", peak.FrequencyDirection, "degree"));
                values.Add(new("moire.dominant.spatial_direction_degrees", peak.SpatialDirection, "degree"));
            }
            return new AlgorithmMeasurementArtifact("moire-analysis-summary", values);
        }

        private static AlgorithmTableArtifact BuildSuggestions(IReadOnlyList<MoireCandidate> candidates, MoireAnalysisParameters parameters)
            => new("moire-notch-suggestions",
            [
                new("Rank", "integer"), new("FrequencyX", "number", "cycles/pixel"), new("FrequencyY", "number", "cycles/pixel"),
                new("ConjugateX", "number", "cycles/pixel"), new("ConjugateY", "number", "cycles/pixel"),
                new("Frequency", "number", "cycles/pixel"), new("Period", "number", "px"),
                new("FrequencyDirection", "number", "degree"), new("SpatialDirection", "number", "degree"),
                new("Power", "number", "nominal-8bit-DN^2"), new("RadialBackgroundPower", "number", "nominal-8bit-DN^2"),
                new("Prominence", "number", "ratio"), new("SuggestedSigma", "number", "cycles/pixel"),
                new("SuggestedAttenuation", "number", "ratio"), new("Explanation", "string"),
            ], candidates.Select((candidate, index) => Row(
                ("Rank", index + 1), ("FrequencyX", candidate.Peak.FrequencyX), ("FrequencyY", candidate.Peak.FrequencyY),
                ("ConjugateX", -candidate.Peak.FrequencyX), ("ConjugateY", -candidate.Peak.FrequencyY),
                ("Frequency", candidate.Peak.Frequency), ("Period", candidate.Peak.Period),
                ("FrequencyDirection", candidate.Peak.FrequencyDirection), ("SpatialDirection", candidate.Peak.SpatialDirection),
                ("Power", candidate.Peak.Power), ("RadialBackgroundPower", candidate.RadialBackgroundPower),
                ("Prominence", candidate.Prominence), ("SuggestedSigma", parameters.NotchSigmaCyclesPerPixel),
                ("SuggestedAttenuation", parameters.NotchAttenuation),
                ("Explanation", "Narrow periodic peak above its same-radius spectral background; apply the symmetric conjugate notch together."))).ToArray());

        private static object CandidateData(MoireCandidate candidate, MoireAnalysisParameters parameters) => new
        {
            candidate.Peak.FrequencyX, candidate.Peak.FrequencyY, candidate.Peak.Frequency, candidate.Peak.Period,
            candidate.Peak.FrequencyDirection, candidate.Peak.SpatialDirection, candidate.Peak.Power,
            candidate.RadialBackgroundPower, candidate.Prominence,
            suggestedSigma = parameters.NotchSigmaCyclesPerPixel, suggestedAttenuation = parameters.NotchAttenuation,
        };

        private static Dictionary<string, JsonElement> Row(params (string Name, object Value)[] values)
            => values.ToDictionary(value => value.Name, value => AlgorithmJson.ToElement(value.Value), StringComparer.Ordinal);

        private static string Classification(double score) => score switch { < 20 => "low", < 50 => "moderate", < 75 => "high", _ => "very-high" };
        private static int Mod(int value, int modulus) { int result = value % modulus; return result < 0 ? result + modulus : result; }

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path = null) => new()
        {
            InvocationId = context.Invocation.InvocationId,
            AlgorithmId = context.Descriptor.Id,
            AlgorithmVersion = context.Descriptor.Version,
            Status = AlgorithmResultStatus.Failed,
            Failures = [new AlgorithmFailure(code, message, path)],
        };

        private sealed record MoireCandidate(FrequencySpectrumAlgorithmProvider.SpectrumPeak Peak, double Prominence, double RadialBackgroundPower);
    }
}
