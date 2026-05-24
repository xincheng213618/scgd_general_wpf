using Conoscope.Core;
using OpenCvSharp;
using System;

namespace Conoscope.Processing.Preprocess
{
    public sealed record DustRemovalOptions(
        DustRemovalMode Mode,
        double ThresholdPercent,
        int MinArea,
        int MaxArea,
        int RepairRadius);

    internal readonly record struct DustRemovalSummary(
        int DarkComponentCount,
        int BrightComponentCount,
        int DarkPixelCount,
        int BrightPixelCount)
    {
        public bool HasCandidates => DarkPixelCount > 0 || BrightPixelCount > 0;
    }

    internal static class DustRemovalProcessor
    {
        public static DustRemovalSummary Apply(ref Mat? xMat, ref Mat? yMat, ref Mat? zMat, DustRemovalOptions options)
        {
            if (xMat == null || yMat == null || zMat == null)
            {
                return default;
            }

            int darkComponents;
            int brightComponents;
            using Mat darkMask = ShouldDetectDarkDust(options.Mode)
                ? CreateDustMask(yMat, options, darkSpot: true, out darkComponents)
                : CreateEmptyMask(yMat, out darkComponents);
            using Mat brightMask = ShouldDetectBrightDust(options.Mode)
                ? CreateDustMask(yMat, options, darkSpot: false, out brightComponents)
                : CreateEmptyMask(yMat, out brightComponents);

            int darkPixels = Cv2.CountNonZero(darkMask);
            int brightPixels = Cv2.CountNonZero(brightMask);
            if (darkPixels == 0 && brightPixels == 0)
            {
                return new DustRemovalSummary(darkComponents, brightComponents, darkPixels, brightPixels);
            }

            xMat = ReplaceChannelWithDustRepair(xMat, darkMask, brightMask, options);
            yMat = ReplaceChannelWithDustRepair(yMat, darkMask, brightMask, options);
            zMat = ReplaceChannelWithDustRepair(zMat, darkMask, brightMask, options);
            return new DustRemovalSummary(darkComponents, brightComponents, darkPixels, brightPixels);
        }

        public static DustRemovalSummary ApplyToSingleChannel(ref Mat? channelMat, Mat luminanceMat, DustRemovalOptions options)
        {
            if (channelMat == null)
            {
                return default;
            }

            int darkComponents;
            int brightComponents;
            using Mat darkMask = ShouldDetectDarkDust(options.Mode)
                ? CreateDustMask(luminanceMat, options, darkSpot: true, out darkComponents)
                : CreateEmptyMask(luminanceMat, out darkComponents);
            using Mat brightMask = ShouldDetectBrightDust(options.Mode)
                ? CreateDustMask(luminanceMat, options, darkSpot: false, out brightComponents)
                : CreateEmptyMask(luminanceMat, out brightComponents);

            int darkPixels = Cv2.CountNonZero(darkMask);
            int brightPixels = Cv2.CountNonZero(brightMask);
            if (darkPixels == 0 && brightPixels == 0)
            {
                return new DustRemovalSummary(darkComponents, brightComponents, darkPixels, brightPixels);
            }

            channelMat = ReplaceChannelWithDustRepair(channelMat, darkMask, brightMask, options);
            return new DustRemovalSummary(darkComponents, brightComponents, darkPixels, brightPixels);
        }

        private static bool ShouldDetectDarkDust(DustRemovalMode mode)
        {
            return mode is DustRemovalMode.DarkSpot or DustRemovalMode.Both;
        }

        private static bool ShouldDetectBrightDust(DustRemovalMode mode)
        {
            return mode is DustRemovalMode.BrightSpot or DustRemovalMode.Both;
        }

        private static Mat CreateEmptyMask(Mat source, out int componentCount)
        {
            componentCount = 0;
            return new Mat(source.Rows, source.Cols, MatType.CV_8UC1, Scalar.All(0));
        }

