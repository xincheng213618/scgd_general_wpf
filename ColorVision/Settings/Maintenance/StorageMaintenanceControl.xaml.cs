using ColorVision.Common.Utilities;
using ColorVision.Database;
using ColorVision.Engine.Services.Operations;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Maintenance;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Settings.Maintenance;

public partial class StorageMaintenanceControl : UserControl
{
    public static IReadOnlyList<string> ResetSectionNames => ConfigurationMaintenanceControl.ResetSectionNames;
    public StorageMaintenanceViewModel ViewModel { get; }
    public StorageOverviewViewModel Overview { get; }
    public Task ScanTask { get; private set; } = Task.CompletedTask;
    private readonly ConfigHandler? _configHandler;
    private Window? _owner;
    private bool _loaded;

    public StorageMaintenanceControl() : this(CreateProductionViewModel(), ConfigHandler.GetInstance(),
        new StorageOverviewViewModel(AssemblyHandler.GetInstance().LoadImplementations<IStorageUsageProvider>())) { }

    // Explicit models with no handler are isolated hosts: no production providers or configuration are opened.
    public StorageMaintenanceControl(StorageMaintenanceViewModel viewModel, ConfigHandler? configHandler = null)
        : this(viewModel, configHandler, new StorageOverviewViewModel([])) { }

    public StorageMaintenanceControl(StorageMaintenanceViewModel viewModel, ConfigHandler? configHandler, StorageOverviewViewModel overview)
    {
        ViewModel = viewModel;
        Overview = overview;
        _configHandler = configHandler;
        InitializeComponent();
        DataContext = this;
    }

    private static StorageMaintenanceViewModel CreateProductionViewModel() => new(
        ConfigService.Instance.GetRequiredService<StorageMaintenanceConfig>(), Array.Empty<IFileMaintenanceProvider>()) { Status = string.Empty };

    private async void Control_Loaded(object sender, RoutedEventArgs e)
    {
        if (_loaded) return;
        _loaded = true;
        _owner = Window.GetWindow(this);
        if (_owner != null) { _owner.Closing -= Owner_Closing; _owner.Closing += Owner_Closing; }
        if (Overview.IsBusy) await Overview.ScanTask;
        if (!_loaded) return;
        ScanTask = Overview.ScanAsync();
        await ScanTask;
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        _loaded = false;
        Overview.Cancel();
        ViewModel.Cancel();
        if (_owner != null) _owner.Closing -= Owner_Closing;
        _owner = null;
    }

    private void Owner_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsBusy) return;
        e.Cancel = true;
        ShowMessage(MaintenanceText.BusyClose);
    }

    private async void RefreshStorage_Click(object sender, RoutedEventArgs e)
    {
        ScanTask = Overview.ScanAsync();
        await ScanTask;
    }
    private void CancelStorage_Click(object sender, RoutedEventArgs e) => Overview.Cancel();
    private void Files_Click(object sender, RoutedEventArgs e) { if (_configHandler != null) FileManagementWindow.OpenWindow(); }
    private void Memory_Click(object sender, RoutedEventArgs e) { if (_configHandler != null) ColorVision.Engine.Services.Devices.Camera.Local.LocalCalibrationCacheManagerWindow.OpenWindow(); }

    private void Database_Click(object sender, RoutedEventArgs e) => RunAction(MaintenanceActions.OpenDatabase, () =>
    {
        var flow = new FlowOperationsRuntimeStatusProvider().Capture();
        if (!flow.Available || flow.IsActive) throw new InvalidOperationException(MaintenanceText.Get("FlowBusy"));
        DatabaseCleanupWindow.OpenWindow(Window.GetWindow(this));
    });
    private void Snapshots_Click(object sender, RoutedEventArgs e) => RunAction(MaintenanceActions.OpenSnapshots,
        () => new ApplicationSnapshotsWindow { Owner = Window.GetWindow(this) }.ShowDialog());

    private void ManageCategory_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || (sender as FrameworkElement)?.DataContext is not StorageUsageItem item || !item.CanManage) return;
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, ItemsSource = item.Files, EnableRowVirtualization = true };
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.FilePath, Binding = new Binding("FullPath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.Size + " (B)", Binding = new Binding("Length"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.Modified, Binding = new Binding("LastWriteTimeUtc") { StringFormat = "yyyy-MM-dd HH:mm:ss" }, Width = 160 });
        var panel = new DockPanel { Margin = new Thickness(16) };
        var actions = new WrapPanel { Margin = new Thickness(0, 12, 0, 0), HorizontalAlignment = HorizontalAlignment.Right };
        DockPanel.SetDock(actions, Dock.Bottom);
        var window = new Window { Title = item.Title, Width = 960, Height = 560, MinWidth = 660, MinHeight = 360, Owner = Window.GetWindow(this), Content = panel, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        window.SetResourceReference(Window.BackgroundProperty, "GlobalBackground");
        window.SetResourceReference(Window.ForegroundProperty, "GlobalTextBrush");
        window.ApplyCaption();
        void AddAction(string label, Action action)
        {
            var button = new Button { Content = label, Padding = new Thickness(12, 5, 12, 5), Margin = new Thickness(8, 0, 0, 0) };
            button.Click += (_, _) => { try { action(); } catch (Exception ex) { MessageBox.Show(window, ex.Message, item.Title, MessageBoxButton.OK, MessageBoxImage.Warning); } };
            actions.Children.Add(button);
        }
        AddAction(MaintenanceText.OpenFileLocation, () =>
        {
            if (grid.SelectedItem is FileMaintenanceEntry file && File.Exists(file.FullPath)) PlatformHelper.OpenFolderAndSelectFile(file.FullPath);
            else
            {
                string[] roots = item.SourceDirectories.Where(Directory.Exists).ToArray();
                if (roots.Length == 1) PlatformHelper.OpenFolder(roots[0]);
                else if (roots.Length > 1)
                {
                    var menu = new ContextMenu();
                    foreach (string root in roots) { var entry = new MenuItem { Header = root }; entry.Click += (_, _) => PlatformHelper.OpenFolder(root); menu.Items.Add(entry); }
                    menu.IsOpen = true;
                }
            }
        });
        if (item.Category.OpenManager is { } manager) AddAction(MaintenanceText.Get("ResourceManager"), manager);
        if (!item.IsProtected && item.ManagementCategoryId is { } categoryId)
            AddAction(MaintenanceText.FileManagement, () => { window.Close(); FileManagementWindow.OpenCategory(categoryId); });
        AddAction(MaintenanceText.Close, window.Close);
        panel.Children.Add(actions);
        if (item.IsProtected)
        {
            var notice = new TextBlock { Text = MaintenanceText.Get("ReadOnlyResources"), TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 12) };
            DockPanel.SetDock(notice, Dock.Top); panel.Children.Add(notice);
        }
        panel.Children.Add(grid);
        window.ShowDialog();
    }

    private void RunAction(string actionId, Action action)
    {
        if (_configHandler == null || ViewModel.IsBusy) return;
        try { MaintenanceAccess.Demand(actionId); action(); }
        catch (AccessDeniedException) { ShowMessage(MaintenanceText.Get("AccessDenied")); }
        catch (Exception ex) { ViewModel.Status = string.Format(MaintenanceText.Failed, ex.Message); ShowMessage(ViewModel.Status); }
    }
    private void ShowMessage(string message) => MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
