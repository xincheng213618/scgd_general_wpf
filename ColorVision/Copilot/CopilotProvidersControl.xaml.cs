using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;

namespace ColorVision.Copilot
{
    public partial class CopilotProvidersControl : UserControl
    {
        public CopilotProvidersControl() => InitializeComponent();
        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;
        private void AddProvider_Click(object sender, RoutedEventArgs e) { ViewModel.ConnectProviderSearchText = string.Empty; ViewModel.IsAddingModel = true; }
        private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.IsAddingModel = false;
        private void ChooseProvider_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { Tag: CopilotConnectProviderOption option }) ViewModel.AddProvider(option);
        }
        private void ProviderMenu_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button { ContextMenu: { } menu } button) { menu.PlacementTarget = button; menu.Placement = PlacementMode.Bottom; menu.IsOpen = true; }
        }
        private void RenameProvider_Click(object sender, RoutedEventArgs e) { RenamePanel.Visibility = Visibility.Visible; ProviderNameBox.Focus(); ProviderNameBox.SelectAll(); }
        private void DeleteProvider_Click(object sender, RoutedEventArgs e) => ViewModel.DeleteProvider();
        private void AddModel_Click(object sender, RoutedEventArgs e) => EditModel(null);
        private void EditModel_Click(object sender, RoutedEventArgs e) { if (sender is Button { Tag: CopilotProfileConfig model }) EditModel(model); }
        private void EditModel(CopilotProfileConfig? model)
        {
            if (ViewModel.SelectedProvider == null) return;
            new CopilotModelEditorWindow(ViewModel, model) { Owner = Window.GetWindow(this) }.ShowDialog();
        }
        private void TestModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is not Button { Tag: CopilotProfileConfig model }) return;
            ViewModel.SelectedProfile = model;
            if (ViewModel.TestSelectedProfileCommand.CanExecute(null)) ViewModel.TestSelectedProfileCommand.Execute(null);
        }
        private void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: CopilotProfileConfig model }) ViewModel.DeleteModel(model);
        }
    }
}
