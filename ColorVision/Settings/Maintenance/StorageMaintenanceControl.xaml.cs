using ColorVision.Database;
using ColorVision.Engine.Services.Operations;
using ColorVision.Solution.MultiImageViewer;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace ColorVision.Settings.Maintenance;

public partial class StorageMaintenanceControl : UserControl
{
    public static IReadOnlyList<string> ResetSectionNames { get; } = Array.AsReadOnly(new[]
    {
        "ThemeConfig", "LanguageConfig", "MainWindowConfig", "HotKeyConfig", "SearchConfig", "MultiImageViewerConfig"
    });
    public StorageMaintenanceViewModel ViewModel { get; }
    private readonly ConfigHandler? _configHandler;
    private Window? _owner;

    public StorageMaintenanceControl() : this(CreateProductionViewModel(), ConfigHandler.GetInstance()) { }

    // Supplying a model without a handler is an isolated UI host: no real configuration or database is opened.
    public StorageMaintenanceControl(StorageMaintenanceViewModel viewModel, ConfigHandler? configHandler = null)
    {
        ViewModel = viewModel;
        _configHandler = configHandler;
        InitializeComponent();
        DataContext = viewModel;
    }

    private static StorageMaintenanceViewModel CreateProductionViewModel() => new(
        ConfigService.Instance.GetRequiredService<StorageMaintenanceConfig>(), StorageMaintenanceCatalog.CreateRules,
        () =>
        {
            var snapshot = ThumbnailCacheManager.ScanCacheForMaintenance();
            if (!string.IsNullOrEmpty(snapshot.Error)) throw new InvalidOperationException(snapshot.Error);
            return new(snapshot.SizeBytes, snapshot.EntryCount, snapshot);
        },
        snapshot =>
        {
            if (snapshot.Token is not ThumbnailCacheMaintenanceSnapshot token)
                return new(0, 0, snapshot.Count, MaintenanceText.ScanFirst);
            var result = ThumbnailCacheManager.ClearCacheForMaintenance(token);
            return new(result.ReleasedBytes, result.DeletedEntryCount,
                result.RequiresRescan ? snapshot.Count : 0,
                result.Succeeded || result.RequiresRescan ? null : result.Message,
                result.Succeeded || result.RequiresRescan ? result.Message : null);
        }, StorageMaintenanceCatalog.GetProtectionNotice);

