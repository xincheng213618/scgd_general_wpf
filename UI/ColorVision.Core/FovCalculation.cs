using System;
using System.Collections.Generic;
using System.Linq;

namespace ColorVision.Core
{
    /// <summary>
    /// Field-of-view values calculated from a luminous-area quadrilateral.
    /// Corners always use the LT, RT, RB, LB order of <see cref="LuminousAreaDetectionResult"/>.
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
    }

    public sealed record FovCalculationResult
    {
        public required FovMeasurement Measurement { get; init; }
        public LuminousAreaDetectionResult? Detection { get; init; }
        public LuminousAreaDetectionResult? CoarseDetection { get; init; }
        public bool UsedProvidedCorners { get; init; }
        public double LuminanceBoundaryRatio { get; init; }
        public string BoundaryMode => UsedProvidedCorners ? "UpstreamCorners" : "RobustV2";
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
