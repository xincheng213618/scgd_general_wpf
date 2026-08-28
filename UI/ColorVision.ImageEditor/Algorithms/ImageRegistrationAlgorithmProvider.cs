using ColorVision.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic local translation or feature-homography registration.</summary>
    public sealed class ImageRegistrationAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.geometry.registration/v1";
        private const double MinimumPhaseStandardDeviation = 1e-6;
        private const double MinimumPhasePeakUniqueness = 0.05;
        private const int MaximumPhaseDiagnosticPixels = 512 * 512;
        private const int MaximumOrbFeatureBudget = 5_000;
        private const long MaximumDescriptorComparisonBudget = 50_000_000;
        private const long MaximumConsensusWorkBudget = 2_000_000;
        private const int DescriptorMatchBatchSize = 256;
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.image-registration.cpu",
            "ColorVision Image Registration CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            126,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Flow | AlgorithmHostCapabilities.Headless
                | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic | AlgorithmHostCapabilities.MultiInput,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.ImageRegistration, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.ImageRegistration
                && inputs.Count == 2
                && inputs.All(input => Formats.Contains(input.Image.Format));
            reason = supported ? null : "algorithm_input_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmInput[] references = context.Inputs.Where(input => string.Equals(input.Name, "reference", StringComparison.OrdinalIgnoreCase)).ToArray();
            AlgorithmInput[] movingInputs = context.Inputs.Where(input => string.Equals(input.Name, "moving", StringComparison.OrdinalIgnoreCase)).ToArray();
            if (references.Length != 1 || movingInputs.Length != 1)
                return ValueTask.FromResult(Failure(context, "invalid_input_names", "Exactly one 'reference' and one 'moving' input are required.", "inputs"));
            AlgorithmInput referenceInput = references[0];
            AlgorithmInput movingInput = movingInputs[0];
            AlgorithmImageBuffer reference = referenceInput.Image;
            AlgorithmImageBuffer moving = movingInput.Image;
            if (reference.Format != moving.Format)
                return ValueTask.FromResult(Failure(context, "format_mismatch", "Reference and moving formats must match; implicit conversion is forbidden.", "inputs"));
            if (string.IsNullOrWhiteSpace(referenceInput.ColorSpace) || string.IsNullOrWhiteSpace(movingInput.ColorSpace))
                return ValueTask.FromResult(Failure(context, "color_space_unspecified", "Both inputs require an explicit encoded color-space label.", "inputs"));
            if (!string.Equals(referenceInput.ColorSpace, movingInput.ColorSpace, StringComparison.OrdinalIgnoreCase))
                return ValueTask.FromResult(Failure(context, "color_space_mismatch", "Reference and moving color-space labels must match.", "inputs"));
            if (context.Invocation.Roi != null)
                return ValueTask.FromResult(Failure(context, "registration_roi_unsupported", "Registration V1 is full-image only because one ROI cannot unambiguously address two input coordinate systems.", "roi"));

            ImageRegistrationParameters parameters = (ImageRegistrationParameters)context.Parameters;
            if (parameters.Method == ImageRegistrationMethod.OrbHomography
                && !IsOrbWorkWithinBudget(parameters, out string budgetReason))
            {
                return ValueTask.FromResult(Failure(context, "registration_work_budget_exceeded", budgetReason, "parameters"));
            }
            if (parameters.Method == ImageRegistrationMethod.PhaseCorrelation
                && (reference.Width != moving.Width || reference.Height != moving.Height))
            {
                return ValueTask.FromResult(Failure(context, "phase_dimension_mismatch", "Phase correlation requires equal input dimensions.", "inputs"));
            }

            context.Progress?.Report(new AlgorithmProgress(0.03, "registration.prepare"));
            using Mat referenceGray = ToNormalizedGray(reference);
            using Mat movingGray = ToNormalizedGray(moving);
            if (!Cv2.CheckRange(referenceGray, quiet: true) || !Cv2.CheckRange(movingGray, quiet: true))
                return ValueTask.FromResult(Failure(context, "registration_nonfinite_input", "Registration inputs contain NaN or infinity.", "inputs"));

            RegistrationEstimate estimate = parameters.Method == ImageRegistrationMethod.PhaseCorrelation
                ? EstimatePhase(referenceGray, movingGray, parameters, context, cancellationToken)
                : EstimateFeatures(referenceGray, movingGray, parameters, context, cancellationToken);
            if (estimate.Failure != null)
                return ValueTask.FromResult(Failure(context, estimate.Failure.Code, estimate.Failure.Message, estimate.Failure.Path));

            double[] matrix = estimate.Matrix!;
            if (!GeometricTransformAlgorithmProvider.TryInvert(matrix, out double[] inverse, out double determinant))
                return ValueTask.FromResult(Failure(context, "registration_transform_singular", "The estimated moving-to-reference matrix is singular.", "result.matrix"));
            double condition = GeometricTransformAlgorithmProvider.ConditionNumber(matrix, inverse);
            if (!double.IsFinite(condition) || condition > parameters.MaximumConditionNumber)
                return ValueTask.FromResult(Failure(context, "registration_transform_ill_conditioned", "The estimated matrix exceeds the configured condition-number limit.", nameof(parameters.MaximumConditionNumber)));
            if (GeometricTransformAlgorithmProvider.CrossesProjectiveHorizon(matrix, moving.Width, moving.Height))
                return ValueTask.FromResult(Failure(context, "registration_transform_crosses_horizon", "The estimated transform crosses projective infinity inside the moving image.", "result.matrix"));

            AlgorithmImageBuffer? registered = null;
            AlgorithmImageBuffer? validityMask = null;
            List<AlgorithmArtifact> artifacts = new();
            try
            {
                context.Progress?.Report(new AlgorithmProgress(0.74, "registration.warp"));
                using AlgorithmImageMatLease movingLease = AlgorithmImageInterop.BorrowReadOnly(moving);
                using Mat output = new();
                using Mat transform = GeometricTransformAlgorithmProvider.CreateMatrix(matrix, GeometricTransformKind.Perspective);
                Cv2.WarpPerspective(
                    movingLease.Mat,
                    output,
                    transform,
                    new Size(reference.Width, reference.Height),
                    parameters.Interpolation == GeometricTransformInterpolation.Nearest ? InterpolationFlags.Nearest : InterpolationFlags.Linear,
                    parameters.Border == GeometricTransformBorder.Replicate ? BorderTypes.Replicate : BorderTypes.Constant,
                    BorderScalar(moving.Format, parameters));
                cancellationToken.ThrowIfCancellationRequested();
                (byte[] mask, long validPixels) = GeometricTransformAlgorithmProvider.BuildValidityMask(
                    reference.Width, reference.Height, moving.Width, moving.Height, inverse, cancellationToken, context.Progress);
                registered = AlgorithmImageInterop.FromMat(output, reference.DpiX, reference.DpiY);
                validityMask = new AlgorithmImageBuffer(reference.Width, reference.Height, reference.Width, AlgorithmImageFormat.Gray8, mask, reference.DpiX, reference.DpiY);
                double photometricRmse = PhotometricRmse(referenceGray, output, moving.Format, mask, validPixels);
                double inverseResidual = GeometricTransformAlgorithmProvider.InverseResidual(matrix, inverse);
                long outputPixels = checked((long)reference.Width * reference.Height);

                artifacts.Add(new AlgorithmImageArtifact("registered-image", "primary", registered,
                    new Dictionary<string, string>
                    {
                        ["matrixSemantics"] = "moving-pixel-center-to-reference-pixel-center",
                        ["referenceRevision"] = referenceInput.SourceRevision ?? string.Empty,
                        ["movingUri"] = movingInput.SourceUri ?? string.Empty,
                    }));
                registered = null;
                artifacts.Add(new AlgorithmImageArtifact("valid-region-mask", "validity-mask", validityMask,
                    new Dictionary<string, string> { ["valid"] = "255", ["invalid"] = "0" }));
                validityMask = null;
                artifacts.Add(BuildMeasurements(reference, moving, estimate, validPixels, outputPixels, photometricRmse, determinant, condition, inverseResidual));
                artifacts.Add(BuildMatrixTable(matrix, inverse));
                artifacts.Add(BuildMatchTable(estimate.Matches, parameters.MaximumReportedMatches));
                AlgorithmPoint[] footprint = GeometricTransformAlgorithmProvider.SourceFootprint(matrix, moving.Width, moving.Height);
                List<AlgorithmGeometry> geometries =
                [
                    new("moving-to-reference", AlgorithmGeometryKind.Transform, [], Matrix: matrix,
                        Residual: parameters.Method == ImageRegistrationMethod.OrbHomography ? estimate.GeometricRmse : null,
                        Confidence: estimate.Confidence,
                        Measurements: new Dictionary<string, double>
                        {
                            ["conditionNumber"] = condition,
                            ["inverseResidual"] = inverseResidual,
                        }),
                    new("registered-moving-footprint", AlgorithmGeometryKind.Polygon, footprint),
                ];
                foreach (RegistrationMatch match in estimate.Matches.Where(match => match.Inlier).Take(parameters.MaximumReportedMatches))
                    geometries.Add(new AlgorithmGeometry($"registration-inlier-{match.Index}", AlgorithmGeometryKind.Point, [new AlgorithmPoint(match.Reference.X, match.Reference.Y)], Residual: match.ResidualPixels, Confidence: estimate.Confidence));
                artifacts.Add(new AlgorithmGeometryArtifact("image-registration", AlgorithmCoordinateSpace.Pixel, geometries));
                artifacts.Add(new AlgorithmOverlayArtifact("image-registration-overlay", AlgorithmOverlayLifetime.Transient,
                [
                    new("registered-moving-footprint", new AlgorithmOverlayStyle("#FFFFA500", StrokeWidth: 1.5, Label: "registered footprint")),
                    .. geometries.Where(geometry => geometry.Kind == AlgorithmGeometryKind.Point)
                        .Select(geometry => new AlgorithmOverlayItem(geometry.Id, new AlgorithmOverlayStyle("#FF00FF66", StrokeWidth: 1))),
                ]));
                artifacts.Add(new AlgorithmStructuredDataArtifact("image-registration", ResultSchema, AlgorithmJson.ToElement(new
                {
                    method = parameters.Method.ToString(),
                    matrixSemantics = "moving-pixel-center-to-reference-pixel-center",
                    reference = new { reference.Width, reference.Height, format = reference.Format.ToString(), colorSpace = referenceInput.ColorSpace, referenceInput.SourceRevision },
                    moving = new { moving.Width, moving.Height, format = moving.Format.ToString(), colorSpace = movingInput.ColorSpace, movingInput.SourceUri },
                    matrix,
                    inverseMatrix = inverse,
                    determinant,
                    conditionNumber = condition,
                    inverseResidual,
                    estimate.PhaseShiftX,
                    estimate.PhaseShiftY,
                    estimate.PhaseResponse,
                    phasePeakUniqueness = parameters.Method == ImageRegistrationMethod.PhaseCorrelation ? (double?)estimate.PhasePeakUniqueness : null,
                    correlationLoss = parameters.Method == ImageRegistrationMethod.PhaseCorrelation ? (double?)estimate.CorrelationLoss : null,
                    estimate.ReferenceFeatureCount,
                    estimate.MovingFeatureCount,
                    matchCount = estimate.Matches.Count,
                    inlierCount = estimate.Matches.Count(match => match.Inlier),
                    geometricRmse = parameters.Method == ImageRegistrationMethod.OrbHomography ? (double?)estimate.GeometricRmse : null,
                    photometricRmse,
                    estimate.Confidence,
                    confidenceSemantics = "bounded deterministic quality heuristic; not a calibrated probability",
                    output = new { reference.Width, reference.Height, validPixels, invalidPixels = outputPixels - validPixels, validFraction = validPixels / (double)outputPixels },
                    presetId = context.Invocation.PresetId,
                    parameterSchemaVersion = context.Invocation.ParameterSchemaVersion,
                })));
                context.Progress?.Report(new AlgorithmProgress(1, "registration.complete"));
                return ValueTask.FromResult(new AlgorithmResult
                {
                    InvocationId = context.Invocation.InvocationId,
                    AlgorithmId = context.Descriptor.Id,
                    AlgorithmVersion = context.Descriptor.Version,
                    Status = AlgorithmResultStatus.Succeeded,
                    Artifacts = artifacts,
                    Diagnostics = new AlgorithmExecutionDiagnostics
                    {
                        Messages = validPixels == outputPixels
                            ? []
                            : [new AlgorithmDiagnosticMessage("registration_contains_invalid_output", $"{outputPixels - validPixels} output pixel centers do not map into the moving image.", "warning")],
                    },
                });
            }
            catch
            {
                registered?.Dispose();
                validityMask?.Dispose();
                foreach (IDisposable disposable in artifacts.OfType<IDisposable>()) disposable.Dispose();
                throw;
            }
        }

        private static RegistrationEstimate EstimatePhase(
            Mat reference,
            Mat moving,
            ImageRegistrationParameters parameters,
            AlgorithmExecutionContext context,
            CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            context.Progress?.Report(new AlgorithmProgress(0.12, "registration.phase-correlate"));
            Cv2.MeanStdDev(reference, out _, out Scalar referenceStdDev);
            Cv2.MeanStdDev(moving, out _, out Scalar movingStdDev);
            if (referenceStdDev.Val0 < MinimumPhaseStandardDeviation || movingStdDev.Val0 < MinimumPhaseStandardDeviation)
                return RegistrationEstimate.Fail("phase_insufficient_texture", "Phase correlation requires finite, non-constant texture in both inputs.", "inputs");

            PhasePeakQuality peakQuality = MeasurePhasePeakQuality(reference, moving, parameters.UseHannWindow, cancellationToken);
            if (!double.IsFinite(peakQuality.Uniqueness) || peakQuality.Uniqueness < MinimumPhasePeakUniqueness)
                return RegistrationEstimate.Fail(
                    "phase_ambiguous_texture",
                    $"The phase-correlation peak is not unique enough for a reliable shift (uniqueness={peakQuality.Uniqueness.ToString("G8", CultureInfo.InvariantCulture)}).",
                    "inputs");
            if (Cv2.Norm(reference, moving, NormTypes.INF) == 0)
            {
                return new RegistrationEstimate(
                    [1, 0, 0, 0, 1, 0, 0, 0, 1],
                    Math.Clamp(peakQuality.Uniqueness, 0, 1),
                    double.NaN,
                    0,
                    0,
                    0,
                    1,
                    peakQuality.Uniqueness,
                    0,
                    0,
                    []);
            }
            using Mat window = new();
            if (parameters.UseHannWindow) Cv2.CreateHanningWindow(window, reference.Size(), MatType.CV_32FC1);
            Point2d shift = Cv2.PhaseCorrelate(reference, moving, window, out double response);
            cancellationToken.ThrowIfCancellationRequested();
            if (!double.IsFinite(shift.X) || !double.IsFinite(shift.Y) || !double.IsFinite(response))
                return RegistrationEstimate.Fail("phase_estimate_nonfinite", "Phase correlation produced a non-finite estimate.", "inputs");
            if (response < parameters.MinimumPhaseResponse)
                return RegistrationEstimate.Fail("phase_response_below_threshold", $"Phase response {response.ToString("G8", CultureInfo.InvariantCulture)} is below the configured minimum.", nameof(parameters.MinimumPhaseResponse));
            double magnitude = Math.Sqrt(shift.X * shift.X + shift.Y * shift.Y);
            if (magnitude > parameters.MaximumTranslationPixels)
                return RegistrationEstimate.Fail("phase_translation_limit_exceeded", "The estimated translation exceeds the configured maximum.", nameof(parameters.MaximumTranslationPixels));
            double[] matrix = [1, 0, -shift.X, 0, 1, -shift.Y, 0, 0, 1];
            double boundedResponse = Math.Clamp(response, 0, 1);
            double confidence = Math.Sqrt(boundedResponse * Math.Clamp(peakQuality.Uniqueness, 0, 1));
            return new RegistrationEstimate(matrix, confidence, double.NaN, 1 - boundedResponse, shift.X, shift.Y, response, peakQuality.Uniqueness, 0, 0, []);
        }

        private static PhasePeakQuality MeasurePhasePeakQuality(Mat reference, Mat moving, bool useHannWindow, CancellationToken cancellationToken)
        {
            int sourcePixels = checked(reference.Rows * reference.Cols);
            double scale = sourcePixels <= MaximumPhaseDiagnosticPixels
                ? 1
                : Math.Sqrt(MaximumPhaseDiagnosticPixels / (double)sourcePixels);
            int width = Math.Max(2, (int)Math.Floor(reference.Cols * scale));
            int height = Math.Max(2, (int)Math.Floor(reference.Rows * scale));
            using Mat referenceWork = new();
            using Mat movingWork = new();
            if (width == reference.Cols && height == reference.Rows)
            {
                reference.CopyTo(referenceWork);
                moving.CopyTo(movingWork);
            }
            else
            {
                Cv2.Resize(reference, referenceWork, new Size(width, height), interpolation: InterpolationFlags.Nearest);
                Cv2.Resize(moving, movingWork, new Size(width, height), interpolation: InterpolationFlags.Nearest);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (useHannWindow)
            {
                using Mat window = new();
                Cv2.CreateHanningWindow(window, referenceWork.Size(), MatType.CV_32FC1);
                Cv2.Multiply(referenceWork, window, referenceWork);
                Cv2.Multiply(movingWork, window, movingWork);
            }

            using Mat referenceSpectrum = new();
            using Mat movingSpectrum = new();
            Cv2.Dft(referenceWork, referenceSpectrum, DftFlags.ComplexOutput);
            Cv2.Dft(movingWork, movingSpectrum, DftFlags.ComplexOutput);
            cancellationToken.ThrowIfCancellationRequested();
            using Mat crossPower = new();
            Cv2.MulSpectrums(referenceSpectrum, movingSpectrum, crossPower, DftFlags.None, conjB: true);
            Mat[] planes = Cv2.Split(crossPower);
            try
            {
                using Mat magnitude = new();
                Cv2.Magnitude(planes[0], planes[1], magnitude);
                Cv2.Add(magnitude, Scalar.All(1e-12), magnitude);
                Cv2.Divide(planes[0], magnitude, planes[0]);
                Cv2.Divide(planes[1], magnitude, planes[1]);
                Cv2.Merge(planes, crossPower);
            }
            finally
            {
                foreach (Mat plane in planes) plane.Dispose();
            }
            using Mat correlation = new();
            Cv2.Dft(crossPower, correlation, DftFlags.Inverse | DftFlags.RealOutput | DftFlags.Scale);
            cancellationToken.ThrowIfCancellationRequested();

            double maximum = double.NegativeInfinity;
            int peakX = 0;
            int peakY = 0;
            int correlationRows = correlation.Rows;
            int correlationColumns = correlation.Cols;
            for (int y = 0; y < correlationRows; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                for (int x = 0; x < correlationColumns; x++)
                {
                    double value = correlation.At<float>(y, x);
                    if (value > maximum) (maximum, peakX, peakY) = (value, x, y);
                }
            }
            double runnerUp = double.NegativeInfinity;
            const int exclusionRadius = 2;
            for (int y = 0; y < correlationRows; y++)
            {
                if ((y & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                int dy = Math.Min(Math.Abs(y - peakY), correlationRows - Math.Abs(y - peakY));
                for (int x = 0; x < correlationColumns; x++)
                {
                    int dx = Math.Min(Math.Abs(x - peakX), correlationColumns - Math.Abs(x - peakX));
                    if (dx <= exclusionRadius && dy <= exclusionRadius) continue;
                    runnerUp = Math.Max(runnerUp, correlation.At<float>(y, x));
                }
            }
            double uniqueness = maximum <= 1e-12 || !double.IsFinite(runnerUp)
                ? 0
                : Math.Clamp((maximum - runnerUp) / Math.Abs(maximum), 0, 1);
            return new PhasePeakQuality(maximum, runnerUp, uniqueness);
        }

        private static RegistrationEstimate EstimateFeatures(
            Mat referenceGray,
            Mat movingGray,
            ImageRegistrationParameters parameters,
            AlgorithmExecutionContext context,
            CancellationToken cancellationToken)
        {
            context.Progress?.Report(new AlgorithmProgress(0.10, "registration.features"));
            using Mat reference8 = new();
            using Mat moving8 = new();
            referenceGray.ConvertTo(reference8, MatType.CV_8UC1, byte.MaxValue);
            movingGray.ConvertTo(moving8, MatType.CV_8UC1, byte.MaxValue);
            using ORB orb = ORB.Create(parameters.MaximumFeatures, (float)parameters.PyramidScaleFactor, parameters.PyramidLevels,
                31, 0, 2, ORBScoreType.Harris, 31, parameters.FastThreshold);
            using Mat referenceDescriptors = new();
            using Mat movingDescriptors = new();
            orb.DetectAndCompute(reference8, null, out KeyPoint[] referenceKeys, referenceDescriptors);
            cancellationToken.ThrowIfCancellationRequested();
            orb.DetectAndCompute(moving8, null, out KeyPoint[] movingKeys, movingDescriptors);
            cancellationToken.ThrowIfCancellationRequested();
            if (referenceDescriptors.Empty() || movingDescriptors.Empty())
                return RegistrationEstimate.Fail("feature_descriptors_missing", "ORB could not compute descriptors for both inputs.", "inputs");

            context.Progress?.Report(new AlgorithmProgress(0.28, "registration.match"));
            using BFMatcher matcher = new(NormTypes.Hamming, crossCheck: false);
            DMatch[][] forward = KnnMatchInBatches(matcher, movingDescriptors, referenceDescriptors, "registration.match.forward", 0.28, 0.05, cancellationToken, context.Progress);
            DMatch[][] reverse = KnnMatchInBatches(matcher, referenceDescriptors, movingDescriptors, "registration.match.reverse", 0.33, 0.05, cancellationToken, context.Progress);
            Dictionary<(int Moving, int Reference), float> reverseAccepted = RatioMatches(reverse, parameters.LoweRatio)
                .ToDictionary(match => (match.TrainIdx, match.QueryIdx), match => match.Distance);
            RegistrationMatch[] matches = RatioMatches(forward, parameters.LoweRatio)
                .Where(match => reverseAccepted.ContainsKey((match.QueryIdx, match.TrainIdx)))
                .OrderBy(match => match.Distance)
                .ThenBy(match => match.QueryIdx)
                .ThenBy(match => match.TrainIdx)
                .Select((match, index) => new RegistrationMatch(
                    index,
                    new Point2d(movingKeys[match.QueryIdx].Pt.X, movingKeys[match.QueryIdx].Pt.Y),
                    new Point2d(referenceKeys[match.TrainIdx].Pt.X, referenceKeys[match.TrainIdx].Pt.Y),
                    match.Distance,
                    false,
                    double.NaN))
                .ToArray();
            if (matches.Length < parameters.MinimumMatchCount)
                return RegistrationEstimate.Fail("feature_matches_insufficient", $"Only {matches.Length} mutual ratio matches were found.", nameof(parameters.MinimumMatchCount), referenceKeys.Length, movingKeys.Length, matches);

            context.Progress?.Report(new AlgorithmProgress(0.40, "registration.consensus"));
            RegistrationConsensus? best = FindConsensus(matches, parameters, cancellationToken, context.Progress);
            if (best == null)
                return RegistrationEstimate.Fail("feature_consensus_failed", "No non-degenerate four-point homography consensus was found.", "matches", referenceKeys.Length, movingKeys.Length, matches);

            RegistrationMatch[] initial = Evaluate(best.Matrix, matches, parameters.ConsensusReprojectionThresholdPixels);
            RegistrationMatch[] initialInliers = initial.Where(match => match.Inlier).ToArray();
            if (initialInliers.Length < 4)
                return RegistrationEstimate.Fail("feature_consensus_insufficient", "The best consensus has fewer than four inliers.", "matches", referenceKeys.Length, movingKeys.Length, initial);
            using Mat refined = Cv2.FindHomography(
                initialInliers.Select(match => match.Moving),
                initialInliers.Select(match => match.Reference),
                HomographyMethods.None,
                0,
                null,
                0,
                0.995);
            cancellationToken.ThrowIfCancellationRequested();
            if (refined.Empty())
                return RegistrationEstimate.Fail("feature_refinement_failed", "Homography refinement failed.", "matches", referenceKeys.Length, movingKeys.Length, initial);
            double[] matrix = ReadMatrix(refined);
            RegistrationMatch[] evaluated = Evaluate(matrix, matches, parameters.ConsensusReprojectionThresholdPixels);
            RegistrationMatch[] inliers = evaluated.Where(match => match.Inlier).ToArray();
            double ratio = inliers.Length / (double)evaluated.Length;
            double rmse = inliers.Length == 0 ? double.PositiveInfinity : Math.Sqrt(inliers.Sum(match => match.ResidualPixels * match.ResidualPixels) / inliers.Length);
            if (inliers.Length < parameters.MinimumInlierCount)
                return RegistrationEstimate.Fail("feature_inliers_insufficient", $"Only {inliers.Length} inliers remain after refinement.", nameof(parameters.MinimumInlierCount), referenceKeys.Length, movingKeys.Length, evaluated);
            if (ratio < parameters.MinimumInlierRatio)
                return RegistrationEstimate.Fail("feature_inlier_ratio_below_threshold", $"Inlier ratio {ratio.ToString("P2", CultureInfo.InvariantCulture)} is below the configured minimum.", nameof(parameters.MinimumInlierRatio), referenceKeys.Length, movingKeys.Length, evaluated);
            double confidence = Math.Clamp(ratio * Math.Exp(-rmse / Math.Max(1e-9, parameters.ConsensusReprojectionThresholdPixels)), 0, 1);
            return new RegistrationEstimate(matrix, confidence, rmse, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, referenceKeys.Length, movingKeys.Length, evaluated);
        }

        private static IEnumerable<DMatch> RatioMatches(IEnumerable<DMatch[]> groups, double ratio)
            => groups.Where(group => group.Length >= 2 && group[0].Distance < group[1].Distance * ratio).Select(group => group[0]);

        private static bool IsOrbWorkWithinBudget(ImageRegistrationParameters parameters, out string reason)
        {
            long descriptorComparisons = checked(2L * parameters.MaximumFeatures * parameters.MaximumFeatures);
            long consensusWork = checked((long)parameters.MaximumConsensusMatches * parameters.MaximumConsensusEvaluations);
            if (parameters.MaximumFeatures > MaximumOrbFeatureBudget || descriptorComparisons > MaximumDescriptorComparisonBudget)
            {
                reason = $"ORB feature/matching request exceeds the bounded budget ({parameters.MaximumFeatures} features; {descriptorComparisons} worst-case descriptor comparisons).";
                return false;
            }
            if (consensusWork > MaximumConsensusWorkBudget)
            {
                reason = $"ORB consensus request exceeds the bounded budget ({consensusWork} candidate-match evaluations).";
                return false;
            }
            reason = string.Empty;
            return true;
        }

        private static DMatch[][] KnnMatchInBatches(
            BFMatcher matcher,
            Mat queryDescriptors,
            Mat trainDescriptors,
            string stage,
            double progressStart,
            double progressSpan,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int queryRows = queryDescriptors.Rows;
            List<DMatch[]> matches = new(queryRows);
            for (int start = 0; start < queryRows; start += DescriptorMatchBatchSize)
            {
                progress?.Report(new AlgorithmProgress(progressStart + progressSpan * start / Math.Max(1, queryRows), stage));
                cancellationToken.ThrowIfCancellationRequested();
                int end = Math.Min(queryRows, start + DescriptorMatchBatchSize);
                using Mat batch = queryDescriptors.RowRange(start, end);
                DMatch[][] batchMatches = matcher.KnnMatch(batch, trainDescriptors, 2);
                foreach (DMatch[] group in batchMatches)
                {
                    DMatch[] adjusted = new DMatch[group.Length];
                    for (int index = 0; index < group.Length; index++)
                    {
                        DMatch match = group[index];
                        adjusted[index] = new DMatch(match.QueryIdx + start, match.TrainIdx, match.ImgIdx, match.Distance);
                    }
                    matches.Add(adjusted);
                }
                cancellationToken.ThrowIfCancellationRequested();
            }
            return matches.ToArray();
        }

        internal static RegistrationConsensus? FindConsensus(
            RegistrationMatch[] matches,
            ImageRegistrationParameters parameters,
            CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress)
        {
            int count = Math.Min(matches.Length, parameters.MaximumConsensusMatches);
            RegistrationConsensus? best = null;
            int evaluations = 0;
            Point2f[] moving = new Point2f[4];
            Point2f[] reference = new Point2f[4];
            IReadOnlyList<(int A, int B, int C, int D)> samples = BuildConsensusSamples(count, parameters.MaximumConsensusEvaluations);
            foreach ((int a, int b, int c, int d) in samples)
            {
                if ((evaluations++ & 63) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    progress?.Report(new AlgorithmProgress(0.40 + 0.25 * evaluations / Math.Max(1, samples.Count), "registration.consensus"));
                }
                moving[0] = ToPoint(matches[a].Moving);
                moving[1] = ToPoint(matches[b].Moving);
                moving[2] = ToPoint(matches[c].Moving);
                moving[3] = ToPoint(matches[d].Moving);
                reference[0] = ToPoint(matches[a].Reference);
                reference[1] = ToPoint(matches[b].Reference);
                reference[2] = ToPoint(matches[c].Reference);
                reference[3] = ToPoint(matches[d].Reference);
                if (Degenerate(moving) || Degenerate(reference)) continue;
                using Mat candidate = Cv2.GetPerspectiveTransform(moving, reference);
                if (candidate.Empty()) continue;
                double[] matrix = ReadMatrix(candidate);
                if (!GeometricTransformAlgorithmProvider.TryInvert(matrix, out double[] inverse, out _)
                    || !double.IsFinite(GeometricTransformAlgorithmProvider.ConditionNumber(matrix, inverse))) continue;
                (int inlierCount, double squaredResidualSum) = EvaluateConsensus(matrix, matches, parameters.ConsensusReprojectionThresholdPixels, cancellationToken);
                if (inlierCount < 4) continue;
                double rmse = Math.Sqrt(squaredResidualSum / inlierCount);
                if (best == null || inlierCount > best.InlierCount || inlierCount == best.InlierCount && rmse < best.Rmse)
                    best = new RegistrationConsensus(matrix, inlierCount, rmse);
            }
            cancellationToken.ThrowIfCancellationRequested();
            return best;
        }

        internal static IReadOnlyList<(int A, int B, int C, int D)> BuildConsensusSamples(int count, int maximumEvaluations)
        {
            if (count < 4 || maximumEvaluations <= 0) return [];
            long combinations = (long)count * (count - 1) * (count - 2) * (count - 3) / 24;
            int target = (int)Math.Min(maximumEvaluations, combinations);
            List<(int A, int B, int C, int D)> samples = new(target);
            HashSet<ulong> seen = new(target);
            if (count >= 5)
            {
                int a = 1;
                int b = Math.Max(2, count / 3);
                int c = Math.Max(b + 1, count * 2 / 3);
                int d = count - 1;
                SortFour(ref a, ref b, ref c, ref d);
                Add(a, b, c, d);
            }

            ulong state = 0x9E3779B97F4A7C15UL ^ (uint)count * 0xBF58476D1CE4E5B9UL ^ (uint)maximumEvaluations;
            int attempts = 0;
            int maximumAttempts = checked(target * 64 + 1_024);
            while (samples.Count < target && attempts++ < maximumAttempts)
            {
                int a = NextIndex(ref state, count);
                int b;
                do b = NextIndex(ref state, count); while (b == a);
                int c;
                do c = NextIndex(ref state, count); while (c == a || c == b);
                int d;
                do d = NextIndex(ref state, count); while (d == a || d == b || d == c);
                SortFour(ref a, ref b, ref c, ref d);
                Add(a, b, c, d);
            }
            if (samples.Count < target)
            {
                for (int a = 0; a < count - 3 && samples.Count < target; a++)
                for (int b = a + 1; b < count - 2 && samples.Count < target; b++)
                for (int c = b + 1; c < count - 1 && samples.Count < target; c++)
                for (int d = c + 1; d < count && samples.Count < target; d++)
                    Add(a, b, c, d);
            }
            return samples;

            void Add(int a, int b, int c, int d)
            {
                ulong key = (uint)a | ((ulong)(uint)b << 16) | ((ulong)(uint)c << 32) | ((ulong)(uint)d << 48);
                if (seen.Add(key)) samples.Add((a, b, c, d));
            }
        }

        private static int NextIndex(ref ulong state, int count)
        {
            state += 0x9E3779B97F4A7C15UL;
            ulong value = state;
            value = (value ^ (value >> 30)) * 0xBF58476D1CE4E5B9UL;
            value = (value ^ (value >> 27)) * 0x94D049BB133111EBUL;
            value ^= value >> 31;
            return (int)(value % (uint)count);
        }

        private static void SortFour(ref int a, ref int b, ref int c, ref int d)
        {
            if (a > b) (a, b) = (b, a);
            if (c > d) (c, d) = (d, c);
            if (a > c) (a, c) = (c, a);
            if (b > d) (b, d) = (d, b);
            if (b > c) (b, c) = (c, b);
        }

        private static (int InlierCount, double SquaredResidualSum) EvaluateConsensus(
            double[] matrix,
            RegistrationMatch[] matches,
            double threshold,
            CancellationToken cancellationToken)
        {
            int inlierCount = 0;
            double squaredResidualSum = 0;
            double thresholdSquared = threshold * threshold;
            for (int index = 0; index < matches.Length; index++)
            {
                if ((index & 63) == 0) cancellationToken.ThrowIfCancellationRequested();
                RegistrationMatch match = matches[index];
                AlgorithmPoint projected = GeometricTransformAlgorithmProvider.Transform(matrix, match.Moving.X, match.Moving.Y);
                if (!double.IsFinite(projected.X) || !double.IsFinite(projected.Y)) continue;
                double dx = projected.X - match.Reference.X;
                double dy = projected.Y - match.Reference.Y;
                double squaredResidual = dx * dx + dy * dy;
                if (squaredResidual > thresholdSquared) continue;
                inlierCount++;
                squaredResidualSum += squaredResidual;
            }
            return (inlierCount, squaredResidualSum);
        }

        private static bool Degenerate(Point2f[] points)
        {
            double maximumArea2 = 0;
            for (int a = 0; a < points.Length - 2; a++)
            for (int b = a + 1; b < points.Length - 1; b++)
            for (int c = b + 1; c < points.Length; c++)
                maximumArea2 = Math.Max(maximumArea2, Math.Abs((points[b].X - points[a].X) * (points[c].Y - points[a].Y) - (points[b].Y - points[a].Y) * (points[c].X - points[a].X)));
            return maximumArea2 < 1e-3;
        }

        private static RegistrationMatch[] Evaluate(double[] matrix, RegistrationMatch[] matches, double threshold)
        {
            RegistrationMatch[] result = new RegistrationMatch[matches.Length];
            for (int index = 0; index < matches.Length; index++)
            {
                RegistrationMatch match = matches[index];
                AlgorithmPoint projected = GeometricTransformAlgorithmProvider.Transform(matrix, match.Moving.X, match.Moving.Y);
                double residual = double.IsFinite(projected.X) && double.IsFinite(projected.Y)
                    ? Math.Sqrt(Math.Pow(projected.X - match.Reference.X, 2) + Math.Pow(projected.Y - match.Reference.Y, 2))
                    : double.PositiveInfinity;
                result[index] = match with { Inlier = residual <= threshold, ResidualPixels = residual };
            }
            return result;
        }

        private static Mat ToNormalizedGray(AlgorithmImageBuffer image)
        {
            using AlgorithmImageMatLease lease = AlgorithmImageInterop.BorrowReadOnly(image);
            using Mat gray = new();
            if (image.Format.Channels() == 1) lease.Mat.CopyTo(gray);
            else Cv2.CvtColor(lease.Mat, gray, image.Format.Channels() == 3 ? ColorConversionCodes.BGR2GRAY : ColorConversionCodes.BGRA2GRAY);
            Mat normalized = new();
            double peak = image.Format.IsFloatingPoint() ? 1 : image.Format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            gray.ConvertTo(normalized, MatType.CV_32FC1, 1 / peak);
            return normalized;
        }

        private static double PhotometricRmse(Mat referenceGray, Mat registered, AlgorithmImageFormat format, byte[] mask, long validPixels)
        {
            if (validPixels == 0) return double.NaN;
            using Mat registeredGray = new();
            if (format.Channels() == 1) registered.CopyTo(registeredGray);
            else Cv2.CvtColor(registered, registeredGray, format.Channels() == 3 ? ColorConversionCodes.BGR2GRAY : ColorConversionCodes.BGRA2GRAY);
            using Mat normalized = new();
            double peak = format.IsFloatingPoint() ? 1 : format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            registeredGray.ConvertTo(normalized, MatType.CV_32FC1, 1 / peak);
            using Mat difference = new();
            using Mat squared = new();
            Cv2.Absdiff(referenceGray, normalized, difference);
            Cv2.Multiply(difference, difference, squared);
            using Mat maskMat = Mat.FromPixelData(referenceGray.Rows, referenceGray.Cols, MatType.CV_8UC1, mask);
            return Math.Sqrt(Cv2.Mean(squared, maskMat).Val0);
        }

        private static Scalar BorderScalar(AlgorithmImageFormat format, ImageRegistrationParameters parameters)
        {
            double peak = format.IsFloatingPoint() ? 1 : format.BitsPerChannel() == 8 ? byte.MaxValue : ushort.MaxValue;
            return new Scalar(parameters.BorderChannel0 * peak, parameters.BorderChannel1 * peak, parameters.BorderChannel2 * peak, parameters.BorderChannel3 * peak);
        }

        private static AlgorithmMeasurementArtifact BuildMeasurements(
            AlgorithmImageBuffer reference,
            AlgorithmImageBuffer moving,
            RegistrationEstimate estimate,
            long validPixels,
            long outputPixels,
            double photometricRmse,
            double determinant,
            double condition,
            double inverseResidual)
        {
            List<AlgorithmMeasurement> measurements =
            [
                new("registration.reference_width", reference.Width, "px"),
                new("registration.reference_height", reference.Height, "px"),
                new("registration.moving_width", moving.Width, "px"),
                new("registration.moving_height", moving.Height, "px"),
                new("registration.valid_fraction", validPixels / (double)outputPixels, "ratio"),
                new("registration.phase_shift_x", estimate.PhaseShiftX, "px"),
                new("registration.phase_shift_y", estimate.PhaseShiftY, "px"),
                new("registration.phase_response", estimate.PhaseResponse, "ratio"),
                new("registration.phase_peak_uniqueness", estimate.PhasePeakUniqueness, "ratio"),
                new("registration.reference_feature_count", estimate.ReferenceFeatureCount),
                new("registration.moving_feature_count", estimate.MovingFeatureCount),
                new("registration.match_count", estimate.Matches.Count),
                new("registration.inlier_count", estimate.Matches.Count(match => match.Inlier)),
                new("registration.inlier_ratio", estimate.Matches.Count == 0 ? double.NaN : estimate.Matches.Count(match => match.Inlier) / (double)estimate.Matches.Count, "ratio"),
                new("registration.photometric_rmse", photometricRmse, "normalized-DN"),
                new("registration.confidence", estimate.Confidence, "heuristic-ratio"),
                new("registration.determinant", determinant),
                new("registration.condition_number", condition),
                new("registration.inverse_residual", inverseResidual),
            ];
            if (double.IsFinite(estimate.GeometricRmse))
                measurements.Add(new("registration.geometric_rmse", estimate.GeometricRmse, "px"));
            if (double.IsFinite(estimate.CorrelationLoss))
                measurements.Add(new("registration.correlation_loss", estimate.CorrelationLoss, "ratio"));
            return new AlgorithmMeasurementArtifact("image-registration-summary", measurements);
        }

        private static AlgorithmTableArtifact BuildMatrixTable(double[] matrix, double[] inverse)
        {
            List<IReadOnlyDictionary<string, JsonElement>> rows = new(3);
            for (int row = 0; row < 3; row++)
            {
                int offset = row * 3;
                rows.Add(new Dictionary<string, JsonElement>
                {
                    ["Row"] = AlgorithmJson.ToElement(row + 1),
                    ["M1"] = AlgorithmJson.ToElement(matrix[offset]),
                    ["M2"] = AlgorithmJson.ToElement(matrix[offset + 1]),
                    ["M3"] = AlgorithmJson.ToElement(matrix[offset + 2]),
                    ["InverseM1"] = AlgorithmJson.ToElement(inverse[offset]),
                    ["InverseM2"] = AlgorithmJson.ToElement(inverse[offset + 1]),
                    ["InverseM3"] = AlgorithmJson.ToElement(inverse[offset + 2]),
                });
            }
            return new AlgorithmTableArtifact("image-registration-matrix",
            [
                new("Row", "integer"), new("M1", "number"), new("M2", "number"), new("M3", "number"),
                new("InverseM1", "number"), new("InverseM2", "number"), new("InverseM3", "number"),
            ], rows);
        }

        private static AlgorithmTableArtifact BuildMatchTable(IReadOnlyList<RegistrationMatch> matches, int maximum)
        {
            IReadOnlyDictionary<string, JsonElement>[] rows = matches.Take(maximum).Select(match => (IReadOnlyDictionary<string, JsonElement>)new Dictionary<string, JsonElement>
            {
                ["Index"] = AlgorithmJson.ToElement(match.Index),
                ["MovingX"] = AlgorithmJson.ToElement(match.Moving.X),
                ["MovingY"] = AlgorithmJson.ToElement(match.Moving.Y),
                ["ReferenceX"] = AlgorithmJson.ToElement(match.Reference.X),
                ["ReferenceY"] = AlgorithmJson.ToElement(match.Reference.Y),
                ["DescriptorDistance"] = AlgorithmJson.ToElement(match.DescriptorDistance),
                ["ResidualPixels"] = AlgorithmJson.ToElement(match.ResidualPixels),
                ["Inlier"] = AlgorithmJson.ToElement(match.Inlier),
            }).ToArray();
            return new AlgorithmTableArtifact("image-registration-matches",
            [
                new("Index", "integer"), new("MovingX", "number", "px"), new("MovingY", "number", "px"),
                new("ReferenceX", "number", "px"), new("ReferenceY", "number", "px"),
                new("DescriptorDistance", "number"), new("ResidualPixels", "number", "px"), new("Inlier", "boolean"),
            ], rows);
        }

        private static double[] ReadMatrix(Mat matrix)
        {
            using Mat converted = new();
            matrix.ConvertTo(converted, MatType.CV_64FC1);
            double[] result = new double[9];
            for (int row = 0; row < 3; row++)
                for (int column = 0; column < 3; column++)
                    result[row * 3 + column] = converted.At<double>(row, column);
            return result;
        }

        private static Point2f ToPoint(Point2d point) => new((float)point.X, (float)point.Y);

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        internal sealed record RegistrationMatch(
            int Index,
            Point2d Moving,
            Point2d Reference,
            double DescriptorDistance,
            bool Inlier,
            double ResidualPixels);

        internal sealed record RegistrationConsensus(double[] Matrix, int InlierCount, double Rmse);

        private sealed record PhasePeakQuality(double Peak, double RunnerUp, double Uniqueness);

        private sealed record RegistrationEstimate(
            double[]? Matrix,
            double Confidence,
            double GeometricRmse,
            double CorrelationLoss,
            double PhaseShiftX,
            double PhaseShiftY,
            double PhaseResponse,
            double PhasePeakUniqueness,
            int ReferenceFeatureCount,
            int MovingFeatureCount,
            IReadOnlyList<RegistrationMatch> Matches,
            AlgorithmFailure? Failure = null)
        {
            public static RegistrationEstimate Fail(
                string code,
                string message,
                string path,
                int referenceFeatureCount = 0,
                int movingFeatureCount = 0,
                IReadOnlyList<RegistrationMatch>? matches = null)
                => new(null, 0, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, double.NaN, referenceFeatureCount, movingFeatureCount, matches ?? [], new AlgorithmFailure(code, message, path));
        }
    }
}
