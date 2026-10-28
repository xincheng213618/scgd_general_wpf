using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Maintenance;
using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;

namespace ColorVision.Settings.Maintenance;

public partial class FileManagementWindow : Window
{
    private static FileManagementWindow? _instance;
    private static StorageMaintenanceViewModel? _sessionModel;
    public StorageMaintenanceViewModel ViewModel { get; }
    public Task ScanTask { get; private set; } = Task.CompletedTask;

    public FileManagementWindow() : this(_sessionModel ??= new StorageMaintenanceViewModel(
        ConfigService.Instance.GetRequiredService<StorageMaintenanceConfig>(),
        AssemblyHandler.GetInstance().LoadImplementations<IFileMaintenanceProvider>())) { }

    public FileManagementWindow(StorageMaintenanceViewModel viewModel)
    {
        ViewModel = viewModel;
        InitializeComponent();
        FileContent.Content = new FileManagementControl(viewModel);
        this.ApplyCaption();
    }

    public static void OpenWindow()
    {
        if (!Application.Current.Dispatcher.CheckAccess()) { Application.Current.Dispatcher.Invoke(OpenWindow); return; }
        if (_instance == null)
        {
            _instance = new FileManagementWindow
            {
                Owner = Application.Current.MainWindow,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
            };
            _instance.Show();
        }
        else
        {
            if (_instance.WindowState == WindowState.Minimized) _instance.WindowState = WindowState.Normal;
            _instance.ScanTask = _instance.ViewModel.ScanAsync();
        }
        _instance.Activate();
    }

    public static void OpenCategory(string categoryId)
    {
        if (!Application.Current.Dispatcher.CheckAccess()) { Application.Current.Dispatcher.Invoke(() => OpenCategory(categoryId)); return; }
        OpenWindow();
        if (_instance != null) _ = _instance.FocusCategoryAsync(categoryId);
    }

    private async Task FocusCategoryAsync(string categoryId)
    {
        await ScanTask;
        var selected = System.Linq.Enumerable.FirstOrDefault(ViewModel.Items, item => item.Id == categoryId);
        if (selected == null) { ViewModel.Status = MaintenanceText.Get("CategoryUnavailable"); return; }
        foreach (var item in ViewModel.Items) item.IsSelected = ReferenceEquals(item, selected);
        if (FileContent.Content is FileManagementControl control) control.FocusCategory(selected);
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        ScanTask = ViewModel.ScanAsync();
        await ScanTask;
    }

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsBusy) return;
        e.Cancel = true;
        MessageBox.Show(this, MaintenanceText.BusyClose, MaintenanceText.FileManagement, MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void Window_Closed(object? sender, EventArgs e)
    {
        ViewModel.Cancel();
        if (ReferenceEquals(_instance, this))
        {
            _instance = null;
            ConfigService.Instance.Save<StorageMaintenanceConfig>();
        }
    }
}
