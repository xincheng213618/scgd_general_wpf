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
            if (!double.TryParse(txtStepDegrees.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out double stepDegrees)
                || stepDegrees < 0.01
                || stepDegrees > 360)
            {
                MessageBox.Show(Properties.Resources.MsgInvalidSamplingInterval, Properties.Resources.TitleCurrentCurveExport, MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!int.TryParse(txtDecimalPlaces.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int decimalPlaces)
                || decimalPlaces < 0
                || decimalPlaces > 8)
            {
                MessageBox.Show(Properties.Resources.MsgInvalidDecimalPlaces, Properties.Resources.TitleCurrentCurveExport, MessageBoxButton.OK, MessageBoxImage.Warning);
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
    }
}