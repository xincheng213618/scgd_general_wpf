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
    /// <summary>Deterministic CPU contour extraction with host-neutral geometry and measurements.</summary>
    public sealed class ContourAnalysisAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.analysis.contours/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.contours.cpu",
            "ColorVision Contours CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            109,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.Contours, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.Contours
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer image = context.Inputs[0].Image;
            ContourAnalysisParameters parameters = (ContourAnalysisParameters)context.Parameters;
            AlgorithmPixelRoi roi;
            if (context.Invocation.Roi == null)
            {
                roi = AlgorithmPixelRoi.WholeImage(image, "roi");
            }
            else if (context.Invocation.Roi is RectangleAlgorithmRoi or CircleAlgorithmRoi or PolygonAlgorithmRoi)
            {
                roi = AlgorithmPixelRoi.Create(context.Invocation.Roi, image);
            }
            else
            {
                return ValueTask.FromResult(Failure(context, "roi_unsupported", "Contour analysis accepts rectangle, circle or polygon ROI only.", "roi"));
            }
            if (roi.IsEmpty)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.03, "contour.mask", "Thresholding foreground mask"));
            byte[] maskData = new byte[checked(image.Width * image.Height)];
            BinaryAnalysisMaskSummary maskSummary = BinaryAnalysisMaskBuilder.Build(
                image,
                roi,
                parameters.Threshold,
                parameters.ForegroundPolarity == ContourForegroundPolarity.Bright,
                maskData,
                cancellationToken,
                value => context.Progress?.Report(new AlgorithmProgress(0.03 + 0.37 * value, "contour.mask")));
            if (maskSummary.RoiPixels == 0)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.43, "contour.extract", "Extracting contours"));
            using AlgorithmImageBuffer maskBuffer = new(image.Width, image.Height, image.Width, AlgorithmImageFormat.Gray8, maskData);
            using AlgorithmImageMatLease mask = AlgorithmImageInterop.BorrowReadOnly(maskBuffer);
            Cv2.FindContours(
                mask.Mat,
                out Point[][] extracted,
                out HierarchyIndex[] hierarchy,
                ResolveRetrievalMode(parameters.RetrievalMode),
                ResolveApproximationMode(parameters.ApproximationMode));
            cancellationToken.ThrowIfCancellationRequested();
            if (extracted.Length > parameters.MaximumCandidates)
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "contour_limit_exceeded",
                    $"Detected {extracted.Length} contours, exceeding MaximumCandidates={parameters.MaximumCandidates}.",
                    nameof(ContourAnalysisParameters.MaximumCandidates),
                    new Dictionary<string, string>
                    {
                        ["detected"] = extracted.Length.ToString(CultureInfo.InvariantCulture),
                        ["limit"] = parameters.MaximumCandidates.ToString(CultureInfo.InvariantCulture),
                    }));
            }

            context.Progress?.Report(new AlgorithmProgress(0.62, "contour.measure", "Simplifying and measuring contours"));
            List<ContourCandidate> candidates = new(extracted.Length);
            long totalPoints = 0;
            for (int index = 0; index < extracted.Length; index++)
            {
                if ((index & 127) == 0) cancellationToken.ThrowIfCancellationRequested();
                Point[] points = parameters.SimplificationEpsilon > 0
                    ? Cv2.ApproxPolyDP(extracted[index], parameters.SimplificationEpsilon, true)
                    : extracted[index];
                totalPoints = checked(totalPoints + points.Length);
                if (totalPoints > parameters.MaximumTotalPoints)
                {
                    return ValueTask.FromResult(Failure(
                        context,
                        "contour_point_limit_exceeded",
                        $"Extracted contours contain more than MaximumTotalPoints={parameters.MaximumTotalPoints} structured points.",
                        nameof(ContourAnalysisParameters.MaximumTotalPoints),
                        new Dictionary<string, string>
                        {
                            ["observedAtLeast"] = totalPoints.ToString(CultureInfo.InvariantCulture),
                            ["limit"] = parameters.MaximumTotalPoints.ToString(CultureInfo.InvariantCulture),
                        }));
                }
                HierarchyIndex topology = index < hierarchy.Length ? hierarchy[index] : new HierarchyIndex(-1, -1, -1, -1);
                candidates.Add(Measure(image, index, points, topology, parameters));
                context.Progress?.Report(new AlgorithmProgress(0.62 + 0.22 * (index + 1) / Math.Max(1, extracted.Length), "contour.measure"));
            }
            cancellationToken.ThrowIfCancellationRequested();

            IReadOnlyList<AlgorithmArtifact> artifacts = BuildArtifacts(context, image, roi, parameters, candidates, maskSummary, totalPoints);
            int acceptedCount = candidates.Count(candidate => candidate.Accepted);
            List<AlgorithmDiagnosticMessage> messages = new();
            if (roi.WasClipped)
                messages.Add(new AlgorithmDiagnosticMessage("roi_clipped", "The requested ROI was intersected with the image bounds."));
            if (acceptedCount > parameters.MaximumOverlayContours)
            {
                messages.Add(new AlgorithmDiagnosticMessage(
                    "contour_overlay_truncated",
                    $"Accepted {acceptedCount} contours; displayed {parameters.MaximumOverlayContours} by overlay limit.",
                    Data: new Dictionary<string, string>
                    {
                        ["accepted"] = acceptedCount.ToString(CultureInfo.InvariantCulture),
                        ["displayed"] = parameters.MaximumOverlayContours.ToString(CultureInfo.InvariantCulture),
                    }));
            }

            context.Progress?.Report(new AlgorithmProgress(1, "contour.complete"));
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

        private static ContourCandidate Measure(
            AlgorithmImageBuffer image,
            int index,
            Point[] points,
            HierarchyIndex hierarchy,
            ContourAnalysisParameters parameters)
        {
            double orientedArea = points.Length >= 3 ? Cv2.ContourArea(points, true) : 0;
            double area = Math.Abs(orientedArea);
            double perimeter = points.Length >= 2 ? Cv2.ArcLength(points, true) : 0;
            Rect bounds = points.Length > 0 ? Cv2.BoundingRect(points) : default;
            Moments? moments = points.Length >= 3 ? Cv2.Moments(points) : null;
            double centroidX;
            double centroidY;
            if (moments != null && Math.Abs(moments.M00) > double.Epsilon)
            {
                centroidX = moments.M10 / moments.M00;
                centroidY = moments.M01 / moments.M00;
            }
            else if (points.Length > 0)
            {
                centroidX = points.Average(point => (double)point.X);
                centroidY = points.Average(point => (double)point.Y);
            }
            else
            {
                centroidX = double.NaN;
                centroidY = double.NaN;
            }
            double circularity = perimeter > 0 ? Math.Clamp(4 * Math.PI * area / (perimeter * perimeter), 0, 1) : 0;
            Point[] hull = points.Length >= 3 ? Cv2.ConvexHull(points) : [];
            double hullArea = hull.Length >= 3 ? Math.Abs(Cv2.ContourArea(hull)) : 0;
            double solidity = hullArea > 0 ? Math.Clamp(area / hullArea, 0, 1) : 0;
            double fillRatio = bounds.Width > 0 && bounds.Height > 0 ? Math.Clamp(area / checked((double)bounds.Width * bounds.Height), 0, 1) : 0;
            bool touchesImageBorder = bounds.X <= 0 || bounds.Y <= 0 || bounds.Right >= image.Width || bounds.Bottom >= image.Height;

            List<string> reasons = new();
            if (area < parameters.MinimumArea) reasons.Add("area_below_minimum");
            if (parameters.MaximumArea != 0 && area > parameters.MaximumArea) reasons.Add("area_above_maximum");
            if (perimeter < parameters.MinimumPerimeter) reasons.Add("perimeter_below_minimum");
            if (parameters.MaximumPerimeter != 0 && perimeter > parameters.MaximumPerimeter) reasons.Add("perimeter_above_maximum");
            if (points.Length < parameters.MinimumPointCount) reasons.Add("point_count_below_minimum");
            if (parameters.MaximumPointCount != 0 && points.Length > parameters.MaximumPointCount) reasons.Add("point_count_above_maximum");
            if (circularity < parameters.MinimumCircularity) reasons.Add("circularity_below_minimum");
            if (solidity < parameters.MinimumSolidity) reasons.Add("solidity_below_minimum");
            if (parameters.ExcludeImageBorder && touchesImageBorder) reasons.Add("touches_image_border");
            return new ContourCandidate(
                index,
                points,
                hierarchy.Next,
                hierarchy.Previous,
                hierarchy.Child,
                hierarchy.Parent,
                area,
                orientedArea,
                perimeter,
                bounds,
                centroidX,
                centroidY,
                circularity,
                solidity,
                fillRatio,
                touchesImageBorder,
                reasons.Count == 0,
                reasons.Count == 0 ? null : string.Join(";", reasons));
        }

        private static IReadOnlyList<AlgorithmArtifact> BuildArtifacts(
            AlgorithmExecutionContext context,
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            ContourAnalysisParameters parameters,
            IReadOnlyList<ContourCandidate> candidates,
            BinaryAnalysisMaskSummary maskSummary,
            long totalPoints)
        {
            ContourCandidate[] accepted = candidates.Where(candidate => candidate.Accepted).ToArray();
            List<AlgorithmMeasurement> measurements =
            [
                new("contour.roi_pixel_count", maskSummary.RoiPixels, "px"),
                new("contour.foreground_pixel_count", maskSummary.ForegroundPixels, "px"),
                new("contour.invalid_pixel_count", maskSummary.InvalidPixels, "px"),
                new("contour.candidate_count", candidates.Count, "contour"),
                new("contour.accepted_count", accepted.Length, "contour"),
                new("contour.rejected_count", candidates.Count - accepted.Length, "contour"),
                new("contour.structured_point_count", totalPoints, "point"),
                new("contour.accepted_area", accepted.Sum(candidate => candidate.Area), "px²"),
                new("contour.accepted_perimeter", accepted.Sum(candidate => candidate.Perimeter), "px"),
            ];
            List<AlgorithmTableColumn> columns =
            [
                new("Index", "integer"), new("Accepted", "boolean"), new("FilterReason", "string"),
                new("Parent", "integer"), new("Child", "integer"), new("Next", "integer"), new("Previous", "integer"),
                new("Area", "number", "px²"), new("OrientedArea", "number", "px²"), new("Perimeter", "number", "px"),
                new("PointCount", "integer", "point"), new("Left", "integer", "px"), new("Top", "integer", "px"),
                new("Width", "integer", "px"), new("Height", "integer", "px"),
                new("CentroidX", "number", "px"), new("CentroidY", "number", "px"),
                new("Circularity", "number", "ratio"), new("Solidity", "number", "ratio"),
                new("FillRatio", "number", "ratio"), new("TouchesImageBorder", "boolean"),
            ];
            List<IReadOnlyDictionary<string, JsonElement>> rows = candidates.Select(candidate =>
                (IReadOnlyDictionary<string, JsonElement>)Row(
                    ("Index", candidate.Index), ("Accepted", candidate.Accepted), ("FilterReason", candidate.FilterReason),
                    ("Parent", candidate.Parent), ("Child", candidate.Child), ("Next", candidate.Next), ("Previous", candidate.Previous),
                    ("Area", candidate.Area), ("OrientedArea", candidate.OrientedArea), ("Perimeter", candidate.Perimeter),
                    ("PointCount", candidate.Points.Length), ("Left", candidate.Bounds.X), ("Top", candidate.Bounds.Y),
                    ("Width", candidate.Bounds.Width), ("Height", candidate.Bounds.Height),
                    ("CentroidX", candidate.CentroidX), ("CentroidY", candidate.CentroidY),
                    ("Circularity", candidate.Circularity), ("Solidity", candidate.Solidity),
                    ("FillRatio", candidate.FillRatio), ("TouchesImageBorder", candidate.TouchesImageBorder)))
                .ToList();

            List<AlgorithmGeometry> geometries = [roi.Geometry];
            geometries.AddRange(candidates.Select(candidate => new AlgorithmGeometry(
                $"contour-{candidate.Index}",
                ResolveGeometryKind(candidate.Points.Length),
                candidate.Points.Select(point => new AlgorithmPoint(point.X, point.Y)).ToArray(),
                Confidence: candidate.Solidity,
                FilterReason: candidate.FilterReason,
                Measurements: new Dictionary<string, double>
                {
                    ["index"] = candidate.Index,
                    ["area"] = candidate.Area,
                    ["orientedArea"] = candidate.OrientedArea,
                    ["perimeter"] = candidate.Perimeter,
                    ["pointCount"] = candidate.Points.Length,
                    ["centroidX"] = candidate.CentroidX,
                    ["centroidY"] = candidate.CentroidY,
                    ["circularity"] = candidate.Circularity,
                    ["solidity"] = candidate.Solidity,
                    ["fillRatio"] = candidate.FillRatio,
                    ["parent"] = candidate.Parent,
                    ["child"] = candidate.Child,
                })));
            List<AlgorithmOverlayItem> overlays = [new("roi", new AlgorithmOverlayStyle("#FFFFA500", "#08FFA500", 1.25, "ROI"))];
            overlays.AddRange(accepted.Take(parameters.MaximumOverlayContours).Select(candidate =>
                new AlgorithmOverlayItem(
                    $"contour-{candidate.Index}",
                    new AlgorithmOverlayStyle("#FF00E5FF", null, 1.5, $"#{candidate.Index} A={candidate.Area:G5}"))));

            JsonElement structured = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Format.ToString(), image.DpiX, image.DpiY },
                roi = context.Invocation.Roi,
                roiPixelBounds = new { roi.MinimumX, roi.MinimumY, roi.MaximumXExclusive, roi.MaximumYExclusive, roi.WasClipped },
                parameters,
                threshold = new
                {
                    nominalScale = "0..255",
                    comparison = parameters.ForegroundPolarity == ContourForegroundPolarity.Bright ? "intensity >= threshold" : "intensity <= threshold",
                    colorIntensity = "0.114*B + 0.587*G + 0.299*R; alpha ignored",
                    floatingPointNominalRange = "0..1",
                    invalidPixels = "NaN or Infinity are background and counted",
                },
                geometryCoordinates = "full-image pixel coordinates at boundary pixel centers; contour closure is implicit",
                measurements = "area/perimeter/centroid describe the emitted contour after optional simplification",
                confidence = "contour solidity (area / convex-hull area), not classification probability",
                hierarchy = "OpenCV next/previous/child/parent indices; -1 means none",
                counts = new
                {
                    roiPixels = maskSummary.RoiPixels,
                    foregroundPixels = maskSummary.ForegroundPixels,
                    invalidPixels = maskSummary.InvalidPixels,
                    candidates = candidates.Count,
                    accepted = accepted.Length,
                    totalPoints,
                },
            });

            return
            [
                new AlgorithmMeasurementArtifact("contour-summary", measurements),
                new AlgorithmTableArtifact("contours", columns, rows),
                new AlgorithmGeometryArtifact("contour-geometry", AlgorithmCoordinateSpace.Pixel, geometries),
                new AlgorithmOverlayArtifact("contour-overlay", AlgorithmOverlayLifetime.Transient, overlays),
                new AlgorithmStructuredDataArtifact("contour-provenance", ResultSchema, structured),
            ];
        }

        private static RetrievalModes ResolveRetrievalMode(ContourRetrievalMode mode) => mode switch
        {
            ContourRetrievalMode.External => RetrievalModes.External,
            ContourRetrievalMode.List => RetrievalModes.List,
            ContourRetrievalMode.Tree => RetrievalModes.Tree,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        private static ContourApproximationModes ResolveApproximationMode(ContourApproximationMode mode) => mode switch
        {
            ContourApproximationMode.None => ContourApproximationModes.ApproxNone,
            ContourApproximationMode.Simple => ContourApproximationModes.ApproxSimple,
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };

        private static AlgorithmGeometryKind ResolveGeometryKind(int pointCount) => pointCount switch
        {
            <= 1 => AlgorithmGeometryKind.Point,
            2 => AlgorithmGeometryKind.Line,
            _ => AlgorithmGeometryKind.Polygon,
        };

        private static AlgorithmResult Failure(
            AlgorithmExecutionContext context,
            string code,
            string message,
            string path,
            IReadOnlyDictionary<string, string>? details = null)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path, details)],
            };

        private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(value => value.Name, value => AlgorithmJson.ToElement(value.Value), StringComparer.Ordinal);

        private sealed record ContourCandidate(
            int Index,
            Point[] Points,
            int Next,
            int Previous,
            int Child,
            int Parent,
            double Area,
            double OrientedArea,
            double Perimeter,
            Rect Bounds,
            double CentroidX,
            double CentroidY,
            double Circularity,
            double Solidity,
            double FillRatio,
            bool TouchesImageBorder,
            bool Accepted,
            string? FilterReason);
    }
}
