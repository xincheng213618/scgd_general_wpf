using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic normalized algebraic initialization and geometric/Huber circle fit.</summary>
    public sealed class CircleFitAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.measurement.circle-fit/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.circle-fit.cpu",
            "ColorVision Circle Fit CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            114,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.CircleFit, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.CircleFit
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Invocation.Roi is not PolylineAlgorithmRoi path || path.Points.Count < 3)
                return ValueTask.FromResult(Failure(context, "circle_fit_points_required", "Circle fit requires a polyline ROI containing at least three point vertices.", "roi"));

            AlgorithmImageBuffer image = context.Inputs[0].Image;
            CircleFitParameters parameters = (CircleFitParameters)context.Parameters;
            AlgorithmPoint[] points = path.Points
                .Select(point => AlgorithmCoordinates.ToPixel(point, path.CoordinateSpace, image.DpiX, image.DpiY))
                .ToArray();
            if (points.Length > parameters.MaximumPoints)
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "circle_fit_point_limit_exceeded",
                    $"The ROI contains {points.Length} points, exceeding MaximumPoints={parameters.MaximumPoints}.",
                    nameof(parameters.MaximumPoints)));
            }

            context.Progress?.Report(new AlgorithmProgress(0.05, "circle-fit.solve", "Fitting circle"));
            CircleState? fit = Fit(points, parameters, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            string? rejection = ResolveRejection(fit, parameters);
            context.Progress?.Report(new AlgorithmProgress(0.72, "circle-fit.artifacts", "Building circle artifacts"));
            IReadOnlyList<AlgorithmArtifact> artifacts = BuildArtifacts(context, image, path, parameters, points, fit, rejection, cancellationToken);
            List<AlgorithmDiagnosticMessage> diagnostics = new();
            if (rejection != null)
                diagnostics.Add(new AlgorithmDiagnosticMessage("circle_fit_rejected", $"The circle fit was rejected: {rejection}.", "warning"));
            else if (fit!.InlierCount != points.Length)
                diagnostics.Add(new AlgorithmDiagnosticMessage("circle_fit_points_rejected", $"Rejected {points.Length - fit.InlierCount} of {points.Length} points by radial residual threshold.", "warning"));

            context.Progress?.Report(new AlgorithmProgress(1, "circle-fit.complete"));
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

        private static CircleState? Fit(AlgorithmPoint[] points, CircleFitParameters parameters, CancellationToken cancellationToken)
        {
            double[] weights = Enumerable.Repeat(1d, points.Length).ToArray();
            CircleState? fit = parameters.Mode == CircleFitMode.RobustHuber
                ? FitConsensus(points, parameters, cancellationToken) ?? FitAlgebraic(points, weights, cancellationToken)
                : FitAlgebraic(points, weights, cancellationToken);
            if (fit == null) return null;

            if (parameters.Mode == CircleFitMode.RobustHuber)
            {
                for (int iteration = 0; iteration < parameters.MaximumIterations; iteration++)
                {
                    double[] residuals = Residuals(points, fit, cancellationToken);
                    double[] absoluteResiduals = residuals.Select(Math.Abs).ToArray();
                    double scale = Math.Max(1e-12, 1.4826 * Median(absoluteResiduals));
                    double cutoff = parameters.HuberTuningConstant * scale;
                    for (int index = 0; index < weights.Length; index++)
                        weights[index] = absoluteResiduals[index] <= cutoff ? 1 : cutoff / absoluteResiduals[index];
                    CircleState? next = RefineGeometric(points, weights, fit, parameters, cancellationToken);
                    if (next == null) return null;
                    double change = Math.Max(
                        Math.Max(Math.Abs(next.CenterX - fit.CenterX), Math.Abs(next.CenterY - fit.CenterY)),
                        Math.Abs(next.Radius - fit.Radius));
                    fit = next;
                    if (change <= parameters.ConvergenceTolerance) break;
                }
            }

            bool[] previousInliers = new bool[points.Length];
            for (int pass = 0; pass < 4; pass++)
            {
                double[] residuals = Residuals(points, fit, cancellationToken);
                bool[] inliers = residuals.Select(value => Math.Abs(value) <= parameters.ResidualThresholdPixels).ToArray();
                int count = inliers.Count(value => value);
                fit = fit with { ResidualValues = residuals, InlierValues = inliers, InlierCount = count };
                if (count < 3 || inliers.SequenceEqual(previousInliers)) break;
                previousInliers = inliers;
                double[] inlierWeights = inliers.Select(value => value ? 1d : 0d).ToArray();
                CircleState? refined = FitAlgebraic(points, inlierWeights, cancellationToken);
                if (refined == null) break;
                fit = RefineGeometric(points, inlierWeights, refined, parameters, cancellationToken) ?? refined;
            }

            double[] finalResiduals = Residuals(points, fit, cancellationToken);
            bool[] finalInliers = finalResiduals.Select(value => Math.Abs(value) <= parameters.ResidualThresholdPixels).ToArray();
            int finalCount = finalInliers.Count(value => value);
            double rms = finalCount == 0 ? double.NaN : Math.Sqrt(finalResiduals.Where((_, index) => finalInliers[index]).Average(value => value * value));
            double maximum = finalCount == 0 ? double.NaN : finalResiduals.Where((_, index) => finalInliers[index]).Max(value => Math.Abs(value));
            double coverage = AngularCoverageDegrees(points, fit, finalInliers);
            double confidence = finalCount == 0 ? 0
                : coverage / 360 * finalCount / points.Length / (1 + rms / parameters.ResidualThresholdPixels);
            return fit with
            {
                ResidualValues = finalResiduals,
                InlierValues = finalInliers,
                InlierCount = finalCount,
                RootMeanSquareResidual = rms,
                MaximumResidual = maximum,
                AngularCoverageDegrees = coverage,
                Confidence = Math.Clamp(confidence, 0, 1),
            };
        }

        private static CircleState? FitConsensus(AlgorithmPoint[] points, CircleFitParameters parameters, CancellationToken cancellationToken)
        {
            long combinationCount = points.Length < 2_000
                ? (long)points.Length * (points.Length - 1) * (points.Length - 2) / 6
                : long.MaxValue;
            int evaluationBound = (int)Math.Max(1, Math.Min(int.MaxValue, parameters.MaximumConsensusEvaluations / points.Length));
            int candidateLimit = Math.Min(parameters.MaximumConsensusCandidates, evaluationBound);
            CircleState? best = null;
            int bestInliers = -1;
            double bestRms = double.PositiveInfinity;
            int evaluated = 0;

            if (combinationCount <= candidateLimit)
            {
                for (int first = 0; first < points.Length - 2; first++)
                for (int second = first + 1; second < points.Length - 1; second++)
                for (int third = second + 1; third < points.Length; third++)
                    Evaluate(first, second, third);
            }
            else
            {
                HashSet<(int First, int Second, int Third)> triples = new();
                int attempts = 0;
                while (triples.Count < candidateLimit && attempts < candidateLimit * 32)
                {
                    uint value = unchecked((uint)(attempts + 1) * 2654435761u);
                    int first = (int)(value % (uint)points.Length);
                    value = unchecked(value * 2246822519u + 3266489917u);
                    int second = (int)(value % (uint)points.Length);
                    value = unchecked(value * 2246822519u + 668265263u);
                    int third = (int)(value % (uint)points.Length);
                    if (first != second && first != third && second != third)
                    {
                        int[] ordered = [first, second, third];
                        Array.Sort(ordered);
                        triples.Add((ordered[0], ordered[1], ordered[2]));
                    }
                    attempts++;
                }
                foreach ((int first, int second, int third) in triples) Evaluate(first, second, third);
            }
            return best;

            void Evaluate(int first, int second, int third)
            {
                if ((evaluated++ & 31) == 0) cancellationToken.ThrowIfCancellationRequested();
                if (!TryCircleThrough(points[first], points[second], points[third], out CircleState? candidate) || candidate is null) return;
                int inliers = 0;
                double squaredResidualSum = 0;
                for (int index = 0; index < points.Length; index++)
                {
                    if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double dx = points[index].X - candidate.CenterX;
                    double dy = points[index].Y - candidate.CenterY;
                    double residual = Math.Abs(Math.Sqrt(dx * dx + dy * dy) - candidate.Radius);
                    if (residual > parameters.ResidualThresholdPixels) continue;
                    inliers++;
                    squaredResidualSum += residual * residual;
                }
                double rms = inliers == 0 ? double.PositiveInfinity : Math.Sqrt(squaredResidualSum / inliers);
                if (inliers > bestInliers || (inliers == bestInliers && rms < bestRms))
                {
                    best = candidate;
                    bestInliers = inliers;
                    bestRms = rms;
                }
            }
        }

        private static bool TryCircleThrough(AlgorithmPoint first, AlgorithmPoint second, AlgorithmPoint third, out CircleState? circle)
        {
            double x2 = second.X - first.X;
            double y2 = second.Y - first.Y;
            double x3 = third.X - first.X;
            double y3 = third.Y - first.Y;
            double scale = Math.Max(Math.Sqrt(x2 * x2 + y2 * y2), Math.Sqrt(x3 * x3 + y3 * y3));
            if (!double.IsFinite(scale) || scale <= 1e-12)
            {
                circle = null;
                return false;
            }
            x2 /= scale;
            y2 /= scale;
            x3 /= scale;
            y3 /= scale;
            double determinant = 2 * (x2 * y3 - y2 * x3);
            if (Math.Abs(determinant) <= 1e-12)
            {
                circle = null;
                return false;
            }
            double norm2 = x2 * x2 + y2 * y2;
            double norm3 = x3 * x3 + y3 * y3;
            double centerXNormalized = (norm2 * y3 - norm3 * y2) / determinant;
            double centerYNormalized = (x2 * norm3 - x3 * norm2) / determinant;
            double centerX = first.X + centerXNormalized * scale;
            double centerY = first.Y + centerYNormalized * scale;
            double radius = Math.Sqrt(centerXNormalized * centerXNormalized + centerYNormalized * centerYNormalized) * scale;
            if (!double.IsFinite(centerX) || !double.IsFinite(centerY) || !double.IsFinite(radius) || radius <= 1e-12)
            {
                circle = null;
                return false;
            }
            circle = new CircleState(centerX, centerY, radius);
            return true;
        }

        private static CircleState? FitAlgebraic(AlgorithmPoint[] points, double[] weights, CancellationToken cancellationToken)
        {
            double sumWeight = 0;
            double meanX = 0;
            double meanY = 0;
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                double weight = weights[index];
                sumWeight += weight;
                meanX += weight * points[index].X;
                meanY += weight * points[index].Y;
            }
            if (sumWeight <= 0) return null;
            meanX /= sumWeight;
            meanY /= sumWeight;

            double scaleSquared = 0;
            for (int index = 0; index < points.Length; index++)
            {
                double dx = points[index].X - meanX;
                double dy = points[index].Y - meanY;
                scaleSquared += weights[index] * (dx * dx + dy * dy);
            }
            double scale = Math.Sqrt(scaleSquared / sumWeight);
            if (!double.IsFinite(scale) || scale <= 1e-12) return null;

            double[,] matrix = new double[3, 3];
            double[] right = new double[3];
            int positiveWeights = 0;
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                double weight = weights[index];
                if (weight <= 0) continue;
                positiveWeights++;
                double x = (points[index].X - meanX) / scale;
                double y = (points[index].Y - meanY) / scale;
                double z = x * x + y * y;
                double a = 2 * x;
                double b = 2 * y;
                matrix[0, 0] += weight * a * a;
                matrix[0, 1] += weight * a * b;
                matrix[0, 2] += weight * a;
                matrix[1, 1] += weight * b * b;
                matrix[1, 2] += weight * b;
                matrix[2, 2] += weight;
                right[0] += weight * a * z;
                right[1] += weight * b * z;
                right[2] += weight * z;
            }
            if (positiveWeights < 3) return null;
            matrix[1, 0] = matrix[0, 1];
            matrix[2, 0] = matrix[0, 2];
            matrix[2, 1] = matrix[1, 2];
            if (!TrySolve3(matrix, right, out double[] solution)) return null;
            double radiusSquared = solution[0] * solution[0] + solution[1] * solution[1] + solution[2];
            if (!double.IsFinite(radiusSquared) || radiusSquared <= 1e-24) return null;
            double centerX = meanX + solution[0] * scale;
            double centerY = meanY + solution[1] * scale;
            double radius = Math.Sqrt(radiusSquared) * scale;
            if (!double.IsFinite(centerX) || !double.IsFinite(centerY) || !double.IsFinite(radius)) return null;
            return new CircleState(centerX, centerY, radius);
        }

        private static CircleState? RefineGeometric(
            AlgorithmPoint[] points,
            double[] weights,
            CircleState initial,
            CircleFitParameters parameters,
            CancellationToken cancellationToken)
        {
            CircleState fit = initial;
            for (int iteration = 0; iteration < parameters.MaximumIterations; iteration++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double[,] matrix = new double[3, 3];
                double[] right = new double[3];
                int usable = 0;
                for (int index = 0; index < points.Length; index++)
                {
                    if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                    double weight = weights[index];
                    if (weight <= 0) continue;
                    double dx = fit.CenterX - points[index].X;
                    double dy = fit.CenterY - points[index].Y;
                    double distance = Math.Sqrt(dx * dx + dy * dy);
                    if (!double.IsFinite(distance) || distance <= 1e-15) continue;
                    usable++;
                    double residual = distance - fit.Radius;
                    double[] jacobian = [dx / distance, dy / distance, -1];
                    for (int row = 0; row < 3; row++)
                    {
                        right[row] -= weight * jacobian[row] * residual;
                        for (int column = row; column < 3; column++)
                            matrix[row, column] += weight * jacobian[row] * jacobian[column];
                    }
                }
                if (usable < 3) return null;
                matrix[1, 0] = matrix[0, 1];
                matrix[2, 0] = matrix[0, 2];
                matrix[2, 1] = matrix[1, 2];
                if (!TrySolve3(matrix, right, out double[] delta)) return fit;
                double radius = fit.Radius + delta[2];
                if (!double.IsFinite(radius) || radius <= 0) return fit;
                fit = fit with { CenterX = fit.CenterX + delta[0], CenterY = fit.CenterY + delta[1], Radius = radius };
                if (Math.Max(Math.Max(Math.Abs(delta[0]), Math.Abs(delta[1])), Math.Abs(delta[2])) <= parameters.ConvergenceTolerance) break;
            }
            return fit;
        }

        private static bool TrySolve3(double[,] matrix, double[] right, out double[] solution)
        {
            double[,] augmented = new double[3, 4];
            double maximum = 0;
            for (int row = 0; row < 3; row++)
            {
                for (int column = 0; column < 3; column++)
                {
                    augmented[row, column] = matrix[row, column];
                    maximum = Math.Max(maximum, Math.Abs(matrix[row, column]));
                }
                augmented[row, 3] = right[row];
            }
            if (!double.IsFinite(maximum) || maximum <= 0)
            {
                solution = [];
                return false;
            }

            for (int pivot = 0; pivot < 3; pivot++)
            {
                int best = pivot;
                for (int row = pivot + 1; row < 3; row++)
                    if (Math.Abs(augmented[row, pivot]) > Math.Abs(augmented[best, pivot])) best = row;
                if (Math.Abs(augmented[best, pivot]) <= maximum * 1e-12)
                {
                    solution = [];
                    return false;
                }
                if (best != pivot)
                {
                    for (int column = pivot; column < 4; column++)
                        (augmented[pivot, column], augmented[best, column]) = (augmented[best, column], augmented[pivot, column]);
                }
                double divisor = augmented[pivot, pivot];
                for (int column = pivot; column < 4; column++) augmented[pivot, column] /= divisor;
                for (int row = 0; row < 3; row++)
                {
                    if (row == pivot) continue;
                    double factor = augmented[row, pivot];
                    for (int column = pivot; column < 4; column++) augmented[row, column] -= factor * augmented[pivot, column];
                }
            }
            solution = [augmented[0, 3], augmented[1, 3], augmented[2, 3]];
            return solution.All(double.IsFinite);
        }

        private static double[] Residuals(AlgorithmPoint[] points, CircleState fit, CancellationToken cancellationToken)
        {
            double[] residuals = new double[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                double dx = points[index].X - fit.CenterX;
                double dy = points[index].Y - fit.CenterY;
                residuals[index] = Math.Sqrt(dx * dx + dy * dy) - fit.Radius;
            }
            return residuals;
        }

        private static double AngularCoverageDegrees(AlgorithmPoint[] points, CircleState fit, bool[] inliers)
        {
            double[] angles = points.Where((_, index) => inliers[index])
                .Select(point => Math.Atan2(point.Y - fit.CenterY, point.X - fit.CenterX))
                .OrderBy(value => value)
                .ToArray();
            if (angles.Length < 2) return 0;
            double maximumGap = 2 * Math.PI - angles[^1] + angles[0];
            for (int index = 1; index < angles.Length; index++) maximumGap = Math.Max(maximumGap, angles[index] - angles[index - 1]);
            return Math.Clamp((2 * Math.PI - maximumGap) * 180 / Math.PI, 0, 360);
        }

        private static double Median(double[] values)
        {
            Array.Sort(values);
            int middle = values.Length / 2;
            return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
        }

        private static string? ResolveRejection(CircleState? fit, CircleFitParameters parameters)
        {
            if (fit == null) return "degenerate_point_distribution";
            if (fit.InlierCount < parameters.MinimumInlierCount) return "insufficient_inliers";
            if (fit.Radius < parameters.MinimumRadiusPixels) return "radius_below_minimum";
            if (parameters.MaximumRadiusPixels != 0 && fit.Radius > parameters.MaximumRadiusPixels) return "radius_above_maximum";
            if (fit.AngularCoverageDegrees < parameters.MinimumAngularCoverageDegrees) return "angular_coverage_below_minimum";
            return null;
        }

        private static IReadOnlyList<AlgorithmArtifact> BuildArtifacts(
            AlgorithmExecutionContext context,
            AlgorithmImageBuffer image,
            PolylineAlgorithmRoi sourceRoi,
            CircleFitParameters parameters,
            AlgorithmPoint[] points,
            CircleState? fit,
            string? rejection,
            CancellationToken cancellationToken)
        {
            List<AlgorithmTableColumn> columns =
            [
                new("PointIndex", "integer"), new("X", "number", "px"), new("Y", "number", "px"),
                new("ProjectionX", "number", "px"), new("ProjectionY", "number", "px"),
                new("SignedRadialResidual", "number", "px"), new("AbsoluteResidual", "number", "px"),
                new("Accepted", "boolean"), new("RejectionReason", "string"),
            ];
            List<IReadOnlyDictionary<string, JsonElement>> rows = new(points.Length);
            List<AlgorithmGeometry> geometries = new(points.Length + 1);
            List<AlgorithmOverlayItem> overlays = new(Math.Min(points.Length, parameters.MaximumOverlayPoints) + 1);
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                AlgorithmPoint point = points[index];
                double? residual = fit?.Residuals.ElementAtOrDefault(index);
                bool accepted = rejection == null && fit!.Inliers[index];
                string? reason = rejection ?? (accepted ? null : "residual_above_threshold");
                double distance = fit == null ? double.NaN : Math.Sqrt(Math.Pow(point.X - fit.CenterX, 2) + Math.Pow(point.Y - fit.CenterY, 2));
                double projectionScale = fit == null || distance <= 1e-15 ? double.NaN : fit.Radius / distance;
                double projectionX = fit == null ? double.NaN : fit.CenterX + (point.X - fit.CenterX) * projectionScale;
                double projectionY = fit == null ? double.NaN : fit.CenterY + (point.Y - fit.CenterY) * projectionScale;
                rows.Add(Row(
                    ("PointIndex", index), ("X", point.X), ("Y", point.Y),
                    ("ProjectionX", projectionX), ("ProjectionY", projectionY),
                    ("SignedRadialResidual", residual), ("AbsoluteResidual", residual is double value ? Math.Abs(value) : null),
                    ("Accepted", accepted), ("RejectionReason", reason)));
                string pointId = $"point-{index}";
                geometries.Add(new AlgorithmGeometry(
                    pointId,
                    AlgorithmGeometryKind.Point,
                    [point],
                    Residual: residual is double absolute ? Math.Abs(absolute) : null,
                    Confidence: accepted && fit != null ? fit.Confidence : 0,
                    FilterReason: reason));
                if (index < parameters.MaximumOverlayPoints)
                    overlays.Add(new AlgorithmOverlayItem(pointId, new AlgorithmOverlayStyle(accepted ? "#FF36E36E" : "#FFFF7A33", null, 1.5, index.ToString())));
            }

            if (rejection == null && fit != null)
            {
                geometries.Insert(0, new AlgorithmGeometry(
                    "fitted-circle",
                    AlgorithmGeometryKind.Circle,
                    [new(fit.CenterX, fit.CenterY)],
                    Radius: fit.Radius,
                    Residual: fit.RootMeanSquareResidual,
                    Confidence: fit.Confidence,
                    Measurements: new Dictionary<string, double>
                    {
                        ["radius"] = fit.Radius,
                        ["maximumResidual"] = fit.MaximumResidual,
                        ["angularCoverageDegrees"] = fit.AngularCoverageDegrees,
                    }));
                overlays.Insert(0, new AlgorithmOverlayItem("fitted-circle", new AlgorithmOverlayStyle("#FF00B7FF", null, 2, "Fit")));
            }

            int acceptedPointCount = rejection == null ? fit!.InlierCount : 0;
            IReadOnlyList<AlgorithmMeasurement> measurements =
            [
                new("circle_fit.accepted", rejection == null ? 1 : 0, "boolean", Confidence: fit?.Confidence),
                new("circle_fit.point_count", points.Length, "point"),
                new("circle_fit.inlier_count", acceptedPointCount, "point"),
                new("circle_fit.rejected_count", points.Length - acceptedPointCount, "point"),
                new("circle_fit.center_x", fit?.CenterX ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("circle_fit.center_y", fit?.CenterY ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("circle_fit.radius", fit?.Radius ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("circle_fit.rms_residual", fit?.RootMeanSquareResidual ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("circle_fit.maximum_residual", fit?.MaximumResidual ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("circle_fit.angular_coverage", fit?.AngularCoverageDegrees ?? 0, "degree", Confidence: fit?.Confidence),
                new("circle_fit.confidence", fit?.Confidence ?? 0, "ratio"),
            ];
            JsonElement provenance = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Format.ToString(), image.DpiX, image.DpiY },
                sourcePoints = sourceRoi,
                parameters,
                accepted = rejection == null,
                rejectionReason = rejection,
                circle = fit == null ? null : new
                {
                    fit.CenterX, fit.CenterY, fit.Radius, fit.RootMeanSquareResidual,
                    fit.MaximumResidual, fit.AngularCoverageDegrees, fit.Confidence,
                },
                coordinateRule = "top-left-origin; integer coordinates are pixel centers; physical ROI points convert through source DPI",
                fitRule = parameters.Mode == CircleFitMode.LeastSquares
                    ? "normalized algebraic initialization followed by geometric least-squares refinement"
                    : "normalized algebraic Huber IRLS followed by residual-threshold geometric refinement",
                residualRule = "signed radial distance minus fitted radius in pixels; positive outside the circle",
                coverageRule = "360 degrees minus the largest angular gap between accepted points around the fitted center",
                confidenceRule = "coverage-fraction*inlier-fraction/(1+RMS/threshold); deterministic quality score, not probability",
                pixelAccess = "none; image metadata provides document dimensions and DPI only",
            });
            return
            [
                new AlgorithmMeasurementArtifact("circle-fit-summary", measurements),
                new AlgorithmTableArtifact("circle-fit-points", columns, rows),
                new AlgorithmGeometryArtifact("circle-fit-geometry", AlgorithmCoordinateSpace.Pixel, geometries),
                new AlgorithmOverlayArtifact("circle-fit-overlay", AlgorithmOverlayLifetime.Transient, overlays),
                new AlgorithmStructuredDataArtifact("circle-fit-provenance", ResultSchema, provenance),
            ];
        }

        private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(value => value.Name, value => AlgorithmJson.ToElement(value.Value), StringComparer.Ordinal);

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string? path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        private sealed record CircleState(
            double CenterX,
            double CenterY,
            double Radius,
            double[]? ResidualValues = null,
            bool[]? InlierValues = null,
            int InlierCount = 0,
            double RootMeanSquareResidual = double.NaN,
            double MaximumResidual = double.NaN,
            double AngularCoverageDegrees = 0,
            double Confidence = 0)
        {
            public double[] Residuals => ResidualValues ?? Array.Empty<double>();
            public bool[] Inliers => InlierValues ?? Array.Empty<bool>();
        }
    }
}
