using System;
using System.Windows;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotAddModelWindow : Window
    {
        private bool _committed;

        public CopilotAddModelWindow(CopilotSettingsViewModel viewModel)
        {
            ArgumentNullException.ThrowIfNull(viewModel);

            InitializeComponent();
            DataContext = viewModel;
            Loaded += CopilotAddModelWindow_Loaded;
            Closed += CopilotAddModelWindow_Closed;
        }

        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;

        private void CopilotAddModelWindow_Loaded(object sender, RoutedEventArgs e)
        {
            SearchTextBox.Focus();
        }

        private void WindowChrome_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState != MouseButtonState.Pressed)
                return;

            try
            {
                DragMove();
            }
            catch (InvalidOperationException)
            {
            }
        }

        private void ConnectPageGrid_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (ConnectPageGrid.IsVisible)
                ApiKeyPasswordBox.Focus();
        }

        private void SubmitButton_Click(object sender, RoutedEventArgs e)
        {
            if (ViewModel.AddQuickProfile(useNow: true))
            {
                _committed = true;
                DialogResult = true;
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearQuickAddModelDraft();
            DialogResult = false;
        }

        private void CopilotAddModelWindow_Closed(object? sender, EventArgs e)
        {
            if (!_committed)
                ViewModel.ClearQuickAddModelDraft();
        }
    }
}
