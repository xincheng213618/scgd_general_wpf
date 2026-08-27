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
    /// <summary>Deterministic nearest/bilinear sampling along an open or closed polyline.</summary>
    public sealed class ImageProfileAlgorithmProvider : IImageAlgorithmProvider
    {
        private const string ResultSchema = "colorvision.analysis.image-profile/v1";
        private static readonly IReadOnlySet<AlgorithmImageFormat> Formats = Enum.GetValues<AlgorithmImageFormat>().ToHashSet();

        public AlgorithmProviderMetadata Metadata { get; } = new(
            "colorvision.image-profile.cpu",
            "ColorVision Image Profile CPU",
            AlgorithmProviderKind.Cpu,
            AlgorithmExecutionPlane.Local,
            109,
            AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Batch | AlgorithmHostCapabilities.Flow
                | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic
                | AlgorithmHostCapabilities.Roi,
            Formats,
            "1.0.0");

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.ImageProfile
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Invocation.Roi is not PolylineAlgorithmRoi path)
                return ValueTask.FromResult(Failure(context, "profile_path_required", "Image profile requires a polyline ROI.", "roi"));

            AlgorithmImageBuffer image = context.Inputs[0].Image;
            ImageProfileParameters parameters = (ImageProfileParameters)context.Parameters;
            AlgorithmPoint[] points = path.Points
                .Select(point => AlgorithmCoordinates.ToPixel(point, path.CoordinateSpace, image.DpiX, image.DpiY))
                .ToArray();
            Segment[] segments = BuildSegments(points, parameters.ClosePath, image.DpiX, image.DpiY);
            if (segments.Length == 0)
                return ValueTask.FromResult(Failure(context, "profile_path_degenerate", "The profile path has no positive-length segment.", "roi.points"));

            double totalPixels = segments[^1].PixelStart + segments[^1].PixelLength;
            double totalMillimetres = segments[^1].PhysicalStart + segments[^1].PhysicalLength;
            int requestedSamples;
            try { requestedSamples = CountSamples(totalPixels, parameters.SampleSpacingPixels, parameters.ClosePath); }
            catch (OverflowException)
            {
                return ValueTask.FromResult(Failure(context, "profile_sample_limit_exceeded", "The requested sampling count exceeds the supported integer range.", nameof(parameters.SampleSpacingPixels)));
            }
            if (requestedSamples > parameters.MaximumSamples)
            {
                return ValueTask.FromResult(Failure(
                    context,
                    "profile_sample_limit_exceeded",
                    $"The path requires {requestedSamples} samples, exceeding MaximumSamples={parameters.MaximumSamples}.",
                    nameof(parameters.MaximumSamples)));
            }

            ChannelDefinition[] channels = Channels(image.Format, parameters);
            List<AlgorithmTableColumn> columns =
            [
                new("SampleIndex", "integer"), new("RequestedIndex", "integer"), new("SegmentIndex", "integer"),
                new("DistancePixels", "number", "px"), new("DistanceMillimetres", "number", "mm"),
                new("XPixel", "number", "px"), new("YPixel", "number", "px"),
            ];
            foreach (ChannelDefinition channel in channels)
            {
                columns.Add(new AlgorithmTableColumn(channel.Name, "number", image.Format.IsFloatingPoint() ? "value" : "DN"));
                columns.Add(new AlgorithmTableColumn(channel.Name + "Status", "string"));
            }

            List<IReadOnlyDictionary<string, JsonElement>> rows = new(requestedSamples);
            ChannelStatistics[] statistics = channels.Select(channel => new ChannelStatistics(channel.Name)).ToArray();
            int skipped = 0;
            int clamped = 0;
            int outputIndex = 0;
            int segmentIndex = 0;
            context.Progress?.Report(new AlgorithmProgress(0.05, "profile.sample", "Sampling image profile"));
            for (int requestedIndex = 0; requestedIndex < requestedSamples; requestedIndex++)
            {
                if ((requestedIndex & 1023) == 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    context.Progress?.Report(new AlgorithmProgress(0.05 + 0.8 * requestedIndex / Math.Max(1, requestedSamples), "profile.sample"));
                }

                double distance = SampleDistance(requestedIndex, requestedSamples, totalPixels, parameters.SampleSpacingPixels, parameters.ClosePath);
                while (segmentIndex < segments.Length - 1 && distance > segments[segmentIndex].PixelStart + segments[segmentIndex].PixelLength)
                    segmentIndex++;
                Segment segment = segments[segmentIndex];
                double local = Math.Clamp(distance - segment.PixelStart, 0, segment.PixelLength);
                double ratio = segment.PixelLength <= 0 ? 0 : local / segment.PixelLength;
                double x = segment.Start.X + (segment.End.X - segment.Start.X) * ratio;
                double y = segment.Start.Y + (segment.End.Y - segment.Start.Y) * ratio;
                bool outside = x < 0 || x > image.Width - 1 || y < 0 || y > image.Height - 1;
                if (outside)
                {
                    if (parameters.BoundaryMode == ImageProfileBoundaryMode.Reject)
                    {
                        return ValueTask.FromResult(Failure(
                            context,
                            "profile_sample_out_of_bounds",
                            $"Sample {requestedIndex} at ({x:R}, {y:R}) is outside the image.",
                            "roi.points"));
                    }
                    if (parameters.BoundaryMode == ImageProfileBoundaryMode.Skip)
                    {
                        skipped++;
                        continue;
                    }
                    x = Math.Clamp(x, 0, image.Width - 1);
                    y = Math.Clamp(y, 0, image.Height - 1);
                    clamped++;
                }

                double[] raw = SampleRaw(image, x, y, parameters.Interpolation);
                Dictionary<string, JsonElement> row = Row(
                    ("SampleIndex", outputIndex),
                    ("RequestedIndex", requestedIndex),
                    ("SegmentIndex", segmentIndex),
                    ("DistancePixels", distance),
                    ("DistanceMillimetres", segment.PhysicalStart + ratio * segment.PhysicalLength),
                    ("XPixel", x),
                    ("YPixel", y));
                for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
                {
                    double value = channels[channelIndex].Read(raw);
                    row[channels[channelIndex].Name] = Value(double.IsFinite(value) ? value : null);
                    row[channels[channelIndex].Name + "Status"] = Value(Classification(value));
                    statistics[channelIndex].Add(value);
                }
                rows.Add(row);
                outputIndex++;
            }

            if (rows.Count == 0)
                return ValueTask.FromResult(Failure(context, "profile_no_samples", "No profile samples remain after applying the boundary rule.", "roi"));

            context.Progress?.Report(new AlgorithmProgress(0.9, "profile.artifacts", "Building profile artifacts"));
            List<AlgorithmMeasurement> measurements =
            [
                new("profile.sample_count", rows.Count, "sample"),
                new("profile.requested_sample_count", requestedSamples, "sample"),
                new("profile.skipped_sample_count", skipped, "sample"),
                new("profile.clamped_sample_count", clamped, "sample"),
                new("profile.path_length_pixels", totalPixels, "px"),
                new("profile.path_length_millimetres", totalMillimetres, "mm"),
            ];
            for (int index = 0; index < statistics.Length; index++)
            {
                ChannelStatistics stats = statistics[index];
                measurements.Add(new AlgorithmMeasurement("channel.finite_count", stats.Count, "sample", index, Qualifiers: ChannelQualifier(stats.Name)));
                measurements.Add(new AlgorithmMeasurement("channel.invalid_count", stats.InvalidCount, "sample", index, Qualifiers: ChannelQualifier(stats.Name)));
                if (stats.Count > 0)
                {
                    string unit = image.Format.IsFloatingPoint() ? "value" : "DN";
                    measurements.Add(new AlgorithmMeasurement("channel.minimum", stats.Minimum, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                    measurements.Add(new AlgorithmMeasurement("channel.maximum", stats.Maximum, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                    measurements.Add(new AlgorithmMeasurement("channel.mean", stats.Mean, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                }
            }

            AlgorithmGeometry geometry = new(
                "profile-path",
                parameters.ClosePath ? AlgorithmGeometryKind.Polygon : AlgorithmGeometryKind.Polyline,
                points);
            JsonElement provenance = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Format.ToString(), image.DpiX, image.DpiY },
                path = context.Invocation.Roi,
                parameters,
                requestedSamples,
                returnedSamples = rows.Count,
                skipped,
                clamped,
                distanceRule = "piecewise-euclidean-pixel-and-dpi-aware-millimetres",
                openEndpointRule = "include-first-and-last",
                closedEndpointRule = "include-first-do-not-repeat-at-total-length",
                nearestRule = "floor-coordinate-plus-one-half",
                luminance = "Rec.601: 0.299R + 0.587G + 0.114B",
            });
            List<AlgorithmDiagnosticMessage> diagnostics = new();
            if (clamped > 0) diagnostics.Add(new AlgorithmDiagnosticMessage("profile_samples_clamped", $"Clamped {clamped} samples to image bounds."));
            if (skipped > 0) diagnostics.Add(new AlgorithmDiagnosticMessage("profile_samples_skipped", $"Skipped {skipped} out-of-bounds samples."));
            return ValueTask.FromResult(new AlgorithmResult
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Succeeded,
                Artifacts =
                [
                    new AlgorithmMeasurementArtifact("image-profile", measurements),
                    new AlgorithmTableArtifact("image-profile-samples", columns, rows),
                    new AlgorithmGeometryArtifact("image-profile-geometry", AlgorithmCoordinateSpace.Pixel, [geometry]),
                    new AlgorithmOverlayArtifact("image-profile-overlay", AlgorithmOverlayLifetime.Transient,
                        [new AlgorithmOverlayItem("profile-path", new AlgorithmOverlayStyle("#FF00B7FF", null, 1.5, "Profile"))]),
                    new AlgorithmStructuredDataArtifact("image-profile-provenance", ResultSchema, provenance),
                ],
                Diagnostics = new AlgorithmExecutionDiagnostics { Messages = diagnostics },
            });
        }

        private static Segment[] BuildSegments(AlgorithmPoint[] points, bool closed, double dpiX, double dpiY)
        {
            List<Segment> segments = new();
            double pixelStart = 0;
            double physicalStart = 0;
            int count = closed ? points.Length : points.Length - 1;
            for (int index = 0; index < count; index++)
            {
                AlgorithmPoint start = points[index];
                AlgorithmPoint end = points[(index + 1) % points.Length];
                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double pixels = Math.Sqrt(dx * dx + dy * dy);
                if (pixels <= 0) continue;
                double mmX = dx * 25.4 / dpiX;
                double mmY = dy * 25.4 / dpiY;
                double physical = Math.Sqrt(mmX * mmX + mmY * mmY);
                segments.Add(new Segment(start, end, pixels, physical, pixelStart, physicalStart));
                pixelStart += pixels;
                physicalStart += physical;
            }
            return segments.ToArray();
        }

        private static int CountSamples(double length, double spacing, bool closed)
        {
            if (closed) return checked((int)Math.Ceiling(length / spacing));
            int regular = checked((int)Math.Floor(length / spacing) + 1);
            double last = (regular - 1) * spacing;
            return last < length - 1e-10 * Math.Max(1, length) ? checked(regular + 1) : regular;
        }

        private static double SampleDistance(int index, int count, double length, double spacing, bool closed)
        {
            if (!closed && index == count - 1) return length;
            return Math.Min(length, index * spacing);
        }

        private static double[] SampleRaw(AlgorithmImageBuffer image, double x, double y, ImageProfileInterpolation interpolation)
        {
            if (interpolation == ImageProfileInterpolation.Nearest)
                return ReadRaw(image, Math.Clamp((int)Math.Floor(x + 0.5), 0, image.Width - 1), Math.Clamp((int)Math.Floor(y + 0.5), 0, image.Height - 1));

            int x0 = Math.Clamp((int)Math.Floor(x), 0, image.Width - 1);
            int y0 = Math.Clamp((int)Math.Floor(y), 0, image.Height - 1);
            int x1 = Math.Min(x0 + 1, image.Width - 1);
            int y1 = Math.Min(y0 + 1, image.Height - 1);
            double tx = x - Math.Floor(x);
            double ty = y - Math.Floor(y);
            double[] topLeft = ReadRaw(image, x0, y0);
            double[] topRight = ReadRaw(image, x1, y0);
            double[] bottomLeft = ReadRaw(image, x0, y1);
            double[] bottomRight = ReadRaw(image, x1, y1);
            double[] result = new double[topLeft.Length];
            for (int channel = 0; channel < result.Length; channel++)
            {
                double top = Lerp(topLeft[channel], topRight[channel], tx);
                double bottom = Lerp(bottomLeft[channel], bottomRight[channel], tx);
                result[channel] = Lerp(top, bottom, ty);
            }
            return result;
        }

        private static double Lerp(double start, double end, double amount)
        {
            if (amount <= 0) return start;
            if (amount >= 1) return end;
            return start + (end - start) * amount;
        }

        private static double[] ReadRaw(AlgorithmImageBuffer image, int x, int y)
        {
            ReadOnlySpan<byte> data = image.Data.Span;
            int channels = image.Format.Channels();
            int bytesPerChannel = image.Format.BitsPerChannel() / 8;
            int pixel = checked(y * image.Stride + x * image.Format.BytesPerPixel());
            double[] values = new double[channels];
            for (int channel = 0; channel < channels; channel++)
            {
                int offset = pixel + channel * bytesPerChannel;
                values[channel] = image.Format.BitsPerChannel() switch
                {
                    8 => data[offset],
                    16 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
                    32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4))),
                    _ => throw new ArgumentOutOfRangeException(nameof(image)),
                };
            }
            return values;
        }

        private static ChannelDefinition[] Channels(AlgorithmImageFormat format, ImageProfileParameters parameters)
        {
            if (format.Channels() == 1) return [new ChannelDefinition("Gray", values => values[0])];
            List<ChannelDefinition> channels =
            [
                new("B", values => values[0]),
                new("G", values => values[1]),
                new("R", values => values[2]),
            ];
            if (format.Channels() == 4 && parameters.IncludeAlpha) channels.Add(new ChannelDefinition("A", values => values[3]));
            if (parameters.IncludeLuminance) channels.Add(new ChannelDefinition("Luminance", values => 0.114 * values[0] + 0.587 * values[1] + 0.299 * values[2]));
            return channels.ToArray();
        }

        private static string Classification(double value) => value switch
        {
            _ when double.IsNaN(value) => "NaN",
            _ when double.IsPositiveInfinity(value) => "+Infinity",
            _ when double.IsNegativeInfinity(value) => "-Infinity",
            _ => "Finite",
        };

        private static IReadOnlyDictionary<string, string> ChannelQualifier(string name)
            => new Dictionary<string, string> { ["channelName"] = name };

        private static AlgorithmResult Failure(AlgorithmExecutionContext context, string code, string message, string path)
            => new()
            {
                InvocationId = context.Invocation.InvocationId,
                AlgorithmId = context.Descriptor.Id,
                AlgorithmVersion = context.Descriptor.Version,
                Status = AlgorithmResultStatus.Failed,
                Failures = [new AlgorithmFailure(code, message, path)],
            };

        private static Dictionary<string, JsonElement> Row(params (string Name, object? Value)[] values)
            => values.ToDictionary(value => value.Name, value => Value(value.Value), StringComparer.Ordinal);

        private static JsonElement Value(object? value) => AlgorithmJson.ToElement(value);

        private sealed record ChannelDefinition(string Name, Func<double[], double> Read);

        private sealed class ChannelStatistics(string name)
        {
            public string Name { get; } = name;
            public long Count { get; private set; }
            public long InvalidCount { get; private set; }
            public double Minimum { get; private set; } = double.PositiveInfinity;
            public double Maximum { get; private set; } = double.NegativeInfinity;
            public double Mean { get; private set; }

            public void Add(double value)
            {
                if (!double.IsFinite(value))
                {
                    InvalidCount++;
                    return;
                }
                Count++;
                Minimum = Math.Min(Minimum, value);
                Maximum = Math.Max(Maximum, value);
                Mean += (value - Mean) / Count;
            }
        }

        private sealed record Segment(
            AlgorithmPoint Start,
            AlgorithmPoint End,
            double PixelLength,
            double PhysicalLength,
            double PixelStart,
            double PhysicalStart);
    }
}
