using ColorVision.Copilot;
using ColorVision.Engine.Services.Operations;
using ColorVision.Settings.Maintenance;
using ColorVision.UI.Authorizations;
using ColorVision.UI.Desktop.Wizards;
using ColorVision.Update;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Media;

namespace ColorVision.Recovery;

internal enum StartupMaintenanceMode { SetupWizard, Recovery, SafeStart, SkipSelectedPlugins }

/// <summary>Opens maintenance windows; only explicit startup-only actions use the restart handoff.</summary>
internal static class StartupMaintenanceController
{
    internal const string ArgumentName = "startup-maintenance";
    internal const string SkipPluginsArgumentName = "startup-skip-plugins";
    private static int _requestActive;

    internal static StartupMaintenanceMode? ParseMode(string? value) => value?.ToLowerInvariant() switch
    {
        "setup" => StartupMaintenanceMode.SetupWizard,
        "recovery" => StartupMaintenanceMode.Recovery,
        "safe-start" => StartupMaintenanceMode.SafeStart,
        "skip-plugins" => StartupMaintenanceMode.SkipSelectedPlugins,
        _ => null,
    };

    internal static bool ShouldShowRecovery(StartupMaintenanceMode? mode, bool startupWasHealthy)
        => mode == StartupMaintenanceMode.Recovery || !startupWasHealthy;

    internal static bool ShouldCompleteCancelledRecovery(StartupMaintenanceMode? mode, bool startupWasHealthy)
        => mode == StartupMaintenanceMode.Recovery && startupWasHealthy;

