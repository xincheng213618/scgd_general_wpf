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
    private bool _newUserGuideScheduled;

    internal static bool TryRecordNewUserGuideOffer(MainWindowConfig config)
    {
        ArgumentNullException.ThrowIfNull(config);
        if (config.HasShownNewUserGuide)
            return false;

        config.HasShownNewUserGuide = true;
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
        if (_newUserGuideScheduled || Config.HasShownNewUserGuide)
            return;

        _newUserGuideScheduled = true;
        _ = Dispatcher.BeginInvoke(new Action(() =>
        {
            if (!IsVisible)
                return;

            try
            {
                if (!TryRecordNewUserGuideOffer(Config))
                    return;

                ConfigService.Instance.Save<MainWindowConfig>();
                ShowNewUserGuide();
            }
            catch (Exception ex)
            {
                log.Warn("Failed to persist the one-time new-user guide state; automatic display was suppressed.", ex);
            }
        }), DispatcherPriority.ContextIdle);
    }

    private void MainWindow_NewUserGuidePreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (!NewUserGuideOverlay.IsGuideOpen || e.Key != Key.Escape)
            return;

        NewUserGuideOverlay.Dismiss();
        e.Handled = true;
    }
}
