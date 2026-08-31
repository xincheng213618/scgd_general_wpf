using ColorVision.Recovery;
using ColorVision.Solution.Workspace;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;

namespace ColorVision;

public partial class App
{
    private bool _maintenanceExitRequested;

    internal void ShutdownForMaintenance()
    {
        // This is independent of replacement success: even a failed launch must not install an
        // unrelated update that became ready while the failure message was pumping events.
        _maintenanceExitRequested = true;
        if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
            Shutdown();
    }

    internal static bool ShouldApplyPrefetchedUpdateOnExit(bool maintenanceExitRequested)
        => !maintenanceExitRequested;

    internal void PrepareRuntimeRecovery(Window operationWindow)
    {
        ArgumentNullException.ThrowIfNull(operationWindow);
        Dispatcher.VerifyAccess();
        StartupMaintenanceController.ThrowIfBlocked(this);
        // This existing method prepares dirty documents without removing them from the workspace.
        if (!EditorDocumentService.TryCloseAllDocuments())
            throw new OperationCanceledException(StartupMaintenanceText.Get("OperationCancelled"));

        Window[] otherWindows = Windows.Cast<Window>()
            .Where(window => ShouldCloseBeforeRecovery(window, MainWindow, operationWindow))
            .OrderByDescending(GetOwnerDepth).ToArray();
        if (!TryCloseMaintenanceWindows(otherWindows, () => StartupMaintenanceController.ThrowIfBlocked(this)))
            throw new OperationCanceledException(StartupMaintenanceText.Get("OperationCancelled"));

        ConfigHandler.GetInstance().SaveConfigs();
        StartupMaintenanceController.ThrowIfBlocked(this);
    }

    internal static bool ShouldCloseBeforeRecovery(Window window, Window mainWindow, Window operationWindow)
    {
        if (ReferenceEquals(window, mainWindow)) return false;
        for (Window? current = operationWindow; current != null; current = current.Owner)
            if (ReferenceEquals(window, current)) return false;
        return true;
    }

    internal bool TryRestartForMaintenance(StartupMaintenanceMode mode, IReadOnlyList<string> skippedPluginKeys)
    {
        StartupMaintenanceController.ThrowIfBlocked(this);
        string executablePath = Environment.ProcessPath ?? string.Empty;
        if (!File.Exists(executablePath))
            throw new InvalidOperationException(StartupMaintenanceText.Get("ApplicationUnavailable"));
        ProcessStartInfo startInfo = StartupMaintenanceController.CreateRestartStartInfo(executablePath, Environment.ProcessId, mode, skippedPluginKeys);
        Window primaryWindow = MainWindow;
        ShutdownMode previousShutdownMode = ShutdownMode;
        bool previousReplacement = _isSingleInstanceReplacement;
        bool handedOff = false;
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;
        try
        {
            // Child windows must see their own Closing event; owner shutdown skips those save/cancel hooks.
            Window[] windows = Windows.Cast<Window>()
                .OrderBy(window => ReferenceEquals(window, primaryWindow) ? 1 : 0)
                .ThenByDescending(GetOwnerDepth).ToArray();
            if (!TryCloseMaintenanceWindows(windows, () =>
                {
                    StartupMaintenanceController.ThrowIfBlocked(this);
                    ConfigHandler.GetInstance().SaveConfigs();
                }))
                return false;

            // All windows explicitly accepted closing. The child waits before reading configuration or
            // acquiring the single-instance mutex; normal exit cleanup can finish writing first.
            StartupMaintenanceController.FinishAcceptedClose(
                () =>
                {
                    using Process replacement = Process.Start(startInfo)
                        ?? throw new InvalidOperationException(StartupMaintenanceText.Get("ApplicationUnavailable"));
                    _isSingleInstanceReplacement = true;
                    handedOff = true;
                },
                exception => MessageBox.Show(
                    string.Format(StartupMaintenanceText.Get("RestartFailedAfterClose"), exception.Message),
                    "ColorVision", MessageBoxButton.OK, MessageBoxImage.Warning),
                ShutdownForMaintenance);
            return handedOff;
        }
        finally
        {
            if (!handedOff)
            {
                _isSingleInstanceReplacement = previousReplacement;
                if (!Dispatcher.HasShutdownStarted && !Dispatcher.HasShutdownFinished)
                    ShutdownMode = previousShutdownMode;
            }
        }
    }

    internal static bool TryCloseMaintenanceWindows(IReadOnlyList<Window> windows, Action beforeAcceptedClose)
    {
        foreach (Window window in windows)
        {
            bool closed = false;
            Exception? closeFailure = null;
            void OnClosed(object? sender, EventArgs args) => closed = true;
            void OnClosing(object? sender, CancelEventArgs args)
            {
                if (args.Cancel) return;
                try { beforeAcceptedClose(); }
                catch (Exception ex) { closeFailure = ex; args.Cancel = true; }
            }
            window.Closed += OnClosed;
            window.Closing += OnClosing;
            try { window.Close(); }
            finally
            {
                window.Closed -= OnClosed;
                window.Closing -= OnClosing;
            }
            if (closeFailure != null) throw closeFailure;
            if (!closed) return false;
        }
        return true;
    }

    private static int GetOwnerDepth(Window window)
    {
        int depth = 0;
        for (Window? owner = window.Owner; owner != null; owner = owner.Owner) depth++;
        return depth;
    }
}
