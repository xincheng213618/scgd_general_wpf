using ColorVision.Algorithms;
using System;
using System.Buffers;
using System.Buffers.Binary;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.ImageEditor.Algorithms
{
    /// <summary>Deterministic nearest/bilinear sampling along an open or closed polyline.</summary>
    public sealed class ImageProfileAlgorithmProvider : IImageAlgorithmProvider, IAlgorithmDescriptorSupport
    {
        private const string ResultSchema = "colorvision.analysis.image-profile/v1";
        private const int EstimatedRowBaseBytes = 256;
        private const int EstimatedCellBytes = 256;
        private const int MeasurementBatchSamples = 256;
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

        public bool CanExecuteDescriptor(AlgorithmDescriptor descriptor, out string? reason)
        {
            return StandardAlgorithmAdapterContract.IsCanonicalProviderContract(descriptor, StandardAlgorithmIds.ImageProfile, out reason);
        }

        public bool CanExecute(AlgorithmDescriptor descriptor, IReadOnlyList<AlgorithmInput> inputs, out string? reason)
        {
            bool supported = descriptor.Id == StandardAlgorithmIds.ImageProfile
                && inputs.Count == 1
                && Formats.Contains(inputs[0].Image.Format);
            reason = supported ? null : "algorithm_or_format_not_implemented";
            return supported;
        }

        public ValueTask<AlgorithmResult> ExecuteAsync(AlgorithmExecutionContext context, CancellationToken cancellationToken)
            => ValueTask.FromResult(ExecuteCore(context, new ProfileRaster(context.Inputs[0].Image), cancellationToken));

        // Interactive measurement sources are request-owned and do not require a full display-frame copy.
        public AlgorithmResult ExecuteMeasurement(AlgorithmInvocation invocation, ImageProfileParameters parameters,
            IImageProfileMeasurementSource source, ImageSelectionScope scope, CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress = null)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!parameters.Validate().IsValid
                || invocation.Roi is not PolylineAlgorithmRoi path || !path.Validate().IsValid
                || invocation.AlgorithmId != StandardAlgorithmIds.ImageProfile)
                throw new ArgumentException("Invalid profile invocation or parameters.");
            if (source.Width != scope.PixelWidth || source.Height != scope.PixelHeight || source.Width <= 0 || source.Height <= 0)
                throw new ArgumentException("The measurement plane dimensions do not match the current image.");
            if (source.RawChannelCount is < 1 or > 6 || source.ChannelNames.Count is < 1 or > 9 || source.ChannelNames.Distinct().Count() != source.ChannelNames.Count)
                throw new ArgumentException("Invalid measurement channels.");
            if (!double.IsFinite(scope.DpiX) || scope.DpiX <= 0 || !double.IsFinite(scope.DpiY) || scope.DpiY <= 0)
                throw new ArgumentException("Invalid profile DPI.");
            return ExecuteInteractive(invocation, parameters, new ProfileRaster(source, scope), cancellationToken, progress);
        }

        // Keep the sampling footprint in original pixel coordinates for clamp/skip/reject.
        internal static Int32Rect? GetDisplaySlice(PolylineAlgorithmRoi path, ImageProfileParameters parameters, ImageSelectionScope scope)
        {
            if (path.CoordinateSpace != AlgorithmCoordinateSpace.Pixel || path.Points.Count > ImageProfileParameters.MaximumPathPoints
                || scope.PixelWidth <= 0 || scope.PixelHeight <= 0 || !path.Validate().IsValid || !parameters.Validate().IsValid)
                return null;

            AlgorithmPoint start = path.Points[0];
            double minX = start.X, maxX = start.X, minY = start.Y, maxY = start.Y;
            int segmentCount = parameters.ClosePath ? path.Points.Count : path.Points.Count - 1;
            for (int index = 1; index <= segmentCount; index++)
            {
                AlgorithmPoint end = path.Points[index % path.Points.Count];
                // Include the endpoint as evaluated by the sampler, where rounding can move it across a pixel boundary.
                double sampledEndX = start.X + (end.X - start.X);
                double sampledEndY = start.Y + (end.Y - start.Y);
                if (!double.IsFinite(sampledEndX) || !double.IsFinite(sampledEndY)) return null;
                minX = Math.Min(minX, Math.Min(end.X, sampledEndX));
                maxX = Math.Max(maxX, Math.Max(end.X, sampledEndX));
                minY = Math.Min(minY, Math.Min(end.Y, sampledEndY));
                maxY = Math.Max(maxY, Math.Max(end.Y, sampledEndY));
                start = end;
            }

            var (x, width) = SliceAxis(minX, maxX, scope.PixelWidth, parameters.Interpolation);
            var (y, height) = SliceAxis(minY, maxY, scope.PixelHeight, parameters.Interpolation);
            return width == scope.PixelWidth && height == scope.PixelHeight ? null : new Int32Rect(x, y, width, height);
        }

        private static (int First, int Count) SliceAxis(double minimum, double maximum, int size, ImageProfileInterpolation interpolation)
        {
            minimum = Math.Clamp(minimum, 0, size - 1);
            maximum = Math.Clamp(maximum, 0, size - 1);
            int first = interpolation == ImageProfileInterpolation.Nearest ? NearestPixel(minimum, size) : (int)Math.Floor(minimum);
            int last = interpolation == ImageProfileInterpolation.Nearest ? NearestPixel(maximum, size) : Math.Min((int)Math.Floor(maximum) + 1, size - 1);
            return (first, last - first + 1);
        }

        internal AlgorithmResult ExecuteDisplaySlice(AlgorithmInvocation invocation, ImageProfileParameters parameters,
            AlgorithmImageBuffer buffer, Int32Rect bounds, ImageSelectionScope scope, CancellationToken cancellationToken,
            IProgress<AlgorithmProgress>? progress = null)
            => ExecuteInteractive(invocation, parameters, new ProfileRaster(buffer, bounds, scope), cancellationToken, progress);

        private AlgorithmResult ExecuteInteractive(AlgorithmInvocation invocation, ImageProfileParameters parameters,
            ProfileRaster raster, CancellationToken cancellationToken, IProgress<AlgorithmProgress>? progress)
        {
            ImageAlgorithmPlatform.Catalog.TryResolve(StandardAlgorithmIds.ImageProfile, out AlgorithmDescriptor? descriptor);
            AlgorithmExecutionContext context = new()
            {
                Descriptor = descriptor!, Invocation = invocation, Parameters = parameters, Inputs = [], Progress = progress,
            };
            return ExecuteCore(context, raster, cancellationToken);
        }

        private AlgorithmResult ExecuteCore(AlgorithmExecutionContext context, ProfileRaster image, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (context.Invocation.Roi is not PolylineAlgorithmRoi path)
                return Failure(context, "profile_path_required", "Image profile requires a polyline ROI.", "roi");
            if (path.Points.Count > ImageProfileParameters.MaximumPathPoints)
            {
                return Failure(
                    context,
                    "profile_path_point_limit_exceeded",
                    $"The profile path contains {path.Points.Count} points, exceeding the platform limit of {ImageProfileParameters.MaximumPathPoints}.",
                    "roi.points");
            }

            ImageProfileParameters parameters = (ImageProfileParameters)context.Parameters;
            AlgorithmPoint[] points = new AlgorithmPoint[path.Points.Count];
            for (int index = 0; index < points.Length; index++)
            {
                if ((index & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                points[index] = AlgorithmCoordinates.ToPixel(path.Points[index], path.CoordinateSpace, image.DpiX, image.DpiY);
            }
            Segment[] segments = BuildSegments(points, parameters.ClosePath, image.DpiX, image.DpiY, cancellationToken);
            if (segments.Length == 0)
                return Failure(context, "profile_path_degenerate", "The profile path has no positive-length segment.", "roi.points");

            double totalPixels = segments[^1].PixelStart + segments[^1].PixelLength;
            double totalMillimetres = segments[^1].PhysicalStart + segments[^1].PhysicalLength;
            int requestedSamples;
            try { requestedSamples = CountSamples(totalPixels, parameters.SampleSpacingPixels, parameters.ClosePath); }
            catch (OverflowException)
            {
                return Failure(context, "profile_sample_limit_exceeded", "The requested sampling count exceeds the supported integer range.", nameof(parameters.SampleSpacingPixels));
            }
            if (requestedSamples > parameters.MaximumSamples)
            {
                return Failure(
                    context,
                    "profile_sample_limit_exceeded",
                    $"The path requires {requestedSamples} samples, exceeding MaximumSamples={parameters.MaximumSamples}.",
                    nameof(parameters.MaximumSamples));
            }
            if (requestedSamples > ImageProfileParameters.ExecutionMaximumSamples)
            {
                return Failure(
                    context,
                    "profile_execution_sample_budget_exceeded",
                    $"The path requires {requestedSamples} samples, exceeding the execution budget of {ImageProfileParameters.ExecutionMaximumSamples}. The schema-v1 MaximumSamples value remains valid for preset compatibility.",
                    nameof(parameters.SampleSpacingPixels));
            }

            ChannelDefinition[] channels = image.Source != null ? image.Source.ChannelNames
                .Select((name, index) => new ChannelDefinition(name, values => image.Source.GetChannelValue(index, values), index))
                .Where(channel => parameters.IncludeLuminance || channel.Name != "Luminance").ToArray() : Channels(image.Format, parameters);
            List<AlgorithmTableColumn> columns =
            [
                new("SampleIndex", "integer"), new("RequestedIndex", "integer"), new("SegmentIndex", "integer"),
                new("DistancePixels", "number", "px"), new("DistanceMillimetres", "number", "mm"),
                new("XPixel", "number", "px"), new("YPixel", "number", "px"),
            ];
            foreach (ChannelDefinition channel in channels)
            {
                columns.Add(new AlgorithmTableColumn(channel.Name, "number", image.Source != null ? image.Source.GetUnit(channel.SourceIndex) : image.Unit));
                columns.Add(new AlgorithmTableColumn(channel.Name + "Status", "string"));
            }

            long estimatedResultBytes;
            try
            {
                estimatedResultBytes = checked((long)requestedSamples * (EstimatedRowBaseBytes + (long)columns.Count * EstimatedCellBytes));
            }
            catch (OverflowException)
            {
                estimatedResultBytes = long.MaxValue;
            }
            if (estimatedResultBytes > ImageProfileParameters.MaximumEstimatedResultBytes)
            {
                return Failure(
                    context,
                    "profile_result_budget_exceeded",
                    $"The requested {requestedSamples} rows and {columns.Count} columns require an estimated {estimatedResultBytes} bytes, exceeding the platform result budget of {ImageProfileParameters.MaximumEstimatedResultBytes} bytes.",
                    nameof(parameters.SampleSpacingPixels));
            }

            List<IReadOnlyDictionary<string, JsonElement>> rows = new(requestedSamples);
            ChannelStatistics[] statistics = channels.Select(channel => new ChannelStatistics(channel.Name)).ToArray();
            int skipped = 0;
            int clamped = 0;
            int outputIndex = 0;
            int segmentIndex = 0;
            double[] raw = new double[image.Source?.RawChannelCount ?? image.Format.Channels()];
            IBatchedImageProfileMeasurementSource? batchSource = image.Source as IBatchedImageProfileMeasurementSource;
            int batchCapacity = batchSource == null ? 0 : Math.Min(requestedSamples, MeasurementBatchSamples);
            bool bilinear = parameters.Interpolation != ImageProfileInterpolation.Nearest;
            int neighbors = bilinear ? 4 : 1;
            ProfileSample[] batch = batchCapacity == 0 ? [] : new ProfileSample[batchCapacity];
            (int X, int Y)[] pixels = batchCapacity == 0 ? [] : new (int, int)[batchCapacity * neighbors];
            (double X, double Y)[] weights = !bilinear || batchCapacity == 0 ? [] : new (double, double)[batchCapacity];
            double[] pixelValues = batchCapacity == 0 ? [] : new double[pixels.Length * raw.Length];
            int batchCount = 0;
            context.Progress?.Report(new AlgorithmProgress(0.05, "profile.sample", "Sampling image profile"));
            using ProfileRowWriter rowWriter = new(columns, rows);
            ProfileCell[] rowValues = new ProfileCell[rowWriter.ColumnCount];
            int[] valueColumns = channels.Select(channel => rowWriter.GetColumn(channel.Name)).ToArray();
            int[] statusColumns = channels.Select(channel => rowWriter.GetColumn(channel.Name + "Status")).ToArray();
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
                        return Failure(
                            context,
                            "profile_sample_out_of_bounds",
                            $"Sample {requestedIndex} at ({x:R}, {y:R}) is outside the image.",
                            "roi.points");
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

                ProfileSample sample = new(requestedIndex, segment.OriginalIndex, distance,
                    segment.PhysicalStart + ratio * segment.PhysicalLength, x, y);
                if (batchSource == null)
                {
                    SampleRaw(image, x, y, parameters.Interpolation, raw);
                    AppendSample(sample);
                }
                else
                {
                    batch[batchCount++] = sample;
                    if (batchCount == batch.Length) FlushSamples();
                }
            }
            FlushSamples();
            rowWriter.Flush();

            void FlushSamples()
            {
                if (batchCount == 0) return;
                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < batchCount; index++)
                {
                    ProfileSample sample = batch[index];
                    if (!bilinear)
                        pixels[index] = (NearestPixel(sample.X, image.Width), NearestPixel(sample.Y, image.Height));
                    else
                    {
                        var (x0, y0, x1, y1, tx, ty) = BilinearFootprint(image, sample.X, sample.Y);
                        int first = index * neighbors;
                        pixels[first] = (x0, y0);
                        pixels[first + 1] = (x1, y0);
                        pixels[first + 2] = (x0, y1);
                        pixels[first + 3] = (x1, y1);
                        weights[index] = (tx, ty);
                    }
                }

                int channelsPerPixel = raw.Length;
                int pixelCount = batchCount * neighbors;
                batchSource!.ReadPixels(pixels.AsMemory(0, pixelCount), pixelValues.AsMemory(0, pixelCount * channelsPerPixel));
                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < batchCount; index++)
                {
                    int offset = index * neighbors * channelsPerPixel;
                    if (!bilinear) pixelValues.AsSpan(offset, channelsPerPixel).CopyTo(raw);
                    else
                    {
                        var (tx, ty) = weights[index];
                        for (int channel = 0; channel < channelsPerPixel; channel++, offset++)
                        {
                            double top = Lerp(pixelValues[offset], pixelValues[offset + channelsPerPixel], tx);
                            double bottom = Lerp(pixelValues[offset + 2 * channelsPerPixel], pixelValues[offset + 3 * channelsPerPixel], tx);
                            raw[channel] = Lerp(top, bottom, ty);
                        }
                    }
                    AppendSample(batch[index]);
                }
                batchCount = 0;
            }

            void AppendSample(ProfileSample sample)
            {
                // The fixed columns occupy the first seven slots in the table layout above.
                rowValues[0] = new(outputIndex++, IsInteger: true);
                rowValues[1] = new(sample.RequestedIndex, IsInteger: true);
                rowValues[2] = new(sample.SegmentIndex, IsInteger: true);
                rowValues[3] = new(sample.Distance);
                rowValues[4] = new(sample.PhysicalDistance);
                rowValues[5] = new(sample.X);
                rowValues[6] = new(sample.Y);
                for (int channelIndex = 0; channelIndex < channels.Length; channelIndex++)
                {
                    double value = channels[channelIndex].Read(raw);
                    rowValues[valueColumns[channelIndex]] = double.IsFinite(value) ? new(value) : new(0, IsNull: true);
                    rowValues[statusColumns[channelIndex]] = new(0, Status: Classification(value));
                    statistics[channelIndex].Add(value);
                }
                rowWriter.Append(rowValues);
            }

            if (rows.Count == 0)
                return Failure(context, "profile_no_samples", "No profile samples remain after applying the boundary rule.", "roi");

            context.Progress?.Report(new AlgorithmProgress(0.9, "profile.artifacts", "Building profile artifacts"));
            List<AlgorithmMeasurement> measurements =
            [
                new("profile.sample_count", rows.Count, "sample"),
                new("profile.requested_sample_count", requestedSamples, "sample"),
                new("profile.skipped_sample_count", skipped, "sample"),
                new("profile.clamped_sample_count", clamped, "sample"),
                new("profile.path_length_pixels", totalPixels, "px"),
                new("profile.path_length_millimetres", totalMillimetres, "mm"),
                new("profile.estimated_result_bytes", estimatedResultBytes, "byte"),
            ];
            for (int index = 0; index < statistics.Length; index++)
            {
                ChannelStatistics stats = statistics[index];
                measurements.Add(new AlgorithmMeasurement("channel.finite_count", stats.Count, "sample", index, Qualifiers: ChannelQualifier(stats.Name)));
                measurements.Add(new AlgorithmMeasurement("channel.invalid_count", stats.InvalidCount, "sample", index, Qualifiers: ChannelQualifier(stats.Name)));
                if (stats.Count > 0)
                {
                    string? unit = image.Source?.GetUnit(channels[index].SourceIndex) ?? (image.Source == null ? image.Unit : null);
                    measurements.Add(new AlgorithmMeasurement("channel.minimum", stats.Minimum, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                    measurements.Add(new AlgorithmMeasurement("channel.maximum", stats.Maximum, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                    measurements.Add(new AlgorithmMeasurement("channel.mean", stats.Mean, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                    measurements.Add(new AlgorithmMeasurement("channel.stddev.population", stats.StandardDeviation, unit, index, Qualifiers: ChannelQualifier(stats.Name)));
                }
            }

            AlgorithmGeometry geometry = new(
                "profile-path",
                parameters.ClosePath ? AlgorithmGeometryKind.Polygon : AlgorithmGeometryKind.Polyline,
                points);
            JsonElement provenance = AlgorithmJson.ToElement(new
            {
                schema = ResultSchema,
                input = new { image.Width, image.Height, format = image.Source?.FormatName ?? image.Format.ToString(), channels = image.Source?.ChannelNames, valueRule = image.Source?.ValueDescription, image.DpiX, image.DpiY },
                path = context.Invocation.Roi,
                parameters,
                requestedSamples,
                returnedSamples = rows.Count,
                estimatedResultBytes,
                resultBudgetBytes = ImageProfileParameters.MaximumEstimatedResultBytes,
                skipped,
                clamped,
                distanceRule = "piecewise-euclidean-pixel-and-dpi-aware-millimetres",
                openEndpointRule = "include-first-and-last",
                closedEndpointRule = "include-first-do-not-repeat-at-total-length",
                nearestRule = "floor-coordinate-plus-one-half",
                luminance = image.Source != null ? "measurement channels; see input.valueRule" : "Rec.601: 0.299R + 0.587G + 0.114B",
            });
            List<AlgorithmDiagnosticMessage> diagnostics = new();
            if (clamped > 0) diagnostics.Add(new AlgorithmDiagnosticMessage("profile_samples_clamped", $"Clamped {clamped} samples to image bounds."));
            if (skipped > 0) diagnostics.Add(new AlgorithmDiagnosticMessage("profile_samples_skipped", $"Skipped {skipped} out-of-bounds samples."));
            cancellationToken.ThrowIfCancellationRequested();
            return new AlgorithmResult
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
            };
        }

        private static Segment[] BuildSegments(AlgorithmPoint[] points, bool closed, double dpiX, double dpiY, CancellationToken cancellationToken)
        {
            List<Segment> segments = new();
            double pixelStart = 0;
            double physicalStart = 0;
            int count = closed ? points.Length : points.Length - 1;
            for (int index = 0; index < count; index++)
            {
                if ((index & 255) == 0) cancellationToken.ThrowIfCancellationRequested();
                AlgorithmPoint start = points[index];
                AlgorithmPoint end = points[(index + 1) % points.Length];
                double dx = end.X - start.X;
                double dy = end.Y - start.Y;
                double pixels = Math.Sqrt(dx * dx + dy * dy);
                if (pixels <= 0) continue;
                double mmX = dx * 25.4 / dpiX;
                double mmY = dy * 25.4 / dpiY;
                double physical = Math.Sqrt(mmX * mmX + mmY * mmY);
                segments.Add(new Segment(index, start, end, pixels, physical, pixelStart, physicalStart));
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

        private static void SampleRaw(ProfileRaster image, double x, double y, ImageProfileInterpolation interpolation, Span<double> result)
        {
            if (interpolation == ImageProfileInterpolation.Nearest)
            {
                ReadRaw(image, NearestPixel(x, image.Width), NearestPixel(y, image.Height), result);
                return;
            }

            var (x0, y0, x1, y1, tx, ty) = BilinearFootprint(image, x, y);
            for (int channel = 0; channel < result.Length; channel++)
            {
                double top = Lerp(ReadRaw(image, x0, y0, channel), ReadRaw(image, x1, y0, channel), tx);
                double bottom = Lerp(ReadRaw(image, x0, y1, channel), ReadRaw(image, x1, y1, channel), tx);
                result[channel] = Lerp(top, bottom, ty);
            }
        }

        private static int NearestPixel(double coordinate, int size) => Math.Clamp((int)Math.Floor(coordinate + 0.5), 0, size - 1);

        private static (int X0, int Y0, int X1, int Y1, double Tx, double Ty) BilinearFootprint(ProfileRaster image, double x, double y)
        {
            double floorX = Math.Floor(x), floorY = Math.Floor(y);
            int x0 = Math.Clamp((int)floorX, 0, image.Width - 1);
            int y0 = Math.Clamp((int)floorY, 0, image.Height - 1);
            return (x0, y0, Math.Min(x0 + 1, image.Width - 1), Math.Min(y0 + 1, image.Height - 1), x - floorX, y - floorY);
        }

        private static double Lerp(double start, double end, double amount)
        {
            if (amount <= 0) return start;
            if (amount >= 1) return end;
            return start + (end - start) * amount;
        }

        private static void ReadRaw(ProfileRaster image, int x, int y, Span<double> values)
        {
            for (int channel = 0; channel < values.Length; channel++) values[channel] = ReadRaw(image, x, y, channel);
        }

        private static double ReadRaw(ProfileRaster image, int x, int y, int channel)
        {
            if (image.Source != null) return image.Source.Read(x, y, channel);
            ReadOnlySpan<byte> data = image.Buffer!.Data.Span;
            int bytesPerChannel = image.Format.BitsPerChannel() / 8;
            int offset = checked((y - image.OriginY) * image.Buffer.Stride + (x - image.OriginX) * image.Format.BytesPerPixel() + channel * bytesPerChannel);
            return image.Format.BitsPerChannel() switch
            {
                8 => data[offset],
                16 => BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(offset, 2)),
                32 => BitConverter.Int32BitsToSingle(BinaryPrimitives.ReadInt32LittleEndian(data.Slice(offset, 4))),
                _ => throw new ArgumentOutOfRangeException(nameof(image)),
            };
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

        private readonly record struct ProfileSample(int RequestedIndex, int SegmentIndex, double Distance, double PhysicalDistance, double X, double Y);

        private readonly record struct ProfileCell(double Number, string? Status = null, bool IsInteger = false, bool IsNull = false)
        {
            public void Write(Utf8JsonWriter writer)
            {
                if (Status != null) writer.WriteStringValue(Status);
                else if (IsNull) writer.WriteNullValue();
                else if (IsInteger) writer.WriteNumberValue((int)Number);
                else if (double.IsFinite(Number)) writer.WriteNumberValue(Number);
                else writer.WriteStringValue(double.IsNaN(Number) ? "NaN" : double.IsPositiveInfinity(Number) ? "Infinity" : "-Infinity");
            }
        }

        private sealed class ProfileRowWriter : IDisposable
        {
            private const int BlockRows = 256;
            private readonly ArrayBufferWriter<byte> _buffer = new();
            private readonly Utf8JsonWriter _writer;
            private readonly List<IReadOnlyDictionary<string, JsonElement>> _rows;
            private readonly Dictionary<string, int> _columns = new(StringComparer.Ordinal);
            private readonly IReadOnlyList<string> _names;
            private int _pendingRows;

            public ProfileRowWriter(List<AlgorithmTableColumn> columns, List<IReadOnlyDictionary<string, JsonElement>> rows)
            {
                List<string> names = new(columns.Count);
                foreach (AlgorithmTableColumn column in columns)
                {
                    // Preserve the first position and last assigned value when names overlap.
                    if (_columns.TryAdd(column.Name, names.Count)) names.Add(column.Name);
                }
                _names = names.AsReadOnly();
                _rows = rows;
                _writer = new Utf8JsonWriter(_buffer);
            }

            public int ColumnCount => _names.Count;
            public int GetColumn(string name) => _columns[name];

            public void Append(ReadOnlySpan<ProfileCell> values)
            {
                if (_pendingRows == 0) _writer.WriteStartArray();
                _writer.WriteStartArray();
                foreach (ProfileCell value in values) value.Write(_writer);
                _writer.WriteEndArray();
                if (++_pendingRows == BlockRows) Flush();
            }

            public void Flush()
            {
                if (_pendingRows == 0) return;
                _writer.WriteEndArray();
                _writer.Flush();
                // A block owns only its typed cells. Rows share the column map and keep the block
                // alive independently of the reusable encoding buffer and the result's other rows.
                using (JsonDocument document = JsonDocument.Parse(_buffer.WrittenMemory))
                {
                    foreach (JsonElement cells in document.RootElement.Clone().EnumerateArray())
                        _rows.Add(new ProfileRow(_names, _columns, cells));
                }
                _buffer.ResetWrittenCount();
                _writer.Reset(_buffer);
                _pendingRows = 0;
            }

            public void Dispose() => _writer.Dispose();
        }

        private sealed class ProfileRow(IReadOnlyList<string> names, Dictionary<string, int> columns, JsonElement cells)
            : IReadOnlyDictionary<string, JsonElement>
        {
            public int Count => names.Count;
            public IEnumerable<string> Keys => names;
            public IEnumerable<JsonElement> Values => cells.EnumerateArray();
            public JsonElement this[string key] => cells[columns[key]];

            public bool ContainsKey(string key) => columns.ContainsKey(key);

            public bool TryGetValue(string key, out JsonElement value)
            {
                bool found = columns.TryGetValue(key, out int column);
                value = found ? cells[column] : default;
                return found;
            }

            public IEnumerator<KeyValuePair<string, JsonElement>> GetEnumerator()
            {
                int index = 0;
                foreach (JsonElement cell in cells.EnumerateArray()) yield return new(names[index++], cell);
            }

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        private sealed class ProfileRaster
        {
            public AlgorithmImageBuffer? Buffer { get; }
            public IImageProfileMeasurementSource? Source { get; }
            public int Width { get; }
            public int Height { get; }
            public double DpiX { get; }
            public double DpiY { get; }
            public int OriginX { get; }
            public int OriginY { get; }
            public AlgorithmImageFormat Format => Buffer?.Format ?? AlgorithmImageFormat.Gray32Float;
            public string Unit => Format.IsFloatingPoint() ? "value" : "DN";
            public ProfileRaster(AlgorithmImageBuffer buffer)
            {
                Buffer = buffer; Width = buffer.Width; Height = buffer.Height; DpiX = buffer.DpiX; DpiY = buffer.DpiY;
            }
            public ProfileRaster(AlgorithmImageBuffer buffer, Int32Rect bounds, ImageSelectionScope scope)
            {
                Buffer = buffer;
                Width = scope.PixelWidth;
                Height = scope.PixelHeight;
                DpiX = scope.DpiX;
                DpiY = scope.DpiY;
                OriginX = bounds.X;
                OriginY = bounds.Y;
            }
            public ProfileRaster(IImageProfileMeasurementSource source, ImageSelectionScope scope)
            {
                Source = source; Width = source.Width; Height = source.Height; DpiX = scope.DpiX; DpiY = scope.DpiY;
            }
        }

        private sealed record ChannelDefinition(string Name, Func<double[], double> Read, int SourceIndex = 0);

        private sealed class ChannelStatistics(string name)
        {
            public string Name { get; } = name;
            public long Count { get; private set; }
            public long InvalidCount { get; private set; }
            public double Minimum { get; private set; } = double.PositiveInfinity;
            public double Maximum { get; private set; } = double.NegativeInfinity;
            public double Mean { get; private set; }
            public double StandardDeviation => Count == 0 ? double.NaN : Math.Sqrt(_m2 / Count);

            private double _m2;

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
                double delta = value - Mean;
                Mean += delta / Count;
                _m2 += delta * (value - Mean);
            }
        }

        private sealed record Segment(
            int OriginalIndex,
            AlgorithmPoint Start,
            AlgorithmPoint End,
            double PixelLength,
            double PhysicalLength,
            double PixelStart,
            double PhysicalStart);
    }
}
