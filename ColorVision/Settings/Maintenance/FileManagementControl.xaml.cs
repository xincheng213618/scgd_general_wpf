using ColorVision.Common.Utilities;
using ColorVision.UI.Maintenance;
using ColorVision.Themes;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Settings.Maintenance;

public partial class FileManagementControl : UserControl
{
    public StorageMaintenanceViewModel ViewModel { get; }

    public FileManagementControl(StorageMaintenanceViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        DataContext = viewModel;
    }

    private async void Scan_Click(object sender, RoutedEventArgs e) => await ViewModel.ScanAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.Cancel();
    private void OpenLocation_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button button || button.DataContext is not StorageCleanupItem item) return;
        string[] directories = item.SourceDirectories.Where(Directory.Exists).ToArray();
        if (directories.Length == 1) { OpenLocation(directories[0]); return; }
        if (directories.Length == 0) return;
        var menu = new ContextMenu { PlacementTarget = button };
        foreach (string directory in directories)
        {
            var entry = new MenuItem { Header = directory };
            entry.Click += (_, _) => OpenLocation(directory);
            menu.Items.Add(entry);
        }
        menu.IsOpen = true;
    }

    private void OpenLocation(string path)
    {
        try { PlatformHelper.OpenFolder(path); }
        catch (Exception ex) { ViewModel.Status = string.Format(MaintenanceText.Failed, ex.Message); }
    }
    private async void CleanSelected_Click(object sender, RoutedEventArgs e) => await ConfirmAndClean(ViewModel.Items.Where(item => item.IsSelected && item.CanClean).ToArray());
    private async void CleanItem_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is StorageCleanupItem item && item.CanClean)
            await ConfirmAndClean(new[] { item });
    }

    private async System.Threading.Tasks.Task ConfirmAndClean(IReadOnlyList<StorageCleanupItem> items)
    {
        if (ViewModel.IsBusy || items.Count == 0) return;
        string message = string.Format(MaintenanceText.ConfirmClean, items.Sum(item => item.Count), StorageCleanupItem.FormatBytes(items.Sum(item => item.Bytes)));
        if (MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.FileManagement, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await ViewModel.CleanupAsync(items);
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StorageCleanupItem item) return;
        if (item.Id == "thumbnails") { ShowMessage(MaintenanceText.ThumbnailList); return; }
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, ItemsSource = item.Files, Margin = new Thickness(12), EnableRowVirtualization = true };
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.FilePath, Binding = new Binding("FullPath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.Size + " (B)", Binding = new Binding("Length"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.Modified + " (UTC)", Binding = new Binding("LastWriteTimeUtc") { StringFormat = "yyyy-MM-dd HH:mm:ss" }, Width = 160 });
        var openFile = new MenuItem { Header = MaintenanceText.OpenFileLocation };
        openFile.Click += (_, _) =>
        {
            if (grid.SelectedItem is not FileMaintenanceEntry entry || !File.Exists(entry.FullPath)) return;
            try { PlatformHelper.OpenFolderAndSelectFile(entry.FullPath); }
            catch (Exception ex) { ShowMessage(string.Format(MaintenanceText.Failed, ex.Message)); }
        };
        grid.ContextMenu = new ContextMenu { Items = { openFile } };
        grid.ContextMenu.Opened += (_, _) => openFile.IsEnabled = grid.SelectedItem is FileMaintenanceEntry entry && File.Exists(entry.FullPath);
        ShowDetails(item.Title, grid);
    }

    private void Warnings_Click(object sender, RoutedEventArgs e) => ShowDetails(MaintenanceText.Warnings,
        new TextBox { Text = string.Join(Environment.NewLine, ViewModel.Issues), IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, Margin = new Thickness(14) });

    private void ShowDetails(string title, UIElement content)
    {
        var window = new Window { Title = title, Width = 900, Height = 520, MinWidth = 650, MinHeight = 320, Content = content, Owner = Window.GetWindow(this), WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.SetResourceReference(Window.BackgroundProperty, "GlobalBackground");
        window.SetResourceReference(Window.ForegroundProperty, "GlobalTextBrush");
        window.ApplyCaption();
        window.ShowDialog();
    }

    private void ShowMessage(string message) => MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.FileManagement, MessageBoxButton.OK, MessageBoxImage.Information);
}

public sealed class RetentionLabelConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is 0 ? MaintenanceText.ClearAll : System.Convert.ToString(value, culture) ?? string.Empty;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => Binding.DoNothing;
}
