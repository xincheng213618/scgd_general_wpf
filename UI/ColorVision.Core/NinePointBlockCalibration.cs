using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

namespace ColorVision.Core
{
    public sealed class NinePointBlockCalibrationOptions
    {
        public int SearchRadius { get; init; } = 160;
        public double MaximumShift { get; init; } = 80;
        public double MinimumConfidence { get; init; } = 0.25;
    }

    public sealed class NinePointBlockCalibrationPoint
    {
        public NinePointBlockCalibrationPoint(int index, LuminousAreaPoint seed, LuminousAreaPoint center,
            IReadOnlyList<LuminousAreaPoint> corners, double confidence, bool success = true, string failureReason = "")
        {
            Index = index;
            Seed = seed;
            Center = success ? center : seed;
            Corners = Array.AsReadOnly(corners.ToArray());
            Confidence = confidence;
            Success = success;
            FailureReason = failureReason;
        }

        /// <summary>The zero-based position in the supplied seed list.</summary>
        public int Index { get; }
        public LuminousAreaPoint Seed { get; }
        public LuminousAreaPoint Center { get; }
        /// <summary>Full-image coordinates in LT, RT, RB, LB order.</summary>
        public IReadOnlyList<LuminousAreaPoint> Corners { get; }
        public double Confidence { get; }
        public bool Success { get; }
        public string FailureReason { get; }
    }

    /// <summary>
    /// Refines nine approximate positions against nine separate luminous blocks.
    /// Each unavailable or unreliable block retains its supplied seed position.
    /// Invalid inputs, cancellation and native infrastructure failures still throw.
    /// </summary>
    public static class NinePointBlockCalibrator
    {
        private static readonly string[] SideNames = ["Top", "Right", "Bottom", "Left"];

        public static IReadOnlyList<NinePointBlockCalibrationPoint> Refine(HImage image,
            IReadOnlyList<LuminousAreaPoint> seeds, NinePointBlockCalibrationOptions options,
            CancellationToken cancellationToken = default) =>
            RefineWithDetector(image, seeds, options, LuminousAreaNative.DetectV2, cancellationToken);

