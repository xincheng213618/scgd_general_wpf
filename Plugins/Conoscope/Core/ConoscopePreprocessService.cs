using log4net;
using OpenCvSharp;
using System;

namespace Conoscope.Core
{
    public sealed record ConoscopeDustRemovalOptions(
        DustRemovalMode Mode,
        double ThresholdPercent,
        int MinArea,
        int MaxArea,
        int RepairRadius);

    public sealed record ConoscopePreprocessOptions(
        bool ClampNonPositiveXyz,
        float PositiveFloor,
        bool DustRemovalEnabled,
        ConoscopeDustRemovalOptions DustRemoval,
        ImageFilterType FilterType,
        int KernelSize,
        double Sigma,
        int BilateralD,
        double SigmaColor,
        double SigmaSpace)
    {
        public static ConoscopePreprocessOptions FromConfig(ConoscopeConfig config, float positiveFloor)
        {
            int minArea = Math.Max(1, config.DustMinArea);
            int maxArea = Math.Max(minArea, config.DustMaxArea);
            ImageFilterType filterType = Enum.IsDefined(config.FilterType)
                ? config.FilterType
                : ImageFilterType.None;

            return new ConoscopePreprocessOptions(
                config.ClampNonPositiveXyzOnLoad,
                positiveFloor,
                config.DustRemovalEnabled,
                new ConoscopeDustRemovalOptions(
                    config.DustRemovalMode,
                    config.DustThresholdPercent,
                    minArea,
                    maxArea,
                    Math.Max(1, config.DustRepairRadius)),
                filterType,
                ConoscopeNumericHelper.NormalizeOddKernelSize(config.FilterKernelSize),
                config.FilterSigma,
                Math.Max(1, config.FilterD),
                config.FilterSigmaColor,
                config.FilterSigmaSpace);
        }
    }

    internal static class ConoscopePreprocessService
    {
        public static int ClampNonPositive(Mat mat, float lowerBound)
        {
            using Mat mask = new Mat();
            Cv2.Compare(mat, Scalar.All(0), mask, CmpTypes.LE);
            int count = Cv2.CountNonZero(mask);
            if (count > 0)
            {
                mat.SetTo(Scalar.All(lowerBound), mask);
            }

            return count;
        }

        public static void Apply(ref Mat? xMat, ref Mat? yMat, ref Mat? zMat, ConoscopePreprocessOptions options, ILog? log = null)
        {
            if (xMat == null || yMat == null || zMat == null)
            {
                return;
            }

            Mat xSource = xMat;
            Mat ySource = yMat;
            Mat zSource = zMat;

            if (options.DustRemovalEnabled)
            {
                ConoscopeDustRemovalSummary summary = ConoscopeDustRemovalProcessor.Apply(ref xSource, ref ySource, ref zSource, options.DustRemoval);
                if (!summary.HasCandidates)
                {
                    log?.Info($"灰尘滤除未检测到候选区域: mode={options.DustRemoval.Mode}, threshold={options.DustRemoval.ThresholdPercent:F1}%");
                }
                else
                {
                    log?.Info(
                        $"灰尘滤除完成: mode={options.DustRemoval.Mode}, darkComponents={summary.DarkComponentCount}, brightComponents={summary.BrightComponentCount}, darkPixels={summary.DarkPixelCount}, brightPixels={summary.BrightPixelCount}, threshold={options.DustRemoval.ThresholdPercent:F1}%, area={options.DustRemoval.MinArea}-{options.DustRemoval.MaxArea}, radius={options.DustRemoval.RepairRadius}");
                }
            }

            if (options.FilterType != ImageFilterType.None)
            {
                ArgumentNullException.ThrowIfNull(xSource);
                ArgumentNullException.ThrowIfNull(ySource);
                ArgumentNullException.ThrowIfNull(zSource);
                xSource = ReplaceWithFilteredMat(xSource, options);
                ySource = ReplaceWithFilteredMat(ySource, options);
                zSource = ReplaceWithFilteredMat(zSource, options);
                log?.Info($"滤波应用到XYZ通道完成: {options.FilterType}, kernelSize={options.KernelSize}");
            }

            xMat = xSource;
            yMat = ySource;
            zMat = zSource;
        }

        private static Mat ReplaceWithFilteredMat(Mat source, ConoscopePreprocessOptions options)
        {
            Mat filtered = ApplyFilter(source, options);
            source.Dispose();
            return filtered;
        }

        private static Mat ApplyFilter(Mat src, ConoscopePreprocessOptions options)
        {
            Mat dst = new Mat();
            Mat workMat = src;

            switch (options.FilterType)
            {
                case ImageFilterType.LowPass:
                    Cv2.Blur(workMat, dst, new Size(options.KernelSize, options.KernelSize));
                    break;
                case ImageFilterType.MovingAverage:
                    Cv2.BoxFilter(workMat, dst, workMat.Type(), new Size(options.KernelSize, options.KernelSize));
                    break;
                case ImageFilterType.Gaussian:
                    Cv2.GaussianBlur(workMat, dst, new Size(options.KernelSize, options.KernelSize), options.Sigma);
                    break;
                case ImageFilterType.Median:
                    if (src.Depth() == MatType.CV_32F)
                    {
                        Cv2.MedianBlur(workMat, dst, options.KernelSize);
                    }
                    else
                    {
                        using Mat floatMat = new Mat();
                        workMat.ConvertTo(floatMat, MatType.CV_32FC1);
                        Cv2.MedianBlur(floatMat, dst, options.KernelSize);
                        Mat result = new Mat();
                        dst.ConvertTo(result, src.Type());
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                case ImageFilterType.Bilateral:
                    if (src.Depth() == MatType.CV_32F)
                    {
                        Cv2.BilateralFilter(workMat, dst, options.BilateralD, options.SigmaColor, options.SigmaSpace);
                    }
                    else
                    {
                        using Mat floatMat = new Mat();
                        workMat.ConvertTo(floatMat, MatType.CV_32FC1);
                        Cv2.BilateralFilter(floatMat, dst, options.BilateralD, options.SigmaColor, options.SigmaSpace);
                        Mat result = new Mat();
                        dst.ConvertTo(result, src.Type());
                        dst.Dispose();
                        dst = result;
                    }
                    break;
                default:
                    return src.Clone();
            }

            return dst;
        }
    }
}