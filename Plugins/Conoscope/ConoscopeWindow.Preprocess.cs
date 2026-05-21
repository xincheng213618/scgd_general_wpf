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
                ImageFilterType.None => Properties.Resources.FilterNone,
                ImageFilterType.LowPass => $"{Properties.Resources.FilterLowPass} 核 {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.MovingAverage => $"{Properties.Resources.FilterMean} 核 {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Gaussian => $"{Properties.Resources.FilterGaussian} 核 {PreprocessConfig.FilterKernelSize}  σ {PreprocessConfig.FilterSigma:F1}",
                ImageFilterType.Median => $"{Properties.Resources.FilterMedian} 核 {PreprocessConfig.FilterKernelSize}",
                ImageFilterType.Bilateral => $"{Properties.Resources.FilterBilateral} d {PreprocessConfig.FilterD}  σC {PreprocessConfig.FilterSigmaColor:F0}  σS {PreprocessConfig.FilterSigmaSpace:F0}",
                _ => Properties.Resources.FilterDefaultParams
            };

            string dustSummary = PreprocessConfig.DustRemovalEnabled
                ? $"{Properties.Resources.DustLabel} {FormatDustRemovalMode(PreprocessConfig.DustRemovalMode)} {PreprocessConfig.DustThresholdPercent:F1}%"
                : Properties.Resources.DustOff;

            return $"{filterSummary} | {dustSummary}";
        }

        private string BuildWindowFilterConfigToolTip()
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
