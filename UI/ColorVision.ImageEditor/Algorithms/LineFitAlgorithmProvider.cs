using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic total-least-squares/Huber line fit over a host-neutral polyline point set.</summary>
    public sealed class LineFitAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.measurement.line-fit/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.line-fit.cpu",
            "ColorVision Line Fit CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            113,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.LineFit, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.LineFit
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Invocation.Roi is not PolylineAlgorithmRoi path)
                return ValueTask.FromResult(Failure(context, "line_fit_points_required", "Line fit requires a polyline ROI whose vertices are the input point set.", "roi"));

            AlgorithmImageBuffer image = context.Inputs[0].Image;
            LineFitParameters parameters = (LineFitParameters)context.Parameters;
            AlgorithmPoint[] points = path.Points
                .Select(point => AlgorithmCoordinates.ToPixel(point, path.CoordinateSpace, image.DpiX, image.DpiY))
                .ToArray();
            if (points.Length > parameters.MaximumPoints)
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "line_fit_point_limit_exceeded",
                    $"The ROI contains {points.Length} points, exceeding MaximumPoints={parameters.MaximumPoints}.",
                    nameof(parameters.MaximumPoints)));
            }

            context.Progress?.Report(new AlgorithmProgress(0.05, "line-fit.solve", "Fitting line"));
            FitState? fit = Fit(points, parameters, cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            context.Progress?.Report(new AlgorithmProgress(0.72, "line-fit.artifacts", "Building line artifacts"));

            string? fitRejection = fit == null ? "degenerate_point_distribution"
                : fit.InlierCount < parameters.MinimumInlierCount ? "insufficient_inliers"
                : null;
            if (fitRejection != null && fit != null) fit = fit with { Accepted = false };
            IReadOnlyList<AlgorithmArtifact> artifacts = BuildArtifacts(context, image, path, parameters, points, fit, fitRejection, cancellationToken);
            List<AlgorithmDiagnosticMessage> diagnostics = new();
            if (fitRejection != null)
                diagnostics.Add(new AlgorithmDiagnosticMessage("line_fit_rejected", $"The line fit was rejected: {fitRejection}.", "warning"));
            else if (fit!.InlierCount != points.Length)
                diagnostics.Add(new AlgorithmDiagnosticMessage("line_fit_points_rejected", $"Rejected {points.Length - fit.InlierCount} of {points.Length} points by residual threshold.", "warning"));

            context.Progress?.Report(new AlgorithmProgress(1, "line-fit.complete"));
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

        private static FitState? Fit(AlgorithmPoint[] points, LineFitParameters parameters, CancellationToken cancellationToken)
        {
            double[] weights = Enumerable.Repeat(1d, points.Length).ToArray();
            FitState? fit = FitTls(points, weights, cancellationToken);
            if (fit == null) return null;

            if (parameters.Mode == LineFitMode.RobustHuber)
            {
                for (int iteration = 0; iteration < parameters.MaximumIterations; iteration++)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    double[] absoluteResiduals = Residuals(points, fit, cancellationToken).Select(Math.Abs).ToArray();
                    double scale = Math.Max(1e-12, 1.4826 * Median(absoluteResiduals));
                    double cutoff = parameters.HuberTuningConstant * scale;
                    for (int index = 0; index < weights.Length; index++)
                        weights[index] = absoluteResiduals[index] <= cutoff ? 1 : cutoff / absoluteResiduals[index];
                    FitState? next = FitTls(points, weights, cancellationToken);
                    if (next == null) return null;
                    double directionDelta = 1 - Math.Abs(fit.DirectionX * next.DirectionX + fit.DirectionY * next.DirectionY);
                    double offsetDelta = Math.Abs(fit.C - next.C);
                    fit = next;
                    if (directionDelta <= parameters.ConvergenceTolerance && offsetDelta <= parameters.ConvergenceTolerance) break;
                }
            }

            bool[] previousInliers = new bool[points.Length];
            for (int pass = 0; pass < 4; pass++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                double[] residuals = Residuals(points, fit, cancellationToken);
                bool[] inliers = residuals.Select(value => Math.Abs(value) <= parameters.ResidualThresholdPixels).ToArray();
                int count = inliers.Count(value => value);
                fit = fit with { ResidualValues = residuals, InlierValues = inliers, InlierCount = count };
                if (count < 2 || inliers.SequenceEqual(previousInliers)) break;
                previousInliers = inliers;
                double[] inlierWeights = inliers.Select(value => value ? 1d : 0d).ToArray();
                FitState? refined = FitTls(points, inlierWeights, cancellationToken);
                if (refined == null) break;
                fit = refined;
            }

            double[] finalResiduals = Residuals(points, fit, cancellationToken);
            bool[] finalInliers = finalResiduals.Select(value => Math.Abs(value) <= parameters.ResidualThresholdPixels).ToArray();
            int finalCount = finalInliers.Count(value => value);
            double rms = finalCount == 0 ? double.NaN : Math.Sqrt(finalResiduals.Where((_, index) => finalInliers[index]).Average(value => value * value));
            double maximum = finalCount == 0 ? double.NaN : finalResiduals.Where((_, index) => finalInliers[index]).Max(value => Math.Abs(value));
            double confidence = finalCount == 0 ? 0
                : (double)finalCount / points.Length / (1 + rms / parameters.ResidualThresholdPixels) * fit.Linearity;
            return fit with
            {
                Accepted = finalCount >= parameters.MinimumInlierCount,
                ResidualValues = finalResiduals,
                InlierValues = finalInliers,
                InlierCount = finalCount,
                RootMeanSquareResidual = rms,
                MaximumResidual = maximum,
                Confidence = Math.Clamp(confidence, 0, 1),
            };
        }

        private static FitState? FitTls(AlgorithmPoint[] points, double[] weights, CancellationToken cancellationToken)
        {
            double sumWeight = 0;
            double centerX = 0;
            double centerY = 0;
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                double weight = weights[index];
                sumWeight += weight;
                centerX += weight * points[index].X;
                centerY += weight * points[index].Y;
            }
            if (sumWeight <= 0) return null;
            centerX /= sumWeight;
            centerY /= sumWeight;

            double xx = 0;
            double xy = 0;
            double yy = 0;
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                double dx = points[index].X - centerX;
                double dy = points[index].Y - centerY;
                double weight = weights[index];
                xx += weight * dx * dx;
                xy += weight * dx * dy;
                yy += weight * dy * dy;
            }
            double trace = xx + yy;
            if (trace <= 1e-24) return null;
            double eigenvalueSpread = Math.Sqrt((xx - yy) * (xx - yy) + 4 * xy * xy);
            double linearity = eigenvalueSpread / trace;
            if (linearity <= 1e-12) return null;

            double angle = 0.5 * Math.Atan2(2 * xy, xx - yy);
            double directionX = Math.Cos(angle);
            double directionY = Math.Sin(angle);
            if (directionX < 0 || (Math.Abs(directionX) <= 1e-15 && directionY < 0))
            {
                directionX = -directionX;
                directionY = -directionY;
            }
            double normalX = -directionY;
            double normalY = directionX;
            double c = -(normalX * centerX + normalY * centerY);
            return new FitState(centerX, centerY, directionX, directionY, normalX, normalY, c, linearity);
        }

        private static double[] Residuals(AlgorithmPoint[] points, FitState fit, CancellationToken cancellationToken)
        {
            double[] residuals = new double[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 1023) == 0) cancellationToken.ThrowIfCancellationRequested();
                residuals[index] = fit.NormalX * points[index].X + fit.NormalY * points[index].Y + fit.C;
            }
            return residuals;
        }

        private static double Median(double[] values)
        {
            Array.Sort(values);
            int middle = values.Length / 2;
            return values.Length % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
        }

        private static IReadOnlyList<AlgorithmArtifact> BuildArtifacts(
            AlgorithmExecutionContext context,
            AlgorithmImageBuffer image,
            PolylineAlgorithmRoi sourceRoi,
            LineFitParameters parameters,
            AlgorithmPoint[] points,
            FitState? fit,
            string? fitRejection,
            CancellationToken cancellationToken)
        {
            List<AlgorithmTableColumn> columns =
            [
                new("PointIndex", "integer"), new("X", "number", "px"), new("Y", "number", "px"),
                new("ProjectionX", "number", "px"), new("ProjectionY", "number", "px"),
                new("SignedResidual", "number", "px"), new("AbsoluteResidual", "number", "px"),
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
                bool accepted = fitRejection == null && fit!.Inliers[index];
                string? reason = fitRejection ?? (accepted ? null : "residual_above_threshold");
                double projectionX = residual is double signed ? point.X - signed * fit!.NormalX : double.NaN;
                double projectionY = residual is double signedY ? point.Y - signedY * fit!.NormalY : double.NaN;
                rows.Add(Row(
                    ("PointIndex", index), ("X", point.X), ("Y", point.Y),
                    ("ProjectionX", projectionX), ("ProjectionY", projectionY),
                    ("SignedResidual", residual), ("AbsoluteResidual", residual is double value ? Math.Abs(value) : null),
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

            AlgorithmPoint[]? endpoints = null;
            if (fitRejection == null && fit != null)
            {
                endpoints = parameters.OutputExtent == LineFitOutputExtent.ImageBounds
                    ? ClipToImage(fit, image.Width, image.Height)
                    : InlierSpan(points, fit);
                if (endpoints[0] == endpoints[1]) endpoints = InlierSpan(points, fit);
                geometries.Insert(0, new AlgorithmGeometry(
                    "fitted-line",
                    AlgorithmGeometryKind.Line,
                    endpoints,
                    Residual: fit.RootMeanSquareResidual,
                    Confidence: fit.Confidence,
                    Measurements: new Dictionary<string, double>
                    {
                        ["angleDegrees"] = Math.Atan2(fit.DirectionY, fit.DirectionX) * 180 / Math.PI,
                        ["normalX"] = fit.NormalX,
                        ["normalY"] = fit.NormalY,
                        ["c"] = fit.C,
                        ["linearity"] = fit.Linearity,
                        ["maximumResidual"] = fit.MaximumResidual,
                    }));
                overlays.Insert(0, new AlgorithmOverlayItem("fitted-line", new AlgorithmOverlayStyle("#FF00B7FF", null, 2, "Fit")));
            }

            double angleDegrees = fit == null ? double.NaN : Math.Atan2(fit.DirectionY, fit.DirectionX) * 180 / Math.PI;
            int acceptedPointCount = fitRejection == null ? fit!.InlierCount : 0;
            IReadOnlyList<AlgorithmMeasurement> measurements =
            [
                new("line_fit.accepted", fitRejection == null ? 1 : 0, "boolean", Confidence: fit?.Confidence),
                new("line_fit.point_count", points.Length, "point"),
                new("line_fit.inlier_count", acceptedPointCount, "point"),
                new("line_fit.rejected_count", points.Length - acceptedPointCount, "point"),
                new("line_fit.angle_degrees", angleDegrees, "degree", Confidence: fit?.Confidence),
                new("line_fit.direction_x", fit?.DirectionX ?? double.NaN),
                new("line_fit.direction_y", fit?.DirectionY ?? double.NaN),
                new("line_fit.normal_x", fit?.NormalX ?? double.NaN),
                new("line_fit.normal_y", fit?.NormalY ?? double.NaN),
                new("line_fit.c", fit?.C ?? double.NaN, "px"),
                new("line_fit.linearity", fit?.Linearity ?? 0, "ratio"),
                new("line_fit.rms_residual", fit?.RootMeanSquareResidual ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("line_fit.maximum_residual", fit?.MaximumResidual ?? double.NaN, "px", Confidence: fit?.Confidence),
                new("line_fit.confidence", fit?.Confidence ?? 0, "ratio"),
            ];
            JsonElement provenance = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Format.ToString(), image.DpiX, image.DpiY },
                sourcePoints = sourceRoi,
                parameters,
                accepted = fitRejection == null,
                rejectionReason = fitRejection,
                line = fit == null ? null : new
                {
                    fit.CenterX, fit.CenterY, fit.DirectionX, fit.DirectionY,
                    fit.NormalX, fit.NormalY, fit.C, fit.Linearity, angleDegrees,
                    fit.RootMeanSquareResidual, fit.MaximumResidual, fit.Confidence,
                    endpoints,
                },
                coordinateRule = "top-left-origin; integer coordinates are pixel centers; physical ROI points convert through source DPI",
                fitRule = parameters.Mode == LineFitMode.TotalLeastSquares
                    ? "orthogonal total least squares followed by residual-threshold refit"
                    : "deterministic Huber IRLS orthogonal fit followed by residual-threshold refit",
                residualRule = "signed perpendicular Euclidean distance in pixels; positive along the reported unit normal",
                confidenceRule = "linearity*inlier-fraction/(1+RMS/threshold); deterministic quality score, not probability",
                pixelAccess = "none; image metadata provides document dimensions and DPI only",
            });
            return
            [
                new AlgorithmMeasurementArtifact("line-fit-summary", measurements),
                new AlgorithmTableArtifact("line-fit-points", columns, rows),
                new AlgorithmGeometryArtifact("line-fit-geometry", AlgorithmCoordinateSpace.Pixel, geometries),
                new AlgorithmOverlayArtifact("line-fit-overlay", AlgorithmOverlayLifetime.Transient, overlays),
                new AlgorithmStructuredDataArtifact("line-fit-provenance", ResultSchema, provenance),
            ];
        }

        private static AlgorithmPoint[] InlierSpan(AlgorithmPoint[] points, FitState fit)
        {
            double[] projections = points.Where((_, index) => fit.Inliers[index])
                .Select(point => (point.X - fit.CenterX) * fit.DirectionX + (point.Y - fit.CenterY) * fit.DirectionY)
                .ToArray();
            if (projections.Length == 0) return [new(fit.CenterX, fit.CenterY), new(fit.CenterX, fit.CenterY)];
            return [At(projections.Min()), At(projections.Max())];

            AlgorithmPoint At(double distance) => new(fit.CenterX + distance * fit.DirectionX, fit.CenterY + distance * fit.DirectionY);
        }

        private static AlgorithmPoint[] ClipToImage(FitState fit, int width, int height)
        {
            List<(double T, AlgorithmPoint Point)> candidates = new(4);
            AddForX(0);
            AddForX(width - 1);
            AddForY(0);
            AddForY(height - 1);
            (double T, AlgorithmPoint Point)[] distinct = candidates
                .GroupBy(item => (Math.Round(item.Point.X, 9), Math.Round(item.Point.Y, 9)))
                .Select(group => group.First())
                .OrderBy(item => item.T)
                .ToArray();
            if (distinct.Length >= 2) return [distinct[0].Point, distinct[^1].Point];
            return [new(fit.CenterX, fit.CenterY), new(fit.CenterX, fit.CenterY)];

            void AddForX(double x)
            {
                if (Math.Abs(fit.DirectionX) <= 1e-15) return;
                double t = (x - fit.CenterX) / fit.DirectionX;
                double y = fit.CenterY + t * fit.DirectionY;
                if (y >= -1e-9 && y <= height - 1 + 1e-9) candidates.Add((t, new(x, Math.Clamp(y, 0, height - 1))));
            }

            void AddForY(double y)
            {
                if (Math.Abs(fit.DirectionY) <= 1e-15) return;
                double t = (y - fit.CenterY) / fit.DirectionY;
                double x = fit.CenterX + t * fit.DirectionX;
                if (x >= -1e-9 && x <= width - 1 + 1e-9) candidates.Add((t, new(Math.Clamp(x, 0, width - 1), y)));
            }
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

        private sealed record FitState(
            double CenterX,
            double CenterY,
            double DirectionX,
            double DirectionY,
            double NormalX,
            double NormalY,
            double C,
            double Linearity,
            bool Accepted = false,
            double[]? ResidualValues = null,
            bool[]? InlierValues = null,
            int InlierCount = 0,
            double RootMeanSquareResidual = double.NaN,
            double MaximumResidual = double.NaN,
            double Confidence = 0)
        {
            public double[] Residuals => ResidualValues ?? Array.Empty<double>();
            public bool[] Inliers => InlierValues ?? Array.Empty<bool>();
        }
    }
}
