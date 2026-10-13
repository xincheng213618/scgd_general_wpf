using ColorVision.Database;
using ColorVision.Engine.Services.Operations;
using ColorVision.Solution.MultiImageViewer;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.HotKey;
using ColorVision.UI.Languages;
using ColorVision.UI.Serach;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Settings.Maintenance;

public partial class StorageMaintenanceControl : UserControl
{
    public static IReadOnlyList<string> ResetSectionNames { get; } = Array.AsReadOnly(new[]
    {
        typeof(ThemeConfig), typeof(LanguageConfig), typeof(MainWindowConfig), typeof(HotKeyConfig), typeof(SearchConfig), typeof(MultiImageViewerConfig)
    }.SelectMany(type => new[] { type.FullName!, type.Name }).Distinct(StringComparer.Ordinal).ToArray());
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
        ConfigService.Instance.GetRequiredService<StorageMaintenanceConfig>(), Array.Empty<ColorVision.UI.Maintenance.IFileMaintenanceProvider>()) { Status = string.Empty };

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

    private void Files_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler != null) FileManagementWindow.OpenWindow();
    }

    private void Memory_Click(object sender, RoutedEventArgs e)
    {
        if (_configHandler != null) ColorVision.Engine.Services.Devices.Camera.Local.LocalCalibrationCacheManagerWindow.OpenWindow();
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