        // Keep rejection/lifecycle checks deterministic without requiring native loading in their tests.
        internal static IReadOnlyList<NinePointBlockCalibrationPoint> RefineWithDetector(HImage image,
            IReadOnlyList<LuminousAreaPoint> seeds, NinePointBlockCalibrationOptions options,
            Func<HImage, RoiRect, double, LuminousAreaDetectionResult> detector,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(seeds);
            ArgumentNullException.ThrowIfNull(options);
            ArgumentNullException.ThrowIfNull(detector);
            cancellationToken.ThrowIfCancellationRequested();
            ValidateImage(image);
            if (seeds.Count != 9) throw new ArgumentException("九点校准必须提供恰好 9 个初始点。", nameof(seeds));
            if (options.SearchRadius <= 0 || options.SearchRadius > (int.MaxValue - 1) / 2)
                throw new ArgumentOutOfRangeException(nameof(options), "搜索半径必须是有效的正整数。");
            if (!double.IsFinite(options.MaximumShift) || options.MaximumShift <= 0 || options.MaximumShift > options.SearchRadius)
                throw new ArgumentOutOfRangeException(nameof(options), "最大校准位移必须大于 0 且不超过搜索半径。");
            if (!double.IsFinite(options.MinimumConfidence) || options.MinimumConfidence < 0 || options.MinimumConfidence > 1)
                throw new ArgumentOutOfRangeException(nameof(options), "最小置信度必须在 0 到 1 之间。");

            LuminousAreaPoint[] seedSnapshot = seeds.ToArray();
            RoiRect[] regions = new RoiRect[seedSnapshot.Length];
            for (int index = 0; index < seedSnapshot.Length; index++)
            {
                LuminousAreaPoint seed = seedSnapshot[index];
                if (!double.IsFinite(seed.X) || !double.IsFinite(seed.Y) || seed.X < 0 || seed.Y < 0
                    || seed.X >= image.cols || seed.Y >= image.rows)
                    throw new ArgumentException($"第 {index + 1} 点的初始坐标无效或超出图像。", nameof(seeds));
                long x = (long)Math.Floor(seed.X) - options.SearchRadius;
                long y = (long)Math.Floor(seed.Y) - options.SearchRadius;
                int size = options.SearchRadius * 2 + 1;
                if (x >= 0 && y >= 0 && x + size <= image.cols && y + size <= image.rows)
                    regions[index] = new RoiRect((int)x, (int)y, size, size);
                for (int previous = 0; previous < index; previous++)
                    if (seed == seedSnapshot[previous]) throw new ArgumentException("九点校准的初始点不能重复。", nameof(seeds));
            }

            NinePointBlockCalibrationPoint[] results = new NinePointBlockCalibrationPoint[seedSnapshot.Length];
            IReadOnlyList<LuminousAreaPoint>?[] candidates = new IReadOnlyList<LuminousAreaPoint>?[seedSnapshot.Length];
            for (int index = 0; index < seedSnapshot.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                RoiRect region = regions[index];
                if (region.Width == 0)
                {
                    results[index] = Failed(index, seedSnapshot[index], "搜索区域超出原图。");
                    continue;
                }
                LuminousAreaDetectionResult detection = detector(image, region, options.MinimumConfidence);
                cancellationToken.ThrowIfCancellationRequested();
                ThrowIfInfrastructureFailure(index, detection);
                if (!detection.Success || !detection.HasValidCorners)
                {
                    results[index] = Failed(index, seedSnapshot[index], $"亮块四边定位失败：{detection.FailureReason}。", detection);
                    continue;
                }
                if (detection.Warnings.Any(IsBlockingWarning))
                {
                    results[index] = Failed(index, seedSnapshot[index], $"亮块存在不完整边界或多候选：{string.Join(", ", detection.Warnings)}。", detection);
                    continue;
                }
                if (!detection.Confidence.HasValue || !double.IsFinite(detection.Confidence.Value)
                    || detection.Confidence.Value < options.MinimumConfidence || detection.Confidence.Value > 1
                    || !HasMeasuredSides(detection.SideQuality))
                {
                    results[index] = Failed(index, seedSnapshot[index], "亮块没有满足置信度要求的四条实测边。", detection);
                    continue;
                }
                if (detection.Corners.Any(point => point.X <= region.X || point.Y <= region.Y
                    || point.X >= (long)region.X + region.Width - 1 || point.Y >= (long)region.Y + region.Height - 1))
                {
                    results[index] = Failed(index, seedSnapshot[index], "亮块触及搜索区域边界。", detection);
                    continue;
                }

                candidates[index] = detection.Corners;
                LuminousAreaPoint center = IntersectDiagonals(detection.Corners);
                double shiftSquared = DistanceSquared(center, seedSnapshot[index]);
                if (!double.IsFinite(shiftSquared) || shiftSquared > options.MaximumShift * options.MaximumShift)
                {
                    results[index] = Failed(index, seedSnapshot[index], "亮块中心超出最大校准位移。", detection);
                    continue;
                }
                bool belongsToAnotherSeed = false;
                for (int other = 0; other < seedSnapshot.Length; other++)
                    if (other != index && DistanceSquared(center, seedSnapshot[other]) <= shiftSquared)
                        belongsToAnotherSeed = true;
                if (belongsToAnotherSeed)
                {
                    results[index] = Failed(index, seedSnapshot[index], "亮块中心更接近其他初始点，无法确认点位对应关系。", detection);
                    continue;
                }
                results[index] = new NinePointBlockCalibrationPoint(index, seedSnapshot[index], center,
                    detection.Corners, detection.Confidence.Value);
            }

            // Resolve collisions after all detections so both affected points fall back,
            // regardless of which seed happened to be processed first.
            for (int index = 0; index < results.Length; index++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (candidates[index] is not { } current) continue;
                for (int previous = 0; previous < index; previous++)
                {
                    if (candidates[previous] is not { } earlier || !Overlaps(current, earlier)) continue;
                    results[index] = Failed(index, seedSnapshot[index], $"与第 {previous + 1} 点重复命中或亮块区域重叠。");
                    results[previous] = Failed(previous, seedSnapshot[previous], $"与第 {index + 1} 点重复命中或亮块区域重叠。");
                }
            }
            cancellationToken.ThrowIfCancellationRequested();
            return Array.AsReadOnly(results);
        }

        private static void ValidateImage(HImage image)
        {
            int bytes = image.depth switch { 8 => 1, 16 => 2, 32 => 4, 64 => 8, _ => 0 };
            if (image.pData == IntPtr.Zero || image.cols <= 0 || image.rows <= 0
                || image.channels is not (1 or 3 or 4) || bytes == 0
                || image.stride < (long)image.cols * image.channels * bytes)
                throw new ArgumentException("九点校准需要有效的原图像素及行步长。", nameof(image));
        }

