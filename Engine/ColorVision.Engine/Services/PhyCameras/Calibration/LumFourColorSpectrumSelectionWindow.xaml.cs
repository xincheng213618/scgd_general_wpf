using ColorVision.Engine.Services.Devices.Spectrum;
using ColorVision.Themes;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Engine.Services.PhyCameras.Calibration
{
    public partial class LumFourColorSpectrumSelectionWindow : Window
    {
        public SpectrumColorMeasurementSummary? SelectedResult => ResultsGrid.SelectedItem as SpectrumColorMeasurementSummary;

        public LumFourColorSpectrumSelectionWindow(string deviceName, string target, IReadOnlyList<SpectrumColorMeasurementSummary> results)
        {
            InitializeComponent();
            this.ApplyCaption();
            TargetText.Text = $"{deviceName} → {target} 光谱参考";
            ResultsGrid.ItemsSource = results;
        }

        private void ResultsGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) => UseButton.IsEnabled = SelectedResult != null;
        private void Use_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedResult != null) DialogResult = true;
        }
    }
}
