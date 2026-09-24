using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Copilot
{
    public partial class CopilotAddModelControl : UserControl
    {
        public CopilotAddModelControl() => InitializeComponent();

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.CancelAddModel();

        private void Add_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.AddQuickProfile(useNow: false))
                ViewModel.CancelAddModel();
        }

        private void UseLocalCodex_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.AddLocalCodexCommand.CanExecute(null))
                return;
            ViewModel.CancelAddModel();
        }
    }
}
