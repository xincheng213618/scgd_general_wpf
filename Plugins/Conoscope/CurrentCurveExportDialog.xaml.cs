using System;
using System.Globalization;
using System.Windows;

namespace Conoscope
{
    public partial class CurrentCurveExportDialog : Window
    {
        public CurrentCurveExportSettings Settings { get; private set; } = new CurrentCurveExportSettings();

        public CurrentCurveExportDialog()
        {
            InitializeComponent();
            txtStepDegrees.Text = Settings.StepDegrees.ToString("F2", CultureInfo.InvariantCulture);
            chkIncludeMetadata.IsChecked = Settings.IncludeMetadata;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseStepDegrees(txtStepDegrees.Text, out double stepDegrees))
            {
                MessageBox.Show("采样间隔必须是 0.01 到 360 之间的数值", "当前曲线导出", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Settings = new CurrentCurveExportSettings
            {
                StepDegrees = stepDegrees,
                IncludeMetadata = chkIncludeMetadata.IsChecked == true
            };

            DialogResult = true;
            Close();
        }

        private static bool TryParseStepDegrees(string? text, out double stepDegrees)
        {
            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out stepDegrees)
                && stepDegrees >= 0.01
                && stepDegrees <= 360;
        }
    }

    public sealed class CurrentCurveExportSettings
    {
        public double StepDegrees { get; init; } = 0.01;
        public bool IncludeMetadata { get; init; } = true;
    }
}