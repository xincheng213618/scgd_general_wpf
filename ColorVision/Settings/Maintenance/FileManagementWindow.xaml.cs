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
            _ = _instance.ViewModel.ScanAsync();
        }
        _instance.Activate();
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
