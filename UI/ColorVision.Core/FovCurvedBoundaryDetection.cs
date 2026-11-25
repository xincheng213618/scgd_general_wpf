using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Core
{
    /// <summary>
    /// Measures a filled, isolated luminous area within the image.
    /// Segmentation supplies a search seed only; full-resolution local gradients
    /// locate the edge without a centre-luminance criterion or straight-line fit.
    /// </summary>
    internal static class FovCurvedBoundaryDetector
    {
        private const int MaximumSeedDimension = 1024;
        private const int ContourSamples = 720;
        private const double RayAngleStep = 2 * Math.PI / ContourSamples;
        // A weak candidate may need a second look at its position.
        private const double WeakEdgeContrast = 0.25;
        private const double NeighborContrastFraction = 0.8;

        [Flags]
        private enum ImageEdge
        {
            None = 0,
            Left = 1,
            Top = 2,
            Right = 4,
            Bottom = 8
        }

        private readonly record struct RayBoundary(LuminousAreaPoint Point, double Radius, double Contrast, ImageEdge FrameEdges, bool OuterSampleIsLit)
        {
            public bool IsWeak => FrameEdges == ImageEdge.None && Contrast < WeakEdgeContrast;
        }

        private readonly record struct EdgeSearch(double Start, double End, double MinimumContrast, bool RequireExterior = false);

        internal static (LuminousAreaDetectionResult Detection, FovBoundaryGeometry Geometry) Detect(HImage image, RoiRect roi)
        {
            ImageReader reader = new(image, roi);
            int step = Math.Max(1, (int)Math.Ceiling(Math.Max(reader.Width, reader.Height) / (double)MaximumSeedDimension));
            double smoothing = Math.Max(1, step / 2d);
            double plateauDistance = 2 * step + 2 * smoothing;
            int width = (reader.Width + step - 1) / step;
            int height = (reader.Height + step - 1) / step;
            double[] values = reader.ReadSeedGrid(step, width, height);
            double minimum = double.PositiveInfinity;
            double maximum = double.NegativeInfinity;
            foreach (double value in values)
            {
                minimum = Math.Min(minimum, value);
                maximum = Math.Max(maximum, value);
            }
            if (maximum <= minimum) throw new InvalidOperationException("曲边发光区没有可定位的明暗边界。");
            double brightThreshold = FindOtsuThreshold(values, minimum, maximum);
            double threshold = FindSeedThreshold(values, minimum, brightThreshold);
            int[] labels = new int[values.Length];
            int[] queue = new int[values.Length];
            int largestLabel = 0;
            int largestArea = 0;
            int label = 0;
            for (int index = 0; index < values.Length; index++)
            {
                if (labels[index] != 0 || values[index] <= threshold) continue;
                label++;
                int count = 1;
                queue[0] = index;
                labels[index] = label;
                for (int head = 0; head < count; head++)
                {
                    int current = queue[head];
                    int x = current % width;
                    int y = current / width;
                    Visit(x - 1, y);
                    Visit(x + 1, y);
                    Visit(x, y - 1);
                    Visit(x, y + 1);
                }
                if (count > largestArea) { largestArea = count; largestLabel = label; }

                void Visit(int x, int y)
                {
                    if (x < 0 || y < 0 || x >= width || y >= height) return;
                    int next = y * width + x;
                    if (labels[next] != 0 || values[next] <= threshold) return;
                    labels[next] = label;
                    queue[count++] = next;
                }
            }
            if (largestArea < 16) throw new InvalidOperationException("未找到完整的曲边发光区。");

            LuminousAreaPoint[] corners = new LuminousAreaPoint[4];
            double[] support = [double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity, double.NegativeInfinity];
            double minX = double.PositiveInfinity, minY = double.PositiveInfinity;
            double maxX = double.NegativeInfinity, maxY = double.NegativeInfinity;
            int right = reader.X + reader.Width - 1;
            int bottom = reader.Y + reader.Height - 1;
            ImageEdge imageEdges = ImageEdge.None;
            if (reader.X == 0) imageEdges |= ImageEdge.Left;
            if (reader.Y == 0) imageEdges |= ImageEdge.Top;
            if (right == image.cols - 1) imageEdges |= ImageEdge.Right;
            if (bottom == image.rows - 1) imageEdges |= ImageEdge.Bottom;
            ImageEdge touchedEdges = ImageEdge.None;
            for (int index = 0; index < labels.Length; index++)
            {
                if (labels[index] != largestLabel) continue;
                int x = index % width;
                int y = index / width;
                ImageEdge seedEdges = ImageEdge.None;
                if (x == 0) seedEdges |= ImageEdge.Left;
                if (y == 0) seedEdges |= ImageEdge.Top;
                if (x == width - 1) seedEdges |= ImageEdge.Right;
                if (y == height - 1) seedEdges |= ImageEdge.Bottom;
                if ((seedEdges & ~imageEdges) != ImageEdge.None)
                    throw new InvalidOperationException("曲边发光区接触图像或搜索区域边缘，无法确认完整边界。");
                touchedEdges |= seedEdges;
                LuminousAreaPoint point = new(reader.X + x * step, reader.Y + y * step);
                minX = Math.Min(minX, point.X); maxX = Math.Max(maxX, point.X);
                minY = Math.Min(minY, point.Y); maxY = Math.Max(maxY, point.Y);
                for (int corner = 0; corner < 4; corner++)
                {
                    double score = Support(point, corner);
                    if (score > support[corner]) { support[corner] = score; corners[corner] = point; }
                }
            }
            ValidateFilledComponent(labels, queue, width, height, largestLabel);
            LuminousAreaPoint center = new((minX + maxX) / 2, (minY + maxY) / 2);
            if (ComponentIndex(center) < 0) throw new InvalidOperationException("曲边模式要求中心位于连续、填充的发光区内。");

            ImageEdge usedImageEdges = ImageEdge.None;
            int recoveredRays = 0;
            int skippedExtremumProbes = 0;
            Dictionary<double, double> raySamples = new();
            List<LuminousAreaPoint> contour = new(ContourSamples + 4);
            var initialContour = new RayBoundary[ContourSamples];
            var measuredContrasts = new double?[ContourSamples];
            for (int sample = 0; sample < ContourSamples; sample++)
                initialContour[sample] = FindBoundary(center, 2 * Math.PI * sample / ContourSamples);
            for (int sample = 0; sample < ContourSamples; sample++)
            {
                RayBoundary boundary = initialContour[sample];
                if (boundary.IsWeak)
                {
                    int previous = (sample + ContourSamples - 1) % ContourSamples;
                    int next = (sample + 1) % ContourSamples;
                    boundary = RecoverWeakBoundary(center, 2 * Math.PI * sample / ContourSamples,
                        boundary, initialContour[previous], initialContour[next]);
                }
                if (boundary.FrameEdges == ImageEdge.None) measuredContrasts[sample] = boundary.Contrast;
                contour.Add(RecordBoundary(boundary));
            }

            // Include support-extremum directions to keep narrow tips between
            // uniform rays. They are contour samples, not assumed image corners.
            for (int corner = 0; corner < 4; corner++)
            {
                LuminousAreaPoint seed = corners[corner];
                double angle = Math.Atan2(seed.Y - center.Y, seed.X - center.X);
                double radius = Math.Sqrt(Math.Pow(seed.X - center.X, 2) + Math.Pow(seed.Y - center.Y, 2));
                double searchAngle = Math.Atan2(2 * step, radius);
                LuminousAreaPoint best = contour.MaxBy(point => Support(point, corner));
                for (int sample = -8; sample <= 8; sample++)
                {
                    // A probe only refines an extremum already supported by
                    // the measured ring. Keep that extremum if the probe
                    // cannot supply a usable local gradient.
                    RayBoundary? probe = LocateBoundary(center, angle + searchAngle * sample / 8);
                    if (probe is not RayBoundary measured)
                    {
                        skippedExtremumProbes++;
                        continue;
                    }
                    if (Support(measured.Point, corner) > Support(best, corner)) best = measured.Point;
                }
                best = MeasureBoundary(center, Math.Atan2(best.Y - center.Y, best.X - center.X));
                corners[corner] = best;
                contour.Add(best);
            }
            minX = contour.Min(point => point.X); maxX = contour.Max(point => point.X);
            minY = contour.Min(point => point.Y); maxY = contour.Max(point => point.Y);
            center = new((minX + maxX) / 2, (minY + maxY) / 2);
            if (ComponentIndex(center) < 0) throw new InvalidOperationException("曲边发光区中心不在填充区域内。");

            double diagonalAngle = Math.Atan2(maxY - minY, maxX - minX);
            FovBoundaryGeometry geometry = new()
            {
                Center = center,
                Left = MeasureBoundary(center, Math.PI),
                Right = MeasureBoundary(center, 0),
                Top = MeasureBoundary(center, -Math.PI / 2),
                Bottom = MeasureBoundary(center, Math.PI / 2),
                DiagonalPoints = [MeasureBoundary(center, diagonalAngle - Math.PI), MeasureBoundary(center, -diagonalAngle),
                    MeasureBoundary(center, diagonalAngle), MeasureBoundary(center, Math.PI - diagonalAngle)],
                Contour = contour.OrderBy(point => Math.Atan2(point.Y - center.Y, point.X - center.X)).ToArray()
            };
            double confidence = CalculateContourConfidence(measuredContrasts)
                ?? throw new InvalidOperationException("曲边发光区没有可定位的明暗边界。");
            string diagnostic = $"SeedStep={step}; Seed=NoiseBoundedOtsu; Sampling=SpatialMedian; ContourSamples={contour.Count}; Edge=ContrastSupportedLocalGradient; Center=ContourBounds; Axes=ImageXY; Confidence=WeakestSupportedArc.";
            if (usedImageEdges != ImageEdge.None) diagnostic += $" FrameEdges={usedImageEdges}.";
            if (recoveredRays > 0) diagnostic += $" NeighborRecoveredRays={recoveredRays}.";
            if (skippedExtremumProbes > 0) diagnostic += $" SkippedExtremumProbes={skippedExtremumProbes}.";
            LuminousAreaDetectionResult detection = new(true, "CurvedBoundary", null, confidence, null, string.Empty, null, diagnostic: diagnostic);
            return (detection, geometry);

            int ComponentIndex(LuminousAreaPoint point)
            {
                int x = (int)Math.Round((point.X - reader.X) / step);
                int y = (int)Math.Round((point.Y - reader.Y) / step);
                // The last sample can be up to one seed spacing from the image edge.
                if (x == width && (touchedEdges & ImageEdge.Right) != ImageEdge.None) x = width - 1;
                if (y == height && (touchedEdges & ImageEdge.Bottom) != ImageEdge.None) y = height - 1;
                if (x < 0 || y < 0 || x >= width || y >= height) return -1;
                int index = y * width + x;
                return labels[index] == largestLabel ? index : -1;
            }

            LuminousAreaPoint RecordBoundary(RayBoundary boundary)
            {
                usedImageEdges |= boundary.FrameEdges;
                return boundary.Point;
            }

            LuminousAreaPoint MeasureBoundary(LuminousAreaPoint origin, double angle)
            {
                RayBoundary boundary = FindBoundary(origin, angle);
                if (boundary.IsWeak)
                {
                    // Supplemental rays guide this measurement. They must not
                    // invalidate a ray that already has its own measured edge.
                    try
                    {
                        RayBoundary previous = FindBoundary(origin, angle - RayAngleStep);
                        RayBoundary next = FindBoundary(origin, angle + RayAngleStep);
                        boundary = RecoverWeakBoundary(origin, angle, boundary, previous, next);
                    }
                    catch (InvalidOperationException)
                    {
                    }
                }
                return RecordBoundary(boundary);
            }

            RayBoundary RecoverWeakBoundary(LuminousAreaPoint origin, double angle, RayBoundary boundary,
                RayBoundary previous, RayBoundary next)
            {
                if (previous.FrameEdges != ImageEdge.None || next.FrameEdges != ImageEdge.None)
                    return boundary;

                // A shadow can span several rays. If the outer flank is still
                // lit, look for a separate transition to background within the
                // original seed bracket. The two sampling windows cannot overlap.
                if (previous.IsWeak || next.IsWeak)
                {
                    if (!boundary.OuterSampleIsLit) return boundary;
                    var exterior = new EdgeSearch(boundary.Radius + 2 * plateauDistance, double.PositiveInfinity, WeakEdgeContrast, RequireExterior: true);
                    return LocateBoundary(origin, angle, exterior) ?? boundary;
                }

                double expectedRadius = (previous.Radius + next.Radius) / 2;
                double tolerance = Math.Max(2 * step, Math.Abs(previous.Radius - next.Radius));
                if (Math.Abs(boundary.Radius - expectedRadius) <= tolerance) return boundary;

                // Neighboring rays only constrain the search. The replacement
                // must be an independently measured gradient on this ray.
                double minimumContrast = Math.Max(WeakEdgeContrast, Math.Min(previous.Contrast, next.Contrast) * NeighborContrastFraction);
                var search = new EdgeSearch(expectedRadius - tolerance, expectedRadius + tolerance, minimumContrast);
                RayBoundary corrected = LocateBoundary(origin, angle, search) ?? boundary;
                if (corrected != boundary) recoveredRays++;
                return corrected;
            }

            RayBoundary FindBoundary(LuminousAreaPoint origin, double angle) =>
                LocateBoundary(origin, angle) ?? throw new InvalidOperationException("曲边发光区存在缺失、裁切或低对比边界。");

            RayBoundary? LocateBoundary(LuminousAreaPoint origin, double angle, EdgeSearch? search = null)
            {
                raySamples.Clear();
                double dx = Math.Cos(angle);
                double dy = Math.Sin(angle);
                double limitX = dx > 0 ? (right - origin.X) / dx
                    : dx < 0 ? (reader.X - origin.X) / dx : double.PositiveInfinity;
                double limitY = dy > 0 ? (bottom - origin.Y) / dy
                    : dy < 0 ? (reader.Y - origin.Y) / dy : double.PositiveInfinity;
                double limit = Math.Min(limitX, limitY);
                ImageEdge limitEdges = ImageEdge.None;
                if (limitX <= limitY) limitEdges |= dx > 0 ? ImageEdge.Right : ImageEdge.Left;
                if (limitY <= limitX) limitEdges |= dy > 0 ? ImageEdge.Bottom : ImageEdge.Top;
                bool canEndAtImageEdge = (limitEdges & touchedEdges) == limitEdges;
                double radius = 0;
                double innerRadius = double.NaN;
                while (true)
                {
                    LuminousAreaPoint point = new(origin.X + dx * radius, origin.Y + dy * radius);
                    int index = ComponentIndex(point);
                    if (index < 0) break;
                    if (radius >= limit)
                    {
                        if (!canEndAtImageEdge) throw new InvalidOperationException("曲边发光区边界不可用。");
                        break;
                    }
                    if (values[index] > brightThreshold) innerRadius = radius;
                    radius = Math.Min(radius + Math.Max(0.5, step / 2d), limit);
                }
                for (double r = radius + 2 * step; r < limit; r += step)
                {
                    LuminousAreaPoint point = new(origin.X + dx * r, origin.Y + dy * r);
                    if (point.X < reader.X || point.Y < reader.Y || point.X >= reader.X + reader.Width || point.Y >= reader.Y + reader.Height) break;
                    if (ComponentIndex(point) >= 0) throw new InvalidOperationException("曲边模式要求连续填充图案，当前轮廓含孔洞或同一方向的多段边界。");
                }
                // Confirm brightness at the actual image edge, including non-grid pixels.
                // A dark opening in the edge must use its measured light-to-dark transition.
                if (canEndAtImageEdge && limit - radius <= step && At(limit) > threshold)
                {
                    LuminousAreaPoint point = new(
                        (limitEdges & ImageEdge.Left) != ImageEdge.None ? reader.X :
                            (limitEdges & ImageEdge.Right) != ImageEdge.None ? right : origin.X + dx * limit,
                        (limitEdges & ImageEdge.Top) != ImageEdge.None ? reader.Y :
                            (limitEdges & ImageEdge.Bottom) != ImageEdge.None ? bottom : origin.Y + dy * limit);
                    return new(point, limit, 1, limitEdges, false);
                }
                // The bright and dim seeds bracket vignetting. Allow four seed
                // cells for sampling/median uncertainty, but do not search far
                // outside this component and jump onto an unrelated lens ring.
                double start = Math.Max(smoothing, (double.IsNaN(innerRadius) ? radius : innerRadius) - 4 * step);
                double end = Math.Min(radius + 4 * step, limit);
                double minimumDrop = (maximum - minimum) * 0.01;
                if (search is { } guided)
                {
                    start = Math.Max(start, guided.Start);
                    end = Math.Min(end, guided.End);
                }
                double bestScore = double.NegativeInfinity;
                double bestDrop = double.NegativeInfinity;
                double bestRadius = 0;
                int ties = 0;
                double coarseStep = Math.Max(1, step / 2d);
                Search(start, end, coarseStep);
                if (!double.IsFinite(bestDrop)) return null;
                double refinementStart = Math.Max(start, bestRadius - coarseStep);
                double refinementEnd = Math.Min(end, bestRadius + coarseStep);
                bestScore = double.NegativeInfinity;
                bestDrop = double.NegativeInfinity;
                ties = 0;
                Search(refinementStart, refinementEnd, 1);
                double inside = At(bestRadius - plateauDistance);
                double outside = At(bestRadius + plateauDistance);
                if (!double.IsFinite(bestDrop) || bestDrop <= minimumDrop || !double.IsFinite(inside) || !double.IsFinite(outside))
                {
                    return null;
                }
                double contrast = Math.Clamp((inside - outside) / Math.Max(inside - minimum, (maximum - minimum) * 1e-6), 0, 1);
                if (search is { } recovery && (contrast < recovery.MinimumContrast || (recovery.RequireExterior && outside > threshold))) return null;
                return new(new(origin.X + dx * bestRadius, origin.Y + dy * bestRadius), bestRadius, contrast, ImageEdge.None, outside > threshold);

                void Search(double from, double to, double increment)
                {
                    for (double r = from; r <= to; r += increment)
                    {
                        double drop = At(r - smoothing) - At(r + smoothing);
                        double localInside = At(r - plateauDistance);
                        double localOutside = At(r + plateauDistance);
                        double localContrast = Math.Clamp((localInside - localOutside) / Math.Max(localInside - minimum, (maximum - minimum) * 1e-6), 0, 1);
                        if (search is { } guided && !(drop > minimumDrop && localContrast >= guided.MinimumContrast
                            && (!guided.RequireExterior || localOutside <= threshold)))
                            continue;
                        // A text stroke can have a strong gradient without a
                        // sustained light-to-dark transition across the edge.
                        double score = Math.Max(0, drop) * localContrast;
                        if (score > bestScore + (maximum - minimum) * 1e-10)
                        {
                            bestScore = score;
                            bestDrop = drop;
                            bestRadius = r;
                            ties = 1;
                        }
                        else if (Math.Abs(score - bestScore) <= (maximum - minimum) * 1e-10)
                        {
                            bestRadius = (bestRadius * ties + r) / (ties + 1);
                            ties++;
                        }
                    }
                }

                double At(double distance)
                {
                    // A complete edge can have less background than the usual
                    // plateau width. Use the last available background sample.
                    distance = Math.Min(distance, limit);
                    if (raySamples.TryGetValue(distance, out double value)) return value;
                    double x = Math.Clamp(origin.X + dx * distance, reader.X, reader.X + reader.Width - 1);
                    double y = Math.Clamp(origin.Y + dy * distance, reader.Y, reader.Y + reader.Height - 1);
                    value = reader.SampleRobust(x, y, step);
                    raySamples.Add(distance, value);
                    return value;
                }
            }
        }

        // Each entry represents one uniformly spaced ray. Null marks a frame
        // closure without a measured gradient and breaks adjacency.
        internal static double? CalculateContourConfidence(IReadOnlyList<double?> contrasts)
        {
            double? confidence = null;
            int count = contrasts.Count;
            for (int index = 0; index < count; index++)
            {
                if (contrasts[index] is not double localContrast) continue;
                // Ignore an isolated dip only when both immediate neighbors
                // support it. Always read the original values so two adjacent
                // weak rays remain visible in the diagnostic, including at 360°.
                if (count >= 3 && contrasts[(index + count - 1) % count] is double previous
                    && contrasts[(index + 1) % count] is double next)
                {
                    localContrast = Math.Max(localContrast, Math.Min(previous, next));
                }
                confidence = Math.Min(confidence ?? localContrast, localContrast);
            }
            return confidence;
        }

        private static void ValidateFilledComponent(int[] labels, int[] queue, int width, int height, int component)
        {
            // Mark the component's exterior in the label workspace.
            // Unlike angular rays, this also finds holes between neighbouring
            // contour directions. Reuse the connected-component queue.
            int count = 0;
            // Contacts with the image can split exterior background into several regions.
            for (int x = 0; x < width; x++)
            {
                Visit(x, 0);
                Visit(x, height - 1);
            }
            for (int y = 1; y < height - 1; y++)
            {
                Visit(0, y);
                Visit(width - 1, y);
            }
            for (int head = 0; head < count; head++)
            {
                int current = queue[head];
                int x = current % width;
                int y = current / width;
                Visit(x - 1, y);
                Visit(x + 1, y);
                Visit(x, y - 1);
                Visit(x, y + 1);
                // Complement the foreground's four-connectivity: a diagonal
                // opening to the exterior is not an enclosed hole.
                Visit(x - 1, y - 1);
                Visit(x + 1, y - 1);
                Visit(x - 1, y + 1);
                Visit(x + 1, y + 1);
            }
            if (labels.Any(value => value != component && value != -1))
                throw new InvalidOperationException("曲边模式要求连续填充图案，当前轮廓含可分辨的孔洞。");

            void Visit(int x, int y)
            {
                if (x < 0 || y < 0 || x >= width || y >= height) return;
                int next = y * width + x;
                if (labels[next] == component || labels[next] == -1) return;
                labels[next] = -1;
                queue[count++] = next;
            }
        }

        private static double Support(LuminousAreaPoint point, int corner) => corner switch
        {
            0 => -point.X - point.Y,
            1 => point.X - point.Y,
            2 => point.X + point.Y,
            _ => -point.X + point.Y
        };

        private static double FindSeedThreshold(double[] values, double minimum, double brightThreshold)
        {
            // Split the original dark class once more to recover dim corners.
            // Keep the seed above measured background noise; splitting noise
            // itself must not grow the component throughout the background.
            double maximum = minimum;
            foreach (double value in values)
                if (value <= brightThreshold) maximum = Math.Max(maximum, value);
            if (maximum <= minimum) return brightThreshold;
            double threshold = FindOtsuThreshold(values, minimum, maximum);
            int count = 0;
            double sum = 0;
            foreach (double value in values)
            {
                if (value > threshold) continue;
                sum += value;
                count++;
            }
            double mean = sum / count;
            double variance = 0;
            foreach (double value in values)
                if (value <= threshold) variance += (value - mean) * (value - mean);
            variance /= count;
            return Math.Min(brightThreshold, Math.Max(threshold, mean + 3 * Math.Sqrt(variance)));
        }

        private static double FindOtsuThreshold(double[] values, double minimum, double maximum)
        {
            int[] histogram = new int[256];
            int totalCount = 0;
            foreach (double value in values)
            {
                if (value > maximum) continue;
                histogram[Math.Clamp((int)((value - minimum) / (maximum - minimum) * 255), 0, 255)]++;
                totalCount++;
            }
            double total = 0;
            for (int bin = 0; bin < histogram.Length; bin++) total += bin * (double)histogram[bin];
            int count = 0;
            double sum = 0;
            double best = -1;
            int threshold = 0;
            for (int bin = 0; bin < histogram.Length - 1; bin++)
            {
                count += histogram[bin];
                sum += bin * (double)histogram[bin];
                if (count == 0 || count == totalCount) continue;
                double delta = sum / count - (total - sum) / (totalCount - count);
                double variance = (double)count * (totalCount - count) * delta * delta;
                if (variance > best) { best = variance; threshold = bin; }
            }
            return minimum + (threshold + 0.5) / 255 * (maximum - minimum);
        }

        private readonly struct ImageReader
        {
            private readonly HImage image;
            private readonly int stride;
            public int X { get; }
            public int Y { get; }
            public int Width { get; }
            public int Height { get; }

            public ImageReader(HImage image, RoiRect roi)
            {
                if (image.pData == IntPtr.Zero || image.cols <= 0 || image.rows <= 0)
                    throw new ArgumentException("曲边模式需要可用图像，不能仅凭四个角点计算。", nameof(image));
                if (!(image.depth is 8 or 16 && image.channels is 1 or 3 or 4) && !(image.depth == 32 && image.channels == 1))
                    throw new ArgumentException("曲边模式支持 8/16 位交错灰度或彩色图像，以及 32 位单通道浮点图像。", nameof(image));
                this.image = image;
                int packedStride = checked(image.cols * image.channels * (image.depth / 8));
                stride = image.stride > 0 ? image.stride : packedStride;
                if (stride < packedStride) throw new ArgumentException("图像行跨度无效。", nameof(image));
                bool fullImage = roi.Width == 0 && roi.Height == 0;
                X = fullImage ? 0 : roi.X;
                Y = fullImage ? 0 : roi.Y;
                Width = fullImage ? image.cols : roi.Width;
                Height = fullImage ? image.rows : roi.Height;
                if (X < 0 || Y < 0 || Width <= 0 || Height <= 0 || (long)X + Width > image.cols || (long)Y + Height > image.rows)
                    throw new ArgumentException("搜索区域必须完整位于图像内。", nameof(roi));
            }

            public double[] ReadSeedGrid(int step, int width, int height)
            {
                double[] values = new double[checked(width * height)];
                int rowWidth = width + 4;
                double[] rows = new double[5 * rowWidth];
                Span<double> samples = stackalloc double[25];
                // Adjacent seed windows share 20 of their 25 samples. Keep only
                // five rows, including the clamped halo at the actual ROI edge.
                // This is the same spatial median as SampleRobust, not a second
                // image resize or a median of already filtered values.
                for (int row = 0; row < height + 4; row++)
                {
                    int offset = row % 5 * rowWidth;
                    int sourceY = Y + Math.Clamp((row - 2) * step, 0, Height - 1);
                    for (int x = 0; x < rowWidth; x++)
                    {
                        double value = Sample(X + Math.Clamp((x - 2) * step, 0, Width - 1), sourceY);
                        if (!double.IsFinite(value)) throw new InvalidOperationException("曲边发光区包含非有限像素值。");
                        rows[offset + x] = value;
                    }
                    if (row < 4) continue;
                    int y = row - 4;
                    for (int x = 0; x < width; x++)
                    {
                        for (int j = 0; j < 5; j++)
                            rows.AsSpan((y + j) % 5 * rowWidth + x, 5).CopyTo(samples.Slice(j * 5, 5));
                        values[y * width + x] = SelectMedian(samples);
                    }
                }
                return values;
            }

            public double SampleRobust(double x, double y, double spacing)
            {
                if (x < X || y < Y || x > X + Width - 1 || y > Y + Height - 1) return double.NaN;
                // Use a spatial median in both directions so thin strokes are
                // rejected regardless of their orientation or image colour.
                // No pixels are overwritten or enclosed regions flood-filled.
                Span<double> samples = stackalloc double[25];
                int index = 0;
                for (int j = -2; j <= 2; j++)
                    for (int i = -2; i <= 2; i++)
                    {
                        double value = Sample(Math.Clamp(x + i * spacing, X, X + Width - 1), Math.Clamp(y + j * spacing, Y, Y + Height - 1));
                        if (!double.IsFinite(value)) return double.NaN;
                        samples[index++] = value;
                    }
                return SelectMedian(samples);
            }

            private static double SelectMedian(Span<double> values)
            {
                // Both callers supply exactly 25 finite samples. Partition
                // around the middle order statistic without sorting each half.
                const int middle = 12;
                int left = 0, right = 24;
                while (left < right)
                {
                    double pivot = values[(left + right) / 2];
                    int i = left, j = right;
                    do
                    {
                        while (values[i] < pivot) i++;
                        while (values[j] > pivot) j--;
                        if (i <= j)
                        {
                            (values[i], values[j]) = (values[j], values[i]);
                            i++;
                            j--;
                        }
                    } while (i <= j);
                    if (middle <= j) right = j;
                    else if (middle >= i) left = i;
                    else break;
                }
                return values[middle];
            }

            private double Sample(double x, double y)
            {
                if (x < X || y < Y || x > X + Width - 1 || y > Y + Height - 1) return double.NaN;
                int x0 = (int)Math.Floor(x);
                int y0 = (int)Math.Floor(y);
                int x1 = Math.Min(x0 + 1, X + Width - 1);
                int y1 = Math.Min(y0 + 1, Y + Height - 1);
                double fx = x - x0;
                double fy = y - y0;
                return (Read(x0, y0) * (1 - fx) + Read(x1, y0) * fx) * (1 - fy)
                    + (Read(x0, y1) * (1 - fx) + Read(x1, y1) * fx) * fy;
            }

            private unsafe double Read(int x, int y)
            {
                byte* row = (byte*)image.pData + (long)y * stride;
                int channels = Math.Min(image.channels, 3);
                double sum = 0;
                for (int channel = 0; channel < channels; channel++)
                    sum += image.depth switch
                    {
                        8 => row[x * image.channels + channel],
                        16 => ((ushort*)row)[x * image.channels + channel],
                        _ => ((float*)row)[x]
                    };
                return sum / channels;
            }
        }
    }
}
