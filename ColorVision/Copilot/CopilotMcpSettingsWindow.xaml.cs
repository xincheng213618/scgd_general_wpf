using ColorVision.Themes;
using System;
using System.Windows;

namespace ColorVision.Copilot
{
    public partial class CopilotMcpSettingsWindow : Window
    {
        public CopilotMcpSettingsWindow(CopilotSettingsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            this.ApplyCaption();
            DataContext = viewModel;
        }

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        public bool HasAppliedChanges => ViewModel.HasAppliedChanges;

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

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
