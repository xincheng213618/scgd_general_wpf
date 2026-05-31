using ColorVision.Themes;
using System.Windows;

namespace ColorVision.Copilot
{
    public partial class CopilotSettingsWindow : Window
    {
        public CopilotSettingsWindow()
        {
            InitializeComponent();
            this.ApplyCaption();
            DataContext = new CopilotSettingsViewModel();
        }

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        private void ApplyButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.Save();
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ViewModel.Save())
                return;

            DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}