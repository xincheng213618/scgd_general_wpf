using Conoscope.Help;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopeWindow
    {
        private void btnOpenConoscopeHelp_Click(object sender, RoutedEventArgs e)
        {
            OpenHelpCenter();
        }

        private void OpenHelpCenter()
        {
            ConoscopeHelpWindow.ShowWindow(this);
        }
    }
}