using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.UI;
using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private sealed class PseudoColorMapOption
        {
            public PseudoColorMapOption(string name, ColormapTypes value)
            {
                Name = name;
                Value = value;
            }

            public string Name { get; }
            public ColormapTypes Value { get; }
        }

        private void InitializePreprocessControls()
        {
            isUpdatingPreprocessControls = true;
            try
            {
                InitializePseudoColorMapOptions();
                chkWindowApplyFilterOnOpen.IsChecked = PreprocessConfig.ApplyFilterOnOpen;
                cbWindowFilterType.SelectedValue = NormalizeFilterType(PreprocessConfig.FilterType);
                tbWindowFilterConfigSummary.Text = BuildWindowFilterConfigSummary();
                tbWindowFilterConfigSummary.ToolTip = BuildWindowFilterConfigToolTip();
                chkWindowUsePseudoColor.IsChecked = RenderingConfig.UsePseudoColor;
                SelectPseudoColorMap(RenderingConfig.PseudoColorMap);
                imgWindowPseudoColorMapPreview.Source = ColormapConstats.CreatePreviewImage(RenderingConfig.PseudoColorMap);
            }
            finally
            {
                isUpdatingPreprocessControls = false;
            }

            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && ActiveView != null;
        }

        private string BuildWindowFilterConfigSummary()
        {
            string filterSummary = NormalizeFilterType(PreprocessConfig.FilterType) switch
            {
                ImageFilterType.None => "不过滤",
                ImageFilterType.LowPass => $"低通 核 {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.MovingAverage => $"均值 核 {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Gaussian => $"高斯 核 {PreprocessConfig.FilterKernelSize}  σ {PreprocessConfig.FilterSigma:F1}",
                ImageFilterType.Median => $"中值 核 {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Bilateral => $"双边 d {PreprocessConfig.FilterD}  σC {PreprocessConfig.FilterSigmaColor:F0}  σS {PreprocessConfig.FilterSigmaSpace:F0}",
                _ => "使用默认参数"
            };

            string dustSummary = PreprocessConfig.DustRemovalEnabled
                ? $"灰尘 {FormatDustRemovalMode(PreprocessConfig.DustRemovalMode)} {PreprocessConfig.DustThresholdPercent:F1}%"
                : "灰尘关闭";

            return $"{filterSummary} | {dustSummary}";
        }

        private string BuildWindowFilterConfigToolTip()
        {
            string dustSummary = PreprocessConfig.DustRemovalEnabled
                ? $"灰尘滤除: {FormatDustRemovalMode(PreprocessConfig.DustRemovalMode)}，阈值 {PreprocessConfig.DustThresholdPercent:F1}% ，面积 {PreprocessConfig.DustMinArea}-{PreprocessConfig.DustMaxArea}px，修复半径 {PreprocessConfig.DustRepairRadius}px"
                : "灰尘滤除: 关闭";

            return NormalizeFilterType(PreprocessConfig.FilterType) switch
            {
                ImageFilterType.None => $"滤波: 无。{dustSummary}",
                ImageFilterType.LowPass => $"滤波: 低通，核大小 {PreprocessConfig.FilterKernelSize}。{dustSummary}",
                ImageFilterType.MovingAverage => $"滤波: 均值，核大小 {PreprocessConfig.FilterKernelSize}。{dustSummary}",
                ImageFilterType.Gaussian => $"滤波: 高斯，核大小 {PreprocessConfig.FilterKernelSize}，Sigma {PreprocessConfig.FilterSigma:F1}。{dustSummary}",
                ImageFilterType.Median => $"滤波: 中值，核大小 {PreprocessConfig.FilterKernelSize}。{dustSummary}",
                ImageFilterType.Bilateral => $"滤波: 双边，d {PreprocessConfig.FilterD}，SigmaColor {PreprocessConfig.FilterSigmaColor:F0}，SigmaSpace {PreprocessConfig.FilterSigmaSpace:F0}。{dustSummary}",
                _ => $"滤波: 使用默认参数。{dustSummary}"
            };
        }

        private static string FormatDustRemovalMode(DustRemovalMode mode)
        {
            return mode switch
            {
                DustRemovalMode.DarkSpot => "暗斑",
                DustRemovalMode.BrightSpot => "亮斑",
                DustRemovalMode.Both => "暗斑+亮斑",
                _ => mode.ToString()
            };
        }

        private void WindowPreprocess_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            PreprocessConfig.ApplyFilterOnOpen = chkWindowApplyFilterOnOpen.IsChecked == true;
            SavePreprocessConfig();
        }

        private void cbWindowFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (cbWindowFilterType.SelectedValue is ImageFilterType filterType)
            {
                PreprocessConfig.FilterType = NormalizeFilterType(filterType);
                SavePreprocessConfig();
            }
        }

        private void cbWindowPseudoColorMap_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (cbWindowPseudoColorMap.SelectedItem is PseudoColorMapOption selectedItem)
            {
                RenderingConfig.PseudoColorMap = selectedItem.Value;
                imgWindowPseudoColorMapPreview.Source = ColormapConstats.CreatePreviewImage(selectedItem.Value);
                SaveRenderingConfig();
            }
        }

        private void WindowDisplay_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            RenderingConfig.UsePseudoColor = chkWindowUsePseudoColor.IsChecked == true;
            SaveRenderingConfig();
        }

        private void InitializePseudoColorMapOptions()
        {
            ComboBox? pseudoColorMapComboBox = cbWindowPseudoColorMap;
            if (pseudoColorMapComboBox == null || pseudoColorMapComboBox.ItemsSource != null)
            {
                return;
            }

            pseudoColorMapComboBox.DisplayMemberPath = nameof(PseudoColorMapOption.Name);
            pseudoColorMapComboBox.ItemsSource = Enum.GetValues<ColormapTypes>()
                .Select(item => new PseudoColorMapOption(ColormapNameFormatter.Format(item), item))
                .ToArray();
        }

        private void SelectPseudoColorMap(ColormapTypes colormapType)
        {
            if (cbWindowPseudoColorMap?.ItemsSource == null)
            {
                return;
            }

            cbWindowPseudoColorMap.SelectedItem = cbWindowPseudoColorMap.Items
                .OfType<PseudoColorMapOption>()
                .FirstOrDefault(item => item.Value == colormapType);
        }

        private void btnOpenPreprocessSettings_Click(object sender, RoutedEventArgs e)
        {
            ConoscopePreprocessSettingsWindow dialog = new ConoscopePreprocessSettingsWindow(ConoscopeConfig)
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            };

            dialog.ShowDialog();
        }

        private void SavePreprocessConfig()
        {
            ConfigService.Instance.Save<ConoscopeConfig>();
            InitializePreprocessControls();
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.RefreshPreprocessControlsFromConfig();
            }
        }

        private void SaveRenderingConfig()
        {
            ConfigService.Instance.Save<ConoscopeConfig>();
            InitializePreprocessControls();
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.RefreshRenderingFromConfig();
            }
        }

        private void ConoscopeConfig_PropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls)
            {
                return;
            }

            bool preprocessChanged = IsPreprocessProperty(e.PropertyName);
            bool displayChanged = IsDisplayProperty(e.PropertyName);
            if (!preprocessChanged && !displayChanged)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(() =>
            {
                InitializePreprocessControls();
                foreach (ConoscopeView view in GetOpenViews())
                {
                    if (displayChanged)
                    {
                        view.RefreshRenderingFromConfig();
                    }
                    else
                    {
                        view.RefreshPreprocessControlsFromConfig();
                    }
                }
            }));
        }

        private static bool IsPreprocessProperty(string? propertyName)
        {
            return propertyName is nameof(ConoscopeConfig.ApplyFilterOnOpen)
                or nameof(ConoscopeConfig.ClampNonPositiveXyzOnLoad)
                or nameof(ConoscopeConfig.DustRemovalEnabled)
                or nameof(ConoscopeConfig.DustRemovalMode)
                or nameof(ConoscopeConfig.DustThresholdPercent)
                or nameof(ConoscopeConfig.DustMinArea)
                or nameof(ConoscopeConfig.DustMaxArea)
                or nameof(ConoscopeConfig.DustRepairRadius)
                or nameof(ConoscopeConfig.FilterType)
                or nameof(ConoscopeConfig.FilterKernelSize)
                or nameof(ConoscopeConfig.FilterSigma)
                or nameof(ConoscopeConfig.FilterD)
                or nameof(ConoscopeConfig.FilterSigmaColor)
                or nameof(ConoscopeConfig.FilterSigmaSpace);
        }

        private static bool IsDisplayProperty(string? propertyName)
        {
            return propertyName is nameof(ConoscopeConfig.DisplayChannel)
                or nameof(ConoscopeConfig.PseudoColorMap)
                or nameof(ConoscopeConfig.UsePseudoColor);
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }
    }
}