    internal static IReadOnlyList<string> ParseSkippedPluginKeys(string? value)
    {
        try
        {
            return JsonSerializer.Deserialize<string[]>(value ?? "[]")?
                .Where(key => !string.IsNullOrWhiteSpace(key)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray() ?? [];
        }
        catch (JsonException) { return []; }
    }

    internal static ProcessStartInfo CreateRestartStartInfo(string executablePath, int processId, StartupMaintenanceMode mode,
        IReadOnlyList<string>? skippedPluginKeys = null)
    {
        if (!Path.IsPathFullyQualified(executablePath)
            || !string.Equals(Path.GetFileName(executablePath), "ColorVision.exe", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(StartupMaintenanceText.Get("ApplicationUnavailable"));
        if (processId <= 0) throw new ArgumentOutOfRangeException(nameof(processId));
        string modeArgument = mode switch
        {
            StartupMaintenanceMode.SetupWizard => "setup",
            StartupMaintenanceMode.Recovery => "recovery",
            StartupMaintenanceMode.SafeStart => "safe-start",
            StartupMaintenanceMode.SkipSelectedPlugins => "skip-plugins",
            _ => throw new ArgumentOutOfRangeException(nameof(mode)),
        };
        ProcessStartInfo startInfo = new(executablePath)
        {
            UseShellExecute = false,
            WorkingDirectory = Path.GetDirectoryName(executablePath)!,
        };
        startInfo.ArgumentList.Add("-r");
        startInfo.ArgumentList.Add("--wait-for-process");
        startInfo.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        startInfo.ArgumentList.Add("--" + ArgumentName);
        startInfo.ArgumentList.Add(modeArgument);
        if (mode == StartupMaintenanceMode.SkipSelectedPlugins)
        {
            if (skippedPluginKeys == null || skippedPluginKeys.Count == 0 || skippedPluginKeys.Any(string.IsNullOrWhiteSpace))
                throw new ArgumentException("Select at least one plugin to skip.", nameof(skippedPluginKeys));
            startInfo.ArgumentList.Add("--" + SkipPluginsArgumentName);
            startInfo.ArgumentList.Add(JsonSerializer.Serialize(skippedPluginKeys));
        }
        return startInfo;
    }

    internal static void Request(StartupMaintenanceMode mode)
    {
        if (Application.Current is not App app || !app.Dispatcher.CheckAccess()
            || app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished)
            return;
        if (Interlocked.CompareExchange(ref _requestActive, 1, 0) != 0) return;
        string title = StartupMaintenanceText.Get(mode == StartupMaintenanceMode.SetupWizard ? "WizardTitle" : "RecoveryTitle");
        try
        {
            ThrowIfOpeningBlocked(app);
            if (mode == StartupMaintenanceMode.SetupWizard)
            {
                ShowOwnedWindow(app.MainWindow, new WizardWindow(runInitializers: false));
                return;
            }

            using StartupRecoveryWindow recovery = new(null, true, true, () => ThrowIfBlocked(app), app.PrepareRuntimeRecovery);
            ShowOwnedWindow(app.MainWindow, recovery);
            if (recovery.Result.Action == StartupRecoveryAction.RunSetupWizard)
            {
                ShowOwnedWindow(app.MainWindow, new WizardWindow(runInitializers: false));
                return;
            }

            StartupMaintenanceMode? restartMode = GetRestartMode(recovery.Result.Action);
            if (restartMode != null)
                RunRequest(
                    () => ThrowIfBlocked(app),
                    () => MessageBox.Show(app.MainWindow, StartupMaintenanceText.Get("ConfirmPluginRestart"),
                        title, MessageBoxButton.YesNo, MessageBoxImage.Warning, MessageBoxResult.No) == MessageBoxResult.Yes,
                    () => app.TryRestartForMaintenance(restartMode.Value, recovery.Result.SelectedPluginKeys));
        }
        catch (Exception ex)
        {
            ReportFailure(ex, failure =>
                {
                    string message = string.Format(StartupMaintenanceText.Get(app.Windows.Count == 0
                        ? "RestartFailedAfterClose" : "RestartFailed"), failure.Message);
                    if (app.MainWindow is { IsLoaded: true, IsVisible: true } owner)
                        MessageBox.Show(owner, message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                    else
                        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);
                },
                () => app.Windows.Count > 0,
                app.ShutdownForMaintenance);
        }
        finally { Volatile.Write(ref _requestActive, 0); }
    }

    internal static StartupMaintenanceMode? GetRestartMode(StartupRecoveryAction action) => action switch
    {
        StartupRecoveryAction.SkipAllOnce => StartupMaintenanceMode.SafeStart,
        StartupRecoveryAction.SkipSelectedOnce => StartupMaintenanceMode.SkipSelectedPlugins,
        _ => null,
    };

    internal static void ShowOwnedWindow(Window owner, Window window)
    {
        window.Owner = owner;
        window.WindowStartupLocation = WindowStartupLocation.CenterOwner;
        window.ShowDialog();
    }

    // Dependencies are injected so tests never construct the application or touch user configuration.
    internal static bool RunRequest(Action validate, Func<bool> confirm, Func<bool> restart)
    {
        validate();
        if (!confirm()) return false;
        validate();
        return restart();
    }

    internal static void FinishAcceptedClose(Action startReplacement, Action<Exception> reportFailure, Action shutdown)
    {
        try { startReplacement(); }
        catch (Exception ex) { reportFailure(ex); }
        finally { shutdown(); }
    }

    internal static void ReportFailure(Exception failure, Action<Exception> report, Func<bool> hasOpenWindows, Action shutdown)
    {
        try { report(failure); }
        finally { if (!hasOpenWindows()) shutdown(); }
    }

    internal static void ThrowIfBlocked(App app)
    {
        ThrowIfOpeningBlocked(app);
        var flow = new FlowOperationsRuntimeStatusProvider().Capture();
        if (!flow.Available || flow.IsActive)
            throw new InvalidOperationException(StartupMaintenanceText.Get("FlowBusy"));
        if (CopilotAgentTaskHost.Shared.IsActive || CopilotAgentTaskHost.Shared.QueuedCount > 0
            || CopilotBackgroundShellCommandRegistry.Shared.HasActiveCommands
            || app.Windows.Cast<Window>().Any(HasBusyView))
            throw new InvalidOperationException(StartupMaintenanceText.Get("TaskBusy"));
        if (CombinedUpdateCoordinator.HasPackageMaintenanceProtection || ExitUpdateHandoff.HasActiveUpdateForCleanup())
            throw new InvalidOperationException(StartupMaintenanceText.Get("UpdateBusy"));
    }

    internal static void ThrowIfOpeningBlocked(App app)
    {
        if (app.Dispatcher.HasShutdownStarted || app.Dispatcher.HasShutdownFinished
            || app.MainWindow is not MainWindow { IsLoaded: true, IsVisible: true })
            throw new InvalidOperationException(StartupMaintenanceText.Get("ApplicationUnavailable"));
        if (Authorization.Instance == null || Authorization.Instance.PermissionMode > PermissionMode.Administrator)
            throw new InvalidOperationException(StartupMaintenanceText.Get("AdminRequired"));
    }

    private static bool HasBusyView(DependencyObject element)
    {
        if (element is ApplicationSnapshotsWindow { IsBusy: true }
            || element is FrameworkElement { DataContext: CopilotChatViewModel { IsBusy: true } }
            || element is StorageMaintenanceControl { ViewModel.IsBusy: true })
            return true;
        int count = VisualTreeHelper.GetChildrenCount(element);
        for (int i = 0; i < count; i++)
            if (HasBusyView(VisualTreeHelper.GetChild(element, i))) return true;
        return false;
    }
}
