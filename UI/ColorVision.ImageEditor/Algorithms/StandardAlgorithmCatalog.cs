using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;

namespace ColorVision.ImageEditor.Algorithms
{
    public static class StandardAlgorithmCatalog
    {
        private static readonly HashSet<AlgorithmId> CopilotAllowlist = new()
        {
            StandardAlgorithmIds.Invert,
            StandardAlgorithmIds.Canny,
            StandardAlgorithmIds.BasicAdjustment,
            StandardAlgorithmIds.Threshold,
            StandardAlgorithmIds.Sharpen,
            StandardAlgorithmIds.GaussianBlur,
            StandardAlgorithmIds.MedianBlur,
            StandardAlgorithmIds.Morphology,
            StandardAlgorithmIds.Denoise,
            StandardAlgorithmIds.AutoLevels,
            StandardAlgorithmIds.WhiteBalance,
            StandardAlgorithmIds.HistogramEqualization,
            StandardAlgorithmIds.PseudoColor,
        };

        private static readonly IReadOnlySet<AlgorithmImageFormat> CommonFormats = new HashSet<AlgorithmImageFormat>
        {
            AlgorithmImageFormat.Gray8,
            AlgorithmImageFormat.Gray16,
            AlgorithmImageFormat.Gray32Float,
            AlgorithmImageFormat.Bgr24,
            AlgorithmImageFormat.Bgr48,
            AlgorithmImageFormat.Bgr96Float,
            AlgorithmImageFormat.Bgra32,
            AlgorithmImageFormat.Bgra64,
            AlgorithmImageFormat.Bgra128Float,
        };

        private static readonly IReadOnlySet<AlgorithmImageFormat> ColorFormats = new HashSet<AlgorithmImageFormat>
        {
            AlgorithmImageFormat.Bgr24,
            AlgorithmImageFormat.Bgr48,
            AlgorithmImageFormat.Bgr96Float,
            AlgorithmImageFormat.Bgra32,
            AlgorithmImageFormat.Bgra64,
            AlgorithmImageFormat.Bgra128Float,
        };

        private const AlgorithmHostCapabilities CommonCapabilities = AlgorithmHostCapabilities.Interactive
            | AlgorithmHostCapabilities.Batch
            | AlgorithmHostCapabilities.Flow
            | AlgorithmHostCapabilities.Headless
            | AlgorithmHostCapabilities.Local
            | AlgorithmHostCapabilities.Deterministic;

