#pragma warning disable OCVS002
using OpenCvSharp;
using System;
using System.Threading.Tasks;
using Point = System.Windows.Point;

namespace Conoscope.Core
{
    public enum ConoscopeCoordinateSystem
    {
        Polar = 0,
        HorizontalVertical = 1
    }

    /// <summary>
    /// Cached reverse map from an H/V angular preview to the source polar image.
    /// Source pixels remain authoritative; this object owns preview-only maps.
    /// </summary>
    internal sealed class ConoscopeHorizontalVerticalProjection : IDisposable
    {
        internal const int DefaultMaximumPreviewSide = 2049;

        private readonly Mat mapX;
        private readonly Mat mapY;
        private int disposed;

        private ConoscopeHorizontalVerticalProjection(
            int sourceWidth,
            int sourceHeight,
            Point sourceCenter,
            double sourcePixelsPerDegree,
            double maxPolarAngle,
            int outputSize)
        {
            SourceWidth = sourceWidth;
            SourceHeight = sourceHeight;
            SourceCenter = sourceCenter;
            SourcePixelsPerDegree = sourcePixelsPerDegree;
            MaxPolarAngle = maxPolarAngle;
            OutputSize = outputSize;
            OutputCenter = new Point((outputSize - 1) / 2.0, (outputSize - 1) / 2.0);
            OutputRadius = (outputSize - 1) / 2.0;
            OutputPixelsPerDegree = OutputRadius / maxPolarAngle;

            mapX = new Mat(outputSize, outputSize, MatType.CV_32FC1);
            mapY = new Mat(outputSize, outputSize, MatType.CV_32FC1);
            InvalidMask = new Mat(outputSize, outputSize, MatType.CV_8UC1);
            try
            {
                BuildReverseMap();
            }
            catch
            {
                mapX.Dispose();
                mapY.Dispose();
                InvalidMask.Dispose();
                throw;
            }
        }

        public int SourceWidth { get; }
        public int SourceHeight { get; }
        public Point SourceCenter { get; }
        public double SourcePixelsPerDegree { get; }
        public double MaxPolarAngle { get; }
        public int OutputSize { get; }
        public Point OutputCenter { get; }
        public double OutputRadius { get; }
        public double OutputPixelsPerDegree { get; }
        public Mat InvalidMask { get; }

        public static ConoscopeHorizontalVerticalProjection Create(
            int sourceWidth,
            int sourceHeight,
            Point sourceCenter,
            double sourcePixelsPerDegree,
            double maxPolarAngle,
            int maximumPreviewSide = DefaultMaximumPreviewSide)
        {
            if (sourceWidth <= 0 || sourceHeight <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourceWidth));
            }

