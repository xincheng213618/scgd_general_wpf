using OpenCvSharp;
using System;
using System.Threading;

namespace ColorVision.ImageEditor.Algorithms
{
    internal enum MorphologyOperation
    {
        Erode,
        Dilate,
        Open,
        Close,
        Gradient,
        TopHat,
        BlackHat,
    }

    internal enum FilterDenoiseOperation
    {
        Bilateral,
        Blur,
    }

    internal static class OpenCvImageAlgorithms
    {
        public static void AdjustBasic(Mat mat, double exposure, double brightness, double contrast, double gamma, CancellationToken cancellationToken = default)
            => AdjustBasic(mat, mat, exposure, brightness, contrast, gamma, cancellationToken);

        public static void AdjustBasic(Mat source, Mat destination, double exposure, double brightness, double contrast, double gamma,
            CancellationToken cancellationToken = default)
            => AdjustBasicTo(source, () => destination, exposure, brightness, contrast, gamma, cancellationToken);

        /// <summary>Requests one caller-owned destination at final writeback, avoiding storage for superseded work.</summary>
        public static void AdjustBasicTo(Mat source, Func<Mat> createDestination, double exposure, double brightness, double contrast, double gamma,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(createDestination);
            MatType depth = source.Depth();
            double maximum = GetNominalMaximum(depth);
            double exposureGain = Math.Pow(2, Math.Clamp(exposure, -5, 5));
            double brightnessOffset = Math.Clamp(brightness, -100, 100) / 100;
            double contrastGain = 1 + Math.Clamp(contrast, -100, 100) / 100;
            double gammaValue = Math.Clamp(gamma, 0.1, 5);
            double alpha = exposureGain * contrastGain;
            double beta = brightnessOffset * contrastGain + 0.5 * (1 - contrastGain);
            cancellationToken.ThrowIfCancellationRequested();

            if ((depth == MatType.CV_8U || depth == MatType.CV_16U) && !source.Empty())
            {
                // Integer identity is exact across the full sample range. Float inputs still need clipping.
                if (alpha == 1 && beta == 0 && gammaValue == 1)
                {
                    Mat destination = RequestDestination(createDestination, cancellationToken);
                    if (!ReferenceEquals(source, destination)) source.CopyTo(destination);
                    cancellationToken.ThrowIfCancellationRequested();
                    return;
                }

                // Amortize table construction even for adjustments that do not need a power calculation.
                int minimumPixels = depth == MatType.CV_16U ? 512 * 512 : 64 * 64;
                if (source.Total() >= minimumPixels && double.IsFinite(alpha) && double.IsFinite(beta) && double.IsFinite(gammaValue))
                {
                    ApplyIntegerToneAdjustment(source, createDestination, depth, alpha, beta, gammaValue, maximum, cancellationToken);
                    return;
                }
            }

            if (source.Channels() != 4)
            {
                ApplyToneAdjustment(source, createDestination, alpha, beta, gammaValue, maximum, cancellationToken);
                return;
            }

            Mat[] channels = Cv2.Split(source);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                for (int index = 0; index < 3; index++)
                {
                    Mat channel = channels[index];
                    ApplyToneAdjustment(channel, () => channel, alpha, beta, gammaValue, maximum, cancellationToken);
                }

                Mat destination = RequestDestination(createDestination, cancellationToken);
                Cv2.Merge(channels, destination);
                cancellationToken.ThrowIfCancellationRequested();
            }
            finally
            {
                foreach (Mat channel in channels)
                {
                    channel.Dispose();
                }
            }
        }

