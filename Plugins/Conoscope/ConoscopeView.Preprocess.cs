using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Processing.Preprocess;
using System;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializePreprocessControls()
        {
            MigrateLegacyDustRemovalFilterType();
        }

        private void btnOpenPreprocessSettings_Click(object sender, RoutedEventArgs e)
        {
            OpenPreprocessSettings();
        }

        private void OpenPreprocessSettings()
        {
            ConoscopePreprocessSettingsWindow dialog = new ConoscopePreprocessSettingsWindow(ConoscopeConfig)
            {
                Owner = Window.GetWindow(this),
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            dialog.ShowDialog();
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

        private void btnApplyFilter_Click(object sender, RoutedEventArgs e)
        {
            ApplyPreprocessFromCurrentSettings();
        }

        internal void ApplyPreprocessFromCurrentSettings()
        {
            try
            {
                if (!HasXyzData())
                {
                    MessageBox.Show("请先获取图像", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (!HasPreprocessEnabled())
                {
                    RestoreOriginalMats();
                    RefreshDisplayedImage();
                    log.Info("已恢复原始数据");
                    MessageBox.Show("已恢复原始数据", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
                    return;
                }

                RestoreOriginalMats();
                log.Info($"开始应用预处理: clamp={PreprocessConfig.ClampNonPositiveXyzOnLoad}, dust={PreprocessConfig.DustRemovalEnabled}, filter={PreprocessConfig.FilterType}");
                ApplyPreprocessToCurrentMats();
                RefreshDisplayedImage();

                log.Info("预处理应用成功，数据已更新");
                MessageBox.Show("预处理应用成功", "提示", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                log.Error($"应用滤波失败: {ex.Message}", ex);
                MessageBox.Show($"应用滤波失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
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
            return ConoscopePreprocessOptions.FromConfig(PreprocessConfig, MinPositiveXyzValue);
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
