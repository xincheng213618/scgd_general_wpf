using Conoscope.Analysis;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private void btnOpenContrastTest_Click(object sender, RoutedEventArgs e)
        {
            new ContrastTestWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private void btnOpenColorGamut_Click(object sender, RoutedEventArgs e)
        {
            new ColorGamutWindow
            {
                Owner = this,
                WindowStartupLocation = WindowStartupLocation.CenterOwner
            }.Show();
        }

        private void btnOpenActiveView3D_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.Open3DForCurrentView();
        }

        private void btnOpenActiveViewCie_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.OpenCieForCurrentView();
        }

        private void btnExportAngleMode_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportAngleMode();
        }

        private void btnExportCircleMode_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.ExportCircleMode();
        }

        private void btnAdvancedExport_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.AdvancedExport();
        }
    }
}
