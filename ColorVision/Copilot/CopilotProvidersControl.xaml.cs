using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;

namespace ColorVision.Copilot
{
    public partial class CopilotProvidersControl : UserControl
    {
        public CopilotProvidersControl() => InitializeComponent();
        private CopilotSettingsViewModel ViewModel => (CopilotSettingsViewModel)DataContext;
        private void AddProvider_Click(object sender, RoutedEventArgs e) { ViewModel.ConnectProviderSearchText = string.Empty; ViewModel.IsAddingModel = true; }
        private void Back_Click(object sender, RoutedEventArgs e) => ViewModel.IsAddingModel = false;
        private void ProviderList_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBox list && e.OriginalSource is DependencyObject source
                && ItemsControl.ContainerFromElement(list, source) is ListBoxItem)
            {
                ViewModel.IsAddingModel = false;
                RenamePanel.Visibility = Visibility.Collapsed;
            }
        }
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
            ViewModel.RequestProfileConnectionTest(model);
        }
        private void DeleteModel_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { Tag: CopilotProfileConfig model }) ViewModel.DeleteModel(model);
        }
        private void ModelMenu_Opened(object sender, RoutedEventArgs e)
        {
            if (sender is not ContextMenu menu || menu.Items[0] is not MenuItem item) return;
            item.IsEnabled = ViewModel.SelectedProvider?.Models.Count > 1;
            item.ToolTip = item.IsEnabled ? DisplayText.Get("删除此模型") : DisplayText.Get("供应商至少保留一个模型；可从供应商菜单删除整个连接。");
        }
    }
}
