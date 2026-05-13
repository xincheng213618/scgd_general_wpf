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
                ComboBoxHelper.SelectItemByTag(cbFilterType, NormalizeFilterType(ConoscopeConfig.FilterType).ToString());
                ComboBoxHelper.SelectItemByTag(cbDustMode, ConoscopeConfig.DustRemovalMode.ToString());
                chkClampNonPositiveXyzOnLoad.IsChecked = ConoscopeConfig.ClampNonPositiveXyzOnLoad;
                chkDustRemovalEnabled.IsChecked = ConoscopeConfig.DustRemovalEnabled;

                sliderKernelSize.Value = ConoscopeConfig.FilterKernelSize;
                sliderSigma.Value = ConoscopeConfig.FilterSigma;
                sliderD.Value = ConoscopeConfig.FilterD;
                sliderSigmaColor.Value = ConoscopeConfig.FilterSigmaColor;
                sliderSigmaSpace.Value = ConoscopeConfig.FilterSigmaSpace;
                sliderDustThreshold.Value = ConoscopeConfig.DustThresholdPercent;
                sliderDustMinArea.Value = ConoscopeConfig.DustMinArea;
                sliderDustMaxArea.Value = Math.Max(ConoscopeConfig.DustMinArea, ConoscopeConfig.DustMaxArea);
                sliderDustRepairRadius.Value = ConoscopeConfig.DustRepairRadius;
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
            ConoscopeConfig.ClampNonPositiveXyzOnLoad = chkClampNonPositiveXyzOnLoad?.IsChecked == true;
            ConoscopeConfig.FilterType = NormalizeFilterType(GetSelectedFilterType());
            ConoscopeConfig.FilterKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize((int)(sliderKernelSize?.Value ?? ConoscopeConfig.FilterKernelSize));
            ConoscopeConfig.FilterSigma = sliderSigma?.Value ?? ConoscopeConfig.FilterSigma;
            ConoscopeConfig.FilterD = Math.Max(1, (int)(sliderD?.Value ?? ConoscopeConfig.FilterD));
            ConoscopeConfig.FilterSigmaColor = sliderSigmaColor?.Value ?? ConoscopeConfig.FilterSigmaColor;
            ConoscopeConfig.FilterSigmaSpace = sliderSigmaSpace?.Value ?? ConoscopeConfig.FilterSigmaSpace;
            ConoscopeConfig.DustRemovalEnabled = IsDustRemovalEnabled();
            ConoscopeConfig.DustRemovalMode = GetSelectedDustRemovalMode();
            ConoscopeConfig.DustThresholdPercent = sliderDustThreshold?.Value ?? ConoscopeConfig.DustThresholdPercent;
            ConoscopeConfig.DustMinArea = Math.Max(1, (int)(sliderDustMinArea?.Value ?? ConoscopeConfig.DustMinArea));
            ConoscopeConfig.DustMaxArea = Math.Max(ConoscopeConfig.DustMinArea, (int)(sliderDustMaxArea?.Value ?? ConoscopeConfig.DustMaxArea));
            ConoscopeConfig.DustRepairRadius = Math.Max(1, (int)(sliderDustRepairRadius?.Value ?? ConoscopeConfig.DustRepairRadius));
        }

        private void UpdatePreprocessSummary()
        {
            if (tbPreprocessSummary == null)
            {
                return;
            }

            string openPolicy = ConoscopeConfig.ApplyFilterOnOpen ? "打开时应用" : "手动应用";
            string clampPolicy = ConoscopeConfig.ClampNonPositiveXyzOnLoad ? "XYZ<=0 修正" : "不修正 XYZ";
            string dustPolicy = ConoscopeConfig.DustRemovalEnabled ? $"灰尘滤除 {ConoscopeConfig.DustRemovalMode}" : "无灰尘滤除";
            string filterPolicy = NormalizeFilterType(ConoscopeConfig.FilterType) == ImageFilterType.None
                ? "无滤波"
                : ConoscopeConfig.FilterType.ToString();
            string pseudoColorPolicy = $"{ConoscopeChannelDisplayFormatter.GetLabel(ConoscopeConfig.DisplayChannel)} / {ColormapNameFormatter.Format(ConoscopeConfig.PseudoColorMap)}";

            tbPreprocessSummary.Text = $"{openPolicy} / {clampPolicy} / {dustPolicy} / {filterPolicy} / 伪彩 {pseudoColorPolicy}";
        }

        private void MigrateLegacyDustRemovalFilterType()
        {
            const int legacyDustRemovalFilterValue = 6;
            if ((int)ConoscopeConfig.FilterType == legacyDustRemovalFilterValue)
            {
                ConoscopeConfig.DustRemovalEnabled = true;
                ConoscopeConfig.FilterType = ImageFilterType.None;
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
                log.Info($"开始应用预处理: clamp={ConoscopeConfig.ClampNonPositiveXyzOnLoad}, dust={ConoscopeConfig.DustRemovalEnabled}, filter={ConoscopeConfig.FilterType}");
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
            return ConoscopePreprocessOptions.FromConfig(ConoscopeConfig, MinPositiveXyzValue);
        }

        private bool HasPreprocessEnabled()
        {
            return ConoscopeConfig.ClampNonPositiveXyzOnLoad
                || ConoscopeConfig.DustRemovalEnabled
                || NormalizeFilterType(ConoscopeConfig.FilterType) != ImageFilterType.None;
        }
    }
}
