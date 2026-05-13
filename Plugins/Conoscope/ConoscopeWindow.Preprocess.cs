using ColorVision.Core;
using ColorVision.ImageEditor;
using ColorVision.UI;
using Conoscope.Core;
using Conoscope.Presentation.Formatters;
using Conoscope.Presentation.Helpers;
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
                chkWindowApplyFilterOnOpen.IsChecked = PreprocessConfig.ApplyFilterOnOpen;
                chkWindowClampNonPositiveXyzOnLoad.IsChecked = PreprocessConfig.ClampNonPositiveXyzOnLoad;
                chkWindowDustRemovalEnabled.IsChecked = PreprocessConfig.DustRemovalEnabled;
                ComboBoxHelper.SelectItemByTag(cbWindowPseudoColorChannel, RenderingConfig.DisplayChannel.ToString());
                ComboBoxHelper.SelectItemByTag(cbWindowFilterType, PreprocessConfig.FilterType.ToString());
                SelectPseudoColorMap(RenderingConfig.PseudoColorMap);
                imgWindowPseudoColorMapPreview.Source = ColormapConstats.CreatePreviewImage(RenderingConfig.PseudoColorMap);
            }
            finally
            {
                isUpdatingPreprocessControls = false;
            }

            btnApplyPreprocessToActiveView.IsEnabled = !isRunningOperation && ActiveView != null;
        }

        private void InitializePseudoColorMapOptions()
        {
            cbWindowPseudoColorMap.DisplayMemberPath = nameof(PseudoColorMapOption.Name);
            cbWindowPseudoColorMap.ItemsSource = Enum.GetValues<ColormapTypes>()
                .Select(item => new PseudoColorMapOption(ColormapNameFormatter.Format(item), item))
                .ToArray();
        }

        private void SelectPseudoColorMap(ColormapTypes colormapType)
        {
            cbWindowPseudoColorMap.SelectedItem = cbWindowPseudoColorMap.Items
                .OfType<PseudoColorMapOption>()
                .FirstOrDefault(item => item.Value == colormapType);
        }

        private void WindowPreprocess_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            PreprocessConfig.ApplyFilterOnOpen = chkWindowApplyFilterOnOpen.IsChecked == true;
            PreprocessConfig.ClampNonPositiveXyzOnLoad = chkWindowClampNonPositiveXyzOnLoad.IsChecked == true;
            PreprocessConfig.DustRemovalEnabled = chkWindowDustRemovalEnabled.IsChecked == true;
            SavePreprocessConfig();
        }

        private void cbWindowFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (cbWindowFilterType.SelectedItem is ComboBoxItem selectedItem
                && selectedItem.Tag is string filterTag
                && Enum.TryParse(filterTag, out ImageFilterType filterType))
            {
                PreprocessConfig.FilterType = filterType;
                SavePreprocessConfig();
            }
        }

        private void cbWindowPseudoColorChannel_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (isUpdatingPreprocessControls || !IsInitialized)
            {
                return;
            }

            if (cbWindowPseudoColorChannel.SelectedItem is ComboBoxItem selectedItem
                && selectedItem.Tag is string channelTag
                && Enum.TryParse(channelTag, out ExportChannel channel))
            {
                RenderingConfig.DisplayChannel = channel;
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
                SavePreprocessConfig();
            }
        }

        private void SavePreprocessConfig()
        {
            ConfigService.Instance.Save<ConoscopeConfig>();
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

            if (e.PropertyName is nameof(ConoscopeConfig.ApplyFilterOnOpen)
                or nameof(ConoscopeConfig.ClampNonPositiveXyzOnLoad)
                or nameof(ConoscopeConfig.DustRemovalEnabled)
                or nameof(ConoscopeConfig.FilterType)
                or nameof(ConoscopeConfig.DisplayChannel)
                or nameof(ConoscopeConfig.PseudoColorMap))
            {
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    InitializePreprocessControls();
                    foreach (ConoscopeView view in GetOpenViews())
                    {
                        view.RefreshRenderingFromConfig();
                    }
                }));
            }
        }
    }
}