        private static bool HasMeasuredSides(IReadOnlyList<LuminousAreaSideQuality> sides)
        {
            if (sides.Count != SideNames.Length) return false;
            for (int index = 0; index < sides.Count; index++)
            {
                LuminousAreaSideQuality side = sides[index];
                if (!string.Equals(side.Side, SideNames[index], StringComparison.Ordinal)
                    || side.Score is not double score || !double.IsFinite(score) || score <= 0 || score > 1
                    || !PositiveMetric(side, "Coverage") || !PositiveMetric(side, "InlierCount")
                    || !PositiveMetric(side, "SampleCount") || !PositiveMetric(side, "ContrastP10")) return false;
            }
            return true;
        }

        private static bool PositiveMetric(LuminousAreaSideQuality side, string name) =>
            side.Metrics.TryGetValue(name, out double value) && double.IsFinite(value) && value > 0;

        private static LuminousAreaPoint IntersectDiagonals(IReadOnlyList<LuminousAreaPoint> corners)
        {
            LuminousAreaPoint a = corners[0], b = corners[1];
            double ax = corners[2].X - a.X, ay = corners[2].Y - a.Y;
            double bx = corners[3].X - b.X, by = corners[3].Y - b.Y;
            double denominator = ax * by - ay * bx;
            double t = ((b.X - a.X) * by - (b.Y - a.Y) * bx) / denominator;
            return new LuminousAreaPoint(a.X + t * ax, a.Y + t * ay);
        }

        private static double DistanceSquared(LuminousAreaPoint a, LuminousAreaPoint b) =>
            (a.X - b.X) * (a.X - b.X) + (a.Y - b.Y) * (a.Y - b.Y);

        private static bool Overlaps(IReadOnlyList<LuminousAreaPoint> first, IReadOnlyList<LuminousAreaPoint> second) =>
            !HasSeparatingEdge(first, second) && !HasSeparatingEdge(second, first);

        private static bool HasSeparatingEdge(IReadOnlyList<LuminousAreaPoint> first, IReadOnlyList<LuminousAreaPoint> second)
        {
            for (int index = 0; index < first.Count; index++)
            {
                LuminousAreaPoint a = first[index], b = first[(index + 1) % first.Count];
                double nx = a.Y - b.Y, ny = b.X - a.X;
                double firstMin = double.PositiveInfinity, firstMax = double.NegativeInfinity;
                double secondMin = double.PositiveInfinity, secondMax = double.NegativeInfinity;
                foreach (LuminousAreaPoint point in first)
                {
                    double projection = point.X * nx + point.Y * ny;
                    firstMin = Math.Min(firstMin, projection);
                    firstMax = Math.Max(firstMax, projection);
                }
                foreach (LuminousAreaPoint point in second)
                {
                    double projection = point.X * nx + point.Y * ny;
                    secondMin = Math.Min(secondMin, projection);
                    secondMax = Math.Max(secondMax, projection);
                }
                if (firstMax < secondMin || secondMax < firstMin) return true;
            }
            return false;
        }

        private static NinePointBlockCalibrationPoint Failed(int index, LuminousAreaPoint seed, string reason,
            LuminousAreaDetectionResult? detection = null) =>
            new(index, seed, seed, detection?.Corners ?? Array.Empty<LuminousAreaPoint>(), detection?.Confidence ?? 0, false, reason);

        private static bool IsBlockingWarning(string warning) =>
            warning.StartsWith("Inferred", StringComparison.Ordinal) || warning.StartsWith("Weak", StringComparison.Ordinal)
            || warning.StartsWith("Partial", StringComparison.Ordinal) || warning.StartsWith("ComparableCandidateCount=", StringComparison.Ordinal)
            || warning is "CandidateTouchesImageBorder" or "AmbiguousCandidates" or "MultipleComparableCandidates";

        private static void ThrowIfInfrastructureFailure(int index, LuminousAreaDetectionResult detection)
        {
            if (detection.FailureReason is "NativeLibraryUnavailable" or "NativeEntryPointUnavailable" or "NativeLibraryIncompatible"
                or "NativeCallFailed" or "ManagedInteropFailed" or "ResultParseFailed" or "InvalidConfiguration" or "UnsupportedImage")
                throw new InvalidOperationException($"九点校准第 {index + 1} 点调用失败：{detection.FailureReason}。{detection.Diagnostic}");
        }
    }
}
