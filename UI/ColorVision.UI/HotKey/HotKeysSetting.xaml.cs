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
        if (sender is not Button { DataContext: HotkeySettingsRow row }) return;
        HotkeyEditWindow dialog = new(ViewModel, row) { Owner = Window.GetWindow(this) };
        dialog.ShowDialog();
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: HotkeySettingsRow row }) ViewModel.Clear(row);
    }

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
