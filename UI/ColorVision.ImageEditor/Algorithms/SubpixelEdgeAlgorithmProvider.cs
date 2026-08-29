using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Managed deterministic caliper provider with bilinear band sampling and parabolic peak refinement.</summary>
    public sealed class SubpixelEdgeAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.measurement.subpixel-edge/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.subpixel-edge.cpu",
            "ColorVision Subpixel Edge CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            112,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.SubpixelEdge, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.SubpixelEdge
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Invocation.Roi is not PolylineAlgorithmRoi path)
                return ValueTask.FromResult(Failure(context, "subpixel_edge_calipers_required", "Subpixel edge requires a polyline ROI whose consecutive points define calipers.", "roi"));

            AlgorithmImageBuffer image = context.Inputs[0].Image;
            SubpixelEdgeParameters parameters = (SubpixelEdgeParameters)context.Parameters;
            AlgorithmPoint[] points = path.Points
                .Select(point => AlgorithmCoordinates.ToPixel(point, path.CoordinateSpace, image.DpiX, image.DpiY))
                .ToArray();
            int caliperCount = points.Length - 1;
            if (caliperCount > parameters.MaximumCalipers)
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "subpixel_edge_caliper_limit_exceeded",
                    $"The ROI defines {caliperCount} calipers, exceeding MaximumCalipers={parameters.MaximumCalipers}.",
                    nameof(parameters.MaximumCalipers)));
            }

            List<AlgorithmTableColumn> columns =
            [
                new("CaliperIndex", "integer"), new("Accepted", "boolean"), new("RejectionReason", "string"),
                new("StartX", "number", "px"), new("StartY", "number", "px"),
                new("EndX", "number", "px"), new("EndY", "number", "px"),
                new("Length", "number", "px"), new("SampleCount", "integer"), new("ActualSpacing", "number", "px"),
                new("EdgeX", "number", "px"), new("EdgeY", "number", "px"),
                new("Distance", "number", "px"), new("Fraction", "number"),
                new("SignedGradient", "number", "nominal-8bit-DN/px"),
                new("GradientMagnitude", "number", "nominal-8bit-DN/px"), new("DetectedPolarity", "string"),
                new("Confidence", "number"), new("LocalizationUncertainty", "number", "px"),
                new("ClampedSampleCount", "integer"),
            ];
            List<IReadOnlyDictionary<string, JsonElement>> rows = new(caliperCount);
            List<AlgorithmGeometry> geometries = new(caliperCount * 2);
            List<AlgorithmOverlayItem> overlays = new(Math.Min(parameters.MaximumOverlayCalipers, caliperCount) * 2);
            List<EdgeDetection> accepted = new();
            int rejected = 0;
            int clamped = 0;
            long totalSamples = 0;

            context.Progress?.Report(new AlgorithmProgress(0.03, "subpixel-edge.sample", "Sampling calipers"));
            for (int index = 0; index < caliperCount; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                context.Progress?.Report(new AlgorithmProgress(0.03 + 0.86 * index / Math.Max(1, caliperCount), "subpixel-edge.sample"));
                AlgorithmPoint start = points[index];
                AlgorithmPoint end = points[index + 1];
                string caliperId = $"caliper-{index}";
                geometries.Add(new AlgorithmGeometry(caliperId, AlgorithmGeometryKind.Line, [start, end]));
                if (index < parameters.MaximumOverlayCalipers)
                    overlays.Add(new AlgorithmOverlayItem(caliperId, new AlgorithmOverlayStyle("#FF00B7FF", null, 1, $"C{index}")));

                CaliperResult result = AnalyzeCaliper(image, index, start, end, parameters, ref totalSamples, cancellationToken);
                rows.Add(result.Row);
                clamped += result.ClampedSampleCount;
                if (result.Detection is not EdgeDetection detection)
                {
                    rejected++;
                    continue;
                }
                accepted.Add(detection);
                string edgeId = $"edge-{index}";
                geometries.Add(new AlgorithmGeometry(
                    edgeId,
                    AlgorithmGeometryKind.Point,
                    [detection.Point],
                    Residual: detection.LocalizationUncertainty,
                    Confidence: detection.Confidence,
                    Measurements: new Dictionary<string, double>
                    {
                        ["signedGradient"] = detection.SignedGradient,
                        ["gradientMagnitude"] = Math.Abs(detection.SignedGradient),
                        ["distance"] = detection.Distance,
                        ["fraction"] = detection.Fraction,
                        ["localizationUncertainty"] = detection.LocalizationUncertainty,
                    }));
                if (index < parameters.MaximumOverlayCalipers)
                    overlays.Add(new AlgorithmOverlayItem(edgeId, new AlgorithmOverlayStyle("#FF36E36E", "#6636E36E", 1.5, $"E{index}")));
            }
            cancellationToken.ThrowIfCancellationRequested();
            context.Progress?.Report(new AlgorithmProgress(0.92, "subpixel-edge.artifacts", "Building edge artifacts"));

            List<AlgorithmMeasurement> measurements =
            [
                new("subpixel_edge.caliper_count", caliperCount, "caliper"),
                new("subpixel_edge.accepted_count", accepted.Count, "edge"),
                new("subpixel_edge.rejected_count", rejected, "caliper"),
                new("subpixel_edge.total_sample_count", totalSamples, "sample"),
                new("subpixel_edge.clamped_sample_count", clamped, "sample"),
            ];
            if (accepted.Count > 0)
            {
                measurements.Add(new AlgorithmMeasurement("subpixel_edge.mean_gradient_magnitude", accepted.Average(item => Math.Abs(item.SignedGradient)), "nominal-8bit-DN/px"));
                measurements.Add(new AlgorithmMeasurement("subpixel_edge.maximum_gradient_magnitude", accepted.Max(item => Math.Abs(item.SignedGradient)), "nominal-8bit-DN/px"));
                measurements.Add(new AlgorithmMeasurement("subpixel_edge.mean_confidence", accepted.Average(item => item.Confidence), "ratio"));
                measurements.Add(new AlgorithmMeasurement("subpixel_edge.mean_localization_uncertainty", accepted.Average(item => item.LocalizationUncertainty), "px"));
            }

            JsonElement provenance = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Format.ToString(), image.DpiX, image.DpiY },
                calipers = context.Invocation.Roi,
                parameters,
                coordinateRule = "top-left-origin; integer coordinates are pixel centers",
                segmentRule = "each consecutive polyline point pair is one independent directed caliper",
                intensityRule = "canonical BGR Rec.601 luminance; alpha ignored; 8/16/normalized-float mapped to nominal 0..255",
                samplingRule = "bilinear centerline sampling with optional integer-pixel normal-band averaging",
                smoothingRule = "finite one-dimensional Gaussian truncated at three sigma",
                responseRule = "central gradient in nominal-8bit-DN-per-pixel; polarity-filtered strongest peak",
                refinementRule = "three-response parabolic interpolation clamped to the neighboring sample interval",
                confidenceRule = "response/(response+off-peak RMS noise+minimum-gradient); quality score, not probability",
                residualRule = "localization uncertainty from sampling interval, response curvature and off-peak RMS; heuristic pixels, not calibrated metrology uncertainty",
            });
            List<AlgorithmDiagnosticMessage> diagnostics = new();
            if (rejected > 0) diagnostics.Add(new AlgorithmDiagnosticMessage("subpixel_edge_calipers_rejected", $"Rejected {rejected} of {caliperCount} calipers; inspect the table reasons.", "warning"));
            if (clamped > 0) diagnostics.Add(new AlgorithmDiagnosticMessage("subpixel_edge_samples_clamped", $"Clamped {clamped} band samples to image bounds.", "warning"));
            context.Progress?.Report(new AlgorithmProgress(1, "subpixel-edge.complete"));
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmMeasurementArtifact("subpixel-edge-summary", measurements),
                    new AlgorithmTableArtifact("subpixel-edges", columns, rows),
                    new AlgorithmGeometryArtifact("subpixel-edge-geometry", AlgorithmCoordinateSpace.Pixel, geometries),
                    new AlgorithmOverlayArtifact("subpixel-edge-overlay", AlgorithmOverlayLifetime.Transient, overlays),
                    new AlgorithmStructuredDataArtifact("subpixel-edge-provenance", ResultSchema, provenance),
                ],
                Diagnostics = new AlgorithmExecutionDiagnostics { Messages = diagnostics },
            });
        }

        private static CaliperResult AnalyzeCaliper(
            AlgorithmImageBuffer image,
            int index,
            AlgorithmPoint start,
            AlgorithmPoint end,
            SubpixelEdgeParameters parameters,
            ref long totalSamples,
            CancellationToken cancellationToken)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(length) || length <= 1e-9)
                return Rejected(index, start, end, length, 0, null, "degenerate_caliper", 0);

            int count;
            try { count = checked((int)Math.Ceiling(length / parameters.SampleSpacingPixels) + 1); }
            catch (OverflowException)
            {
                return Rejected(index, start, end, length, 0, null, "sample_count_overflow", 0);
            }
            if (count > parameters.MaximumSamplesPerCaliper)
                return Rejected(index, start, end, length, count, null, "sample_limit_exceeded", 0);
            if (totalSamples + count > parameters.MaximumTotalSamples)
                return Rejected(index, start, end, length, count, null, "total_sample_limit_exceeded", 0);
            totalSamples += count;
            double spacing = length / (count - 1);
            double directionX = dx / length;
            double directionY = dy / length;
            double normalX = -directionY;
            double normalY = directionX;
            bool clamp = parameters.BoundaryMode == SubpixelEdgeBoundaryMode.Clamp;
            double[] profile = new double[count];
            int clampedSamples = 0;
            for (int sample = 0; sample < count; sample++)
            {
                if ((sample & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                double distance = sample * spacing;
                double x = start.X + directionX * distance;
                double y = start.Y + directionY * distance;
                double sum = 0;
                int bandCount = 0;
                for (int offset = -parameters.NormalAveragingRadiusPixels; offset <= parameters.NormalAveragingRadiusPixels; offset++)
                {
                    if (!AlgorithmIntensitySampler.TrySampleLuminanceNominal(
                        image,
                        x + normalX * offset,
                        y + normalY * offset,
                        clamp,
                        out double value,
                        out bool wasClamped))
                    {
                        string reason = x + normalX * offset < 0 || x + normalX * offset > image.Width - 1
                            || y + normalY * offset < 0 || y + normalY * offset > image.Height - 1
                            ? "sample_out_of_bounds"
                            : "invalid_sample";
                        return Rejected(index, start, end, length, count, spacing, reason, clampedSamples);
                    }
                    if (wasClamped) clampedSamples++;
                    sum += value;
                    bandCount++;
                }
                profile[sample] = sum / bandCount;
            }

            int[] smoothingRadii = GaussianBoxRadii(parameters.SmoothingSigmaPixels / spacing);
            int smoothingRadius = smoothingRadii.Sum();
            if (count < 2 * smoothingRadius + 7)
                return Rejected(index, start, end, length, count, spacing, "insufficient_samples", clampedSamples);
            double[] smoothed = Smooth(profile, smoothingRadii, cancellationToken);
            double[] signedResponse = Enumerable.Repeat(double.NaN, count).ToArray();
            int responseFirst = smoothingRadius + 1;
            int responseLast = count - smoothingRadius - 2;
            for (int sample = responseFirst; sample <= responseLast; sample++)
                signedResponse[sample] = (smoothed[sample + 1] - smoothed[sample - 1]) / (2 * spacing);

            int candidateFirst = responseFirst + 1;
            int candidateLast = responseLast - 1;
            int bestIndex = -1;
            double bestScore = double.NegativeInfinity;
            for (int sample = candidateFirst; sample <= candidateLast; sample++)
            {
                double score = Score(signedResponse[sample], parameters.Polarity);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestIndex = sample;
                }
            }
            if (bestIndex < 0 || !double.IsFinite(bestScore) || bestScore <= Math.Max(parameters.MinimumGradient, 1e-12))
                return Rejected(index, start, end, length, count, spacing, "gradient_below_minimum", clampedSamples);

            double left = Score(signedResponse[bestIndex - 1], parameters.Polarity);
            double center = bestScore;
            double right = Score(signedResponse[bestIndex + 1], parameters.Polarity);
            double denominator = left - 2 * center + right;
            double offsetSamples = denominator < -1e-12 ? 0.5 * (left - right) / denominator : 0;
            offsetSamples = Math.Clamp(offsetSamples, -1, 1);
            double refinedSigned = Quadratic(signedResponse[bestIndex - 1], signedResponse[bestIndex], signedResponse[bestIndex + 1], offsetSamples);
            double refinedScore = Math.Max(0, Score(refinedSigned, parameters.Polarity));
            double distancePixels = Math.Clamp((bestIndex + offsetSamples) * spacing, 0, length);
            double fraction = distancePixels / length;
            AlgorithmPoint point = new(start.X + directionX * distancePixels, start.Y + directionY * distancePixels);
            double noise = OffPeakRms(signedResponse, responseFirst, responseLast, bestIndex, Math.Max(2, smoothingRadius));
            double confidence = refinedScore / Math.Max(refinedScore + noise + parameters.MinimumGradient, 1e-12);
            double curvature = Math.Max(0, 2 * center - left - right) / (spacing * spacing);
            double uncertainty = Math.Clamp(
                spacing * (noise + parameters.MinimumGradient) / Math.Max(curvature * spacing, 1e-12),
                spacing / 20,
                length);
            EdgeDetection detection = new(
                point,
                distancePixels,
                fraction,
                refinedSigned,
                refinedSigned >= 0 ? "Rising" : "Falling",
                confidence,
                uncertainty);
            return Accepted(index, start, end, length, count, spacing, clampedSamples, detection);
        }

        private static int[] GaussianBoxRadii(double sigmaSamples)
        {
            if (sigmaSamples <= 0) return [];
            const int passCount = 3;
            double idealWidth = Math.Sqrt(12 * sigmaSamples * sigmaSamples / passCount + 1);
            int lowerWidth = Math.Max(1, (int)Math.Floor(idealWidth));
            if ((lowerWidth & 1) == 0) lowerWidth--;
            int upperWidth = lowerWidth + 2;
            double numerator = 12 * sigmaSamples * sigmaSamples
                - passCount * lowerWidth * lowerWidth
                - 4 * passCount * lowerWidth
                - 3 * passCount;
            int lowerCount = Math.Clamp((int)Math.Round(numerator / (-4 * lowerWidth - 4)), 0, passCount);
            return Enumerable.Range(0, passCount)
                .Select(index => (index < lowerCount ? lowerWidth : upperWidth) / 2)
                .ToArray();
        }

        private static double[] Smooth(double[] input, int[] radii, CancellationToken cancellationToken)
        {
            if (radii.Length == 0 || radii.All(radius => radius == 0)) return input;
            double[] first = new double[input.Length];
            double[] second = new double[input.Length];
            double[] source = input;
            double[] target = first;
            int validStart = 0;
            int validEnd = input.Length - 1;
            for (int pass = 0; pass < radii.Length; pass++)
            {
                int radius = radii[pass];
                if (radius == 0) continue;
                Array.Fill(target, double.NaN);
                int outputStart = validStart + radius;
                int outputEnd = validEnd - radius;
                int width = radius * 2 + 1;
                double sum = 0;
                for (int index = outputStart - radius; index <= outputStart + radius; index++) sum += source[index];
                for (int index = outputStart; index <= outputEnd; index++)
                {
                    if ((index & 4095) == 0) cancellationToken.ThrowIfCancellationRequested();
                    if (index > outputStart)
                        sum += source[index + radius] - source[index - radius - 1];
                    target[index] = sum / width;
                }
                validStart = outputStart;
                validEnd = outputEnd;
                source = target;
                target = ReferenceEquals(target, first) ? second : first;
            }
            return source;
        }

        private static double Score(double signed, SubpixelEdgePolarity polarity) => polarity switch
        {
            SubpixelEdgePolarity.Rising => signed,
            SubpixelEdgePolarity.Falling => -signed,
            _ => Math.Abs(signed),
        };

        private static double Quadratic(double left, double center, double right, double x)
            => center + 0.5 * (right - left) * x + 0.5 * (left - 2 * center + right) * x * x;

        private static double OffPeakRms(double[] response, int first, int last, int peak, int exclusionRadius)
        {
            double sumSquares = 0;
            int count = 0;
            for (int index = first; index <= last; index++)
            {
                if (Math.Abs(index - peak) <= exclusionRadius || !double.IsFinite(response[index])) continue;
                sumSquares += response[index] * response[index];
                count++;
            }
            return count == 0 ? 0 : Math.Sqrt(sumSquares / count);
        }

        private static CaliperResult Accepted(
            int index,
            AlgorithmPoint start,
            AlgorithmPoint end,
            double length,
            int count,
            double spacing,
            int clampedSamples,
            EdgeDetection detection)
            => new(Row(
                ("CaliperIndex", index), ("Accepted", true), ("RejectionReason", null),
                ("StartX", start.X), ("StartY", start.Y), ("EndX", end.X), ("EndY", end.Y),
                ("Length", length), ("SampleCount", count), ("ActualSpacing", spacing),
                ("EdgeX", detection.Point.X), ("EdgeY", detection.Point.Y),
                ("Distance", detection.Distance), ("Fraction", detection.Fraction),
                ("SignedGradient", detection.SignedGradient), ("GradientMagnitude", Math.Abs(detection.SignedGradient)),
                ("DetectedPolarity", detection.Polarity), ("Confidence", detection.Confidence),
                ("LocalizationUncertainty", detection.LocalizationUncertainty), ("ClampedSampleCount", clampedSamples)),
                detection,
                clampedSamples);

        private static CaliperResult Rejected(
            int index,
            AlgorithmPoint start,
            AlgorithmPoint end,
            double length,
            int count,
            double? spacing,
            string reason,
            int clampedSamples)
            => new(Row(
                ("CaliperIndex", index), ("Accepted", false), ("RejectionReason", reason),
                ("StartX", start.X), ("StartY", start.Y), ("EndX", end.X), ("EndY", end.Y),
                ("Length", double.IsFinite(length) ? length : null), ("SampleCount", count), ("ActualSpacing", spacing),
                ("EdgeX", null), ("EdgeY", null), ("Distance", null), ("Fraction", null),
                ("SignedGradient", null), ("GradientMagnitude", null), ("DetectedPolarity", null),
                ("Confidence", null), ("LocalizationUncertainty", null), ("ClampedSampleCount", clampedSamples)),
                null,
                clampedSamples);

        private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(value => value.Name, value => AlgorithmJson.ToElement(value.Value), StringComparer.Ordinal);

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        private sealed record CaliperResult(
            IReadOnlyDictionary<string, JsonElement> Row,
            EdgeDetection? Detection,
            int ClampedSampleCount);

        private sealed record EdgeDetection(
            AlgorithmPoint Point,
            double Distance,
            double Fraction,
            double SignedGradient,
            string Polarity,
            double Confidence,
            double LocalizationUncertainty);
    }
}
