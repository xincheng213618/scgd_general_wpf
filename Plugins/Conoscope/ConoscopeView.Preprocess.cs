using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using Conoscope.Presentation.Helpers;
using Conoscope.Processing.Preprocess;
using System;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private void InitializeFilterControls()
        {
            isUpdatingFilterControls = true;
            try
            {
                MigrateLegacyDustRemovalFilterType();
                ComboBoxHelper.SelectItemByTag(cbFilterType, NormalizeFilterType(PreprocessConfig.FilterType).ToString());
                ComboBoxHelper.SelectItemByTag(cbDustMode, PreprocessConfig.DustRemovalMode.ToString());
                chkClampNonPositiveXyzOnLoad.IsChecked = PreprocessConfig.ClampNonPositiveXyzOnLoad;
                chkDustRemovalEnabled.IsChecked = PreprocessConfig.DustRemovalEnabled;

                sliderKernelSize.Value = PreprocessConfig.FilterKernelSize;
                sliderSigma.Value = PreprocessConfig.FilterSigma;
                sliderD.Value = PreprocessConfig.FilterD;
                sliderSigmaColor.Value = PreprocessConfig.FilterSigmaColor;
                sliderSigmaSpace.Value = PreprocessConfig.FilterSigmaSpace;
                sliderDustThreshold.Value = PreprocessConfig.DustThresholdPercent;
                sliderDustMinArea.Value = PreprocessConfig.DustMinArea;
                sliderDustMaxArea.Value = Math.Max(PreprocessConfig.DustMinArea, PreprocessConfig.DustMaxArea);
                sliderDustRepairRadius.Value = PreprocessConfig.DustRepairRadius;
            }
            finally
            {
                isUpdatingFilterControls = false;
            }

            UpdateFilterParameterVisibility(GetSelectedFilterType());
            UpdatePreprocessSummary();
        }

        private void FilterParameter_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingFilterControls || !IsInitialized)
            {
                return;
            }

            SaveFilterControlsToConfig();
            UpdateFilterParameterVisibility(GetSelectedFilterType());
            UpdatePreprocessSummary();
        }

        private void SaveFilterControlsToConfig()
        {
            PreprocessConfig.ClampNonPositiveXyzOnLoad = chkClampNonPositiveXyzOnLoad?.IsChecked == true;
            PreprocessConfig.FilterType = NormalizeFilterType(GetSelectedFilterType());
            PreprocessConfig.FilterKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize((int)(sliderKernelSize?.Value ?? PreprocessConfig.FilterKernelSize));
            PreprocessConfig.FilterSigma = sliderSigma?.Value ?? PreprocessConfig.FilterSigma;
            PreprocessConfig.FilterD = Math.Max(1, (int)(sliderD?.Value ?? PreprocessConfig.FilterD));
            PreprocessConfig.FilterSigmaColor = sliderSigmaColor?.Value ?? PreprocessConfig.FilterSigmaColor;
            PreprocessConfig.FilterSigmaSpace = sliderSigmaSpace?.Value ?? PreprocessConfig.FilterSigmaSpace;
            PreprocessConfig.DustRemovalEnabled = IsDustRemovalEnabled();
            PreprocessConfig.DustRemovalMode = GetSelectedDustRemovalMode();
            PreprocessConfig.DustThresholdPercent = sliderDustThreshold?.Value ?? PreprocessConfig.DustThresholdPercent;
            PreprocessConfig.DustMinArea = Math.Max(1, (int)(sliderDustMinArea?.Value ?? PreprocessConfig.DustMinArea));
            PreprocessConfig.DustMaxArea = Math.Max(PreprocessConfig.DustMinArea, (int)(sliderDustMaxArea?.Value ?? PreprocessConfig.DustMaxArea));
            PreprocessConfig.DustRepairRadius = Math.Max(1, (int)(sliderDustRepairRadius?.Value ?? PreprocessConfig.DustRepairRadius));
        }

        private void UpdatePreprocessSummary()
        {
            if (tbPreprocessSummary == null)
            {
                return;
            }

            string openPolicy = PreprocessConfig.ApplyFilterOnOpen ? "打开时应用" : "手动应用";
            string clampPolicy = PreprocessConfig.ClampNonPositiveXyzOnLoad ? "XYZ<=0 修正" : "不修正 XYZ";
            string dustPolicy = PreprocessConfig.DustRemovalEnabled ? $"灰尘滤除 {PreprocessConfig.DustRemovalMode}" : "无灰尘滤除";
            string filterPolicy = NormalizeFilterType(PreprocessConfig.FilterType) == ImageFilterType.None
                ? "无滤波"
                : PreprocessConfig.FilterType.ToString();
            string pseudoColorPolicy = $"{ConoscopeChannelDisplayFormatter.GetLabel(RenderingConfig.DisplayChannel)} / {ColormapNameFormatter.Format(RenderingConfig.PseudoColorMap)}";

            tbPreprocessSummary.Text = $"{openPolicy} / {clampPolicy} / {dustPolicy} / {filterPolicy} / 伪彩 {pseudoColorPolicy}";
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

        private void cbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFilterType == null)
            {
                return;
            }

            ImageFilterType selectedFilter = GetSelectedFilterType();

            if (!isUpdatingFilterControls)
            {
                SaveFilterControlsToConfig();
            }

            UpdateFilterParameterVisibility(selectedFilter);
            UpdatePreprocessSummary();

            if (sliderKernelSize != null && sliderSigma != null && sliderD != null && sliderSigmaColor != null && sliderSigmaSpace != null)
            {
                sliderKernelSize.IsEnabled = false;
                sliderSigma.IsEnabled = false;
                sliderD.IsEnabled = false;
                sliderSigmaColor.IsEnabled = false;
                sliderSigmaSpace.IsEnabled = false;

                switch (selectedFilter)
                {
                    case ImageFilterType.None:
                        break;
                    case ImageFilterType.LowPass:
                    case ImageFilterType.MovingAverage:
                    case ImageFilterType.Median:
                        sliderKernelSize.IsEnabled = true;
                        break;
                    case ImageFilterType.Gaussian:
                        sliderKernelSize.IsEnabled = true;
                        sliderSigma.IsEnabled = true;
                        break;
                    case ImageFilterType.Bilateral:
                        sliderD.IsEnabled = true;
                        sliderSigmaColor.IsEnabled = true;
                        sliderSigmaSpace.IsEnabled = true;
                        break;
                }
            }
        }

        private void UpdateFilterParameterVisibility(ImageFilterType selectedFilter)
        {
            if (rowFilterKernel == null || rowFilterSigma == null || rowFilterD == null || rowFilterSigmaColor == null || rowFilterSigmaSpace == null
                || rowDustMode == null || rowDustThreshold == null || rowDustMinArea == null || rowDustMaxArea == null || rowDustRepairRadius == null)
            {
                return;
            }

            bool showKernel = selectedFilter is ImageFilterType.LowPass or ImageFilterType.MovingAverage or ImageFilterType.Gaussian or ImageFilterType.Median;
            bool showSigma = selectedFilter == ImageFilterType.Gaussian;
            bool showBilateral = selectedFilter == ImageFilterType.Bilateral;
            bool showDust = IsDustRemovalEnabled();

            rowFilterKernel.Visibility = showKernel ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigma.Visibility = showSigma ? Visibility.Visible : Visibility.Collapsed;
            rowFilterD.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigmaColor.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowFilterSigmaSpace.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            rowDustMode.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustThreshold.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustMinArea.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustMaxArea.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;
            rowDustRepairRadius.Visibility = showDust ? Visibility.Visible : Visibility.Collapsed;

            if (sliderDustThreshold != null && sliderDustMinArea != null && sliderDustMaxArea != null && sliderDustRepairRadius != null && cbDustMode != null)
            {
                sliderDustThreshold.IsEnabled = showDust;
                sliderDustMinArea.IsEnabled = showDust;
                sliderDustMaxArea.IsEnabled = showDust;
                sliderDustRepairRadius.IsEnabled = showDust;
                cbDustMode.IsEnabled = showDust;
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

        private bool HasPreprocessEnabled()
        {
            return PreprocessConfig.ClampNonPositiveXyzOnLoad
                || PreprocessConfig.DustRemovalEnabled
                || NormalizeFilterType(PreprocessConfig.FilterType) != ImageFilterType.None;
        }
    }
}
