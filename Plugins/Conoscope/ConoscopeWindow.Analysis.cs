using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private void btnOpenActiveView3D_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.Open3DForCurrentView();
        }

        private void btnOpenActiveViewCie_Click(object sender, RoutedEventArgs e)
        {
            ActiveView?.OpenCieForCurrentView();
        }
    }
}
