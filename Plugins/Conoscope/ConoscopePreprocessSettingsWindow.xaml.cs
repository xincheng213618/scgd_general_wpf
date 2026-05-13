using Conoscope.Core;
using System.Windows;

namespace Conoscope
{
    public partial class ConoscopePreprocessSettingsWindow : Window
    {
        public ConoscopePreprocessSettingsWindow(ConoscopeConfig config)
        {
            InitializeComponent();
            PreprocessSettingsHost.Content = new ConoscopePreprocessSettingsControl(config, persistChanges: true);
        }

        private void btnClose_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            ConoscopeModuleService.RefreshAllConoscopeConfiguration();
        }
    }
}