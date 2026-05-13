using System;
using System.Globalization;
using System.Windows;
using Conoscope.Core;

namespace Conoscope
{
    public partial class CurrentCurveExportDialog : Window
    {
        public ConoscopeCrossSectionExportOptions ExportOptions { get; private set; }
            = new ConoscopeCrossSectionExportOptions { StepDegrees = 1.0, IncludeMetadata = true };

        public CurrentCurveExportDialog(ConoscopeCrossSectionExportOptions? exportOptions = null)
        {
            InitializeComponent();
            ExportOptions = exportOptions ?? ExportOptions;
            txtStepDegrees.Text = ExportOptions.StepDegrees.ToString(CultureInfo.InvariantCulture);
            chkIncludeMetadata.IsChecked = ExportOptions.IncludeMetadata;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseStepDegrees(txtStepDegrees.Text, out double stepDegrees))
            {
                MessageBox.Show("采样间隔必须是 0.01 到 360 之间的数值", "当前曲线导出", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExportOptions = new ConoscopeCrossSectionExportOptions
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
}