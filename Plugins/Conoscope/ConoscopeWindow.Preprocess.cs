using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.UI;
using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private ImageFilterType lastEnabledWindowFilterType = ImageFilterType.LowPass;

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
                chkWindowUsePseudoColor.IsChecked = RenderingConfig.UsePseudoColor;
                chkWindowUsePseudoColorRangeLimit.IsChecked = RenderingConfig.UsePseudoColorRangeLimit;
                SelectPseudoColorMap(RenderingConfig.PseudoColorMap);

                ImageFilterType filterType = NormalizeFilterType(PreprocessConfig.FilterType);
                if (filterType != ImageFilterType.None)
                {
                    lastEnabledWindowFilterType = filterType;
                }

                chkWindowEnableFilter.IsChecked = filterType != ImageFilterType.None;
                cbWindowFilterType.SelectedValue = filterType == ImageFilterType.None ? lastEnabledWindowFilterType : filterType;
                txtWindowFilterKernelSize.Text = PreprocessConfig.FilterKernelSize.ToString();
                txtWindowFilterSigma.Text = PreprocessConfig.FilterSigma.ToString("0.0");
                txtWindowFilterD.Text = PreprocessConfig.FilterD.ToString();
                txtWindowFilterSigmaColor.Text = PreprocessConfig.FilterSigmaColor.ToString("0");
                txtWindowFilterSigmaSpace.Text = PreprocessConfig.FilterSigmaSpace.ToString("0");

                chkWindowDustRemovalEnabled.IsChecked = PreprocessConfig.DustRemovalEnabled;

                UpdateWindowPreprocessVisibility();
            }
            finally
            {
                isUpdatingPreprocessControls = false;
            }

            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && ActiveView != null;
        }

        private void UpdateWindowPreprocessVisibility()
        {
            bool usePseudoColor = chkWindowUsePseudoColor.IsChecked == true;
            bool useFilter = chkWindowEnableFilter.IsChecked == true;
            ImageFilterType filterType = cbWindowFilterType.SelectedValue is ImageFilterType selectedFilterType
                ? NormalizeFilterType(selectedFilterType)
                : lastEnabledWindowFilterType;

            panelWindowPseudoColorOptions.Visibility = usePseudoColor ? Visibility.Visible : Visibility.Collapsed;
            panelWindowFilterOptions.Visibility = useFilter ? Visibility.Visible : Visibility.Collapsed;

            fieldWindowFilterKernel.Visibility = filterType is ImageFilterType.LowPass or ImageFilterType.MovingAverage or ImageFilterType.Gaussian or ImageFilterType.Median
                ? Visibility.Visible
                : Visibility.Collapsed;
            fieldWindowFilterSigma.Visibility = filterType == ImageFilterType.Gaussian
                ? Visibility.Visible
                : Visibility.Collapsed;
            bool showBilateral = filterType == ImageFilterType.Bilateral;
            fieldWindowFilterD.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            fieldWindowFilterSigmaColor.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
            fieldWindowFilterSigmaSpace.Visibility = showBilateral ? Visibility.Visible : Visibility.Collapsed;
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

        private void chkWindowEnableFilter_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            bool isEnabled = chkWindowEnableFilter.IsChecked == true;
            ImageFilterType currentFilterType = NormalizeFilterType(PreprocessConfig.FilterType);
            if (!isEnabled)
            {
                if (currentFilterType != ImageFilterType.None)
                {
                    lastEnabledWindowFilterType = currentFilterType;
                }

                PreprocessConfig.FilterType = ImageFilterType.None;
            }
            else
            {
                ImageFilterType selectedFilterType = cbWindowFilterType.SelectedValue is ImageFilterType filterType
                    ? NormalizeFilterType(filterType)
                    : lastEnabledWindowFilterType;
                lastEnabledWindowFilterType = selectedFilterType;
                PreprocessConfig.FilterType = selectedFilterType;
            }

            UpdateWindowPreprocessVisibility();
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
                lastEnabledWindowFilterType = NormalizeFilterType(filterType);
                if (chkWindowEnableFilter.IsChecked == true)
                {
                    PreprocessConfig.FilterType = lastEnabledWindowFilterType;
                }

                UpdateWindowPreprocessVisibility();
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
            RenderingConfig.UsePseudoColorRangeLimit = chkWindowUsePseudoColorRangeLimit.IsChecked == true;
            UpdateWindowPreprocessVisibility();
            SaveRenderingConfig();
        }

        private void WindowDustRemoval_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            PreprocessConfig.DustRemovalEnabled = chkWindowDustRemovalEnabled.IsChecked == true;
            SavePreprocessConfig();
        }

        private void WindowPreprocessValue_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter)
            {
                return;
            }

            CommitWindowPreprocessValues();
            e.Handled = true;
        }

        private void WindowPreprocessValue_LostFocus(object sender, RoutedEventArgs e)
        {
            CommitWindowPreprocessValues();
        }

        private void CommitWindowPreprocessValues()
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (!TryApplyWindowPreprocessValues())
            {
                InitializePreprocessControls();
                return;
            }

            SavePreprocessConfig();
        }

        private bool TryApplyWindowPreprocessValues()
        {
            if (chkWindowEnableFilter.IsChecked == true)
            {
                if (fieldWindowFilterKernel.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowInt(txtWindowFilterKernelSize, out int kernelSize))
                    {
                        return false;
                    }

                    PreprocessConfig.FilterKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize(kernelSize);
                }

                if (fieldWindowFilterSigma.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowDouble(txtWindowFilterSigma, out double filterSigma))
                    {
                        return false;
                    }

                    PreprocessConfig.FilterSigma = filterSigma;
                }

                if (fieldWindowFilterD.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowInt(txtWindowFilterD, out int filterD))
                    {
                        return false;
                    }

                    PreprocessConfig.FilterD = filterD;
                }

                if (fieldWindowFilterSigmaColor.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowDouble(txtWindowFilterSigmaColor, out double sigmaColor))
                    {
                        return false;
                    }

                    PreprocessConfig.FilterSigmaColor = sigmaColor;
                }

                if (fieldWindowFilterSigmaSpace.Visibility == Visibility.Visible)
                {
                    if (!TryParseWindowDouble(txtWindowFilterSigmaSpace, out double sigmaSpace))
                    {
                        return false;
                    }

                    PreprocessConfig.FilterSigmaSpace = sigmaSpace;
                }
            }

            return true;
        }

        private static bool TryParseWindowInt(TextBox? textBox, out int value)
        {
            value = 0;
            if (!ConoscopeNumericHelper.TryParseDouble(textBox?.Text, out double parsedValue) || !double.IsFinite(parsedValue))
            {
                return false;
            }

            value = Math.Max(1, (int)Math.Round(parsedValue));
            return true;
        }

        private static bool TryParseWindowDouble(TextBox? textBox, out double value)
        {
            value = 0;
            return ConoscopeNumericHelper.TryParseDouble(textBox?.Text, out value) && double.IsFinite(value);
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
                view.ApplyWindowPreprocessDefaults();
            }
        }

        private void SaveRenderingConfig()
        {
            ConfigService.Instance.Save<ConoscopeConfig>();
            InitializePreprocessControls();
            foreach (ConoscopeView view in GetOpenViews())
            {
                view.ApplyWindowRenderingDefaults();
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
                        view.ApplyWindowRenderingDefaults();
                    }
                    else
                    {
                        view.ApplyWindowPreprocessDefaults();
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
                or nameof(ConoscopeConfig.UsePseudoColor)
                or nameof(ConoscopeConfig.UsePseudoColorRangeLimit);
        }

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }
    }
}
