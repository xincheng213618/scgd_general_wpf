using System;
using System.Buffers;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using System.Runtime.CompilerServices;
using System.Runtime.ExceptionServices;
using System.Threading.Tasks;

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
        private const int SeedCellsPerWorker = 32 * 1024;
        private const int MaximumWorkers = 12;
        private const int SeedBackground = -2;
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
            int width = (reader.Width - 1) / step + 1;
            int height = (reader.Height - 1) / step + 1;
            int length = checked(width * height);
            int workers = image.depth == 32 ? 1 : Math.Min(MaximumWorkers, Environment.ProcessorCount);
            int seedWorkers = Math.Min(workers, (length - 1) / SeedCellsPerWorker + 1);
            var values = ArrayPool<double>.Shared.Rent(length);
            int[]? labels = null;
            try
            {
                reader.ReadSeedGrid(step, width, height, seedWorkers, values);
                labels = ArrayPool<int>.Shared.Rent(length);
                return Measure(image, reader, width, height, step, workers, values, labels);
            }
            finally
            {
                if (labels != null) ArrayPool<int>.Shared.Return(labels);
                ArrayPool<double>.Shared.Return(values);
            }
        }

        private static (LuminousAreaDetectionResult Detection, FovBoundaryGeometry Geometry) Measure(
            HImage image, ImageReader reader, int width, int height, int step, int workers, double[] values, int[] labels)
        {
            double smoothing = Math.Max(1, step / 2d);
            double plateauDistance = 2 * step + 2 * smoothing;
            int length = width * height;
            ReadOnlySpan<double> grid = values.AsSpan(0, length);
            var (minimum, maximum) = FindRange(grid);
            if (maximum <= minimum) throw new InvalidOperationException("曲边发光区没有可定位的明暗边界。");
            double signalRange = maximum - minimum;
            double minimumDrop = signalRange * 0.01;
            double scoreTolerance = signalRange * 1e-10;
            double contrastFloor = signalRange * 1e-6;
            double brightThreshold = FindOtsuThreshold(grid, minimum, maximum);
            double threshold = FindSeedThreshold(grid, minimum, brightThreshold);
            var pending = new Stack<SeedRun>(width);
            var (largestLabel, largestArea, componentCount) = LabelSeedComponents(grid, threshold, labels, width, pending);
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
            for (int y = 0; y < height; y++)
            {
                var row = labels.AsSpan(y * width, width);
                int first = row.IndexOf(largestLabel);
                if (first < 0) continue;
                int last = row.LastIndexOf(largestLabel);
                ImageEdge seedEdges = ImageEdge.None;
                if (first == 0) seedEdges |= ImageEdge.Left;
                if (y == 0) seedEdges |= ImageEdge.Top;
                if (last == width - 1) seedEdges |= ImageEdge.Right;
                if (y == height - 1) seedEdges |= ImageEdge.Bottom;
                if ((seedEdges & ~imageEdges) != ImageEdge.None)
                    throw new InvalidOperationException("曲边发光区接触图像或搜索区域边缘，无法确认完整边界。");
                touchedEdges |= seedEdges;
                LuminousAreaPoint leftPoint = new(reader.X + first * step, reader.Y + y * step);
                LuminousAreaPoint rightPoint = new(reader.X + last * step, leftPoint.Y);
                minX = Math.Min(minX, leftPoint.X); maxX = Math.Max(maxX, rightPoint.X);
                minY = Math.Min(minY, leftPoint.Y); maxY = Math.Max(maxY, leftPoint.Y);
                for (int corner = 0; corner < 4; corner++)
                {
                    // At fixed Y each diagonal support is monotone in X.
                    LuminousAreaPoint point = corner is 0 or 3 ? leftPoint : rightPoint;
                    double score = Support(point, corner);
                    if (score > support[corner]) { support[corner] = score; corners[corner] = point; }
                }
            }
            ValidateFilledComponent(labels, width, height, largestLabel, largestArea, componentCount, pending);
            LuminousAreaPoint center = new((minX + maxX) / 2, (minY + maxY) / 2);
            if (ComponentIndex(center) < 0) throw new InvalidOperationException("曲边模式要求中心位于连续、填充的发光区内。");

            ImageEdge usedImageEdges = ImageEdge.None;
            int recoveredRays = 0;
            int skippedExtremumProbes = 0;
            Dictionary<double, double> raySamples = new();
            List<LuminousAreaPoint> contour = new(ContourSamples + 4);
            var initialContour = new RayBoundary[ContourSamples];
            var measuredContrasts = new double?[ContourSamples];
            var caches = new Dictionary<double, double>[workers];
            caches[0] = raySamples;
            for (int i = 1; i < workers; i++) caches[i] = new();
            MeasureBatch(ContourSamples, (sample, cache) =>
                initialContour[sample] = FindBoundary(center, 2 * Math.PI * sample / ContourSamples, cache));
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
            var probes = new RayBoundary?[17];
            for (int corner = 0; corner < 4; corner++)
            {
                LuminousAreaPoint seed = corners[corner];
                double angle = Math.Atan2(seed.Y - center.Y, seed.X - center.X);
                double radius = Math.Sqrt(Math.Pow(seed.X - center.X, 2) + Math.Pow(seed.Y - center.Y, 2));
                double searchAngle = Math.Atan2(2 * step, radius);
                LuminousAreaPoint best = contour.MaxBy(point => Support(point, corner));
                MeasureBatch(probes.Length, (index, cache) =>
                    probes[index] = LocateBoundary(center, angle + searchAngle * (index - 8) / 8, cache));
                foreach (RayBoundary? probe in probes)
                {
                    // A probe only refines an extremum already supported by
                    // the measured ring. Keep that extremum if the probe
                    // cannot supply a usable local gradient.
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

            void MeasureBatch(int count, Action<int, Dictionary<double, double>> measure)
            {
                if (workers == 1)
                {
                    for (int i = 0; i < count; i++) measure(i, raySamples);
                    return;
                }

                // Interleave directions so a wide dark or clipped arc does not
                // concentrate the work on one worker. Preserve angular error order.
                var errors = new InvalidOperationException?[count];
                Parallel.For(0, workers, worker =>
                {
                    int i = worker;
                    try
                    {
                        for (; i < count; i += workers) measure(i, caches[worker]);
                    }
                    catch (InvalidOperationException error)
                    {
                        errors[i] = error;
                    }
                });
                foreach (var error in errors)
                    if (error != null) ExceptionDispatchInfo.Throw(error);
            }

            LuminousAreaPoint MeasureBoundary(LuminousAreaPoint origin, double angle)
            {
                RayBoundary boundary = FindBoundary(origin, angle, raySamples);
                if (boundary.IsWeak)
                {
                    // Supplemental rays guide this measurement. They must not
                    // invalidate a ray that already has its own measured edge.
                    try
                    {
                        RayBoundary previous = FindBoundary(origin, angle - RayAngleStep, raySamples);
                        RayBoundary next = FindBoundary(origin, angle + RayAngleStep, raySamples);
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
                    return LocateBoundary(origin, angle, raySamples, exterior) ?? boundary;
                }

                double expectedRadius = (previous.Radius + next.Radius) / 2;
                double tolerance = Math.Max(2 * step, Math.Abs(previous.Radius - next.Radius));
                if (Math.Abs(boundary.Radius - expectedRadius) <= tolerance) return boundary;

                // Neighboring rays only constrain the search. The replacement
                // must be an independently measured gradient on this ray.
                double minimumContrast = Math.Max(WeakEdgeContrast, Math.Min(previous.Contrast, next.Contrast) * NeighborContrastFraction);
                var search = new EdgeSearch(expectedRadius - tolerance, expectedRadius + tolerance, minimumContrast);
                RayBoundary corrected = LocateBoundary(origin, angle, raySamples, search) ?? boundary;
                if (corrected != boundary) recoveredRays++;
                return corrected;
            }

            RayBoundary FindBoundary(LuminousAreaPoint origin, double angle, Dictionary<double, double> samples) =>
                LocateBoundary(origin, angle, samples) ?? throw new InvalidOperationException("曲边发光区存在缺失、裁切或低对比边界。");

            RayBoundary? LocateBoundary(LuminousAreaPoint origin, double angle, Dictionary<double, double> samples, EdgeSearch? search = null)
            {
                samples.Clear();
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
                double contrast = Math.Clamp((inside - outside) / Math.Max(inside - minimum, contrastFloor), 0, 1);
                if (search is { } recovery && (contrast < recovery.MinimumContrast || (recovery.RequireExterior && outside > threshold))) return null;
                return new(new(origin.X + dx * bestRadius, origin.Y + dy * bestRadius), bestRadius, contrast, ImageEdge.None, outside > threshold);

                void Search(double from, double to, double increment)
                {
                    for (double r = from; r <= to; r += increment)
                    {
                        double drop = At(r - smoothing) - At(r + smoothing);
                        double localInside = At(r - plateauDistance);
                        double localOutside = At(r + plateauDistance);
                        double localContrast = Math.Clamp((localInside - localOutside) / Math.Max(localInside - minimum, contrastFloor), 0, 1);
                        if (search is { } guided && !(drop > minimumDrop && localContrast >= guided.MinimumContrast
                            && (!guided.RequireExterior || localOutside <= threshold)))
                            continue;
                        // A text stroke can have a strong gradient without a
                        // sustained light-to-dark transition across the edge.
                        double score = Math.Max(0, drop) * localContrast;
                        if (score > bestScore + scoreTolerance)
                        {
                            bestScore = score;
                            bestDrop = drop;
                            bestRadius = r;
                            ties = 1;
                        }
                        else if (Math.Abs(score - bestScore) <= scoreTolerance)
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
                    if (samples.TryGetValue(distance, out double value)) return value;
                    double x = Math.Clamp(origin.X + dx * distance, reader.X, reader.X + reader.Width - 1);
                    double y = Math.Clamp(origin.Y + dy * distance, reader.Y, reader.Y + reader.Height - 1);
                    value = reader.SampleRobust(x, y, step);
                    samples.Add(distance, value);
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

        private static (int Label, int Area, int Count) LabelSeedComponents(
            ReadOnlySpan<double> values, double threshold, int[] labels, int width, Stack<SeedRun> pending)
        {
            int length = values.Length;
            for (int index = 0; index < length; index++)
                labels[index] = values[index] > threshold ? 0 : SeedBackground;

            int largestLabel = 0, largestArea = 0, label = 0;
            for (int index = 0; index < length; index++)
            {
                if (labels[index] != 0) continue;
                label++;
                // A new component in row-major order has no unvisited neighbour
                // to its left or above. Isolated bright pixels need no flood fill.
                bool rightIsBright = index % width < width - 1 && labels[index + 1] == 0;
                bool belowIsBright = index < length - width && labels[index + width] == 0;
                int area = 1;
                if (rightIsBright || belowIsBright)
                    area = FillSeedRuns(labels, width, length, pending, index, 0, label, includeDiagonals: false);
                else
                    labels[index] = label;
                if (area > largestArea) { largestArea = area; largestLabel = label; }
            }
            return (largestLabel, largestArea, label);
        }

        private static void ValidateFilledComponent(int[] labels, int width, int height, int largestLabel,
            int largestArea, int componentCount, Stack<SeedRun> pending)
        {
            int length = width * height;
            // Every pixel outside the selected component participates in the
            // eight-connected exterior, including other isolated bright areas.
            if (componentCount > 1)
                for (int i = 0; i < length; i++)
                    if (labels[i] != largestLabel) labels[i] = SeedBackground;
            int exteriorArea = 0;
            for (int x = 0; x < width; x++)
            {
                VisitExterior(x);
                VisitExterior(length - width + x);
            }
            for (int y = 1; y < height - 1; y++)
            {
                VisitExterior(y * width);
                VisitExterior((y + 1) * width - 1);
            }
            if (largestArea + exteriorArea != length)
                throw new InvalidOperationException("曲边模式要求连续填充图案，当前轮廓含可分辨的孔洞。");

            void VisitExterior(int index)
            {
                if (labels[index] == SeedBackground)
                    exteriorArea += FillSeedRuns(labels, width, length, pending, index, SeedBackground, -1, includeDiagonals: true);
            }
        }

        private readonly record struct SeedRun(int Row, int Left, int Right);

        private static int FillSeedRuns(int[] labels, int width, int length, Stack<SeedRun> pending, int start, int from, int replacement, bool includeDiagonals)
        {
            int diagonal = includeDiagonals ? 1 : 0;
            int lastRow = length - width;
            int count = 0;
            int column = start % width;
            SeedRun run = ClaimRun(start - column, column);
            do
            {
                int left = Math.Max(0, run.Left - diagonal);
                int right = Math.Min(width - 1, run.Right + diagonal);
                if (run.Row >= width) FindRuns(run.Row - width, left, right);
                if (run.Row < lastRow) FindRuns(run.Row + width, left, right);
            } while (pending.TryPop(out run));
            return count;

            SeedRun ClaimRun(int row, int column)
            {
                int left = labels.AsSpan(row, column).LastIndexOfAnyExcept(from) + 1;
                int nextColumn = column + 1;
                int after = labels.AsSpan(row + nextColumn, width - nextColumn).IndexOfAnyExcept(from);
                int right = after < 0 ? width - 1 : column + after;
                int runLength = right - left + 1;
                labels.AsSpan(row + left, runLength).Fill(replacement);
                count += runLength;
                return new(row, left, right);
            }

            void FindRuns(int row, int left, int right)
            {
                while (left <= right)
                {
                    int next = labels.AsSpan(row + left, right - left + 1).IndexOf(from);
                    if (next < 0) return;
                    SeedRun claimed = ClaimRun(row, left + next);
                    pending.Push(claimed);
                    left = claimed.Right + 1;
                }
            }
        }

        private static double Support(LuminousAreaPoint point, int corner) => corner switch
        {
            0 => -point.X - point.Y,
            1 => point.X - point.Y,
            2 => point.X + point.Y,
            _ => -point.X + point.Y
        };

        private static (double Min, double Max) FindRange(ReadOnlySpan<double> values)
        {
            // Seed values have already been checked for non-finite pixels.
            var minima = new Vector<double>(values[0]);
            var maxima = minima;
            var vectors = MemoryMarshal.Cast<double, Vector<double>>(values);
            foreach (var vector in vectors)
            {
                minima = Vector.Min(minima, vector);
                maxima = Vector.Max(maxima, vector);
            }

            double min = minima[0], max = maxima[0];
            for (int i = 1; i < Vector<double>.Count; i++)
            {
                min = Math.Min(min, minima[i]);
                max = Math.Max(max, maxima[i]);
            }
            foreach (double value in values[(vectors.Length * Vector<double>.Count)..])
            {
                min = Math.Min(min, value);
                max = Math.Max(max, value);
            }
            return (min, max);
        }

        private static double FindSeedThreshold(ReadOnlySpan<double> values, double minimum, double brightThreshold)
        {
            // Split the original dark class once more to recover dim corners.
            // Keep the seed above measured background noise; splitting noise
            // itself must not grow the component throughout the background.
            double maximum = FindDarkMaximum(values, minimum, brightThreshold);
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

        private static double FindDarkMaximum(ReadOnlySpan<double> values, double minimum, double brightThreshold)
        {
            double result = minimum;
            if (Vector.IsHardwareAccelerated)
            {
                var initial = new Vector<double>(minimum);
                var maximum = initial;
                var upper = new Vector<double>(brightThreshold);
                var vectors = MemoryMarshal.Cast<double, Vector<double>>(values);
                foreach (var vector in vectors)
                    maximum = Vector.Max(maximum, Vector.ConditionalSelect(Vector.LessThanOrEqual(vector, upper), vector, initial));
                for (int lane = 0; lane < Vector<double>.Count; lane++)
                    result = Math.Max(result, maximum[lane]);
                values = values[(vectors.Length * Vector<double>.Count)..];
            }
            foreach (double value in values)
                if (value <= brightThreshold) result = Math.Max(result, value);
            return result;
        }

        private static double FindOtsuThreshold(ReadOnlySpan<double> values, double minimum, double maximum)
        {
            Span<int> histogram = stackalloc int[256];
            histogram.Clear();
            double range = maximum - minimum;
            int totalCount;
            if (Avx.IsSupported && values.Length >= 256)
            {
                totalCount = FillOtsuHistogramAvx(values, minimum, maximum, histogram);
            }
            else
            {
                totalCount = 0;
                foreach (double value in values)
                {
                    if (value > maximum) continue;
                    histogram[(int)((value - minimum) / range * 255)]++;
                    totalCount++;
                }
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

        private static int FillOtsuHistogramAvx(ReadOnlySpan<double> values, double minimum, double maximum, Span<int> histogram)
        {
            const int binCount = 256;
            const int bankLength = binCount + 1;
            // Each lane updates its own histogram to avoid dependencies at frequent bins.
            // The last bin in each bank absorbs values outside the dark-class bound.
            Span<int> banks = stackalloc int[bankLength * 4];
            banks.Clear();
            double range = maximum - minimum;
            var lower = Vector256.Create(minimum);
            var upper = Vector256.Create(maximum);
            var divisor = Vector256.Create(range);
            var scale = Vector256.Create(255d);
            var rejected = Vector256.Create((double)binCount);

            int i = 0;
            ref double start = ref MemoryMarshal.GetReference(values);
            for (; i <= values.Length - Vector256<double>.Count; i += Vector256<double>.Count)
            {
                var samples = Vector256.LoadUnsafe(ref start, (nuint)i);
                var scaled = Avx.Multiply(Avx.Divide(Avx.Subtract(samples, lower), divisor), scale);
                var accepted = Avx.CompareLessThanOrEqual(samples, upper);
                var bins = Avx.ConvertToVector128Int32WithTruncation(Avx.BlendVariable(rejected, scaled, accepted));
                banks[bins.GetElement(0)]++;
                banks[bankLength + bins.GetElement(1)]++;
                banks[2 * bankLength + bins.GetElement(2)]++;
                banks[3 * bankLength + bins.GetElement(3)]++;
            }

            for (; i < values.Length; i++)
                if (values[i] <= maximum) banks[(int)((values[i] - minimum) / range * 255)]++;

            int totalCount = 0;
            for (int bin = 0; bin < binCount; bin++)
            {
                int count = banks[bin] + banks[bin + bankLength] + banks[bin + 2 * bankLength] + banks[bin + 3 * bankLength];
                histogram[bin] = count;
                totalCount += count;
            }
            return totalCount;
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

            public void ReadSeedGrid(int step, int width, int height, int workers, double[] values)
            {
                if (workers == 1)
                {
                    ReadSeedRows(step, width, 0, height, values);
                    return;
                }

                // Each stripe reads its own halo and writes disjoint seed rows.
                // Limit CPU use when measurement runs alongside other nodes.
                ImageReader reader = this;
                Parallel.For(0, workers, worker =>
                    reader.ReadSeedRows(step, width, worker * height / workers, (worker + 1) * height / workers, values));
            }

            private unsafe void ReadSeedRows(int step, int width, int startRow, int endRow, double[] values)
            {
                int rowWidth = width + 4;
                double[] rows = new double[5 * rowWidth];
                Span<double> samples = stackalloc double[25];
                Span<int> windowOffsets = stackalloc int[5];
                int depth = image.depth, channels = image.channels;
                bool gray = channels == 1;
                var columnOffsets = new int[rowWidth];
                for (int i = 0; i < rowWidth; i++)
                    columnOffsets[i] = (X + (int)Math.Clamp((long)(i - 2) * step, 0, Width - 1)) * channels;
                // Adjacent seed windows share 20 of 25 pixels. Keep five source
                // rows with a two-sample halo clamped to the actual ROI edge.
                for (int row = startRow; row < endRow + 4; row++)
                {
                    int offset = row % 5 * rowWidth;
                    int sourceY = Y + (int)Math.Clamp((long)(row - 2) * step, 0, Height - 1);
                    byte* sourceRow = (byte*)image.pData + (long)sourceY * stride;
                    if (depth == 32)
                    {
                        for (int x = 0; x < rowWidth; x++)
                        {
                            double value = ReadFloatSeed(columnOffsets[x], sourceY);
                            if (!double.IsFinite(value)) throw new InvalidOperationException("曲边发光区包含非有限像素值。");
                            rows[offset + x] = value;
                        }
                    }
                    else
                    {
                        for (int x = 0; x < rowWidth; x++)
                            rows[offset + x] = ReadPixel(sourceRow, columnOffsets[x], depth, gray);
                    }
                    if (row < startRow + 4) continue;
                    int y = row - 4;
                    for (int j = 0; j < windowOffsets.Length; j++)
                        windowOffsets[j] = (y + j) % 5 * rowWidth;
                    if (image.depth != 32 && FovSeedMedian.TryWriteRow(
                        rows.AsSpan(windowOffsets[0], rowWidth), rows.AsSpan(windowOffsets[1], rowWidth),
                        rows.AsSpan(windowOffsets[2], rowWidth), rows.AsSpan(windowOffsets[3], rowWidth),
                        rows.AsSpan(windowOffsets[4], rowWidth), values.AsSpan(y * width, width)))
                        continue;
                    for (int x = 0; x < width; x++)
                    {
                        for (int j = 0; j < 5; j++)
                            rows.AsSpan(windowOffsets[j] + x, 5).CopyTo(samples.Slice(j * 5, 5));
                        values[y * width + x] = SelectMedian(samples);
                    }
                }
            }

            public unsafe double SampleRobust(double x, double y, double spacing)
            {
                int right = X + Width - 1;
                int bottom = Y + Height - 1;
                if (x < X || y < Y || x > right || y > bottom) return double.NaN;
                int depth = image.depth;
                int channels = image.channels;
                bool gray = channels == 1;
                Span<int> leftOffset = stackalloc int[5];
                Span<int> rightOffset = stackalloc int[5];
                Span<double> horizontalFraction = stackalloc double[5];
                for (int i = 0; i < 5; i++)
                {
                    double localX = Math.Clamp(x + (i - 2) * spacing, X, right);
                    int left = (int)localX;
                    leftOffset[i] = left * channels;
                    rightOffset[i] = Math.Min(left + 1, right) * channels;
                    horizontalFraction[i] = localX - left;
                }

                byte* data = (byte*)image.pData;
                Span<double> samples = stackalloc double[25];
                Span<double> previousBottom = stackalloc double[5];
                int previousBottomY = -1;
                int index = 0;
                for (int j = -2; j <= 2; j++)
                {
                    double localY = Math.Clamp(y + j * spacing, Y, bottom);
                    int topY = (int)localY;
                    int bottomY = Math.Min(topY + 1, bottom);
                    double verticalFraction = localY - topY;
                    byte* row0 = data + (long)topY * stride;
                    byte* row1 = data + (long)bottomY * stride;
                    bool reuseTop = topY == previousBottomY;

                    for (int i = 0; i < 5; i++)
                    {
                        int x0 = leftOffset[i];
                        int x1 = rightOffset[i];
                        double fraction = horizontalFraction[i];
                        double topValue = reuseTop ? previousBottom[i] : Blend(row0, x0, x1, fraction, depth, gray);
                        double bottomValue = bottomY == topY ? topValue : Blend(row1, x0, x1, fraction, depth, gray);
                        double value = topValue * (1 - verticalFraction) + bottomValue * verticalFraction;
                        if (depth == 32 && !double.IsFinite(value)) return double.NaN;
                        samples[index++] = value;
                        previousBottom[i] = bottomValue;
                    }
                    previousBottomY = bottomY;
                }
                return SelectMedian(samples);
            }

            private unsafe double ReadFloatSeed(int x, int y)
            {
                byte* data = (byte*)image.pData;
                byte* row = data + (long)y * stride;

                // The float sampler considers every bilinear neighbour, even
                // when its weight is zero. Preserve non-finite propagation.
                int right = Math.Min(x + 1, X + Width - 1);
                int bottom = Math.Min(y + 1, Y + Height - 1);
                byte* nextRow = data + (long)bottom * stride;
                return Blend(row, x, right, 0, 32, true) * 1d + Blend(nextRow, x, right, 0, 32, true) * 0d;
            }

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe double Blend(byte* row, int x0, int x1, double fraction, int depth, bool gray) =>
                ReadPixel(row, x0, depth, gray) * (1 - fraction) + ReadPixel(row, x1, depth, gray) * fraction;

            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            private static unsafe double ReadPixel(byte* row, int offset, int depth, bool gray)
            {
                if (depth == 16)
                {
                    ushort* words = (ushort*)row;
                    return gray ? words[offset] : (words[offset] + words[offset + 1] + words[offset + 2]) / 3d;
                }
                if (depth == 8)
                    return gray ? row[offset] : (row[offset] + row[offset + 1] + row[offset + 2]) / 3d;

                // Match the original mean's starting accumulator for -0f.
                return 0d + ((float*)row)[offset];
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
        }
    }
}
