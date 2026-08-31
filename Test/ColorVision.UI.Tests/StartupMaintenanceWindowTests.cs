using ColorVision.Recovery;
using System.Windows;
using System.Windows.Threading;

namespace ColorVision.UI.Tests;

[Collection(AssemblyDiscoveryCollection.CollectionName)]
public sealed class StartupMaintenanceWindowTests
{
    [Fact]
    public void OwnedMaintenanceDialogClosesOnlyItsChildAndPreservesTheMainWindow()
    {
        WpfTestHost.Invoke(() =>
        {
            Application application = Application.Current;
            Window previousMainWindow = application.MainWindow;
            var owner = CreateTestWindow();
            var child = CreateTestWindow();
            int ownerClosing = 0, ownerClosed = 0, childClosing = 0, childClosed = 0;
            bool observedShown = false, timedOut = false;
            Exception? observationFailure = null;
            var timeout = new DispatcherTimer(DispatcherPriority.Send) { Interval = TimeSpan.FromSeconds(5) };
            owner.Closing += (_, _) => ownerClosing++;
            owner.Closed += (_, _) => ownerClosed++;
            child.Closing += (_, _) => childClosing++;
            child.Closed += (_, _) => childClosed++;
            timeout.Tick += (_, _) => { timedOut = true; if (childClosed == 0) child.Close(); };
            child.Loaded += (_, _) => child.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                try
                {
                    Assert.Same(owner, child.Owner);
                    Assert.Equal(WindowStartupLocation.CenterOwner, child.WindowStartupLocation);
                    Assert.Same(owner, application.MainWindow);
                    Assert.True(owner.IsVisible);
                    Assert.True(child.IsVisible);
                    Assert.Same(child, Assert.Single(owner.OwnedWindows.Cast<Window>()));
                    observedShown = true;
                }
                catch (Exception exception) { observationFailure = exception; }
                finally { if (childClosed == 0) child.Close(); }
            }));
            try
            {
                // Only synthetic WPF windows are shown. No App, wizard, recovery view or user configuration is constructed.
                application.MainWindow = owner;
                owner.Show();
                timeout.Start();

                StartupMaintenanceController.ShowOwnedWindow(owner, child);

                Assert.False(timedOut, "The synthetic maintenance dialog failed to auto-close within five seconds.");
                Assert.Null(observationFailure);
                Assert.True(observedShown);
                Assert.Equal(1, childClosing);
                Assert.Equal(1, childClosed);
                Assert.False(child.IsVisible);
                Assert.Equal(0, ownerClosing);
                Assert.Equal(0, ownerClosed);
                Assert.Same(owner, application.MainWindow);
                Assert.True(owner.IsVisible);
                Assert.True(owner.IsEnabled);
                Assert.Empty(owner.OwnedWindows.Cast<Window>());
            }
            finally
            {
                timeout.Stop();
                application.MainWindow = previousMainWindow;
                if (childClosed == 0) child.Close();
                if (ownerClosed == 0) owner.Close();
            }
        });
    }

    [Theory]
    [InlineData(StartupRecoveryAction.SkipAllOnce, nameof(StartupMaintenanceMode.SafeStart))]
    [InlineData(StartupRecoveryAction.SkipSelectedOnce, nameof(StartupMaintenanceMode.SkipSelectedPlugins))]
    [InlineData(StartupRecoveryAction.NormalStart, null)]
    [InlineData(StartupRecoveryAction.DisableSelectedAndStart, null)]
    [InlineData(StartupRecoveryAction.RunSetupWizard, null)]
    [InlineData(StartupRecoveryAction.Exit, null)]
    [InlineData((StartupRecoveryAction)int.MaxValue, null)]
    public void OnlyExplicitTemporaryPluginSkipActionsRequestRestart(StartupRecoveryAction action, string? expectedMode)
    {
        Assert.Equal(expectedMode, StartupMaintenanceController.GetRestartMode(action)?.ToString());
    }

    private static Window CreateTestWindow() => new()
    {
        Width = 320, Height = 200, Left = -10000, Top = -10000,
        WindowStartupLocation = WindowStartupLocation.Manual,
        ShowInTaskbar = false, ShowActivated = false, Opacity = 0, WindowStyle = WindowStyle.None
    };
}