        public static AlgorithmCatalog Create()
        {
            AlgorithmCatalog catalog = new();
            catalog.Register(Descriptor(StandardAlgorithmIds.Invert, "反相", "像素处理", "逐位反转像素；保持尺寸、位深和通道。", new NoAlgorithmParameters(), CommonFormats, "_invert"), "Invert", "InvertImage");
            catalog.Register(Descriptor(StandardAlgorithmIds.Canny, "Canny 边缘检测", "像素处理", "转换为 Gray8 后执行 Canny，输出 Gray8。", new CannyParameters(), CommonFormats, "_canny"), "Canny", "EdgeDetection");
            catalog.Register(Descriptor(StandardAlgorithmIds.BasicAdjustment, "基础调整", "像素处理", "曝光、亮度、对比度和 Gamma 调整；四通道 alpha 保持不变。", new BasicAdjustmentParameters(), CommonFormats, "_adjusted"), "BasicAdjustment");
            catalog.Register(Descriptor(StandardAlgorithmIds.Threshold, "阈值处理", "像素处理", "逐通道二值阈值；保持输入格式。", new ThresholdParameters(), CommonFormats, "_threshold"), "Threshold");
            catalog.Register(Descriptor(StandardAlgorithmIds.Sharpen, "锐化", "像素处理", "固定 3x3 锐化核；保持输入格式。", new NoAlgorithmParameters(), CommonFormats, "_sharpen"), "Sharpen");
            catalog.Register(Descriptor(StandardAlgorithmIds.GaussianBlur, "高斯模糊", "像素处理", "奇数核高斯模糊；保持输入格式。", new GaussianBlurParameters(), CommonFormats, "_gaussian"), "GaussianBlur");
            catalog.Register(Descriptor(StandardAlgorithmIds.MedianBlur, "中值滤波", "像素处理", "奇数核中值滤波；保持输入格式。", new MedianBlurParameters(), CommonFormats, "_median"), "MedianBlur");
            catalog.Register(Descriptor(StandardAlgorithmIds.Morphology, "形态学操作", "像素处理", "腐蚀、膨胀、开闭运算、梯度、顶帽或黑帽。", new MorphologyParameters(), CommonFormats, "_morphology"), "Morphology", "Erode", "Dilate", "MorphologyEx");
            catalog.Register(Descriptor(StandardAlgorithmIds.Denoise, "降噪滤波", "像素处理", "双边滤波或均值滤波；四通道 alpha 保持不变。", new DenoiseParameters(), CommonFormats, "_denoise"), "Denoise", "BilateralFilter", "Blur");
            catalog.Register(Descriptor(StandardAlgorithmIds.AutoLevels, "自动色阶", "像素处理", "按输入全局最小值和最大值拉伸到位深标称范围。", new NoAlgorithmParameters(), CommonFormats, "_autolevels"), "AutoLevels", "AutoLevelsAdjust");
            catalog.Register(Descriptor(StandardAlgorithmIds.WhiteBalance, "白平衡", "像素处理", "缩放 B/G/R 通道；四通道 alpha 保持不变。", new WhiteBalanceParameters(), ColorFormats, "_whitebalance"), "WhiteBalance");
            catalog.Register(Descriptor(StandardAlgorithmIds.HistogramEqualization, "直方图均衡化", "像素处理", "灰度直接均衡化，彩色在亮度通道均衡化；输出 Gray8 或 Bgr24。", new NoAlgorithmParameters(), CommonFormats, "_equalized"), "HistogramEqualization");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.RemoveMoire,
                "去除摩尔纹",
                "像素处理",
                "调用兼容 Native 去摩尔纹实现。",
                new NoAlgorithmParameters(),
                CommonFormats,
                "_demoire",
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic), "RemoveMoire");
            catalog.Register(Descriptor(StandardAlgorithmIds.PseudoColor, "伪彩色", "像素处理", "将输入灰度归一化为 Gray8 后应用色图，输出 Bgr24。", new PseudoColorParameters(), CommonFormats, "_pseudo"), "PseudoColor");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.RoiStatistics,
                "ROI 统计",
                "分析测量",
                "计算矩形、圆或多边形 ROI 的通道统计、百分位、直方图、饱和/无效值和坏点候选。",
                new RoiStatisticsParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsRectangleRoi = true,
                SupportsCircleRoi = true,
                SupportsPolygonRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
            }, "RoiStatistics", "ROIStatistics", "RoiStatisticsRectangle", "RoiStatisticsCircle", "RoiStatisticsPolygon");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.ImageProfile,
                "灰度与颜色剖面",
                "分析测量",
                "沿水平、垂直或任意折线按明确的间距、插值和边界规则采样灰度及多通道颜色曲线。",
                new ImageProfileParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsPolylineRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
            }, "ImageProfile", "LineProfile", "ProfileDataExtractor", "SectionalDrawing");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.ImageComparison,
                "图像比较",
                "分析测量",
                "严格比较两个同尺寸、同格式、同编码色彩空间的图像，输出精确差分、MSE/RMSE/PSNR/SSIM 与只读对齐预检。",
                new ImageComparisonParameters(),
                CommonFormats,
                "_comparison",
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local
                    | AlgorithmHostCapabilities.Deterministic | AlgorithmHostCapabilities.MultiInput) with
            {
                MinimumInputCount = 2,
                MaximumInputCount = 2,
                OutputFormats = CommonFormats,
                OutputFormatPolicy = "absolute=same-as-input; signed=float-same-channels; visualizations=bgr24",
                Version = new AlgorithmVersion(1, 1, 0),
                SupportsRectangleRoi = true,
                SupportsCircleRoi = true,
                SupportsPolygonRoi = true,
            }, "ImageComparison", "CompareImage", "ImageDiff");
            return catalog;
        }

        public static bool IsExplicitlyAllowedForCopilot(AlgorithmId id) => CopilotAllowlist.Contains(id);

        private static AlgorithmDescriptor Descriptor(
            AlgorithmId id,
            string name,
            string category,
            string description,
            StandardAlgorithmParameters defaults,
            IReadOnlySet<AlgorithmImageFormat> formats,
            string suffix,
            AlgorithmHostCapabilities capabilities = CommonCapabilities)
        {
            if (IsExplicitlyAllowedForCopilot(id)) capabilities |= AlgorithmHostCapabilities.Copilot;
            JsonElement defaultsJson = AlgorithmJson.ToElement(defaults);
            List<AlgorithmParameterField> fields = new();
            foreach (PropertyInfo property in defaults.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name == nameof(IAlgorithmParameters.SchemaVersion)) continue;
                object? value = property.GetValue(defaults);
                (double? minimum, double? maximum, IReadOnlyList<string>? allowed, string? unit) = ResolveFieldContract(id, property);
                fields.Add(new AlgorithmParameterField(
                    property.Name,
                    property.PropertyType.Name,
                    AlgorithmJson.ToElement(value),
                    Minimum: minimum,
                    Maximum: maximum,
                    AllowedValues: allowed,
                    Unit: unit,
                    Description: property.GetCustomAttribute<DisplayNameAttribute>()?.DisplayName));
            }

            return new AlgorithmDescriptor(
                id,
                new AlgorithmVersion(1, 0, 0),
                name,
                category,
                description,
                defaults.GetType(),
                new AlgorithmParameterSchema(defaults.SchemaVersion, fields, defaultsJson),
                formats,
                capabilities,
                OutputSuffix: suffix,
                OutputFormats: ResolveOutputFormats(id, formats),
                OutputFormatPolicy: ResolveOutputFormatPolicy(id));
        }

        private static (double? Minimum, double? Maximum, IReadOnlyList<string>? Allowed, string? Unit) ResolveFieldContract(
            AlgorithmId id,
            PropertyInfo property)
        {
            IReadOnlyList<string>? allowed = property.PropertyType.IsEnum ? Enum.GetNames(property.PropertyType) : null;
            double? minimum = null;
            double? maximum = null;
            string? unit = null;
            string name = property.Name;

            if (id == StandardAlgorithmIds.Canny)
            {
                if (name is nameof(CannyParameters.LowThreshold) or nameof(CannyParameters.HighThreshold)) (minimum, maximum) = (0, 255);
                else if (name == nameof(CannyParameters.ApertureSize)) allowed = new[] { "3", "5", "7" };
            }
            else if (id == StandardAlgorithmIds.BasicAdjustment)
            {
                if (name == nameof(BasicAdjustmentParameters.Exposure)) (minimum, maximum, unit) = (-5, 5, "EV");
                else if (name is nameof(BasicAdjustmentParameters.Brightness) or nameof(BasicAdjustmentParameters.Contrast)) (minimum, maximum, unit) = (-100, 100, "%");
                else if (name == nameof(BasicAdjustmentParameters.Gamma)) (minimum, maximum) = (0.1, 5);
            }
            else if (id == StandardAlgorithmIds.Threshold && name == nameof(ThresholdParameters.Threshold)) (minimum, maximum) = (0, ushort.MaxValue);
            else if (id == StandardAlgorithmIds.GaussianBlur)
            {
                if (name == nameof(GaussianBlurParameters.KernelSize)) (minimum, maximum) = (1, 255);
                else if (name == nameof(GaussianBlurParameters.Sigma)) (minimum, maximum) = (0, 1000);
            }
            else if (id == StandardAlgorithmIds.MedianBlur && name == nameof(MedianBlurParameters.KernelSize)) (minimum, maximum) = (3, 255);
            else if (id == StandardAlgorithmIds.Morphology)
            {
                if (name == nameof(MorphologyParameters.KernelSize)) (minimum, maximum) = (1, 255);
                else if (name == nameof(MorphologyParameters.Iterations)) (minimum, maximum) = (1, 100);
            }
            else if (id == StandardAlgorithmIds.Denoise)
            {
                if (name == nameof(DenoiseParameters.KernelSize)) (minimum, maximum) = (1, 255);
                else if (name is nameof(DenoiseParameters.SigmaColor) or nameof(DenoiseParameters.SigmaSpace)) (minimum, maximum) = (0, 10000);
            }
            else if (id == StandardAlgorithmIds.WhiteBalance
                     && name is nameof(WhiteBalanceParameters.RedScale) or nameof(WhiteBalanceParameters.GreenScale) or nameof(WhiteBalanceParameters.BlueScale))
            {
                (minimum, maximum) = (0, 16);
            }
            else if (id == StandardAlgorithmIds.PseudoColor)
            {
                if (name is nameof(PseudoColorParameters.Minimum) or nameof(PseudoColorParameters.Maximum)
                    or nameof(PseudoColorParameters.DataMinimum) or nameof(PseudoColorParameters.DataMaximum))
                {
                    (minimum, maximum) = (0, uint.MaxValue);
                }
                else if (name == nameof(PseudoColorParameters.Channel)) (minimum, maximum) = (-1, 3);
            }
            else if (id == StandardAlgorithmIds.RoiStatistics)
            {
                if (name == nameof(RoiStatisticsParameters.HistogramBins)) (minimum, maximum) = (2, 4096);
                else if (name == nameof(RoiStatisticsParameters.BadPixelNeighborhoodRadius)) (minimum, maximum) = (1, 5);
                else if (name == nameof(RoiStatisticsParameters.BadPixelSigmaThreshold)) (minimum, maximum) = (0.1, 100);
                else if (name == nameof(RoiStatisticsParameters.BadPixelMinimumDeviationFraction)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(RoiStatisticsParameters.MaximumBadPixelCandidates)) (minimum, maximum) = (0, 100_000);
                else if (name == nameof(RoiStatisticsParameters.Percentiles)) unit = "%";
            }
            else if (id == StandardAlgorithmIds.ImageProfile)
            {
                if (name == nameof(ImageProfileParameters.SampleSpacingPixels)) (minimum, maximum, unit) = (0.01, 1_000_000, "px");
                else if (name == nameof(ImageProfileParameters.MaximumSamples)) (minimum, maximum) = (2, 1_000_000);
            }
            else if (id == StandardAlgorithmIds.ImageComparison)
            {
                if (name == nameof(ImageComparisonParameters.FloatPeakValue)) (minimum, maximum, unit) = (double.Epsilon, double.MaxValue, "DN");
                else if (name == nameof(ImageComparisonParameters.HeatmapMaximum)) (minimum, maximum, unit) = (0, double.MaxValue, "DN");
                else if (name == nameof(ImageComparisonParameters.SsimWindowSize)) (minimum, maximum, unit) = (3, 255, "px");
                else if (name is nameof(ImageComparisonParameters.SsimK1) or nameof(ImageComparisonParameters.SsimK2)) (minimum, maximum) = (0.000001, 1);
                else if (name == nameof(ImageComparisonParameters.SsimMinimumValidFraction)) (minimum, maximum, unit) = (0.01, 1, "ratio");
                else if (name == nameof(ImageComparisonParameters.AlignmentSearchRadius)) (minimum, maximum, unit) = (0, 32, "px");
                else if (name == nameof(ImageComparisonParameters.AlignmentWarningThresholdPixels)) (minimum, maximum, unit) = (0, 64, "px");
                else if (name == nameof(ImageComparisonParameters.AlignmentMinimumOverlapFraction)) (minimum, maximum, unit) = (0.1, 1, "ratio");
                else if (name == nameof(ImageComparisonParameters.AlignmentMaximumSamples)) (minimum, maximum) = (256, 100_000);
            }

            return (minimum, maximum, allowed, unit);
        }

        private static IReadOnlySet<AlgorithmImageFormat> ResolveOutputFormats(AlgorithmId id, IReadOnlySet<AlgorithmImageFormat> inputFormats)
        {
            if (id == StandardAlgorithmIds.Canny) return new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 };
            if (id == StandardAlgorithmIds.HistogramEqualization) return new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Bgr24 };
            if (id == StandardAlgorithmIds.PseudoColor) return new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Bgr24 };
            return inputFormats;
        }

        private static string ResolveOutputFormatPolicy(AlgorithmId id)
        {
            if (id == StandardAlgorithmIds.Canny) return "always-gray8";
            if (id == StandardAlgorithmIds.HistogramEqualization) return "gray8-for-gray; bgr24-for-color";
            if (id == StandardAlgorithmIds.PseudoColor) return "always-bgr24";
            return "same-as-input";
        }
    }
}
