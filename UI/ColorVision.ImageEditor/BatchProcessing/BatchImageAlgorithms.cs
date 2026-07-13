using ColorVision.Core;
using ColorVision.ImageEditor.Algorithms;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ColorVision.ImageEditor.BatchProcessing
{
    public sealed class BatchImageAlgorithmDefinition
    {
        private readonly Func<Mat, Mat> _apply;

        public BatchImageAlgorithmDefinition(string name, string suffix, object options, Func<Mat, Mat> apply)
        {
            Name = name;
            Suffix = suffix;
            Options = options;
            _apply = apply;
        }

        public string Name { get; }

        public string Suffix { get; }

        public object Options { get; }

        public Mat Apply(Mat source) => _apply(source);
    }

    public static class BatchImageAlgorithms
    {
        public static IReadOnlyList<BatchImageAlgorithmDefinition> CreateAll()
        {
            PseudoColorBatchOptions pseudoColor = new();
            WhiteBalanceBatchOptions whiteBalance = new();
            GammaBatchOptions gamma = new();
            BrightnessContrastBatchOptions brightnessContrast = new();
            ThresholdBatchOptions threshold = new();
            GaussianBlurBatchOptions gaussianBlur = new();
            MedianBlurBatchOptions medianBlur = new();
            CannyBatchOptions canny = new();
            MorphologyBatchOptions morphology = new();
            FilterDenoiseBatchOptions denoise = new();

            return new List<BatchImageAlgorithmDefinition>
            {
                InPlace("反相", "_invert", new NoBatchAlgorithmOptions(), OpenCvImageAlgorithms.Invert),
                new("伪彩色", "_pseudo", pseudoColor, source => ApplyPseudoColor(source, pseudoColor)),
                new("自动色阶", "_autolevels", new NoBatchAlgorithmOptions(), ApplyAutoLevels),
                new("白平衡", "_whitebalance", whiteBalance, source => ApplyWhiteBalance(source, whiteBalance)),
                new("伽马校正", "_gamma", gamma, source => ApplyGamma(source, gamma)),
                InPlace("亮度/对比度", "_brightness", brightnessContrast,
                    mat => OpenCvImageAlgorithms.AdjustBrightnessContrast(mat, brightnessContrast.Contrast, brightnessContrast.Brightness)),
                InPlace("阈值处理", "_threshold", threshold,
                    mat => OpenCvImageAlgorithms.Threshold(mat, threshold.Threshold, GetMaximum(mat.Depth()), ThresholdTypes.Binary)),
                InPlace("锐化", "_sharpen", new NoBatchAlgorithmOptions(), OpenCvImageAlgorithms.Sharpen),
                InPlace("高斯模糊", "_gaussian", gaussianBlur,
                    mat => OpenCvImageAlgorithms.GaussianBlur(mat, gaussianBlur.KernelSize, gaussianBlur.Sigma)),
                InPlace("中值滤波", "_median", medianBlur,
                    mat => OpenCvImageAlgorithms.MedianBlur(mat, medianBlur.KernelSize)),
                new("Canny 边缘检测", "_canny", canny, source => ApplyCanny(source, canny)),
                new("直方图均衡化", "_equalized", new NoBatchAlgorithmOptions(), ApplyHistogramEqualization),
                InPlace("形态学操作", "_morphology", morphology,
                    mat => OpenCvImageAlgorithms.Morphology(mat, (MorphologyOperation)morphology.Operation, morphology.KernelSize, morphology.Iterations)),
                InPlace("降噪滤波", "_denoise", denoise,
                    mat => OpenCvImageAlgorithms.FilterDenoise(mat, (FilterDenoiseOperation)denoise.Operation, denoise.KernelSize, denoise.SigmaColor, denoise.SigmaSpace)),
            };
        }

        private static BatchImageAlgorithmDefinition InPlace(string name, string suffix, object options, Action<Mat> apply)
        {
            return new BatchImageAlgorithmDefinition(name, suffix, options, source =>
            {
                Mat result = source.Clone();
                try
                {
                    apply(result);
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            });
        }

        private static Mat ApplyPseudoColor(Mat source, PseudoColorBatchOptions options)
        {
            using Mat gray = ConvertToGray(source);
            using Mat gray8 = ConvertTo8Bit(gray);
            Mat result = new();
            Cv2.ApplyColorMap(gray8, result, (OpenCvSharp.ColormapTypes)(int)options.Colormap);
            return result;
        }

        private static Mat ApplyAutoLevels(Mat source)
        {
            Mat result = new();
            Cv2.Normalize(source, result, 0, GetMaximum(source.Depth()), NormTypes.MinMax);
            return result;
        }

        private static Mat ApplyWhiteBalance(Mat source, WhiteBalanceBatchOptions options)
        {
            if (source.Channels() < 3)
            {
                throw new InvalidOperationException("白平衡仅支持三通道或四通道彩色图像。");
            }

            Mat[] channels = Cv2.Split(source);
            try
            {
                channels[0].ConvertTo(channels[0], channels[0].Type(), options.BlueScale);
                channels[1].ConvertTo(channels[1], channels[1].Type(), options.GreenScale);
                channels[2].ConvertTo(channels[2], channels[2].Type(), options.RedScale);
                Mat result = new();
                Cv2.Merge(channels, result);
                return result;
            }
            finally
            {
                foreach (Mat channel in channels)
                {
                    channel.Dispose();
                }
            }
        }

        private static Mat ApplyGamma(Mat source, GammaBatchOptions options)
        {
            double gamma = Math.Max(0.01, options.Gamma);
            double maximum = GetMaximum(source.Depth());
            using Mat normalized = new();
            source.ConvertTo(normalized, MatType.MakeType(MatType.CV_32F, source.Channels()), 1d / maximum);
            Cv2.Pow(normalized, 1d / gamma, normalized);
            Mat result = new();
            normalized.ConvertTo(result, source.Type(), maximum);
            return result;
        }

        private static Mat ApplyCanny(Mat source, CannyBatchOptions options)
        {
            using Mat gray = ConvertToGray(source);
            using Mat gray8 = ConvertTo8Bit(gray);
            Mat result = new();
            Cv2.Canny(gray8, result, options.LowThreshold, options.HighThreshold);
            return result;
        }

        private static Mat ApplyHistogramEqualization(Mat source)
        {
            using Mat source8 = ConvertTo8Bit(source);
            if (source8.Channels() == 1)
            {
                Mat grayResult = new();
                Cv2.EqualizeHist(source8, grayResult);
                return grayResult;
            }

            using Mat bgr = ConvertToBgr(source8);
            using Mat yCrCb = new();
            Cv2.CvtColor(bgr, yCrCb, ColorConversionCodes.BGR2YCrCb);
            Mat[] channels = Cv2.Split(yCrCb);
            try
            {
                Cv2.EqualizeHist(channels[0], channels[0]);
                using Mat merged = new();
                Cv2.Merge(channels, merged);
                Mat result = new();
                Cv2.CvtColor(merged, result, ColorConversionCodes.YCrCb2BGR);
                return result;
            }
            finally
            {
                foreach (Mat channel in channels)
                {
                    channel.Dispose();
                }
            }
        }

        private static Mat ConvertToGray(Mat source)
        {
            if (source.Channels() == 1)
            {
                return source.Clone();
            }

            Mat result = new();
            ColorConversionCodes conversion = source.Channels() == 4
                ? ColorConversionCodes.BGRA2GRAY
                : ColorConversionCodes.BGR2GRAY;
            Cv2.CvtColor(source, result, conversion);
            return result;
        }

        private static Mat ConvertToBgr(Mat source)
        {
            if (source.Channels() == 3)
            {
                return source.Clone();
            }

            Mat result = new();
            ColorConversionCodes conversion = source.Channels() == 4
                ? ColorConversionCodes.BGRA2BGR
                : ColorConversionCodes.GRAY2BGR;
            Cv2.CvtColor(source, result, conversion);
            return result;
        }

        private static Mat ConvertTo8Bit(Mat source)
        {
            if (source.Depth() == MatType.CV_8U)
            {
                return source.Clone();
            }

            using Mat normalized = new();
            Cv2.Normalize(source, normalized, 0, byte.MaxValue, NormTypes.MinMax);
            Mat result = new();
            normalized.ConvertTo(result, MatType.CV_8U);
            return result;
        }

        private static double GetMaximum(MatType depth)
        {
            if (depth == MatType.CV_16U)
            {
                return ushort.MaxValue;
            }

            if (depth == MatType.CV_32F || depth == MatType.CV_64F)
            {
                return 1d;
            }

            return byte.MaxValue;
        }
    }

    internal sealed class NoBatchAlgorithmOptions
    {
    }

    internal sealed class PseudoColorBatchOptions
    {
        [DisplayName("色图")]
        public ColorVision.Core.ColormapTypes Colormap { get; set; } = ColorVision.Core.ColormapTypes.COLORMAP_JET;
    }

    internal sealed class WhiteBalanceBatchOptions
    {
        [DisplayName("红色通道系数")]
        public double RedScale { get; set; } = 1;

        [DisplayName("绿色通道系数")]
        public double GreenScale { get; set; } = 1;

        [DisplayName("蓝色通道系数")]
        public double BlueScale { get; set; } = 1;
    }

    internal sealed class GammaBatchOptions
    {
        [DisplayName("Gamma")]
        public double Gamma { get; set; } = 1;
    }

    internal sealed class BrightnessContrastBatchOptions
    {
        [DisplayName("亮度 (-100~150)")]
        public double Brightness { get; set; }

        [DisplayName("对比度 (-50~100)")]
        public double Contrast { get; set; }
    }

    internal sealed class ThresholdBatchOptions
    {
        [DisplayName("阈值")]
        public double Threshold { get; set; } = 128;

    }

    internal sealed class GaussianBlurBatchOptions
    {
        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 5;

        [DisplayName("Sigma")]
        public double Sigma { get; set; } = 1.5;
    }

    internal sealed class MedianBlurBatchOptions
    {
        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 5;
    }

    internal sealed class CannyBatchOptions
    {
        [DisplayName("低阈值")]
        public double LowThreshold { get; set; } = 100;

        [DisplayName("高阈值")]
        public double HighThreshold { get; set; } = 200;
    }

    internal sealed class MorphologyBatchOptions
    {
        [DisplayName("操作")]
        public BatchMorphologyOperation Operation { get; set; } = BatchMorphologyOperation.腐蚀;

        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 3;

        [DisplayName("迭代次数")]
        public int Iterations { get; set; } = 1;
    }

    internal sealed class FilterDenoiseBatchOptions
    {
        [DisplayName("滤波类型")]
        public BatchFilterDenoiseOperation Operation { get; set; } = BatchFilterDenoiseOperation.双边滤波;

        [DisplayName("核大小（奇数）")]
        public int KernelSize { get; set; } = 5;

        [DisplayName("颜色 Sigma")]
        public double SigmaColor { get; set; } = 75;

        [DisplayName("空间 Sigma")]
        public double SigmaSpace { get; set; } = 75;
    }

    internal enum BatchMorphologyOperation
    {
        腐蚀,
        膨胀,
        开运算,
        闭运算,
        形态学梯度,
        顶帽,
        黑帽,
    }

    internal enum BatchFilterDenoiseOperation
    {
        双边滤波,
        均值模糊,
    }
}
