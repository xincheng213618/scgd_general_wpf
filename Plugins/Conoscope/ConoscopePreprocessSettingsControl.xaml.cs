using ColorVision.UI;
using Conoscope.Core;
using Conoscope.Presentation.Helpers;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace Conoscope
{
    public partial class ConoscopePreprocessSettingsControl : UserControl
    {
        private readonly ConoscopeConfig config;
        private readonly bool persistChanges;
        private bool isUpdating;

        public event EventHandler? SettingsChanged;

        public ConoscopePreprocessSettingsControl(ConoscopeConfig config, bool persistChanges = false)
        {
            InitializeComponent();

            this.config = config ?? throw new ArgumentNullException(nameof(config));
            this.persistChanges = persistChanges;

            this.config.PropertyChanged += Config_PropertyChanged;
            Unloaded += ConoscopePreprocessSettingsControl_Unloaded;

            RefreshFromConfig();
        }

        private ConoscopePreprocessSettings PreprocessConfig => config.Preprocess;

        public void RefreshFromConfig()
        {
            isUpdating = true;
            try
            {
                MigrateLegacyDustRemovalFilterType();
                chkPreprocessEnabled.IsChecked = PreprocessConfig.ApplyFilterOnOpen;
                cbFilterType.SelectedValue = NormalizeFilterType(PreprocessConfig.FilterType);
                ComboBoxHelper.SelectItemByTag(cbDustMode, PreprocessConfig.DustRemovalMode.ToString());

                sliderKernelSize.Value = PreprocessConfig.FilterKernelSize;
                sliderSigma.Value = PreprocessConfig.FilterSigma;
                sliderD.Value = PreprocessConfig.FilterD;
                sliderSigmaColor.Value = PreprocessConfig.FilterSigmaColor;
                sliderSigmaSpace.Value = PreprocessConfig.FilterSigmaSpace;
                sliderDustThreshold.Value = PreprocessConfig.DustThresholdPercent;
                sliderDustMinArea.Value = PreprocessConfig.DustMinArea;
                sliderDustMaxArea.Value = Math.Max(PreprocessConfig.DustMinArea, PreprocessConfig.DustMaxArea);
                sliderDustRepairRadius.Value = PreprocessConfig.DustRepairRadius;
                chkDustRemovalEnabled.IsChecked = PreprocessConfig.DustRemovalEnabled;
            }
            finally
            {
                isUpdating = false;
            }

            UpdateFilterParameterVisibility(GetSelectedFilterType());
        }

        private void ConoscopePreprocessSettingsControl_Unloaded(object sender, RoutedEventArgs e)
        {
            config.PropertyChanged -= Config_PropertyChanged;
            Unloaded -= ConoscopePreprocessSettingsControl_Unloaded;
        }

        private void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (isUpdating)
            {
                return;
            }

            Dispatcher.BeginInvoke(new Action(RefreshFromConfig));
        }

        private void PreprocessSetting_Changed(object sender, RoutedEventArgs e)
        {
            if (isUpdating || !IsLoaded)
            {
                return;
            }

            ApplyControlValues();
        }

        private void cbFilterType_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (cbFilterType == null)
            {
                return;
            }

            ImageFilterType selectedFilter = GetSelectedFilterType();
            UpdateFilterParameterVisibility(selectedFilter);

            if (isUpdating || !IsLoaded)
            {
                return;
            }

            ApplyControlValues();
        }

        private void ApplyControlValues()
        {
            PreprocessConfig.ApplyFilterOnOpen = chkPreprocessEnabled.IsChecked == true;
            PreprocessConfig.FilterType = NormalizeFilterType(GetSelectedFilterType());
            PreprocessConfig.FilterKernelSize = ConoscopeNumericHelper.NormalizeOddKernelSize((int)sliderKernelSize.Value);
            PreprocessConfig.FilterSigma = sliderSigma.Value;
            PreprocessConfig.FilterD = Math.Max(1, (int)sliderD.Value);
            PreprocessConfig.FilterSigmaColor = sliderSigmaColor.Value;
            PreprocessConfig.FilterSigmaSpace = sliderSigmaSpace.Value;
            PreprocessConfig.DustRemovalEnabled = chkDustRemovalEnabled.IsChecked == true;
            PreprocessConfig.DustRemovalMode = ComboBoxHelper.GetSelectedEnumByTag(cbDustMode, PreprocessConfig.DustRemovalMode);
            PreprocessConfig.DustThresholdPercent = sliderDustThreshold.Value;
            PreprocessConfig.DustMinArea = Math.Max(1, (int)sliderDustMinArea.Value);
            PreprocessConfig.DustMaxArea = Math.Max(PreprocessConfig.DustMinArea, (int)sliderDustMaxArea.Value);
            PreprocessConfig.DustRepairRadius = Math.Max(1, (int)sliderDustRepairRadius.Value);

            PersistIfNeeded();
            SettingsChanged?.Invoke(this, EventArgs.Empty);
        }

        private void PersistIfNeeded()
        {
            if (!persistChanges)
            {
                return;
            }

            try
            {
                ConfigService.Instance.Save<ConoscopeConfig>();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgPreprocessConfigSaveFailedDetail, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Warning);
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

        private static ImageFilterType NormalizeFilterType(ImageFilterType filterType)
        {
            return Enum.IsDefined(filterType) ? filterType : ImageFilterType.None;
        }

        private ImageFilterType GetSelectedFilterType()
        {
            return cbFilterType.SelectedValue is ImageFilterType filterType
                ? NormalizeFilterType(filterType)
                : NormalizeFilterType(PreprocessConfig.FilterType);
        }

        private void UpdateFilterParameterVisibility(ImageFilterType selectedFilter)
        {
            bool showKernel = selectedFilter is ImageFilterType.LowPass or ImageFilterType.MovingAverage or ImageFilterType.Gaussian or ImageFilterType.Median;
            bool showSigma = selectedFilter == ImageFilterType.Gaussian;
            bool showBilateral = selectedFilter == ImageFilterType.Bilateral;
            bool showDust = chkDustRemovalEnabled.IsChecked == true;

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
        }
    }
}