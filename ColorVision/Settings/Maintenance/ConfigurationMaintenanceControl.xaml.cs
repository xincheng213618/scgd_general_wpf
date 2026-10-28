using ColorVision.Common.MVVM;
using ColorVision.Solution.MultiImageViewer;
using ColorVision.Themes;
using ColorVision.UI;
using ColorVision.UI.Authorizations;
using ColorVision.UI.HotKey;
using ColorVision.UI.Languages;
using ColorVision.UI.Serach;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace ColorVision.Settings.Maintenance;

public sealed class ConfigurationMaintenanceViewModel : ViewModelBase
{
    private bool busy;
    private string status = string.Empty;
    public bool IsBusy { get => busy; internal set { if (SetProperty(ref busy, value)) OnPropertyChanged(nameof(IsIdle)); } }
    public bool IsIdle => !IsBusy;
    public string Status { get => status; internal set => SetProperty(ref status, value); }
}

public partial class ConfigurationMaintenanceControl : UserControl
{
    public static IReadOnlyList<string> ResetSectionNames { get; } = Array.AsReadOnly(new[]
    {
        typeof(ThemeConfig), typeof(LanguageConfig), typeof(MainWindowConfig), typeof(HotKeyConfig), typeof(SearchConfig), typeof(MultiImageViewerConfig)
    }.SelectMany(type => new[] { type.FullName!, type.Name }).Distinct(StringComparer.Ordinal).ToArray());
    public ConfigurationMaintenanceViewModel ViewModel { get; } = new();
    private readonly ConfigHandler? configHandler;
    private Window? owner;

    public ConfigurationMaintenanceControl() : this(ConfigHandler.GetInstance()) { }

    // A null handler is an isolated UI host; loading it never touches production configuration.
    public ConfigurationMaintenanceControl(ConfigHandler? configHandler)
    {
        this.configHandler = configHandler;
        InitializeComponent();
        DataContext = ViewModel;
    }

    private void Control_Loaded(object sender, RoutedEventArgs e)
    {
        owner = Window.GetWindow(this);
        if (owner != null) owner.Closing += Owner_Closing;
        RefreshPendingReset();
    }

    private void Control_Unloaded(object sender, RoutedEventArgs e)
    {
        if (owner != null) owner.Closing -= Owner_Closing;
        owner = null;
    }

    private void Owner_Closing(object? sender, CancelEventArgs e)
    {
        if (!ViewModel.IsBusy) return;
        e.Cancel = true;
        ShowMessage(MaintenanceText.BusyClose);
    }

    private void Backup_Click(object sender, RoutedEventArgs e)
    {
        if (configHandler == null || !CheckAccess(MaintenanceActions.BackupSettings)) return;
        RunAction(MaintenanceActions.BackupSettings, () =>
        {
            configHandler.SaveConfigs();
            var result = configHandler.CreateMaintenanceResetService().CreateBackup();
            RequireSuccess(result);
            ViewModel.Status = string.Format(MaintenanceText.BackupCreated, result.BackupPath);
        });
    }

    private void OpenBackups_Click(object sender, RoutedEventArgs e)
    {
        if (configHandler == null || !CheckAccess(MaintenanceActions.OpenBackups)) return;
        RunAction(MaintenanceActions.OpenBackups, () =>
        {
            string directory = configHandler.CreateMaintenanceResetService().BackupDirectoryPath;
            if (!Directory.Exists(directory)) { ShowMessage(MaintenanceText.CacheUnavailable); return; }
            Process.Start(new ProcessStartInfo(directory) { UseShellExecute = true });
        });
    }

    private void Reset_Click(object sender, RoutedEventArgs e)
    {
        if (configHandler == null || !CheckAccess(MaintenanceActions.ResetSettings)) return;
        var selected = ResetOptions.Children.OfType<CheckBox>().Where(check => check.IsChecked == true).ToArray();
        if (selected.Length == 0) { ShowMessage(MaintenanceText.SelectReset); return; }
        string message = string.Format(MaintenanceText.ResetConfirm, string.Join("、", selected.Select(check => check.Content)));
        if (MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.Reset, MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
        RunAction(MaintenanceActions.ResetSettings, () =>
        {
            configHandler.SaveConfigs();
            var service = configHandler.CreateMaintenanceResetService();
            var plan = service.Prepare(selected.SelectMany(check => ((string)check.Tag).Split(',')));
            RequireSuccess(service.Schedule(plan));
            ViewModel.Status = MaintenanceText.ResetScheduled;
            RefreshPendingReset();
        });
    }

    private void CancelReset_Click(object sender, RoutedEventArgs e)
    {
        if (configHandler == null || !CheckAccess(MaintenanceActions.CancelReset)) return;
        RunAction(MaintenanceActions.CancelReset, () =>
        {
            RequireSuccess(configHandler.CreateMaintenanceResetService().CancelPending());
            ViewModel.Status = MaintenanceText.ResetCancelled;
            RefreshPendingReset();
        });
    }

    private void RefreshPendingReset()
    {
        CancelResetButton.IsEnabled = false;
        if (configHandler == null) return;
        var result = configHandler.CreateMaintenanceResetService().GetPending();
        bool pending = result.Status == ConfigMaintenanceResetStatus.Scheduled;
        CancelResetButton.IsEnabled = pending || (result.Status == ConfigMaintenanceResetStatus.Failed && File.Exists(configHandler.CreateMaintenanceResetService().PendingFilePath));
        ResetStatus.Text = pending ? string.Format(MaintenanceText.Pending, string.Join(", ", result.SectionNames)) : result.ErrorMessage;
        if (!pending && configHandler.LastMaintenanceResetResult is { Status: ConfigMaintenanceResetStatus.Applied } applied)
            ResetStatus.Text = string.Format(MaintenanceText.Get("ResetApplied"), applied.BackupPath);
        if (pending && configHandler.LastMaintenanceResetResult is { Status: ConfigMaintenanceResetStatus.Deferred })
            ResetStatus.Text += " " + MaintenanceText.Get("ResetDeferred");
        if (pending && configHandler.LastMaintenanceResetResult is { Status: ConfigMaintenanceResetStatus.Failed } failed)
            ResetStatus.Text += " " + string.Format(MaintenanceText.Get("ResetStartupFailed"), failed.ErrorMessage);
    }

    private bool CheckAccess(string actionId)
    {
        if (MaintenanceAccess.Evaluate(actionId).Allowed) return true;
        ShowMessage(MaintenanceText.Get("AccessDenied"));
        return false;
    }

    private static void RequireSuccess(ConfigMaintenanceResetResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.ErrorMessage);
    }

    private void RunAction(string actionId, Action action)
    {
        if (ViewModel.IsBusy) return;
        try
        {
            MaintenanceAccess.Demand(actionId);
            ViewModel.IsBusy = true;
            action();
        }
        catch (AccessDeniedException) { ShowMessage(MaintenanceText.Get("AccessDenied")); }
        catch (Exception ex) { ViewModel.Status = string.Format(MaintenanceText.Failed, ex.Message); ShowMessage(ViewModel.Status); }
        finally { ViewModel.IsBusy = false; }
    }

    private void ShowMessage(string message) => MessageBox.Show(Window.GetWindow(this), message, MaintenanceText.Get("ConfigurationTitle"), MessageBoxButton.OK, MessageBoxImage.Information);
}
