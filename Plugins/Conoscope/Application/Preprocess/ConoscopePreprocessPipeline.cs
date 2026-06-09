#pragma warning disable CS8604
using Conoscope.Core;
using Conoscope.Processing.Preprocess;
using log4net;
using OpenCvSharp;
using System;

namespace Conoscope.ApplicationServices.Preprocess
{
    public sealed record ConoscopePreprocessOptions(
        bool ClampNonPositiveXyz,
        float PositiveFloor,
        bool DustRemovalEnabled,
        DustRemovalOptions DustRemoval,
        ImageFilterOptions Filter)
    {
        public static ConoscopePreprocessOptions FromConfig(ConoscopePreprocessSettings config, float positiveFloor)
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
                new DustRemovalOptions(
                    config.DustRemovalMode,
                    config.DustThresholdPercent,
                    minArea,
                    maxArea,
                    Math.Max(1, config.DustRepairRadius)),
                new ImageFilterOptions(
                    filterType,
                    ConoscopeNumericHelper.NormalizeOddKernelSize(config.FilterKernelSize),
                    config.FilterSigma,
                    Math.Max(1, config.FilterD),
                    config.FilterSigmaColor,
                    config.FilterSigmaSpace));
        }
    }

    internal static class ConoscopePreprocessPipeline
    {
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
                DustRemovalSummary summary = DustRemovalProcessor.Apply(ref xSource, ref ySource, ref zSource, options.DustRemoval);
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

            if (options.Filter.FilterType != ImageFilterType.None)
            {
                xSource = ReplaceWithFilteredMat(xSource, options.Filter);
                ySource = ReplaceWithFilteredMat(ySource, options.Filter);
                zSource = ReplaceWithFilteredMat(zSource, options.Filter);
                log?.Info($"滤波应用到XYZ通道完成: {options.Filter.FilterType}, kernelSize={options.Filter.KernelSize}");
            }

            xMat = xSource;
            yMat = ySource;
            zMat = zSource;
        }

        public static void ApplyToSingleChannel(ref Mat? channelMat, ConoscopePreprocessOptions options, ILog? log = null)
        {
            if (channelMat == null)
            {
                return;
            }

            Mat source = channelMat;
            if (options.DustRemovalEnabled)
            {
                DustRemovalSummary summary = DustRemovalProcessor.ApplyToSingleChannel(ref source, source, options.DustRemoval);
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

            if (options.Filter.FilterType != ImageFilterType.None)
            {
                source = ReplaceWithFilteredMat(source, options.Filter);
                log?.Info($"滤波应用到 Y 通道完成: {options.Filter.FilterType}, kernelSize={options.Filter.KernelSize}");
            }

            channelMat = source;
        }

        private static Mat ReplaceWithFilteredMat(Mat source, ImageFilterOptions options)
        {
            Mat filtered = ImageFilterProcessor.Apply(source, options);
            source.Dispose();
            return filtered;
        }
    }
}