        private static Mat CreateDustMask(Mat luminance, DustRemovalOptions options, bool darkSpot, out int componentCount)
        {
            using Mat gray8 = NormalizeToGray8(luminance);
            int backgroundKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize(options.RepairRadius * 2 + 1);
            using Mat kernel = Cv2.GetStructuringElement(
                MorphShapes.Ellipse,
                new Size(backgroundKernelSize, backgroundKernelSize));
            using Mat background = new Mat();
            using Mat diff = new Mat();
            using Mat rawMask = new Mat();

            Cv2.MorphologyEx(gray8, background, darkSpot ? MorphTypes.Close : MorphTypes.Open, kernel);
            if (darkSpot)
            {
                Cv2.Subtract(background, gray8, diff);
            }
            else
            {
                Cv2.Subtract(gray8, background, diff);
            }

            double threshold = Math.Max(1, Math.Min(255, 255.0 * options.ThresholdPercent / 100.0));
            Cv2.Threshold(diff, rawMask, threshold, 255, ThresholdTypes.Binary);

            Mat filteredMask = FilterMaskByArea(rawMask, options.MinArea, options.MaxArea, out componentCount);
            if (componentCount > 0)
            {
                int dilateKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize(Math.Max(1, options.RepairRadius));
                using Mat dilateKernel = Cv2.GetStructuringElement(
                    MorphShapes.Ellipse,
                    new Size(dilateKernelSize, dilateKernelSize));
                Cv2.Dilate(filteredMask, filteredMask, dilateKernel);
            }

            return filteredMask;
        }

        private static Mat NormalizeToGray8(Mat source)
        {
            Mat normalized = new Mat();
            Mat gray8 = new Mat();
            Cv2.Normalize(source, normalized, 0, 255, NormTypes.MinMax);
            normalized.ConvertTo(gray8, MatType.CV_8UC1);
            normalized.Dispose();
            return gray8;
        }

        private static Mat FilterMaskByArea(Mat rawMask, int minArea, int maxArea, out int componentCount)
        {
            Mat filtered = new Mat(rawMask.Rows, rawMask.Cols, MatType.CV_8UC1, Scalar.All(0));
            using Mat labels = new Mat();
            using Mat stats = new Mat();
            using Mat centroids = new Mat();

            int labelsCount = Cv2.ConnectedComponentsWithStats(rawMask, labels, stats, centroids);
            componentCount = 0;
            for (int labelIndex = 1; labelIndex < labelsCount; labelIndex++)
            {
                int area = stats.At<int>(labelIndex, 4);
                if (area < minArea || area > maxArea)
                {
                    continue;
                }

                using Mat componentMask = new Mat();
                Cv2.InRange(labels, new Scalar(labelIndex), new Scalar(labelIndex), componentMask);
                filtered.SetTo(new Scalar(255), componentMask);
                componentCount++;
            }

            return filtered;
        }

        private static Mat ReplaceChannelWithDustRepair(Mat channel, Mat darkMask, Mat brightMask, DustRemovalOptions options)
        {
            Mat repaired = ApplyDustRepairToChannel(channel, darkMask, brightMask, options);
            channel.Dispose();
            return repaired;
        }

        private static Mat ApplyDustRepairToChannel(Mat source, Mat darkMask, Mat brightMask, DustRemovalOptions options)
        {
            Mat result = source.Clone();
            int backgroundKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize(options.RepairRadius * 2 + 1);
            using Mat kernel = Cv2.GetStructuringElement(
                MorphShapes.Ellipse,
                new Size(backgroundKernelSize, backgroundKernelSize));

            if (Cv2.CountNonZero(darkMask) > 0)
            {
                using Mat darkBackground = new Mat();
                Cv2.MorphologyEx(source, darkBackground, MorphTypes.Close, kernel);
                darkBackground.CopyTo(result, darkMask);
            }

            if (Cv2.CountNonZero(brightMask) > 0)
            {
                using Mat brightBackground = new Mat();
                Cv2.MorphologyEx(source, brightBackground, MorphTypes.Open, kernel);
                brightBackground.CopyTo(result, brightMask);
            }

            return result;
        }
    }
}