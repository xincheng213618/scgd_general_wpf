using System.Globalization;
using System.Windows;

namespace Conoscope.MVS
{
    public partial class MVSGratingSettingsWindow : Window
    {
        private readonly MVSViewManager viewManager;

        public MVSGratingSettingsWindow(MVSViewManager viewManager)
        {
            InitializeComponent();
            this.viewManager = viewManager;
            DataContext = viewManager;
        }

        private void AddGratingDiameter_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseDoubleText(tbNewGratingDiameter.Text, out double diameterMillimeters) || diameterMillimeters <= 0)
            {
                MessageBox.Show(Properties.Resources.TestAreaSizeMustBePositive, Properties.Resources.ObservationCameraSettings, MessageBoxButton.OK, MessageBoxImage.Warning);
                tbNewGratingDiameter.Focus();
                tbNewGratingDiameter.SelectAll();
                return;
            }

            viewManager.Config.TryAddGratingDiameter(diameterMillimeters);
            tbNewGratingDiameter.Clear();
        }

        private void RemoveGratingDiameter_Click(object sender, RoutedEventArgs e)
        {
            if (GratingDiameterList.SelectedItem is not double diameterMillimeters)
            {
                return;
            }

            viewManager.Config.RemoveGratingDiameter(diameterMillimeters);
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static bool TryParseDoubleText(string? text, out double value)
        {
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out value))
            {
                return true;
            }

            return double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}