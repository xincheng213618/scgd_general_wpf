using System;
using System.Globalization;
using System.Windows;
using Conoscope.Core;

namespace Conoscope
{
    public partial class CurrentCurveExportDialog : Window
    {
        public ConoscopeCrossSectionExportOptions ExportOptions { get; private set; }
            = new ConoscopeCrossSectionExportOptions { StepDegrees = 1.0, IncludeMetadata = true, DecimalPlaces = 4 };

        public CurrentCurveExportDialog(ConoscopeCrossSectionExportOptions? exportOptions = null)
        {
            InitializeComponent();
            ExportOptions = exportOptions ?? ExportOptions;
            txtStepDegrees.Text = ExportOptions.StepDegrees.ToString(CultureInfo.InvariantCulture);
            txtDecimalPlaces.Text = ExportOptions.DecimalPlaces.ToString(CultureInfo.InvariantCulture);
            chkIncludeMetadata.IsChecked = ExportOptions.IncludeMetadata;
        }

        private void btnExport_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseStepDegrees(txtStepDegrees.Text, out double stepDegrees))
            {
                MessageBox.Show(Properties.Resources.MsgInvalidSamplingInterval, Properties.Resources.TitleCurrentCurveExport, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!TryParseDecimalPlaces(txtDecimalPlaces.Text, out int decimalPlaces))
            {
                MessageBox.Show("小数位数必须是 0-8 之间的整数。", Properties.Resources.TitleCurrentCurveExport, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            ExportOptions = new ConoscopeCrossSectionExportOptions
            {
                StepDegrees = stepDegrees,
                IncludeMetadata = chkIncludeMetadata.IsChecked == true,
                DecimalPlaces = decimalPlaces
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

        private static bool TryParseDecimalPlaces(string? text, out int decimalPlaces)
        {
            return int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out decimalPlaces)
                && decimalPlaces >= 0
                && decimalPlaces <= 8;
        }
    }
}