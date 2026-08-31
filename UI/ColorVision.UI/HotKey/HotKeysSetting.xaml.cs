using System.Windows;
using System.Windows.Controls;

namespace ColorVision.UI.HotKey;

public partial class HotKeysSetting : UserControl
{
    public HotKeysSetting() : this(new HotkeySettingsViewModel()) { }

    public HotKeysSetting(HotkeySettingsViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;
        Loaded += (_, _) => ViewModel.TryRefresh();
    }

    public HotkeySettingsViewModel ViewModel { get; }

    private void Edit_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: HotkeySettingsBindingRow binding }) return;
        HotkeyEditWindow dialog = new(ViewModel, binding.Owner, binding.Index) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void Add_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: HotkeySettingsRow row }) return;
        new HotkeyEditWindow(ViewModel, row, null) { Owner = Window.GetWindow(this) }.ShowDialog();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: HotkeySettingsBindingRow binding }) ViewModel.RemoveBinding(binding);
    }

    private void ClearSearch_Click(object sender, RoutedEventArgs e) { ViewModel.Search = string.Empty; SearchBox.Focus(); }
    private void ClearFilters_Click(object sender, RoutedEventArgs e) => ViewModel.ClearFilters();

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: HotkeySettingsRow row }) ViewModel.Reset(row);
    }

    private void ResetAll_Click(object sender, RoutedEventArgs e)
    {
        if (MessageBox.Show(Window.GetWindow(this), HotkeyEditorText.ResetConfirm, HotkeyEditorText.ResetTitle,
            MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK) ViewModel.ResetAll();
    }
}
