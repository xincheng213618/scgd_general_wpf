using ColorVision.Algorithms;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

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

        private static readonly AlgorithmInteractiveGroupPresentation FilterGroup = new(
            "AlgorithmFilters",
            8,
            "滤波",
            "Algorithm_FilterCategory");

        public static AlgorithmCatalog Create()
        {
            AlgorithmCatalog catalog = new();
            catalog.Register(Descriptor(StandardAlgorithmIds.Invert, "反相", "像素处理", "逐位反转像素；保持尺寸、位深和通道。", new NoAlgorithmParameters(), CommonFormats, "_invert",
                presentation: Presentation(1, Entry("InvertImage", 1, resourceKey: "Invert"))), "Invert", "InvertImage");
            catalog.Register(Descriptor(StandardAlgorithmIds.Canny, "Canny 边缘检测", "像素处理", "转换为 Gray8 后执行 Canny，输出 Gray8。", new CannyParameters(), CommonFormats, "_canny",
                presentation: Presentation(10, Entry("EdgeDetection", 10, resourceKey: "Canny"))), "Canny", "EdgeDetection");
            catalog.Register(Descriptor(StandardAlgorithmIds.BasicAdjustment, "基础调整", "像素处理", "曝光、亮度、对比度和 Gamma 调整；四通道 alpha 保持不变。", new BasicAdjustmentParameters(), CommonFormats, "_adjusted",
                presentation: Presentation(5, Entry("BasicAdjustment", 4, resourceKey: "LuminanceContrastAdjustment"))), "BasicAdjustment");
            catalog.Register(Descriptor(StandardAlgorithmIds.Threshold, "阈值处理", "像素处理", "逐通道二值阈值；当前参数按 0..255 标称刻度映射到输入位深。", new ThresholdParameters(), CommonFormats, "_threshold",
                presentation: Presentation(6, Entry("Threshold", 5, resourceKey: "ThresholdProcessing"))) with { Version = new AlgorithmVersion(1, 1, 0) }, "Threshold");
            catalog.Register(Descriptor(StandardAlgorithmIds.Sharpen, "锐化", "像素处理", "固定 3x3 锐化核；保持输入格式。", new NoAlgorithmParameters(), CommonFormats, "_sharpen",
                presentation: Presentation(7, Entry("Sharpen", 7, resourceKey: "Sharpening"))), "Sharpen");
            catalog.Register(Descriptor(StandardAlgorithmIds.GaussianBlur, "高斯模糊", "像素处理", "奇数核高斯模糊；保持输入格式。", new GaussianBlurParameters(), CommonFormats, "_gaussian",
                presentation: Presentation(8, Entry("GaussianBlur", 8, resourceKey: "GaussianBlur", group: FilterGroup))), "GaussianBlur");
            catalog.Register(Descriptor(StandardAlgorithmIds.MedianBlur, "中值滤波", "像素处理", "奇数核中值滤波；保持输入格式。", new MedianBlurParameters(), CommonFormats, "_median",
                presentation: Presentation(9, Entry("MedianBlur", 9, resourceKey: "MedianFilter", group: FilterGroup))), "MedianBlur");
            catalog.Register(Descriptor(StandardAlgorithmIds.Morphology, "形态学操作", "像素处理", "腐蚀、膨胀、开闭运算、梯度、顶帽或黑帽。", new MorphologyParameters(), CommonFormats, "_morphology",
                presentation: Presentation(12,
                    Entry("Erode", 12, "腐蚀"),
                    Entry("Dilate", 13, "膨胀"),
                    Entry("MorphologyEx", 14, "形态学操作"))), "Morphology", "Erode", "Dilate", "MorphologyEx");
            catalog.Register(Descriptor(StandardAlgorithmIds.Denoise, "降噪滤波", "像素处理", "双边滤波或均值滤波；颜色 Sigma 按 0..255 标称刻度映射，四通道 alpha 保持不变。", new DenoiseParameters(), CommonFormats, "_denoise",
                presentation: Presentation(13,
                    Entry("BilateralFilter", 16, "双边滤波", group: FilterGroup),
                    Entry("Blur", 17, "均值模糊", group: FilterGroup))) with { Version = new AlgorithmVersion(1, 1, 0) }, "Denoise", "BilateralFilter", "Blur");
            catalog.Register(Descriptor(StandardAlgorithmIds.AutoLevels, "自动色阶", "像素处理", "按输入全局最小值和最大值拉伸到位深标称范围。", new NoAlgorithmParameters(), CommonFormats, "_autolevels",
                presentation: Presentation(3, Entry("AutoLevelsAdjust", 2, resourceKey: "AutoLevelsAdjustment"))), "AutoLevels", "AutoLevelsAdjust");
            catalog.Register(Descriptor(StandardAlgorithmIds.WhiteBalance, "白平衡", "像素处理", "缩放 B/G/R 通道；四通道 alpha 保持不变。", new WhiteBalanceParameters(), ColorFormats, "_whitebalance",
                presentation: Presentation(4, Entry("WhiteBalance", 3, resourceKey: "WhiteBalanceAdjustment"))), "WhiteBalance");
            catalog.Register(Descriptor(StandardAlgorithmIds.HistogramEqualization, "直方图均衡化", "像素处理", "灰度直接均衡化，彩色在亮度通道均衡化；输出 Gray8 或 Bgr24。", new NoAlgorithmParameters(), CommonFormats, "_equalized",
                presentation: Presentation(11, Entry("HistogramEqualization", 11, resourceKey: "HistogramEqualization"))), "HistogramEqualization");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.RemoveMoire,
                "去除摩尔纹",
                "像素处理",
                "调用兼容 Native 去摩尔纹实现。",
                new NoAlgorithmParameters(),
                CommonFormats,
                "_demoire",
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Headless | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic,
                Presentation(null, Entry("RemoveMoire", 6, resourceKey: "MoireRemove"))), "RemoveMoire");
            catalog.Register(Descriptor(StandardAlgorithmIds.PseudoColor, "伪彩色", "像素处理", "将输入灰度归一化为 Gray8 后应用色图，输出 Bgr24。", new PseudoColorParameters(), CommonFormats, "_pseudo",
                presentation: Presentation(2)), "PseudoColor");
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
                ResultSemantics = AlgorithmResultSemantics.Analysis,
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
                ResultSemantics = AlgorithmResultSemantics.Analysis,
                Version = new AlgorithmVersion(1, 1, 0),
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
                    | AlgorithmHostCapabilities.Deterministic | AlgorithmHostCapabilities.MultiInput
                    | AlgorithmHostCapabilities.Roi) with
            {
                MinimumInputCount = 2,
                MaximumInputCount = 2,
                OutputFormats = CommonFormats,
                OutputFormatPolicy = "absolute=same-as-input; signed=float-same-channels; visualizations=bgr24",
                Version = new AlgorithmVersion(1, 1, 0),
                SupportsRectangleRoi = true,
                SupportsCircleRoi = true,
                SupportsPolygonRoi = true,
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "ImageComparison", "CompareImage", "ImageDiff");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.BlobComponents,
                "Blob / 连通域",
                "工业测量",
                "按标称阈值提取明/暗前景连通域，返回筛选原因、边界框、质心、面积和填充率。",
                new BlobAnalysisParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsRectangleRoi = true,
                SupportsCircleRoi = true,
                SupportsPolygonRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "BlobAnalysis", "ConnectedComponents", "Blob", "BlobAnalysisWholeImage", "BlobAnalysisRectangle", "BlobAnalysisCircle", "BlobAnalysisPolygon");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.Contours,
                "轮廓提取",
                "工业测量",
                "按标称阈值提取明/暗前景轮廓，返回层级、边界点、面积、周长、质心、圆度、实心度和筛选原因。",
                new ContourAnalysisParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsRectangleRoi = true,
                SupportsCircleRoi = true,
                SupportsPolygonRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "ContourAnalysis", "FindContours", "Contours", "ContourAnalysisWholeImage", "ContourAnalysisRectangle", "ContourAnalysisCircle", "ContourAnalysisPolygon");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.SubpixelEdge,
                "亚像素边缘",
                "工业测量",
                "把折线相邻点对作为有向卡尺，以带宽平均、梯度响应和抛物线插值定位亚像素边缘点，并返回置信度与拒绝原因。",
                new SubpixelEdgeParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsPolylineRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "SubpixelEdge", "CaliperEdge", "SubpixelEdgeHorizontal", "SubpixelEdgeVertical", "SubpixelEdgePolyline");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.LineFit,
                "直线拟合",
                "工业测量",
                "对折线 ROI 顶点表示的点集执行正交总最小二乘或稳健 Huber 拟合，返回每点投影、残差、有效性和拟合质量。",
                new LineFitParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsPolylineRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "LineFit", "FitLine", "LineMeasurement", "LineFitPoints");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.CircleFit,
                "圆拟合",
                "工业测量",
                "对折线 ROI 顶点表示的点集执行归一化代数初值与几何/稳健拟合，返回圆心、半径、径向残差、角覆盖和有效性。",
                new CircleFitParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities | AlgorithmHostCapabilities.Roi) with
            {
                SupportsPolylineRoi = true,
                OutputFormats = new HashSet<AlgorithmImageFormat>(),
                OutputFormatPolicy = "no-image-output",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "CircleFit", "FitCircle", "CircleMeasurement", "CircleFitPoints");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.GeometricTransform,
                "几何变换",
                "几何校正",
                "按显式 source-to-destination 矩阵执行仿射或透视变换，输出原格式图像、有效区域 mask、正逆矩阵和数值诊断。",
                new GeometricTransformParameters(),
                CommonFormats,
                "_transform",
                presentation: Presentation(30, Entry("GeometricTransform", 18, "几何变换..."))) with
            {
                OutputFormats = CommonFormats,
                OutputFormatPolicy = "primary=same-as-input; validity-mask=gray8",
            }, "GeometricTransform", "AffineTransform", "PerspectiveTransform", "HomographyTransform", "WarpAffine", "WarpPerspective");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.ImageRegistration,
                "图像配准",
                "几何校正",
                "将 moving 图像通过相位相关平移或 ORB 特征单应配准到 reference 像素坐标，返回对齐图像、有效区、矩阵、残差与置信度。",
                new ImageRegistrationParameters(),
                CommonFormats,
                "_registered",
                AlgorithmHostCapabilities.Interactive | AlgorithmHostCapabilities.Flow | AlgorithmHostCapabilities.Headless
                    | AlgorithmHostCapabilities.Local | AlgorithmHostCapabilities.Deterministic | AlgorithmHostCapabilities.MultiInput,
                Presentation(null, Entry("ImageRegistration", 19, "图像配准..."))) with
            {
                MinimumInputCount = 2,
                MaximumInputCount = 2,
                OutputFormats = CommonFormats,
                OutputFormatPolicy = "primary=same-as-moving; canvas=reference; validity-mask=gray8",
            }, "ImageRegistration", "RegisterImage", "PhaseCorrelationRegistration", "OrbHomographyRegistration");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.LensDistortionCorrection,
                "镜头畸变校正",
                "几何校正",
                "按显式 Brown-Conrady 相机内参与畸变系数校正图像，输出原格式图像、有效区域 mask、相机矩阵和标定追溯信息。",
                new LensDistortionCorrectionParameters(),
                CommonFormats,
                "_undistorted",
                presentation: Presentation(31, Entry("LensDistortionCorrection", 20, "镜头畸变校正..."))) with
            {
                OutputFormats = CommonFormats,
                OutputFormatPolicy = "primary=same-as-input; canvas=same-as-input; validity-mask=gray8",
            }, "LensDistortionCorrection", "Undistort", "CameraUndistort", "BrownConradyCorrection");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.ImagingCorrection,
                "成像校正",
                "成像校正",
                "按固定顺序执行 dark-frame、flat-field、残余 shading/non-uniformity 和 bad-pixel map 校正，并返回有效性 mask 与完整参考追溯。",
                new ImagingCorrectionParameters(),
                CommonFormats,
                "_corrected",
                CommonCapabilities | AlgorithmHostCapabilities.MultiInput,
                Presentation(32, Entry("ImagingCorrection", 21, "成像校正..."))) with
            {
                MinimumInputCount = 1,
                MaximumInputCount = 5,
                OutputFormats = CommonFormats,
                OutputFormatPolicy = "primary=same-as-source; validity-mask=gray8",
            }, "ImagingCorrection", "DarkFrameCorrection", "FlatFieldCorrection", "ShadingCorrection", "BadPixelCorrection");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.FrequencySpectrum,
                "FFT / 频域分析",
                "频域分析",
                "对标称亮度执行带窗二维 DFT，输出中心化幅度/功率频谱显示、径向/方向频谱、峰值周期/方向和逆变换误差。",
                new FrequencySpectrumParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities,
                Presentation(null, Entry("FrequencySpectrum", 22, "FFT / 频域分析..."))) with
            {
                OutputFormats = new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8 },
                OutputFormatPolicy = "magnitude-and-power=gray8-display; quantitative-values=measurement/table/structured-data",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "FrequencySpectrum", "FFTAnalysis", "DFTAnalysis", "FourierSpectrum");
            catalog.Register(Descriptor(
                StandardAlgorithmIds.MoireAnalysis,
                "摩尔纹分析",
                "频域分析",
                "基于同半径频谱背景解释窄带周期峰值，输出评分、共轭 notch 建议、频域热力图和可选滤波亮度图。",
                new MoireAnalysisParameters(),
                CommonFormats,
                string.Empty,
                CommonCapabilities,
                Presentation(null, Entry("MoireAnalysis", 23, "摩尔纹分析..."))) with
            {
                OutputFormats = new HashSet<AlgorithmImageFormat> { AlgorithmImageFormat.Gray8, AlgorithmImageFormat.Gray32Float },
                OutputFormatPolicy = "spectrum-and-heatmap=gray8-display; optional-filtered-luminance=gray32float",
                ResultSemantics = AlgorithmResultSemantics.Analysis,
            }, "MoireAnalysis", "MoireSpectrumAnalysis", "MoireNotchAnalysis");
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
            AlgorithmHostCapabilities capabilities = CommonCapabilities,
            AlgorithmPresentationMetadata? presentation = null)
        {
            if (IsExplicitlyAllowedForCopilot(id)) capabilities |= AlgorithmHostCapabilities.Copilot;
            JsonElement defaultsJson = AlgorithmJson.ToElement(defaults);
            List<AlgorithmParameterField> fields = new();
            foreach (PropertyInfo property in defaults.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (property.Name == nameof(IAlgorithmParameters.SchemaVersion)
                    || property.SetMethod == null
                    || property.GetCustomAttribute<JsonIgnoreAttribute>() != null)
                {
                    continue;
                }
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
                OutputFormatPolicy: ResolveOutputFormatPolicy(id))
            {
                Presentation = presentation,
            };
        }

        private static AlgorithmPresentationMetadata Presentation(
            int? batchOrder,
            params AlgorithmInteractivePresentation[] interactiveEntries)
            => new(batchOrder, interactiveEntries);

        private static AlgorithmInteractivePresentation Entry(
            string compatibilityId,
            int order,
            string? displayName = null,
            string? resourceKey = null,
            AlgorithmInteractiveGroupPresentation? group = null)
            => new(compatibilityId, order, displayName, resourceKey) { Group = group };

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
            else if (id == StandardAlgorithmIds.Threshold)
            {
                if (name == nameof(ThresholdParameters.Threshold))
                    (minimum, maximum, unit) = (0, ThresholdParameters.MaximumAbsoluteThreshold, "conditional-DN");
            }
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
                else if (name == nameof(DenoiseParameters.SigmaColor)) (minimum, maximum, unit) = (0, byte.MaxValue, "nominal-8bit-DN");
                else if (name == nameof(DenoiseParameters.SigmaSpace)) (minimum, maximum, unit) = (0, 10000, "px");
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
                else if (name == nameof(ImageProfileParameters.MaximumSamples)) (minimum, maximum) = (2, ImageProfileParameters.AbsoluteMaximumSamples);
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
            else if (id == StandardAlgorithmIds.BlobComponents)
            {
                if (name == nameof(BlobAnalysisParameters.Threshold)) (minimum, maximum, unit) = (0, byte.MaxValue, "nominal-8bit-DN");
                else if (name is nameof(BlobAnalysisParameters.MinimumArea) or nameof(BlobAnalysisParameters.MinimumWidth) or nameof(BlobAnalysisParameters.MinimumHeight)) minimum = 1;
                else if (name is nameof(BlobAnalysisParameters.MaximumArea) or nameof(BlobAnalysisParameters.MaximumWidth) or nameof(BlobAnalysisParameters.MaximumHeight)) minimum = 0;
                else if (name == nameof(BlobAnalysisParameters.MaximumCandidates)) (minimum, maximum) = (1, 100_000);
                else if (name == nameof(BlobAnalysisParameters.MaximumOverlayComponents)) (minimum, maximum) = (0, 5_000);
            }
            else if (id == StandardAlgorithmIds.Contours)
            {
                if (name == nameof(ContourAnalysisParameters.Threshold)) (minimum, maximum, unit) = (0, byte.MaxValue, "nominal-8bit-DN");
                else if (name == nameof(ContourAnalysisParameters.SimplificationEpsilon)) (minimum, maximum, unit) = (0, 1_000_000, "px");
                else if (name is nameof(ContourAnalysisParameters.MinimumArea) or nameof(ContourAnalysisParameters.MaximumArea)) (minimum, unit) = (0, "px²");
                else if (name is nameof(ContourAnalysisParameters.MinimumPerimeter) or nameof(ContourAnalysisParameters.MaximumPerimeter)) (minimum, unit) = (0, "px");
                else if (name == nameof(ContourAnalysisParameters.MinimumPointCount)) minimum = 1;
                else if (name == nameof(ContourAnalysisParameters.MaximumPointCount)) minimum = 0;
                else if (name is nameof(ContourAnalysisParameters.MinimumCircularity) or nameof(ContourAnalysisParameters.MinimumSolidity)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(ContourAnalysisParameters.MaximumCandidates)) (minimum, maximum) = (1, 100_000);
                else if (name == nameof(ContourAnalysisParameters.MaximumTotalPoints)) (minimum, maximum) = (1, 10_000_000);
                else if (name == nameof(ContourAnalysisParameters.MaximumOverlayContours)) (minimum, maximum) = (0, 5_000);
            }
            else if (id == StandardAlgorithmIds.SubpixelEdge)
            {
                if (name == nameof(SubpixelEdgeParameters.SampleSpacingPixels)) (minimum, maximum, unit) = (0.05, 2, "px");
                else if (name == nameof(SubpixelEdgeParameters.NormalAveragingRadiusPixels)) (minimum, maximum, unit) = (0, 32, "px");
                else if (name == nameof(SubpixelEdgeParameters.SmoothingSigmaPixels)) (minimum, maximum, unit) = (0, 10, "px");
                else if (name == nameof(SubpixelEdgeParameters.MinimumGradient)) (minimum, maximum, unit) = (0, 255, "nominal-8bit-DN/px");
                else if (name == nameof(SubpixelEdgeParameters.MaximumCalipers)) (minimum, maximum) = (1, 10_000);
                else if (name == nameof(SubpixelEdgeParameters.MaximumSamplesPerCaliper)) (minimum, maximum) = (6, 1_000_000);
                else if (name == nameof(SubpixelEdgeParameters.MaximumTotalSamples)) (minimum, maximum) = (6, 10_000_000);
                else if (name == nameof(SubpixelEdgeParameters.MaximumOverlayCalipers)) (minimum, maximum) = (0, 5_000);
            }
            else if (id == StandardAlgorithmIds.LineFit)
            {
                if (name == nameof(LineFitParameters.ResidualThresholdPixels)) (minimum, maximum, unit) = (0.000001, 1_000_000, "px");
                else if (name == nameof(LineFitParameters.HuberTuningConstant)) (minimum, maximum) = (0.1, 10);
                else if (name == nameof(LineFitParameters.MaximumIterations)) (minimum, maximum) = (1, 1_000);
                else if (name == nameof(LineFitParameters.ConvergenceTolerance)) (minimum, maximum) = (1e-15, 0.1);
                else if (name == nameof(LineFitParameters.MinimumInlierCount)) (minimum, maximum) = (2, 100_000);
                else if (name == nameof(LineFitParameters.MaximumPoints)) (minimum, maximum) = (2, 1_000_000);
                else if (name == nameof(LineFitParameters.MaximumOverlayPoints)) (minimum, maximum) = (0, 10_000);
            }
            else if (id == StandardAlgorithmIds.CircleFit)
            {
                if (name == nameof(CircleFitParameters.ResidualThresholdPixels)) (minimum, maximum, unit) = (0.000001, 1_000_000, "px");
                else if (name == nameof(CircleFitParameters.HuberTuningConstant)) (minimum, maximum) = (0.1, 10);
                else if (name == nameof(CircleFitParameters.MaximumIterations)) (minimum, maximum) = (1, 1_000);
                else if (name == nameof(CircleFitParameters.ConvergenceTolerance)) (minimum, maximum) = (1e-15, 0.1);
                else if (name == nameof(CircleFitParameters.MinimumInlierCount)) (minimum, maximum) = (3, 100_000);
                else if (name is nameof(CircleFitParameters.MinimumRadiusPixels) or nameof(CircleFitParameters.MaximumRadiusPixels)) (minimum, maximum, unit) = (0, 1_000_000_000, "px");
                else if (name == nameof(CircleFitParameters.MinimumAngularCoverageDegrees)) (minimum, maximum, unit) = (0, 360, "degree");
                else if (name == nameof(CircleFitParameters.MaximumPoints)) (minimum, maximum) = (3, 1_000_000);
                else if (name == nameof(CircleFitParameters.MaximumConsensusCandidates)) (minimum, maximum) = (1, 10_000);
                else if (name == nameof(CircleFitParameters.MaximumConsensusEvaluations)) (minimum, maximum) = (3, 100_000_000);
                else if (name == nameof(CircleFitParameters.MaximumOverlayPoints)) (minimum, maximum) = (0, 10_000);
            }
            else if (id == StandardAlgorithmIds.GeometricTransform)
            {
                if (name is nameof(GeometricTransformParameters.M11) or nameof(GeometricTransformParameters.M12) or nameof(GeometricTransformParameters.M13)
                    or nameof(GeometricTransformParameters.M21) or nameof(GeometricTransformParameters.M22) or nameof(GeometricTransformParameters.M23)
                    or nameof(GeometricTransformParameters.M31) or nameof(GeometricTransformParameters.M32) or nameof(GeometricTransformParameters.M33))
                    (minimum, maximum) = (-1e12, 1e12);
                else if (name is nameof(GeometricTransformParameters.OutputWidth) or nameof(GeometricTransformParameters.OutputHeight))
                    (minimum, maximum, unit) = (0, int.MaxValue, "px");
                else if (name == nameof(GeometricTransformParameters.FitPaddingPixels)) (minimum, maximum, unit) = (0, 10_000, "px");
                else if (name is nameof(GeometricTransformParameters.BorderChannel0) or nameof(GeometricTransformParameters.BorderChannel1)
                    or nameof(GeometricTransformParameters.BorderChannel2) or nameof(GeometricTransformParameters.BorderChannel3))
                    (minimum, maximum, unit) = (0, 1, "normalized");
                else if (name == nameof(GeometricTransformParameters.MaximumOutputPixels)) (minimum, maximum, unit) = (1, 1_000_000_000, "px");
                else if (name == nameof(GeometricTransformParameters.MaximumConditionNumber)) (minimum, maximum) = (1, 1e18);
            }
            else if (id == StandardAlgorithmIds.ImageRegistration)
            {
                if (name == nameof(ImageRegistrationParameters.MinimumPhaseResponse)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(ImageRegistrationParameters.MaximumTranslationPixels)) (minimum, maximum, unit) = (0, 1_000_000, "px");
                else if (name == nameof(ImageRegistrationParameters.MaximumFeatures)) (minimum, maximum) = (100, 100_000);
                else if (name == nameof(ImageRegistrationParameters.PyramidScaleFactor)) (minimum, maximum) = (1.01, 2);
                else if (name == nameof(ImageRegistrationParameters.PyramidLevels)) (minimum, maximum) = (1, 32);
                else if (name == nameof(ImageRegistrationParameters.FastThreshold)) (minimum, maximum) = (1, 255);
                else if (name == nameof(ImageRegistrationParameters.LoweRatio)) (minimum, maximum, unit) = (0.1, 0.99, "ratio");
                else if (name is nameof(ImageRegistrationParameters.MinimumMatchCount) or nameof(ImageRegistrationParameters.MinimumInlierCount)) (minimum, maximum) = (4, 100_000);
                else if (name == nameof(ImageRegistrationParameters.ConsensusReprojectionThresholdPixels)) (minimum, maximum, unit) = (0.01, 1_000, "px");
                else if (name == nameof(ImageRegistrationParameters.MinimumInlierRatio)) (minimum, maximum, unit) = (0.01, 1, "ratio");
                else if (name == nameof(ImageRegistrationParameters.MaximumConsensusMatches)) (minimum, maximum) = (4, 200);
                else if (name == nameof(ImageRegistrationParameters.MaximumConsensusEvaluations)) (minimum, maximum) = (1, 1_000_000);
                else if (name == nameof(ImageRegistrationParameters.MaximumReportedMatches)) (minimum, maximum) = (0, 100_000);
                else if (name is nameof(ImageRegistrationParameters.BorderChannel0) or nameof(ImageRegistrationParameters.BorderChannel1)
                    or nameof(ImageRegistrationParameters.BorderChannel2) or nameof(ImageRegistrationParameters.BorderChannel3))
                    (minimum, maximum, unit) = (0, 1, "normalized");
                else if (name == nameof(ImageRegistrationParameters.MaximumConditionNumber)) (minimum, maximum) = (1, 1e18);
            }
            else if (id == StandardAlgorithmIds.LensDistortionCorrection)
            {
                if (name is nameof(LensDistortionCorrectionParameters.FxPixels) or nameof(LensDistortionCorrectionParameters.FyPixels))
                    (minimum, maximum, unit) = (0.000001, 1_000_000_000, "px");
                else if (name is nameof(LensDistortionCorrectionParameters.PrincipalPointX) or nameof(LensDistortionCorrectionParameters.PrincipalPointY))
                    (minimum, maximum, unit) = (-1_000_000_000, 1_000_000_000, "px");
                else if (name is nameof(LensDistortionCorrectionParameters.K1) or nameof(LensDistortionCorrectionParameters.K2)
                    or nameof(LensDistortionCorrectionParameters.P1) or nameof(LensDistortionCorrectionParameters.P2)
                    or nameof(LensDistortionCorrectionParameters.K3) or nameof(LensDistortionCorrectionParameters.K4)
                    or nameof(LensDistortionCorrectionParameters.K5) or nameof(LensDistortionCorrectionParameters.K6))
                    (minimum, maximum) = (-1_000_000, 1_000_000);
                else if (name == nameof(LensDistortionCorrectionParameters.OptimalAlpha)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name is nameof(LensDistortionCorrectionParameters.BorderChannel0) or nameof(LensDistortionCorrectionParameters.BorderChannel1)
                    or nameof(LensDistortionCorrectionParameters.BorderChannel2) or nameof(LensDistortionCorrectionParameters.BorderChannel3))
                    (minimum, maximum, unit) = (0, 1, "normalized");
                else if (name == nameof(LensDistortionCorrectionParameters.MinimumValidFraction)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(LensDistortionCorrectionParameters.CalibrationRmsErrorPixels)) (minimum, maximum, unit) = (0, 1_000_000, "px");
                else if (name == nameof(LensDistortionCorrectionParameters.CalibrationConfidence)) (minimum, maximum, unit) = (0, 1, "ratio");
            }
            else if (id == StandardAlgorithmIds.ImagingCorrection)
            {
                if (name is nameof(ImagingCorrectionParameters.ReferenceZeroThresholdNormalized)
                    or nameof(ImagingCorrectionParameters.ReferenceSaturationThresholdNormalized)
                    or nameof(ImagingCorrectionParameters.MinimumValidReferenceFraction)
                    or nameof(ImagingCorrectionParameters.InvalidReferenceFillNormalized)
                    or nameof(ImagingCorrectionParameters.BadPixelThresholdNormalized))
                    (minimum, maximum, unit) = (0, 1, "normalized");
                else if (name is nameof(ImagingCorrectionParameters.MinimumGain) or nameof(ImagingCorrectionParameters.MaximumGain))
                    (minimum, maximum, unit) = (0, 1_000_000, "gain");
                else if (name == nameof(ImagingCorrectionParameters.BadPixelRadius))
                    (minimum, maximum, unit) = (1, 7, "px");
            }
            else if (id == StandardAlgorithmIds.FrequencySpectrum)
            {
                if (name == nameof(FrequencySpectrumParameters.RadialBinWidthCyclesPerPixel))
                    (minimum, maximum, unit) = (0.000001, 1, "cycles/pixel");
                else if (name == nameof(FrequencySpectrumParameters.DirectionBinWidthDegrees))
                    (minimum, maximum, unit) = (0.1, 180, "degree");
                else if (name is nameof(FrequencySpectrumParameters.MinimumPeakFrequencyCyclesPerPixel)
                    or nameof(FrequencySpectrumParameters.MaximumPeakFrequencyCyclesPerPixel))
                    (minimum, maximum, unit) = (0, Math.Sqrt(0.5), "cycles/pixel");
                else if (name == nameof(FrequencySpectrumParameters.PeakRelativePowerThreshold))
                    (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(FrequencySpectrumParameters.PeakNeighborhoodRadius))
                    (minimum, maximum, unit) = (1, 32, "frequency-bin");
                else if (name == nameof(FrequencySpectrumParameters.MaximumPeaks))
                    (minimum, maximum) = (1, 10_000);
                else if (name == nameof(FrequencySpectrumParameters.MaximumPixels))
                    (minimum, maximum, unit) = (1, 1_000_000_000, "px");
            }
            else if (id == StandardAlgorithmIds.MoireAnalysis)
            {
                if (name is nameof(MoireAnalysisParameters.MinimumFrequencyCyclesPerPixel) or nameof(MoireAnalysisParameters.MaximumFrequencyCyclesPerPixel))
                    (minimum, maximum, unit) = (0.000001, Math.Sqrt(0.5), "cycles/pixel");
                else if (name == nameof(MoireAnalysisParameters.RelativePowerThreshold)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(MoireAnalysisParameters.MinimumProminenceRatio)) (minimum, maximum, unit) = (1, 1_000_000, "ratio");
                else if (name == nameof(MoireAnalysisParameters.PeakNeighborhoodRadius)) (minimum, maximum, unit) = (1, 32, "frequency-bin");
                else if (name == nameof(MoireAnalysisParameters.MaximumSuggestions)) (minimum, maximum) = (1, 1_000);
                else if (name == nameof(MoireAnalysisParameters.NotchSigmaCyclesPerPixel)) (minimum, maximum, unit) = (0.000001, 0.25, "cycles/pixel");
                else if (name == nameof(MoireAnalysisParameters.NotchAttenuation)) (minimum, maximum, unit) = (0, 1, "ratio");
                else if (name == nameof(MoireAnalysisParameters.MaximumPixels)) (minimum, maximum, unit) = (1, 1_000_000_000, "px");
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
