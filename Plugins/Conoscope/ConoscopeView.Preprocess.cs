using ColorVision.Core;
using ColorVision.ImageEditor;
using Conoscope.ApplicationServices.Preprocess;
using Conoscope.Core;
using Conoscope.Processing.Preprocess;
using Conoscope.Presentation.Formatters;
using Conoscope.Presentation.Helpers;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopeView
    {
        private sealed class ViewPseudoColorMapOption
        {
            public ViewPseudoColorMapOption(string name, ColormapTypes value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public ColormapTypes Value { get; }
        }

        private bool isUpdatingViewPreprocessQuickControls;
        private ImageFilterType lastEnabledFilterType = ImageFilterType.LowPass;

        private void InitializePreprocessControls()
        {
            MigrateLegacyDustRemovalFilterType();
            InitializeViewPseudoColorMapOptions();

            isUpdatingViewPreprocessQuickControls = true;
            try
            {
                if (chkViewUsePseudoColor != null)
                {
                    chkViewUsePseudoColor.IsChecked = RenderingConfig.UsePseudoColor;
                }

                if (chkViewDustRemoval != null)
                {
                    chkViewDustRemoval.IsChecked = PreprocessConfig.DustRemovalEnabled;
                }

                ImageFilterType filterType = NormalizeFilterType(PreprocessConfig.FilterType);
                if (filterType != ImageFilterType.None)
                {
                    lastEnabledFilterType = filterType;
                }

                if (chkViewEnableFilter != null)
                {
                    chkViewEnableFilter.IsChecked = filterType != ImageFilterType.None;
                }

                if (cbViewFilterType != null)
                {
                    cbViewFilterType.SelectedValue = filterType == ImageFilterType.None ? lastEnabledFilterType : filterType;
                    cbViewFilterType.IsEnabled = filterType != ImageFilterType.None;
                }

                SelectViewPseudoColorMap(RenderingConfig.PseudoColorMap);

                if (tbViewFilterSummary != null)
                {
                    tbViewFilterSummary.Text = BuildViewFilterSummary();
                    tbViewFilterSummary.ToolTip = BuildViewFilterToolTip();
                }
            }
            finally
            {
                isUpdatingViewPreprocessQuickControls = false;
            }
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

        private void InitializeViewPseudoColorMapOptions()
        {
            ComboBox? comboBox = cbViewPseudoColorMap;
            if (comboBox == null || comboBox.ItemsSource != null)
            {
                return;
            }

            comboBox.DisplayMemberPath = nameof(ViewPseudoColorMapOption.Name);
            comboBox.ItemsSource = Enum.GetValues<ColormapTypes>()
                .Select(item => new ViewPseudoColorMapOption(ColormapNameFormatter.Format(item), item))
                .ToArray();
        }

        private void SelectViewPseudoColorMap(ColormapTypes colormapType)
        {
            if (cbViewPseudoColorMap?.ItemsSource == null)
            {
                return;
            }

            cbViewPseudoColorMap.SelectedItem = cbViewPseudoColorMap.Items
                .OfType<ViewPseudoColorMapOption>()
                .FirstOrDefault(item => item.Value == colormapType);
        }

        private string BuildViewFilterSummary()
        {
            return NormalizeFilterType(PreprocessConfig.FilterType) switch
            {
                ImageFilterType.None => "0",
                ImageFilterType.LowPass => $"{Properties.Resources.FilterLowPass} {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.MovingAverage => $"{Properties.Resources.FilterMean} {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Gaussian => $"{Properties.Resources.FilterGaussian} {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Median => $"{Properties.Resources.FilterMedian} {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Bilateral => $"{Properties.Resources.FilterBilateral} {PreprocessConfig.FilterD}",
                _ => "0"
            };
        }

        private string BuildViewFilterToolTip()
        {
            string dustSummary = PreprocessConfig.DustRemovalEnabled
                ? $"{Properties.Resources.HeaderDustRemoval}: {FormatDustRemovalMode(PreprocessConfig.DustRemovalMode)}，阈值 {PreprocessConfig.DustThresholdPercent:F1}% ，面积 {PreprocessConfig.DustMinArea}-{PreprocessConfig.DustMaxArea}px，修复半径 {PreprocessConfig.DustRepairRadius}px"
                : $"{Properties.Resources.HeaderDustRemoval}: {Properties.Resources.DustOff}";

            return NormalizeFilterType(PreprocessConfig.FilterType) switch
            {
                ImageFilterType.None => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterNone}。{dustSummary}",
                ImageFilterType.LowPass => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterLowPass}，核大小 {PreprocessConfig.FilterKernelSize}。{dustSummary}",
                ImageFilterType.MovingAverage => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterMean}，核大小 {PreprocessConfig.FilterKernelSize}。{dustSummary}",
                ImageFilterType.Gaussian => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterGaussian}，核大小 {PreprocessConfig.FilterKernelSize}，Sigma {PreprocessConfig.FilterSigma:F1}。{dustSummary}",
                ImageFilterType.Median => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterMedian}，核大小 {PreprocessConfig.FilterKernelSize}。{dustSummary}",
                ImageFilterType.Bilateral => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterBilateral}，d {PreprocessConfig.FilterD}，SigmaColor {PreprocessConfig.FilterSigmaColor:F0}，SigmaSpace {PreprocessConfig.FilterSigmaSpace:F0}。{dustSummary}",
                _ => $"{Properties.Resources.HeaderFilter}: {Properties.Resources.FilterDefaultParams}。{dustSummary}"
            };
        }

        private static string FormatDustRemovalMode(DustRemovalMode mode)
        {
            return mode switch
            {
                DustRemovalMode.DarkSpot => Properties.Resources.DustDarkSpot,
                DustRemovalMode.BrightSpot => Properties.Resources.DustBrightSpot,
                DustRemovalMode.Both => Properties.Resources.DustBoth,
                _ => mode.ToString()
            };
        }

        private ImageFilterType GetSelectedViewFilterType()
        {
            return cbViewFilterType?.SelectedValue is ImageFilterType filterType
                ? NormalizeFilterType(filterType)
                : lastEnabledFilterType;
        }

        private void ViewDisplayQuick_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingViewPreprocessQuickControls || !IsInitialized)
            {
                return;
            }

            RenderingConfig.UsePseudoColor = chkViewUsePseudoColor?.IsChecked == true;
            RefreshRenderingFromConfig();
        }

        private void cbViewPseudoColorMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingViewPreprocessQuickControls || !IsInitialized)
            {
                return;
            }

            if (cbViewPseudoColorMap?.SelectedItem is not ViewPseudoColorMapOption selectedItem)
            {
                return;
            }

            RenderingConfig.PseudoColorMap = selectedItem.Value;
            RefreshRenderingFromConfig();
        }

        private void ViewPreprocessQuick_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingViewPreprocessQuickControls || !IsInitialized)
            {
                return;
            }

            PreprocessConfig.DustRemovalEnabled = chkViewDustRemoval?.IsChecked == true;
            InitializePreprocessControls();
        }

        private void chkViewEnableFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingViewPreprocessQuickControls || !IsInitialized)
            {
                return;
            }

            bool isEnabled = chkViewEnableFilter?.IsChecked == true;
            ImageFilterType currentFilterType = NormalizeFilterType(PreprocessConfig.FilterType);
            if (!isEnabled)
            {
                if (currentFilterType != ImageFilterType.None)
                {
                    lastEnabledFilterType = currentFilterType;
                }

                PreprocessConfig.FilterType = ImageFilterType.None;
            }
            else
            {
                PreprocessConfig.FilterType = currentFilterType == ImageFilterType.None ? lastEnabledFilterType : currentFilterType;
            }

            InitializePreprocessControls();
        }

        private void cbViewFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingViewPreprocessQuickControls || !IsInitialized)
            {
                return;
            }

            ImageFilterType filterType = GetSelectedViewFilterType();
            lastEnabledFilterType = filterType;
            if (chkViewEnableFilter?.IsChecked == true)
            {
                PreprocessConfig.FilterType = filterType;
            }

            InitializePreprocessControls();
        }

        private bool HasPreprocessEnabled()
        {
            return PreprocessConfig.ClampNonPositiveXyzOnLoad
                || PreprocessConfig.DustRemovalEnabled
                || NormalizeFilterType(PreprocessConfig.FilterType) != ImageFilterType.None;
        }
    }
}
