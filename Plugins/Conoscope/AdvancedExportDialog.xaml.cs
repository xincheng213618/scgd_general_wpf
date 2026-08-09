using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Automation;
using Conoscope.Core;

namespace Conoscope
{
    /// <summary>
    /// AdvancedExportDialog.xaml 的交互逻辑
    /// </summary>
    public partial class AdvancedExportDialog : Window
    {
        public AdvancedExportSettings Settings { get; private set; }

        public AdvancedExportDialog(AdvancedExportSettings? initialSettings = null, int defaultDecimalPlaces = 4)
        {
            InitializeComponent();
            InitializeLocalizedText();
            Settings = NormalizeSettings(initialSettings, defaultDecimalPlaces);
            ApplySettings(Settings);
            UpdateExportUiState();
        }

        private void InitializeLocalizedText()
        {
            tbChannelPresets.Text = UiText("Ui_ChannelPresets", "快捷选择：");
            btnPresetAll.Content = UiText("Ui_SelectAll", "全选");
            tbFooterHint.Text = UiText("Ui_ExportFooterHint", "仅会导出上方勾选的通道和模式。");
            AutomationProperties.SetName(btnPresetAll, btnPresetAll.Content?.ToString() ?? string.Empty);
        }

        private void ExportOption_Changed(object sender, RoutedEventArgs e)
        {
            UpdateExportUiState();
        }

        private void ExportText_Changed(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            UpdateExportUiState();
        }

        private void btnPresetXyz_Click(object sender, RoutedEventArgs e)
        {
            ApplyChannelPreset(Core.ExportChannel.X, Core.ExportChannel.Y, Core.ExportChannel.Z);
        }

        private void btnPresetChromaticity_Click(object sender, RoutedEventArgs e)
        {
            ApplyChannelPreset(Core.ExportChannel.CieX, Core.ExportChannel.CieY, Core.ExportChannel.CieU, Core.ExportChannel.CieV);
        }

        private void btnPresetAll_Click(object sender, RoutedEventArgs e)
        {
            ApplyChannelPreset(
                Core.ExportChannel.X,
                Core.ExportChannel.Y,
                Core.ExportChannel.Z,
                Core.ExportChannel.CieX,
                Core.ExportChannel.CieY,
                Core.ExportChannel.CieU,
                Core.ExportChannel.CieV,
                Core.ExportChannel.ColorDifference,
                Core.ExportChannel.Contrast);
        }

        private void ApplyChannelPreset(params Core.ExportChannel[] channels)
        {
            HashSet<Core.ExportChannel> selected = new HashSet<Core.ExportChannel>(channels);
            chkChannelX.IsChecked = selected.Contains(Core.ExportChannel.X);
            chkChannelY.IsChecked = selected.Contains(Core.ExportChannel.Y);
            chkChannelZ.IsChecked = selected.Contains(Core.ExportChannel.Z);
            chkChannelCieX.IsChecked = selected.Contains(Core.ExportChannel.CieX);
            chkChannelCieY.IsChecked = selected.Contains(Core.ExportChannel.CieY);
            chkChannelCieU.IsChecked = selected.Contains(Core.ExportChannel.CieU);
            chkChannelCieV.IsChecked = selected.Contains(Core.ExportChannel.CieV);
            chkChannelColorDifference.IsChecked = selected.Contains(Core.ExportChannel.ColorDifference);
            chkChannelContrast.IsChecked = selected.Contains(Core.ExportChannel.Contrast);
            UpdateExportUiState();
        }

