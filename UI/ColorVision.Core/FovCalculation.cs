using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace ColorVision.Core
{
    public enum FovMeasurementMode
    {
        [Description("四角（默认）")]
        FourCorners = 0,

        [Description("轮廓（曲边/非四边形）")]
        CurvedBoundary = 1
    }

    public sealed record FovBoundaryGeometry
    {
        public required IReadOnlyList<LuminousAreaPoint> Contour { get; init; }
        public required LuminousAreaPoint Center { get; init; }
        public required LuminousAreaPoint Left { get; init; }
        public required LuminousAreaPoint Right { get; init; }
        public required LuminousAreaPoint Top { get; init; }
        public required LuminousAreaPoint Bottom { get; init; }
        /// <summary>NW, NE, SE, SW intersections of the two centre lines parallel to the contour bounding-box diagonals; these are not corners.</summary>
        public required IReadOnlyList<LuminousAreaPoint> DiagonalPoints { get; init; }
    }

    /// <summary>
    /// Field-of-view values from four corners or an independently detected contour.
    /// Corners are empty in contour mode; its measurement endpoints are in Boundary.
    /// </summary>
    public sealed record FovMeasurement
    {
        public required IReadOnlyList<LuminousAreaPoint> Corners { get; init; }
        public double DiagonalFovDegrees { get; init; }
        public double HorizontalFovDegrees { get; init; }
        public double VerticalFovDegrees { get; init; }
        public double DirectionalHorizontalFovDegrees { get; init; }
        public double DirectionalVerticalFovDegrees { get; init; }
        public double LeftDownToRightUpDegrees { get; init; }
        public double LeftUpToRightDownDegrees { get; init; }
        public double FovDist { get; init; }
        public double CameraDegrees { get; init; }
        public FovMeasurementMode Mode { get; init; }
        public FovBoundaryGeometry? Boundary { get; init; }
    }

    public sealed record FovCalculationResult
    {
        public required FovMeasurement Measurement { get; init; }
        public LuminousAreaDetectionResult? Detection { get; init; }
        public LuminousAreaDetectionResult? CoarseDetection { get; init; }
        public bool UsedProvidedCorners { get; init; }
        public double LuminanceBoundaryRatio { get; init; }
        public string BoundaryMode => Measurement.Mode == FovMeasurementMode.CurvedBoundary
            ? "CurvedBoundary" : UsedProvidedCorners ? "UpstreamCorners" : "RobustV2";
    }

    /// <summary>
    /// Shared FOV geometry used by ImageView and Flow.
    /// </summary>
    public static class FovCalculator
    {
        public static FovCalculationResult DetectAndCalculate(
            HImage image,
            RoiRect roi,
            double fovDist,
            double cameraDegrees,
            FovMeasurementMode mode,
            double minimumConfidence = 0.25,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners = null)
        {
            ValidateCalibration(fovDist, cameraDegrees);
            if (!double.IsFinite(minimumConfidence) || minimumConfidence < 0 || minimumConfidence > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumConfidence), "Minimum confidence must be between 0 and 1.");
            if (mode == FovMeasurementMode.CurvedBoundary)
            {
                var boundary = FovCurvedBoundaryDetector.Detect(image, roi, minimumConfidence);
                FovBoundaryGeometry geometry = boundary.Geometry;
                double diagonal1 = PixelDistanceToDegrees(Distance(geometry.DiagonalPoints[3], geometry.DiagonalPoints[1]), fovDist, cameraDegrees);
                double diagonal2 = PixelDistanceToDegrees(Distance(geometry.DiagonalPoints[0], geometry.DiagonalPoints[2]), fovDist, cameraDegrees);
                return new FovCalculationResult
                {
                    Measurement = new FovMeasurement
                    {
                        Corners = Array.Empty<LuminousAreaPoint>(),
                        Mode = mode,
                        Boundary = geometry,
                        FovDist = fovDist,
                        CameraDegrees = cameraDegrees,
                        HorizontalFovDegrees = PixelDistanceToDegrees(geometry.Contour.Max(point => point.X) - geometry.Contour.Min(point => point.X), fovDist, cameraDegrees),
                        VerticalFovDegrees = PixelDistanceToDegrees(geometry.Contour.Max(point => point.Y) - geometry.Contour.Min(point => point.Y), fovDist, cameraDegrees),
                        DirectionalHorizontalFovDegrees = PixelDistanceToDegrees(Distance(geometry.Left, geometry.Right), fovDist, cameraDegrees),
                        DirectionalVerticalFovDegrees = PixelDistanceToDegrees(Distance(geometry.Top, geometry.Bottom), fovDist, cameraDegrees),
                        DiagonalFovDegrees = (diagonal1 + diagonal2) / 2,
                        LeftDownToRightUpDegrees = diagonal1,
                        LeftUpToRightDownDegrees = diagonal2
                    },
                    Detection = boundary.Detection
                };
            }
            if (mode != FovMeasurementMode.FourCorners)
                throw new ArgumentOutOfRangeException(nameof(mode), "Unsupported FOV measurement mode.");
            return DetectAndCalculate(image, roi, fovDist, cameraDegrees, minimumConfidence, coarseCorners: coarseCorners);
        }

        // Preserve the original public signature for already compiled callers.
        public static FovCalculationResult DetectAndCalculate(
            HImage image,
            RoiRect roi,
            double fovDist,
            double cameraDegrees,
            double minimumConfidence = 0.25,
            double luminanceBoundaryRatio = FovLuminousAreaDetector.DefaultBoundaryRatio,
            IReadOnlyList<LuminousAreaPoint>? coarseCorners = null)
        {
            ValidateCalibration(fovDist, cameraDegrees);
            if (!double.IsFinite(minimumConfidence) || minimumConfidence < 0 || minimumConfidence > 1)
                throw new ArgumentOutOfRangeException(nameof(minimumConfidence), "Minimum confidence must be between 0 and 1.");
            // Retain the ratio argument for saved/calling-code compatibility only.
            // Geometric FOV must not replace valid edges with an internal isophote.

            LuminousAreaDetectionResult? coarseDetection = null;
            IReadOnlyList<LuminousAreaPoint> seedCorners;
            if (coarseCorners == null)
            {
                coarseDetection = LuminousAreaNative.DetectV2(image, roi, minimumConfidence);
                if (!coarseDetection.Success || !coarseDetection.HasValidCorners)
                {
                    string reason = string.IsNullOrWhiteSpace(coarseDetection.FailureReason)
                        ? "The luminous area was not found."
                        : coarseDetection.FailureReason;
                    throw new InvalidOperationException(reason);
                }
                if (!coarseDetection.Confidence.HasValue || !double.IsFinite(coarseDetection.Confidence.Value)
                    || coarseDetection.Confidence.Value < minimumConfidence)
                {
                    throw new InvalidOperationException($"Luminous-area confidence is below the required value {minimumConfidence:F3}.");
                }
                seedCorners = coarseDetection.Corners;
            }
            else
            {
                if (!LuminousAreaResultParser.TryValidateOrderedCorners(coarseCorners, out string geometryError))
                    throw new ArgumentException($"FOV coarse corners are invalid: {geometryError}", nameof(coarseCorners));
                seedCorners = coarseCorners;
            }

            return new FovCalculationResult
            {
                Measurement = Calculate(seedCorners, fovDist, cameraDegrees),
                Detection = coarseDetection,
                UsedProvidedCorners = coarseCorners != null,
            };
        }

        public static FovCalculationResult CalculateFromCorners(
            IReadOnlyList<LuminousAreaPoint> corners,
            double fovDist,
            double cameraDegrees) => new()
        {
            Measurement = Calculate(corners, fovDist, cameraDegrees),
            UsedProvidedCorners = true
        };

        public static FovMeasurement Calculate(
            IReadOnlyList<LuminousAreaPoint> corners,
            double fovDist,
            double cameraDegrees)
        {
            ArgumentNullException.ThrowIfNull(corners);
            ValidateCalibration(fovDist, cameraDegrees);
            if (corners.Count != 4)
                throw new ArgumentException("FOV calculation requires four corners in LT, RT, RB, LB order.", nameof(corners));
            if (corners.Any(point => !double.IsFinite(point.X) || !double.IsFinite(point.Y)))
                throw new ArgumentException("FOV corners must contain finite coordinates.", nameof(corners));
            if (!LuminousAreaResultParser.TryValidateOrderedCorners(corners, out string geometryError))
                throw new ArgumentException($"FOV corners are invalid: {geometryError}", nameof(corners));

            LuminousAreaPoint lt = corners[0];
            LuminousAreaPoint rt = corners[1];
            LuminousAreaPoint rb = corners[2];
            LuminousAreaPoint lb = corners[3];
            LuminousAreaPoint left = Midpoint(lt, lb);
            LuminousAreaPoint right = Midpoint(rt, rb);
            LuminousAreaPoint up = Midpoint(lt, rt);
            LuminousAreaPoint down = Midpoint(lb, rb);

            double top = PixelDistanceToDegrees(Distance(lt, rt), fovDist, cameraDegrees);
            double bottom = PixelDistanceToDegrees(Distance(lb, rb), fovDist, cameraDegrees);
            double leftEdge = PixelDistanceToDegrees(Distance(lt, lb), fovDist, cameraDegrees);
            double rightEdge = PixelDistanceToDegrees(Distance(rt, rb), fovDist, cameraDegrees);
            double leftDownToRightUp = PixelDistanceToDegrees(Distance(lb, rt), fovDist, cameraDegrees);
            double leftUpToRightDown = PixelDistanceToDegrees(Distance(lt, rb), fovDist, cameraDegrees);

            return new FovMeasurement
            {
                Corners = corners.Select(point => new LuminousAreaPoint(point.X, point.Y)).ToArray(),
                HorizontalFovDegrees = (top + bottom) / 2,
                VerticalFovDegrees = (leftEdge + rightEdge) / 2,
                DiagonalFovDegrees = (leftDownToRightUp + leftUpToRightDown) / 2,
                DirectionalHorizontalFovDegrees = PixelDistanceToDegrees(Distance(left, right), fovDist, cameraDegrees),
                DirectionalVerticalFovDegrees = PixelDistanceToDegrees(Distance(up, down), fovDist, cameraDegrees),
                LeftDownToRightUpDegrees = leftDownToRightUp,
                LeftUpToRightDownDegrees = leftUpToRightDown,
                FovDist = fovDist,
                CameraDegrees = cameraDegrees
            };
        }

        public static double PixelDistanceToDegrees(double pixelDistance, double fovDist, double cameraDegrees)
        {
            ValidateCalibration(fovDist, cameraDegrees);
            if (!double.IsFinite(pixelDistance) || pixelDistance < 0)
                throw new ArgumentOutOfRangeException(nameof(pixelDistance), "Pixel distance must be a finite non-negative value.");
            return 2 * Math.Atan(pixelDistance / fovDist * Math.Tan(cameraDegrees / 2 * Math.PI / 180)) * 180 / Math.PI;
        }

        public static double SensorLengthToDegrees(double sensorLengthMillimeters, double focalLengthMillimeters)
        {
            if (!double.IsFinite(sensorLengthMillimeters) || sensorLengthMillimeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(sensorLengthMillimeters), "Sensor length must be a finite positive value.");
            if (!double.IsFinite(focalLengthMillimeters) || focalLengthMillimeters <= 0)
                throw new ArgumentOutOfRangeException(nameof(focalLengthMillimeters), "Focal length must be a finite positive value.");
            return 2 * Math.Atan(sensorLengthMillimeters / (2 * focalLengthMillimeters)) * 180 / Math.PI;
        }

        public static double EquivalentFocalLengthPixels(double fovDist, double cameraDegrees)
        {
            ValidateCalibration(fovDist, cameraDegrees);
            return fovDist / (2 * Math.Tan(cameraDegrees / 2 * Math.PI / 180));
        }

        public static void ValidateCalibration(double fovDist, double cameraDegrees)
        {
            if (!double.IsFinite(fovDist) || fovDist <= 0)
                throw new ArgumentOutOfRangeException(nameof(fovDist), "FovDist must be a finite positive pixel extent.");
            if (!double.IsFinite(cameraDegrees) || cameraDegrees <= 0 || cameraDegrees >= 180)
                throw new ArgumentOutOfRangeException(nameof(cameraDegrees), "Camera degrees must be between 0 and 180 degrees.");
        }

        private static LuminousAreaPoint Midpoint(LuminousAreaPoint first, LuminousAreaPoint second) =>
            new((first.X + second.X) / 2, (first.Y + second.Y) / 2);

        private static double Distance(LuminousAreaPoint first, LuminousAreaPoint second)
        {
            double dx = second.X - first.X;
            double dy = second.Y - first.Y;
            return Math.Sqrt(dx * dx + dy * dy);
        }
    }
}
