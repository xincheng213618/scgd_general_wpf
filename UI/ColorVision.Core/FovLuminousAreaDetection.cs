using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace ColorVision.Core
{
    /// <summary>
    /// Experimental isophote fit, not a standard-compliant FOV measurement.
    /// Refines a coarse luminous-area quadrilateral to the configured fraction of
    /// the centre luminance. RobustV2 supplies only the search seed; the returned
    /// corners belong to the luminance criterion boundary.
    /// </summary>
    public static class FovLuminousAreaDetector
    {
        public const string AlgorithmName = "FovLuminanceBoundary";
        public const double DefaultBoundaryRatio = 0.5;
        private const int SideSampleCount = 15;
        private const double BoundaryStraightnessReferenceRatio = 0.01;

        public static void ValidateBoundaryRatio(double luminanceBoundaryRatio)
        {
            if (!double.IsFinite(luminanceBoundaryRatio) || luminanceBoundaryRatio <= 0 || luminanceBoundaryRatio >= 1)
                throw new ArgumentOutOfRangeException(nameof(luminanceBoundaryRatio), "Luminance boundary ratio must be between 0 and 1.");
        }

        public static LuminousAreaDetectionResult Detect(
            HImage image,
            RoiRect roi,
            double minimumConfidence,
            double luminanceBoundaryRatio = DefaultBoundaryRatio)
        {
            if (!TryValidateConfiguration(minimumConfidence, luminanceBoundaryRatio, out string configurationError))
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidConfiguration", diagnostic: configurationError);

            LuminousAreaDetectionResult coarse = LuminousAreaNative.DetectV2(image, roi, minimumConfidence);
            if (!coarse.Success || !coarse.HasValidCorners)
            {
                string reason = string.IsNullOrWhiteSpace(coarse.FailureReason) ? "NoCandidate" : coarse.FailureReason;
                return LuminousAreaDetectionResult.CreateFailure(
                    AlgorithmName,
                    reason,
                    coarse.NativeReturnCode,
                    $"RobustV2 coarse localization failed. {coarse.Diagnostic}",
                    coarse.RawJson);
            }

            return Refine(image, roi, coarse.Corners, luminanceBoundaryRatio, coarse);
        }

        internal static LuminousAreaDetectionResult Refine(
            HImage image,
            RoiRect roi,
            IReadOnlyList<LuminousAreaPoint> coarseCorners,
            double luminanceBoundaryRatio,
            LuminousAreaDetectionResult? coarseDetection = null)
        {
            ArgumentNullException.ThrowIfNull(coarseCorners);
            try
            {
                ValidateBoundaryRatio(luminanceBoundaryRatio);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidConfiguration", diagnostic: exception.Message);
            }
            if (!TryResolveImageLayout(image, roi, out ImageLayout layout, out string imageError))
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "UnsupportedImage", diagnostic: imageError);
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(coarseCorners, out string geometryError))
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidGeometry", diagnostic: $"Coarse corners are invalid: {geometryError}");
            if (coarseCorners.Any(point => !layout.Contains(point.X, point.Y)))
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidGeometry", diagnostic: "Coarse corners fall outside the image or selected ROI.");

            LuminousAreaPoint center = FindCenter(coarseCorners);
            if (!layout.Contains(center.X, center.Y))
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidGeometry", diagnostic: "The coarse quadrilateral centre is outside the image or selected ROI.");

            double minimumSpan = Math.Min(
                (Distance(coarseCorners[0], coarseCorners[1]) + Distance(coarseCorners[3], coarseCorners[2])) / 2,
                (Distance(coarseCorners[0], coarseCorners[3]) + Distance(coarseCorners[1], coarseCorners[2])) / 2);
            double referenceLuminance = MeasureCentreLuminance(image, layout, center, minimumSpan);
            if (!double.IsFinite(referenceLuminance) || referenceLuminance <= 0)
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "ReferenceLuminanceUnavailable", diagnostic: "No finite positive luminance was measured at the luminous-area centre.");

            double boundaryLuminance = referenceLuminance * luminanceBoundaryRatio;
            LineFit[] sides = new LineFit[4];
            LuminousAreaSideQuality[] sideQuality = new LuminousAreaSideQuality[4];
            string[] sideNames = ["Top", "Right", "Bottom", "Left"];
            for (int sideIndex = 0; sideIndex < sides.Length; sideIndex++)
            {
                LuminousAreaPoint sideStart = coarseCorners[sideIndex];
                LuminousAreaPoint sideEnd = coarseCorners[(sideIndex + 1) % coarseCorners.Count];
                List<LuminousAreaPoint> boundaryPoints = FindSideBoundaryPoints(
                    image,
                    layout,
                    center,
                    sideStart,
                    sideEnd,
                    boundaryLuminance);
                if (boundaryPoints.Count < Math.Max(5, SideSampleCount / 2))
                {
                    return LuminousAreaDetectionResult.CreateFailure(
                        AlgorithmName,
                        "BoundaryNotFound",
                        diagnostic: $"{sideNames[sideIndex]} found {boundaryPoints.Count}/{SideSampleCount} persistent luminance crossings.");
                }
                if (!TryFitLine(boundaryPoints, out LineFit fitted))
                    return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidGeometry", diagnostic: $"{sideNames[sideIndex]} luminance boundary could not be fitted.");

                sides[sideIndex] = fitted;
                double coverage = boundaryPoints.Count / (double)SideSampleCount;
                double sideLength = Distance(sideStart, sideEnd);
                double fitRmsFraction = fitted.RootMeanSquareError / Math.Max(sideLength, 1);
                double score = coverage / (1 + fitRmsFraction / BoundaryStraightnessReferenceRatio);
                sideQuality[sideIndex] = new LuminousAreaSideQuality(
                    sideNames[sideIndex],
                    Math.Clamp(score, 0, 1),
                    new ReadOnlyDictionary<string, double>(new Dictionary<string, double>
                    {
                        ["BoundaryRatio"] = luminanceBoundaryRatio,
                        ["ReferenceLuminance"] = referenceLuminance,
                        ["BoundaryLuminance"] = boundaryLuminance,
                        ["CrossingCount"] = boundaryPoints.Count,
                        ["FitRmsPixels"] = fitted.RootMeanSquareError,
                        ["SideLengthPixels"] = sideLength,
                        ["FitRmsFraction"] = fitRmsFraction
                    }));
            }

            LuminousAreaPoint[] refinedCorners =
            [
                Intersect(sides[0], sides[3]),
                Intersect(sides[0], sides[1]),
                Intersect(sides[2], sides[1]),
                Intersect(sides[2], sides[3])
            ];
            if (refinedCorners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)
                || !layout.Contains(point.X, point.Y, 2)))
            {
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidGeometry", diagnostic: "Refined luminance-boundary corners fall outside the search region.");
            }
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(refinedCorners, out geometryError))
                return LuminousAreaDetectionResult.CreateFailure(AlgorithmName, "InvalidGeometry", diagnostic: $"Refined corners are invalid: {geometryError}");

            double coarseConfidence = coarseDetection?.Confidence is double value && double.IsFinite(value)
                ? Math.Clamp(value, 0, 1)
                : 1;
            double boundaryConfidence = sideQuality.Min(side => side.Score ?? 0);
            double confidence = Math.Min(coarseConfidence, boundaryConfidence);
            string[] warnings = sideQuality
                .Where(side => side.Score < 0.5)
                .Select(side => $"Weak{side.Side}Boundary")
                .ToArray();
            return new LuminousAreaDetectionResult(
                true,
                AlgorithmName,
                refinedCorners,
                confidence,
                sideQuality,
                string.Empty,
                warnings,
                nativeReturnCode: coarseDetection?.NativeReturnCode ?? 0,
                diagnostic: $"ReferenceLuminance={referenceLuminance:G12}; BoundaryRatio={luminanceBoundaryRatio:G12}; BoundaryLuminance={boundaryLuminance:G12}; BoundaryConfidence={boundaryConfidence:G12}; CoarseConfidence={coarseConfidence:G12}.");
        }

        private static bool TryValidateConfiguration(double minimumConfidence, double luminanceBoundaryRatio, out string error)
        {
            if (!double.IsFinite(minimumConfidence) || minimumConfidence < 0 || minimumConfidence > 1)
            {
                error = "Minimum confidence must be within [0, 1].";
                return false;
            }
            try
            {
                ValidateBoundaryRatio(luminanceBoundaryRatio);
            }
            catch (ArgumentOutOfRangeException exception)
            {
                error = exception.Message;
                return false;
            }
            error = string.Empty;
            return true;
        }

        private static bool TryResolveImageLayout(HImage image, RoiRect roi, out ImageLayout layout, out string error)
        {
            layout = default;
            if (image.pData == IntPtr.Zero || image.cols <= 0 || image.rows <= 0)
            {
                error = "Image data and dimensions are required.";
                return false;
            }
            bool supported = image.depth switch
            {
                8 or 16 => image.channels is 1 or 3 or 4,
                32 => image.channels == 1,
                _ => false
            };
            if (!supported)
            {
                error = $"Only 8/16-bit interleaved 1/3/4-channel images and 32-bit single-channel float images are supported; current image is {image.depth}-bit with {image.channels} channel(s).";
                return false;
            }
            int bytesPerChannel = image.depth / 8;
            int packedStride;
            try { packedStride = checked(image.cols * image.channels * bytesPerChannel); }
            catch (OverflowException)
            {
                error = "Image row size exceeds the supported range.";
                return false;
            }
            int stride = image.stride > 0 ? image.stride : packedStride;
            if (stride < packedStride)
            {
                error = "Image stride is smaller than the packed row size.";
                return false;
            }

            int x = roi.Width > 0 && roi.Height > 0 ? roi.X : 0;
            int y = roi.Width > 0 && roi.Height > 0 ? roi.Y : 0;
            int width = roi.Width > 0 && roi.Height > 0 ? roi.Width : image.cols;
            int height = roi.Width > 0 && roi.Height > 0 ? roi.Height : image.rows;
            if (x < 0 || y < 0 || width <= 0 || height <= 0
                || (long)x + width > image.cols || (long)y + height > image.rows)
            {
                error = "The search ROI must be empty for the full image or a positive rectangle inside the image.";
                return false;
            }
            layout = new ImageLayout(x, y, width, height, stride);
            error = string.Empty;
            return true;
        }

        private static double MeasureCentreLuminance(HImage image, ImageLayout layout, LuminousAreaPoint center, double minimumSpan)
        {
            int radius = (int)Math.Clamp(Math.Round(minimumSpan * 0.01), 2, 32);
            int left = Math.Max(layout.X, (int)Math.Floor(center.X) - radius);
            int right = Math.Min(layout.Right, (int)Math.Ceiling(center.X) + radius);
            int top = Math.Max(layout.Y, (int)Math.Floor(center.Y) - radius);
            int bottom = Math.Min(layout.Bottom, (int)Math.Ceiling(center.Y) + radius);
            List<double> samples = new((right - left + 1) * (bottom - top + 1));
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    double luminance = ReadLuminance(image, layout.Stride, x, y);
                    if (double.IsFinite(luminance) && luminance >= 0) samples.Add(luminance);
                }
            }
            if (samples.Count == 0) return double.NaN;
            samples.Sort();
            int middle = samples.Count / 2;
            return samples.Count % 2 == 0 ? (samples[middle - 1] + samples[middle]) / 2 : samples[middle];
        }

        private static List<LuminousAreaPoint> FindSideBoundaryPoints(
            HImage image,
            ImageLayout layout,
            LuminousAreaPoint center,
            LuminousAreaPoint sideStart,
            LuminousAreaPoint sideEnd,
            double boundaryLuminance)
        {
            List<LuminousAreaPoint> result = new(SideSampleCount);
            for (int index = 0; index < SideSampleCount; index++)
            {
                double amount = 0.08 + 0.84 * index / (SideSampleCount - 1);
                LuminousAreaPoint target = Interpolate(sideStart, sideEnd, amount);
                if (TryFindRayCrossing(image, layout, center, target, boundaryLuminance, out LuminousAreaPoint crossing))
                    result.Add(crossing);
            }
            return result;
        }

        private static bool TryFindRayCrossing(
            HImage image,
            ImageLayout layout,
            LuminousAreaPoint center,
            LuminousAreaPoint target,
            double boundaryLuminance,
            out LuminousAreaPoint crossing)
        {
            crossing = default;
            double dx = target.X - center.X;
            double dy = target.Y - center.Y;
            double targetDistance = Math.Sqrt(dx * dx + dy * dy);
            if (!double.IsFinite(targetDistance) || targetDistance < 2) return false;
            double ux = dx / targetDistance;
            double uy = dy / targetDistance;
            double maximumDistance = DistanceToBounds(center, ux, uy, layout);
            double start = Math.Max(1, targetDistance * 0.45);
            double end = Math.Min(maximumDistance - 1, targetDistance * 1.55);
            if (end <= start + 2) return false;

            double bestDistance = double.PositiveInfinity;
            double bestCrossingDistance = 0;
            for (double distance = Math.Floor(start); distance < end; distance++)
            {
                double inside = SmoothLuminance(image, layout, center, ux, uy, distance);
                double outside = SmoothLuminance(image, layout, center, ux, uy, distance + 1);
                if (!double.IsFinite(inside) || !double.IsFinite(outside)
                    || inside < boundaryLuminance || outside >= boundaryLuminance)
                    continue;
                double persistentInside = SmoothLuminance(image, layout, center, ux, uy, Math.Max(start, distance - 3));
                double persistentOutside = SmoothLuminance(image, layout, center, ux, uy, Math.Min(end, distance + 4));
                if (!double.IsFinite(persistentInside) || !double.IsFinite(persistentOutside)
                    || persistentInside < boundaryLuminance || persistentOutside >= boundaryLuminance)
                    continue;

                double candidateDistance = Math.Abs(distance - targetDistance);
                if (candidateDistance >= bestDistance) continue;
                double denominator = inside - outside;
                double fraction = denominator > double.Epsilon
                    ? Math.Clamp((inside - boundaryLuminance) / denominator, 0, 1)
                    : 0.5;
                bestDistance = candidateDistance;
                bestCrossingDistance = distance + fraction;
            }
            if (!double.IsFinite(bestDistance)) return false;
            crossing = new LuminousAreaPoint(
                center.X + ux * bestCrossingDistance,
                center.Y + uy * bestCrossingDistance);
            return true;
        }

        private static double SmoothLuminance(
            HImage image,
            ImageLayout layout,
            LuminousAreaPoint origin,
            double ux,
            double uy,
            double distance)
        {
            double sum = 0;
            int count = 0;
            for (int offset = -1; offset <= 1; offset++)
            {
                double x = origin.X + ux * (distance + offset);
                double y = origin.Y + uy * (distance + offset);
                if (!layout.Contains(x, y)) continue;
                double value = BilinearLuminance(image, layout, x, y);
                if (!double.IsFinite(value)) continue;
                sum += value;
                count++;
            }
            return count == 0 ? double.NaN : sum / count;
        }

        private static double BilinearLuminance(HImage image, ImageLayout layout, double x, double y)
        {
            int x0 = Math.Clamp((int)Math.Floor(x), layout.X, layout.Right);
            int y0 = Math.Clamp((int)Math.Floor(y), layout.Y, layout.Bottom);
            int x1 = Math.Min(x0 + 1, layout.Right);
            int y1 = Math.Min(y0 + 1, layout.Bottom);
            double fx = x - x0;
            double fy = y - y0;
            double top = ReadLuminance(image, layout.Stride, x0, y0) * (1 - fx)
                + ReadLuminance(image, layout.Stride, x1, y0) * fx;
            double bottom = ReadLuminance(image, layout.Stride, x0, y1) * (1 - fx)
                + ReadLuminance(image, layout.Stride, x1, y1) * fx;
            return top * (1 - fy) + bottom * fy;
        }

        private static unsafe double ReadLuminance(HImage image, int stride, int x, int y)
        {
            byte* row = (byte*)image.pData + (long)y * stride;
            int colorChannels = Math.Min(image.channels, 3);
            if (image.depth == 8)
            {
                byte* pixel = row + x * image.channels;
                double sum = 0;
                for (int channel = 0; channel < colorChannels; channel++) sum += pixel[channel];
                return sum / colorChannels;
            }
            if (image.depth == 16)
            {
                ushort* pixel = (ushort*)row + x * image.channels;
                double sum = 0;
                for (int channel = 0; channel < colorChannels; channel++) sum += pixel[channel];
                return sum / colorChannels;
            }
            return *((float*)row + x);
        }

        private static bool TryFitLine(IReadOnlyList<LuminousAreaPoint> points, out LineFit line)
        {
            if (!TryFitLineCore(points, out LineFit initialLine))
            {
                line = default;
                return false;
            }
            double[] residuals = points.Select(point => Math.Abs(initialLine.A * point.X + initialLine.B * point.Y + initialLine.C)).OrderBy(value => value).ToArray();
            double median = Median(residuals);
            double[] deviations = residuals.Select(value => Math.Abs(value - median)).OrderBy(value => value).ToArray();
            double maximumResidual = Math.Max(1.5, median + 3 * Math.Max(Median(deviations), 0.25));
            LuminousAreaPoint[] inliers = points
                .Where(point => Math.Abs(initialLine.A * point.X + initialLine.B * point.Y + initialLine.C) <= maximumResidual)
                .ToArray();
            line = initialLine;
            return inliers.Length >= 5 && TryFitLineCore(inliers, out line);
        }

        private static bool TryFitLineCore(IReadOnlyList<LuminousAreaPoint> points, out LineFit line)
        {
            line = default;
            if (points.Count < 2) return false;
            double centerX = points.Average(point => point.X);
            double centerY = points.Average(point => point.Y);
            double xx = 0, xy = 0, yy = 0;
            foreach (LuminousAreaPoint point in points)
            {
                double dx = point.X - centerX;
                double dy = point.Y - centerY;
                xx += dx * dx;
                xy += dx * dy;
                yy += dy * dy;
            }
            if (!double.IsFinite(xx + yy) || xx + yy < 1e-6) return false;
            double angle = 0.5 * Math.Atan2(2 * xy, xx - yy);
            double a = -Math.Sin(angle);
            double b = Math.Cos(angle);
            double c = -(a * centerX + b * centerY);
            double squaredError = points.Sum(point => Math.Pow(a * point.X + b * point.Y + c, 2));
            line = new LineFit(a, b, c, Math.Sqrt(squaredError / points.Count));
            return double.IsFinite(line.RootMeanSquareError);
        }

        private static LuminousAreaPoint FindCenter(IReadOnlyList<LuminousAreaPoint> corners)
        {
            LineFit first = LineThrough(corners[0], corners[2]);
            LineFit second = LineThrough(corners[1], corners[3]);
            LuminousAreaPoint intersection = Intersect(first, second);
            return double.IsFinite(intersection.X) && double.IsFinite(intersection.Y)
                ? intersection
                : new LuminousAreaPoint(corners.Average(point => point.X), corners.Average(point => point.Y));
        }

        private static LineFit LineThrough(LuminousAreaPoint first, LuminousAreaPoint second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            double length = Math.Sqrt(dx * dx + dy * dy);
            if (length < double.Epsilon) return default;
            double a = -dy / length;
            double b = dx / length;
            return new LineFit(a, b, -(a * first.X + b * first.Y), 0);
        }

        private static LuminousAreaPoint Intersect(LineFit first, LineFit second)
        {
            double determinant = first.A * second.B - second.A * first.B;
            if (!double.IsFinite(determinant) || Math.Abs(determinant) < 1e-9)
                return new LuminousAreaPoint(double.NaN, double.NaN);
            return new LuminousAreaPoint(
                (first.B * second.C - second.B * first.C) / determinant,
                (first.C * second.A - second.C * first.A) / determinant);
        }

        private static double DistanceToBounds(LuminousAreaPoint origin, double ux, double uy, ImageLayout layout)
        {
            double result = double.PositiveInfinity;
            if (ux > 1e-12) result = Math.Min(result, (layout.Right - origin.X) / ux);
            else if (ux < -1e-12) result = Math.Min(result, (layout.X - origin.X) / ux);
            if (uy > 1e-12) result = Math.Min(result, (layout.Bottom - origin.Y) / uy);
            else if (uy < -1e-12) result = Math.Min(result, (layout.Y - origin.Y) / uy);
            return result;
        }

        private static double Median(IReadOnlyList<double> values)
        {
            int middle = values.Count / 2;
            return values.Count % 2 == 0 ? (values[middle - 1] + values[middle]) / 2 : values[middle];
        }

        private static LuminousAreaPoint Interpolate(LuminousAreaPoint start, LuminousAreaPoint end, double amount) =>
            new(start.X + (end.X - start.X) * amount, start.Y + (end.Y - start.Y) * amount);

        private static double Distance(LuminousAreaPoint first, LuminousAreaPoint second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        private readonly record struct ImageLayout(int X, int Y, int Width, int Height, int Stride)
        {
            public int Right => X + Width - 1;
            public int Bottom => Y + Height - 1;

            public bool Contains(double x, double y, double tolerance = 0) =>
                x >= X - tolerance && y >= Y - tolerance && x <= Right + tolerance && y <= Bottom + tolerance;
        }

        private readonly record struct LineFit(double A, double B, double C, double RootMeanSquareError);
    }
}