        private void UpdateExportUiState()
        {
            if (panelAzimuthSettings == null || panelPolarSettings == null || panelCrossSection == null || btnExport == null)
            {
                return;
            }

            bool exportAzimuth = chkExportAzimuth.IsChecked == true;
            bool exportPolar = chkExportPolar.IsChecked == true;
            bool crossSectionEnabled = chkEnableCrossSection.IsChecked == true;
            bool azimuthCrossSection = rbCrossSectionAzimuth.IsChecked == true;

            panelAzimuthSettings.IsEnabled = exportAzimuth || (crossSectionEnabled && azimuthCrossSection);
            panelPolarSettings.IsEnabled = exportPolar || (crossSectionEnabled && !azimuthCrossSection);
            panelCrossSection.IsEnabled = crossSectionEnabled;
            txtCrossSectionAzimuthAngle.IsEnabled = crossSectionEnabled && azimuthCrossSection;
            txtCrossSectionPolarAngle.IsEnabled = crossSectionEnabled && !azimuthCrossSection;

            List<Core.ExportChannel> channels = CollectSelectedChannels();
            int modeCount = (exportAzimuth ? 1 : 0) + (exportPolar ? 1 : 0);
            btnExport.IsEnabled = channels.Count > 0 && modeCount > 0;

            if (!btnExport.IsEnabled)
            {
                tbExportSummary.Text = UiText("Ui_ExportNoSelectionSummary", "请选择至少一个通道和一种导出模式。");
                return;
            }

            int fileCount = channels.Count * (modeCount + (crossSectionEnabled ? 1 : 0));
            string prefix = string.IsNullOrWhiteSpace(txtFilePrefix?.Text) ? "Conoscope_Export" : txtFilePrefix.Text.Trim();
            string modeName = exportAzimuth ? "Azimuth" : "Polar";
            string channelName = channels[0].ToString();
            string example = $"{prefix}_{modeName}_{channelName}_{DateTime.Now:yyyyMMdd_HHmmss}.csv";
            tbExportSummary.Text = Core.CompositeFormatCache.Format(
                UiText("Ui_ExportSummaryFormat", "预计生成 {0} 个 CSV 文件；示例：{1}"),
                fileCount,
                example);
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (!ValidateInputs())
                {
                    return;
                }

                List<Core.ExportChannel> channels = CollectSelectedChannels();

                if (channels.Count == 0)
                {
                    MessageBox.Show(Properties.Resources.MsgSelectOneChannel, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool exportAzimuth = chkExportAzimuth.IsChecked == true;
                bool exportPolar = chkExportPolar.IsChecked == true;

                if (!exportAzimuth && !exportPolar)
                {
                    MessageBox.Show(Properties.Resources.MsgSelectOneExportMode, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                bool enableCrossSection = chkEnableCrossSection.IsChecked == true;
                CrossSectionType crossSectionType = rbCrossSectionAzimuth.IsChecked == true
                    ? CrossSectionType.Azimuth
                    : CrossSectionType.Polar;
                double azimuthCrossSectionAngle = ParseDoubleOrDefault(txtCrossSectionAzimuthAngle.Text, Settings.CrossSectionAzimuthAngle);
                double polarCrossSectionAngle = ParseDoubleOrDefault(txtCrossSectionPolarAngle.Text, Settings.CrossSectionPolarAngle);

                Settings = new AdvancedExportSettings
                {
                    FilePrefix = txtFilePrefix.Text.Trim(),
                    Channels = channels,
                    ExportAzimuth = exportAzimuth,
                    ExportPolar = exportPolar,
                    AzimuthStep = ParseDoubleOrDefault(txtAzimuthStep.Text, Settings.AzimuthStep),
                    RadialStep = ParseDoubleOrDefault(txtRadialStep.Text, Settings.RadialStep),
                    PolarStep = ParseDoubleOrDefault(txtPolarStep.Text, Settings.PolarStep),
                    CircumferentialStep = ParseDoubleOrDefault(txtCircumferentialStep.Text, Settings.CircumferentialStep),
                    DecimalPlaces = int.Parse(txtDecimalPlaces.Text, CultureInfo.InvariantCulture),
                    EnableCrossSection = enableCrossSection,
                    CrossSectionType = crossSectionType,
                    CrossSectionAzimuthAngle = azimuthCrossSectionAngle,
                    CrossSectionPolarAngle = polarCrossSectionAngle
                };

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(Conoscope.Core.CompositeFormatCache.Format(Properties.Resources.MsgSettingsError, ex.Message), Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private bool ValidateInputs()
        {
            if (string.IsNullOrWhiteSpace(txtFilePrefix.Text))
            {
                MessageBox.Show(Properties.Resources.MsgEnterFilePrefix, Properties.Resources.TitleHint, MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            bool crossSectionEnabled = chkEnableCrossSection.IsChecked == true;
            bool azimuthCrossSection = rbCrossSectionAzimuth.IsChecked == true;
            bool needsAzimuthSettings = chkExportAzimuth.IsChecked == true || (crossSectionEnabled && azimuthCrossSection);
            bool needsPolarSettings = chkExportPolar.IsChecked == true || (crossSectionEnabled && !azimuthCrossSection);

            if (needsAzimuthSettings && (!TryParseDouble(txtAzimuthStep.Text, out double azimuthStep) || azimuthStep < 0.01 || azimuthStep > 180))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidAzimuthStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (needsAzimuthSettings && (!TryParseDouble(txtRadialStep.Text, out double radialStep) || radialStep < 0.01 || radialStep > 80))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidRadialStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (needsPolarSettings && (!TryParseDouble(txtPolarStep.Text, out double polarStep) || polarStep < 0.01 || polarStep > 80))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidRingStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (needsPolarSettings && (!TryParseDouble(txtCircumferentialStep.Text, out double circumStep) || circumStep < 0.01 || circumStep > 360))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidCircularStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!int.TryParse(txtDecimalPlaces.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decimalPlaces)
                || decimalPlaces < 0
                || decimalPlaces > 8)
            {
                MessageBox.Show(Properties.Resources.MsgInvalidDecimalPlaces, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (chkEnableCrossSection.IsChecked == true)
            {
                if (rbCrossSectionAzimuth.IsChecked == true)
                {
                    if (!TryParseDouble(txtCrossSectionAzimuthAngle.Text, out double angle) || angle < 0 || angle > 180)
                    {
                        MessageBox.Show(Properties.Resources.MsgInvalidAzimuthSection, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
                else
                {
                    if (!TryParseDouble(txtCrossSectionPolarAngle.Text, out double angle) || angle < 0 || angle > 80)
                    {
                        MessageBox.Show(Properties.Resources.MsgInvalidPolarSection, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                        return false;
                    }
                }
            }

            return true;
        }

        private void ApplySettings(AdvancedExportSettings settings)
        {
            txtFilePrefix.Text = settings.FilePrefix;
            txtDecimalPlaces.Text = settings.DecimalPlaces.ToString(CultureInfo.InvariantCulture);
            txtAzimuthStep.Text = settings.AzimuthStep.ToString(CultureInfo.InvariantCulture);
            txtRadialStep.Text = settings.RadialStep.ToString(CultureInfo.InvariantCulture);
            txtPolarStep.Text = settings.PolarStep.ToString(CultureInfo.InvariantCulture);
            txtCircumferentialStep.Text = settings.CircumferentialStep.ToString(CultureInfo.InvariantCulture);
            txtCrossSectionAzimuthAngle.Text = settings.CrossSectionAzimuthAngle.ToString(CultureInfo.InvariantCulture);
            txtCrossSectionPolarAngle.Text = settings.CrossSectionPolarAngle.ToString(CultureInfo.InvariantCulture);

            chkChannelX.IsChecked = settings.Channels.Contains(Core.ExportChannel.X);
            chkChannelY.IsChecked = settings.Channels.Contains(Core.ExportChannel.Y);
            chkChannelZ.IsChecked = settings.Channels.Contains(Core.ExportChannel.Z);
            chkChannelCieX.IsChecked = settings.Channels.Contains(Core.ExportChannel.CieX);
            chkChannelCieY.IsChecked = settings.Channels.Contains(Core.ExportChannel.CieY);
            chkChannelCieU.IsChecked = settings.Channels.Contains(Core.ExportChannel.CieU);
            chkChannelCieV.IsChecked = settings.Channels.Contains(Core.ExportChannel.CieV);
            chkChannelColorDifference.IsChecked = settings.Channels.Contains(Core.ExportChannel.ColorDifference);
            chkChannelContrast.IsChecked = settings.Channels.Contains(Core.ExportChannel.Contrast);

            chkExportAzimuth.IsChecked = settings.ExportAzimuth;
            chkExportPolar.IsChecked = settings.ExportPolar;
            chkEnableCrossSection.IsChecked = settings.EnableCrossSection;
            rbCrossSectionAzimuth.IsChecked = settings.CrossSectionType == CrossSectionType.Azimuth;
            rbCrossSectionPolar.IsChecked = settings.CrossSectionType == CrossSectionType.Polar;
            UpdateExportUiState();
        }

        private static AdvancedExportSettings NormalizeSettings(AdvancedExportSettings? settings, int defaultDecimalPlaces)
        {
            List<Core.ExportChannel> channels = settings?.Channels is { Count: > 0 }
                ? new List<Core.ExportChannel>(settings.Channels)
                : new List<Core.ExportChannel> { Core.ExportChannel.Y };

            bool exportAzimuth = settings?.ExportAzimuth ?? true;
            bool exportPolar = settings?.ExportPolar ?? false;
            CrossSectionType crossSectionType = settings?.CrossSectionType ?? CrossSectionType.Azimuth;
            double azimuthCrossSectionAngle = NormalizeValue(settings?.CrossSectionAzimuthAngle ?? 0, 0, 180, 0);
            double polarCrossSectionAngle = NormalizeValue(settings?.CrossSectionPolarAngle ?? 45, 0, 80, 45);

            return new AdvancedExportSettings
            {
                FilePrefix = string.IsNullOrWhiteSpace(settings?.FilePrefix) ? "Conoscope_Export" : settings.FilePrefix.Trim(),
                Channels = channels,
                ExportAzimuth = exportAzimuth,
                ExportPolar = exportPolar,
                AzimuthStep = NormalizeValue(settings?.AzimuthStep ?? 1, 0.01, 180, 1),
                RadialStep = NormalizeValue(settings?.RadialStep ?? 1, 0.01, 80, 1),
                PolarStep = NormalizeValue(settings?.PolarStep ?? 1, 0.01, 80, 1),
                CircumferentialStep = NormalizeValue(settings?.CircumferentialStep ?? 1, 0.01, 360, 1),
                DecimalPlaces = Math.Clamp(settings?.DecimalPlaces ?? defaultDecimalPlaces, 0, 8),
                EnableCrossSection = settings?.EnableCrossSection ?? false,
                CrossSectionType = crossSectionType,
                CrossSectionAzimuthAngle = azimuthCrossSectionAngle,
                CrossSectionPolarAngle = polarCrossSectionAngle
            };
        }

        private List<Core.ExportChannel> CollectSelectedChannels()
        {
            List<Core.ExportChannel> channels = new List<Core.ExportChannel>();
            if (chkChannelX.IsChecked == true) channels.Add(Core.ExportChannel.X);
            if (chkChannelY.IsChecked == true) channels.Add(Core.ExportChannel.Y);
            if (chkChannelZ.IsChecked == true) channels.Add(Core.ExportChannel.Z);
            if (chkChannelCieX.IsChecked == true) channels.Add(Core.ExportChannel.CieX);
            if (chkChannelCieY.IsChecked == true) channels.Add(Core.ExportChannel.CieY);
            if (chkChannelCieU.IsChecked == true) channels.Add(Core.ExportChannel.CieU);
            if (chkChannelCieV.IsChecked == true) channels.Add(Core.ExportChannel.CieV);
            if (chkChannelColorDifference.IsChecked == true) channels.Add(Core.ExportChannel.ColorDifference);
            if (chkChannelContrast.IsChecked == true) channels.Add(Core.ExportChannel.Contrast);
            return channels;
        }

        private static bool TryParseDouble(string? text, out double value)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
                || double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value);
        }

        private static double ParseDoubleOrDefault(string? text, double fallback)
        {
            if (TryParseDouble(text, out double value))
            {
                return value;
            }

            return fallback;
        }

        private static double NormalizeValue(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(min, Math.Min(value, max));
        }

        private static string UiText(string key, string fallback)
        {
            return Properties.Resources.ResourceManager.GetString(key, CultureInfo.CurrentUICulture) ?? fallback;
        }

    }
}