            if (!double.IsFinite(sourcePixelsPerDegree) || sourcePixelsPerDegree <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(sourcePixelsPerDegree));
            }

            if (!double.IsFinite(maxPolarAngle) || maxPolarAngle <= 0 || maxPolarAngle >= 90)
            {
                throw new ArgumentOutOfRangeException(nameof(maxPolarAngle));
            }

            int normalizedMaximum = Math.Max(3, maximumPreviewSide);
            if (normalizedMaximum % 2 == 0)
            {
                normalizedMaximum--;
            }

            int fullResolutionSide = checked((int)Math.Round(maxPolarAngle * sourcePixelsPerDegree) * 2 + 1);
            int outputSize = Math.Min(fullResolutionSide, normalizedMaximum);
            if (outputSize % 2 == 0)
            {
                outputSize--;
            }

            return new ConoscopeHorizontalVerticalProjection(
                sourceWidth,
                sourceHeight,
                sourceCenter,
                sourcePixelsPerDegree,
                maxPolarAngle,
                Math.Max(3, outputSize));
        }

        public Mat RemapGray8(Mat source)
        {
            ObjectDisposedException.ThrowIf(disposed != 0, this);
            ArgumentNullException.ThrowIfNull(source);
            if (source.Empty() || source.Width != SourceWidth || source.Height != SourceHeight || source.Type() != MatType.CV_8UC1)
            {
                throw new ArgumentException("H/V preview requires a source-sized CV_8UC1 image.", nameof(source));
            }

            Mat output = new();
            try
            {
                Cv2.Remap(source, output, mapX, mapY, InterpolationFlags.Linear, BorderTypes.Constant, Scalar.All(0));
                output.SetTo(Scalar.All(0), InvalidMask);
                return output;
            }
            catch
            {
                output.Dispose();
                throw;
            }
        }

        public bool TryMapDisplayPointToSource(Point displayPoint, out Point sourcePoint, out double horizontalAngle, out double verticalAngle, out double polarAngle, out double azimuthAngle)
        {
            horizontalAngle = (displayPoint.X - OutputCenter.X) / OutputPixelsPerDegree;
            verticalAngle = (OutputCenter.Y - displayPoint.Y) / OutputPixelsPerDegree;
            return TryMapHorizontalVerticalToSource(
                horizontalAngle,
                verticalAngle,
                SourceCenter,
                SourcePixelsPerDegree,
                MaxPolarAngle,
                SourceWidth,
                SourceHeight,
                out sourcePoint,
                out polarAngle,
                out azimuthAngle);
        }

        public static bool TryConvertHorizontalVerticalToPolar(
            double horizontalAngle,
            double verticalAngle,
            double maxPolarAngle,
            out double polarAngle,
            out double azimuthAngle)
        {
            polarAngle = double.NaN;
            azimuthAngle = double.NaN;
            if (!double.IsFinite(horizontalAngle)
                || !double.IsFinite(verticalAngle)
                || !double.IsFinite(maxPolarAngle)
                || maxPolarAngle <= 0
                || maxPolarAngle >= 90
                || Math.Abs(horizontalAngle) >= 90
                || Math.Abs(verticalAngle) >= 90)
            {
                return false;
            }

            double tanHorizontal = Math.Tan(DegreesToRadians(horizontalAngle));
            double tanVertical = Math.Tan(DegreesToRadians(verticalAngle));
            polarAngle = RadiansToDegrees(Math.Atan(Math.Sqrt(tanHorizontal * tanHorizontal + tanVertical * tanVertical)));
            azimuthAngle = NormalizeFullAngle(RadiansToDegrees(Math.Atan2(tanVertical, tanHorizontal)));
            return polarAngle <= maxPolarAngle + 0.000001;
        }

        public static bool TryConvertPolarToHorizontalVertical(
            double polarAngle,
            double azimuthAngle,
            double maxPolarAngle,
            out double horizontalAngle,
            out double verticalAngle)
        {
            horizontalAngle = double.NaN;
            verticalAngle = double.NaN;
            if (!double.IsFinite(polarAngle)
                || !double.IsFinite(azimuthAngle)
                || !double.IsFinite(maxPolarAngle)
                || polarAngle < 0
                || polarAngle > maxPolarAngle + 0.000001
                || maxPolarAngle <= 0
                || maxPolarAngle >= 90)
            {
                return false;
            }

            double theta = DegreesToRadians(polarAngle);
            double phi = DegreesToRadians(azimuthAngle);
            double tanTheta = Math.Tan(theta);
            horizontalAngle = RadiansToDegrees(Math.Atan(tanTheta * Math.Cos(phi)));
            verticalAngle = RadiansToDegrees(Math.Atan(tanTheta * Math.Sin(phi)));
            return double.IsFinite(horizontalAngle) && double.IsFinite(verticalAngle);
        }

        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref disposed, 1) != 0)
            {
                return;
            }

            mapX.Dispose();
            mapY.Dispose();
            InvalidMask.Dispose();
            GC.SuppressFinalize(this);
        }

        private unsafe void BuildReverseMap()
        {
            Parallel.For(0, OutputSize, row =>
            {
                float* mapXRow = (float*)mapX.Ptr(row);
                float* mapYRow = (float*)mapY.Ptr(row);
                byte* invalidRow = (byte*)InvalidMask.Ptr(row);
                double verticalAngle = (OutputCenter.Y - row) / OutputPixelsPerDegree;

                for (int column = 0; column < OutputSize; column++)
                {
                    double horizontalAngle = (column - OutputCenter.X) / OutputPixelsPerDegree;
                    bool isValid = TryMapHorizontalVerticalToSource(
                        horizontalAngle,
                        verticalAngle,
                        SourceCenter,
                        SourcePixelsPerDegree,
                        MaxPolarAngle,
                        SourceWidth,
                        SourceHeight,
                        out Point sourcePoint,
                        out _,
                        out _);

                    mapXRow[column] = isValid ? (float)sourcePoint.X : -1;
                    mapYRow[column] = isValid ? (float)sourcePoint.Y : -1;
                    invalidRow[column] = isValid ? (byte)0 : (byte)255;
                }
            });
        }

        private static bool TryMapHorizontalVerticalToSource(
            double horizontalAngle,
            double verticalAngle,
            Point sourceCenter,
            double sourcePixelsPerDegree,
            double maxPolarAngle,
            int sourceWidth,
            int sourceHeight,
            out Point sourcePoint,
            out double polarAngle,
            out double azimuthAngle)
        {
            sourcePoint = default;
            if (!TryConvertHorizontalVerticalToPolar(horizontalAngle, verticalAngle, maxPolarAngle, out polarAngle, out azimuthAngle))
            {
                return false;
            }

            double radius = polarAngle * sourcePixelsPerDegree;
            double azimuthRadians = DegreesToRadians(azimuthAngle);
            sourcePoint = new Point(
                sourceCenter.X + radius * Math.Cos(azimuthRadians),
                sourceCenter.Y - radius * Math.Sin(azimuthRadians));
            return sourcePoint.X >= 0
                && sourcePoint.Y >= 0
                && sourcePoint.X <= sourceWidth - 1
                && sourcePoint.Y <= sourceHeight - 1;
        }

        private static double NormalizeFullAngle(double angle)
        {
            angle %= 360;
            return angle < 0 ? angle + 360 : angle;
        }

        private static double DegreesToRadians(double angle) => angle * Math.PI / 180.0;
        private static double RadiansToDegrees(double angle) => angle * 180.0 / Math.PI;
    }
}
