using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;

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
            Settings = NormalizeSettings(initialSettings, defaultDecimalPlaces);
            ApplySettings(Settings);
        }

        private void chkEnableCrossSection_Changed(object sender, RoutedEventArgs e)
        {
            if (panelCrossSection != null)
            {
                panelCrossSection.IsEnabled = chkEnableCrossSection.IsChecked == true;
            }
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
                double azimuthCrossSectionAngle = ParseDouble(txtCrossSectionAzimuthAngle.Text);
                double polarCrossSectionAngle = ParseDouble(txtCrossSectionPolarAngle.Text);

                Settings = new AdvancedExportSettings
                {
                    FilePrefix = txtFilePrefix.Text.Trim(),
                    Channels = channels,
                    ExportAzimuth = exportAzimuth,
                    ExportPolar = exportPolar,
                    AzimuthStep = ParseDouble(txtAzimuthStep.Text),
                    RadialStep = ParseDouble(txtRadialStep.Text),
                    PolarStep = ParseDouble(txtPolarStep.Text),
                    CircumferentialStep = ParseDouble(txtCircumferentialStep.Text),
                    DecimalPlaces = int.Parse(txtDecimalPlaces.Text, CultureInfo.InvariantCulture),
                    EnableCrossSection = enableCrossSection,
                    CrossSectionType = crossSectionType,
                    CrossSectionAzimuthAngle = azimuthCrossSectionAngle,
                    CrossSectionPolarAngle = polarCrossSectionAngle,
                    CrossSectionAngle = crossSectionType == CrossSectionType.Azimuth
                        ? azimuthCrossSectionAngle
                        : polarCrossSectionAngle
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

            if (!TryParseDouble(txtAzimuthStep.Text, out double azimuthStep) || azimuthStep < 0.01 || azimuthStep > 180)
            {
                MessageBox.Show(Properties.Resources.MsgInvalidAzimuthStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TryParseDouble(txtRadialStep.Text, out double radialStep) || radialStep < 0.01 || radialStep > 80)
            {
                MessageBox.Show(Properties.Resources.MsgInvalidRadialStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TryParseDouble(txtPolarStep.Text, out double polarStep) || polarStep < 0.01 || polarStep > 80)
            {
                MessageBox.Show(Properties.Resources.MsgInvalidRingStep, Properties.Resources.TitleError, MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }

            if (!TryParseDouble(txtCircumferentialStep.Text, out double circumStep) || circumStep < 0.01 || circumStep > 360)
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
            panelCrossSection.IsEnabled = settings.EnableCrossSection;
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
                CrossSectionPolarAngle = polarCrossSectionAngle,
                CrossSectionAngle = crossSectionType == CrossSectionType.Azimuth
                    ? azimuthCrossSectionAngle
                    : polarCrossSectionAngle
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

        private static double ParseDouble(string? text)
        {
            if (TryParseDouble(text, out double value))
            {
                return value;
            }

            throw new FormatException("Invalid numeric input.");
        }

        private static double NormalizeValue(double value, double min, double max, double fallback)
        {
            if (double.IsNaN(value) || double.IsInfinity(value))
            {
                return fallback;
            }

            return Math.Max(min, Math.Min(value, max));
        }

    }

    /// <summary>
    /// 高级导出设置
    /// </summary>
    public class AdvancedExportSettings
    {
        public string FilePrefix { get; set; } = "Conoscope_Export";
        public List<Core.ExportChannel> Channels { get; set; } = new List<Core.ExportChannel>();
        public bool ExportAzimuth { get; set; } = true;
        public bool ExportPolar { get; set; }
        public double AzimuthStep { get; set; } = 1;
        public double RadialStep { get; set; } = 1;
        public double PolarStep { get; set; } = 1;
        public double CircumferentialStep { get; set; } = 1;
        public int DecimalPlaces { get; set; } = 4;
        public bool EnableCrossSection { get; set; }
        public CrossSectionType CrossSectionType { get; set; } = CrossSectionType.Azimuth;
        public double CrossSectionAzimuthAngle { get; set; }
        public double CrossSectionPolarAngle { get; set; } = 45;
        public double CrossSectionAngle { get; set; }
    }

    /// <summary>
    /// 截面类型
    /// </summary>
    public enum CrossSectionType
    {
        Azimuth,  // 方位角截面
        Polar     // 极角截面
    }
}
