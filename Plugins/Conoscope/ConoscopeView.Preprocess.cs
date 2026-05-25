using ColorVision.Core;
using ColorVision.ImageEditor;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Processing.Preprocess;
using Conoscope.Presentation.Formatters;
using Conoscope.Presentation.Helpers;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private ImageFilterType lastEnabledFilterType = ImageFilterType.LowPass;

        private void InitializePreprocessControls()
        {
            MigrateLegacyDustRemovalFilterType();

            ImageFilterType filterType = NormalizeFilterType(PreprocessConfig.FilterType);
            if (filterType != ImageFilterType.None)
            {
                lastEnabledFilterType = filterType;
            }
        }

        private void MigrateLegacyDustRemovalFilterType()
        {
            const int legacyDustRemovalFilterValue = 6;
            if ((int)PreprocessConfig.FilterType == legacyDustRemovalFilterValue)
            {
                PreprocessConfig.DustRemovalEnabled = true;
                PreprocessConfig.FilterType = ImageFilterType.None;
            }
        }

        internal void ApplyPreprocessFromCurrentSettings()
        {
            try
            {
                if (!HasXyzData())
                {
                    MessageBox.Show(Properties.Resources.MsgLoadImageFirst, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!HasPreprocessEnabled())
                {
                    RestoreOriginalMats();
                    RefreshDisplayedImage();
                    log.Info("已恢复原始数据");
                    MessageBox.Show(Properties.Resources.MsgOriginalDataRestored, Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                RestoreOriginalMats();
                log.Info($"开始应用预处理: clamp={PreprocessConfig.ClampNonPositiveXyzOnLoad}, dust={PreprocessConfig.DustRemovalEnabled}, filter={PreprocessConfig.FilterType}");
                ApplyPreprocessToCurrentMats();
                RefreshDisplayedImage();

                log.Info("预处理应用成功，数据已更新");
                MessageBox.Show(Properties.Resources.MsgPreprocessApplied, Properties.Resources.TitleSuccess, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"应用滤波失败: {ex.Message}", ex);
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgPreprocessFailedDetail, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ClampNonPositiveXyzValuesIfEnabled()
        {
            if (XMat == null || YMat == null || ZMat == null)
            {
                return;
            }

            ConoscopePreprocessOptions options = CreatePreprocessOptions();
            if (!options.ClampNonPositiveXyz)
            {
                return;
            }

            int clampedX = XyzClampProcessor.ClampNonPositive(XMat, options.PositiveFloor);
            int clampedY = XyzClampProcessor.ClampNonPositive(YMat, options.PositiveFloor);
            int clampedZ = XyzClampProcessor.ClampNonPositive(ZMat, options.PositiveFloor);
            if (clampedX + clampedY + clampedZ > 0)
            {
                log.Warn($"加载时已将 XYZ<=0 修正为 {options.PositiveFloor}: X={clampedX}, Y={clampedY}, Z={clampedZ}");
            }
        }

        private void ApplyPreprocessToCurrentMats()
        {
            OpenCvSharp.Mat? xMat = XMat;
            OpenCvSharp.Mat? yMat = YMat;
            OpenCvSharp.Mat? zMat = ZMat;
            ConoscopePreprocessPipeline.Apply(ref xMat, ref yMat, ref zMat, CreatePreprocessOptions(), log);
            XMat = xMat;
            YMat = yMat;
            ZMat = zMat;
        }

        private ConoscopePreprocessOptions CreatePreprocessOptions()
        {
            int minArea = Math.Max(1, PreprocessConfig.DustMinArea);
            int maxArea = Math.Max(minArea, PreprocessConfig.DustMaxArea);
            ImageFilterType filterType = NormalizeFilterType(PreprocessConfig.FilterType);

            return new ConoscopePreprocessOptions(
                PreprocessConfig.ClampNonPositiveXyzOnLoad,
                MinPositiveXyzValue,
                PreprocessConfig.DustRemovalEnabled,
                new DustRemovalOptions(
                    PreprocessConfig.DustRemovalMode,
                    PreprocessConfig.DustThresholdPercent,
                    minArea,
                    maxArea,
                    Math.Max(1, PreprocessConfig.DustRepairRadius)),
                new ImageFilterOptions(
                    filterType,
                    ConoscopeNumericHelper.NormalizeOddKernelSize(PreprocessConfig.FilterKernelSize),
                    PreprocessConfig.FilterSigma,
                    Math.Max(1, PreprocessConfig.FilterD),
                    PreprocessConfig.FilterSigmaColor,
                    PreprocessConfig.FilterSigmaSpace));
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }

        private bool HasPreprocessEnabled()
        {
            return PreprocessConfig.ClampNonPositiveXyzOnLoad
                || PreprocessConfig.DustRemovalEnabled
                || NormalizeFilterType(PreprocessConfig.FilterType) != ImageFilterType.None;
        }
    }
}
