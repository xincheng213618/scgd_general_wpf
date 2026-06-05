using OpenCvSharp;
using System;
using System.Linq;

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

        public static void AdjustBrightnessContrast(Mat mat, double contrast, double brightness)
        {
            double alpha = contrast / 300 + 1;
            double beta = brightness * 4 / 5;
            if (mat.Depth() != MatType.CV_8U)
            {
                beta *= 255;
            }

            using Mat source = mat.Clone();
            source.ConvertTo(mat, mat.Type(), alpha, beta);
        }

        public static void Threshold(Mat mat, double threshold, double maxValue, ThresholdTypes type = ThresholdTypes.Binary)
        {
            using Mat source = mat.Clone();
            Cv2.Threshold(source, mat, threshold, maxValue, type);
        }

        public static void GaussianBlur(Mat mat, int kernelSize, double sigma)
        {
            using Mat source = mat.Clone();
            Cv2.GaussianBlur(source, mat, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)), sigma);
        }

        public static void MedianBlur(Mat mat, int kernelSize)
        {
            using Mat source = mat.Clone();
            Cv2.MedianBlur(source, mat, EnsureOdd(kernelSize));
        }

        public static void Sharpen(Mat mat)
        {
            using Mat source = mat.Clone();
            using Mat kernel = Mat.FromArray(new float[,]
            {
                { 0, -1, 0 },
                { -1, 5, -1 },
                { 0, -1, 0 },
            });
            Cv2.Filter2D(source, mat, mat.Depth(), kernel);
        }

        public static void Morphology(Mat mat, MorphologyOperation operation, int kernelSize, int iterations)
        {
            using Mat source = mat.Clone();
            using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Rect, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)));
            iterations = Math.Max(1, iterations);

            switch (operation)
            {
                case MorphologyOperation.Erode:
                    Cv2.Erode(source, mat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Dilate:
                    Cv2.Dilate(source, mat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Open:
                    Cv2.MorphologyEx(source, mat, MorphTypes.Open, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Close:
                    Cv2.MorphologyEx(source, mat, MorphTypes.Close, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.Gradient:
                    Cv2.MorphologyEx(source, mat, MorphTypes.Gradient, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.TopHat:
                    Cv2.MorphologyEx(source, mat, MorphTypes.TopHat, kernel, iterations: iterations);
                    break;
                case MorphologyOperation.BlackHat:
                    Cv2.MorphologyEx(source, mat, MorphTypes.BlackHat, kernel, iterations: iterations);
                    break;
            }
        }

        public static void FilterDenoise(Mat mat, FilterDenoiseOperation operation, int kernelSize, double sigmaColor, double sigmaSpace)
        {
            if (operation == FilterDenoiseOperation.Blur)
            {
                using Mat source = mat.Clone();
                Cv2.Blur(source, mat, new OpenCvSharp.Size(EnsureOdd(kernelSize), EnsureOdd(kernelSize)));
                return;
            }

            ApplyBilateral(mat, EnsureOdd(kernelSize), sigmaColor, sigmaSpace);
        }

        private static void ApplyBilateral(Mat mat, int diameter, double sigmaColor, double sigmaSpace)
        {
            if (mat.Channels() != 4)
            {
                using Mat source = mat.Clone();
                Cv2.BilateralFilter(source, mat, diameter, sigmaColor, sigmaSpace);
                return;
            }

            Mat[] channels = Cv2.Split(mat);
            try
            {
                using Mat color = new();
                using Mat filteredColor = new();
                Cv2.Merge(channels.Take(3).ToArray(), color);
                Cv2.BilateralFilter(color, filteredColor, diameter, sigmaColor, sigmaSpace);

                Mat[] filteredChannels = Cv2.Split(filteredColor);
                try
                {
                    Cv2.Merge(new[] { filteredChannels[0], filteredChannels[1], filteredChannels[2], channels[3] }, mat);
                }
                finally
                {
                    foreach (Mat channel in filteredChannels)
                    {
                        channel.Dispose();
                    }
                }
            }
            finally
            {
                foreach (Mat channel in channels)
                {
                    channel.Dispose();
                }
            }
        }

        private static int EnsureOdd(int value)
        {
            return value % 2 == 0 ? value + 1 : value;
        }
    }
}
