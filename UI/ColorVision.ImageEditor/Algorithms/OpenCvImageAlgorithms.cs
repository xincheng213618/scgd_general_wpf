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
        public static void Invert(Mat mat)
        {
            Cv2.BitwiseNot(mat, mat);
        }

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

            // Integer identity is exact across the full sample range. Float inputs still need clipping.
            if ((depth == MatType.CV_8U || depth == MatType.CV_16U)
                && alpha == 1 && beta == 0 && gammaValue == 1 && !source.Empty())
            {
                Mat destination = RequestDestination(createDestination, cancellationToken);
                if (!ReferenceEquals(source, destination)) source.CopyTo(destination);
                cancellationToken.ThrowIfCancellationRequested();
                return;
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

        public static void Threshold(Mat mat, double threshold, double maxValue, ThresholdTypes type = ThresholdTypes.Binary)
        {
            Cv2.Threshold(mat, mat, threshold, maxValue, type);
        }

        public static void GaussianBlur(Mat mat, int kernelSize, double sigma)
        {
            Cv2.GaussianBlur(mat, mat, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)), sigma);
        }

        public static void MedianBlur(Mat mat, int kernelSize)
        {
            Cv2.MedianBlur(mat, mat, EnsureOdd(kernelSize));
        }

        public static void Sharpen(Mat mat)
        {
            using Mat kernel = Mat.FromArray(new float[,]
            {
                { 0, -1, 0 },
                { -1, 5, -1 },
                { 0, -1, 0 },
            });
            Cv2.Filter2D(mat, mat, mat.Depth(), kernel);
        }

        public static void Morphology(Mat mat, MorphologyOperation operation, int kernelSize, int iterations)
        {
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)));
            iterations = Math.Max(1, iterations);

            switch (operation)
            {
                case MorphologyOperation.Erode:
                    Cv2.Erode(mat, mat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Dilate:
                    Cv2.Dilate(mat, mat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Open:
                    Cv2.MorphologyEx(mat, mat, MorphTypes.Open, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Close:
                    Cv2.MorphologyEx(mat, mat, MorphTypes.Close, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Gradient:
                    Cv2.MorphologyEx(mat, mat, MorphTypes.Gradient, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.TopHat:
                    Cv2.MorphologyEx(mat, mat, MorphTypes.TopHat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.BlackHat:
                    Cv2.MorphologyEx(mat, mat, MorphTypes.BlackHat, kernel, iterations: iterations);
                    break;
            }
        }

        public static void FilterDenoise(Mat mat, FilterDenoiseOperation operation, int kernelSize, double sigmaColor, double sigmaSpace)
        {
            if (operation == FilterDenoiseOperation.Blur)
            {
                Cv2.Blur(mat, mat, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)));
                return;
            }

            ApplyBilateral(mat, EnsureOdd(kernelSize), sigmaColor, sigmaSpace);
        }

        private static void ApplyBilateral(Mat mat, int diameter, double sigmaColor, double sigmaSpace)
        {
            if (mat.Depth() != MatType.CV_8U && mat.Depth() != MatType.CV_32F)
            {
                using Mat converted = new();
                mat.ConvertTo(converted, MatType.MakeType(MatType.CV_32F, mat.Channels()));
                ApplyBilateral(converted, diameter, sigmaColor, sigmaSpace);
                converted.ConvertTo(mat, mat.Type());
                return;
            }

            if (mat.Channels() != 4)
            {
                using Mat source = mat.Clone();
                Cv2.BilateralFilter(source, mat, diameter, sigmaColor, sigmaSpace);
                return;
            }

            using Mat color = new();
            using Mat filteredColor = new();
            using Mat alpha = new();
            Cv2.CvtColor(mat, color, ColorConversionCodes.BGRA2BGR);
            Cv2.ExtractChannel(mat, alpha, 3);
            Cv2.BilateralFilter(color, filteredColor, diameter, sigmaColor, sigmaSpace);
            Cv2.CvtColor(filteredColor, mat, ColorConversionCodes.BGR2BGRA);
            Cv2.InsertChannel(alpha, mat, 3);
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
