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
    /// <summary>Deterministic CPU connected-component analysis with host-neutral structured outputs.</summary>
    public sealed class BlobAnalysisAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.analysis.blob-components/v1";
        private static readonly HashSet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.blob-components.cpu",
            "ColorVision Blob Components CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            110,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.BlobComponents, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.BlobComponents
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            AlgorithmImageBuffer image = context.Inputs[0].Image;
            BlobAnalysisParameters parameters = (BlobAnalysisParameters)context.Parameters;
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
                return ValueTask.FromResult(Failure(context, "roi_unsupported", "Blob analysis accepts rectangle, circle or polygon ROI only.", "roi"));
            }
            if (roi.IsEmpty)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.03, "blob.mask", "Thresholding foreground mask"));
            byte[] maskData = new byte[checked(image.Width * image.Height)];
            BinaryAnalysisMaskSummary maskSummary = BinaryAnalysisMaskBuilder.Build(
                image,
                roi,
                parameters.Threshold,
                parameters.ForegroundPolarity == BlobForegroundPolarity.Bright,
                maskData,
                cancellationToken,
                value => context.Progress?.Report(new AlgorithmProgress(0.03 + 0.40 * value, "blob.mask")));
            (long roiPixels, long foregroundPixels, long invalidPixels) = maskSummary;
            if (roiPixels == 0)
                return ValueTask.FromResult(Failure(context, "roi_empty_after_clip", "The ROI does not contain any image pixel centers after clipping.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.48, "blob.connected-components", "Labeling connected components"));
            using AlgorithmImageBuffer maskBuffer = new(image.Width, image.Height, image.Width, AlgorithmImageFormat.Gray8, maskData);
            using AlgorithmImageMatLease mask = AlgorithmImageInterop.BorrowReadOnly(maskBuffer);
            using Mat labels = new();
            using Mat stats = new();
            using Mat centroids = new();
            PixelConnectivity connectivity = parameters.Connectivity == BlobConnectivity.Four
                ? PixelConnectivity.Connectivity4
                : PixelConnectivity.Connectivity8;
            int labelCount = Cv2.ConnectedComponentsWithStats(mask.Mat, labels, stats, centroids, connectivity, MatType.CV_32S);
            cancellationToken.ThrowIfCancellationRequested();
            int candidateCount = Math.Max(0, labelCount - 1);
            if (candidateCount > parameters.MaximumCandidates)
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "component_limit_exceeded",
                    $"Detected {candidateCount} components, exceeding MaximumCandidates={parameters.MaximumCandidates}.",
                    nameof(BlobAnalysisParameters.MaximumCandidates),
                    new Dictionary<string, string>
                    {
                        ["detected"] = candidateCount.ToString(CultureInfo.InvariantCulture),
                        ["limit"] = parameters.MaximumCandidates.ToString(CultureInfo.InvariantCulture),
                    }));
            }

            context.Progress?.Report(new AlgorithmProgress(0.70, "blob.artifacts", "Filtering components and building artifacts"));
            List<BlobComponent> components = ReadComponents(image, stats, centroids, parameters, labelCount, cancellationToken);
            IReadOnlyList<AlgorithmArtifact> artifacts = BuildArtifacts(
                context,
                image,
                roi,
                parameters,
                components,
                roiPixels,
                foregroundPixels,
                invalidPixels);
            int acceptedCount = components.Count(component => component.Accepted);
            List<AlgorithmDiagnosticMessage> messages = new();
            if (roi.WasClipped)
                messages.Add(new AlgorithmDiagnosticMessage("roi_clipped", "The requested ROI was intersected with the image bounds."));
            if (acceptedCount > parameters.MaximumOverlayComponents)
            {
                messages.Add(new AlgorithmDiagnosticMessage(
                    "blob_overlay_truncated",
                    $"Accepted {acceptedCount} components; displayed {parameters.MaximumOverlayComponents} by overlay limit.",
                    Data: new Dictionary<string, string>
                    {
                        ["accepted"] = acceptedCount.ToString(CultureInfo.InvariantCulture),
                        ["displayed"] = parameters.MaximumOverlayComponents.ToString(CultureInfo.InvariantCulture),
                    }));
            }

            context.Progress?.Report(new AlgorithmProgress(1, "blob.complete"));
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

        private static List<BlobComponent> ReadComponents(
            AlgorithmImageBuffer image,
            Mat stats,
            Mat centroids,
            BlobAnalysisParameters parameters,
            int labelCount,
            CancellationToken cancellationToken)
        {
            List<BlobComponent> components = new(Math.Max(0, labelCount - 1));
            for (int label = 1; label < labelCount; label++)
            {
                if ((label & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                int left = stats.At<int>(label, (int)ConnectedComponentsTypes.Left);
                int top = stats.At<int>(label, (int)ConnectedComponentsTypes.Top);
                int width = stats.At<int>(label, (int)ConnectedComponentsTypes.Width);
                int height = stats.At<int>(label, (int)ConnectedComponentsTypes.Height);
                int area = stats.At<int>(label, (int)ConnectedComponentsTypes.Area);
                double centroidX = centroids.At<double>(label, 0);
                double centroidY = centroids.At<double>(label, 1);
                bool touchesImageBorder = left == 0 || top == 0 || checked(left + width) == image.Width || checked(top + height) == image.Height;
                List<string> reasons = new();
                if (area < parameters.MinimumArea) reasons.Add("area_below_minimum");
                if (parameters.MaximumArea != 0 && area > parameters.MaximumArea) reasons.Add("area_above_maximum");
                if (width < parameters.MinimumWidth) reasons.Add("width_below_minimum");
                if (parameters.MaximumWidth != 0 && width > parameters.MaximumWidth) reasons.Add("width_above_maximum");
                if (height < parameters.MinimumHeight) reasons.Add("height_below_minimum");
                if (parameters.MaximumHeight != 0 && height > parameters.MaximumHeight) reasons.Add("height_above_maximum");
                if (parameters.ExcludeImageBorder && touchesImageBorder) reasons.Add("touches_image_border");
                double fillRatio = area / (double)checked(width * height);
                components.Add(new BlobComponent(
                    label,
                    left,
                    top,
                    width,
                    height,
                    area,
                    centroidX,
                    centroidY,
                    fillRatio,
                    touchesImageBorder,
                    reasons.Count == 0,
                    reasons.Count == 0 ? null : string.Join(";", reasons)));
            }
            return components;
        }

        private static IReadOnlyList<AlgorithmArtifact> BuildArtifacts(
            AlgorithmExecutionContext context,
            AlgorithmImageBuffer image,
            AlgorithmPixelRoi roi,
            BlobAnalysisParameters parameters,
            IReadOnlyList<BlobComponent> components,
            long roiPixels,
            long foregroundPixels,
            long invalidPixels)
        {
            BlobComponent[] accepted = components.Where(component => component.Accepted).ToArray();
            List<AlgorithmMeasurement> measurements =
            [
                new("blob.roi_pixel_count", roiPixels, "px"),
                new("blob.foreground_pixel_count", foregroundPixels, "px"),
                new("blob.invalid_pixel_count", invalidPixels, "px"),
                new("blob.candidate_count", components.Count, "component"),
                new("blob.accepted_count", accepted.Length, "component"),
                new("blob.rejected_count", components.Count - accepted.Length, "component"),
                new("blob.accepted_area", accepted.Sum(component => (long)component.Area), "px"),
            ];
            List<AlgorithmTableColumn> columns =
            [
                new("Label", "integer"), new("Accepted", "boolean"), new("FilterReason", "string"),
                new("Area", "integer", "px"), new("Left", "integer", "px"), new("Top", "integer", "px"),
                new("Width", "integer", "px"), new("Height", "integer", "px"),
                new("CentroidX", "number", "px"), new("CentroidY", "number", "px"),
                new("FillRatio", "number", "ratio"), new("TouchesImageBorder", "boolean"),
            ];
            List<IReadOnlyDictionary<string, JsonElement>> rows = components.Select(component =>
                (IReadOnlyDictionary<string, JsonElement>)Row(
                    ("Label", component.Label), ("Accepted", component.Accepted), ("FilterReason", component.FilterReason),
                    ("Area", component.Area), ("Left", component.Left), ("Top", component.Top),
                    ("Width", component.Width), ("Height", component.Height),
                    ("CentroidX", component.CentroidX), ("CentroidY", component.CentroidY),
                    ("FillRatio", component.FillRatio), ("TouchesImageBorder", component.TouchesImageBorder)))
                .ToList();

            List<AlgorithmGeometry> geometries = [roi.Geometry];
            geometries.AddRange(components.Select(component => new AlgorithmGeometry(
                $"blob-{component.Label}",
                AlgorithmGeometryKind.Rectangle,
                [new(component.Left, component.Top), new(component.Left + component.Width, component.Top + component.Height)],
                Confidence: component.FillRatio,
                FilterReason: component.FilterReason,
                Measurements: new Dictionary<string, double>
                {
                    ["label"] = component.Label,
                    ["area"] = component.Area,
                    ["centroidX"] = component.CentroidX,
                    ["centroidY"] = component.CentroidY,
                    ["width"] = component.Width,
                    ["height"] = component.Height,
                    ["fillRatio"] = component.FillRatio,
                })));
            List<AlgorithmOverlayItem> overlays = [new("roi", new AlgorithmOverlayStyle("#FFFFA500", "#10FFA500", 1.25, "ROI"))];
            overlays.AddRange(accepted.Take(parameters.MaximumOverlayComponents).Select(component =>
                new AlgorithmOverlayItem(
                    $"blob-{component.Label}",
                    new AlgorithmOverlayStyle("#FF00E676", "#1800E676", 1.5, $"#{component.Label} A={component.Area}"))));

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
                    comparison = parameters.ForegroundPolarity == BlobForegroundPolarity.Bright ? "intensity >= threshold" : "intensity <= threshold",
                    colorIntensity = "0.114*B + 0.587*G + 0.299*R; alpha ignored",
                    floatingPointNominalRange = "0..1",
                    invalidPixels = "NaN or Infinity are background and counted",
                },
                connectivity = (int)parameters.Connectivity,
                geometryCoordinates = "full-image pixel coordinates; rectangle maximum edges are exclusive",
                confidence = "bounding-box fill ratio, not classification probability",
                counts = new { roiPixels, foregroundPixels, invalidPixels, candidates = components.Count, accepted = accepted.Length },
            });

            return
            [
                new AlgorithmMeasurementArtifact("blob-summary", measurements),
                new AlgorithmTableArtifact("blob-components", columns, rows),
                new AlgorithmGeometryArtifact("blob-geometry", AlgorithmCoordinateSpace.Pixel, geometries),
                new AlgorithmOverlayArtifact("blob-overlay", AlgorithmOverlayLifetime.Transient, overlays),
                new AlgorithmStructuredDataArtifact("blob-provenance", ResultSchema, structured),
            ];
        }

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

        private sealed record BlobComponent(
            int Label,
            int Left,
            int Top,
            int Width,
            int Height,
            int Area,
            double CentroidX,
            double CentroidY,
            double FillRatio,
            bool TouchesImageBorder,
            bool Accepted,
            string? FilterReason);
    }
}