    private void Control_Loaded(object sender, RoutedEventArgs e)
    {
        _owner = Window.GetWindow(this);
        if (_owner != null) _owner.Closing += Owner_Closing;
        RefreshPendingReset();
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
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

    private async void Scan_Click(object sender, RoutedEventArgs e) => await ViewModel.ScanAsync();
    private void Cancel_Click(object sender, RoutedEventArgs e) => ViewModel.Cancel();
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
        if (MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.Title, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        await ViewModel.CleanupAsync(items);
    }

    private void Details_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not StorageCleanupItem item) return;
        if (item.Id == "thumbnails") { ShowMessage(MaintenanceText.ThumbnailList); return; }
        var grid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, ItemsSource = item.FileScan?.Files, Margin = new Thickness(12), EnableRowVirtualization = true };
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.FilePath, Binding = new Binding("FullPath"), Width = new DataGridLength(1, DataGridLengthUnitType.Star) });
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.Size + " (B)", Binding = new Binding("Length"), Width = 100 });
        grid.Columns.Add(new DataGridTextColumn { Header = MaintenanceText.Modified + " (UTC)", Binding = new Binding("LastWriteTimeUtc") { StringFormat = "yyyy-MM-dd HH:mm:ss" }, Width = 160 });
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

    private void Database_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || !CheckAdministrator()) return;
        RunAction(() =>
        {
            var flow = new FlowOperationsRuntimeStatusProvider().Capture();
            if (!flow.Available || flow.IsActive) throw new InvalidOperationException(MaintenanceText.Get("FlowBusy"));
            DatabaseCleanupWindow.OpenWindow(Window.GetWindow(this));
        });
    }

    private void Snapshots_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || !CheckAdministrator()) return;
        RunAction(() => new ApplicationSnapshotsWindow { Owner = Window.GetWindow(this) }.ShowDialog());
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || !CheckAdministrator()) return;
        RunAction(() =>
        {
            _configHandler.SaveConfigs();
            var result = _configHandler.CreateMaintenanceResetService().CreateBackup();
            RequireSuccess(result);
            ViewModel.Status = string.Format(MaintenanceText.BackupCreated, result.BackupPath);
        });
    }

    private void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || !CheckAdministrator()) return;
        RunAction(() =>
        {
            string directory = _configHandler.CreateMaintenanceResetService().BackupDirectoryPath;
            if (!Directory.Exists(directory)) { ShowMessage(MaintenanceText.CacheUnavailable); return; }
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        });
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || !CheckAdministrator()) return;
        var selected = ResetOptions.Children.OfType<CheckBox>().Where(check => check.IsChecked == true).ToArray();
        if (selected.Length == 0) { ShowMessage(MaintenanceText.SelectReset); return; }
        string message = string.Format(MaintenanceText.ResetConfirm, string.Join("、", selected.Select(check => check.Content)));
        if (MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.Reset, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        RunAction(() =>
        {
            _configHandler.SaveConfigs();
            var service = _configHandler.CreateMaintenanceResetService();
            var plan = service.Prepare(selected.SelectMany(check => ((string)check.Tag).Split(',')));
            RequireSuccess(service.Schedule(plan));
            ViewModel.Status = MaintenanceText.ResetScheduled;
            RefreshPendingReset();
        });
    }

    private void CancelReset_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler == null || !CheckAdministrator()) return;
        RunAction(() =>
        {
            RequireSuccess(_configHandler.CreateMaintenanceResetService().CancelPending());
            ViewModel.Status = MaintenanceText.ResetCancelled;
            RefreshPendingReset();
        });
    }

    private void RefreshPendingReset()
    {
        CancelResetButton.IsEnabled = false;
        if (_configHandler == null) return;
        var result = _configHandler.CreateMaintenanceResetService().GetPending();
        bool pending = result.Status == ConfigMaintenanceResetStatus.Scheduled;
        CancelResetButton.IsEnabled = pending || (result.Status == ConfigMaintenanceResetStatus.Failed && File.Exists(_configHandler.CreateMaintenanceResetService().PendingFilePath));
        ResetStatus.Text = pending ? string.Format(MaintenanceText.Pending, string.Join(", ", result.SectionNames)) : result.ErrorMessage;
        if (!pending && _configHandler.LastMaintenanceResetResult is { Status: ConfigMaintenanceResetStatus.Applied } applied)
            ResetStatus.Text = string.Format(MaintenanceText.Get("ResetApplied"), applied.BackupPath);
        if (pending && _configHandler.LastMaintenanceResetResult is { Status: ConfigMaintenanceResetStatus.Deferred })
            ResetStatus.Text += " " + MaintenanceText.Get("ResetDeferred");
        if (pending && _configHandler.LastMaintenanceResetResult is { Status: ConfigMaintenanceResetStatus.Failed } failed)
            ResetStatus.Text += " " + string.Format(MaintenanceText.Get("ResetStartupFailed"), failed.ErrorMessage);
    }

    private bool CheckAdministrator()
    {
        if (Authorization.Instance != null && Authorization.Instance.PermissionMode <= PermissionMode.Administrator) return true;
        ShowMessage(MaintenanceText.AdminRequired);
        return false;
    }

    private static void RequireSuccess(ConfigMaintenanceResetResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
    }

    private void RunAction(Action action)
    {
        if (ViewModel.IsBusy) return;
        try { action(); }
        catch (Exception ex) { ViewModel.Status = string.Format(MaintenanceText.Failed, ex.Message); ShowMessage(ViewModel.Status); }
    }

    private void ShowMessage(string message) => MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.Title, MessageBoxButton.OK, MessageBoxImage.Information);
}
