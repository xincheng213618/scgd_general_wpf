using ColorVision.Guidance;
using ColorVision.UI;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;

namespace ColorVision;

public partial class MainWindow
{
    internal const int CurrentNewUserGuideVersion = 1;
    private bool _newUserGuideScheduled;

    internal static bool ShouldOfferNewUserGuide(int lastSeenVersion) =>
        lastSeenVersion < CurrentNewUserGuideVersion;

    internal static bool TryRecordNewUserGuideOffer(MainWindowConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (!ShouldOfferNewUserGuide(config.LastSeenNewUserGuideVersion))
            return false;

        config.LastSeenNewUserGuideVersion = CurrentNewUserGuideVersion;
        return true;
    }

    internal void ShowNewUserGuide()
    {
        NewUserGuideOverlay.ShowWelcome(CreateNewUserGuideSteps());
    }

    private IReadOnlyList<NewUserGuideStep> CreateNewUserGuideSteps() =>
    [
        new(
            NewUserGuideText.Get("MenuStepTitle"),
            NewUserGuideText.Get("MenuStepDescription"),
            () => Menu1),
        new(
            NewUserGuideText.Get("ExplorerStepTitle"),
            NewUserGuideText.Get("ExplorerStepDescription"),
            () => ProjectPanelGrid),
        new(
            NewUserGuideText.Get("WorkspaceStepTitle"),
            NewUserGuideText.Get("WorkspaceStepDescription"),
            ResolveActiveWorkspaceTarget),
        new(
            NewUserGuideText.Get("StatusStepTitle"),
            NewUserGuideText.Get("StatusStepDescription"),
            () => StatusBarGrid),
    ];

    private FrameworkElement ResolveActiveWorkspaceTarget() =>
        DockingManager1.ActiveContent as FrameworkElement ?? DockingManager1;

    private void ScheduleNewUserGuideAfterFirstRender()
    {
        if (_newUserGuideScheduled || !ShouldOfferNewUserGuide(Config.LastSeenNewUserGuideVersion))
            return;

        _newUserGuideScheduled = true;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible || !TryRecordNewUserGuideOffer(Config))
                return;

            PersistNewUserGuideOffer();
            ShowNewUserGuide();
        }), DispatcherPriority.ContextIdle);
    }

    private void NewUserGuideOverlay_GuideDismissed(object? sender, EventArgs e)
    {
        if (!TryRecordNewUserGuideOffer(Config))
            return;

        PersistNewUserGuideOffer();
    }

    private void PersistNewUserGuideOffer()
    {
        try
        {
            ConfigService.Instance.Save<MainWindowConfig>();
        }
        catch (Exception ex)
        {
            log.Warn("Failed to persist the new-user guide state.", ex);
        }
    }

    private void MainWindow_NewUserGuidePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!NewUserGuideOverlay.IsGuideOpen || e.Key != Key.Escape)
            return;

        NewUserGuideOverlay.Dismiss();
        e.Handled = true;
    }
}