        public static void GaussianBlur(Mat source, Mat destination, int kernelSize, double sigma)
        {
            Cv2.GaussianBlur(source, destination, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)), sigma);
        }

        public static void MedianBlur(Mat source, Mat destination, int kernelSize)
        {
            Cv2.MedianBlur(source, destination, EnsureOdd(kernelSize));
        }

        public static void Sharpen(Mat source, Mat destination)
        {
            using Mat kernel = Mat.FromArray(new float[,]
            {
                { 0, -1, 0 },
                { -1, 5, -1 },
                { 0, -1, 0 },
            });
            Cv2.Filter2D(source, destination, source.Depth(), kernel);
        }

        public static void Morphology(Mat source, Mat destination, MorphologyOperation operation, int kernelSize, int iterations)
        {
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)));
            iterations = Math.Max(1, iterations);

            switch (operation)
            {
                case MorphologyOperation.Erode:
                    Cv2.Erode(source, destination, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Dilate:
                    Cv2.Dilate(source, destination, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Open:
                    Cv2.MorphologyEx(source, destination, MorphTypes.Open, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Close:
                    Cv2.MorphologyEx(source, destination, MorphTypes.Close, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Gradient:
                    Cv2.MorphologyEx(source, destination, MorphTypes.Gradient, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.TopHat:
                    Cv2.MorphologyEx(source, destination, MorphTypes.TopHat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.BlackHat:
                    Cv2.MorphologyEx(source, destination, MorphTypes.BlackHat, kernel, iterations: iterations);
                    break;
                default:
                    source.CopyTo(destination);
                    break;
            }
        }

        public static void FilterDenoise(Mat source, Mat destination, FilterDenoiseOperation operation, int kernelSize, double sigmaColor, double sigmaSpace)
        {
            if (operation == FilterDenoiseOperation.Blur)
            {
                Cv2.Blur(source, destination, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)));
                return;
            }

            ApplyBilateral(source, destination, EnsureOdd(kernelSize), sigmaColor, sigmaSpace);
        }

        private static void ApplyBilateral(Mat source, Mat destination, int diameter, double sigmaColor, double sigmaSpace)
        {
            if (source.Depth() != MatType.CV_8U && source.Depth() != MatType.CV_32F)
            {
                using Mat converted = new();
                using Mat filtered = new();
                source.ConvertTo(converted, MatType.MakeType(MatType.CV_32F, source.Channels()));
                // The BGRA path filters separate color data and can reuse the converted storage.
                Mat output = source.Channels() == 4 ? converted : filtered;
                ApplyBilateral(converted, output, diameter, sigmaColor, sigmaSpace);
                output.ConvertTo(destination, source.Type());
                return;
            }

            if (source.Channels() != 4)
            {
                Cv2.BilateralFilter(source, destination, diameter, sigmaColor, sigmaSpace);
                return;
            }

            using Mat color = new();
            using Mat filteredColor = new();
            using Mat alpha = new();
            Cv2.CvtColor(source, color, ColorConversionCodes.BGRA2BGR);
            Cv2.ExtractChannel(source, alpha, 3);
            Cv2.BilateralFilter(color, filteredColor, diameter, sigmaColor, sigmaSpace);
            Cv2.CvtColor(filteredColor, destination, ColorConversionCodes.BGR2BGRA);
            Cv2.InsertChannel(alpha, destination, 3);
        }

        private static int EnsureOdd(int value)
        {
            value = Math.Max(1, value);
            return value % 2 == 0 ? value + 1 : value;
        }

        private static Mat RequestDestination(Func<Mat> createDestination, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Mat destination = createDestination() ?? throw new InvalidOperationException("The tone destination factory returned no image.");
            cancellationToken.ThrowIfCancellationRequested();
            return destination;
        }

        private static void ApplyIntegerToneAdjustment(Mat source, Func<Mat> createDestination, MatType depth,
            double alpha, double beta, double gamma, double maximum, CancellationToken cancellationToken)
        {
            using Mat ramp = CreateUnsignedRamp(depth);
            using Mat table = new();
            // Use the original OpenCV passes to retain their saturation and rounding at every integer code.
            ApplyToneAdjustment(ramp, () => table, alpha, beta, gamma, maximum, cancellationToken);
            using Mat? alphaTable = source.Channels() == 4 ? new Mat() : null;
            if (alphaTable != null) Cv2.Merge([table, table, table, ramp], alphaTable);

            Mat destination = RequestDestination(createDestination, cancellationToken);
            using Mat? snapshot = OverlapsAtDifferentOffsets(source, destination) ? source.Clone() : null;
            cancellationToken.ThrowIfCancellationRequested();
            Cv2.LUT(snapshot ?? source, alphaTable ?? table, destination);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static bool OverlapsAtDifferentOffsets(Mat source, Mat destination)
        {
            if (ReferenceEquals(source, destination)) return false;
            nuint sourceStart = (nuint)source.Data;
            nuint destinationStart = (nuint)destination.Data;
            if (sourceStart == destinationStart && source.Step() == destination.Step()) return false;
            return destinationStart < (nuint)source.DataEnd && sourceStart < (nuint)destination.DataEnd;
        }

        internal static unsafe Mat CreateUnsignedRamp(MatType depth)
        {
            int length = depth == MatType.CV_16U ? ushort.MaxValue + 1 : byte.MaxValue + 1;
            Mat ramp = new(1, length, depth);
            if (depth == MatType.CV_16U)
            {
                ushort* pixels = (ushort*)ramp.Data;
                for (int index = 0; index < length; index++) pixels[index] = (ushort)index;
            }
            else
            {
                byte* pixels = (byte*)ramp.Data;
                for (int index = 0; index < length; index++) pixels[index] = (byte)index;
            }
            return ramp;
        }

        private static void ApplyToneAdjustment(Mat source, Func<Mat> createDestination, double alpha, double beta, double gamma, double maximum, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            MatType sourceType = source.Type();
            MatType workingType = source.Depth() == MatType.CV_64F
                ? MatType.MakeType(MatType.CV_64F, source.Channels())
                : MatType.MakeType(MatType.CV_32F, source.Channels());
            using Mat normalized = new();
            source.ConvertTo(normalized, workingType, 1 / maximum);
            cancellationToken.ThrowIfCancellationRequested();
            normalized.ConvertTo(normalized, workingType, alpha, beta);
            cancellationToken.ThrowIfCancellationRequested();
            Cv2.Max(normalized, 0, normalized);
            cancellationToken.ThrowIfCancellationRequested();
            Cv2.Pow(normalized, 1 / gamma, normalized);
            cancellationToken.ThrowIfCancellationRequested();
            Cv2.Min(normalized, 1, normalized);
            cancellationToken.ThrowIfCancellationRequested();

            Mat destination = RequestDestination(createDestination, cancellationToken);
            normalized.ConvertTo(destination, sourceType, maximum);
            cancellationToken.ThrowIfCancellationRequested();
        }

        private static double GetNominalMaximum(MatType depth)
        {
            if (depth == MatType.CV_8U)
            {
                return byte.MaxValue;
            }

            if (depth == MatType.CV_16U)
            {
                return ushort.MaxValue;
            }

            if (depth == MatType.CV_32F || depth == MatType.CV_64F)
            {
                return 1;
            }

            throw new NotSupportedException($"Unsupported image depth for basic adjustment: {depth}");
        }
    }
}
